#!/usr/bin/env python3
"""Hosting money model v3: wallet-only rent, base tariff + traffic surcharge.

Runs the real PHP API against synthetic accounts in a temporary tree through
`php -r`, never against production. Time is advanced by editing `run_from`,
`busy_at`, session and transfer timestamps of stored records, exactly like the
other site backend tests do.
"""
import json,subprocess,tempfile,time
from pathlib import Path
R=Path(__file__).resolve().parents[1];checks=0
def check(value):
 global checks
 checks+=1
 if not value:
  import inspect
  raise AssertionError('hosting seconds check %d at line %d'%(checks,inspect.stack()[1].lineno))
with tempfile.TemporaryDirectory()as td:
 t=Path(td);data=t/'data';users=t/'users.json';token='a'*64
 users.write_text(json.dumps([{'id':1,'token':token,'verified':True},{'id':2,'token':'b'*64,'verified':True}]))
 runner=t/'runner.php';runner.write_text("<?php define('SITE_DATA_DIR',"+json.dumps(str(data))+
  ");define('SITE_USERS_FILE',"+json.dumps(str(users))+
  ");$_POST=json_decode(stream_get_contents(STDIN),true);$_SERVER['REQUEST_METHOD']='POST';$_SERVER['REMOTE_ADDR']='127.0.0.1';$_SERVER['HTTP_X_AUTH_TOKEN']=$_POST['_token']??'';require "+json.dumps(str(R/'Hosting/sites/api.php'))+";")
 lib=json.dumps(str(R/'Hosting/sites/lib.php'));state=json.dumps(str(data/'state.php'))
 def call(action,**values):
  values['action']=action
  p=subprocess.run(['php',str(runner)],input=json.dumps(values),text=True,capture_output=True,check=True)
  assert not p.stderr,p.stderr
  return json.loads(p.stdout)
 def php(code):
  p=subprocess.run(['php','-r',code],capture_output=True,text=True,check=True);assert not p.stderr,p.stderr;return p.stdout
 def advance(seconds,world=None):
  world=world or WORLD
  # Shift the whole simulated clock: run start, visitor beats, transfers, traffic
  # window. Otherwise a heartbeat would look older than the traffic gap.
  php(f"require {lib};$db=site_read({state});foreach($db['sites']as &$s)if($s['world']==={json.dumps(world)}&&!empty($s['active'])){{$s['run_from']-={seconds};if(!empty($s['busy_at']))$s['busy_at']-={seconds};foreach($s['sessions']??[]as $k=>$r)$s['sessions'][$k]['at']-={seconds};foreach($s['transfers']??[]as $k=>$r)$s['transfers'][$k]['at']-={seconds};}}unset($s);site_write({state},$db);")
 WORLD=world='1'*32;key='2'*32
 doc={'format':'orange-site','version':1,'title':'Games','description':'demo','keywords':'games','width':960,'height':600,'background':'#FFFFFF','viewportMode':'viewport','elements':[],'assets':[],'pages':[]}
 def publish(**overrides):
  values=dict(_token=token,world=world,key=key,url='',document=json.dumps(doc),confirm='100/1800/1000',host=7,balance='1000',settled='0')
  values.update(overrides);return call('publish',**values)
 def topup(amount,settled='0',target=world):
  return call('topup',_token=token,world=target,amount=str(amount),balance='1000000',settled=settled)
 def stats(target=world):
  return call('stats',_token=token,world=target)

 # --- 1. Publishing is free and the game balance stays untouched.
 first=publish();check(first['ok'] and first['charged']==0 and first['total']==0)
 item=first['item'];check(item['active'] and item['credit']==0 and item['rate']==100 and item['price']==100)
 # Without money in the wallet the site cannot serve: it pauses, nothing is billed.
 advance(1800,world);bare=stats()
 check(bare['total']==0 and bare['items'][0]['paused'] and bare['items'][0]['pause_reason']=='dry')

 # --- 2. The wallet is the only source of money and rent is per second.
 fund=topup(1000);check(fund['ok'] and fund['wallet']['available']==1000 and not fund['items'][0]['paused'])
 advance(1800);time.sleep(1);run=stats()
 check(abs(run['total']-100)<=2 and abs(run['wallet']['available']-900)<=2)   # one $100 period
 check(run['items'][0]['paid_seconds']>=1798 and run['items'][0]['money_known'])
 # Repeated polls never double-charge.
 for _ in range(3):check(abs(stats()['total']-run['total'])<=1)
 # A dry wallet pauses without debt, and time alone never creates money.
 advance(18000);time.sleep(1);dry=stats()
 check(dry['items'][0]['paused'] and dry['wallet']['available']==0 and dry['total']==1000)
 advance(604800);check(stats()['total']==1000)

 # --- 3. Top-up resumes exactly where the site stopped.
 again=topup(500,settled='1000');check(again['ok'] and not again['items'][0]['paused'])
 check(again['wallet']['available']==500)
 advance(900);time.sleep(1);part=stats()
 # 500 dollars buy 9000 s; 900 s of them cost exactly $50.
 check(abs(part['total']-1050)<=2 and abs(part['wallet']['available']-450)<=2)
 check(part['items'][0]['run_seconds']>=899)      # the live counter follows real time

 # --- 4. Snapshot: never mints money, may shrink the wallet, never below top-ups.
 fake=call('stats',_token=token,world=world,balance='9999999',settled='999999')
 check(fake['wallet']['ceiling']==1500 and fake['wallet']['available']==450)
 rollback=call('stats',_token=token,world=world,balance='0',settled='0')
 check(rollback['wallet']['ceiling']==1500 and rollback['wallet']['available']==450)

 # --- 5. Traffic surcharge: paid only for the seconds somebody was really there.
 url=call('list')['items'][0]['url'];seen=call('view',url=url)
 visit=call('visit',id=item['id'],ticket=seen['ticket'],session='4'*32)  # the only visitor
 check(visit['ok'] and visit['item']['online']==1 and visit['item']['traffic_rate']==20)
 before=stats()['total']
 advance(60);time.sleep(1);busy=stats()
 check(busy['items'][0]['traffic_seconds']>=58)                    # really served
 check(busy['items'][0]['traffic_cost']>=0)
 # 60 s of base rent ($100/1800) + 60 s of serving ($20/1800) = $4.
 check(abs(busy['total']-before-4)<=2)
 check(call('leave',id=item['id'],session='4'*32)['item']['online']==0)
 served=busy['items'][0]['traffic_seconds']
 advance(3600);time.sleep(1);quiet=stats()
 # No visitor, no traffic: only the base rent grows.
 check(quiet['items'][0]['traffic_seconds']==served)
 check(abs(quiet['total']-busy['total']-200)<=3)                   # 3600 s = $200

 # --- 6. Stopping ends every charge and the restart is free.
 stop=call('stop',_token=token,world=world,id=item['id']);check(stop['ok'] and not stop['item']['active'])
 frozen=stats()['total'];advance(86400,world);check(stats()['total']==frozen)
 restart=publish();check(restart['ok'] and restart['charged']==0 and restart['item']['credit']==0)

 # --- 7. A custom domain: one-time $1000 fee plus the $1100 tariff, from the wallet.
 topup(3000)
 rename=publish(url='mygames.com');check(rename['ok'] and rename['charged']==1000 and rename['item']['custom_paid'])
 check(rename['item']['rate']==1100 and rename['item']['price']==1100)
 check(publish(url='mygames.com')['charged']==0)                   # paid once
 check(publish(url='other.com')['charged']==0)                     # renames are free
 advance(1800);time.sleep(1);costly=stats()
 # 1800 s of the custom tariff is $1100, billed from the wallet.
 check(abs(costly['total']-rename['total']-1100)<=3)
 # The fee itself needs money: an empty wallet cannot buy a custom domain.
 broke='7'*32;publish(world=broke,key='8'*32,url='');check(not call('publish',_token=token,world=broke,key='8'*32,url='expensive.com',document=json.dumps(doc),confirm='100/1800/1000')['ok'])

 # --- 8. Prepaid seconds still work and are consumed before cash.
 prepay=php(f"require {lib};$db=site_read({state});$now=time();$s=&$db['sites'][{item['id']}];site_topup($db,'1','{world}','1000');$paid=site_prepay($db,$s,1800,$now);site_write({state},$db);echo json_encode(['paid'=>$paid,'credit'=>$s['credit'],'total'=>$s['total']]);")
 prepay=json.loads(prepay);check(prepay['paid']>=1090 and prepay['paid']<=1110 and prepay['credit']>=1800)
 before_credit=prepay['credit'];advance(1800);time.sleep(1)
 check(stats()['items'][0]['credit']<before_credit)                # seconds, not cash

 # --- 9. A legacy per-period record becomes pre-paid seconds once.
 code=f"require {lib};$now=time();$s=['id'=>90,'url'=>'legacy.com','owner'=>'1','world'=>'legacy','active'=>true,'total'=>1100,'billed_at'=>$now-600];$db=['sites'=>[90=>$s],'wallets'=>['1:legacy'=>['gen'=>1,'ceiling'=>9000,'topups'=>9000,'at'=>$now]]];site_reconcile($db,$now);$c=$db['sites'][90]['credit'];if($c!==1200)exit(2);site_reconcile($db,$now+1200);if($db['sites'][90]['total']!==1100)exit(3);site_reconcile($db,$now+1300);if($db['sites'][90]['total']!==1161)exit(4);echo 'ok';"
 check(php(code)=='ok')

 # --- 10. Local hosting: the site stays in the save, the game reports absolute
 # served seconds, and the tariff follows the domain, never the client's claim.
 lworld='6'*32;topup(5000,target=lworld)
 call('local',_token=token,world=lworld,url='sneaky.example.com',confirm='100/1800/1000',seconds=0,rate=100,balance='5000',settled='0')
 cheater=call('stats',_token=token,world=lworld)
 check(cheater['items'][0]['rate']==1100 and cheater['items'][0]['price']==1100)
 l2='5'*32;local=call('local',_token=token,world=l2,url='local.mysite.com',confirm='100/1800/1000',title='Offline build',visits=3,seconds=0,balance='5000',settled='0')
 check(local['ok'] and local['charged']==0 and local['items'][0]['local'] and local['items'][0]['rate']==100)
 topup(600,target=l2)
 call('local',_token=token,world=l2,url='local.mysite.com',confirm='100/1800/1000',seconds=3600)
 billed=call('stats',_token=token,world=l2)
 check(abs(billed['total']-200)<=2 and billed['items'][0]['visits']==3)
 # An absolute, repeated report is free, so a retried request never double-bills.
 call('local',_token=token,world=l2,url='local.mysite.com',confirm='100/1800/1000',seconds=3600)
 check(abs(call('stats',_token=token,world=l2)['total']-billed['total'])<=1)
 call('local',_token=token,world=l2,url='local.mysite.com',confirm='100/1800/1000',seconds=7200)
 check(abs(call('stats',_token=token,world=l2)['total']-400)<=3)
 check(not call('view',url='local.mysite.com')['ok'])              # never served by the web host
 check(not call('local',_token=token,world=l2,url='mygames.com',confirm='100/1800/1000')['ok'])

 # --- 11. A save is its own wallet: no leakage between saves and accounts.
 other=call('stats',_token=token,world='9'*32);check(other['items']==[] and not other['wallet']['known'])
 check(call('stats',_token='b'*64,world=world)['items']==[])
 print('PASS:',checks,'real PHP hosting-money checks: wallet-only rent, free publication, per-second base tariff, traffic surcharge, pause without debt, snapshot ceiling, top-up resume, prepay, custom tariff, legacy credit migration, local hosting idempotency and save isolation')
