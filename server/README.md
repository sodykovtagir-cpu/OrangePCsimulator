# Orange PC Workshop — серверная часть

Сайт: `https://orangepcsimu.byethost4.com/workshop/`

## Файлы

| Файл | Назначение |
|------|------------|
| `api.php` | API мастерской (list/upload/update/delete/like/download/cover/quiz). Бан-проверка при upload/update. |
| `index.php` | Публичная витрина мастерской (карточки сейвов, поиск, скачивание). |
| `admin.php` | Админка: модерация сейвов, баны, квиз, смена пароля. |
| `style.css` | Общие стили витрины и админки. |
| `admin_quiz.php` | (устарел, заменён админкой `admin.php` — вкладка «Квиз») |

## Админка (модерация)

Адрес: `https://orangepcsimu.byethost4.com/workshop/admin.php`

- **Обзор** — статистика (сейвы, скачивания, лайки, баны).
- **Сейвы** — таблица всех сейвов: удалить, забанить автора, забанить IP.
- **Баны** — список банов, снять бан; добавить бан вручную (автор или IP).
- **Квиз** — отправить квиз в игру (link/title/body), очистить отложенный.
- **Пароль** — смена пароля админа (хранится в `admin_config.json`, hash).

Бан работает на стороне API: забаненный ник или IP получает `403 {"ok":false,"error":"banned"}`
при загрузке (`upload`) и обновлении (`update`) сейвов. Скачивание не блокируется.

Пароль по умолчанию `admin123` — смените на вкладке «Пароль»! ⚠️

## Публичная витрина

`https://orangepcsimu.byethost4.com/workshop/` — красивая витрина с поиском
и карточками. Данные читаются напрямую из `uploads/index.json`.

## Remote Quiz (из админки)

Админ → вкладка «Квиз» → ссылка + заголовок + текст → «Отправить».
Игра опрашивает `api.php?action=quiz&i=1` каждые ~45 сек; квиз выдаётся
**один раз** (файл `quiz_pending.json` очищается после выдачи).

## Файлы данных на сервере

| Файл | Содержимое |
|------|------------|
| `uploads/index.json` | список сейвов (id, title, author, ip, ...) |
| `uploads/*.opc`, `uploads/c*.jpg` | файлы сейвов и обложки |
| `banned.json` | баны: `[{type: author|ip, value, reason, at}]` |
| `quiz_pending.json` | отложенный квиз |
| `admin_config.json` | хеш пароля админа |
| `promos.json` | промокоды: `[{code, cash, btc}]` (НЕ в git) |
| `promo_claimed.json` | выданные промокоды по client-id (НЕ в git) |

Не кладите `config.php`, `admin_config.json`, `promos.json`, `promo_claimed.json` в git.

## UPLOAD_KEY (закрытая загрузка)

Чтобы посторонние не могли заливать сейвы, задай `UPLOAD_KEY` в `config.php`
и впиши **то же самое** значение в клиент `WorkshopClient.UploadKey`
(`Assets/Scripts/Assembly-CSharp/WorkshopClient.cs`). Если клиент не шлёт ключ,
загрузка вернёт `403 bad key`. **Включать только вместе с релизом новой сборки**,
иначе уже установленные клиенты перестанут заливать.

## Аккаунты (account.php) — двухэтапка с email

`server/account.php` — серверная регистрация: **ник + пароль + email → на почту код → вход**.
Автовход через session-токен (клиент хранит в PlayerPrefs).

| action | Метод | Поля | Ответ |
|--------|-------|------|-------|
| `register` | POST | name, email, password, client | `{ok, pending, sent}` (шлёт код на почту) |
| `verify` | POST | email, code, client | `{ok, token, name, email}` |
| `login` | POST | login (имя/email), password, client | `{ok, token, ...}` |
| `resend` | POST | email | `{ok, sent}` |
| `me` | GET/POST | token (заголовок `X-Auth-Token` или поле) | `{ok, name, email, tg, tg_bonus, saves:[...]}` |
| `logout` | POST | token | `{ok}` |
| `tg_link` | POST | token, telegram | бонус 5 BTC (или pending-ссылка при боте) |
| `tg_bonus` | POST | token | `{ok, granted, btc:5}` |

