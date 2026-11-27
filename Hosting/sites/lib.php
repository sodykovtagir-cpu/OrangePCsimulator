<?php
// No account credentials, FTP passwords, or visitor IPs are stored here.
//
// Billing v2 ("time bank", seconds):
//  - SITE_PRICE / SITE_PERIOD is the public price per period: $100 per 30 real
//    minutes, custom domain $1100 per 30 minutes.
//  - The backend stores money in *paid seconds* (SITE_SECOND = 1). Everything
//    is computed in integer micro-dollars so that a period boundary can never
//    round in the player's favour or against them:
//      SITE_CUSTOM_PERIOD = SITE_PRICE + SITE_CUSTOM_PRICE = 1100
//      SITE_CUSTOM_SECOND = SITE_CUSTOM_PERIOD_RATE (integer, see below)
//  - A site buys seconds from the shared wallet of its owner+save. The cash
//    ceiling is the last balance snapshot sent by the game, so the backend can
//    keep charging while the player is offline.
//  - Money never goes below zero on the client: the wallet simply runs dry,
//    the site is paused (`paused`) and keeps its domain, package and history.
//  - Everything is idempotent per wallet generation (`gen`), so a lost save or
//    a repeated request can never charge twice or resurrect old free time.
const SITE_PERIOD = 1800;
const SITE_PRICE = 100;
const SITE_CUSTOM_PRICE = 1000;
// Rent is quoted per second internally (see the money core) so that a period
// never rounds in either direction.
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

