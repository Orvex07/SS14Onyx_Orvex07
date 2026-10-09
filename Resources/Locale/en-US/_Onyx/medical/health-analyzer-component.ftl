health-analyzer-window-diseases = [color=red]Disease presence detected![/color]
health-analyzer-window-entity-bleeding-text = [color=#E85D5D]Bleeding![/color]
health-analyzer-window-disease-type-text = Genotype {$type}:
health-analyzer-window-disease-progress-text = Infection Progress: {NATURALFIXED($progress, 4)}
health-analyzer-window-immunity-progress-text = Immunity Progress: {NATURALFIXED($progress, 4)}
health-analyzer-wound-pain = Pain at { $part }: { $pain }.
health-analyzer-wound-part-summary = [bold]{ CAPITALIZE($part) }[/bold]
    { "    " }{ $details }
health-analyzer-wound-wounds = [color=#E6B566]Wounds:[/color] { $wounds }
health-analyzer-wound-bleeding = [color=#E85D5D]External bleeding:[/color] { NATURALFIXED($rate, 2) } u/s
health-analyzer-wound-internal-bleeding = [color=#D14545]Internal bleeding:[/color] { NATURALFIXED($rate, 2) } u/s
health-analyzer-wound-scars-short = scars: { $count }
health-analyzer-wound-pain-diagnostic = [color=#D98CE8]Pain:[/color] { $pain }
health-analyzer-diagnostic-fracture-hairline = [color=#E6C766]Bone trauma:[/color] hairline fracture
health-analyzer-diagnostic-fracture-simple = [color=#E6A566]Bone trauma:[/color] simple fracture
health-analyzer-diagnostic-fracture-displaced = [color=#E87855]Bone trauma:[/color] displaced fracture
health-analyzer-diagnostic-fracture-comminuted = [color=#E84A4A]Bone trauma:[/color] comminuted fracture
health-analyzer-diagnostic-fracture-reduced = { $fracture } (reduced)
health-analyzer-diagnostic-nerve-minor = [color=#E6C766]Nerves:[/color] minor damage
health-analyzer-diagnostic-nerve-moderate = [color=#E68C55]Nerves:[/color] moderate damage
health-analyzer-diagnostic-nerve-severe = [color=#E84A4A]Nerves:[/color] severe damage
health-analyzer-wound-functionality-impaired = [color=#E6A566]Function:[/color] reduced
health-analyzer-wound-functionality-disabled = [color=#E84A4A]Function:[/color] lost
health-analyzer-wound-functionality-unavailable = [color=#A0A0A0]Function:[/color] part absent
health-analyzer-wound-clotting-inprogress = clotting in progress
health-analyzer-wound-clotting-complete = bleeding stopped
health-analyzer-wound-clotting-mixed = partial hemostasis
health-analyzer-wound-no-findings-part = No findings: { $part }.
