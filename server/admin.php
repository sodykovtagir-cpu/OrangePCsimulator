<?php
/**
 * Orange PC Simulator — админка мастерской.
 *
 * Модерация сейвов, баны, игровые администраторы, квиз, релизы, пароль.
 *
 * Пароль хранится в admin_config.json как хеш. При первом запуске создаётся
 * значение по умолчанию admin123 — СМЕНИТЕ ЕГО на вкладке «Пароль».
 */

session_start();
require_once __DIR__ . '/game_roles.php';

define('DATA_DIR',    __DIR__);
define('INDEX_FILE',  DATA_DIR . '/uploads/index.json');
define('UPLOADS_DIR', DATA_DIR . '/uploads');
define('BAN_FILE',    DATA_DIR . '/banned.json');
define('QUIZ_FILE',   DATA_DIR . '/quiz_pending.json');
define('CONFIG_FILE', DATA_DIR . '/admin_config.json');
define('USERS_FILE',  DATA_DIR . '/users.json');
// Релизы лежат рядом с сайтом, на уровень выше мастерской.
define('FILES_DIR',   DATA_DIR . '/../files');

function load_json($f, $def = []) {
    if (!is_file($f)) return $def;
    $j = json_decode(@file_get_contents($f), true);
    return is_array($j) ? $j : $def;
}
function save_json($f, $data) {
    return @file_put_contents($f, json_encode($data, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT), LOCK_EX);
}
function is_admin()      { return !empty($_SESSION['workshop_admin']); }
function require_admin() { if (!is_admin()) { header('Location: admin.php'); exit; } }
function csrf_token()    { if (empty($_SESSION['csrf'])) $_SESSION['csrf'] = bin2hex(random_bytes(16)); return $_SESSION['csrf']; }
function csrf_ok()       { return isset($_POST['csrf']) && $_POST['csrf'] === ($_SESSION['csrf'] ?? ''); }
function clean($s, $max) {
    $s = trim(preg_replace('/\s+/', ' ', strip_tags((string)$s)));
    return function_exists('mb_substr') ? mb_substr($s, 0, $max) : substr($s, 0, $max);
}
function h($s) { return htmlspecialchars((string)$s, ENT_QUOTES, 'UTF-8'); }

/** Человеческий размер файла. */
function fsize($b) {
    if ($b <= 0) return '—';
    if ($b >= 1048576) return round($b / 1048576, 1) . ' МБ';
    if ($b >= 1024)    return round($b / 1024) . ' КБ';
    return $b . ' Б';
}

/** Предел загрузки, который реально позволяет сервер. */
function upload_limit_bytes() {
    $to_b = function ($v) {
        $v = trim((string)$v);
        if ($v === '') return 0;
        $n = (int)$v;
        switch (strtolower(substr($v, -1))) {
            case 'g': return $n * 1073741824;
            case 'm': return $n * 1048576;
            case 'k': return $n * 1024;
        }
        return $n;
    };
    $a = $to_b(ini_get('upload_max_filesize'));
    $b = $to_b(ini_get('post_max_size'));
    $vals = array_filter([$a, $b]);
    return $vals ? min($vals) : 0;
}

if (!is_file(CONFIG_FILE)) {
    save_json(CONFIG_FILE, ['hash' => password_hash('admin123', PASSWORD_DEFAULT), 'changed' => false]);
}
$cfg = load_json(CONFIG_FILE, ['hash' => '']);

$msg = '';
$msgType = 'ok';

