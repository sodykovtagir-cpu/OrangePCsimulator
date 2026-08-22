<?php
/**
 * Orange PC Simulator — АДМИНКА мастерской.
 * Модерация: удаление сейвов, бан пользователей (по нику и по IP), квиз, смена пароля.
 *
 * Пароль хранится в admin_config.json (hash). При первом запуске создаётся
 * со значением по умолчанию admin123 — СМЕНИТЕ ЕГО на вкладке «Пароль»!
 */

session_start();

define('DATA_DIR', __DIR__);
define('INDEX_FILE', DATA_DIR . '/uploads/index.json');
define('UPLOADS_DIR', DATA_DIR . '/uploads');
define('BAN_FILE', DATA_DIR . '/banned.json');
define('QUIZ_FILE', DATA_DIR . '/quiz_pending.json');
define('CONFIG_FILE', DATA_DIR . '/admin_config.json');

function load_json($f, $def = []) {
    if (!is_file($f)) return $def;
    $j = json_decode(@file_get_contents($f), true);
    return is_array($j) ? $j : $def;
}
function save_json($f, $data) {
    return @file_put_contents($f, json_encode($data, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT), LOCK_EX);
}
function is_admin() {
    return !empty($_SESSION['workshop_admin']);
}
function require_admin() {
    if (!is_admin()) {
        header('Location: admin.php');
        exit;
    }
}
function csrf_token() {
    if (empty($_SESSION['csrf'])) $_SESSION['csrf'] = bin2hex(random_bytes(16));
    return $_SESSION['csrf'];
}
function csrf_ok() {
    return isset($_POST['csrf']) && $_POST['csrf'] === ($_SESSION['csrf'] ?? '');
}
function clean($s, $max) {
    $s = trim(preg_replace('/\s+/', ' ', strip_tags((string)$s)));
    if (function_exists('mb_substr')) return mb_substr($s, 0, $max);
    return substr($s, 0, $max);
}

// --- Init config with default password ---
if (!is_file(CONFIG_FILE)) {
    save_json(CONFIG_FILE, ['hash' => password_hash('admin123', PASSWORD_DEFAULT), 'changed' => false]);
}
$cfg = load_json(CONFIG_FILE, ['hash' => '']);

$msg = '';
$msgType = 'ok';

