<?php
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, X-Upload-Key');
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

$cfgPath = __DIR__ . '/config.php';
if (is_file($cfgPath)) require $cfgPath;
if (!defined('MAX_BYTES')) define('MAX_BYTES', 1048576);
if (!defined('RATE_PER_HOUR')) define('RATE_PER_HOUR', 8);
if (!defined('UPLOAD_KEY')) define('UPLOAD_KEY', '');
if (!defined('UPLOADS_DIR')) define('UPLOADS_DIR', __DIR__ . '/uploads');
if (!is_dir(UPLOADS_DIR)) @mkdir(UPLOADS_DIR, 0755, true);

$INDEX = UPLOADS_DIR . '/index.json';
$action = isset($_GET['action']) ? $_GET['action'] : 'list';

function load_index($path) {
    if (!is_file($path)) return [];
    $j = json_decode(file_get_contents($path), true);
    return is_array($j) ? $j : [];
}
function save_index($path, $items) {
    file_put_contents($path, json_encode($items, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT), LOCK_EX);
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

if ($action === 'list') {
    json_out(['ok' => true, 'items' => load_index($INDEX)]);
}

if ($action === 'download') {
    $id = isset($_GET['id']) ? (int)$_GET['id'] : 0;
    $items = load_index($INDEX);
    $row = null;
    foreach ($items as $it) {
        if ((int)$it['id'] === $id) { $row = $it; break; }
    }
    if (!$row) json_out(['ok' => false, 'error' => 'not found'], 404);
    $path = UPLOADS_DIR . '/' . basename($row['filename']);
    if (!is_file($path)) json_out(['ok' => false, 'error' => 'file missing'], 404);
    foreach ($items as &$it) {
        if ((int)$it['id'] === $id) { $it['downloads'] = isset($it['downloads']) ? $it['downloads'] + 1 : 1; }
    }
    unset($it);
    save_index($INDEX, $items);
    header('Content-Type: application/octet-stream');
    header('Content-Disposition: attachment; filename="' . basename($row['filename']) . '"');
    header('Content-Length: ' . filesize($path));
    readfile($path);
    exit;
}

if ($action === 'upload' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $key = isset($_SERVER['HTTP_X_UPLOAD_KEY']) ? $_SERVER['HTTP_X_UPLOAD_KEY'] : (isset($_POST['key']) ? $_POST['key'] : '');
    if (UPLOAD_KEY !== '' && $key !== UPLOAD_KEY) json_out(['ok' => false, 'error' => 'bad key'], 403);

    $ip = isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : '0';
    $rateFile = UPLOADS_DIR . '/rate_' . md5($ip);
    $hits = 0;
    if (is_file($rateFile) && filemtime($rateFile) > time() - 3600) $hits = (int)file_get_contents($rateFile);
    if ($hits >= RATE_PER_HOUR) json_out(['ok' => false, 'error' => 'rate limit'], 429);

    if (!isset($_FILES['file']) || $_FILES['file']['error'] !== UPLOAD_ERR_OK) json_out(['ok' => false, 'error' => 'no file'], 400);
    if ($_FILES['file']['size'] > MAX_BYTES) json_out(['ok' => false, 'error' => 'too big'], 400);

    $title = clean(isset($_POST['title']) ? $_POST['title'] : 'Save', 80);
    $author = clean(isset($_POST['author']) ? $_POST['author'] : 'Player', 40);
    $desc = clean(isset($_POST['description']) ? $_POST['description'] : '', 280);

    $name = 's' . time() . '_' . bin2hex(random_bytes(3)) . '.opc';
    $dest = UPLOADS_DIR . '/' . $name;
    if (!move_uploaded_file($_FILES['file']['tmp_name'], $dest)) json_out(['ok' => false, 'error' => 'save fail'], 500);

    $items = load_index($INDEX);
    $id = 1;
    foreach ($items as $it) if ((int)$it['id'] >= $id) $id = (int)$it['id'] + 1;
    $items[] = [
        'id' => $id,
        'title' => $title,
        'author' => $author,
        'description' => $desc,
        'filename' => $name,
        'size_bytes' => filesize($dest),
        'created_at' => gmdate('Y-m-d H:i:s'),
        'downloads' => 0,
    ];
    save_index($INDEX, $items);
    file_put_contents($rateFile, (string)($hits + 1));
    json_out(['ok' => true, 'id' => $id]);
}

json_out(['ok' => false, 'error' => 'unknown action'], 400);
