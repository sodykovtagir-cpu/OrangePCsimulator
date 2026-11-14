<?php
// Included only from the authenticated administration controller.
if (!function_exists('is_admin') || !is_admin()) { http_response_code(404); exit; }
function hosted_preview_html($doc) {
 $options='<option value="/">/</option>';
 foreach($doc['pages']??[] as $p)$options.='<option value="'.h($p['path']).'">'.h($p['path']).'</option>';
 $data=json_encode($doc,JSON_HEX_TAG|JSON_HEX_AMP|JSON_HEX_APOS|JSON_HEX_QUOT|JSON_THROW_ON_ERROR);
 $script=file_get_contents(__DIR__.'/sites_preview.js');
 return '<!doctype html><html lang="ru"><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &apos;none&apos;; img-src data:; style-src &apos;unsafe-inline&apos;; script-src &apos;unsafe-inline&apos;; connect-src &apos;none&apos;; form-action &apos;none&apos;; base-uri &apos;none&apos;"><style>html,body{margin:0;background:#eee;font:14px Arial}#toolbar{height:32px;position:sticky;top:0;z-index:10;background:#f3f5f7;display:flex;gap:12px;align-items:center}#notice{color:#7a3300}#page{position:relative;transform-origin:top left;background-size:100% 100%}#scroll{overflow:hidden}button{cursor:pointer}</style><div id="toolbar"><select id="routes">'.$options.'</select><span id="address"></span><span id="notice"></span></div><div id="scroll"><div id="page"></div></div><script type="application/json" id="site-data">'.$data.'</script><script>'.$script.'</script></html>';
}
function hosted_preview_render($doc) {
 echo '<h3>Просмотр сайта</h3><iframe title="Предпросмотр orange-site" sandbox="allow-scripts" referrerpolicy="no-referrer" style="width:100%;height:640px;border:1px solid #ccd4df;background:white" srcdoc="'.h(hosted_preview_html($doc)).'"></iframe><p class="sub">Интерактивный предпросмотр: страницы, картинки и прямые ссылки. Произвольный Lua здесь не запускается.</p><h3>Файлы и изображения</h3><div style="display:flex;flex-wrap:wrap;gap:16px">';
 foreach($doc['assets']??[]as $a){echo '<div style="max-width:100%;border:1px solid #ccd4df;padding:10px"><b>'.h($a['name']).'</b><br>';
  if(in_array($a['media'],['image/png','image/jpeg'],true)){echo '<details><summary><img alt="'.h($a['name']).'" style="width:160px;height:120px;object-fit:contain" src="data:'.h($a['media']).';base64,'.h($a['data']).'"><br>Открыть в полном размере</summary><img alt="'.h($a['name']).'" style="max-width:100%;height:auto" src="data:'.h($a['media']).';base64,'.h($a['data']).'"></details>';}
  else echo '<span>'.strlen(base64_decode($a['data'],true)).' байт</span>';
  echo '</div>';
 }echo '</div>';
}
