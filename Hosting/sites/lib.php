<?php
// No account credentials, FTP passwords, or visitor IPs are stored here.
const SITE_PERIOD = 1800;
const SITE_PRICE = 100;
const SITE_CUSTOM_PRICE = 1000;
const SITE_PREFIX = "<?php http_response_code(404); exit; /*\n";
function site_fail($message, $code=400) { throw new RuntimeException($message, $code); }
function site_text($value,$max) { if (!is_string($value)||strlen($value)>$max||preg_match('//u',$value)!==1) site_fail('Invalid text'); return $value; }
function site_name($value) { return is_string($value)&&$value!==''&&strlen($value)<=320&&$value===trim($value)&&$value!=='.'&&$value!=='..'&&!preg_match('~[\x00-\x1f\x7f/\\\\:"<>|?*]~u',$value); }
function site_route($value) { return is_string($value)&&strlen($value)<=96&&preg_match('~^/(?:[a-z0-9_-]+(?:/[a-z0-9_-]+)*)?$~D',$value); }
function site_number($v,$min,$max) { if(!is_numeric($v)||!is_finite((float)$v)||$v<$min||$v>$max)site_fail('Value outside site bounds'); }
function site_color($value) { if(!is_string($value)||!preg_match('/^#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?$/D',$value))site_fail('Invalid color'); }
function site_commands() {return ['GoToPage'=>1,'RedirectURL'=>1,'OpenFile'=>1,'StartDownload'=>1,'Alert'=>1,'SetText'=>2,'AppendText'=>2,'ClearText'=>1,'GetText'=>1,'SetTextColor'=>2,'SetFontSize'=>2,'SetFont'=>2,'SetPosition'=>3,'MoveBy'=>3,'SetSize'=>3,'SetPivot'=>3,'GetX'=>1,'GetY'=>1,'GetWidth'=>1,'GetHeight'=>1,'SetFillColor'=>2,'SetImage'=>2,'ClearImage'=>1,'SetBackground'=>1,'Show'=>1,'Hide'=>1,'ToggleVisible'=>1,'SetVisible'=>2,'IsVisible'=>1,'SetEnabled'=>2,'IsEnabled'=>1,'GetTitle'=>0,'GetDescription'=>0,'GetURL'=>0,'FileExists'=>1];}
function site_page_validate($page,$assets,&$elements) {
 site_text($page['title']??'',480);site_color($page['background']??'#FFFFFF');site_number($page['width']??960,128,4096);site_number($page['height']??720,128,16384);
 if(!in_array($page['viewportMode']??'',['','viewport'],true))site_fail('Invalid viewport');
 if(!empty($page['backgroundImage'])&&empty($assets[$page['backgroundImage']]['image']))site_fail('Missing background image');
 $list=$page['elements']??[];if(!is_array($list)||count($list)>256)site_fail('Too many elements');$ids=[];$commands=site_commands();
 foreach($list as $e){
  if(!is_array($e)||!site_name($e['id']??null)||isset($ids[$e['id']]))site_fail('Invalid element ID');$ids[$e['id']]=true;
  if(!in_array($e['kind']??'',['text','image','button'],true))site_fail('Unknown element kind');
  foreach(['name'=>320,'text'=>16384,'onClick'=>32768]as $key=>$max)site_text($e[$key]??'',$max);
  foreach(['x'=>[-16384,16384],'y'=>[-16384,16384],'width'=>[1,4096],'height'=>[1,16384],'pivotX'=>[0,1],'pivotY'=>[0,1],'fontSize'=>[6,256]]as $key=>$range)site_number($e[$key]??($key==='fontSize'?24:($key==='width'?240:($key==='height'?48:0))),$range[0],$range[1]);
  if(!in_array($e['font']??'default',['default','mono','medium'],true))site_fail('Unknown font');site_color($e['fill']??'#BEBEBE');site_color($e['textColor']??'#000000');
  site_number($e['layout']??0,0,16);if(!empty($e['layout'])){site_number($e['layoutWidth']??0,1,4096);site_number($e['layoutHeight']??0,1,16384);}
  if(!empty($e['image'])&&empty($assets[$e['image']]['image']))site_fail('Missing image');
  $actions=$e['actions']??[];if(!is_array($actions)||count($actions)>32)site_fail('Too many actions');
  if($e['kind']!=='button'&&(!empty($actions)||!empty($e['onClick'])))site_fail('Only Button supports actions');
  foreach($actions as $a){if(!is_array($a)||!array_key_exists($a['command']??'',$commands)||!is_array($a['arguments']??null)||count($a['arguments'])!==$commands[$a['command']])site_fail('Invalid action');foreach($a['arguments']as $arg)site_text($arg,16384);if($a['command']==='GoToPage'&&!site_route($a['arguments'][0]))site_fail('GoToPage requires a page path such as /about');}
 }
 $elements+=count($list);if($elements>256)site_fail('256 elements across pages maximum');
}
function site_validate($json) {
 if(!is_string($json)||strlen($json)>8388608)site_fail('Package too large');$doc=json_decode($json,true,32);if(!is_array($doc)||($doc['format']??'')!=='orange-site'||($doc['version']??0)!==1)site_fail('Invalid orange-site document');
 site_text($doc['description']??'',4096);site_text($doc['keywords']??'',2048);$assets=[];$bytes=0;$pixels=0;$list=$doc['assets']??[];if(!is_array($list)||count($list)>64)site_fail('Too many files');$fold=[];
 foreach($list as $asset){$name=$asset['name']??null;if(!site_name($name)||isset($fold[strtolower($name)]))site_fail('Invalid or duplicate root file');$fold[strtolower($name)]=true;
  $media=$asset['media']??'';if(!in_array($media,['image/png','image/jpeg','application/x-pcos-file'],true))site_fail('Invalid media');$encoded=$asset['data']??null;if(!is_string($encoded)||strlen($encoded)>2796204)site_fail('File too large');$raw=base64_decode($encoded,true);if($raw===false||strlen($raw)>2097152)site_fail('Invalid file encoding');$bytes+=strlen($raw);if($bytes>6291456)site_fail('Files exceed 6 MiB');$image=$media!=='application/x-pcos-file';
  if($image){$size=@getimagesizefromstring($raw);if(!$size||$size[0]>2048||$size[1]>2048||$size[0]<1||$size[1]<1||($size['mime']??'')!==$media)site_fail('Invalid image');$pixels+=$size[0]*$size[1];if($pixels>8388608)site_fail('Image budget exceeded');}
  $assets[$name]=['image'=>$image];
 }
 if(!empty($doc['icon'])&&empty($assets[$doc['icon']]['image']))site_fail('Missing icon');$elements=0;site_page_validate($doc,$assets,$elements);$routes=['/'=>true];$pages=$doc['pages']??[];if(!is_array($pages)||count($pages)>15)site_fail('16 pages maximum');
 foreach($pages as $page){$path=$page['path']??null;if(!site_route($path)||isset($routes[$path]))site_fail('Invalid or duplicate page route');$routes[$path]=true;site_page_validate($page,$assets,$elements);}return $doc;
}
function site_read($file,$default=[]) {if(!is_file($file))return $default;$raw=file_get_contents($file);if(strncmp($raw,SITE_PREFIX,strlen(SITE_PREFIX))!==0)site_fail('Storage header invalid',503);$data=json_decode(base64_decode(substr($raw,strlen(SITE_PREFIX),-3),true),true);if(!is_array($data))site_fail('Storage unavailable',503);return $data;}
function site_write($file,$data){$raw=SITE_PREFIX.base64_encode(json_encode($data,JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES|JSON_THROW_ON_ERROR))."\n*/";$tmp=$file.'.'.bin2hex(random_bytes(6)).'.tmp.php';if(file_put_contents($tmp,$raw,LOCK_EX)!==strlen($raw)||!rename($tmp,$file)){@unlink($tmp);site_fail('Storage write failed',503);}}
function site_period_price($s){return SITE_PRICE+($s['url']!=='s'.$s['id'].'.sites.pcos'?SITE_CUSTOM_PRICE:0);}
function site_stop_host(&$db,$owner,$world,$host){foreach($db['sites']as &$s)if($s['owner']===$owner&&$s['world']===$world&&$s['host']===$host&&$s['active']){$s['active']=false;$s['unpaid']=false;$s['sessions']=[];$s['transfers']=[];$s['access_epoch']=($s['access_epoch']??0)+1;}unset($s);}
function site_transfers(&$s,$now){$active=[];foreach($s['transfers']??[]as $k=>$v)if($v['at']>$now-30)$active[$k]=$v;$s['transfers']=$active;return count($active);}
function site_bill(&$s,$now){if(empty($s['active']))return;$n=max(0,intdiv($now-$s['billed_at'],SITE_PERIOD));$s['total']+=$n*site_period_price($s);$s['billed_at']+=$n*SITE_PERIOD;}
function site_online(&$s,$now){$active=[];foreach($s['sessions']??[]as $key=>$row)if($row['at']>$now-90)$active[$key]=$row;$s['sessions']=$active;return count($active);}
function site_custom_paid($s){return array_key_exists('custom_paid',$s)?(bool)$s['custom_paid']:$s['url']!=='s'.$s['id'].'.sites.pcos';}
function site_public(&$s,$now){$online=site_online($s,$now);return ['id'=>$s['id'],'url'=>$s['url'],'title'=>$s['title'],'description'=>$s['description'],'downloads'=>$s['downloads']??0,'transferring'=>site_transfers($s,$now),'visits'=>$s['visits'],'online'=>$online,'active'=>(bool)$s['active'],'total'=>$s['total'],'unpaid'=>!empty($s['unpaid']),'banned'=>!empty($s['banned']),'ban_reason'=>$s['ban_reason']??'','icon_data'=>$s['icon_data']??'','icon_media'=>$s['icon_media']??'','cpu_capacity'=>$s['cpu_capacity']??1000,'cpu_load'=>$s['cpu_load']??0,'host_mbps'=>$s['host_mbps']??5,'custom_paid'=>site_custom_paid($s),'next_bill'=>$s['active']&&empty($s['unpaid'])&&empty($s['banned'])?$s['billed_at']+SITE_PERIOD:0,'host'=>$s['host'],'bytes'=>$s['bytes'],'revision'=>$s['revision']];}
function site_user($token,$usersFile){if(!is_string($token)||strlen($token)<32||strlen($token)>256)site_fail('Login required',401);$raw=@file_get_contents($usersFile);$users=json_decode($raw?:'[]',true);foreach(is_array($users)?$users:[]as $u)if(!empty($u['token'])&&hash_equals((string)$u['token'],$token)&&!empty($u['verified']))return (string)$u['id'];site_fail('Session expired',401);}

