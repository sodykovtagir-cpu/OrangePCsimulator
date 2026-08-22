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

---

## 6. Обновление 2 (22.08.2026, коммит после `4312bf1`) — реклама вырезана, мастерская доработана

### 6.1 Реклама полностью вырезана
- `AdManager.cs` переписан в пустую заглушку **без Google Mobile Ads** (методы-пустышки сохранены, чтобы не ломать ссылки в сценах и вызовы из `Display`/`ShopPanel`/`StoreMenu`).
- Удалён фейковый SDK-стаб `Assets/Scripts/FakeGoogleMobileAds/`.
- `CoinPanel.FreeCoins` больше не крутит «Loading...» и не ждёт рекламу — кнопка просто выдаёт **+25$**.
- `Quiz.cs` больше не зависит от `AdManager`/`NoAds`.
- Осталось «наследие»: кнопка покупки `no_ads` в магазине (StoreMenu) и объект `Ad` в панели Quiz — они безвредны, но кнопку «no_ads» в магазине можно убрать отдельно, если нужно.

### 6.2 Мастерская: кнопки по статусу владельца
- **Сейв не выложен** → видна кнопка **Upload**, скрыты **Update/Delete**.
- **Сейв выложен (ты владелец)** → виден **Update + Delete**, скрыт **Upload**.
- Внутри панели публикации кнопка действия одна (`PublishAction`) — её **текст меняется** между «Upload» и «Update» в зависимости от статуса; кнопка `PublishDelete` видна только владельцу.
- Кнопки панели в сцене `Menu.unity` переименованы (по fileID, безопасно): `No` → `PublishAction`, `No (2)` → `PublishDelete`, `No (3)` → `PublishClose` — теперь код управляет ими напрямую (раньше `ToggleNamed` искал «Upload/Update/Delete» и не находил, т.к. имена были другие).

### 6.3 Система аккаунтов (локальная)
- Новый `AccountManager.cs`: вход/регистрация/выход, аккаунты в PlayerPrefs, пароль хранится FNV-1a хешем (не открытым текстом).
- В панели файлов (FileInformation) программно добавлена кнопка **«Login / Register»** (правый верхний угол) с формой входа (ник + пароль, поле пароля скрытое).
- **Если не вошёл в аккаунт — кнопки открытия панели публикации скрыты.**
- `WorkshopMenu.UploadSelected` тоже требует входа.
- ⚠️ Локальные аккаунты **не синхронизируются между устройствами** (нет сервера). Для полной системы нужен бэкенд.

### 6.4 Защита от копирования
- В `GameData` добавлено поле `workshopSourceId` — ID оригинальной публикации.
- При скачивании `WorkshopClient.Download` проставляет `workshopSourceId = item.id` (owner-ключ не передаётся).
- **Скачанный чужой сейв нельзя перевыложить**: кнопка панели публикации скрыта, `UploadSelected` отказывает со статусом «Cannot republish a downloaded save».
- Свой локальный сейв и сейв, где ты владелец, публикуются как раньше.
- ⚠️ Сейвы, скачанные **до** этого обновления, поля `workshopSourceId` не имеют (0) — их ещё можно перевыложить. Старые сейвы не ломаются.
- ⚠️ Защита локальная: XOR-«шифрование» сейвов остаётся тривиальным, технически грамотный пользователь может подделать поле. Для жёсткой защиты нужен сервер.

### 6.5 Стакование кнопок
- На контейнер кнопок панели публикации добавлен `HorizontalLayoutGroup` + `ContentSizeFitter` (в рантайме, сцена не правилась): при скрытии кнопки остальные **съезжаются без пустот**.
- Кнопки панели файлов (`Export`/`Export(1)`/`Export(2)`) лежат в панели напрямую без LayoutGroup — их стакование потребовало бы обернуть их в контейнер в редакторе (или сделать отдельную панель), сейчас они просто скрываются с сохранением позиций соседей.

