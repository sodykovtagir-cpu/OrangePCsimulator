# Кастомная крышка в коробке и пароль Wi-Fi звёздочками

Две правки: доставка заказной крышки/стекла и маскирование поля пароля Wi-Fi.
Обе не требуют ручных действий в редакторе Unity — всё уже размечено в префабах
и подхватывается кодом.

## 1. Кастомная крышка приезжает в коробке

### Что было

`ModForge.Purchase()` списывал деньги и сразу вызывал
`Main.Instance.InstantDelivery(prefab)`: крышка (или стекло с рисунком)
появлялась из «портала» готовая, без коробки. Остальные покупки в игре едут
через очередь доставки и коробку — заказная крышка выбивалась из этого ряда.

Проблема глубже, чем «поставить коробку»: коробка выкладывает содержимое в
`Start()` (то есть почти сразу после `Instantiate`), а рисунок покупателя
наносится **после** создания экземпляра. Если сделать «в лоб», игрок получил бы
крышку без картинки или картинку «мимо» предмета.

### Как сделано

```
ModForge.Purchase
  └─ Spawn(prefab, texture)
       └─ Main.DeliverCustom(itemPrefab, boxPrefab, configure)   // рисунок — в configure
            ├─ Box.CustomDelivery = фабрика предмета   (очередь заказов)
            ├─ Main.Delivered → событие Spawned         (любая доставка)
            └─ DeliverPackage(boxPrefab)                // коробка едет как обычная покупка
                 └─ через 5 с: коробка у двери
                      ├─ Spawned → Box.ReleaseFrom(configure)
                      │     ├─ предмет создан (Instantiate itemPrefab)
                      │     ├─ CustomPaint.SetTexture(texture, bytes)   // рисунок уже на нём
                      │     └─ предмет выложен из коробки
                      └─ позже Start() коробки: уже выложено → молчит
```

- **`Box`** (`Assets/Scripts/Assembly-CSharp/Box.cs`): появились
  `CustomDelivery` (отложенное содержимое), `NeedsContent` (коробка без зашитого
  `prefab`, т.е. `Box_CustomCover`), `Pending`, `ReleaseFrom(configure)` и
  `Release`. `Start()` пропускает работу, если предмет уже выложен, и **не
  трогает** обычные коробки, у которых есть свой `prefab`.
- **`Main`** (`Main.cs`): `DeliverCustom(...)`, `DeliverPackage(...)`, событие
  `Spawned` (срабатывает на **любой** доставке — и мгновенной, и через очередь),
  очередь `customItems`/`customConfigures`, чтобы несколько заказов подряд не
  путали рисунки. При `Box.Restoring` (загрузка сейва) ничего не спавнится —
  содержимое уже восстановлено отдельной записью.
- **`ModForge`**: `caseBoxPrefab` (поле в инспекторе, уже привязано в
  `Assets/GameObject/ModForge.prefab` к `Box_CustomCover.prefab`), при пустом
  поле коробка грузится из `Resources/Box_CustomCover`. Если коробки нет вообще,
  заказ не теряется: деньги возвращаются (`m.AddMoney(price)`).
- **`CustomPaint`**: `Data`/`HasDesign`, безопасное применение текстуры
  (рендерер без материала больше не роняет игру, рисунок всё равно запоминается
  для сейва).
- **Новый префаб** `Assets/GameObject/Box_CustomCover.prefab` (guid
  `45f4ee7830e9463889927add6b7b4dd9`) собран из коробки проекта: тег `Box`,
  коллайдеры, `Rigidbody`, две петли, пустой `prefab` (содержимое задаёт код).

### Что проверить в игре

1. ModForge → выбрать `.pic` → купить крышку.
2. У двери приезжает **коробка** (звонок + звук), а не готовая крышка в воздухе.
3. Открыть коробку — внутри крышка/стекло с выбранным рисунком.
4. Купить две крышки подряд — у каждой свой рисунок.
5. Сохраниться, перезагрузиться — дубликата крышки нет.

## 2. Пароль Wi-Fi скрыт звёздочками

### Что было

В `Assets/Scripts/Assembly-CSharp/PC/Component/WifiPanelPrefab.prefab` поле
пароля стояло со `m_ContentType: 0` (Standard) и `m_InputType: 0`, поэтому
символы были видны открытым текстом. В коде тип ставился только внутри
`Initialize()`, и любое другое состояние поля оставалось открытым.

### Как сделано

- Префаб: `m_ContentType: 7` (`ContentType.Password`) + `m_InputType: 2`
  (`InputType.Password`) + `m_AsteriskChar: 42` (`*`).
- `WifiNetworkEntry`: `Awake()` и `PreparePasswordField()` принудительно ставят
  `contentType = Password`, `asteriskChar = '*'` и `ForceLabelUpdate()` — до
  `Initialize`, при показе поля ввода и после ввода пароля.
- `RouterWebPages` (сайт роутера): общий помощник `Secure(InputField)` для обоих
  полей пароля — «Новый пароль Wi-Fi» и «Пароль основной Wi-Fi сети для
  повторителя».

### Важно про `InputType`

`ContentType.Password` (7) маскирует ввод, но `InputType` при этом должен
оставаться `Password` (2) или `Standard` (0). `InputType.Pin` (8) — это поле
PIN-кода по центру, звёздочек оно не рисует.

## Тесты

```bash
python3 tools/test_custom_cover_delivery.py   # 42 проверки, компилирует настоящие Box.cs и CustomPaint.cs
python3 tools/test_wifi_taskbar.py            # контракты Wi-Fi панели по-прежнему зелёные
```

`tools/test_custom_cover_delivery.py` реально прогоняет путь доставки
(заказ → коробка → предмет с рисунком), проверяет два заказа подряд, тишину при
загрузке сейва, обычные коробки, а также исходники и ассеты.
