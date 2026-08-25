<?php
/**
 * Orange PC Simulator — СЕРВЕР АККАУНТОВ.
 *
 * Двухэтапка: регистрация (ник + пароль + email) -> на почту код -> подтверждение.
 * Автовход: после входа выдаётся session-токен, клиент хранит его на устройстве.
 * Привязка Telegram через бота (TELEGRAM_BOT_TOKEN) + одноразовый бонус 5 BTC.
 *
 * Хранилище (НЕ в git):
 *   users.json        — аккаунты (пароль — password_hash, код — hash)
 *   tg_pending.json   — ожидающие подтверждения привязки к Telegram
 *
 * Файлы данных и пароль конфига в git НЕ класть.
 */

header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, X-Auth-Token');
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') { http_response_code(204); exit; }

$cfgPath = __DIR__ . '/config.php';
if (is_file($cfgPath)) require $cfgPath;

// ---- defaults ----
if (!defined('USERS_FILE'))         define('USERS_FILE', __DIR__ . '/users.json');
if (!defined('TG_PENDING_FILE'))    define('TG_PENDING_FILE', __DIR__ . '/tg_pending.json');
if (!defined('CODE_TTL'))           define('CODE_TTL', 900);          // 15 минут
if (!defined('MAIL_FROM'))          define('MAIL_FROM', 'orangepcsimu@internet.ru');
if (!defined('MAIL_FROM_NAME'))     define('MAIL_FROM_NAME', 'Orange PC Simulator');
// SMTP: задаётся в config.php. Mail.ru: smtp.mail.ru:465 ssl.
if (!defined('MAIL_SMTP_HOST'))     define('MAIL_SMTP_HOST', '');
if (!defined('MAIL_SMTP_PORT'))     define('MAIL_SMTP_PORT', 465);
if (!defined('MAIL_SMTP_USER'))     define('MAIL_SMTP_USER', '');
if (!defined('MAIL_SMTP_PASS'))     define('MAIL_SMTP_PASS', '');
if (!defined('MAIL_SMTP_SECURE'))   define('MAIL_SMTP_SECURE', 'ssl'); // ssl|tls|''
if (!defined('TELEGRAM_BOT_TOKEN')) define('TELEGRAM_BOT_TOKEN', '');
if (!defined('TELEGRAM_BOT_USERNAME')) define('TELEGRAM_BOT_USERNAME', ''); // без @

function load_json($f, $def = []) {
    if (!is_file($f)) return $def;
    $j = json_decode(@file_get_contents($f), true);
    return is_array($j) ? $j : $def;
}
function save_json($f, $data) {
    return @file_put_contents($f, json_encode($data, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT), LOCK_EX);
}
function json_out($data, $code = 200) {
    http_response_code($code);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($data, JSON_UNESCAPED_UNICODE);
    exit;
}
function clean($s, $max) {
    $s = trim(preg_replace('/\s+/', ' ', strip_tags((string)$s)));
    if (function_exists('mb_substr')) return mb_substr($s, 0, $max);
    return substr($s, 0, $max);
}
function norm_name($s) { return strtolower(trim((string)$s)); }

function users() { return load_json(USERS_FILE); }
function save_users($u) { return save_json(USERS_FILE, $u); }

function find_by_name($name) {
    $n = norm_name($name);
    foreach (users() as $i => $u) if (norm_name($u['name']) === $n) return $i;
    return -1;
}
function find_by_email($email) {
    $e = strtolower(trim((string)$email));
    foreach (users() as $i => $u) if (strtolower(trim($u['email'])) === $e) return $i;
    return -1;
}
function find_by_token($token) {
    if ($token === '') return -1;
    foreach (users() as $i => $u) if (!empty($u['token']) && hash_equals($u['token'], $token)) return $i;
    return -1;
}

function gen_code() {
    return (string)random_int(100000, 999999);
}
function gen_token() {
    return bin2hex(random_bytes(32));
}
function valid_email($e) {
    return (bool)filter_var($e, FILTER_VALIDATE_EMAIL);
}

/** Читает многострочный ответ SMTP. */
function smtp_read($sock) {
    $data = '';
    stream_set_timeout($sock, 20);
    while (($line = @fgets($sock, 515)) !== false) {
        $data .= $line;
        if (strlen($line) >= 4 && $line[3] === ' ') break;
        if (strlen($line) < 4) break;
    }
    return [(int)substr($data, 0, 3), $data];
}