// ===================== Действия =====================
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act = isset($_POST['act']) ? $_POST['act'] : '';

    if ($act === 'login') {
        $pass = isset($_POST['pass']) ? $_POST['pass'] : '';
        if (password_verify($pass, $cfg['hash'])) {
            $_SESSION['workshop_admin'] = true;
            session_regenerate_id(true);
        } else {
            $msg = 'Неверный пароль'; $msgType = 'err';
        }
    }
    elseif ($act === 'logout') {
        unset($_SESSION['workshop_admin']);
    }
    elseif (is_admin() && csrf_ok()) {

        if ($act === 'game_admin_set') {
            $id = isset($_POST['user_id']) ? (int)$_POST['user_id'] : 0;
            $enabled = isset($_POST['enabled']) && $_POST['enabled'] === '1';
            if (game_set_admin($id, $enabled)) {
                $msg = $enabled ? "Игровой администратор назначен: #$id" : "Права сняты: #$id";
            } else {
                $msg = 'Не удалось изменить роль. Проверьте ID, подтверждение почты и права на файл ролей.';
                $msgType = 'err';
            }
        }
        elseif ($act === 'delete') {
            $id = isset($_POST['id']) ? (int)$_POST['id'] : 0;
            $items = load_json(INDEX_FILE);
            foreach ($items as $i => $it) {
                if ((int)$it['id'] === $id) {
                    @unlink(UPLOADS_DIR . '/' . basename($it['filename']));
                    if (!empty($it['cover'])) @unlink(UPLOADS_DIR . '/' . basename($it['cover']));
                    array_splice($items, $i, 1);
                    save_json(INDEX_FILE, $items);
                    $msg = "Сейв #$id удалён"; break;
                }
            }
            if ($msg === '') { $msg = "Сейв #$id не найден"; $msgType = 'err'; }
        }
        /* Массовое удаление: когда налетел спам, по одному щёлкать больно. */
        elseif ($act === 'delete_bulk') {
            $ids = isset($_POST['ids']) && is_array($_POST['ids']) ? array_map('intval', $_POST['ids']) : [];
            if (!$ids) { $msg = 'Ничего не выбрано'; $msgType = 'err'; }
            else {
                $items = load_json(INDEX_FILE);
                $kept = []; $n = 0;
                foreach ($items as $it) {
                    if (in_array((int)$it['id'], $ids, true)) {
                        @unlink(UPLOADS_DIR . '/' . basename($it['filename']));
                        if (!empty($it['cover'])) @unlink(UPLOADS_DIR . '/' . basename($it['cover']));
                        $n++;
                    } else $kept[] = $it;
                }
                save_json(INDEX_FILE, $kept);
                $msg = "Удалено сейвов: $n";
            }
        }
        /* Переименование: иногда проще поправить название, чем сносить работу. */
        elseif ($act === 'rename') {
            $id = isset($_POST['id']) ? (int)$_POST['id'] : 0;
            $title = clean(isset($_POST['title']) ? $_POST['title'] : '', 80);
            if ($title === '') { $msg = 'Пустое название'; $msgType = 'err'; }
            else {
                $items = load_json(INDEX_FILE); $found = false;
                foreach ($items as &$it) {
                    if ((int)$it['id'] === $id) { $it['title'] = $title; $found = true; break; }
                }
                unset($it);
                if ($found) { save_json(INDEX_FILE, $items); $msg = "Сейв #$id переименован"; }
                else { $msg = "Сейв #$id не найден"; $msgType = 'err'; }
            }
        }
        elseif ($act === 'ban') {
            $type = isset($_POST['btype']) ? ($_POST['btype'] === 'ip' ? 'ip' : 'author') : 'author';
            $value = clean(isset($_POST['value']) ? $_POST['value'] : '', 64);
            $reason = clean(isset($_POST['reason']) ? $_POST['reason'] : '', 120);
            if ($value === '') { $msg = 'Укажите значение для бана'; $msgType = 'err'; }
            else {
                $bans = load_json(BAN_FILE);
                $valueLower = strtolower($value); $dup = false;
                foreach ($bans as $b) {
                    if ($b['type'] === $type && strtolower($b['value']) === $valueLower) { $dup = true; break; }
                }
                if ($dup) { $msg = 'Такой бан уже есть'; $msgType = 'err'; }
                else {
                    $bans[] = ['type' => $type, 'value' => $value, 'reason' => $reason, 'at' => gmdate('Y-m-d H:i:s')];
                    save_json(BAN_FILE, $bans);
                    $msg = ($type === 'ip' ? 'IP' : 'Автор') . " забанен: $value";
                }
            }
        }
        elseif ($act === 'unban') {
            $idx = isset($_POST['idx']) ? (int)$_POST['idx'] : -1;
            $bans = load_json(BAN_FILE);
            if ($idx >= 0 && $idx < count($bans)) {
                $v = $bans[$idx]['value'];
                array_splice($bans, $idx, 1);
                save_json(BAN_FILE, $bans);
                $msg = "Бан снят: $v";
            }
        }
        elseif ($act === 'quiz_send') {
            $link = clean(isset($_POST['link']) ? $_POST['link'] : '', 300);
            if ($link === '') { $msg = 'Ссылка обязательна'; $msgType = 'err'; }
            else {
                $payload = [
                    'link'  => $link,
                    'title' => clean(isset($_POST['title']) ? $_POST['title'] : '', 80),
                    'body'  => clean(isset($_POST['body']) ? $_POST['body'] : '', 280),
                ];
                $ok = save_json(QUIZ_FILE, $payload);
                $msg = $ok === false ? 'Ошибка записи quiz_pending.json (права на папку?)' : 'Квиз отправлен. Игра заберёт его при следующем опросе.';
                if ($ok === false) $msgType = 'err';
            }
        }
        elseif ($act === 'quiz_clear') {
            save_json(QUIZ_FILE, []);
            $msg = 'Отложенный квиз очищен';
        }
        /* Релизы: заливаем сборку прямо отсюда, сайт подхватит её сам. */
        elseif ($act === 'release_upload') {
            if (!is_dir(FILES_DIR)) @mkdir(FILES_DIR, 0755, true);

            if (empty($_FILES['file']) || $_FILES['file']['error'] !== UPLOAD_ERR_OK) {
                $code = isset($_FILES['file']) ? $_FILES['file']['error'] : -1;
                $msg = $code === UPLOAD_ERR_INI_SIZE || $code === UPLOAD_ERR_FORM_SIZE
                    ? 'Файл больше, чем разрешает сервер (' . fsize(upload_limit_bytes()) . '). Залейте по FTP в папку files/'
                    : 'Загрузка не удалась (код ' . $code . ')';
                $msgType = 'err';
            } else {
                $name = basename($_FILES['file']['name']);
                $ext  = strtolower(pathinfo($name, PATHINFO_EXTENSION));
                if (!in_array($ext, ['exe', 'apk', 'zip'], true)) {
                    $msg = 'Можно загружать только exe, apk или zip'; $msgType = 'err';
                } else {
                    // Имя чистим, но версию и платформу сохраняем: по ним сайт
                    // строит подпись кнопки.
                    $safe = preg_replace('/[^A-Za-z0-9._-]/', '_', $name);
                    if (move_uploaded_file($_FILES['file']['tmp_name'], FILES_DIR . '/' . $safe)) {
                        $msg = "Релиз загружен: $safe";
                    } else {
                        $msg = 'Не удалось сохранить файл (права на папку files/?)'; $msgType = 'err';
                    }
                }
            }
        }
        elseif ($act === 'release_delete') {
            $name = basename(isset($_POST['name']) ? $_POST['name'] : '');
            $path = FILES_DIR . '/' . $name;
            if ($name !== '' && is_file($path) && @unlink($path)) $msg = "Файл удалён: $name";
            else { $msg = 'Не удалось удалить файл'; $msgType = 'err'; }
        }
        elseif ($act === 'pass') {
            $cur = isset($_POST['current']) ? $_POST['current'] : '';
            $new = isset($_POST['new']) ? $_POST['new'] : '';
            $new2 = isset($_POST['new2']) ? $_POST['new2'] : '';
            if (!password_verify($cur, $cfg['hash'])) { $msg = 'Текущий пароль неверен'; $msgType = 'err'; }
            elseif (strlen($new) < 6) { $msg = 'Новый пароль — минимум 6 символов'; $msgType = 'err'; }
            elseif ($new !== $new2) { $msg = 'Пароли не совпадают'; $msgType = 'err'; }
            else {
                save_json(CONFIG_FILE, ['hash' => password_hash($new, PASSWORD_DEFAULT), 'changed' => true]);
                $cfg['hash'] = '';
                $msg = 'Пароль изменён';
            }
        }
    }
    elseif (is_admin() && !csrf_ok()) {
        $msg = 'CSRF-проверка не пройдена'; $msgType = 'err';
    }
}

