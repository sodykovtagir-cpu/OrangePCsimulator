<?php
// Transport large saves through hosts with a smaller per-request PHP cap.
// Staging is authenticated by account + a random per-upload secret, expires
// after one hour, and is bounded to four sessions per account / 32 total.
define('WORKSHOP_CHUNK_BYTES', 8 * 1024 * 1024);

function workshop_chunk_dir() {
    $dir = UPLOADS_DIR . '/.chunks';
    if (!is_dir($dir) && !@mkdir($dir, 0700, true) && !is_dir($dir))
        json_out(['ok' => false, 'error' => 'staging unavailable'], 500);
    if (!is_file($dir . '/index.php'))
        @file_put_contents($dir . '/index.php', '<?php http_response_code(403); exit;');
    return $dir;
}
function workshop_chunk_meta($path) {
    $raw = @file_get_contents($path);
    // Metadata has a constant PHP exit prefix, so secrets are never served
    // even if a host ignores .htaccess. No uploaded content is executable.
    $prefix = "<?php exit; ?>\n";
    if ($raw === false || substr($raw, 0, strlen($prefix)) !== $prefix) return null;
    return json_decode(substr($raw, strlen($prefix)), true);
}
function workshop_chunk_cleanup($base) {
    @unlink($base . '.bin'); @unlink($base . '.meta.php'); @unlink($base . '.lock');
}
function workshop_chunk_owner() {
    return isset($GLOBALS['user']['id']) ? (int)$GLOBALS['user']['id'] : 0;
}
function workshop_chunk_session($id, $key, $owner) {
    if (!preg_match('/^[a-f0-9]{32}$/D', $id) || !preg_match('/^[a-f0-9]{48}$/D', $key))
        json_out(['ok' => false, 'error' => 'bad upload session'], 400);
    $base = workshop_chunk_dir() . '/' . $id;
    $meta = workshop_chunk_meta($base . '.meta.php');
    if (!is_array($meta) || !hash_equals($meta['key'], $key) || (int)$meta['owner'] !== (int)$owner)
        json_out(['ok' => false, 'error' => 'upload session not found'], 403);
    if ((int)$meta['created'] < time() - 3600) {
        workshop_chunk_cleanup($base);
        json_out(['ok' => false, 'error' => 'upload session expired'], 410);
    }
    return [$base, $meta];
}
function workshop_handle_chunk($owner) {
    $total = isset($_POST['total_bytes']) ? (int)$_POST['total_bytes'] : 0;
    $offset = isset($_POST['offset']) ? (int)$_POST['offset'] : -1;
    if ($total > min(MAX_BYTES, WORKSHOP_SAVE_LIMIT_BYTES)) workshop_too_big();
    if ($total <= 0 || $offset < 0 || !isset($_FILES['chunk']))
        json_out(['ok' => false, 'error' => 'bad chunk'], 400);
    $file = $_FILES['chunk'];
    if ($file['error'] !== UPLOAD_ERR_OK || !is_uploaded_file($file['tmp_name']))
        json_out(['ok' => false, 'error' => 'chunk upload failed'], 400);
    $size = filesize($file['tmp_name']);
    if ($size <= 0 || $size > WORKSHOP_CHUNK_BYTES || $offset + $size > $total)
        json_out(['ok' => false, 'error' => 'bad chunk size'], 413);
    $id = isset($_POST['upload_id']) ? (string)$_POST['upload_id'] : '';
    $key = isset($_POST['upload_key']) ? (string)$_POST['upload_key'] : '';
    if ($id === '') {
        if ($offset !== 0) json_out(['ok' => false, 'error' => 'bad offset'], 409);
        $dir = workshop_chunk_dir(); $count = 0; $own = 0;
        foreach (glob($dir . '/*.meta.php') as $path) {
            $m = workshop_chunk_meta($path);
            if (!is_array($m) || (int)$m['created'] < time() - 3600) {
                workshop_chunk_cleanup(substr($path, 0, -9)); continue;
            }
            $count++; if ((int)$m['owner'] === (int)$owner) $own++;
        }
        if ($count >= 32 || $own >= 4) json_out(['ok' => false, 'error' => 'too many pending uploads'], 429);
        $id = bin2hex(random_bytes(16)); $key = bin2hex(random_bytes(24));
        $base = $dir . '/' . $id;
        $meta = ['key' => $key, 'owner' => (int)$owner, 'total' => $total, 'created' => time()];
        if (file_put_contents($base . '.meta.php', "<?php exit; ?>\n" . json_encode($meta), LOCK_EX) === false)
            json_out(['ok' => false, 'error' => 'staging unavailable'], 500);
    } else {
        list($base, $meta) = workshop_chunk_session($id, $key, $owner);
        if ((int)$meta['total'] !== $total) json_out(['ok' => false, 'error' => 'total changed'], 409);
    }
    $lock = fopen($base . '.lock', 'c');
    if (!$lock || !flock($lock, LOCK_EX)) json_out(['ok' => false, 'error' => 'staging busy'], 503);
    clearstatcache(true, $base . '.bin');
    $received = is_file($base . '.bin') ? filesize($base . '.bin') : 0;
    if ($received !== $offset) json_out(['ok' => false, 'error' => 'bad offset'], 409);
    $src = fopen($file['tmp_name'], 'rb'); $dst = fopen($base . '.bin', 'ab');
    if (!$src || !$dst) json_out(['ok' => false, 'error' => 'staging unavailable'], 500);
    $copied = stream_copy_to_stream($src, $dst);
    fclose($src); fclose($dst); flock($lock, LOCK_UN); fclose($lock);
    if ($copied !== $size) json_out(['ok' => false, 'error' => 'chunk write failed'], 500);
    json_out(['ok' => true, 'upload_id' => $id, 'upload_key' => $key, 'received_bytes' => $received + $size]);
}
function workshop_prepare_staged_upload() {
    if (empty($_POST['staged_upload'])) return false;
    list($base, $meta) = workshop_chunk_session((string)$_POST['staged_upload'],
        isset($_POST['staged_key']) ? (string)$_POST['staged_key'] : '', workshop_chunk_owner());
    clearstatcache(true, $base . '.bin');
    $size = is_file($base . '.bin') ? filesize($base . '.bin') : -1;
    if ($size > min(MAX_BYTES, WORKSHOP_SAVE_LIMIT_BYTES)) workshop_too_big();
    if ($size !== (int)$meta['total']) json_out(['ok' => false, 'error' => 'incomplete upload'], 400);
    $GLOBALS['workshop_staged_base'] = $base;
    return true;
}
function workshop_move_save_upload($destination) {
    if (!empty($GLOBALS['workshop_staged_base'])) {
        $base = $GLOBALS['workshop_staged_base'];
        if (!rename($base . '.bin', $destination)) return false;
        workshop_chunk_cleanup($base);
        return true;
    }
    return move_uploaded_file($_FILES['file']['tmp_name'], $destination);
}