/** Команда SMTP. $expect — код или массив допустимых кодов. */
function smtp_cmd($sock, $cmd, $expect) {
    if ($cmd !== null && $cmd !== '') fwrite($sock, $cmd . "\r\n");
    list($code, $raw) = smtp_read($sock);
    $ok = is_array($expect) ? in_array($code, $expect, true) : ($code === (int)$expect);
    return [$ok, $code, $raw];
}

/** SMTP-клиент (AUTH LOGIN). ssl = implicit TLS :465, tls = STARTTLS :587. */
function smtp_send($to, $subject, $htmlBody, $textBody) {
    $host = MAIL_SMTP_HOST;
    if ($host === '') return false;
    $port   = (int)MAIL_SMTP_PORT;
    $user   = MAIL_SMTP_USER;
    $pass   = MAIL_SMTP_PASS;
    $secure = strtolower((string)MAIL_SMTP_SECURE);

    $ctx = stream_context_create([
        'ssl' => [
            'verify_peer'       => false,
            'verify_peer_name'  => false,
            'allow_self_signed' => true,
            'crypto_method'     => STREAM_CRYPTO_METHOD_TLS_CLIENT,
        ],
    ]);
    $scheme = ($secure === 'ssl') ? 'ssl' : 'tcp';
    $errno = 0; $errstr = '';
    $sock = @stream_socket_client(
        $scheme . '://' . $host . ':' . $port,
        $errno,
        $errstr,
        20,
        STREAM_CLIENT_CONNECT,
        $ctx
    );
    if (!$sock) return false;

    list($ok) = smtp_cmd($sock, null, [220]);
    if (!$ok) { fclose($sock); return false; }
    list($ok) = smtp_cmd($sock, 'EHLO orangepcsimu.local', [250]);
    if (!$ok) { fclose($sock); return false; }

    if ($secure === 'tls') {
        list($ok) = smtp_cmd($sock, 'STARTTLS', [220]);
        if (!$ok) { fclose($sock); return false; }
        if (!@stream_socket_enable_crypto($sock, true, STREAM_CRYPTO_METHOD_TLS_CLIENT)) {
            fclose($sock);
            return false;
        }
        list($ok) = smtp_cmd($sock, 'EHLO orangepcsimu.local', [250]);
        if (!$ok) { fclose($sock); return false; }
    }

    if ($user !== '') {
        list($ok) = smtp_cmd($sock, 'AUTH LOGIN', [334]);
        if (!$ok) { fclose($sock); return false; }
        list($ok) = smtp_cmd($sock, base64_encode($user), [334]);
        if (!$ok) { fclose($sock); return false; }
        list($ok) = smtp_cmd($sock, base64_encode($pass), [235, 250]);
        if (!$ok) { fclose($sock); return false; }
    }

    list($ok) = smtp_cmd($sock, 'MAIL FROM:<' . MAIL_FROM . '>', [250]);
    if (!$ok) { fclose($sock); return false; }
    list($ok) = smtp_cmd($sock, 'RCPT TO:<' . $to . '>', [250, 251]);
    if (!$ok) { fclose($sock); return false; }
    list($ok) = smtp_cmd($sock, 'DATA', [354]);
    if (!$ok) { fclose($sock); return false; }

    $subj = function_exists('mb_encode_mimeheader')
        ? mb_encode_mimeheader($subject, 'UTF-8')
        : $subject;
    $fromName = function_exists('mb_encode_mimeheader')
        ? mb_encode_mimeheader(MAIL_FROM_NAME, 'UTF-8')
        : MAIL_FROM_NAME;
    $msgid = '<' . bin2hex(random_bytes(8)) . '@internet.ru>';
    $headers = "From: " . $fromName . " <" . MAIL_FROM . ">\r\n"
             . "To: <" . $to . ">\r\n"
             . "Date: " . gmdate('D, d M Y H:i:s') . " +0000\r\n"
             . "Message-ID: " . $msgid . "\r\n"
             . "MIME-Version: 1.0\r\n"
             . "Content-Type: text/html; charset=utf-8\r\n"
             . "Subject: " . $subj . "\r\n";
    $body = str_replace(["\r\n.", "\n."], ["\r\n..", "\n.."], $htmlBody);
    fwrite($sock, $headers . "\r\n" . $body . "\r\n.\r\n");
    list($ok) = smtp_cmd($sock, null, [250]);
    @smtp_cmd($sock, 'QUIT', [221, 250]);
    fclose($sock);
    return $ok;
}