// ===================== Данные =====================
$items       = load_json(INDEX_FILE);
$bans        = load_json(BAN_FILE);
$pendingQuiz = load_json(QUIZ_FILE, null);
$users       = load_json(USERS_FILE);
$adminIds    = function_exists('game_admin_ids') ? game_admin_ids() : [];

$releases = [];
if (is_dir(FILES_DIR)) {
    foreach (scandir(FILES_DIR) as $f) {
        if ($f === '.' || $f === '..') continue;
        $p = FILES_DIR . '/' . $f;
        if (!is_file($p)) continue;
        if (!in_array(strtolower(pathinfo($f, PATHINFO_EXTENSION)), ['exe','apk','zip'], true)) continue;
        $releases[] = ['name' => $f, 'size' => filesize($p), 'time' => filemtime($p)];
    }
    usort($releases, function ($a, $b) { return $b['time'] <=> $a['time']; });
}

$totalDl = 0; $totalLikes = 0; $totalBytes = 0;
foreach ($items as $it) {
    $totalDl    += (int)($it['downloads'] ?? 0);
    $totalLikes += (int)($it['likes'] ?? 0);
    $totalBytes += (int)($it['size_bytes'] ?? 0);
}
$verified = 0;
foreach ($users as $u) if (!empty($u['verified'])) $verified++;

// топ-5 по скачиваниям — видно, что у людей заходит
$top = $items;
usort($top, function ($a, $b) { return (int)($b['downloads'] ?? 0) <=> (int)($a['downloads'] ?? 0); });
$top = array_slice($top, 0, 5);
$maxDl = $top ? max(1, (int)($top[0]['downloads'] ?? 1)) : 1;

