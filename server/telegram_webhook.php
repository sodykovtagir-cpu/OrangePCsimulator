<?php
/**
 * Orange PC Simulator — WEBHOOK подтверждения привязки Telegram.
 *
 * Почему webhook, а не поллер: на бесплатном byethost ЗАБЛОКИРОВАН исходящий
 * доступ к api.telegram.org (DNS не резолвится), поэтому бот не может сам
 * поллить getUpdates. Telegram, наоборот, МОЖЕТ прислать нам обновление
 * (входящий запрос) — webhook это и использует.
 *
 * Настройка (один раз, с машины, где есть доступ к api.telegram.org):
 *   https://api.telegram.org/bot<TOKEN>/setWebhook
 *       ?url=https://orangepcsimu.byethost4.com/workshop/telegram_webhook.php
 *       &secret_token=<TWOJ_SECRET>         (проверка, что запрос реально от Telegram)
 *
 * Флоу:
 *   1. Игра: account.php?action=tg_link -> сервер создаёт pending-код
 *      и отдаёт ссылку https://t.me/<BOT>?start=<code>.
 *   2. Игрок жмёт /start <code> (или /link <code>) у бота.
 *   3. Telegram присылает update на этот webhook.
 *   4. Скрипт сверяет код с tg_pending.json, помечает user tg_bonus=true,
 *      сохраняет tg_chat_id и отвечает Telegram'у 200.
 *
 * Ответботное сообщение (sendMessage) отсюда не шлём, т.к. нет исходящего
 * доступа — по желанию его можно отправить с внешнего поллера.
 */

require __DIR__ . '/config.php';
if (!defined('TELEGRAM_BOT_TOKEN')) define('TELEGRAM_BOT_TOKEN', '');
if (!defined('TELEGRAM_WEBHOOK_SECRET')) define('TELEGRAM_WEBHOOK_SECRET', '');
if (!defined('TG_PENDING_FILE')) define('TG_PENDING_FILE', __DIR__ . '/tg_pending.json');
if (!defined('USERS_FILE')) define('USERS_FILE', __DIR__ . '/users.json');

function wl($f, $def = []) { if (!is_file($f)) return $def; $j = json_decode(@file_get_contents($f), true); return is_array($j) ? $j : $def; }
function ws($f, $d) { return @file_put_contents($f, json_encode($d, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT), LOCK_EX); }

// Проверка секрета (если задан при setWebhook) — защита от подделки.
$header = isset($_SERVER['HTTP_X_TELEGRAM_BOT_API_SECRET_TOKEN']) ? $_SERVER['HTTP_X_TELEGRAM_BOT_API_SECRET_TOKEN'] : '';
if (TELEGRAM_WEBHOOK_SECRET !== '' && !hash_equals(TELEGRAM_WEBHOOK_SECRET, $header)) {
    http_response_code(403);
    echo 'forbidden';
    return;
}

$input = file_get_contents('php://input');
$update = $input ? json_decode($input, true) : null;

// Отвечаем 200 сразу, чтобы Telegram не ретраил.
if (empty($update) || !isset($update['message'])) {
    http_response_code(200);
    echo 'ok';
    return;
}

$msg = $update['message'];
$text = isset($msg['text']) ? trim($msg['text']) : '';
$chatId = isset($msg['chat']['id']) ? (int)$msg['chat']['id'] : 0;

$code = '';
if (preg_match('#^/start\s+([A-Za-z0-9]+)#', $text, $m)) $code = strtolower($m[1]);
elseif (preg_match('#^/link\s+([A-Za-z0-9]+)#', $text, $m)) $code = strtolower($m[1]);

if ($code === '') { http_response_code(200); echo 'ok'; return; }

$pending = wl(TG_PENDING_FILE);
if (!isset($pending[$code])) { http_response_code(200); echo 'ok'; return; }

$rec = $pending[$code];
$users = wl(USERS_FILE);
$found = false;
foreach ($users as $k => $user) {
    if ((int)$user['id'] === (int)$rec['user']) {
        $users[$k]['tg_chat_id'] = $chatId;
        $users[$k]['tg_username'] = isset($msg['from']['username']) ? '@' . $msg['from']['username'] : ($users[$k]['tg_username'] ?? '');
        $users[$k]['tg_bonus'] = true;
        $users[$k]['tg_verified_at'] = gmdate('Y-m-d H:i:s');
        $found = true;
        break;
    }
}
if ($found) ws(USERS_FILE, $users);
unset($pending[$code]);
ws(TG_PENDING_FILE, $pending);

http_response_code(200);
echo 'ok';