/** Отправка письма: SMTP если настроен, иначе mail(). */
function send_mail($to, $subject, $htmlBody, $textBody) {
    if (MAIL_SMTP_HOST !== '' && smtp_send($to, $subject, $htmlBody, $textBody)) return true;
    $headers = "MIME-Version: 1.0\r\n"
             . "Content-Type: text/html; charset=utf-8\r\n"
             . "From: " . MAIL_FROM_NAME . " <" . MAIL_FROM . ">\r\n";
    return @mail($to, $subject, $htmlBody, $headers);
}

function send_verify_code($email, $code) {
    $link = 'https://orangepcsimu.byethost4.com/workshop/';
    $html = "<div style='font-family:sans-serif;max-width:520px;margin:24px auto;border:1px solid #eee;border-radius:12px;padding:24px'>"
          . "<h2 style='color:#f47b20'>Orange PC Simulator</h2>"
          . "<p>Ваш код подтверждения:</p>"
          . "<div style='font-size:30px;font-weight:700;letter-spacing:6px;color:#222'>$code</div>"
          . "<p style='color:#777;font-size:13px'>Код действует 15 минут. Если вы не регистрировались — просто проигнорируйте письмо.</p>"
          . "<p style='color:#aaa;font-size:12px'>Orange PC Simulator · <a href='$link'>$link</a></p></div>";
    $text = "Ваш код подтверждения: $code (действует 15 минут)";
    return send_mail($email, 'Код подтверждения — Orange PC Simulator', $html, $text);
}

$action = isset($_GET['action']) ? $_GET['action'] : '';

// ================= REGISTER =================
if ($action === 'register' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $name  = clean(isset($_POST['name'])  ? $_POST['name']  : '', 20);
    $email = clean(isset($_POST['email']) ? $_POST['email'] : '', 120);
    $pass  = isset($_POST['password']) ? $_POST['password'] : '';
    $client = clean(isset($_POST['client']) ? $_POST['client'] : '', 64);

    if (mb_strlen($name) < 3 || mb_strlen($name) > 20) json_out(['ok' => false, 'error' => 'bad name'], 400);
    if (!valid_email($email)) json_out(['ok' => false, 'error' => 'bad email'], 400);
    if (strlen($pass) < 6) json_out(['ok' => false, 'error' => 'bad password'], 400);

    $users = users();
    $byName = find_by_name($name);
    if ($byName >= 0 && !empty($users[$byName]['verified'])) json_out(['ok' => false, 'error' => 'name taken'], 409);
    $byEmail = find_by_email($email);
    if ($byEmail >= 0 && !empty($users[$byEmail]['verified'])) json_out(['ok' => false, 'error' => 'email taken'], 409);

    // Переиспользуем/обновляем незавершённую регистрацию (тот же email или имя)
    $i = $byName >= 0 ? $byName : $byEmail;
    $code = gen_code();
    $record = [
        'id'          => $i >= 0 ? (int)$users[$i]['id'] : (count($users) ? (int)end($users)['id'] + 1 : 1),
        'name'        => $name,
        'email'       => $email,
        'pass_hash'   => password_hash($pass, PASSWORD_DEFAULT),
        'verified'    => false,
        'code_hash'   => password_hash($code, PASSWORD_DEFAULT),
        'code_expires'=> time() + CODE_TTL,
        'token'       => '',
        'tg_username' => '',
        'tg_pending'  => '',
        'tg_bonus'    => false,
        'created_at'  => isset($users[$i]['created_at']) ? $users[$i]['created_at'] : gmdate('Y-m-d H:i:s'),
        'client'      => $client,
    ];
    if ($i >= 0) $users[$i] = $record; else $users[] = $record;
    save_users($users);

    $sent = send_verify_code($email, $code);
    json_out(['ok' => true, 'pending' => true, 'email' => $email, 'name' => $name, 'sent' => $sent]);
}

