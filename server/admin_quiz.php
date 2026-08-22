<?php
/**
 * Orange PC Simulator — Remote Quiz Admin Panel
 * -------------------------------------------------
 * Позволяет отправить квиз в игру с сайта (админка).
 * Игра опрашивает api.php?action=quiz&i=1 и показывает квиз одноразово.
 *
 * УСТАНОВКА:
 *   1. Залейте этот файл в папку, где лежит api.php мастерской (byethost).
 *   2. Откройте в браузере:  https://ВАШ_САЙТ/admin_quiz.php
 *   3. Войдите (пароль по умолчанию: admin123) — ОБЯЗАТЕЛЬНО поменяйте ниже!
 *   4. В api.php добавьте блок из api_quiz_snippet.php.
 */

$ADMIN_PASSWORD = 'admin123';   // <- поменяйте!
$DATA_FILE = __DIR__ . '/quiz_pending.json';

session_start();

$msg = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $act = isset($_POST['act']) ? $_POST['act'] : '';
    if ($act === 'login') {
        if (isset($_POST['pass']) && $_POST['pass'] === $ADMIN_PASSWORD) {
            $_SESSION['quiz_admin'] = true;
        } else {
            $msg = '<p class="err">Wrong password</p>';
        }
    } elseif ($act === 'logout') {
        unset($_SESSION['quiz_admin']);
    } elseif (!empty($_SESSION['quiz_admin']) && $act === 'send') {
        $link = trim(isset($_POST['link']) ? $_POST['link'] : '');
        if ($link === '') {
            $msg = '<p class="err">Link is required</p>';
        } else {
            $payload = array(
                'link'  => $link,
                'title' => trim(isset($_POST['title']) ? $_POST['title'] : ''),
                'body'  => trim(isset($_POST['body']) ? $_POST['body'] : ''),
            );
            $ok = @file_put_contents($DATA_FILE, json_encode($payload));
            $msg = $ok === false
                ? '<p class="err">Cannot write ' . htmlspecialchars($DATA_FILE) . ' — check folder permissions</p>'
                : '<p class="ok">Quiz sent. The game will show it on the next poll (one-shot).</p>';
        }
    } elseif (!empty($_SESSION['quiz_admin']) && $act === 'clear') {
        @file_put_contents($DATA_FILE, json_encode(array()));
        $msg = '<p class="ok">Pending quiz cleared.</p>';
    }
}

$pending = null;
if (file_exists($DATA_FILE)) {
    $pending = json_decode(@file_get_contents($DATA_FILE), true);
}
$logged = !empty($_SESSION['quiz_admin']);
?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Orange PC — Remote Quiz Admin</title>
<style>
  body { font-family: Arial, sans-serif; background:#1b1b1b; color:#eee; max-width:560px; margin:40px auto; padding:0 16px; }
  h1 { color:#ff8800; }
  input, textarea { width:100%; box-sizing:border-box; padding:8px; margin:6px 0 14px; border-radius:6px; border:1px solid #444; background:#2a2a2a; color:#eee; }
  button { background:#ff8800; color:#000; border:0; padding:10px 18px; border-radius:6px; font-weight:bold; cursor:pointer; }
  .ok { color:#5cd65c; } .err { color:#ff5c5c; }
  .card { background:#242424; padding:16px 18px; border-radius:10px; margin-bottom:16px; }
  label { font-size:13px; color:#aaa; }
</style>
</head>
<body>
<h1>Orange PC — Remote Quiz</h1>
<?php echo $msg; ?>
<?php if (!$logged): ?>
<div class="card">
  <form method="post">
    <input type="hidden" name="act" value="login">
    <label>Admin password</label>
    <input type="password" name="pass" autofocus>
    <button type="submit">Login</button>
  </form>
</div>
<?php else: ?>
<div class="card">
  <form method="post">
    <input type="hidden" name="act" value="send">
    <label>Link (обязательно)</label>
    <input type="text" name="link" placeholder="https://...">
    <label>Title (заголовок в игре)</label>
    <input type="text" name="title" placeholder="Quiz">
    <label>Body (текст в игре)</label>
    <textarea name="body" rows="3" placeholder="Answer and win!"></textarea>
    <button type="submit">Send to game</button>
  </form>
</div>
<div class="card">
  <p>Current pending:
  <?php if (!empty($pending['link'])): ?>
    <b><?php echo htmlspecialchars($pending['link']); ?></b>
    <form method="post" style="display:inline"><input type="hidden" name="act" value="clear"><button type="submit">Clear</button></form>
  <?php else: echo 'none'; endif; ?>
  </p>
</div>
<div class="card">
  <form method="post"><input type="hidden" name="act" value="logout"><button type="submit">Logout</button></form>
</div>
<?php endif; ?>
</body>
</html>