function site_wallet_key($owner,$world){return $owner.':'.$world;}
function site_suspend(&$s){if(empty($s['unpaid'])){$s['access_epoch']=($s['access_epoch']??0)+1;$s['sessions']=[];$s['transfers']=[];}$s['unpaid']=true;}
function site_reconcile(&$db,$now){
 $totals=[];foreach($db['sites']as $s){$key=site_wallet_key($s['owner'],$s['world']);$totals[$key]=($totals[$key]??0)+$s['total'];}
 foreach($db['sites']as &$s){if(empty($s['active'])||!empty($s['banned']))continue;$key=site_wallet_key($s['owner'],$s['world']);$ceiling=$db['wallets'][$key]['ceiling']??null;$price=site_period_price($s);
  // Upgrade only future billing boundaries; never retroactively increase old bills.
  if(!isset($s['recurring_from']))$s['recurring_from']=$s['billed_at']+max(1,(int)floor(($now-$s['billed_at'])/SITE_PERIOD)+1)*SITE_PERIOD;
  if(!empty($s['unpaid'])){if($ceiling!==null&&$ceiling-$totals[$key]>=$price){$s['unpaid']=false;$s['total']+=$price;$totals[$key]+=$price;$s['billed_at']=$now;}continue;}
  $n=max(0,intdiv($now-$s['billed_at'],SITE_PERIOD));if(!$n)continue;
  $legacy=min($n,max(0,(int)ceil(($s['recurring_from']-$s['billed_at'])/SITE_PERIOD)-1));
  $funds=$ceiling===null?PHP_INT_MAX:max(0,$ceiling-$totals[$key]);
  $oldPaid=min($legacy,intdiv((int)$funds,SITE_PRICE));$funds-=$oldPaid*SITE_PRICE;
  $newPaid=$oldPaid===$legacy?min($n-$legacy,intdiv((int)$funds,$price)):0;
  $paid=$oldPaid+$newPaid;$cost=$oldPaid*SITE_PRICE+$newPaid*$price;
  $s['total']+=$cost;$totals[$key]+=$cost;$s['billed_at']+=$paid*SITE_PERIOD;
  if($paid<$n)site_suspend($s);
 }unset($s);
}
function site_snapshot(&$db,$owner,$world,$balance,$settled){
 if(!is_scalar($balance)||!is_scalar($settled)||!ctype_digit((string)$balance)||!ctype_digit((string)$settled))site_fail('Invalid balance snapshot');
 $total=0;foreach($db['sites']as $s)if($s['owner']===$owner&&$s['world']===$world)$total+=$s['total'];
 if((float)$balance>9999999||(float)$settled>$total)site_fail('Invalid settled bill');
 $db['wallets'][site_wallet_key($owner,$world)]=['ceiling'=>(int)$balance+(int)$settled];
}
function site_available_funds($db,$owner,$world){$key=site_wallet_key($owner,$world);if(!isset($db['wallets'][$key]))return PHP_INT_MAX;$total=0;foreach($db['sites']as $s)if($s['owner']===$owner&&$s['world']===$world)$total+=$s['total'];return max(0,$db['wallets'][$key]['ceiling']-$total);}
function site_icon_data($doc){
 foreach($doc['assets']??[]as $a)if(($a['name']??'')===($doc['icon']??'')){
  $raw=base64_decode($a['data'],true);if(!$raw)return ['',''];$size=@getimagesizefromstring($raw);if(!$size)return ['',''];
  if(function_exists('imagecreatefromstring')){$src=@imagecreatefromstring($raw);if($src){$w=max(1,(int)round($size[0]*min(1,96/$size[0],96/$size[1])));$h=max(1,(int)round($size[1]*min(1,96/$size[0],96/$size[1])));$dst=imagecreatetruecolor($w,$h);imagealphablending($dst,false);imagesavealpha($dst,true);imagecopyresampled($dst,$src,0,0,0,0,$w,$h,$size[0],$size[1]);ob_start();imagepng($dst);$png=ob_get_clean();imagedestroy($dst);imagedestroy($src);if(strlen($png)<=65536)return [base64_encode($png),'image/png'];}}
  if(strlen($raw)<=65536&&$size[0]<=256&&$size[1]<=256)return [$a['data'],$a['media']];return ['',''];
 }return ['',''];
}
