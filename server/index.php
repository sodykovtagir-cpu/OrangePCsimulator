<?php
/**
 * Orange PC Simulator — публичная витрина мастерской.
 * Читает uploads/index.json напрямую (без api.php, чтобы не спотыкаться об анти-бот).
 */
$INDEX = __DIR__ . '/uploads/index.json';
$items = [];
if (is_file($INDEX)) {
    $j = json_decode(@file_get_contents($INDEX), true);
    if (is_array($j)) $items = $j;
}
// сортировка: новые сверху
usort($items, function ($a, $b) {
    return strcmp($b['created_at'], $a['created_at']);
});
function size_fmt($b) {
    if ($b >= 1048576) return round($b / 1048576, 1) . ' MB';
    if ($b >= 1024) return round($b / 1024) . ' KB';
    return $b . ' B';
}
function cover_style($it) {
    if (!empty($it['cover']) && is_file(__DIR__ . '/uploads/' . basename($it['cover'])))
        return 'background-image:url(uploads/' . rawurlencode($it['cover']) . ')';
    return '';
}
?>
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Orange PC Simulator — Workshop</title>
<link rel="stylesheet" href="style.css">
</head>
<body>
<div class="wrap">
  <div class="topbar">
    <div class="brand">
      <div class="logo">O</div>
      <div>
        <h1>Orange PC Simulator <span style="color:var(--orange)">Workshop</span></h1>
        <small>Мастерская сохранений</small>
      </div>
    </div>
    <div class="nav"></div>
  </div>

  <input type="text" class="search" id="q" placeholder="Поиск по названию или автору..." autofocus>

  <?php if (empty($items)): ?>
    <div class="empty">В мастерской пока нет сохранений. Станьте первым!</div>
  <?php else: ?>
  <div class="grid" id="grid">
    <?php foreach ($items as $it):
      $id = (int)$it['id'];
      $title = isset($it['title']) ? $it['title'] : 'Untitled';
      $author = isset($it['author']) ? $it['author'] : 'Player';
      $desc = isset($it['description']) ? $it['description'] : '';
      $dl = isset($it['downloads']) ? (int)$it['downloads'] : 0;
      $lk = isset($it['likes']) ? (int)$it['likes'] : 0;
      $sz = isset($it['size_bytes']) ? (int)$it['size_bytes'] : 0;
      $dt = isset($it['created_at']) ? $it['created_at'] : '';
    ?>
    <div class="card" data-search="<?php echo htmlspecialchars(mb_strtolower($title . ' ' . $author)); ?>">
      <div class="cover" style="<?php echo cover_style($it); ?>"><?php echo cover_style($it) ? '' : '🖥'; ?></div>
      <div class="body">
        <h3><?php echo htmlspecialchars($title); ?></h3>
        <div class="author">by <?php echo htmlspecialchars($author); ?></div>
        <?php if ($desc !== ''): ?><div class="desc"><?php echo htmlspecialchars($desc); ?></div><?php endif; ?>
        <div class="meta">
          <span><?php echo $dl; ?> ⬇</span>
          <span><?php echo $lk; ?> ♥</span>
          <span><?php echo size_fmt($sz); ?></span>
          <span><?php echo htmlspecialchars(substr($dt, 0, 10)); ?></span>
        </div>
        <div class="row">
          <a class="btn primary" href="api.php?action=download&id=<?php echo $id; ?>">Скачать</a>
        </div>
      </div>
    </div>
    <?php endforeach; ?>
  </div>
  <?php endif; ?>

  <div class="footer">Orange PC Simulator · мастерская</div>
</div>
<script>
var q = document.getElementById('q');
if (q) q.addEventListener('input', function () {
  var v = this.value.trim().toLowerCase();
  document.querySelectorAll('#grid .card').forEach(function (c) {
    c.style.display = (!v || c.dataset.search.indexOf(v) >= 0) ? '' : 'none';
  });
});
</script>
</body>
</html>
