<?php
ini_set('display_errors','0');
header('Access-Control-Allow-Origin: *');header('Access-Control-Allow-Methods: POST, OPTIONS');header('Access-Control-Allow-Headers: Content-Type, X-Auth-Token');header('Content-Type: application/json; charset=utf-8');header('Cache-Control: no-store');
if(($_SERVER['REQUEST_METHOD']??'')==='OPTIONS'){http_response_code(204);exit;}
require_once __DIR__.'/lib.php';
$dir=defined('SITE_DATA_DIR')?SITE_DATA_DIR:__DIR__.'/data';$users=defined('SITE_USERS_FILE')?SITE_USERS_FILE:dirname(__DIR__).'/workshop/users.json';
$lock=null;
function site_owned_local($db,$owner,$world){$n=0;foreach($db['sites']as $s)if($s['owner']===$owner&&$s['world']===$world&&site_local($s))$n++;return $n;}

try{
 if(($_SERVER['CONTENT_LENGTH']??0)>33554432)site_fail('Request too large',413);
 if(!is_dir($dir)&&!mkdir($dir,0755,true))site_fail('Storage unavailable',503);
 $lock=fopen($dir.'/lock.php','c+');if(!$lock||!flock($lock,LOCK_EX))site_fail('Busy',503);
 $db=site_read($dir.'/state.php',['sites'=>[],'next'=>1,'secret'=>bin2hex(random_bytes(32)),'rates'=>[]]);$now=time();$action=$_POST['action']??$_GET['action']??'health';
 // An opaque salted IP hash is used only for short-lived abuse limiting.
 $rateKey=hash_hmac('sha256',$_SERVER['REMOTE_ADDR']??'local',$db['secret']);$bucket=intdiv($now,60);
 foreach($db['rates']as $k=>$rate)if($rate[0]<$bucket)unset($db['rates'][$k]);
 $rate=$db['rates'][$rateKey]??[$bucket,0];if(++$rate[1]>180)site_fail('Rate limited',429);$db['rates'][$rateKey]=$rate;
 $db['wallets']=$db['wallets']??[];
 if(in_array($action,['publish','stats','stop','host_stop','topup','local'],true)){
  $owner=site_user($_SERVER['HTTP_X_AUTH_TOKEN']??$_POST['token']??'',$users);$world=$_POST['world']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$world))site_fail('Invalid save identity');
  $snapshot=false;
  if(isset($_POST['balance'])){
   $snapshot=true;
   $settled=$_POST['settled']??null;
   if($settled===null){$ledger=json_decode($_POST['ledgers']??'{}',true);$settled=0;if(is_array($ledger)&&is_array($ledger['items']??null)&&count($ledger['items'])<=1000)foreach($ledger['items']as $entry)if(is_array($entry)&&($entry['owner']??'')===$owner)$settled=$entry['charged']??0;}
   site_snapshot($db,$owner,$world,$_POST['balance'],$settled);
  }
  // Locally hosted publications (kept in the game save): merged into the same
  // wallet so their rent is deducted from the same money. Nothing is served.
  if(isset($_POST['locals'])){try{site_locals($db,$owner,$world,json_decode($_POST['locals'],true),$now);}catch(Throwable $e){}}
  $profiles=json_decode($_POST['hosts']??'{}',true);$profiles=is_array($profiles)?($profiles['items']??[]):[];
  if(is_array($profiles)&&count($profiles)<=200)foreach($profiles as $profile)if(is_array($profile))foreach($db['sites']as &$site)if($site['owner']===$owner&&$site['world']===$world&&$site['host']==($profile['host']??null)){
   foreach(['cpu_capacity'=>[100,1000000],'cpu_load'=>[0,1],'host_mbps'=>[.02,10000]]as $field=>$range){$v=$profile[$field]??null;if(is_numeric($v)&&is_finite((float)$v))$site[$field]=max($range[0],min($range[1],(float)$v));}
  }unset($site);
 }
 site_reconcile($db,$now);
 // The game balance is the ceiling of real money: anything above it can never
 // be collected, so it is written off here instead of becoming debt.
 if(!empty($snapshot))site_settle_shortfall($db,$owner,$world);
 if(isset($owner,$world)){
  $off=explode(',',$_POST['stopped_hosts']??'');if($action==='host_stop')$off[]=$_POST['host']??'';
  foreach(array_slice($off,0,200)as $host)if(ctype_digit((string)$host))site_stop_host($db,$owner,$world,(int)$host);
 }
 if(in_array($action,['list','view','info'],true))foreach($db['sites']as &$site)if(!array_key_exists('icon_data',$site)){try{$package=site_read($dir.'/p_'.$site['id'].'.php');$doc=json_decode($package['document'],true);[$site['icon_data'],$site['icon_media']]=site_icon_data($doc);}catch(Throwable $e){$site['icon_data']='';$site['icon_media']='';}}unset($site);
 $out=['ok'=>true,'service'=>'orange-sites','version'=>1,'now'=>$now,'period'=>SITE_PERIOD,'price'=>SITE_PRICE,'custom_price'=>SITE_CUSTOM_PRICE];
 if($action==='health'){$out['available']=true;}
 elseif($action==='list'){
  $q=strtolower(substr(trim($_POST['query']??''),0,160));$out['items']=[];
  foreach($db['sites']as &$s)if($s['active']&&empty($s['banned'])&&($q===''||strpos(strtolower($s['url'].' '.$s['title'].' '.$s['description'].' '.$s['keywords']),$q)!==false)){$out['items'][]=site_public($s,$now);if(count($out['items'])>=100)break;}unset($s);
 }
 elseif(in_array($action,['view','info','visit','leave','transfer'],true)){
  $id=(string)($_POST['id']??'');if($action==='view'||$action==='info'){
   $url=strtolower(trim($_POST['url']??''));$url=preg_replace('~^https?://~','',$url);$domain=explode('/',$url,2)[0];$id='';foreach($db['sites']as $key=>$s)if($s['url']===$domain&&($s['active']||!empty($s['banned']))){$id=(string)$key;break;}
  }
  if(!isset($db['sites'][$id])||(!$db['sites'][$id]['active']&&empty($db['sites'][$id]['banned'])))site_fail('Site unavailable',404);$s=&$db['sites'][$id];
  if($action==='view'||$action==='info'){
   $out['item']=site_public($s,$now);
   // A locally hosted site has no package on this web host: it is only visible
   // in its owner's save and can never be served from here.
   if(site_local($s))site_fail('Site unavailable',404);
   if($action==='view'&&empty($s['unpaid'])&&empty($s['banned'])){$package=site_read($dir.'/p_'.$id.'.php');$out['document']=$package['document'];if(!isset($out['document']))site_fail('Site unavailable',404);}
   $nonce=bin2hex(random_bytes(16));$ticket=$nonce.'.'.($now+300);$out['ticket']=$ticket.'.'.hash_hmac('sha256',$id.'|'.($s['access_epoch']??0).'|'.$ticket,$db['secret']);
  }else{
   if(!empty($s['banned']))site_fail('Сайт заблокирован администрацией',403);
   if(!empty($s['unpaid']))site_fail('Хост не оплачен',402);
   if($action==='transfer'){
    $ticket=$_POST['ticket']??'';$parts=explode('.',$ticket);if(count($parts)!==3||!ctype_digit($parts[1])||(int)$parts[1]<$now||!hash_equals(hash_hmac('sha256',$id.'|'.($s['access_epoch']??0).'|'.$parts[0].'.'.$parts[1],$db['secret']),$parts[2]))site_fail('Visit ticket expired',403);
    $package=site_read($dir.'/p_'.$id.'.php');$doc=json_decode($package['document'],true);$found=false;foreach($doc['assets']??[]as $asset)if(($asset['name']??'')===($_POST['file']??''))$found=true;if(!$found)site_fail('File unavailable',404);
    $phase=$_POST['phase']??'check';if(!in_array($phase,['check','start','pulse','end','cancel'],true))site_fail('Invalid transfer phase');
    $transferKey=hash('sha256',$parts[0].'|'.($_POST['file']??''));site_transfers($s,$now);
    if($phase==='start'||$phase==='pulse'){if(!isset($s['transfers'][$transferKey])&&count($s['transfers'])>=1000)site_fail('Site busy',429);if(!isset($s['transfers'][$transferKey])&&$phase==='start')$s['downloads']=($s['downloads']??0)+1;$s['transfers'][$transferKey]=['at'=>$now];}
    elseif($phase==='end'||$phase==='cancel')unset($s['transfers'][$transferKey]);
    $renew=$parts[0].'.'.($now+300);$out['ticket']=$renew.'.'.hash_hmac('sha256',$id.'|'.($s['access_epoch']??0).'|'.$renew,$db['secret']);
    $out['item']=site_public($s,$now);
   }else{
   $session=$_POST['session']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$session))site_fail('Invalid session');$key=hash('sha256',$session);site_online($s,$now);
   if($action==='leave')unset($s['sessions'][$key]);
   else{
    $ticket=$_POST['ticket']??'';$parts=explode('.',$ticket);if(count($parts)!==3||!ctype_digit($parts[1])||(int)$parts[1]<$now||!hash_equals(hash_hmac('sha256',$id.'|'.($s['access_epoch']??0).'|'.$parts[0].'.'.$parts[1],$db['secret']),$parts[2]))site_fail('Visit ticket expired',403);
    $existing=$s['sessions'][$key]??null;if(!$existing){if(count($s['sessions'])>=1000)site_fail('Site busy',429);$s['visits']++;}
    $s['sessions'][$key]=['at'=>$now];
    // Renew only an active browser session, not a downloaded/cached package.
    $ticket=$parts[0].'.'.($now+300);$out['ticket']=$ticket.'.'.hash_hmac('sha256',$id.'|'.($s['access_epoch']??0).'|'.$ticket,$db['secret']);
   }
   $out['item']=site_public($s,$now);
   }
  }unset($s);
 }
 elseif(in_array($action,['publish','stats','stop','host_stop','topup','local'],true)){
  $owner=site_user($_SERVER['HTTP_X_AUTH_TOKEN']??$_POST['token']??'',$users);$out['owner']=$owner;$world=$_POST['world']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$world))site_fail('Invalid save identity');
  if($action==='publish'){
   $key=$_POST['key']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$key))site_fail('Invalid publication identity');$raw=$_POST['document']??'';$doc=site_validate($raw);
   $id='';foreach($db['sites']as $k=>$site)if($site['owner']===$owner&&$site['world']===$world&&$site['key']===$key){$id=(string)$k;break;}
   if($id===''){ $owned=0;foreach($db['sites']as $entry)if($entry['owner']===$owner)$owned++;if($owned>=16)site_fail('16 publications per account maximum',409);if(count($db['sites'])>=200)site_fail('Host full',503);$id=(string)$db['next']++;}
   $old=$db['sites'][$id]??null;if($old&&!empty($old['banned']))site_fail('Сайт заблокирован администрацией',403);$url=strtolower(trim($_POST['url']??''));$url=preg_replace('~^https?://~','',$url);$url=rtrim($url,'/');
   if($url==='')$url=$old?$old['url']:'s'.$id.'.sites.pcos';
   if(!preg_match('/^(?:www\.)?[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,}$/D',$url)||strlen($url)>160||strpos($url,'..')!==false)site_fail('Invalid game URL');
   $reserved=json_decode(file_get_contents(__DIR__.'/reserved.json'),true);if(in_array($url,$reserved?:[],true)||preg_match('/(?:^|\.)(?:byethost\d*\.com|sites\.pcos)$/D',$url)&&$url!=='s'.$id.'.sites.pcos')site_fail('Reserved URL',409);
   foreach($db['sites']as $k=>$site)if((string)$k!==$id&&$site['url']===$url)site_fail('URL already taken',409);
   $custom=$url!=='s'.$id.'.sites.pcos';$fresh=!$old||!$old['active']||!empty($old['unpaid']);
   $paid=$old?site_custom_paid($old):false;
   // A fresh or restarted publication prepays one period of hosting as seconds:
   // $100 / 1800 s, or $1100 / 1800 s with a custom domain (the custom rate
   // includes the one-time $1000 domain fee, exactly as before). Converting an
   // already paid publication to a custom domain charges the fee once.
   // Publication itself is free: nothing is charged from the game balance when a
   // site goes live. The only up-front charge is the one-time custom-domain fee,
   // and even that is paid from the hosting wallet (filled with action=topup),
   // never straight from the game.
   $is_default=$old&&$old['url']==='s'.$id.'.sites.pcos';
   $custom_fee=($custom&&(!$paid||$is_default))?SITE_CUSTOM_PRICE:0;
   $cost=$custom_fee;
   // The exact fixed tariff must be confirmed; the client cannot submit price,
   // paid-until timestamps or a negative charge.
   if(($_POST['confirm']??'')!=='100/1800/1000')site_fail('Confirm tariff first',409);
   // A publication may exist without a wallet: it simply does not run until the
   // player tops the wallet up (site_reconcile pauses it), but the domain and the
   // package are kept so nothing is lost. Only a paid domain fee blocks now.
   if($cost>0&&site_available_funds($db,$owner,$world)<$cost)site_fail('Недостаточно денег в кошельке хостинга: пополни его',402);
   $s=$old?:['id'=>(int)$id,'owner'=>$owner,'world'=>$world,'key'=>$key,'visits'=>0,'sessions'=>[],'total'=>0,'revision'=>0];
   if($old&&$old['url']!==$url){$s['sessions']=[];$s['transfers']=[];$s['access_epoch']=($s['access_epoch']??0)+1;}
   $s['custom_paid']=$paid||$custom;[$s['icon_data'],$s['icon_media']]=site_icon_data($doc);
   $s['active']=true;$s['paused']=false;$s['pause_reason']='';$s['unpaid']=false;
   // A fresh publication prepays one period of seconds; an update only adds the
   // one-time custom-domain fee (if the domain was upgraded just now).
   if($fresh){
    // No prepaid period any more: the wallet pays as the site serves, and it is
    // the player who decides when to move money into that wallet.
    $s['run_from']=$now;$s['billed_seconds']=0;$s['run_seconds']=0;$s['credit']=0;
    $s['traffic_seconds']=0;$s['traffic_billed']=0;$s['traffic_paid']=0;$s['busy_at']=0;
    $s['total']+=$cost;
   }else{
    $s['total']+=$custom_fee;
   }$s['url']=$url;$s['title']=$doc['title'];$s['description']=$doc['description']??'';$s['keywords']=$doc['keywords']??'';$s['bytes']=strlen($raw);$s['host']=(int)($_POST['host']??0);$s['revision']++;
   site_write($dir.'/p_'.$id.'.php',['document'=>$raw]);$db['sites'][$id]=$s;$out['charged']=$cost;$published_id=$id;
  }
  // Local hosting: register or update a site that stays on the player's disk.
  // `action=local` never stores a package on the web host.
  if($action==='local'){
   $domain=strtolower(trim($_POST['url']??''));$domain=preg_replace('~^https?://~','',$domain);$domain=rtrim($domain,'/');
   if(!preg_match('/^(?:www\.)?[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,}$/D',$domain)||strlen($domain)>160||strpos($domain,'..')!==false)site_fail('Invalid game URL');
   $reserved=json_decode(file_get_contents(__DIR__.'/reserved.json'),true);
   if(in_array($domain,$reserved?:[],true)||preg_match('/(?:^|\.)(?:byethost\d*\.com|sites\.pcos)$/D',$domain))site_fail('Reserved URL',409);
   foreach($db['sites']as $known=>$site)if($site['url']===$domain&&!($site['owner']===$owner&&$site['world']===$world&&site_local($site)))site_fail('URL already taken',409);
   if(($_POST['confirm']??'')!=='100/1800/1000')site_fail('Confirm tariff first',409);
   // The tariff follows the domain, not the client's claim.
   $custom=!preg_match('~^local\.~',$domain);
   $rate=$custom?SITE_RATE_CUSTOM:SITE_RATE_STANDARD;
   $found=null;foreach($db['sites']as $known=>$site)if($site['owner']===$owner&&$site['world']===$world&&site_local($site)&&$site['url']===$domain){$found=(string)$known;break;}
   $fresh=$found===null||empty($db['sites'][$found]['active'])||!empty($db['sites'][$found]['unpaid']);
   // Hosting from the local disk costs nothing up front either: only the seconds
   // the game really serves are billed from the wallet, and the custom-domain
   // fee stays a one-time charge.
   $custom_paid=$found!==null&&site_custom_paid($db['sites'][$found]);
   $custom_fee=($custom&&!$custom_paid)?SITE_CUSTOM_PRICE:0;
   $cost=$custom_fee;
   // A publication may exist without a wallet: it simply does not run until the
   // player tops the wallet up (site_reconcile pauses it), but the domain and the
   // package are kept so nothing is lost. Only a paid domain fee blocks now.
   if($cost>0&&site_available_funds($db,$owner,$world)<$cost)site_fail('Недостаточно денег в кошельке хостинга: пополни его',402);
   if($fresh&&site_owned_local($db,$owner,$world)>=16)site_fail('16 publications per account maximum',409);
   if($found===null){
    if(count($db['sites'])>=200)site_fail('Host full',503);
    $found=(string)$db['next']++;
    $db['sites'][$found]=['id'=>(int)$found,'owner'=>$owner,'world'=>$world,'key'=>substr(hash_hmac('sha256',$domain,defined('SITE_DATA_DIR')?SITE_DATA_DIR:__DIR__),0,32),
     'kind'=>'local','url'=>$domain,'title'=>$domain,'description'=>'','keywords'=>'','visits'=>0,'sessions'=>[],'transfers'=>[],
     'total'=>0,'revision'=>1,'banned'=>false,'banned_reason'=>'','credit'=>0,'paid_seconds'=>0,'billed_seconds'=>0,
     'run_seconds'=>0,'client_seconds'=>0,'run_from'=>0,'host'=>0,'bytes'=>0,'cpu_capacity'=>1000,'cpu_load'=>0,'host_mbps'=>5,'icon_data'=>'','icon_media'=>''];
   }
   $s=&$db['sites'][$found];
   $s['rate']=$rate;$s['rate_locked']=true;$s['custom_paid']=$custom;
   // Absolute served total; a repeated request never doubles the rent.
   $s['client_seconds']=max((int)($s['client_seconds']??0),max(0,(int)($_POST['seconds']??0)));
   $title=trim((string)($_POST['title']??$domain));if(!is_string($title))$title=$domain;$s['title']=substr($title,0,120);
   $s['visits']=max((int)$s['visits'],(int)($_POST['visits']??0));
   if($fresh){$s['active']=true;$s['paused']=false;$s['pause_reason']='';$s['unpaid']=false;$s['run_from']=0;$s['billed_seconds']=0;$s['run_seconds']=0;$s['traffic_seconds']=0;$s['traffic_billed']=0;$s['traffic_paid']=0;$s['busy_at']=0;$s['credit']=0;$s['total']+=$cost;}
   $s['revision']=(int)$s['revision']+1;
   $published_id=$found;$out['charged']=$cost;
   unset($s);
  }
  if($action==='stop'){$id=(string)($_POST['id']??'');if(!isset($db['sites'][$id])||$db['sites'][$id]['owner']!==$owner||$db['sites'][$id]['world']!==$world)site_fail('Not your publication',403);$db['sites'][$id]['active']=false;$db['sites'][$id]['unpaid']=false;$db['sites'][$id]['paused']=false;$db['sites'][$id]['pause_reason']='';$db['sites'][$id]['run_from']=0;$db['sites'][$id]['credit']=0;$db['sites'][$id]['sessions']=[];$db['sites'][$id]['transfers']=[];$db['sites'][$id]['access_epoch']=($db['sites'][$id]['access_epoch']??0)+1;$out['item']=site_public($db['sites'][$id],$now);}
  // Explicit top-up: move money from the game balance to the hosting wallet so
  // rent can keep running while the player is offline. The client sends the new
  // balance snapshot together with the amount; the amount is added to the
  // ceiling, so it is charged from the game exactly once.
  if($action==='topup'){
   $amount=$_POST['amount']??'';site_topup($db,$owner,$world,$amount);
   $out['charged']=(int)$amount;$out['balance']=$_POST['balance']??null;
  }
  // Re-evaluate runway/pause flags after this request's mutation, so a fresh
  // publication already reports its "time left" without waiting for a poll.
  site_reconcile($db,$now);
  $out['items']=[];$out['total']=0;
  foreach($db['sites']as &$s)if($s['owner']===$owner&&$s['world']===$world){$private=site_public($s,$now);$private['publication_key']=$s['key'];$out['items'][]=$private;$out['total']+=$s['total'];}unset($s);
  // The published publication is reported with its post-reconcile state.
  if($action==='publish'&&isset($published_id)&&isset($db['sites'][$published_id])){$item=site_public($db['sites'][$published_id],$now);$item['publication_key']=$db['sites'][$published_id]['key'];$out['item']=$item;}
  $wallet=$db['wallets'][site_wallet_key($owner,$world)]??null;
  // `available` is the known cash left for this save; null means "not synced
  // yet" (money lives in the game save, the backend cannot see it).
  $out['wallet']=['known'=>$wallet!==null,'ceiling'=>$wallet!==null?(int)$wallet['ceiling']:0,'spent'=>(int)$out['total'],'available'=>$wallet!==null?site_available_funds($db,$owner,$world):0];
 }
 else site_fail('Unknown action',404);
 site_write($dir.'/state.php',$db);flock($lock,LOCK_UN);fclose($lock);$lock=null;echo json_encode($out,JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES|JSON_THROW_ON_ERROR);
}catch(Throwable $e){if(is_resource($lock)){flock($lock,LOCK_UN);fclose($lock);} $code=$e->getCode();http_response_code($code>=400&&$code<=599?$code:503);echo json_encode(['ok'=>false,'service'=>'orange-sites','version'=>1,'error'=>$code>=400&&$code<=499?$e->getMessage():'Site service unavailable']);}
