health-analyzer-window-diseases = [color=red]Обнаружено наличие заболевания![/color]
health-analyzer-window-disease-type-text = Генотип { $type }:
health-analyzer-window-disease-progress-text = Прогресс инфекции: { NATURALFIXED($progress, 4) }
health-analyzer-window-immunity-progress-text = Прогресс иммунитета: { NATURALFIXED($progress, 4) }
health-analyzer-wound-pain = Боль в области «{ $part }»: { $pain }.
health-analyzer-wound-part-summary = [bold]{ CAPITALIZE($part) }[/bold]
    { "    " }{ $details }
health-analyzer-wound-wounds = [color=#E6B566]Раны:[/color] { $wounds }
health-analyzer-wound-bleeding = [color=#E85D5D]Наружное кровотечение:[/color] { NATURALFIXED($rate, 2) } ед./с
health-analyzer-wound-internal-bleeding = [color=#D14545]Внутреннее кровотечение:[/color] { NATURALFIXED($rate, 2) } ед./с
health-analyzer-wound-scars-short = рубцы: { $count }
health-analyzer-wound-pain-diagnostic = [color=#D98CE8]Боль:[/color] { $pain }
health-analyzer-diagnostic-fracture-hairline = [color=#E6C766]Кость:[/color] трещина
health-analyzer-diagnostic-fracture-simple = [color=#E6A566]Кость:[/color] простой перелом
health-analyzer-diagnostic-fracture-displaced = [color=#E87855]Кость:[/color] перелом со смещением
health-analyzer-diagnostic-fracture-comminuted = [color=#E84A4A]Кость:[/color] оскольчатый перелом
health-analyzer-diagnostic-fracture-reduced = { $fracture } (вправлен)
health-analyzer-diagnostic-nerve-minor = [color=#E6C766]Нервы:[/color] слабое повреждение
health-analyzer-diagnostic-nerve-moderate = [color=#E68C55]Нервы:[/color] умеренное повреждение
health-analyzer-diagnostic-nerve-severe = [color=#E84A4A]Нервы:[/color] тяжёлое повреждение
health-analyzer-wound-functionality-impaired = [color=#E6A566]Функция:[/color] снижена
health-analyzer-wound-functionality-disabled = [color=#E84A4A]Функция:[/color] утрачена
health-analyzer-wound-functionality-unavailable = [color=#A0A0A0]Функция:[/color] часть отсутствует
health-analyzer-wound-clotting-inprogress = кровь сворачивается
health-analyzer-wound-clotting-complete = кровотечение остановлено
health-analyzer-wound-clotting-mixed = частичный гемостаз
health-analyzer-wound-no-findings-part = Патологий не выявлено: { $part }.
