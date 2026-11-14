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
 foreach($db['sites']as &$s)site_bill($s,$now);unset($s);
 $out=['ok'=>true,'service'=>'orange-sites','version'=>1,'now'=>$now,'period'=>SITE_PERIOD,'price'=>SITE_PRICE,'custom_price'=>SITE_CUSTOM_PRICE];
 if($action==='health'){$out['available']=true;}
 elseif($action==='list'){
  $q=strtolower(substr(trim($_POST['query']??''),0,160));$out['items']=[];
  foreach($db['sites']as &$s)if($s['active']&&($q===''||strpos(strtolower($s['url'].' '.$s['title'].' '.$s['description'].' '.$s['keywords']),$q)!==false)){$out['items'][]=site_public($s,$now);if(count($out['items'])>=100)break;}unset($s);
 }
 elseif(in_array($action,['view','visit','leave'],true)){
  $id=(string)($_POST['id']??'');if($action==='view'){
   $url=strtolower(trim($_POST['url']??''));$url=preg_replace('~^https?://~','',$url);$domain=explode('/',$url,2)[0];$id='';foreach($db['sites']as $key=>$s)if($s['url']===$domain&&$s['active']){$id=(string)$key;break;}
  }
  if(!isset($db['sites'][$id])||!$db['sites'][$id]['active'])site_fail('Site unavailable',404);$s=&$db['sites'][$id];
  if($action==='view'){
   $package=site_read($dir.'/p_'.$id.'.php');$out['document']=$package['document'];$out['item']=site_public($s,$now);
   $nonce=bin2hex(random_bytes(16));$ticket=$nonce.'.'.($now+300);$out['ticket']=$ticket.'.'.hash_hmac('sha256',$id.'|'.$ticket,$db['secret']);
  }else{
   $session=$_POST['session']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$session))site_fail('Invalid session');$key=hash('sha256',$session);site_online($s,$now);
   if($action==='leave')unset($s['sessions'][$key]);
   else{
    $ticket=$_POST['ticket']??'';$parts=explode('.',$ticket);if(count($parts)!==3||!ctype_digit($parts[1])||(int)$parts[1]<$now||!hash_equals(hash_hmac('sha256',$id.'|'.$parts[0].'.'.$parts[1],$db['secret']),$parts[2]))site_fail('Visit ticket expired',403);
    $existing=$s['sessions'][$key]??null;if(!$existing){if(count($s['sessions'])>=1000)site_fail('Site busy',429);$s['visits']++;}
    $s['sessions'][$key]=['at'=>$now];
    // Renew only an active browser session, not a downloaded/cached package.
    $ticket=$parts[0].'.'.($now+300);$out['ticket']=$ticket.'.'.hash_hmac('sha256',$id.'|'.$ticket,$db['secret']);
   }
   $out['item']=site_public($s,$now);
  }unset($s);
 }
 elseif(in_array($action,['publish','stats','stop'],true)){
  $owner=site_user($_SERVER['HTTP_X_AUTH_TOKEN']??$_POST['token']??'',$users);$out['owner']=$owner;$world=$_POST['world']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$world))site_fail('Invalid save identity');
  if($action==='publish'){
   $key=$_POST['key']??'';if(!preg_match('/^[a-f0-9]{32}$/D',$key))site_fail('Invalid publication identity');$raw=$_POST['document']??'';$doc=site_validate($raw);
   $id='';foreach($db['sites']as $k=>$site)if($site['owner']===$owner&&$site['world']===$world&&$site['key']===$key){$id=(string)$k;break;}
   if($id===''){ $owned=0;foreach($db['sites']as $entry)if($entry['owner']===$owner)$owned++;if($owned>=16)site_fail('16 publications per account maximum',409);if(count($db['sites'])>=200)site_fail('Host full',503);$id=(string)$db['next']++;}
   $old=$db['sites'][$id]??null;$url=strtolower(trim($_POST['url']??''));$url=preg_replace('~^https?://~','',$url);$url=rtrim($url,'/');
   if($url==='')$url=$old?$old['url']:'s'.$id.'.sites.pcos';
   if(!preg_match('/^(?:www\.)?[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,}$/D',$url)||strlen($url)>160||strpos($url,'..')!==false)site_fail('Invalid game URL');
   $reserved=json_decode(file_get_contents(__DIR__.'/reserved.json'),true);if(in_array($url,$reserved?:[],true)||preg_match('/(?:^|\.)(?:byethost\d*\.com|sites\.pcos)$/D',$url)&&$url!=='s'.$id.'.sites.pcos')site_fail('Reserved URL',409);
   foreach($db['sites']as $k=>$site)if((string)$k!==$id&&$site['url']===$url)site_fail('URL already taken',409);
   if($old&&$old['url']!==$url)site_fail('Published URL is immutable; stop and create another project',409);
   $custom=$url!=='s'.$id.'.sites.pcos';$fresh=!$old||!$old['active'];
   $cost=($fresh?SITE_PRICE:0)+(!$old&&$custom?SITE_CUSTOM_PRICE:0);
   // The exact fixed tariff must be confirmed; the client cannot submit price,
   // paid-until timestamps or a negative charge.
   if(($_POST['confirm']??'')!=='100/1800/1000')site_fail('Confirm tariff first',409);
   $s=$old?:['id'=>(int)$id,'owner'=>$owner,'world'=>$world,'key'=>$key,'visits'=>0,'sessions'=>[],'total'=>0,'revision'=>0];
   $s['total']+=$cost;$s['active']=true;if($fresh)$s['billed_at']=$now;$s['url']=$url;$s['title']=$doc['title'];$s['description']=$doc['description']??'';$s['keywords']=$doc['keywords']??'';$s['bytes']=strlen($raw);$s['host']=(int)($_POST['host']??0);$s['revision']++;
   site_write($dir.'/p_'.$id.'.php',['document'=>$raw]);$db['sites'][$id]=$s;$out['item']=site_public($db['sites'][$id],$now);$out['charged']=$cost;
  }
  if($action==='stop'){$id=(string)($_POST['id']??'');if(!isset($db['sites'][$id])||$db['sites'][$id]['owner']!==$owner||$db['sites'][$id]['world']!==$world)site_fail('Not your publication',403);$db['sites'][$id]['active']=false;$db['sites'][$id]['sessions']=[];}
  $out['items']=[];$out['total']=0;
  foreach($db['sites']as &$s)if($s['owner']===$owner&&$s['world']===$world){$private=site_public($s,$now);$private['publication_key']=$s['key'];$out['items'][]=$private;$out['total']+=$s['total'];}unset($s);
 }
 else site_fail('Unknown action',404);
 site_write($dir.'/state.php',$db);flock($lock,LOCK_UN);fclose($lock);$lock=null;echo json_encode($out,JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES|JSON_THROW_ON_ERROR);
}catch(Throwable $e){if(is_resource($lock)){flock($lock,LOCK_UN);fclose($lock);} $code=$e->getCode();http_response_code($code>=400&&$code<=599?$code:503);echo json_encode(['ok'=>false,'service'=>'orange-sites','version'=>1,'error'=>$code>=400&&$code<=499?$e->getMessage():'Site service unavailable']);}