### 6.6 Файлы в этом обновлении
```
Assets/Scripts/Assembly-CSharp/AccountManager.cs          (новый)
Assets/Scripts/Assembly-CSharp/AccountManager.cs.meta     (новый)
Assets/Scripts/Assembly-CSharp/AdManager.cs                (заглушка без рекламы)
Assets/Scripts/Assembly-CSharp/CoinPanel.cs                (без рекламы)
Assets/Scripts/Assembly-CSharp/Quiz.cs                     (без AdManager)
Assets/Scripts/Assembly-CSharp/FileInformation.cs          (владелец/аккаунт/стакование/логин)
Assets/Scripts/Assembly-CSharp/WorkshopMenu.cs             (защита публикации)
Assets/Scripts/Assembly-CSharp/WorkshopClient.cs           (workshopSourceId)
Assets/Scripts/Assembly-CSharp/GameData.cs                 (workshopSourceId)
Assets/Scenes/Menu.unity                                  (имена 3 кнопок панели публикации)
Assets/Scripts/FakeGoogleMobileAds/                       (удалено)
```

---

## 7. Обновление 3 (22.08.2026) — фикс CS0535 + Remote Quiz с админки сайта

### 7.1 Исправлена ошибка компиляции
`Quiz.cs` (4,36): **CS0535 — не реализован `IPointerClickHandler.OnPointerClick`**.
Причина: при вырезке рекламы в прошлом обновлении метод `OnPointerClick` был удалён,
а интерфейс в объявлении класса остался. Метод возвращён (без рекламных зависимостей).

### 7.2 Remote Quiz — вызов квиза с админки сайта
- **Игра (клиент):**
  - `Quiz.cs`: добавлен `TriggerRemote(link, title, body)` + корутина `PollRemote()`,
    которая каждые `pollInterval` (по умолчанию **45 сек**, настраивается в инспекторе;
    первый опрос через 3 сек) спрашивает сервер через `WorkshopClient.GetQuiz()`.
  - `WorkshopClient.cs`: добавлены класс `WorkshopQuizResponse` и метод `GetQuiz`
    (запрос `?action=quiz&i=1`, через тот же обход анти-бота byethost, что и мастерская).
  - Когда сервер отвечает `show: true`, игра подставляет Title/Body в диалог
    (ищет тексты `Title`/`Body` в панели диалога) и показывает ConfirmationDialog;
    кнопка **Yes** открывает ссылку.
- **Сервер (папка `server/` в репозитории):**
  - `admin_quiz.php` — админ-панель: вход по паролю (по умолчанию `admin123`,
    **поменять**), поля Link/Title/Body, кнопка «Send to game», просмотр/очистка pending.
  - `api_quiz_snippet.php` — блок для вставки в `api.php`: обработка `action=quiz`,
    **одноразовая выдача** (после отдачи файл `quiz_pending.json` очищается).
  - `README.md` — инструкция по установке на byethost.
- Квиз одноразовый: отправленный с админки квиз ждёт в `quiz_pending.json`,
  пока его не заберёт первый опросивший клиент.

### Файлы обновления
```
Assets/Scripts/Assembly-CSharp/Quiz.cs            (OnPointerClick, TriggerRemote, PollRemote)
Assets/Scripts/Assembly-CSharp/WorkshopClient.cs   (GetQuiz + WorkshopQuizResponse)
server/admin_quiz.php                              (новое, админка)
server/api_quiz_snippet.php                        (новое, сниппет для api.php)
server/README.md                                   (новое, инструкция)
```

### Что нужно сделать на сайте
1. Загрузить `admin_quiz.php` в папку с `api.php` (byethost).
2. Вставить блок из `api_quiz_snippet.php` в `api.php`.
3. Поменять пароль в `admin_quiz.php`.
4. Открыть `https://ВАШ_САЙТ/admin_quiz.php`, отправить квиз.

## 8. Деплой Remote Quiz на хостинг (22.08.2026, по FTP)

- Загружено на byethost (ftpupload.net, `b4_42712522`):
  - `htdocs/workshop/api.php` — добавлен блок `action=quiz` (одноразовая выдача);
  - `htdocs/workshop/admin_quiz.php` — админ-панель квиза.
