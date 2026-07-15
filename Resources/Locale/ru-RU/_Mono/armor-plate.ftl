armor-plate-break = Your { $plateName } has shattered!
armor-plate-examine-with-plate = Has a [color=yellow]{ $plateName }[/color] installed. Durability: [color={ $durabilityColor }]{ $percent }%[/color]
armor-plate-examine-with-plate-simple = Has a [color=yellow]{ $plateName }[/color] installed.
armor-plate-examine-no-plate = No armor plate installed.
armor-plate-examine-no-storage = No storage compartment for armor plates.

armor-plate-gait-sprint = скорость бега

armor-plate-ratios-display = { $deltasign ->
    [-1] [color=cyan]Поглощает[/color] [color=yellow]{$ratioPercent}%[/color] от [color=yellow]{$dmgType}[/color] и принимает как [color=yellow]x{$multiplier}[/color] урона прочности.
    [0] Не подвержен воздействию {$dmgType}
    [1] [color=fuchsia]Усиливает[/color] [color=yellow]{$dmgType}[/color] на [color=yellow]{$ratioPercent}%[/color] и принимает дополнительный урон как [color=yellow]x{$multiplier}[/color] урона прочности.
    *[other] {$dmgType} не должно иметь такое значение поглощения!
    }

armor-plate-attributes-examine = Эта бронепластина:

armor-plate-examinable-verb-message = Осмотреть характеристики защиты и прочности.

armor-plate-examinable-verb-text = Характеристики пластины

armor-plate-gait-speed = скорость

armor-plate-gait-walk = скорость ходьбы

armor-plate-initial-durability = Рассчитана на [color=yellow]{ $durability }[/color] стандартных единиц урона.

armor-plate-speed-display = { $deltasign ->
    [-1] Увеличивает вашу {$gait} на [color=yellow]{$speedPercent}%[/color].
    [0] Не влияет на вашу скорость.
    [1] Уменьшает вашу {$gait} на [color=yellow]{$speedPercent}%[/color].
    *[other] Не должно быть такого значения скорости!
    }

armor-plate-stamina-value = Наносит [color=yellow]{$multiplier}%[/color] от поглощённого урона как урон выносливости.
