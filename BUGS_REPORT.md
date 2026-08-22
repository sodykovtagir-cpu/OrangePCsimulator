# Отчёт: аудит багов и недоработок — Orange PC Simulator

**Дата аудита:** 22.08.2026
**Ветка:** `main`
**Unity:** 2022.3.51f1
**Проверено:** 282 C#-файла (~35 000 строк), 10 сцен, мастерская (workshop), сохранения, настройки, VR, магазин.

---

## 1. Исправлено и запушено ✅

Коммит `0d2d2cf` (10 файлов). Всё, что ниже — точечные, безопасные правки без изменения геймплея.

| # | Файл | Что было | Что стало |
|---|------|----------|-----------|
| 1 | `Main.cs` | `using UnityEditor;` без `#if` — **не компилируется ни одна player-сборка** (Android/iOS/WebGL/Desktop) | Убрано. Проект снова собирается в билды |
| 2 | `Main.cs` `AddItem/GetNewId` | `items.Add()` кидал `ArgumentException` при дубликате ID; `Random.Range(int.MinValue, int.MaxValue)` — переполнение `max-min` (известный баг Unity: `Range(1, int.MaxValue)` даёт отрицательные значения), риск схлопывания диапазона и бесконечного цикла в `do/while`; мёртвая переменная `id2` | `items[id] = item` (индексатор); ID теперь от `Guid.GetHashCode()` с защитой от 0; мёртвый код убран |
| 3 | `Main.cs` `ExitVirtualWorld` | `throw new InvalidOperationException()`, если `Player.Instance == null` — краш при выходе из VR | `Debug.LogWarning` + аккуратный `return` |
| 4 | `MainMenu.cs` `LoadExample` | `while (!op.isDone) { }` — блокировка главного потока: **на WebGL запрос никогда не завершится (вечный фриз), на Android — ANR**; плюс NRE при `fileContents == null` | Корутина с `yield return` + проверка на пустой результат |
| 5 | `CoinPanel.cs` | 5× `throw new InvalidOperationException()` в «страховках» — **краш панели монет**, если поле не назначено в инспекторе; `float.Parse` — **краш на нечисловом вводе**, в русской локали `"1.5"` парсится как 15 | Возвраты вместо throw; `float.TryParse` + `InvariantCulture` |
| 6 | `CheatingDetector.cs` | `CheatDetected.Invoke()` без подписчиков → NRE | `CheatDetected?.Invoke()` |
| 7 | `Item.cs` `FromData` | `jObject.TryGetValue("glue", out var val); glue = val.Value<bool>();` — **NRE при загрузке старых сейвов** без поля `glue` (считалось как «N предметов не загрузилось») | Null-безопасное чтение |
| 8 | `WorkshopClient.cs` `DeleteSave/Like` | `JsonUtility.FromJson` без `try/catch` — если сервер вернул HTML (анти-бот страница), необработанное исключение | Обработка ошибки с передачей в колбэк |
| 9 | `FileInformation.cs` | `#if UNITY_STANDALONE || UNITY_EDITOR` + `using UnityEditor` + `EditorUtility` — **standalone-сборка не компилируется** | `EditorUtility` только под `UNITY_EDITOR`; в билдах — `NativeFilePicker` |
| 10 | `SceneSettings.cs` | NRE в `OnDestroy`, если `inputName == null`; compiled-регэксп пересоздавался **на каждый вызов** `CheckName` | Null-guard; регэксп кэшируется один раз |
| 11 | `Terminal.cs` | `int.Parse`/`float.Parse`/`Convert.ToInt32(hex,16)` на вводе игрока — команда `delay abc` роняла корутину терминала | `TryParse` + обработка `FormatException` |
| 12 | `AdManager.cs` | `NoAds` никогда не читался из PlayerPrefs — флаг «Без рекламы» не работал; `RemoveAds()` не сохранял состояние | Чтение в `Awake`, установка и сохранение в `RemoveAds` |

---

