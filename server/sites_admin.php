<?php
// Loaded only by the authenticated workshop administration page. No independent
// login, default password or public moderation API is introduced here.
if (!function_exists('is_admin')) { http_response_code(404); exit; }
function hosted_admin_store($change=null) {
 if(!is_admin())throw new RuntimeException('Admin required');
 require_once dirname(__DIR__).'/sites/lib.php';
 $dir=dirname(__DIR__).'/sites/data';if(!is_dir($dir))return ['sites'=>[]];
 $lock=fopen($dir.'/lock.php','c+');if(!$lock||!flock($lock,LOCK_EX))throw new RuntimeException('Sites storage busy');
 try{$db=site_read($dir.'/state.php',['sites'=>[]]);site_reconcile($db,time());if($change)$change($db);site_write($dir.'/state.php',$db);return $db;}
 finally{flock($lock,LOCK_UN);fclose($lock);}
}
function hosted_admin_ban($id,$ban,$reason) {
 return hosted_admin_store(function(&$db)use($id,$ban,$reason){
  if(!isset($db['sites'][$id]))throw new RuntimeException('Сайт не найден');
  $s=&$db['sites'][$id];$s['banned']=$ban;$s['ban_reason']=$ban?$reason:'';
  // Both actions leave it stopped. Unbanning must not unexpectedly resume rent.
  $s['active']=false;$s['unpaid']=false;$s['sessions']=[];$s['transfers']=[];$s['access_epoch']=($s['access_epoch']??0)+1;
 });
}
function hosted_admin_render($csrf) {
 try{$db=hosted_admin_store();}catch(Throwable $e){echo '<p>Хранилище сайтов недоступно.</p>';return;}
 echo '<h2>Сайты игроков</h2><table><tr><th>Сайт / владелец</th><th>Состояние / нагрузка</th><th>Модерация</th></tr>';
 foreach($db['sites']as $s){
  $state=!empty($s['banned'])?'Заблокирован':(!empty($s['unpaid'])?'Хост не оплачен':(!empty($s['active'])?'Работает':'Остановлен'));
  echo '<tr><td><b>'.h($s['title']).'</b><br>'.h($s['url']).'<br>Владелец #'.h($s['owner']).' / сайт #'.(int)$s['id'].'<br><small>Сохранение: '.h($s['world']).'</small></td><td>'.h($state).'<br>Визитов: '.(int)$s['visits'].' / онлайн: '.site_online($s,time()).'<br>Начислено: $'.(int)$s['total'].'<br>'.h($s['ban_reason']??'').'</td><td>';
  echo '<form method="post"><input type="hidden" name="csrf" value="'.h($csrf).'"><input type="hidden" name="act" value="'.(!empty($s['banned'])?'site_unban':'site_ban').'"><input type="hidden" name="site_id" value="'.(int)$s['id'].'"><input name="reason" maxlength="240" placeholder="Причина блокировки"><button class="btn">'.(!empty($s['banned'])?'Разблокировать':'Заблокировать').'</button></form><a href="?tab=sites&amp;site_id='.(int)$s['id'].'">Посмотреть сайт</a></td></tr>';
 }
 echo '</table>';
 $id=isset($_GET['site_id'])?(int)$_GET['site_id']:0;
 if($id>0&&isset($db['sites'][$id])){try{$package=site_read(dirname(__DIR__).'/sites/data/p_'.$id.'.php');$doc=json_decode($package['document'],true);echo '<h3>Пакет #'.$id.'</h3><p>Имена корневых файлов: ';foreach($doc['assets']??[]as $asset)echo h($asset['name']).' ';echo '</p>';
  site_validate($package['document']);require_once __DIR__.'/sites_preview.php';hosted_preview_render($doc);

 }catch(Throwable $e){echo '<p>Пакет недоступен.</p>';}}
}
