"""Regression tests for discovery, balancing and complete NUnit result accounting."""

from contextlib import chdir
import copy
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

import sharding as ci


def case(identity, fixture='Tests.A', name=None, state='Runnable'):
    return {'id': str(identity), 'class': fixture, 'name': name or fixture + '.Test', 'state': state}


def discovery_xml(cases):
    root = ET.Element('test-run', testcasecount=str(len(cases)))
    for c in cases:
        ET.SubElement(root, 'test-case', id=c['id'], classname=c['class'], fullname=c['name'], runstate=c['state'])
    return root


def result_xml(cases):
    root = ET.Element('test-run', result='Passed', total=str(len(cases)), **{'end-time': '2026-10-08'})
    suite = ET.SubElement(root, 'test-suite', result='Passed')
    for c in cases:
        node = ET.SubElement(suite, 'test-case', id=c['id'], classname=c['class'], fullname=c['name'],
                             result='Passed', duration='2.5')
        if c['state'] in ci.DISABLED:
            node.set('result', 'Skipped')
            node.set('label', 'Explicit' if c['state'] == 'Explicit' else 'Ignored')
    return root


class ShardingTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.enterContext(chdir(temporary.name))

    def xml(self, root, path=Path('result.xml')):
        path.parent.mkdir(parents=True, exist_ok=True)
        ET.ElementTree(root).write(path, encoding='utf-8')
        return path

    def check(self, cases, root=None, strict=True):
        return ci.result_cases({c['id']: c for c in cases}, self.xml(root if root is not None else result_xml(cases)), strict)

    def reports(self, suite, cases, count=1):
        plan = {'cases': cases, 'shards': ci.partition(cases, count, {})}
        ci.write_json(ci.PLAN / (suite + '.json'), plan)
        paths = []
        for i, classes in enumerate(plan['shards']):
            selected = [c for c in cases if c['class'] in classes]
            path = ci.RESULTS / suite / str(i) / 'nunit' / (ci.SUITES[suite] + '.xml')
            paths.append(self.xml(result_xml(selected), path))
        return plan, paths

    def test_native_discovery_preserves_inherited_nunit_states(self):
        root = ET.Element('NUnitXml')
        run = ET.SubElement(root, 'test-run', testcasecount='4')
        for i, state in enumerate(('Runnable', *ci.DISABLED)):
            fixture = ET.SubElement(run, 'test-suite', runstate=state)
            ET.SubElement(fixture, 'test-case', id=str(i), classname='Generic`1', fullname='custom name', runstate='Runnable')
        self.assertEqual(['Runnable', *ci.DISABLED], [c['state'] for c in ci.discovery(self.xml(root))])

    def test_invalid_or_incomplete_discovery_rejected(self):
        for problem in ('id', 'classname', 'fullname', 'count', 'NotRunnable', 'duplicate', 'empty'):
            with self.subTest(problem=problem):
                root = discovery_xml([case(1)])
                if problem in ('id', 'classname', 'fullname'):
                    del root[0].attrib[problem]
                elif problem == 'count':
                    root.set('testcasecount', '2')
                elif problem == 'NotRunnable':
                    root[0].set('runstate', problem)
                elif problem == 'duplicate':
                    root.append(copy.deepcopy(root[0]))
                    root.set('testcasecount', '2')
                else:
                    root.remove(root[0])
                with self.assertRaises(ValueError):
                    ci.discovery(self.xml(root))

    def test_parameterized_custom_and_identical_names_remain_separate(self):
        cases = [case(1, name='Same("a,b(quote\\\")")'), case(2, name='same custom name'),
                 case(3, name='same custom name'), case(4, 'Tests.B', 'same custom name')]
        self.assertEqual(4, len(ci.discovery(self.xml(discovery_xml(cases)))))
        self.assertEqual({'Tests.A': 7.5, 'Tests.B': 2.5}, self.check(cases))

    def test_lpt_balances_class_medians_and_keeps_classes_whole(self):
        cases = [case(i, 'Tests.' + name) for i, name in enumerate('AABC', 1)]
        samples = {'Tests.A': [10, 10, 500], 'Tests.B': [6], 'Tests.C': [6]}
        self.assertEqual([['Tests.A'], ['Tests.B', 'Tests.C']], ci.partition(cases, 2, samples))

    def test_no_statistics_caps_shards_and_excludes_explicit(self):
        cases = [case(1), case(2, 'Tests.B'), case(3, 'Manual', state='Explicit')]
        self.assertEqual([['Tests.A'], ['Tests.B']], ci.partition(cases, ci.MAX_SHARDS, {}))
        self.assertEqual(ci.partition(cases, 2, {}), ci.partition(cases[::-1], 2, {}))
        for count in (0, -1, ci.MAX_SHARDS + 1, True, 1.5):
            with self.assertRaises(ValueError):
                ci.partition(cases, count, {})
        with self.assertRaises(ValueError):
            ci.partition([case(1, state='Explicit')], 1, {})

    def test_zero_weights_never_create_empty_shards(self):
        cases = [case(i, 'Tests.' + name) for i, name in enumerate('ABCDE', 1)]
        samples = {c['class']: [0] for c in cases}
        for weight in (0, 10):
            samples['Tests.A'] = [weight]
            shards = ci.partition(cases, 3, samples)
            self.assertTrue(all(shards))
            self.assertEqual(5, sum(map(len, shards)))
            self.assertEqual(shards, ci.partition(cases[::-1], 3, samples))

    def test_bad_statistics_fall_back(self):
        self.assertEqual({}, ci.load_timings())
        for values in ([], [float('nan')], [float('inf')], [-1], [True], [10**400], [1] * (ci.HISTORY + 1)):
            ci.write_json(ci.TIMINGS, {'schema': ci.SCHEMA, 'samples': {'Tests.A': values}})
            self.assertEqual({}, ci.load_timings())
        ci.write_json(ci.TIMINGS, {'schema': ci.SCHEMA - 1, 'samples': {}})
        self.assertEqual({}, ci.load_timings())
        ci.write_json(ci.TIMINGS, {'schema': ci.SCHEMA, 'samples': {'Tests.A': [1, 2]}})
        self.assertEqual({'Tests.A': [1, 2]}, ci.load_timings())

    def test_prepare_creates_complete_matrix_and_exact_filters(self):
        cases = [case(1), case(2, 'Tests.B'), case(3, 'Generic`1')]

        def discover(command, **kwargs):
            dll = Path(command[2])
            self.xml(discovery_xml(cases), dll.parent / 'Dump' / ('D_' + dll.name + '.dump'))

        with patch.object(ci.subprocess, 'run', side_effect=discover):
            ci.prepare(2)
        matrix = ci.read_json(ci.PLAN / 'matrix.json')['include']
        self.assertEqual([('integration', 0), ('integration', 1), ('unit', 0)],
                         [(job['suite'], job['shard']) for job in matrix])
        for job in matrix:
            suite, index = job['suite'], job['shard']
            plan = ci.read_json(ci.PLAN / (suite + '.json'))
            config = ET.parse(ci.PLAN / suite / f'{index}.runsettings').getroot().find('NUnit')
            self.assertEqual(' or '.join("class == '" + c + "'" for c in plan['shards'][index]), config.findtext('Where'))
            self.assertEqual(ci.TIMEOUTS[suite], job['timeout'])
            self.assertEqual(ci.SUITES[suite], job['assembly'])

    def test_discovery_exit_or_timeout_cannot_produce_matrix(self):
        for error in (subprocess.CalledProcessError(1, 'dotnet'), subprocess.TimeoutExpired('dotnet', 1)):
            with patch.object(ci.subprocess, 'run', side_effect=error):
                with self.assertRaises(subprocess.SubprocessError):
                    ci.prepare(2)
            self.assertFalse((ci.PLAN / 'matrix.json').exists())

    def test_exact_escaped_filter_and_nunit_rules(self):
        config = Path('settings.xml')
        ci.settings(config, ["Quote'\\Name", 'Generic`1[X]'])
        nunit = ET.parse(config).getroot().find('NUnit')
        self.assertEqual("class == 'Quote\\'\\\\Name' or class == 'Generic`1[X]'", nunit.findtext('Where'))
        for key, value in {'ExplicitMode': 'None', 'PreFilter': 'false', 'MapWarningTo': 'Failed',
                           'UseNUnitIdforTestCaseId': 'true', 'TestOutputXml': 'nunit'}.items():
            self.assertEqual(value, nunit.findtext(key))
        with self.assertRaises(ValueError):
            ci.settings(config, [])

    def test_missing_extra_duplicate_changed_and_unfinished_results_fail(self):
        cases = [case(1), case(2)]
        for problem in ('missing', 'extra', 'duplicate', 'classname', 'fullname', 'end-time', 'total'):
            with self.subTest(problem=problem):
                root = result_xml(cases)
                suite = root[0]
                if problem == 'missing':
                    suite.remove(suite[-1])
                    root.set('total', '1')
                elif problem in ('extra', 'duplicate'):
                    node = copy.deepcopy(suite[0])
                    if problem == 'extra':
                        node.set('id', '999')
                    suite.append(node)
                    root.set('total', '3')
                elif problem in ('classname', 'fullname'):
                    suite[0].set(problem, 'changed')
                elif problem == 'end-time':
                    del root.attrib['end-time']
                else:
                    root.set('total', '99')
                with self.assertRaises(ValueError):
                    self.check(cases, root)

    def test_failed_case_setup_or_teardown_fails(self):
        for tag in ('test-run', 'test-suite', 'test-case'):
            root = result_xml([case(1)])
            next(root.iter(tag)).set('result', 'Failed')
            with self.assertRaises(ValueError):
                self.check([case(1)], root)

    def test_static_and_dynamic_skips_and_optional_explicit_records(self):
        cases = [case(1), case(2, state='Ignored'), case(3, state='Explicit'), case(4)]
        root = result_xml(cases)
        node = root[0][-1]
        node.set('result', 'Skipped')
        node.set('label', 'Ignored')
        ET.SubElement(node, 'reason')
        self.assertEqual({'Tests.A': 2.5}, self.check(cases, root))
        root[0].remove(root[0][2])
        root.set('total', '3')
        self.check(cases, root)
        self.assertEqual({}, self.check([case(1, state='Ignored')]))

    def test_disabled_execution_and_unexplained_or_all_dynamic_skips_fail(self):
        for state in ci.DISABLED:
            with self.assertRaises(ValueError):
                self.check([case(1, state=state)], result_xml([case(1)]))
        for outcome in ('Skipped', 'Inconclusive'):
            root = result_xml([case(1)])
            node = root[0][0]
            node.set('result', outcome)
            node.set('label', 'Ignored')
            with self.assertRaises(ValueError):
                self.check([case(1)], root)
            ET.SubElement(node, 'reason')
            with self.assertRaises(ValueError):
                self.check([case(1)], root)

    def test_invalid_durations_and_integration_warnings_fail(self):
        for value in ('NaN', 'Infinity', '-1', 'text'):
            root = result_xml([case(1)])
            root[0][0].set('duration', value)
            with self.assertRaises(ValueError):
                self.check([case(1)], root)
        root = result_xml([case(1)])
        root.set('label', 'Warning')
        with self.assertRaises(ValueError):
            self.check([case(1)], root)
        self.check([case(1)], root, strict=False)

    def test_aggregate_requires_every_report_and_rejects_duplicates(self):
        plan, paths = self.reports('integration', [case(1), case(2, 'Tests.B')], 2)
        directory = ci.RESULTS / 'integration'
        self.assertEqual({'Tests.A': 2.5, 'Tests.B': 2.5}, ci.aggregate('integration', directory))
        extra = paths[0].with_name('extra.xml')
        extra.write_bytes(paths[0].read_bytes())
        with self.assertRaises(ValueError):
            ci.aggregate('integration', directory)
        extra.unlink()
        paths[1].unlink()
        with self.assertRaises(ValueError):
            ci.aggregate('integration', directory)

    def test_overlapping_or_missing_class_assignments_fail(self):
        plan, _ = self.reports('integration', [case(1), case(2, 'Tests.B')], 2)
        for classes in (plan['shards'][0], [], ['Unknown']):
            plan['shards'][1] = classes
            ci.write_json(ci.PLAN / 'integration.json', plan)
            with self.assertRaises(ValueError):
                ci.aggregate('integration', ci.RESULTS / 'integration')

    def test_truncated_xml_cannot_publish_statistics(self):
        self.reports('integration', [case(1)])
        _, unit_paths = self.reports('unit', [case(1)])
        ci.write_json(ci.TIMINGS, {'schema': ci.SCHEMA, 'samples': {'Tests.A': [1]}})
        previous = ci.TIMINGS.read_bytes()
        unit_paths[0].write_text('<test-run>')
        with self.assertRaises(ET.ParseError):
            ci.verify()
        self.assertEqual(previous, ci.TIMINGS.read_bytes())

    def test_success_updates_bounded_history_and_drops_retired_classes(self):
        self.reports('integration', [case(1)])
        self.reports('unit', [case(1)])
        ci.write_json(ci.TIMINGS, {'schema': ci.SCHEMA, 'samples': {'Tests.A': [1] * ci.HISTORY, 'retired': [50]}})
        ci.verify()
        self.assertEqual({'Tests.A': [1] * (ci.HISTORY - 1) + [2.5]}, ci.load_timings())


if __name__ == '__main__':
    unittest.main()