Хранилище: `users.json` (пароль и код — `password_hash`), `tg_pending.json`.
**Почта:** SMTP Mail.ru (`smtp.mail.ru:465`, ящик `orangepcsimu@internet.ru`).
Пароль и хост задаются в `config.php` (`MAIL_SMTP_*`) — файл **не в git**.
`MAIL_FROM` должен совпадать с ящиком (`orangepcsimu@internet.ru`), иначе Mail.ru
отклонит письмо. Если `MAIL_SMTP_HOST` пуст — fallback на PHP `mail()`.

**Telegram-бот (через WEBHOOK):** на бесплатном byethost **заблокирован исходящий
доступ к `api.telegram.org`** (DNS не резолвится), поэтому поллер не работает.
Подтверждение идёт через **webhook**:
1. В `config.php` задай `TELEGRAM_BOT_TOKEN` (от @BotFather),
   `TELEGRAM_BOT_USERNAME` (юзернейм бота без `@`) и `TELEGRAM_WEBHOOK_SECRET`.
2. Один раз зарегистрируй webhook (с машины с доступом к Telegram):
   ```
   https://api.telegram.org/bot<TOKEN>/setWebhook?url=https://orangepcsimu.byethost4.com/workshop/telegram_webhook.php&secret_token=<SECRET>
   ```
3. `tg_link` вернёт deep-link `https://t.me/<BOT>?start=<code>`. Игрок жмёт
   `/start <code>` у бота → Telegram шлёт update на webhook → `telegram_webhook.php`
   подтверждает привязку и выдаёт +5 BTC (по секрету из заголовка).
Без токена — мягкая привязка (бонус сразу). `users.json`, `tg_pending.json`
в git **не клади** (уже в `.gitignore`).

`users.json`, `tg_pending.json` в git **не клади** (уже в `.gitignore`).

## Промокоды (Giveaway) — серверная проверка

Коды больше не зашиты в клиент. Игра шлёт `POST api.php?action=redeem`
с `code` и `client` (client-id). Сервер сверяет код с `promos.json` и выдаёт
его **один раз** на client-id (помечает в `promo_claimed.json`), т.е. удаление
PlayerPrefs на клиенте не даёт повторно заклеймить. Ответ: `{ok, cash, btc}`.

## Деплой

```
ftp → htdocs/workshop/
  api.php, index.php, admin.php, style.css
  uploads/ (с правами записи), banned.json (создастся сам)
  promos.json + промокоды (создать руками, НЕ в git)
```

## Лимит сейва и загрузка частями

- Один сейв: максимум **30 × 1024 × 1024 = 31 457 280 байт**. Проверка выполняется на сервере и при публикации, и при замене файла.
- Разворачивать вместе: `api.php`, `upload_limits.php`, `upload_chunks.php`. В существующем `config.php` установить `MAX_BYTES` в `31457280`, не заменяя остальные параметры/секреты.
- Рекомендуемые PHP-настройки: `upload_max_filesize=30M`, `post_max_size=40M` (запас на обложку и multipart). Файлы `.htaccess`/`.user.ini` приложены, но хостинг может принудительно игнорировать эти значения.
- На текущем Byethost фактически действуют `20M` на файл и `30M` на POST. Новый игровой клиент отправляет сейвы больше 8 МиБ частями через `action=upload_chunk`, затем обычным `upload`/`update` публикует собранный файл. Поэтому итоговый сейв до 30 МиБ проходит без изменения тарифных ограничений PHP. Старую сборку игры нужно обновить: у неё собственный предел 10 МиБ и нет сегментированной загрузки.
- Сессия загрузки привязана к аккаунту и случайному секрету; проверяются порядок частей, размер каждой (до 8 МиБ), общий размер и полнота. Успешная публикация поглощает сессию. Незавершённые файлы удаляются при новых загрузках через час; одновременно разрешены четыре сессии на аккаунт и 32 всего. Клиенту не передаётся путь к файлу на сервере.
- `tools/test_workshop_save_limit.py`: прямые HTTP-загрузки на границе 30 МиБ, обновление, отклонение +1 байта и переполненного POST.
- `tools/test_workshop_chunk_upload.py`: сборка и скачивание 30 МиБ при PHP-лимитах 20M/30M, проверка хеша, отказ для +1 байта, неверного секрета, перекрывающейся части, неполной/повторной публикации; замена существующего сейва.
