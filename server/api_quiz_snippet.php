<?php
/**
 * Вставьте этот блок в api.php мастерской (в начало обработчика,
 * перед остальными action — или добавьте как новый elseif).
 *
 * action=quiz: одноразово отдаёт квиз, отправленный из админки
 * (admin_quiz.php). После выдачи файл quiz_pending.json очищается,
 * поэтому каждый квиз показывается в игре только один раз.
 *
 * Пример вставки в api.php:
 *
 *   $action = isset($_GET['action']) ? $_GET['action'] : '';
 *   if ($action === 'quiz') {
 *       ...этот блок...
 *   }
 *   elseif ($action === 'list') { ... }
 *   ...
 */
if (isset($_GET['action']) && $_GET['action'] === 'quiz') {
    header('Content-Type: application/json');
    $f = __DIR__ . '/quiz_pending.json';
    if (file_exists($f)) {
        $d = json_decode(@file_get_contents($f), true);
        if (is_array($d) && !empty($d['link'])) {
            // one-shot: выдаём и сразу очищаем
            @file_put_contents($f, json_encode(array()));
            echo json_encode(array(
                'ok'    => true,
                'show'  => true,
                'link'  => $d['link'],
                'title' => isset($d['title']) ? $d['title'] : '',
                'body'  => isset($d['body']) ? $d['body'] : '',
            ));
            exit;
        }
    }
    echo json_encode(array('ok' => true, 'show' => false));
    exit;
}
