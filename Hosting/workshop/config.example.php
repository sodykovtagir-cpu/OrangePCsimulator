<?php
// Скопируй в config.php и заполни. config.php на GitHub не клади.

define('DB_HOST', 'sql212.byethost4.com');
define('DB_USER', 'b4_42712522');
define('DB_PASS', 'ПАРОЛЬ_MYSQL');
define('DB_NAME', 'b4_42712522_workshop');

// Пустой = заливать может кто угодно (с лимитом). Лучше придумай ключ.
define('UPLOAD_KEY', '');

define('MAX_BYTES', 10485760); // 10 MB
define('RATE_PER_HOUR', 8);
define('UPLOADS_DIR', __DIR__ . '/uploads');

// ---- Почта (код подтверждения регистрации) ----
// Mail.ru / internet.ru: smtp.mail.ru, порт 465 (ssl) или 587 (tls).
// MAIL_FROM и MAIL_SMTP_USER должны быть одним и тем же ящиком.
// Пароль — только в config.php, не в example и не в git.
define('MAIL_FROM', 'orangepcsimu@internet.ru');
define('MAIL_FROM_NAME', 'Orange PC Simulator');
define('MAIL_SMTP_HOST', 'smtp.mail.ru');
define('MAIL_SMTP_PORT', 465);
define('MAIL_SMTP_USER', 'orangepcsimu@internet.ru');
define('MAIL_SMTP_PASS', ''); // пароль ящика / пароль приложения
define('MAIL_SMTP_SECURE', 'ssl'); // ssl (465) | tls (587)

// ---- Telegram-бот (привязка аккаунта + бонус 5 BTC) ----
// Токен от @BotFather. Пусто = мягкая привязка без проверки ботом.
// TELEGRAM_BOT_USERNAME — юзернейм бота БЕЗ @ (напр. 'orangepcsimubot').
// TELEGRAM_WEBHOOK_SECRET — секрет для проверки webhook-запросов.
// Подтверждение через WEBHOOK (byethost не имеет исходящего доступа к api.telegram.org).
define('TELEGRAM_BOT_TOKEN', '');
define('TELEGRAM_BOT_USERNAME', '');
define('TELEGRAM_WEBHOOK_SECRET', '');
