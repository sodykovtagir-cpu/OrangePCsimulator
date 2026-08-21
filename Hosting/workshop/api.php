<?php
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, X-Upload-Key');
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

$config = __DIR__ . '/config.php';
if (!is_file($config)) {
    json_out(['ok' => false, 'error' => 'config.php missing'], 500);
}
require $config;

$action = isset($_GET['action']) ? $_GET['action'] : 'list';

try {
    $pdo = new PDO(
        'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
        DB_USER,
        DB_PASS,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (Exception $e) {
    json_out(['ok' => false, 'error' => 'db'], 500);
}

if ($action === 'list') {
    $st = $pdo->query('SELECT id, title, author, description, size_bytes, created_at, downloads FROM saves ORDER BY id DESC LIMIT 200');
    json_out(['ok' => true, 'items' => $st->fetchAll(PDO::FETCH_ASSOC)]);
}

if ($action === 'download') {
    $id = isset($_GET['id']) ? (int)$_GET['id'] : 0;
    $st = $pdo->prepare('SELECT filename FROM saves WHERE id = ?');
    $st->execute([$id]);
    $row = $st->fetch(PDO::FETCH_ASSOC);
    if (!$row) {
        json_out(['ok' => false, 'error' => 'not found'], 404);
    }
    $path = UPLOADS_DIR . '/' . basename($row['filename']);
    if (!is_file($path)) {
        json_out(['ok' => false, 'error' => 'file missing'], 404);
    }
    $pdo->prepare('UPDATE saves SET downloads = downloads + 1 WHERE id = ?')->execute([$id]);
    header('Content-Type: application/octet-stream');
    header('Content-Disposition: attachment; filename="' . basename($row['filename']) . '"');
    header('Content-Length: ' . filesize($path));
    readfile($path);
    exit;
}

if ($action === 'upload' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $key = '';
    if (isset($_SERVER['HTTP_X_UPLOAD_KEY'])) $key = $_SERVER['HTTP_X_UPLOAD_KEY'];
    if (isset($_POST['key'])) $key = $_POST['key'];
    if (UPLOAD_KEY !== '' && $key !== UPLOAD_KEY) {
        json_out(['ok' => false, 'error' => 'bad key'], 403);
    }

    $ip = isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : '0';
    $rateFile = sys_get_temp_dir() . '/opc_ws_' . md5($ip);
    $hits = 0;
    if (is_file($rateFile) && filemtime($rateFile) > time() - 3600) {
        $hits = (int)file_get_contents($rateFile);
    } else {
        @unlink($rateFile);
    }
    if ($hits >= RATE_PER_HOUR) {
        json_out(['ok' => false, 'error' => 'rate limit'], 429);
    }

    if (!isset($_FILES['file']) || $_FILES['file']['error'] !== UPLOAD_ERR_OK) {
        json_out(['ok' => false, 'error' => 'no file'], 400);
    }
    if ($_FILES['file']['size'] > MAX_BYTES) {
        json_out(['ok' => false, 'error' => 'too big'], 400);
    }

    $title = clean(isset($_POST['title']) ? $_POST['title'] : 'Save', 80);
    $author = clean(isset($_POST['author']) ? $_POST['author'] : 'Player', 40);
    $desc = clean(isset($_POST['description']) ? $_POST['description'] : '', 280);

    if (!is_dir(UPLOADS_DIR)) {
        mkdir(UPLOADS_DIR, 0755, true);
    }

    $name = 's' . time() . '_' . bin2hex(random_bytes(4)) . '.opc';
    $dest = UPLOADS_DIR . '/' . $name;
    if (!move_uploaded_file($_FILES['file']['tmp_name'], $dest)) {
        json_out(['ok' => false, 'error' => 'save fail'], 500);
    }

    $st = $pdo->prepare('INSERT INTO saves (title, author, description, filename, size_bytes) VALUES (?,?,?,?,?)');
    $st->execute([$title, $author, $desc, $name, filesize($dest)]);
    file_put_contents($rateFile, (string)($hits + 1));
    json_out(['ok' => true, 'id' => (int)$pdo->lastInsertId()]);
}

json_out(['ok' => false, 'error' => 'unknown action'], 400);

function clean($s, $max) {
    $s = trim(preg_replace('/\s+/', ' ', strip_tags((string)$s)));
    if (function_exists('mb_substr')) return mb_substr($s, 0, $max);
    return substr($s, 0, $max);
}

function json_out($data, $code = 200) {
    http_response_code($code);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($data, JSON_UNESCAPED_UNICODE);
    exit;
}