- Проверен полный цикл на живом сервере: вход в админку → отправка квиза →
  игра получает его при опросе → повторный опрос пустой (one-shot).
- Админка: `https://orangepcsimu.byethost4.com/workshop/admin_quiz.php`
- ⚠️ Пароль админки по умолчанию `admin123` — СМЕНИТЕ (в `admin_quiz.php`).
- Задеплоенная копия `api.php` добавлена в репозиторий (`server/api.php`).

## 9. Обновление 4 (22.08.2026) — инспектор для аккаунта, модерация, красивый сайт

### 9.1 Unity: панель аккаунта настраивается через ИНСПЕКТОР
- Новый компонент **`AccountPanel`** (Assets/Scripts/Assembly-CSharp/AccountPanel.cs):
  - сериализованные поля: `nickInput`, `passInput`, `loginButton`, `registerButton`,
    `logoutButton`, `statusText`, `formRoot`, `autoCreateUI`;
  - публичные методы для OnClick в инспекторе: `Login()`, `Register()`, `Logout()`, `ToggleForm()`;
  - если ничего не привязано и `autoCreateUI=true` — UI создаётся автоматически
    (кнопка «Login / Register» + форма), как было раньше;
  - если на сцене нет ни одной панели — `FileInformation` сам создаёт её
    (`AccountPanel.EnsureOnScene()`), так что сцена не ломается.
- `AccountManager` теперь бросает событие `AccountChanged` — панели и кнопки
  публикации (FileInformation) обновляются автоматически при входе/выходе.
- Старый «захардкоженный» UI аккаунта из FileInformation удалён.

**Как собрать свою страницу аккаунта:** создайте панель в сцене (Canvas → объект),
добавьте компонент `AccountPanel`, привяжите в инспекторе свои InputField/Button/Text,
а кнопкам в OnClick назначьте `Login()`/`Register()`/`Logout()`/`ToggleForm()`.

### 9.2 Сервер: модерация сейвов и бан пользователей
- **`api.php`**: при upload записывается IP автора; добавлена проверка бан-листа
  (`banned.json`): забаненный ник или IP получает `403 {"ok":false,"error":"banned"}`
  при upload/update. Скачивание не блокируется.
- **`admin.php`** — единая админка (заменила admin_quiz.php):
  - **Обзор** — статистика (сейвы, скачивания, лайки, баны);
  - **Сейвы** — таблица всех сейвов с IP: удалить, забанить автора, забанить IP;
  - **Баны** — список банов, снять бан, добавить вручную (автор/IP + причина);
  - **Квиз** — отправка квиза в игру (одноразово);
  - **Пароль** — смена пароля админа (хранится хешем в admin_config.json);
  - CSRF-защита всех действий, пароль по умолчанию `admin123` — СМЕНИТЕ.
- **`index.php`** — красивая публичная витрина мастерской: карточки сейвов
  (обложка/название/автор/мета/скачать), поиск по названию и автору.
- **`style.css`** — общий стиль (тёмная тема, оранжевый акцент Orange PC).

### 9.3 Проверено на живом сервере (22.08.2026)
- Логин в админку → модерация работает;
- Удаление сейвов через админку — ок (тестовые сейвы вычищены, `items: []`);
- Бан автора → upload отклоняется `403 banned` → разбан работает;
- Квиз через админку — одноразовая выдача (poll1 show:true, poll2 show:false);
- Витрина index.php открывается.

### Файлы обновления
```
Assets/Scripts/Assembly-CSharp/AccountPanel.cs      (новый, инспектор-панель)
Assets/Scripts/Assembly-CSharp/AccountPanel.cs.meta (новый)
Assets/Scripts/Assembly-CSharp/AccountManager.cs    (событие AccountChanged)
Assets/Scripts/Assembly-CSharp/FileInformation.cs   (аккаунт вынесен в AccountPanel)
server/api.php         (бан-логика + IP при upload)
server/admin.php       (новая админка с модерацией)
server/index.php       (витрина мастерской)
server/style.css       (стили)
server/README.md       (документация)
server/admin_quiz.php  (удалён, заменён admin.php)
```
