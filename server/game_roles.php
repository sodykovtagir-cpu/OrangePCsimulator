<?php
/** Game-account roles. Administrative changes require the web admin session + CSRF. */
if (!defined('GAME_ROLES_FILE')) define('GAME_ROLES_FILE', __DIR__ . '/game_admins.json');
if (!defined('GAME_ROLE_USERS_FILE')) define('GAME_ROLE_USERS_FILE', __DIR__ . '/users.json');

function game_role_users() {
    $data = is_file(GAME_ROLE_USERS_FILE) ? json_decode(@file_get_contents(GAME_ROLE_USERS_FILE), true) : [];
    return is_array($data) ? $data : [];
}

function game_admin_ids() {
    $data = is_file(GAME_ROLES_FILE) ? json_decode(@file_get_contents(GAME_ROLES_FILE), true) : [];
    if (!is_array($data) || !isset($data['admin_user_ids']) || !is_array($data['admin_user_ids'])) return [];
    $ids = [];
    foreach ($data['admin_user_ids'] as $id) {
        if ((is_int($id) || (is_string($id) && ctype_digit($id))) && (int)$id > 0) $ids[] = (int)$id;
    }
    return array_values(array_unique($ids));
}

function game_is_admin_user($user) {
    return is_array($user) && !empty($user['verified']) && !empty($user['id'])
        && in_array((int)$user['id'], game_admin_ids(), true);
}

function game_is_admin_id($id) {
    if ((int)$id <= 0) return false;
    foreach (game_role_users() as $user) {
        if ((int)($user['id'] ?? 0) === (int)$id) return game_is_admin_user($user);
    }
    return false;
}

function game_set_admin($id, $enabled) {
    $id = (int)$id;
    if ($id <= 0) return false;
    $found = false;
    foreach (game_role_users() as $user) {
        if ((int)($user['id'] ?? 0) === $id && !empty($user['verified'])) { $found = true; break; }
    }
    if (!$found) return false;

    // Serialize role edits, and write atomically. Do not modify users/passwords/tokens.
    $lock = @fopen(GAME_ROLES_FILE . '.lock', 'c');
    if (!$lock || !flock($lock, LOCK_EX)) { if ($lock) fclose($lock); return false; }
    $ids = game_admin_ids();
    $ids = array_values(array_filter($ids, function ($value) use ($id) { return $value !== $id; }));
    if ($enabled) $ids[] = $id;
    sort($ids, SORT_NUMERIC);
    $data = json_encode(['version' => 1, 'admin_user_ids' => $ids], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
    $temporary = GAME_ROLES_FILE . '.tmp.' . bin2hex(random_bytes(6));
    $ok = @file_put_contents($temporary, $data, LOCK_EX) !== false && @rename($temporary, GAME_ROLES_FILE);
    if (is_file($temporary)) @unlink($temporary);
    flock($lock, LOCK_UN); fclose($lock);
    return $ok;
}
