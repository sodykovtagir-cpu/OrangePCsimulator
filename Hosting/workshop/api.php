<?php
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, X-Upload-Key');
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') { http_response_code(204); exit; }

$cfgPath = __DIR__ . '/config.php';
if (is_file($cfgPath)) require $cfgPath;
if (!defined('MAX_BYTES')) define('MAX_BYTES', 10485760); // 10 MB
if (!defined('MAX_COVER')) define('MAX_COVER', 300000);
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
function pub($it) {
    unset($it['owner_key'], $it['liked'], $it['ip']);
    if (empty($it['likes'])) $it['likes'] = 0;
    if (empty($it['downloads'])) $it['downloads'] = 0;
    $it['has_cover'] = !empty($it['cover']);
    return $it;
}
function find_item(&$items, $id) {
    foreach ($items as $i => $it) {
        if ((int)$it['id'] === $id) return $i;
    }
    return -1;
}
function save_cover($id) {
    if (!isset($_FILES['cover']) || $_FILES['cover']['error'] !== UPLOAD_ERR_OK) return '';
    if ($_FILES['cover']['size'] > MAX_COVER) return '';
    $tmp = $_FILES['cover']['tmp_name'];
    $name = 'c' . $id . '.jpg';
    $dest = UPLOADS_DIR . '/' . $name;
    if (!is_uploaded_file($tmp)) return '';
    if (!move_uploaded_file($tmp, $dest)) return '';
    return $name;
}

if ($action === 'list') {
    $out = [];
    foreach (load_index($INDEX) as $it) $out[] = pub($it);
    json_out(['ok' => true, 'items' => $out]);
}

if ($action === 'cover') {
    $id = isset($_GET['id']) ? (int)$_GET['id'] : 0;
    $items = load_index($INDEX);
    $i = find_item($items, $id);
    if ($i < 0 || empty($items[$i]['cover'])) { http_response_code(404); exit; }
    $path = UPLOADS_DIR . '/' . basename($items[$i]['cover']);
    if (!is_file($path)) { http_response_code(404); exit; }
    header('Content-Type: image/jpeg');
    header('Content-Length: ' . filesize($path));
    readfile($path);
    exit;
}

if ($action === 'download') {
    $id = isset($_GET['id']) ? (int)$_GET['id'] : 0;
    $items = load_index($INDEX);
    $i = find_item($items, $id);
    if ($i < 0) json_out(['ok' => false, 'error' => 'not found'], 404);
    $path = UPLOADS_DIR . '/' . basename($items[$i]['filename']);
    if (!is_file($path)) json_out(['ok' => false, 'error' => 'file missing'], 404);
    $items[$i]['downloads'] = isset($items[$i]['downloads']) ? $items[$i]['downloads'] + 1 : 1;
    save_index($INDEX, $items);
    header('Content-Type: application/octet-stream');
    header('Content-Disposition: attachment; filename="' . basename($items[$i]['filename']) . '"');
    header('Content-Length: ' . filesize($path));
    readfile($path);
    exit;
}

if ($action === 'like' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $id = isset($_POST['id']) ? (int)$_POST['id'] : 0;
    $client = clean(isset($_POST['client']) ? $_POST['client'] : '', 64);
    if ($client === '') json_out(['ok' => false, 'error' => 'no client'], 400);
    $items = load_index($INDEX);
    $i = find_item($items, $id);
    if ($i < 0) json_out(['ok' => false, 'error' => 'not found'], 404);
    if (!isset($items[$i]['liked']) || !is_array($items[$i]['liked'])) $items[$i]['liked'] = [];
    $hash = hash('sha256', $client);
    if (!in_array($hash, $items[$i]['liked'], true)) {
        $items[$i]['liked'][] = $hash;
    }
    $items[$i]['likes'] = count($items[$i]['liked']);
    save_index($INDEX, $items);
    json_out(['ok' => true, 'likes' => $items[$i]['likes']]);
}

