#!/usr/bin/env python3
"""Run from the repo root: prepare [--shards N], then verify after successful CI workers."""

import argparse
from collections import Counter, defaultdict
import json
import math
import os
from pathlib import Path
import statistics
import subprocess
import sys
import xml.etree.ElementTree as ET

PLAN = Path('.ci-plan')
RESULTS = Path('test_results')
TIMINGS = Path('.ci-timings/timings.json')
SCHEMA = 2  # Timing samples are total test-case seconds per class.
HISTORY = 5
MAX_SHARDS = 16
DISCOVERY_TIMEOUT = 5 * 60
SUITES = {'integration': 'Content.IntegrationTests', 'unit': 'Content.Tests'}
TIMEOUTS = {'integration': 35 * 60, 'unit': 20 * 60}
DISABLED = ('Explicit', 'Ignored', 'Skipped')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def discovery(path):
    runs = list(ET.parse(path).getroot().iter('test-run'))
    require(len(runs) == 1, 'Expected one NUnit discovery run')
    cases = []

    def visit(node, inherited='Runnable'):
        state = inherited if inherited in DISABLED else node.get('runstate', inherited)
        require(state in ('Runnable', *DISABLED), f"Invalid discovery: {node.get('fullname')}: {state}")
        if node.tag == 'test-case':
            case = dict(id=node.get('id'), name=node.get('fullname'), state=state)
            case['class'] = node.get('classname')
            require(all(case.values()), 'Missing NUnit identity')
            cases.append(case)
        for child in node:
            if child.tag in ('test-suite', 'test-case'):
                visit(child, state)

    visit(runs[0])
    require(cases and len(cases) == int(runs[0].get('testcasecount', '-1')), 'Incomplete discovery')
    require(len({c['id'] for c in cases}) == len(cases), 'Duplicate NUnit ids')
    return cases


def load_timings():
    try:
        data = read_json(TIMINGS)
        require(data['schema'] == SCHEMA and isinstance(data['samples'], dict), 'Invalid timing schema')
        for values in data['samples'].values():
            require(isinstance(values, list) and 1 <= len(values) <= HISTORY, 'Invalid timing history')
            require(all(type(v) in (int, float) and math.isfinite(v) and v >= 0 for v in values), 'Invalid duration')
        return data['samples']
    except (OSError, ValueError, KeyError, TypeError, OverflowError) as error:
        print(f'Timing fallback (case counts): {error}', file=sys.stderr)
        return {}


def partition(cases, count, samples):
    require(type(count) is int and 1 <= count <= MAX_SHARDS, f'Shard count must be 1..{MAX_SHARDS}')
    counts = Counter(c['class'] for c in cases if c['state'] != 'Explicit')
    require(counts, 'No non-explicit tests')
    weights = {name: statistics.median(samples[name]) if name in samples else size for name, size in counts.items()}
    shards = [[] for _ in range(min(count, len(counts)))]
    loads = [0.0] * len(shards)
    for name in sorted(counts, key=lambda c: (-weights[c], c)):
        # The class-count tie break also handles zero-duration fixtures.
        index = min(range(len(shards)), key=lambda i: (loads[i], len(shards[i]), i))
        shards[index].append(name)
        loads[index] += weights[name]
    return shards


def settings(path, classes=None, strict=True):
    root = ET.Element('RunSettings')
    nunit = ET.SubElement(root, 'NUnit')
    values = {'RandomSeed': '12345', 'ConsoleOut': '0', 'ExplicitMode': 'None',
              'PreFilter': 'false', 'UseNUnitIdforTestCaseId': 'true', 'TestOutputXml': 'nunit'}
    if strict:
        values['MapWarningTo'] = 'Failed'
    if classes is None:
        values['DumpXmlTestDiscovery'] = 'true'
        values['WorkDirectory'] = str(Path.cwd())
    else:
        require(classes, 'Empty NUnit filter')
        quote = lambda s: "'" + s.replace('\\', '\\\\').replace("'", "\\'") + "'"
        values['Where'] = ' or '.join('class == ' + quote(c) for c in classes)
    for key, value in values.items():
        ET.SubElement(nunit, key).text = value
    path.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(root).write(path, encoding='utf-8', xml_declaration=True)


