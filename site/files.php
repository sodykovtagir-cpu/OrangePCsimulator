<?php
/**
 * Список релизов для сайта.
 *
 * Показывает то, что реально лежит в папке files/. Чтобы выложить новую
 * версию, достаточно залить файл по FTP — сайт подхватит сам, править
 * ничего не нужно.
 *
 * Если папка пуста, сайт покажет ссылки на телеграм-канал: игроку всегда
 * есть откуда скачать.
 *
 * GitHub Releases сознательно НЕ используем: там лежат старые dev-сборки
 * 1.0.x, и игрок скачал бы их вместо свежей версии.
 */

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

/** Платформа и версия по имени файла. */
function describe($name) {
    $ext = strtolower(pathinfo($name, PATHINFO_EXTENSION));

    $platform = 'Файл';
    if ($ext === 'apk') $platform = 'Android';
    elseif ($ext === 'exe' || $ext === 'zip') $platform = 'Windows';
    if (stripos($name, 'android') !== false) $platform = 'Android';
    elseif (stripos($name, 'windows') !== false) $platform = 'Windows';

    $version = '';
    if (preg_match('/(\d+\.\d+(?:\.\d+)?)/', $name, $m)) $version = $m[1];

    return [$platform, $version, strtoupper($ext)];
}

$items = [];

/* ---------- 1. файлы на самом сайте ---------- */
$dir = __DIR__ . '/files';
if (is_dir($dir)) {
    foreach (scandir($dir) as $name) {
        if ($name === '.' || $name === '..') continue;
        $path = $dir . '/' . $name;
        if (!is_file($path)) continue;

        $ext = strtolower(pathinfo($name, PATHINFO_EXTENSION));
        if (!in_array($ext, ['exe', 'apk', 'zip'], true)) continue;

        list($platform, $version, $kind) = describe($name);
        $items[] = [
            'name' => $name,
            'url'  => 'files/' . rawurlencode($name),
            'size' => filesize($path),
            'platform' => $platform,
            'version'  => $version,
            'kind'     => $kind,
            'source'   => 'site',
            'time'     => filemtime($path),
        ];
    }
}

/* Новые версии сверху. Номера сравниваем по-человечески, иначе 1.8.9
   оказался бы выше 1.8.41. */
usort($items, function ($a, $b) {
    $c = version_compare($b['version'], $a['version']);
    if ($c !== 0) return $c;
    return $b['time'] <=> $a['time'];
});

echo json_encode(['ok' => true, 'items' => $items], JSON_UNESCAPED_UNICODE);
