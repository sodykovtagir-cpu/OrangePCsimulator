# Orange PC Workshop (Byethost)

Сайт: `https://orangepcsimu.byethost4.com`

## 1. MySQL
В cPanel → MySQL создай базу, например `b4_42712522_workshop`.
Импортируй `workshop/schema.sql`.

## 2. config
Скопируй `workshop/config.example.php` → `workshop/config.php`, впиши пароль MySQL и имя базы.
`config.php` в git не клади.

## 3. FTP
Залей папку `workshop/` в `htdocs/workshop/` (или `public_html/workshop/`).
Создай на сервере `workshop/uploads/` с правами записи.

Проверка в браузере:
`https://orangepcsimu.byethost4.com/workshop/api.php?action=list`

Должен быть JSON `{"ok":true,"items":[]}`.
Если видишь HTML с рекламой — открой сайт один раз в браузере (защита Byethost), потом снова API.

## 4. Игра
В Unity на объекте меню повесь `WorkshopMenu`.
URL по умолчанию уже `https://orangepcsimu.byethost4.com/workshop/api.php`.

Кнопка в меню: `WorkshopMenu.Show`.