## 2. Критичные баги — требуют решений по продукту (не тронуты)

### 2.1. Реклама полностью не реализована ⚠️ (самое важное)
`AdManager.cs` — **заглушка**: все методы (`RequestBanner`, `ShowInterstitial`, `CreateAndLoadRewardedAd` и др.) пустые, а событие `EarnedReward` имеет пустые `add/remove` — **не срабатывает никогда**.
- Кнопка «Бесплатные монеты» (`CoinPanel.FreeCoins`): текст навсегда «Loading...», кнопка навсегда неактивна.
- Вместо реального Google Mobile Ads в проекте лежит **фейковый стаб** `Assets/Scripts/FakeGoogleMobileAds/GMA.cs` (там `Reward.Type` — `int`, а код сравнивает `reward.Type == "Coin"` — всегда `false`).
- `Quiz`/блокировка рекламы (`NoAds`) не имеют смысла без самой рекламы.

**Что делать:** подключить реальный пакет Google Mobile Ads (или AdMob + mediation), реализовать методы в `AdManager`, убрать фейковый стаб, пересобрать `Reward`-логику.

### 2.2. Промокоды зашиты в клиент 🚨
`Giveaway.cs` содержит рабочие промокоды, включая `Testertest09009990122` → **1 000 000$ + 10 000 BTC**. Код лежит в открытом репозитории — любой может «заклеймить» и затем обойти защиту, удалив PlayerPrefs (`Giveaway`). Это ломает экономику игры.
**Что делать:** перенести промокоды на сервер (проверка + одноразовое использование по клиенту), либо хотя бы убрать крупные бонусы и добавить серверную метку.

### 2.3. Сохранения «шифруются» XOR с константой 129
`SaveUtility.EncryptDecrypt` — XOR-«шифрование» с фиксированным ключом. Файлы `.opc` тривиально редактируются, плюс любой читер может подделать `coin`, `hardcore`, `playtime`. Для инди-игры допустимо, но:
- минимум — уникальный ключ на устройство + контрольная сумма/HMAC содержимого;
- критичные поля (монеты, биткоины) — дублировать с проверкой.

### 2.4. Гравитация игрока: неаккуратный расчёт в `Player.FixedUpdate`
- `playerDirection.y` не обнуляется перед новым кадром: при падении скорость стартует с −9.8 м/с и копится квадратично;
- на земле контроллер каждый кадр «вдавливается» в пол (`motion.y = gravity * dt`), что на склонах даёт проскальзывание.
Классический паттерн: `velocity.y = isGrounded ? -2 : velocity.y - g*dt`, затем `controller.Move(velocity*dt)`. Требует ручной проверки ощущений прыжка.

### 2.5. Словарь предметов `Main.items` не чистится
`SaveManager.LoadData` уничтожает стартовые предметы (`Destroy`), но их ID остаются в `Main.Instance.items` — при повторных загрузках словарь растёт, ID «зомби» висят в памяти. После фикса #2 краха больше нет, но стоит добавить `Main.RemoveItem(id)` при уничтожении.

### 2.6. `WorkshopClient`: сертификаты и анти-бот обход
- `AcceptAllCerts` принимает **любой** сертификат — MITM-уязвимость (для byethost сомнительный сертификат — понятно, но лучше whitelist-хост или нормальный домен).
- Обход анти-бот cookie `__test` (AES-дешифровка `toNumbers`) — хрупко: при смене схемы защиты byethost **мастерская полностью отвалится**. Плюс это может нарушать условия хостинга.
- `UploadKey` (статик) нигде не присваивается — мёртвый код; фактическое управление ключами идёт через `WorkshopLocal` (ок).

---

## 3. Недоработки и риски (средний приоритет)

