# Исправления отсутствующих literal Loc.GetString ID.
# Держатся в _Hex, чтобы не менять ресурсы внешних модулей.

## Общий интерфейс

Station = Станция
View = Просмотр
view-variables = Просмотр переменных
cmd-parse-failure-int = Не удалось распознать «{ $arg }» как целое число.
silicon-law-print-error = Не удалось напечатать закон.
time-transfer-panel-warning-no-perms = У вас нет прав на перенос времени.

## Настройки, административные команды и чат

panicbunker-command-overall-minutes-age-set = Минимальный возраст аккаунта установлен на { $minutes } мин.
chat-manager-language-prefix = [{ $language }]
cmd-dungen_pack_vis = Визуализация пакета комнат подземелья завершена.
command-whitelistadd-added = Игрок { $username } добавлен в белый список.
command-whitelistremove-removed = Игрок { $username } удалён из белого списка.
whitelist-not-whitelisted = Вы не состоите в белом списке этого сервера.
cmd-ghostrolewhitelist-ghost-role-does-not-exist = Роль призрака «{ $ghost-role }» не существует.
cmd-ghostrolewhitelist-already-whitelisted = Игрок { $player } уже добавлен в белый список роли { $ghostRoleName } ({ $ghostRoleId }).
cmd-ghostrolewhitelist-hint-job = Введите идентификатор роли призрака.
cmd-ghostrolewhitelist-job-does-not-exist = Роль призрака «{ $ghostRole }» не существует.

## Предметы, взаимодействия и уведомления

armor-plate-item-durability = Прочность: [color={$durabilityColor}]{$percent}%[/color]
battery-examinable-verb-text = Проверить защиту от ЭМИ
bin-component-on-examine-text = Внутри находится предметов: { $count }.
book-read-verb = Читать
of-holding-warn = Вы чувствуете, как реальность вокруг вас искажается!
strippable-component-item-slot-occupied = Слот уже занят: { $owner }.
gun-damage-modifier-examine = Выстрелы наносят [color={$color}]{$damage}x[/color] урона.

## Автоматическая регенерация реагентов

autoreagent-switch = Переключить регенерируемый реагент
autoregen-switched = Регенерируемый реагент переключён на { $reagent }.

## Артефактный дробитель

artifact-crusher-autolocks-enable = Замки машины с лязгом закрываются!
artifact-crusher-examine-autolocks = Автоматические замки машины [color=red]включены[/color].
artifact-crusher-examine-no-autolocks = Автоматические замки машины [color=green]выключены[/color].

## Оружейные модификаторы

es-gun-attachments-inspect-modifier-recovery = Влияет на восстановление отдачи: [bold][color={$color}]{NATURALFIXED($modifier, 2)}x[/color][/bold].
es-gun-attachments-inspect-modifier-recoil = Влияет на увеличение отдачи: [bold][color={$color}]{NATURALFIXED($modifier, 2)}x[/color][/bold].
es-gun-attachments-inspect-modifier-minspread = Влияет на минимальный разброс: [bold][color={$color}]{NATURALFIXED($modifier, 2)}x[/color][/bold].
es-gun-attachments-inspect-modifier-maxspread = Влияет на максимальный разброс: [bold][color={$color}]{NATURALFIXED($modifier, 2)}x[/color][/bold].

## Карты, экспедиции и экономика

discord-round-unknown-map = неизвестная карта
salvage-expedition-mission-completed = Миссия экспедиции выполнена!
salvage-expedition-mission-failed = Миссия экспедиции провалена.
bank-withdraw-failed = Не удалось снять средства со счёта станции.
station-id = Идентификатор станции
bounty-contracts-ui-create-error-vessel-name-too-long = Название судна слишком длинное.

## Контейнеры

stack-holder-empty = Контейнер пуст.
stack-holder = В контейнере: { $number } × { $item }.

## Интерфейс движка и отладка

discord-rpc-in-main-menu-logo-text = Похоже, Кулсвилль — отстой
color-selector-input-hex = Шестнадцатеричный
color-white = белый
option-button-filter = Фильтр
popup-copy-button = Копировать
popup-title = Внимание!
dev-window-tab-render-targets-title = Цели рендеринга
dev-window-tab-textures-title = Текстуры
vv-sound-collection = Коллекция
vv-sound-loop = Зациклить
vv-sound-max-distance = Максимальная дистанция
vv-sound-none = Нет
vv-sound-path = Путь
vv-sound-pitch = Высота тона
vv-sound-play-offset = Смещение воспроизведения (с)
vv-sound-reference-distance = Эталонная дистанция
vv-sound-rolloff-factor = Коэффициент затухания
vv-sound-variation = Изменение высоты тона
vv-sound-volume = Громкость

## Команды движка

cmd-cvar_subs-arg-name = <имя>
cmd-cvar_subs-invalid-args = Необходимо указать ровно один аргумент.
cmd-merge_grids-angle = [Угол]
cmd-merge_grids-hintA = Сетка A
cmd-merge_grids-hintB = Сетка B
cmd-merge_grids-xOffset = Смещение по X
cmd-merge_grids-yOffset = Смещение по Y
cmd-parse-failure-cultureinfo = «{$arg}» не является допустимым CultureInfo.
cmd-parse-failure-enum = {$arg} не является перечислением {$enum}.
cmd-parse-failure-grid = {$arg} не является допустимой сеткой.
cmd-parse-failure-session = Сессия пользователя {$username} не найдена.
cmd-pvs-override-info-clients = У сущности {$nuid} есть переопределение сессии для {$clients}.
cmd-pvs-override-info-desc = Выводит сведения о переопределениях PVS, связанных с сущностью.
cmd-pvs-override-info-empty = У сущности {$nuid} нет переопределений PVS.
cmd-pvs-override-info-global = У сущности {$nuid} есть глобальное переопределение.
cmd-savemap-error = Не удалось сохранить карту. Подробности смотрите в журнале сервера.

## Эффекты реагентов

reagent-effect-status-effect-ClawsGrowthSuppression = подавление роста когтей
reagent-effect-status-effect-CorticalBorerProtection = защита от кортикального буравчика