def prepare(count):
    samples = load_timings()
    matrix = []
    for suite, assembly in SUITES.items():
        dll = Path('bin') / assembly / (assembly + '.dll')
        config = PLAN / suite / 'discovery.runsettings'
        dump = dll.parent / 'Dump' / ('D_' + dll.name + '.dump')
        dump.unlink(missing_ok=True)
        settings(config)
        with (config.parent / 'discovery.log').open('w', encoding='utf-8') as log:
            subprocess.run(['dotnet', 'vstest', str(dll), '--ListTests', '--Settings:' + str(config)],
                           stdout=log, stderr=subprocess.STDOUT, check=True, timeout=DISCOVERY_TIMEOUT)
        (config.parent / 'discovery.xml').write_bytes(dump.read_bytes())
        cases = discovery(dump)
        shards = partition(cases, count if suite == 'integration' else 1, samples if suite == 'integration' else {})
        write_json(PLAN / (suite + '.json'), {'cases': cases, 'shards': shards})
        for index, classes in enumerate(shards):
            settings(PLAN / suite / f'{index}.runsettings', classes, strict=suite == 'integration')
            matrix.append(dict(suite=suite, assembly=assembly, shard=index, timeout=TIMEOUTS[suite]))
    write_json(PLAN / 'matrix.json', {'include': matrix})


def result_cases(expected, path, strict):
    root = ET.parse(path).getroot()
    actual = list(root.iter('test-case'))
    require(root.tag == 'test-run' and root.get('end-time'), 'Incomplete NUnit result')
    require(actual and len(actual) == int(root.get('total', '-1')), 'Incomplete case records')
    for node in root.iter():
        if node.tag in ('test-run', 'test-suite', 'test-case'):
            outcome = node.get('result')
            require(outcome in ('Passed', 'Skipped', 'Inconclusive', 'Warning'), f'Failed/incomplete result: {outcome}')
            require(not strict or (outcome != 'Warning' and node.get('label') != 'Warning'), 'Integration warning')
    seen = set()
    durations = defaultdict(float)
    for node in actual:
        identity = node.get('id')
        require(identity in expected and identity not in seen, f'Unexpected/duplicate case: {identity}')
        seen.add(identity)
        case = expected[identity]
        require(node.get('classname') == case['class'] and node.get('fullname') == case['name'], 'Discovery changed')
        outcome, label = node.get('result'), node.get('label')
        if case['state'] in DISABLED:
            expected_label = 'Explicit' if case['state'] == 'Explicit' else 'Ignored'
            require(outcome == 'Skipped' and label == expected_label, 'Disabled test executed')
        elif outcome in ('Skipped', 'Inconclusive'):
            require(node.find('reason') is not None and (outcome == 'Inconclusive' or label == 'Ignored'), 'Unexplained skip')
        elif outcome == 'Passed':
            duration = float(node.get('duration', 'nan'))
            require(math.isfinite(duration) and duration >= 0, 'Invalid duration')
            durations[case['class']] += duration
    missing = [c['name'] for i, c in expected.items() if i not in seen and c['state'] != 'Explicit']
    require(not missing, f'Missing tests: {missing[:5]}')
    require(durations or all(c['state'] in DISABLED for c in expected.values()), 'All runnable tests skipped')
    return durations


def aggregate(suite, directory):
    plan = read_json(PLAN / (suite + '.json'))
    require(plan['cases'] and plan['shards'], 'Empty test plan')
    assigned = [c for shard in plan['shards'] for c in shard]
    required = {c['class'] for c in plan['cases'] if c['state'] != 'Explicit'}
    require(set(assigned) == required and len(assigned) == len(required), 'Missing/duplicate planned classes')
    paths = [directory / str(i) / 'nunit' / (SUITES[suite] + '.xml') for i in range(len(plan['shards']))]
    require(set(directory.glob('*/nunit/*.xml')) == set(paths), 'Missing/extra shard reports')
    observed = {}
    for classes, path in zip(plan['shards'], paths):
        expected = {c['id']: c for c in plan['cases'] if c['class'] in classes}
        observed.update(result_cases(expected, path, strict=suite == 'integration'))
    return observed


def verify():
    # GitHub requires every worker to succeed before invoking this command.
    observed = aggregate('integration', RESULTS / 'integration')
    aggregate('unit', RESULTS / 'unit')
    previous = load_timings()
    samples = {name: (previous.get(name, []) + [value])[-HISTORY:] for name, value in observed.items()}
    write_json(TIMINGS, {'schema': SCHEMA, 'samples': samples})
    print('Complete NUnit coverage verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'verify'])
    parser.add_argument('--shards', type=int, default=os.environ.get('ONYX_TEST_SHARDS') or 4)
    args = parser.parse_args()
    try:
        require(1 <= args.shards <= MAX_SHARDS, f'Shard count must be 1..{MAX_SHARDS}')
        if args.command == 'prepare':
            prepare(args.shards)
        else:
            verify()
    except (ValueError, OSError, ET.ParseError, KeyError, TypeError, subprocess.SubprocessError) as error:
        sys.exit(f'Onyx CI failed: {error}')