// ================= VERIFY (код из почты) =================
if ($action === 'verify' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $name  = clean(isset($_POST['name']) ? $_POST['name'] : '', 20);
    $email = clean(isset($_POST['email']) ? $_POST['email'] : '', 120);
    $code  = clean(isset($_POST['code']) ? $_POST['code'] : '', 10);
    $client = clean(isset($_POST['client']) ? $_POST['client'] : '', 64);

    $users = users();
    $i = $email !== '' ? find_by_email($email) : find_by_name($name);
    if ($i < 0) json_out(['ok' => false, 'error' => 'no account'], 404);
    $u = &$users[$i];
    if (!empty($u['verified'])) json_out(['ok' => false, 'error' => 'already'], 409);
    if (time() > (int)$u['code_expires']) json_out(['ok' => false, 'error' => 'expired'], 410);
    if (!password_verify($code, $u['code_hash'])) json_out(['ok' => false, 'error' => 'bad code'], 401);

    $u['verified'] = true;
    $u['code_hash'] = '';
    $u['token'] = gen_token();
    if ($client !== '') $u['client'] = $client;
    save_users($users);

    json_out(['ok' => true, 'token' => $u['token'], 'name' => $u['name'], 'email' => $u['email'], 'tg_bonus' => !empty($u['tg_bonus'])]);
}

// ================= LOGIN =================
if ($action === 'login' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $ident = clean(isset($_POST['login']) ? $_POST['login'] : '', 120); // имя или email
    $pass  = isset($_POST['password']) ? $_POST['password'] : '';
    $client = clean(isset($_POST['client']) ? $_POST['client'] : '', 64);

    $users = users();
    $i = ($ident !== '' && strpos($ident, '@') !== false) ? find_by_email($ident) : find_by_name($ident);
    if ($i < 0) json_out(['ok' => false, 'error' => 'no account'], 404);
    $u = &$users[$i];
    if (!password_verify($pass, $u['pass_hash'])) json_out(['ok' => false, 'error' => 'bad password'], 401);
    if (empty($u['verified'])) json_out(['ok' => false, 'error' => 'unverified'], 403);

    $u['token'] = gen_token();
    if ($client !== '') $u['client'] = $client;
    save_users($users);

    json_out(['ok' => true, 'token' => $u['token'], 'name' => $u['name'], 'email' => $u['email'], 'tg_bonus' => !empty($u['tg_bonus'])]);
}

// ================= RESEND =================
if ($action === 'resend' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $email = clean(isset($_POST['email']) ? $_POST['email'] : '', 120);
    $users = users();
    $i = find_by_email($email);
    if ($i < 0) json_out(['ok' => false, 'error' => 'no account'], 404);
    if (!empty($users[$i]['verified'])) json_out(['ok' => false, 'error' => 'already'], 409);
    $code = gen_code();
    $users[$i]['code_hash'] = password_hash($code, PASSWORD_DEFAULT);
    $users[$i]['code_expires'] = time() + CODE_TTL;
    save_users($users);
    $sent = send_verify_code($email, $code);
    json_out(['ok' => true, 'sent' => $sent]);
}

// ================= ME (автовход / профиль + свои сейвы) =================
if ($action === 'me' && ($_SERVER['REQUEST_METHOD'] === 'GET' || $_SERVER['REQUEST_METHOD'] === 'POST')) {
    $token = isset($_SERVER['HTTP_X_AUTH_TOKEN']) ? $_SERVER['HTTP_X_AUTH_TOKEN'] : (isset($_POST['token']) ? $_POST['token'] : '');
    $users = users();
    $i = find_by_token($token);
    if ($i < 0) json_out(['ok' => false, 'error' => 'no session'], 401);
    $u = $users[$i];

    // Свои сейвы: по owner_user_id (надёжно). Имя автора больше не критерий владения.
    $items = load_json(__DIR__ . '/uploads/index.json');
    $mine = [];
    $uid = (int)$u['id'];
    $myName = norm_name($u['name']);
    foreach ($items as $it) {
        $owned = !empty($it['owner_user_id']) && (int)$it['owner_user_id'] === $uid;
        if (!$owned && empty($it['owner_user_id'])) {
            $on = norm_name(isset($it['owner_name']) ? $it['owner_name'] : '');
            $an = norm_name(isset($it['author']) ? $it['author'] : '');
            $owned = ($on !== '' && $on === $myName) || ($an !== '' && $an === $myName);
        }
        if (!$owned) continue;
        $mine[] = [
            'id' => (int)$it['id'],
            'title' => isset($it['title']) ? $it['title'] : '',
            'description' => isset($it['description']) ? $it['description'] : '',
            'downloads' => (int)($it['downloads'] ?? 0),
            'likes' => (int)($it['likes'] ?? 0),
            'owner_key' => isset($it['owner_key']) ? $it['owner_key'] : '',
            'has_cover' => !empty($it['cover']),
            'created_at' => isset($it['created_at']) ? $it['created_at'] : '',
        ];
    }
    json_out([
        'ok' => true,
        'name' => $u['name'],
        'email' => $u['email'],
        'tg' => $u['tg_username'],
        'tg_bonus' => !empty($u['tg_bonus']),
        'verified' => !empty($u['verified']),
        'saves' => $mine,
    ]);
}