| Файл | Проблема |
|------|----------|
| `MainMenu.cs` `LoadAsync` | `menuManager.ShowMenu("Loading")` без null-проверки (NRE, если не назначен) |
| `VirtualWorld.cs` `AddBitcoin` | `CloudOnceManager.Instance` без `?.` — NRE, если менеджер отсутствует в сцене |
| `CloudOnceManager.cs` / `FakeCloudOnce` | Достижения/лидерборды — **локальные заглушки на PlayerPrefs**: не синхронизируются, накручиваются удалением PlayerPrefs. `long.Parse` по PlayerPrefs может упасть на мусоре |
| `CheatingDetector` / `IntShadow`/`FloatShadow` | Соль `value ^ Environment.TickCount` предсказуема; проверка только двух полей обходится правкой обоих; `BitcoinManager.OnCheaterDetected` → `Application.Quit()` (неприятно, но намеренно) |
| `WorkshopMenu.OnEnable` | Сетевой запрос `RefreshList()` при **каждом** открытии панели; при офлайне — ошибка на каждый показ. Лучше лениво один раз + кнопка обновления |
| `WorkshopLocal.Norm` | `Path.GetFullPath` вне try — бросит на невалидном пути |
| `FileMenu.Import` | На WebGL `NativeFilePicker` тихо ничего не делает (импорт недоступен); текст ошибки «версия 1.7.0+» захардкожен; `slotParent` без null-проверки |
| `SaveUtility.GetTextFromStreamingAssets` | Блокирует поток через `WaitOne()` — на WebGL запрещено; сейчас не используется, но при включении — фриз |
| `SceneSettings.Create` | Имя файла из `inputName` без `.Trim()` — сейв « » (пробел) создаёт файл ` .opc` |
| `CoinPanel.EarnCoins` | `source.PlayOneShot` без null-проверки (source — с `RequireComponent`, ок, но если объект выключен...) |
| `Main.Update` | `Input.GetButtonDown("Cancel")` не работает на мобильных (нет кнопки Cancel в Input Manager) |
| `LockFps.cs` | `Update()` каждым кадром перепроверяет `targetFrameRate` — достаточно один раз в `Start` |
| `PcKeybinds` / `InputManager.PcInput` | `public const bool PcInput = true;` — не используется (мёртвая константа) |

---

## 4. Мелочи / чистка

- **Неиспользуемые ассеты** (можно удалить, ~десятки МБ): `Assets/Google Sheets to Unity/`, `Assets/Plugins/WebViewObject/` — ни один игровой скрипт их не использует.
- `WorkshopMenu.LoadCover` — корутина-мёртвый код (обложки грузятся через `WorkshopClient.DownloadCover`).
- `FakeCloudOnce`, `FakeGoogleMobileAds` — заглушки, живущие «пока не подключён реальный SDK».
- `FileMenu.cs` — кривое форматирование (смесь табов/пробелов) — стоит прогнать форматтер.
- `Assets/Editor/PreventPlayWithMissingScripts.cs` — полезный стражник: реальных missing scripts в сценах/префабах **не найдено** (найденные GUID — встроенные uGUI-компоненты, это норма).
- Секретов/токенов в репозитории не обнаружено.

---

## 5. Рекомендуемый план (приоритеты)

1. **Сборка:** убедиться, что после фикса #1 проект собирается на целевые платформы (Android/iOS/WebGL).
2. **Ads:** подключить реальный SDK и реализовать `AdManager` (разблокирует «Бесплатные монеты» и Quiz-рекламу).
3. **Экономика:** убрать/перенести промокоды на сервер; усилить защиту сейвов (раздел 2.3).
4. **Геймплей:** проверить ощущения прыжка после правки `Player.FixedUpdate` (раздел 2.4); почистить `Main.items`.
5. **Мастерская:** решить вопрос с byethost/сертификатами (раздел 2.6), добавить ретраи и обработку офлайна.
6. **Чистка:** удалить неиспользуемые ассеты и мёртвый код, прогнать форматтер.

---

*Отчёт составлен автоматически по результатам статического аудита; правки из раздела 1 запушены в `main` (коммит `0d2d2cf`). Пункты разделов 2–4 требуют ручного тестирования и продуктовых решений.*