// ---------------------------------------------------------------- money core
// Billing v2, "time bank":
//  - Price is still $100 per 1800 real seconds (custom domain $1100 per 1800),
//    but every site holds a credit of paid seconds and is charged per second,
//    not per completed period. Integer math only: no period rounding.
//  - Money is never taken from a site twice: each site has `billed_seconds`
//    for the current billing window and `credit` for prepaid seconds.
//  - Available cash of one account+save is `ceiling - sum(total of its sites)`,
//    where `ceiling` is the last balance snapshot sent by the game
//    (`balance + already settled`). A site can never push the player below
//    zero: when the money runs out the site is paused and keeps its domain,
//    package, visits and billing history.
//  - A paused or stopped site never accrues rent. The billing window is a
//    fixed 1800-second UTC slot only so that usage can be measured exactly
//    once per request, not to charge per period.
// Rent is quoted per second internally so that no period ever rounds.
const SITE_RATE_STANDARD = SITE_PRICE;                   // $ per 1800 seconds
const SITE_RATE_CUSTOM = SITE_PRICE + SITE_CUSTOM_PRICE; // $ per 1800 seconds
const SITE_EPOCH = 1767225600;                           // 2026-01-01T00:00:00Z
// Traffic surcharge: seconds during which the site actually served somebody
// (an open browser session or a running download). A published site nobody
// visits pays only the base rent; a busy one pays a little more.
const SITE_TRAFFIC_RATE = 20;                            // $ per 1800 s of real serving
const SITE_BUSY_WINDOW = 90;                             // a session is online for 90 s after its beat
function site_wallet_key($owner,$world){return $owner.':'.$world;}
function site_rate($s){
 if(isset($s['rate'])&&!empty($s['rate_locked']))return (int)$s['rate']===SITE_RATE_CUSTOM?SITE_RATE_CUSTOM:SITE_RATE_STANDARD;
 return site_period_price($s)===SITE_RATE_CUSTOM?SITE_RATE_CUSTOM:SITE_RATE_STANDARD;
}
// A locally hosted site lives in the player's save, not on this web host: it is
// billed and moderated like any publication but never served over the network.
function site_local($s){return ($s['kind']??'')==='local';}
// Standard publications use the temporary s<number>.sites.pcos address, whether
// it was reserved by the server or generated by the game while offline.
function site_custom_domain($s){return !preg_match('~^s\d+\.sites\.pcos$~',(string)($s['url']??''));}
function site_period_price($s){return SITE_PRICE+(site_custom_domain($s)?SITE_CUSTOM_PRICE:0);}
// Cost of a number of seconds, floor (never invents money).
function site_cost($rate,$seconds){return intdiv(max(0,(int)$rate)*max(0,(int)$seconds),SITE_PERIOD);}
// Seconds affordable with an amount of dollars, floor.
function site_seconds_for($rate,$dollars){$rate=max(1,(int)$rate);return intdiv(max(0,(int)$dollars)*SITE_PERIOD,$rate);}
function site_running($s){return !empty($s['active'])&&empty($s['paused'])&&empty($s['banned']);}
function site_total($db,$owner,$world){$sum=0;foreach($db['sites']as $s)if($s['owner']===$owner&&$s['world']===$world)$sum+=(int)$s['total'];return $sum;}
// Known cash left for this account+save. Money exists only in the wallet the
// player explicitly filled with action=topup: a wallet that was never created
// has nothing, so a publication cannot run on credit the game never agreed to.
function site_available_funds($db,$owner,$world){
 $key=site_wallet_key($owner,$world);
 if(!isset($db['wallets'][$key]))return 0;
 return max(0,(int)$db['wallets'][$key]['ceiling']-site_total($db,$owner,$world));
}
function site_pause(&$s,$reason){
 if(empty($s['paused'])){$s['access_epoch']=($s['access_epoch']??0)+1;$s['sessions']=[];$s['transfers']=[];}
 $s['paused']=true;$s['pause_reason']=$reason;
 $s['run_from']=0;$s['run_seconds']=0;$s['billed_seconds']=0;$s['credit']=0;
 // Whatever traffic was already served stays behind us: a pause never turns
 // past serving into future debt.
 $s['busy_at']=0;$s['traffic_seconds']=max(0,(int)($s['traffic_seconds']??0));$s['traffic_billed']=$s['traffic_seconds'];
 // `unpaid` stays for clients that predate `paused`: a paused site is not
 // served either way and never accrues new rent.
 $s['unpaid']=$reason!=='user';
}
function site_resume(&$s,$now){
 $s['paused']=false;$s['pause_reason']='';$s['unpaid']=false;
 $s['run_from']=$now;$s['run_seconds']=0;$s['billed_seconds']=0;
}
// Seconds of real serving since the site was last reconciled. A browser session
// counts while its heartbeat is fresh, a download while it is open; the gap
// between two requests is capped so a long silence never invents traffic.
function site_busy(&$s,$now){
 $active=false;$oldest=0;
 foreach($s['sessions']??[]as $row)if(($row['at']??0)>$now-SITE_BUSY_WINDOW){$active=true;$oldest=$oldest>0?min($oldest,(int)$row['at']):(int)$row['at'];}
 if(!$active)foreach($s['transfers']??[]as $row)if(($row['at']??0)>$now-30){$active=true;$oldest=$oldest>0?min($oldest,(int)$row['at']):(int)$row['at'];}
 $busy=max(0,(int)($s['traffic_seconds']??0));$last=(int)($s['busy_at']??0);
 // A visitor seen for the first time since the last request: the session beat is
 // the earliest moment we know the site was serving, so the gap starts there.
 if($active&&$last<=0&&$oldest>0&&$oldest<=$now)$last=max(1,$oldest);
 if($active&&$last>0&&$last<=$now)$busy+=min(SITE_BUSY_WINDOW,$now-$last);
 if($active)$s['busy_at']=$now;else $s['busy_at']=0;
 $s['traffic_seconds']=$busy;
 return $busy;
}
// Accrue rent for every running site, once per request. Rent is continuous:
// seconds since `run_from` are priced at the site's rate, minus prepaid seconds
// in `credit`, and the difference against `billed_seconds` is what is due now.
// Money comes from the shared cash ceiling of the owner+save, in publication
// order, and never goes below zero: when it ends the site is paused and the
// uncovered part is written off (site_settle_shortfall), so no debt appears.
function site_reconcile(&$db,$now){
 $db['wallets']=is_array($db['wallets']??null)?$db['wallets']:[];
 foreach($db['sites']as &$s){
  $s['active']=!empty($s['active']);$s['paused']=!empty($s['paused']);
  $s['credit']=max(0,(int)($s['credit']??0));
  $s['paid_seconds']=max(0,(int)($s['paid_seconds']??0));
  $s['total']=max(0,(int)($s['total']??0));
  $s['billed_seconds']=max(0,(int)($s['billed_seconds']??0));
  $s['run_seconds']=max(0,(int)($s['run_seconds']??0));
  if(!array_key_exists('run_from',$s)){
   // Migrated record: rent used to be billed per completed period. Never
   // invent retroactive debt and never grant free time: an active site starts
   // a fresh run now, a site that was already unpaid stays paused.
   $legacy_billed=isset($s['billed_at'])?(int)$s['billed_at']:0;
   unset($s['billed_at'],$s['recurring_from'],$s['window_used']);
   $s['custom_paid']=site_custom_paid($s);
   $s['run_from']=0;$s['run_seconds']=0;$s['billed_seconds']=0;
   if($s['active']&&empty($s['banned'])&&empty($s['unpaid'])){
    $s['run_from']=$now;
    // The old biller charged per completed period, so billed_at+PERIOD was
    // already paid for. Convert the unconsumed part into seconds of credit: the
    // price cancels out because both tariffs are linear in time.
    if($legacy_billed>0)$s['credit']=max(0,min(SITE_PERIOD,$legacy_billed+SITE_PERIOD-(int)$now));
   }
   elseif(!empty($s['unpaid'])&&empty($s['banned'])){$s['paused']=true;$s['pause_reason']='legacy';}
  }
  if(!site_running($s)){if(!$s['active'])$s['run_from']=0;$s['busy_at']=0;continue;}
  // Traffic is billed for every tariff: seconds with a live visitor cost extra.
  $traffic=site_busy($s,$now);
  $traffic_due=site_cost(SITE_TRAFFIC_RATE,$traffic)-site_cost(SITE_TRAFFIC_RATE,(int)($s['traffic_billed']??0));
  if(site_local($s)){
   // A local site lives on the player's disk, so it costs nothing while nobody
   // hosts it: only the seconds the game really served are billed, plus the
   // network time somebody actually spent on it.
   $elapsed=max(0,min(31536000,(int)($s['client_seconds']??0)));
  }else{
   if((int)$s['run_from']<=0)$s['run_from']=$now;
   $elapsed=max(0,$now-(int)$s['run_from']);
  }
  $delta=$elapsed-(int)$s['run_seconds'];
  if($delta<0){$delta=0;$s['run_seconds']=$elapsed;}
  // Prepaid seconds are consumed before any cash is asked for.
  $consume=min((int)$s['credit'],$delta);$s['credit']-=$consume;
  $billable=(int)$s['billed_seconds']+($delta-$consume);
  $rate=site_rate($s);
  $base_due=max(0,site_cost($rate,$billable)-site_cost($rate,(int)$s['billed_seconds']));
  $due=$base_due+$traffic_due;
  if($due<=0){$s['run_seconds']=$elapsed;$s['billed_seconds']=$billable;$s['traffic_billed']=$traffic;$s['paid_seconds']+=$delta-$consume;continue;}
  $owner=$s['owner'];$world=$s['world'];
  $available=site_available_funds($db,$owner,$world);
  if(empty($db['wallets'][site_wallet_key($owner,$world)])){
   // No wallet at all: nothing is owed and nothing runs. The site keeps its
   // served seconds so a later top-up continues exactly where it stopped.
   site_pause($s,'dry');continue;
  }
  if($due<=$available){
   $s['total']+=$due;$s['run_seconds']=$elapsed;$s['billed_seconds']=$billable;
   $s['traffic_billed']=$traffic;$s['paid_seconds']+=$delta-$consume;
   continue;
  }
  // Cash ended during this request: pay only what is known to exist, then pause.
  // The rent is paid first, the traffic surcharge with what is left.
  $base_paid=min($base_due,max(0,$available));
  $traffic_paid=min($traffic_due,max(0,$available-$base_paid));
  $pay=$base_paid+$traffic_paid;
  $s['total']+=$pay;
  $s['paid_seconds']+=site_seconds_for($rate,$base_paid);
  $s['traffic_paid']=(int)($s['traffic_paid']??0)+site_seconds_for(SITE_TRAFFIC_RATE,$traffic_paid);
  site_pause($s,'dry');
 }
 unset($s);
 // Fair share of the remaining cash, for the panel's "time left" estimate.
 $running=[];foreach($db['sites']as $id=>$s)if(site_running($s))$running[$id]=$s;
 foreach($running as $id=>$s){
  $key=site_wallet_key($s['owner'],$s['world']);
  if(!isset($db['wallets'][$key])){$db['sites'][$id]['runway']=0;$db['sites'][$id]['money_known']=false;continue;}
  $peers=0;foreach($running as $other)if(site_wallet_key($other['owner'],$other['world'])===$key)$peers++;
  $db['sites'][$id]['money_known']=true;
  // The estimate assumes one visitor per site, so it never over-promises.
  $db['sites'][$id]['runway']=site_seconds_for(site_rate($s)+SITE_TRAFFIC_RATE,intdiv(site_available_funds($db,$s['owner'],$s['world']),max(1,$peers)));
 }
 return $db;
}
// Latest balance snapshot from the game: the ceiling of known cash for this
// account+save. Repeated identical snapshots are harmless.
// Merge the publications that the game hosts locally into the same wallet, so
// their rent is deducted from the same money and shares the same ceiling.
// `seconds` is the total served time reported by the game, not a price.
function site_locals(&$db,$owner,$world,$entries,$now){
 if(!is_array($entries)||count($entries)>16)return [];
 $out=[];
 foreach($entries as $entry){
  if(!is_array($entry))continue;
  $domain=strtolower(trim((string)($entry['domain']??'')));
  if(!preg_match('/^(?:www\.)?[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,}$/D',$domain)||strlen($domain)>160||strpos($domain,'..')!==false)continue;
  $seconds=$entry['seconds']??null;$rate=$entry['rate']??SITE_RATE_STANDARD;
  if(!is_scalar($seconds)||!ctype_digit((string)$seconds)||(int)$seconds>31536000)continue;
  // The tariff follows the domain: a custom address is never billed as a
  // generated one, whatever the client claims.
  $rate=preg_match('~^(?:local\.|s\d+\.sites\.pcos$)~',$domain)?SITE_RATE_STANDARD:SITE_RATE_CUSTOM;
  $key=substr(hash_hmac('sha256',$domain,defined('SITE_DATA_DIR')?SITE_DATA_DIR:__DIR__),0,32);
  $id=null;
  foreach($db['sites']as $known=>$s)if($s['owner']===$owner&&$s['world']===$world&&site_local($s)&&$s['url']===$domain){$id=$known;break;}
  if($id===null){
   if(count($db['sites'])>=200)continue;
   $clash=false;foreach($db['sites']as $s)if($s['url']===$domain)$clash=true;
   if($clash)continue;
   $id=(string)$db['next']++;
   $db['sites'][$id]=['id'=>(int)$id,'owner'=>$owner,'world'=>$world,'key'=>$key,'kind'=>'local','url'=>$domain,
    'title'=>$domain,'description'=>'','keywords'=>'','visits'=>(int)($entry['visits']??0),'sessions'=>[],'transfers'=>[],
    'total'=>0,'revision'=>1,'active'=>true,'paused'=>false,'pause_reason'=>'','unpaid'=>false,'banned'=>false,
    'credit'=>0,'paid_seconds'=>0,'billed_seconds'=>0,'run_seconds'=>0,'client_seconds'=>0,'run_from'=>0,
    'rate'=>$rate,'rate_locked'=>true,'custom_paid'=>$rate===SITE_RATE_CUSTOM,'host'=>0,'bytes'=>0,
    'cpu_capacity'=>1000,'cpu_load'=>0,'host_mbps'=>5,'icon_data'=>'','icon_media'=>''];
  }
  $s=&$db['sites'][$id];
  $s['rate']=$rate;$s['rate_locked']=true;$s['custom_paid']=$rate===SITE_RATE_CUSTOM;
  // Absolute total reported by the game: max() keeps this idempotent.
  $s['client_seconds']=max((int)($s['client_seconds']??0),max(0,(int)$seconds));
  $s['visits']=max((int)$s['visits'],(int)($entry['visits']??0));
  $out[$id]=$s['url'];
  unset($s);
 }
 return $out;
}
function site_snapshot(&$db,$owner,$world,$balance,$settled,$deposited=null){
 if(!is_scalar($balance)||!ctype_digit((string)$balance))site_fail('Invalid balance snapshot');
 if((float)$balance>9999999)site_fail('Invalid balance snapshot');
 if($settled!==null&&(!is_scalar($settled)||!ctype_digit((string)$settled)))site_fail('Invalid balance snapshot');
 if($deposited!==null&&(!is_scalar($deposited)||!ctype_digit((string)$deposited)))site_fail('Invalid balance snapshot');
 $limit=$deposited===null?null:(int)$deposited;
 $key=site_wallet_key($owner,$world);
 $total=site_total($db,$owner,$world);
 // The game balance is *not* hosting money any more: only `action=topup` moves
 // cash into the wallet. The snapshot is used for two things: it tells the
 // backend how much the save has already paid into hosting, and it lets a save
 // rollback take that money back (the player really got it back in the game).
 $paid=$settled===null?null:min((int)$settled,$total);
 if(!isset($db['wallets'][$key])){
  $ceiling=$paid===null?0:$paid;
  $db['wallets'][$key]=['gen'=>1,'ceiling'=>$ceiling,'topups'=>$ceiling,'at'=>time(),'balance'=>(int)$balance];
 }else{
  $wallet=&$db['wallets'][$key];
  $wallet['gen']=(int)$db['wallets'][$key]['gen']+1;$wallet['at']=time();$wallet['balance']=(int)$balance;
  if(!isset($wallet['topups']))$wallet['topups']=(int)$wallet['ceiling'];
  // The ceiling never grows from a snapshot: a modified client could claim any
  // balance. It may shrink.
  if($limit!==null){
   // The save reports exactly how much it moved into hosting. Loading an older
   // save takes that money back in the game, so it is taken back here as well:
   // the same dollars never sit in the save and in the wallet at once. Spent
   // rent stays spent, so the wallet never goes below what was already billed.
   $wallet['ceiling']=max($total,min((int)$wallet['ceiling'],$limit));
  }elseif($paid!==null){
   // Older builds do not report deposits: the money stays, and a rollback can
   // only remove what was never received by this backend.
   $wallet['ceiling']=min((int)$wallet['ceiling'],max($paid,(int)$wallet['topups']));
  }
  unset($wallet);
 }
 // Fresh money restarts sites that were only paused because the cash ran out.
 // A site paused by its owner or by moderation stays stopped.
 foreach($db['sites']as &$s){
  if($s['owner']!==$owner||$s['world']!==$world||!$s['active']||!empty($s['banned'])||empty($s['paused']))continue;
  if(($s['pause_reason']??'')!=='dry')continue;
  if(site_available_funds($db,$owner,$world)>0)site_resume($s,time());
 }
 unset($s);
 return $db;
}
// The player reported a balance and the bill is larger than the cash that is
// known to exist: the game never goes negative, so the part that cannot be paid
// is written off instead of becoming debt. Only already-paused sites lose
// their uncovered part; running sites keep their bill.
function site_settle_shortfall(&$db,$owner,$world){
 $key=site_wallet_key($owner,$world);if(!isset($db['wallets'][$key]))return 0;
 $ceiling=max(0,(int)$db['wallets'][$key]['ceiling']);
 $over=site_total($db,$owner,$world)-$ceiling;
 if($over<=0)return 0;
 $written=0;
 $ids=[];foreach($db['sites']as $id=>$s)if($s['owner']===$owner&&$s['world']===$world&&!empty($s['paused'])&&empty($s['banned']))$ids[]=$id;
 rsort($ids);
 foreach($ids as $id){
  if($over<=0)break;
  $cut=min($over,(int)$db['sites'][$id]['total']);
  if($cut<=0)continue;
  $db['sites'][$id]['total']-=$cut;$db['sites'][$id]['written_off']=(int)($db['sites'][$id]['written_off']??0)+$cut;
  $over-=$cut;$written+=$cut;
 }
 return $written;
}
// Explicitly add money to the hosting wallet from the game balance.
function site_topup(&$db,$owner,$world,$amount){
 if(!is_scalar($amount)||!ctype_digit((string)$amount))site_fail('Invalid top-up');
 $amount=(int)$amount;if($amount<1||$amount>10000000)site_fail('Invalid top-up');
 $key=site_wallet_key($owner,$world);
 // The very first top-up creates the wallet: money exists only once the player
 // has moved it here, so there is nothing to migrate or to trust from the client.
 if(!isset($db['wallets'][$key]))$db['wallets'][$key]=['gen'=>0,'ceiling'=>0,'topups'=>0,'at'=>time()];
 $db['wallets'][$key]['ceiling']=(int)$db['wallets'][$key]['ceiling']+$amount;
 // `topups` is the money this backend really received: the ceiling may fall back
 // to it on a save rollback, never above it.
 $db['wallets'][$key]['topups']=(isset($db['wallets'][$key]['topups'])?(int)$db['wallets'][$key]['topups']:(int)$db['wallets'][$key]['ceiling']-$amount)+$amount;
 $db['wallets'][$key]['gen']=(int)$db['wallets'][$key]['gen']+1;
 foreach($db['sites']as &$s){
  if($s['owner']!==$owner||$s['world']!==$world||!$s['active']||!empty($s['banned'])||!$s['paused'])continue;
  if($s['pause_reason']==='dry')site_resume($s,time());
 }
 unset($s);
 return $amount;
}
// Prepay paid seconds with money already on the hosting wallet.
function site_prepay(&$db,&$s,$seconds,$now){
 $seconds=max(0,(int)$seconds);if($seconds===0)return 0;
 $rate=site_rate($s);
 $affordable=site_seconds_for($rate,site_available_funds($db,$s['owner'],$s['world']));
 $buy=min($seconds,$affordable);
 if($buy<=0)site_fail('Хост не оплачен: недостаточно денег на публикацию',402);
 $price=max(1,site_cost($rate,$buy));
 $s['credit']=(int)($s['credit']??0)+$buy;
 $s['total']=(int)($s['total']??0)+$price;
 return $price;
}
function site_bill(&$s,$now){ /* legacy hook: rent is continuous now */ }
function site_suspend(&$s){site_pause($s,'dry');}
function site_stop_host(&$db,$owner,$world,$host){foreach($db['sites']as &$s)if($s['owner']===$owner&&$s['world']===$world&&$s['host']===$host&&$s['active']){$s['active']=false;$s['paused']=false;$s['unpaid']=false;$s['sessions']=[];$s['transfers']=[];$s['access_epoch']=($s['access_epoch']??0)+1;$s['run_from']=0;$s['credit']=0;}unset($s);}
function site_transfers(&$s,$now){$active=[];foreach($s['transfers']??[]as $k=>$v)if($v['at']>$now-30)$active[$k]=$v;$s['transfers']=$active;return count($active);}
function site_online(&$s,$now){$active=[];foreach($s['sessions']??[]as $key=>$row)if($row['at']>$now-90)$active[$key]=$row;$s['sessions']=$active;return count($active);}
function site_custom_paid($s){return array_key_exists('custom_paid',$s)?(bool)$s['custom_paid']:site_custom_domain($s);}
function site_public(&$s,$now){
 $online=site_online($s,$now);$paused=!empty($s['paused'])&&empty($s['banned']);
 return ['id'=>$s['id'],'url'=>$s['url'],'title'=>$s['title'],'description'=>$s['description'],
  'downloads'=>$s['downloads']??0,'transferring'=>site_transfers($s,$now),'visits'=>$s['visits'],'online'=>$online,
  'active'=>(bool)$s['active'],'total'=>(int)$s['total'],'unpaid'=>!empty($s['unpaid'])||$paused,'paused'=>$paused,
  'pause_reason'=>$paused?(string)($s['pause_reason']??''):'',
  'paid_seconds'=>(int)($s['paid_seconds']??0),'credit'=>(int)($s['credit']??0),
  'run_seconds'=>(int)($s['run_seconds']??0),'runway'=>(int)($s['runway']??0),'money_known'=>!empty($s['money_known']),
  'price'=>site_period_price($s),'rate'=>site_rate($s),'traffic_rate'=>SITE_TRAFFIC_RATE,
  'traffic_seconds'=>(int)($s['traffic_seconds']??0),'traffic_billed'=>(int)($s['traffic_billed']??0),
  'traffic_cost'=>site_cost(SITE_TRAFFIC_RATE,(int)($s['traffic_billed']??0)),
  'banned'=>!empty($s['banned']),'ban_reason'=>$s['ban_reason']??'','icon_data'=>$s['icon_data']??'','icon_media'=>$s['icon_media']??'',
  'cpu_capacity'=>$s['cpu_capacity']??1000,'cpu_load'=>$s['cpu_load']??0,'host_mbps'=>$s['host_mbps']??5,
  'custom_paid'=>site_custom_paid($s),
  'reserved'=>!empty($s['reserved']),
  'next_bill'=>(site_running($s)&&!empty($s['money_known']))?$now+(int)($s['runway']??0):0,
  'host'=>$s['host'],'bytes'=>$s['bytes'],'revision'=>$s['revision'],'local'=>site_local($s),'domain'=>$s['url']];
}
function site_user($token,$usersFile){if(!is_string($token)||strlen($token)<32||strlen($token)>256)site_fail('Login required',401);$raw=@file_get_contents($usersFile);$users=json_decode($raw?:'[]',true);foreach(is_array($users)?$users:[]as $u)if(!empty($u['token'])&&hash_equals((string)$u['token'],$token)&&!empty($u['verified']))return (string)$u['id'];site_fail('Session expired',401);}
function site_icon_data($doc){
 foreach($doc['assets']??[]as $a)if(($a['name']??'')===($doc['icon']??'')){
  $raw=base64_decode($a['data'],true);if(!$raw)return ['',''];$size=@getimagesizefromstring($raw);if(!$size)return ['',''];
  if(function_exists('imagecreatefromstring')){$src=@imagecreatefromstring($raw);if($src){$w=max(1,(int)round($size[0]*min(1,96/$size[0],96/$size[1])));$h=max(1,(int)round($size[1]*min(1,96/$size[0],96/$size[1])));$dst=imagecreatetruecolor($w,$h);imagealphablending($dst,false);imagesavealpha($dst,true);imagecopyresampled($dst,$src,0,0,0,0,$w,$h,$size[0],$size[1]);ob_start();imagepng($dst);$png=ob_get_clean();imagedestroy($dst);imagedestroy($src);if(strlen($png)<=65536)return [base64_encode($png),'image/png'];}}
  if(strlen($raw)<=65536&&$size[0]<=256&&$size[1]<=256)return [$a['data'],$a['media']];return ['',''];
 }return ['',''];
}