$tab = isset($_GET['tab']) ? $_GET['tab'] : 'dashboard';
$defaultPass = empty($cfg['changed']);
?>
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Orange PC — админка</title>
<style>
  *{box-sizing:border-box;margin:0;padding:0}
  :root{
    --bg:#11141a; --panel:#1b1f27; --panel2:#222732; --line:#2e3540;
    --text:#e9edf2; --dim:#94a0ae; --orange:#ff8800; --orange2:#ffb347;
    --green:#3ecf6e; --red:#ff5c5c; --blue:#4fa3ff;
  }
  body{
    font-family:"Segoe UI",system-ui,-apple-system,sans-serif;background:var(--bg);color:var(--text);
    min-height:100vh;
    background-image:radial-gradient(900px 500px at 80% -10%,rgba(255,136,0,.10),transparent 60%),
                     radial-gradient(700px 400px at 0% 100%,rgba(79,163,255,.08),transparent 60%);
  }
  a{color:var(--orange2);text-decoration:none}
  a:hover{text-decoration:underline}

  /* ---------- шапка ---------- */
  header{
    position:sticky;top:0;z-index:50;display:flex;align-items:center;gap:14px;
    padding:13px 20px;background:rgba(17,20,26,.9);backdrop-filter:blur(10px);
    border-bottom:1px solid var(--line);
  }
  .logo{
    width:38px;height:38px;border-radius:10px;display:grid;place-items:center;flex:none;
    background:linear-gradient(145deg,var(--orange),#cc6a00);color:#1a1204;font-weight:800;font-size:15px;
    box-shadow:0 5px 16px rgba(255,136,0,.32);
  }
  header h1{font-size:16px;font-weight:600;line-height:1.2}
  header h1 small{display:block;font-size:11.5px;color:var(--dim);font-weight:400}
  .spacer{flex:1}
  .badge{font-size:11.5px;padding:4px 10px;border-radius:20px;background:rgba(62,207,110,.15);
         color:var(--green);border:1px solid rgba(62,207,110,.3)}

  .wrap{max-width:1180px;margin:0 auto;padding:20px}

  /* ---------- вкладки ---------- */
  nav{display:flex;gap:7px;flex-wrap:wrap;margin-bottom:20px}
  nav a{
    padding:9px 16px;border-radius:9px;background:var(--panel);border:1px solid var(--line);
    color:var(--text);font-size:13.5px;text-decoration:none;position:relative;
    transition:transform .14s ease,background .14s,border-color .14s;
  }
  nav a:hover{background:var(--panel2);transform:translateY(-1px);text-decoration:none}
  nav a.on{
    background:linear-gradient(145deg,var(--orange),#cc6a00);color:#1a1204;font-weight:600;
    border-color:transparent;box-shadow:0 5px 16px rgba(255,136,0,.3);
  }
  nav a .n{
    display:inline-block;margin-left:7px;padding:1px 7px;border-radius:10px;font-size:11px;
    background:rgba(255,255,255,.12);
  }
  nav a.on .n{background:rgba(0,0,0,.18)}

  /* ---------- карточки ---------- */
  .card{
    background:var(--panel);border:1px solid var(--line);border-radius:13px;padding:18px;
    margin-bottom:16px;animation:rise .32s ease both;
  }
  @keyframes rise{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:none}}
  .card h2{font-size:15px;margin-bottom:4px}
  .card .sub{color:var(--dim);font-size:13px;margin-bottom:14px;line-height:1.5}

  .stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px;margin-bottom:16px}
  .stat{
    background:var(--panel);border:1px solid var(--line);border-radius:13px;padding:16px;
    animation:rise .32s ease both;transition:transform .16s,border-color .16s;
  }
  .stat:hover{transform:translateY(-3px);border-color:rgba(255,136,0,.45)}
  .stat b{display:block;font-size:26px;font-weight:600;line-height:1.1;margin-bottom:3px}
  .stat span{color:var(--dim);font-size:12.5px}
  .stat.o b{color:var(--orange2)} .stat.g b{color:var(--green)} .stat.b b{color:var(--blue)}

  .bars{display:grid;gap:9px}
  .bar .t{display:flex;justify-content:space-between;font-size:13px;margin-bottom:4px}
  .bar .t span:last-child{color:var(--dim)}
  .bar .track{height:7px;border-radius:5px;background:var(--panel2);overflow:hidden}
  .bar .fill{
    height:100%;border-radius:5px;background:linear-gradient(90deg,var(--orange),var(--orange2));
    animation:grow .7s cubic-bezier(.2,.8,.3,1) both;
  }
  @keyframes grow{from{width:0}}

  /* ---------- таблицы ---------- */
  table{width:100%;border-collapse:collapse;font-size:13.5px}
  th{text-align:left;color:var(--dim);font-weight:500;font-size:12px;text-transform:uppercase;
     letter-spacing:.4px;padding:0 10px 9px;border-bottom:1px solid var(--line)}
  td{padding:11px 10px;border-bottom:1px solid #232a34;vertical-align:middle}
  tbody tr{transition:background .13s}
  tbody tr:hover{background:rgba(255,255,255,.035)}
  .cover{width:54px;height:36px;border-radius:5px;background:#262c36 center/cover no-repeat;flex:none}
  .muted{color:var(--dim);font-size:12px}
  .nick{font-weight:600}
  .nick.adm{color:var(--red)}

  input,textarea,select{
    width:100%;padding:10px 12px;border-radius:9px;background:#141820;border:1px solid var(--line);
    color:var(--text);font-size:13.5px;font-family:inherit;transition:border-color .14s,box-shadow .14s;
  }
  input:focus,textarea:focus,select:focus{
    outline:none;border-color:var(--orange);box-shadow:0 0 0 3px rgba(255,136,0,.14);
  }
  label{display:block;font-size:12.5px;color:var(--dim);margin:0 0 5px}
  .field{margin-bottom:12px}
  .row2{display:grid;grid-template-columns:1fr 1fr;gap:12px}

  .btn{
    display:inline-flex;align-items:center;gap:7px;padding:9px 16px;border-radius:9px;border:1px solid var(--line);
    background:var(--panel2);color:var(--text);font-size:13.5px;cursor:pointer;font-family:inherit;
    transition:transform .13s,background .13s,border-color .13s;
  }
  .btn:hover{background:#2b323d;transform:translateY(-1px)}
  .btn:active{transform:translateY(0)}
  .btn.primary{background:linear-gradient(145deg,var(--orange),#cc6a00);color:#1a1204;border-color:transparent;font-weight:600}
  .btn.danger{background:rgba(255,92,92,.14);color:var(--red);border-color:rgba(255,92,92,.35)}
  .btn.danger:hover{background:rgba(255,92,92,.24)}
  .btn.ok{background:rgba(62,207,110,.14);color:var(--green);border-color:rgba(62,207,110,.35)}
  .btn.sm{padding:6px 11px;font-size:12.5px}

  /* ---------- уведомление ---------- */
  .toast{
    position:fixed;right:20px;bottom:20px;z-index:100;max-width:380px;
    padding:13px 17px;border-radius:11px;font-size:13.5px;line-height:1.45;
    background:var(--panel);border:1px solid var(--line);box-shadow:0 14px 40px rgba(0,0,0,.5);
    animation:toastIn .32s cubic-bezier(.2,.9,.3,1) both;
  }
  .toast.ok{border-left:3px solid var(--green)}
  .toast.err{border-left:3px solid var(--red)}
  @keyframes toastIn{from{opacity:0;transform:translateY(14px) scale(.97)}to{opacity:1;transform:none}}

  .warn{
    padding:12px 15px;border-radius:10px;font-size:13px;line-height:1.5;margin-bottom:16px;
    background:rgba(255,136,0,.1);border:1px solid rgba(255,136,0,.3);color:var(--orange2);
  }
  .empty{text-align:center;color:var(--dim);padding:34px 10px;font-size:13.5px}

  /* ---------- вход ---------- */
  .login{max-width:360px;margin:9vh auto;animation:rise .4s ease both}
  .login .logo{width:54px;height:54px;font-size:20px;margin:0 auto 16px;border-radius:14px}
  .login h1{text-align:center;font-size:19px;margin-bottom:5px}
  .login p{text-align:center;color:var(--dim);font-size:13px;margin-bottom:20px}

  .tools{display:flex;gap:9px;flex-wrap:wrap;align-items:center;margin-bottom:14px}
  .tools input[type=search]{flex:1;min-width:190px}
  .hide{display:none!important}
  @media (max-width:640px){
    .row2{grid-template-columns:1fr}
    .wrap{padding:14px}
    td,th{padding-left:6px;padding-right:6px}
  }
</style>
</head>
<body>

<?php if (!is_admin()): ?>
  <div class="login">
    <div class="logo">PC</div>
    <h1>Админка мастерской</h1>
    <p>Orange PC Simulator</p>
    <form method="post" class="card">
      <input type="hidden" name="act" value="login">
      <div class="field">
        <label>Пароль</label>
        <input type="password" name="pass" autofocus required>
      </div>
      <button class="btn primary" style="width:100%;justify-content:center">Войти</button>
    </form>
  </div>
  <?php if ($msg): ?><div class="toast <?php echo $msgType; ?>"><?php echo h($msg); ?></div><?php endif; ?>

<?php else: ?>
  <header>
    <div class="logo">PC</div>
    <h1>Orange PC <small>админка мастерской</small></h1>
    <div class="spacer"></div>
    <span class="badge">Админ</span>
    <form method="post" style="display:inline">
      <input type="hidden" name="act" value="logout">
      <button class="btn sm">Выйти</button>
    </form>
  </header>

  <div class="wrap">
    <?php if ($defaultPass): ?>
      <div class="warn">Пароль до сих пор стандартный. Смените его на вкладке «Пароль» — иначе в админку зайдёт кто угодно.</div>
    <?php endif; ?>

    <nav>
      <?php
      $tabs = [
        'dashboard' => ['Обзор', null],
        'saves'     => ['Сейвы', count($items)],
        'accounts'  => ['Аккаунты', count($users)],
        'banned'    => ['Баны', count($bans)],
        'releases'  => ['Релизы', count($releases)],
        'quiz'      => ['Квиз', null],
        'pass'      => ['Пароль', null],
      ];
      foreach ($tabs as $k => $t) {
          $on = $tab === $k ? ' class="on"' : '';
          echo '<a href="?tab=' . $k . '"' . $on . '>' . h($t[0]);
          if ($t[1] !== null) echo '<span class="n">' . (int)$t[1] . '</span>';
          echo '</a>';
      }
      ?>
    </nav>

    <?php $csrf = csrf_token(); ?>

    <?php if ($tab === 'dashboard'): ?>
      <div class="stats">
        <div class="stat o" style="animation-delay:.02s"><b><?php echo count($items); ?></b><span>сейвов в мастерской</span></div>
        <div class="stat g" style="animation-delay:.06s"><b><?php echo $totalDl; ?></b><span>скачиваний</span></div>
        <div class="stat" style="animation-delay:.1s"><b><?php echo $totalLikes; ?></b><span>лайков</span></div>
        <div class="stat b" style="animation-delay:.14s"><b><?php echo $verified; ?>/<?php echo count($users); ?></b><span>подтверждённых аккаунтов</span></div>
        <div class="stat" style="animation-delay:.18s"><b><?php echo count($bans); ?></b><span>банов</span></div>
        <div class="stat" style="animation-delay:.22s"><b><?php echo fsize($totalBytes); ?></b><span>занято сейвами</span></div>
      </div>

      <div class="card">
        <h2>Самые скачиваемые</h2>
        <div class="sub">Что людям заходит больше всего.</div>
        <?php if (!$top): ?><div class="empty">Пока пусто</div><?php else: ?>
          <div class="bars">
            <?php foreach ($top as $i => $t): $d = (int)($t['downloads'] ?? 0); ?>
              <div class="bar">
                <div class="t"><span><?php echo h($t['title'] ?? 'Без названия'); ?>
                  <span class="muted">· <?php echo h($t['author'] ?? '—'); ?></span></span>
                  <span><?php echo $d; ?></span></div>
                <div class="track"><div class="fill"
                  style="width:<?php echo max(3, round($d / $maxDl * 100)); ?>%;animation-delay:<?php echo .05 * $i; ?>s"></div></div>
              </div>
            <?php endforeach; ?>
          </div>
        <?php endif; ?>
      </div>

      <div class="card">
        <h2>Что где лежит</h2>
        <div class="sub">На случай, если понадобится залезть по FTP.</div>
        <table>
          <tr><td class="muted">Сейвы игроков</td><td><code>workshop/uploads/</code></td></tr>
          <tr><td class="muted">Список сейвов</td><td><code>workshop/uploads/index.json</code></td></tr>
          <tr><td class="muted">Аккаунты</td><td><code>workshop/users.json</code></td></tr>
          <tr><td class="muted">Релизы игры</td><td><code>files/</code></td></tr>
          <tr><td class="muted">Сайт</td><td><code>index.html</code> в корне</td></tr>
        </table>
      </div>

    <?php elseif ($tab === 'saves'): ?>
      <div class="card">
        <h2>Сейвы</h2>
        <div class="sub">Можно переименовать или удалить. Удаление уносит и файл, и обложку.</div>

        <?php if (!$items): ?><div class="empty">Мастерская пуста</div><?php else: ?>
          <form method="post" id="bulk">
            <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
            <input type="hidden" name="act" value="delete_bulk">
            <div class="tools">
              <input type="search" id="q" placeholder="Поиск по названию или автору…" oninput="filt('saveRow',this.value)">
              <button type="button" class="btn sm" onclick="allBoxes(true)">Выделить всё</button>
              <button type="button" class="btn sm" onclick="allBoxes(false)">Снять</button>
              <button class="btn sm danger" onclick="return confirm('Удалить выбранные сейвы?')">Удалить выбранные</button>
            </div>

            <table>
              <thead><tr>
                <th style="width:26px"></th><th style="width:60px">Обложка</th><th>Название</th>
                <th>Автор</th><th>↓</th><th>♥</th><th>Размер</th><th></th>
              </tr></thead>
              <tbody>
              <?php foreach ($items as $it):
                $id = (int)($it['id'] ?? 0);
                $cov = !empty($it['cover']) && is_file(UPLOADS_DIR . '/' . basename($it['cover']))
                     ? 'uploads/' . rawurlencode($it['cover']) : '';
              ?>
                <tr class="saveRow" data-s="<?php echo h(mb_strtolower(($it['title'] ?? '') . ' ' . ($it['author'] ?? ''))); ?>">
                  <td><input type="checkbox" name="ids[]" value="<?php echo $id; ?>" style="width:auto"></td>
                  <td><div class="cover" <?php if ($cov) echo 'style="background-image:url(\'' . h($cov) . '\')"'; ?>></div></td>
                  <td>
                    <div class="nick"><?php echo h($it['title'] ?? 'Без названия'); ?></div>
                    <div class="muted">#<?php echo $id; ?> · <?php echo h(substr($it['created_at'] ?? '', 0, 10)); ?></div>
                  </td>
                  <td><?php echo h($it['author'] ?? '—'); ?></td>
                  <td><?php echo (int)($it['downloads'] ?? 0); ?></td>
                  <td><?php echo (int)($it['likes'] ?? 0); ?></td>
                  <td class="muted"><?php echo fsize((int)($it['size_bytes'] ?? 0)); ?></td>
                  <td style="white-space:nowrap">
                    <button type="button" class="btn sm" onclick="ren(<?php echo $id; ?>,'<?php echo h(addslashes($it['title'] ?? '')); ?>')">Имя</button>
                    <button type="button" class="btn sm danger" onclick="del(<?php echo $id; ?>)">Удалить</button>
                  </td>
                </tr>
              <?php endforeach; ?>
              </tbody>
            </table>
          </form>

          <!-- отдельные формы: вложенные формы в HTML запрещены -->
          <form method="post" id="renForm" class="hide">
            <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
            <input type="hidden" name="act" value="rename">
            <input type="hidden" name="id" id="renId">
            <input type="hidden" name="title" id="renTitle">
          </form>
          <form method="post" id="delForm" class="hide">
            <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
            <input type="hidden" name="act" value="delete">
            <input type="hidden" name="id" id="delId">
          </form>
        <?php endif; ?>
      </div>

    <?php elseif ($tab === 'accounts'): ?>
      <div class="card">
        <h2>Аккаунты и администраторы</h2>
        <div class="sub">Роль получает подтверждённый аккаунт. Красный ник — администратор: игра берёт это с сервера при входе.</div>
        <div class="tools"><input type="search" placeholder="Поиск по нику или почте…" oninput="filt('accRow',this.value)"></div>
        <table>
          <thead><tr><th>ID</th><th>Ник</th><th>Почта</th><th>Подтверждён</th><th>Роль</th><th></th></tr></thead>
          <tbody>
          <?php foreach ($users as $u):
            $uid = (int)($u['id'] ?? 0);
            $isAdm = in_array($uid, $adminIds, true);
            $ver = !empty($u['verified']);
          ?>
            <tr class="accRow" data-s="<?php echo h(mb_strtolower(($u['name'] ?? '') . ' ' . ($u['email'] ?? ''))); ?>">
              <td class="muted"><?php echo $uid; ?></td>
              <td><span class="nick <?php echo $isAdm ? 'adm' : ''; ?>"><?php echo h($u['name'] ?? '—'); ?></span></td>
              <td class="muted"><?php echo h($u['email'] ?? '—'); ?></td>
              <td><?php echo $ver ? '<span style="color:var(--green)">да</span>' : '<span class="muted">нет</span>'; ?></td>
              <td><?php echo $isAdm ? 'Администратор' : 'Игрок'; ?></td>
              <td>
                <?php if (!$ver): ?>
                  <span class="muted">сначала почта</span>
                <?php else: ?>
                  <form method="post" style="display:inline">
                    <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
                    <input type="hidden" name="act" value="game_admin_set">
                    <input type="hidden" name="user_id" value="<?php echo $uid; ?>">
                    <input type="hidden" name="enabled" value="<?php echo $isAdm ? '0' : '1'; ?>">
                    <button class="btn sm <?php echo $isAdm ? 'danger' : 'ok'; ?>">
                      <?php echo $isAdm ? 'Снять админа' : 'Назначить админом'; ?>
                    </button>
                  </form>
                <?php endif; ?>
              </td>
            </tr>
          <?php endforeach; ?>
          </tbody>
        </table>
        <?php if (!$users): ?><div class="empty">Аккаунтов пока нет</div><?php endif; ?>
      </div>

    <?php elseif ($tab === 'banned'): ?>
      <div class="card">
        <h2>Новый бан</h2>
        <div class="sub">Бан по автору закрывает публикацию под этим ником, по IP — с этого адреса. Скачивать не мешает.</div>
        <form method="post">
          <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
          <input type="hidden" name="act" value="ban">
          <div class="row2">
            <div class="field"><label>Что банить</label>
              <select name="btype"><option value="author">Автор (ник)</option><option value="ip">IP-адрес</option></select>
            </div>
            <div class="field"><label>Значение</label><input name="value" required placeholder="ник или 1.2.3.4"></div>
          </div>
          <div class="field"><label>Причина (по желанию)</label><input name="reason" placeholder="спам"></div>
          <button class="btn primary">Забанить</button>
        </form>
      </div>
      <div class="card">
        <h2>Действующие баны</h2>
        <?php if (!$bans): ?><div class="empty">Банов нет</div><?php else: ?>
          <table>
            <thead><tr><th>Тип</th><th>Значение</th><th>Причина</th><th>Когда</th><th></th></tr></thead>
            <tbody>
            <?php foreach ($bans as $i => $b): ?>
              <tr>
                <td><?php echo $b['type'] === 'ip' ? 'IP' : 'Автор'; ?></td>
                <td class="nick"><?php echo h($b['value']); ?></td>
                <td class="muted"><?php echo h($b['reason'] ?? '—'); ?></td>
                <td class="muted"><?php echo h($b['at'] ?? ''); ?></td>
                <td>
                  <form method="post" style="display:inline">
                    <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
                    <input type="hidden" name="act" value="unban">
                    <input type="hidden" name="idx" value="<?php echo $i; ?>">
                    <button class="btn sm ok">Снять</button>
                  </form>
                </td>
              </tr>
            <?php endforeach; ?>
            </tbody>
          </table>
        <?php endif; ?>
      </div>

    <?php elseif ($tab === 'releases'): ?>
      <div class="card">
        <h2>Релизы игры</h2>
        <div class="sub">Файлы из этой папки сайт показывает в окне «Скачать мод». Пока папка пуста, на сайте остаются ссылки на телеграм.</div>
        <?php $lim = upload_limit_bytes(); ?>
        <div class="warn">Через браузер сервер примет файл не больше <b><?php echo fsize($lim); ?></b>.
          APK обычно тяжелее — такой заливайте по FTP в папку <code>files/</code>, список обновится сам.</div>
        <form method="post" enctype="multipart/form-data">
          <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
          <input type="hidden" name="act" value="release_upload">
          <div class="field"><label>Файл (exe, apk или zip)</label><input type="file" name="file" accept=".exe,.apk,.zip" required></div>
          <button class="btn primary">Загрузить</button>
        </form>
      </div>
      <div class="card">
        <h2>Что лежит сейчас</h2>
        <?php if (!$releases): ?><div class="empty">Файлов нет — на сайте показываются ссылки на телеграм</div><?php else: ?>
          <table>
            <thead><tr><th>Файл</th><th>Размер</th><th>Загружен</th><th></th></tr></thead>
            <tbody>
            <?php foreach ($releases as $r): ?>
              <tr>
                <td class="nick"><?php echo h($r['name']); ?></td>
                <td class="muted"><?php echo fsize($r['size']); ?></td>
                <td class="muted"><?php echo date('Y-m-d H:i', $r['time']); ?></td>
                <td>
                  <form method="post" style="display:inline" onsubmit="return confirm('Удалить файл?')">
                    <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
                    <input type="hidden" name="act" value="release_delete">
                    <input type="hidden" name="name" value="<?php echo h($r['name']); ?>">
                    <button class="btn sm danger">Удалить</button>
                  </form>
                </td>
              </tr>
            <?php endforeach; ?>
            </tbody>
          </table>
        <?php endif; ?>
      </div>

    <?php elseif ($tab === 'quiz'): ?>
      <div class="card">
        <h2>Квиз</h2>
        <div class="sub">Игра заберёт его при следующем опросе сервера и покажет игрокам.</div>
        <form method="post">
          <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
          <input type="hidden" name="act" value="quiz_send">
          <div class="field"><label>Ссылка</label><input name="link" required placeholder="https://..."></div>
          <div class="field"><label>Заголовок</label><input name="title" placeholder="Новый опрос"></div>
          <div class="field"><label>Текст</label><textarea name="body" rows="3" placeholder="Коротко, о чём это"></textarea></div>
          <button class="btn primary">Отправить</button>
        </form>
      </div>
      <div class="card">
        <h2>Сейчас в очереди</h2>
        <?php if (!$pendingQuiz): ?><div class="empty">Пусто</div><?php else: ?>
          <table>
            <tr><td class="muted">Ссылка</td><td><?php echo h($pendingQuiz['link'] ?? ''); ?></td></tr>
            <tr><td class="muted">Заголовок</td><td><?php echo h($pendingQuiz['title'] ?? ''); ?></td></tr>
            <tr><td class="muted">Текст</td><td><?php echo h($pendingQuiz['body'] ?? ''); ?></td></tr>
          </table>
          <form method="post" style="margin-top:13px">
            <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
            <input type="hidden" name="act" value="quiz_clear">
            <button class="btn danger">Очистить</button>
          </form>
        <?php endif; ?>
      </div>

    <?php elseif ($tab === 'pass'): ?>
      <div class="card" style="max-width:420px">
        <h2>Смена пароля</h2>
        <div class="sub">Пароль только от этой админки. Игровые аккаунты и деньги он не трогает.</div>
        <form method="post">
          <input type="hidden" name="csrf" value="<?php echo $csrf; ?>">
          <input type="hidden" name="act" value="pass">
          <div class="field"><label>Текущий</label><input type="password" name="current" required></div>
          <div class="field"><label>Новый (от 6 символов)</label><input type="password" name="new" required></div>
          <div class="field"><label>Ещё раз</label><input type="password" name="new2" required></div>
          <button class="btn primary">Сменить</button>
        </form>
      </div>
    <?php endif; ?>
  </div>

  <?php if ($msg): ?><div class="toast <?php echo $msgType; ?>" id="toast"><?php echo h($msg); ?></div><?php endif; ?>

  <script>
    /* Поиск по таблице: прячем строки, не подходящие под запрос. */
    function filt(cls, q){
      q = (q || '').toLowerCase().trim();
      document.querySelectorAll('.' + cls).forEach(function(tr){
        tr.classList.toggle('hide', q !== '' && (tr.dataset.s || '').indexOf(q) === -1);
      });
    }
    function allBoxes(on){
      document.querySelectorAll('#bulk input[type=checkbox]').forEach(function(c){
        if(!c.closest('tr').classList.contains('hide')) c.checked = on;
      });
    }
    /* Переименование: спрашиваем новое имя и отправляем отдельной формой,
       потому что вложенные формы в HTML недопустимы. */
    function ren(id, cur){
      var t = prompt('Новое название сейва:', cur);
      if(t === null) return;
      t = t.trim();
      if(t === '') return;
      document.getElementById('renId').value = id;
      document.getElementById('renTitle').value = t;
      document.getElementById('renForm').submit();
    }
    function del(id){
      if(!confirm('Удалить сейв #' + id + '? Файл и обложка тоже исчезнут.')) return;
      document.getElementById('delId').value = id;
      document.getElementById('delForm').submit();
    }
    var t = document.getElementById('toast');
    if(t) setTimeout(function(){
      t.style.transition = 'opacity .4s, transform .4s';
      t.style.opacity = '0'; t.style.transform = 'translateY(10px)';
      setTimeout(function(){ t.remove(); }, 420);
    }, 4200);
  </script>
<?php endif; ?>
</body>
</html>