// ================= LOGOUT =================
if ($action === 'logout' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $token = isset($_POST['token']) ? $_POST['token'] : '';
    $users = users();
    $i = find_by_token($token);
    if ($i >= 0) { $users[$i]['token'] = ''; save_users($users); }
    json_out(['ok' => true]);
}

// ================= TG LINK + бонус 5 BTC =================
if ($action === 'tg_link' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $token = isset($_POST['token']) ? $_POST['token'] : '';
    $tg    = clean(isset($_POST['telegram']) ? $_POST['telegram'] : '', 64);
    $users = users();
    $i = find_by_token($token);
    if ($i < 0) json_out(['ok' => false, 'error' => 'no session'], 401);
    if ($tg === '') json_out(['ok' => false, 'error' => 'no telegram'], 400);
    if (strpos($tg, '@') !== 0) $tg = '@' . $tg;
    $users[$i]['tg_username'] = $tg;

    // С ботом — создаём pending-код и отдаём deep-link на БОТА (ссылка для запуска
    // бота с кодом). Подтверждение приходит через webhook (telegram_webhook.php),
    // т.к. byethost не имеет исходящего доступа к api.telegram.org.
    if (TELEGRAM_BOT_TOKEN !== '') {
        $code = bin2hex(random_bytes(4));
        $bot = TELEGRAM_BOT_USERNAME !== '' ? str_replace('@', '', TELEGRAM_BOT_USERNAME) : '';
        if ($bot === '') {
            // Если юзернейм бота не задан — попробуем вытащить из токена не можем,
            // отдаём мягкую привязку, чтобы не ломать фичу.
            $granted = empty($users[$i]['tg_bonus']);
            if ($granted) $users[$i]['tg_bonus'] = true;
            save_users($users);
            json_out(['ok' => true, 'pending' => false, 'tg' => $tg, 'tg_bonus' => true, 'granted' => $granted, 'btc' => $granted ? 5 : 0]);
        }
        $link = 'https://t.me/' . $bot . '?start=' . $code;
        $pending = load_json(TG_PENDING_FILE);
        $pending[$code] = ['user' => (int)$users[$i]['id'], 'email' => $users[$i]['email'], 'at' => time()];
        save_json(TG_PENDING_FILE, $pending);
        // Не выдаём бонус сразу — ждём подтверждения ботом.
        save_users($users);
        json_out(['ok' => true, 'pending' => true, 'link' => $link, 'tg' => $tg]);
    }

    // Без бота — мягкая привязка, бонус выдаётся сразу (один раз).
    $granted = empty($users[$i]['tg_bonus']);
    if ($granted) $users[$i]['tg_bonus'] = true;
    save_users($users);
    json_out(['ok' => true, 'pending' => false, 'tg' => $tg, 'tg_bonus' => true, 'granted' => $granted, 'btc' => $granted ? 5 : 0]);
}

// ================= TG BONUS (отдельно) =================
if ($action === 'tg_bonus' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $token = isset($_POST['token']) ? $_POST['token'] : '';
    $users = users();
    $i = find_by_token($token);
    if ($i < 0) json_out(['ok' => false, 'error' => 'no session'], 401);
    if (!empty($users[$i]['tg_bonus'])) json_out(['ok' => false, 'error' => 'already'], 409);
    $users[$i]['tg_bonus'] = true;
    save_users($users);
    json_out(['ok' => true, 'granted' => true, 'btc' => 5]);
}

json_out(['ok' => false, 'error' => 'unknown action'], 400);
