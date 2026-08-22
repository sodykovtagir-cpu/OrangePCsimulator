<?php
/**
 * Orange PC Simulator — подтверждение привязки Telegram через бота.
 *
 * Настройки:
 *   config.php  ->  TELEGRAM_BOT_TOKEN  (токен бота от @BotFather)
 *
 * Как работает:
 *   1. Игра делает account.php?action=tg_link (с токеном сессии) -> сервер
 *      создаёт pending-код и отдаёт ссылку https://t.me/<bot>?start=<code>.
 *   2. Игрок жмёт /start <code> (или /link <code>) в боте.
 *   3. Этот скрипт полает getUpdates, находит сообщение с кодом, сверяет с
 *      tg_pending.json и помечает привязку как подтверждённую (+ бонус 5 BTC).
 *
 * Запуск: периодически вызывай (например, каждые 30 сек) через cron/WEB или
 * запусти в бесконечный цикл с CLI:  php telegram_bot.php loop
 */

require __DIR__ . '/config.php';
if (!defined('TELEGRAM_BOT_TOKEN')) define('TELEGRAM_BOT_TOKEN', '');
if (!defined('TG_PENDING_FILE'))    define('TG_PENDING_FILE', __DIR__ . '/tg_pending.json');
if (!defined('USERS_FILE'))         define('USERS_FILE', __DIR__ . '/users.json');

if (TELEGRAM_BOT_TOKEN === '') {
    http_response_code(500);
    echo "TELEGRAM_BOT_TOKEN not configured in config.php";
    return;
}

function tg_api($method, $params = []) {
    $url = 'https://api.telegram.org/bot' . TELEGRAM_BOT_TOKEN . '/' . $method;
    $ch = curl_init($url);
    curl_setopt_array($ch, [
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_POST => true,
        CURLOPT_POSTFIELDS => http_build_query($params),
        CURLOPT_TIMEOUT => 60,
    ]);
    $res = curl_exec($ch);
    curl_close($ch);
    return json_decode($res, true);
}
function load_json($f, $def = []) { if (!is_file($f)) return $def; $j = json_decode(@file_get_contents($f), true); return is_array($j) ? $j : $def; }
function save_json($f, $d) { return @file_put_contents($f, json_encode($d, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT), LOCK_EX); }

$pending = load_json(TG_PENDING_FILE);

$loop = (isset($argv[1]) && $argv[1] === 'loop');
$offset = 0;

do {
    $upd = tg_api('getUpdates', ['timeout' => 30, 'offset' => $offset, 'allowed_updates' => ['message']]);
    if (empty($upd['ok']) || empty($upd['result'])) { if ($loop) sleep(2); else break; }

    foreach ($upd['result'] as $u) {
        $offset = max($offset, (int)$u['update_id'] + 1);
        if (empty($u['message']['text'])) continue;
        $text = trim($u['message']['text']);
        $chatId = $u['message']['chat']['id'];

        // /start <code>  или  /link <code>
        $code = '';
        if (preg_match('#^/(start|link)\s+([A-Za-z0-9]+)#', $text, $m)) $code = strtolower($m[2]);
        if ($code === '' || !isset($pending[$code])) {
            tg_api('sendMessage', ['chat_id' => $chatId, 'text' => "Команда не распознана или ссылка устарела."]);
            continue;
        }

        $rec = $pending[$code];
        $users = load_json(USERS_FILE);
        $found = false;
        foreach ($users as $k => $user) {
            if ((int)$user['id'] === (int)$rec['user']) {
                $users[$k]['tg_chat_id'] = $chatId;
                $users[$k]['tg_bonus'] = true;
                $found = true;
                break;
            }
        }
        if ($found) save_json(USERS_FILE, $users);
        unset($pending[$code]);
        save_json(TG_PENDING_FILE, $pending);

        tg_api('sendMessage', ['chat_id' => $chatId, 'text' => "✅ Аккаунт привязан, бонус +5 BTC начислен!"]);
    }
} while ($loop);
