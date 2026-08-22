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