if ($action === 'upload' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $gate = isset($_SERVER['HTTP_X_UPLOAD_KEY']) ? $_SERVER['HTTP_X_UPLOAD_KEY'] : (isset($_POST['key']) ? $_POST['key'] : '');
    if (UPLOAD_KEY !== '' && $gate !== UPLOAD_KEY) json_out(['ok' => false, 'error' => 'bad key'], 403);

    $ip = isset($_SERVER['REMOTE_ADDR']) ? $_SERVER['REMOTE_ADDR'] : '0';
    $rateFile = UPLOADS_DIR . '/rate_' . md5($ip);
    $hits = 0;
    if (is_file($rateFile) && filemtime($rateFile) > time() - 3600) $hits = (int)file_get_contents($rateFile);
    if ($hits >= RATE_PER_HOUR) json_out(['ok' => false, 'error' => 'rate limit'], 429);

    if (!isset($_FILES['file'])) json_out(['ok' => false, 'error' => 'no file'], 400);
    $ferr = $_FILES['file']['error'];
    if ($ferr === UPLOAD_ERR_INI_SIZE || $ferr === UPLOAD_ERR_FORM_SIZE) json_out(['ok' => false, 'error' => 'too big'], 400);
    if ($ferr !== UPLOAD_ERR_OK) json_out(['ok' => false, 'error' => 'no file'], 400);
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
    $owner = bin2hex(random_bytes(8));
    $cover = save_cover($id);
    $items[] = [
        'id' => $id,
        'title' => $title,
        'author' => $author,
        'description' => $desc,
        'filename' => $name,
        'cover' => $cover,
        'size_bytes' => filesize($dest),
        'created_at' => gmdate('Y-m-d H:i:s'),
        'downloads' => 0,
        'likes' => 0,
        'liked' => [],
        'owner_key' => $owner,
    ];
    save_index($INDEX, $items);
    file_put_contents($rateFile, (string)($hits + 1));
    json_out(['ok' => true, 'id' => $id, 'owner_key' => $owner]);
}

if ($action === 'update' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $id = isset($_POST['id']) ? (int)$_POST['id'] : 0;
    $owner = isset($_POST['owner_key']) ? $_POST['owner_key'] : '';
    $items = load_index($INDEX);
    $i = find_item($items, $id);
    if ($i < 0) json_out(['ok' => false, 'error' => 'not found'], 404);
    if (empty($items[$i]['owner_key']) || $items[$i]['owner_key'] !== $owner) json_out(['ok' => false, 'error' => 'forbidden'], 403);
    if (isset($_POST['title'])) $items[$i]['title'] = clean($_POST['title'], 80);
    if (isset($_POST['author'])) $items[$i]['author'] = clean($_POST['author'], 40);
    if (isset($_POST['description'])) $items[$i]['description'] = clean($_POST['description'], 280);
    if (isset($_FILES['file']) && $_FILES['file']['error'] === UPLOAD_ERR_OK) {
        if ($_FILES['file']['size'] > MAX_BYTES) json_out(['ok' => false, 'error' => 'too big'], 400);
        $dest = UPLOADS_DIR . '/' . basename($items[$i]['filename']);
        move_uploaded_file($_FILES['file']['tmp_name'], $dest);
        $items[$i]['size_bytes'] = filesize($dest);
    }
    $c = save_cover($id);
    if ($c !== '') $items[$i]['cover'] = $c;
    save_index($INDEX, $items);
    json_out(['ok' => true, 'id' => $id]);
}

if ($action === 'delete' && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $id = isset($_POST['id']) ? (int)$_POST['id'] : 0;
    $owner = isset($_POST['owner_key']) ? $_POST['owner_key'] : '';
    $items = load_index($INDEX);
    $i = find_item($items, $id);
    if ($i < 0) json_out(['ok' => false, 'error' => 'not found'], 404);
    if (empty($items[$i]['owner_key']) || $items[$i]['owner_key'] !== $owner) json_out(['ok' => false, 'error' => 'forbidden'], 403);
    @unlink(UPLOADS_DIR . '/' . basename($items[$i]['filename']));
    if (!empty($items[$i]['cover'])) @unlink(UPLOADS_DIR . '/' . basename($items[$i]['cover']));
    array_splice($items, $i, 1);
    save_index($INDEX, $items);
    json_out(['ok' => true]);
}

json_out(['ok' => false, 'error' => 'unknown action'], 400);
