<?php
// Скопируй в config.php и заполни. config.php на GitHub не клади.

define('DB_HOST', 'sql212.byethost4.com');
define('DB_USER', 'b4_42712522');
define('DB_PASS', 'ПАРОЛЬ_MYSQL');
define('DB_NAME', 'b4_42712522_workshop');

// Пустой = заливать может кто угодно (с лимитом). Лучше придумай ключ.
define('UPLOAD_KEY', '');

define('MAX_BYTES', 1048576); // 1 MB
define('RATE_PER_HOUR', 8);
define('UPLOADS_DIR', __DIR__ . '/uploads');

// ---- Почта (код подтверждения регистрации) ----
// Если MAIL_SMTP_HOST пуст — используется встроенный PHP mail() на byethost.
// Если хочешь SMTP (Gmail/Яндекс и т.п.) — заполни и получи пароль приложения.
define('MAIL_FROM', 'noreply@orangepcsimu.byethost4.com');
define('MAIL_FROM_NAME', 'Orange PC Simulator');
define('MAIL_SMTP_HOST', '');
define('MAIL_SMTP_PORT', 587);
define('MAIL_SMTP_USER', '');
define('MAIL_SMTP_PASS', '');
define('MAIL_SMTP_SECURE', 'tls'); // tls | ssl | пусто

// ---- Telegram-бот (привязка аккаунта + бонус 5 BTC) ----
// Токен от @BotFather. Пусто = мягкая привязка без проверки ботом.
// TELEGRAM_BOT_USERNAME — юзернейм бота БЕЗ @ (напр. 'orangepcsimubot').
// TELEGRAM_WEBHOOK_SECRET — секрет для проверки webhook-запросов.
// Подтверждение через WEBHOOK (byethost не имеет исходящего доступа к api.telegram.org).
define('TELEGRAM_BOT_TOKEN', '');
define('TELEGRAM_BOT_USERNAME', '');
define('TELEGRAM_WEBHOOK_SECRET', '');
