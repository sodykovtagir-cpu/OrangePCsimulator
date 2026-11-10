<?php
// A save is at most 30 MiB. The request envelope is larger to allow a cover
// image, title, token and multipart boundaries alongside an exact-limit save.
define('WORKSHOP_SAVE_LIMIT_BYTES', 30 * 1024 * 1024);
define('WORKSHOP_REQUEST_LIMIT_BYTES', 40 * 1024 * 1024);

function workshop_too_big() {
    json_out([
        'ok' => false,
        'error' => 'file > 30MB',
        'max_bytes' => min(MAX_BYTES, WORKSHOP_SAVE_LIMIT_BYTES)
    ], 413);
}

function workshop_validate_request_size() {
    // PHP clears both $_POST and $_FILES when post_max_size is exceeded.
    // Detect this explicitly rather than silently treating an update as a
    // metadata-only success or returning a misleading authentication error.
    $length = isset($_SERVER['CONTENT_LENGTH']) ? (int)$_SERVER['CONTENT_LENGTH'] : 0;
    if ($length > WORKSHOP_REQUEST_LIMIT_BYTES) workshop_too_big();
    if ($length > 0 && empty($_POST) && empty($_FILES) &&
        stripos(isset($_SERVER['CONTENT_TYPE']) ? $_SERVER['CONTENT_TYPE'] : '', 'multipart/form-data') === 0) {
        workshop_too_big();
    }
}

function workshop_validate_save_upload($required) {
    if (workshop_prepare_staged_upload()) return true;
    if (!isset($_FILES['file'])) {
        if ($required) json_out(['ok' => false, 'error' => 'no file'], 400);
        return false;
    }
    $file = $_FILES['file'];
    $error = isset($file['error']) ? $file['error'] : UPLOAD_ERR_NO_FILE;
    if ($error === UPLOAD_ERR_NO_FILE && !$required) return false;
    if ($error === UPLOAD_ERR_INI_SIZE || $error === UPLOAD_ERR_FORM_SIZE) workshop_too_big();
    if ($error !== UPLOAD_ERR_OK || !isset($file['tmp_name']) || !is_uploaded_file($file['tmp_name'])) {
        json_out(['ok' => false, 'error' => 'upload failed'], 400);
    }
    // Check the real temporary file, not a client-supplied Content-Length.
    $size = filesize($file['tmp_name']);
    if ($size === false) json_out(['ok' => false, 'error' => 'upload failed'], 400);
    if ($size > min(MAX_BYTES, WORKSHOP_SAVE_LIMIT_BYTES)) workshop_too_big();
    return true;
}

require_once __DIR__ . '/upload_chunks.php';
