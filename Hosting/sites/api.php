<?php
ini_set('display_errors','0');
header('Access-Control-Allow-Origin: *');header('Access-Control-Allow-Methods: POST, OPTIONS');header('Access-Control-Allow-Headers: Content-Type, X-Auth-Token');header('Content-Type: application/json; charset=utf-8');header('Cache-Control: no-store');
if(($_SERVER['REQUEST_METHOD']??'')==='OPTIONS'){http_response_code(204);exit;}
require_once __DIR__.'/lib.php';
$dir=defined('SITE_DATA_DIR')?SITE_DATA_DIR:__DIR__.'/data';$users=defined('SITE_USERS_FILE')?SITE_USERS_FILE:dirname(__DIR__).'/workshop/users.json';
$lock=null;
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
 if(in_array($action,['publish','stats','stop','host_stop'],true)){
  $owner=site_user($_SERVER['HTTP_X_AUTH_TOKEN']??$_POST['token']??'',$users);$world=$_POST['world']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$world))site_fail('Invalid save identity');
  if(isset($_POST['balance'])){
   $settled=$_POST['settled']??null;
   if($settled===null){$ledger=json_decode($_POST['ledgers']??'{}',true);$settled=0;if(is_array($ledger)&&is_array($ledger['items']??null)&&count($ledger['items'])<=1000)foreach($ledger['items']as $entry)if(is_array($entry)&&($entry['owner']??'')===$owner)$settled=$entry['charged']??0;}
   site_snapshot($db,$owner,$world,$_POST['balance'],$settled);
  }
  $profiles=json_decode($_POST['hosts']??'{}',true);$profiles=is_array($profiles)?($profiles['items']??[]):[];
  if(is_array($profiles)&&count($profiles)<=200)foreach($profiles as $profile)if(is_array($profile))foreach($db['sites']as &$site)if($site['owner']===$owner&&$site['world']===$world&&$site['host']==($profile['host']??null)){
   foreach(['cpu_capacity'=>[100,1000000],'cpu_load'=>[0,1],'host_mbps'=>[.02,10000]]as $field=>$range){$v=$profile[$field]??null;if(is_numeric($v)&&is_finite((float)$v))$site[$field]=max($range[0],min($range[1],(float)$v));}
  }unset($site);
 }
 site_reconcile($db,$now);
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
   $out['item']=site_public($s,$now);if($action==='view'&&empty($s['unpaid'])&&empty($s['banned'])){$package=site_read($dir.'/p_'.$id.'.php');$out['document']=$package['document'];}
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
 elseif(in_array($action,['publish','stats','stop','host_stop'],true)){
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
   $cost=($fresh?SITE_PRICE+($custom?SITE_CUSTOM_PRICE:0):($custom&&$old['url']==='s'.$id.'.sites.pcos'?SITE_CUSTOM_PRICE:0));
   // The exact fixed tariff must be confirmed; the client cannot submit price,
   // paid-until timestamps or a negative charge.
   if(($_POST['confirm']??'')!=='100/1800/1000')site_fail('Confirm tariff first',409);
   if(site_available_funds($db,$owner,$world)<$cost)site_fail('Хост не оплачен: недостаточно денег на публикацию',402);
   $s=$old?:['id'=>(int)$id,'owner'=>$owner,'world'=>$world,'key'=>$key,'visits'=>0,'sessions'=>[],'total'=>0,'revision'=>0];
   if($old&&$old['url']!==$url){$s['sessions']=[];$s['transfers']=[];$s['access_epoch']=($s['access_epoch']??0)+1;}
   $s['custom_paid']=$paid||$custom;$s['unpaid']=false;[$s['icon_data'],$s['icon_media']]=site_icon_data($doc);
   $s['total']+=$cost;$s['active']=true;if($fresh){$s['billed_at']=$now;$s['recurring_from']=$now;}$s['url']=$url;$s['title']=$doc['title'];$s['description']=$doc['description']??'';$s['keywords']=$doc['keywords']??'';$s['bytes']=strlen($raw);$s['host']=(int)($_POST['host']??0);$s['revision']++;
   site_write($dir.'/p_'.$id.'.php',['document'=>$raw]);$db['sites'][$id]=$s;$out['item']=site_public($db['sites'][$id],$now);$out['charged']=$cost;
  }
  if($action==='stop'){$id=(string)($_POST['id']??'');if(!isset($db['sites'][$id])||$db['sites'][$id]['owner']!==$owner||$db['sites'][$id]['world']!==$world)site_fail('Not your publication',403);$db['sites'][$id]['active']=false;$db['sites'][$id]['unpaid']=false;$db['sites'][$id]['sessions']=[];$db['sites'][$id]['transfers']=[];$db['sites'][$id]['access_epoch']=($db['sites'][$id]['access_epoch']??0)+1;$out['item']=site_public($db['sites'][$id],$now);}
  $out['items']=[];$out['total']=0;
  foreach($db['sites']as &$s)if($s['owner']===$owner&&$s['world']===$world){$private=site_public($s,$now);$private['publication_key']=$s['key'];$out['items'][]=$private;$out['total']+=$s['total'];}unset($s);
 }
 else site_fail('Unknown action',404);
 site_write($dir.'/state.php',$db);flock($lock,LOCK_UN);fclose($lock);$lock=null;echo json_encode($out,JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES|JSON_THROW_ON_ERROR);
}catch(Throwable $e){if(is_resource($lock)){flock($lock,LOCK_UN);fclose($lock);} $code=$e->getCode();http_response_code($code>=400&&$code<=599?$code:503);echo json_encode(['ok'=>false,'service'=>'orange-sites','version'=>1,'error'=>$code>=400&&$code<=499?$e->getMessage():'Site service unavailable']);}