// ================= Actions (POST) =================
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

        if ($act === 'delete') {
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
        elseif ($act === 'ban') {
            $type = isset($_POST['btype']) ? ($_POST['btype'] === 'ip' ? 'ip' : 'author') : 'author';
            $value = clean(isset($_POST['value']) ? $_POST['value'] : '', 64);
            $reason = clean(isset($_POST['reason']) ? $_POST['reason'] : '', 120);
            if ($value === '') { $msg = 'Укажите значение для бана'; $msgType = 'err'; }
            else {
                $bans = load_json(BAN_FILE);
                $valueLower = strtolower($value);
                $dup = false;
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

// ================= Data =================
$items = load_json(INDEX_FILE);
$bans = load_json(BAN_FILE);
$pendingQuiz = load_json(QUIZ_FILE, null);
$totalDl = 0; $totalLk = 0;
foreach ($items as $it) { $totalDl += (int)($it['downloads'] ?? 0); $totalLk += (int)($it['likes'] ?? 0); }

$tab = isset($_GET['tab']) ? preg_replace('/[^a-z]/', '', $_GET['tab']) : 'dashboard';
$tab = in_array($tab, ['dashboard', 'saves', 'banned', 'quiz', 'pass'], true) ? $tab : 'dashboard';

function size_fmt($b) {
    if ($b >= 1048576) return round($b / 1048576, 1) . ' MB';
    if ($b >= 1024) return round($b / 1024) . ' KB';
    return $b . ' B';
}
?>
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Orange PC Workshop — Админка</title>
<link rel="stylesheet" href="style.css">
</head>
<body>
<div class="wrap">
  <div class="topbar">
    <div class="brand">
      <div class="logo">O</div>
      <div>
        <h1>Orange PC <span style="color:var(--orange)">Admin</span></h1>
        <small>Модерация мастерской</small>
      </div>
    </div>
    <?php if (is_admin()): ?>
    <form method="post" style="display:flex;gap:8px;align-items:center">
      <span class="badge ok">Админ</span>
      <input type="hidden" name="act" value="logout">
      <button class="btn small" type="submit">Выйти</button>
    </form>
    <?php endif; ?>
  </div>

  <?php if ($msg !== ''): ?><div class="msg <?php echo $msgType; ?>"><?php echo htmlspecialchars($msg); ?></div><?php endif; ?>

  <?php if (!is_admin()): ?>
    <div class="panel" style="max-width:380px;margin:60px auto">
      <h2>Вход для модерации</h2>
      <form method="post">
        <input type="hidden" name="act" value="login">
        <label>Пароль админа</label>
        <input type="password" name="pass" autofocus required>
        <div style="height:12px"></div>
        <button class="btn primary" type="submit" style="width:100%">Войти</button>
      </form>
    </div>
  <?php else: ?>

  <div class="nav" style="margin-bottom:20px">
    <a class="<?php echo $tab==='dashboard'?'active':''; ?>" href="?tab=dashboard">Обзор</a>
    <a class="<?php echo $tab==='saves'?'active':''; ?>" href="?tab=saves">Сейвы (<?php echo count($items); ?>)</a>
    <a class="<?php echo $tab==='banned'?'active':''; ?>" href="?tab=banned">Баны (<?php echo count($bans); ?>)</a>
    <a class="<?php echo $tab==='quiz'?'active':''; ?>" href="?tab=quiz">Квиз</a>
    <a class="<?php echo $tab==='pass'?'active':''; ?>" href="?tab=pass">Пароль</a>
  </div>

  <?php if ($tab === 'dashboard'): ?>
    <div class="stats">
      <div class="stat"><div class="num"><?php echo count($items); ?></div><div class="lbl">Сейвов</div></div>
      <div class="stat"><div class="num"><?php echo $totalDl; ?></div><div class="lbl">Скачиваний</div></div>
      <div class="stat"><div class="num"><?php echo $totalLk; ?></div><div class="lbl">Лайков</div></div>
      <div class="stat"><div class="num"><?php echo count($bans); ?></div><div class="lbl">Банов</div></div>
    </div>
    <div class="panel">
      <h2>Быстрые действия</h2>
      <p class="sub">Переходы: <a href="?tab=saves">модерация сейвов</a> · <a href="?tab=banned">список банов</a> · <a href="?tab=quiz">отправить квиз</a>.</p>
      <p class="sub">Публичная витрина: <a href="index.php" target="_blank">index.php</a> · API: <a href="api.php?action=list" target="_blank">api.php?action=list</a></p>
      <?php if (empty($cfg['changed'])): ?>
      <div class="msg err" style="margin-top:10px">⚠️ Пароль админа всё ещё по умолчанию (<b>admin123</b>) — смените на вкладке «Пароль»!</div>
      <?php endif; ?>
    </div>
  <?php elseif ($tab === 'saves'): ?>
    <div class="panel">
      <h2>Сейвы</h2>
      <p class="sub">Удаление и баны применяются сразу.</p>
      <?php if (empty($items)): ?><div class="empty">Сейвов нет.</div><?php else: ?>
      <div class="table-wrap">
      <table>
        <tr><th>ID</th><th>Название</th><th>Автор</th><th>IP</th><th>Размер</th><th>Дата</th><th>⬇</th><th>♥</th><th>Действия</th></tr>
        <?php foreach (array_reverse($items) as $it):
          $id = (int)$it['id'];
          $author = htmlspecialchars($it['author'] ?? 'Player');
          $ip = htmlspecialchars($it['ip'] ?? '—');
          $title = htmlspecialchars($it['title'] ?? 'Untitled');
        ?>
        <tr>
          <td>#<?php echo $id; ?></td>
          <td><?php echo $title; ?></td>
          <td><span class="author"><?php echo $author; ?></span></td>
          <td><span class="ip"><?php echo $ip; ?></span></td>
          <td><?php echo size_fmt((int)($it['size_bytes'] ?? 0)); ?></td>
          <td><?php echo htmlspecialchars(substr($it['created_at'] ?? '', 0, 10)); ?></td>
          <td><?php echo (int)($it['downloads'] ?? 0); ?></td>
          <td><?php echo (int)($it['likes'] ?? 0); ?></td>
          <td style="white-space:nowrap">
            <form method="post" style="display:inline" onsubmit="return confirm('Удалить сейв #<?php echo $id; ?>?');">
              <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
              <input type="hidden" name="act" value="delete"><input type="hidden" name="id" value="<?php echo $id; ?>">
              <button class="btn small danger" type="submit">Удалить</button>
            </form>
            <form method="post" style="display:inline">
              <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
              <input type="hidden" name="act" value="ban"><input type="hidden" name="btype" value="author">
              <input type="hidden" name="value" value="<?php echo htmlspecialchars($it['author'] ?? 'Player'); ?>">
              <button class="btn small" type="submit">Бан автора</button>
            </form>
            <?php if (!empty($it['ip'])): ?>
            <form method="post" style="display:inline">
              <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
              <input type="hidden" name="act" value="ban"><input type="hidden" name="btype" value="ip">
              <input type="hidden" name="value" value="<?php echo htmlspecialchars($it['ip']); ?>">
              <button class="btn small" type="submit">Бан IP</button>
            </form>
            <?php endif; ?>
          </td>
        </tr>
        <?php endforeach; ?>
      </table>
      </div>
      <?php endif; ?>
    </div>
  <?php elseif ($tab === 'banned'): ?>
    <div class="panel">
      <h2>Баны</h2>
      <p class="sub">Забаненные не могут загружать/обновлять сейвы (проверка по нику и IP).</p>
      <form method="post" style="max-width:420px">
        <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
        <input type="hidden" name="act" value="ban">
        <label>Тип</label>
        <select name="btype"><option value="author">Автор (ник)</option><option value="ip">IP-адрес</option></select>
        <label>Значение</label>
        <input type="text" name="value" placeholder="ник или 1.2.3.4" required>
        <label>Причина (необязательно)</label>
        <input type="text" name="reason" placeholder="например: читерство">
        <div style="height:12px"></div>
        <button class="btn primary" type="submit">Забанить</button>
      </form>
      <?php if (empty($bans)): ?>
        <div class="empty">Банов нет.</div>
      <?php else: ?>
      <div class="table-wrap" style="margin-top:16px">
      <table>
        <tr><th>Тип</th><th>Значение</th><th>Причина</th><th>Когда</th><th></th></tr>
        <?php foreach ($bans as $i => $b): ?>
        <tr>
          <td><span class="badge banned"><?php echo $b['type'] === 'ip' ? 'IP' : 'Автор'; ?></span></td>
          <td><?php echo htmlspecialchars($b['value']); ?></td>
          <td><?php echo htmlspecialchars($b['reason'] ?? ''); ?></td>
          <td><?php echo htmlspecialchars($b['at'] ?? ''); ?></td>
          <td>
            <form method="post" style="display:inline">
              <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
              <input type="hidden" name="act" value="unban"><input type="hidden" name="idx" value="<?php echo $i; ?>">
              <button class="btn small" type="submit">Снять бан</button>
            </form>
          </td>
        </tr>
        <?php endforeach; ?>
      </table>
      </div>
      <?php endif; ?>
    </div>
  <?php elseif ($tab === 'quiz'): ?>
    <div class="panel" style="max-width:520px">
      <h2>Отправить квиз в игру</h2>
      <p class="sub">Игра опрашивает сервер каждые ~45 сек и покажет квиз один раз.</p>
      <form method="post">
        <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
        <input type="hidden" name="act" value="quiz_send">
        <label>Ссылка (обязательно)</label>
        <input type="text" name="link" placeholder="https://..." required>
        <label>Заголовок</label>
        <input type="text" name="title" placeholder="Quiz" value="<?php echo htmlspecialchars($pendingQuiz['title'] ?? ''); ?>">
        <label>Текст</label>
        <textarea name="body" rows="3" placeholder="Ответь и выиграй!"><?php echo htmlspecialchars($pendingQuiz['body'] ?? ''); ?></textarea>
        <div style="height:12px"></div>
        <button class="btn primary" type="submit">Отправить</button>
      </form>
      <?php if (!empty($pendingQuiz['link'])): ?>
      <div style="margin-top:14px;padding-top:12px;border-top:1px solid var(--line)">
        <span class="badge ok">Ожидает отправки</span>
        <span style="font-size:13px;color:var(--muted)"> <?php echo htmlspecialchars($pendingQuiz['link']); ?></span>
        <form method="post" style="display:inline">
          <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
          <input type="hidden" name="act" value="quiz_clear">
          <button class="btn small danger" type="submit">Очистить</button>
        </form>
      </div>
      <?php endif; ?>
    </div>
  <?php elseif ($tab === 'pass'): ?>
    <div class="panel" style="max-width:420px">
      <h2>Смена пароля админа</h2>
      <form method="post">
        <input type="hidden" name="csrf" value="<?php echo csrf_token(); ?>">
        <input type="hidden" name="act" value="pass">
        <label>Текущий пароль</label>
        <input type="password" name="current" required>
        <label>Новый пароль (мин. 6 символов)</label>
        <input type="password" name="new" required>
        <label>Повторите новый пароль</label>
        <input type="password" name="new2" required>
        <div style="height:12px"></div>
        <button class="btn primary" type="submit">Сменить пароль</button>
      </form>
    </div>
  <?php endif; ?>

  <div class="footer">Orange PC Simulator · <a href="index.php">витрина</a></div>
  <?php endif; ?>
</div>
</body>
</html>
