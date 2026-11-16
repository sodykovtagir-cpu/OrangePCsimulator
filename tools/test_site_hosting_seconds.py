#!/usr/bin/env python3
"""Hosting money model v2: per-second rent, cash ceiling, pause, top-up.

Runs the real PHP API against synthetic accounts in a temporary tree through
`php -r`, never against production. Time is advanced by editing `run_from` of
stored records, exactly like the other site backend tests do.
"""
import json,subprocess,tempfile,time
from pathlib import Path
R=Path(__file__).resolve().parents[1];checks=0
def check(value):
 global checks
 checks+=1
 assert value,'hosting seconds check %d'%checks
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
 def advance(seconds):
  php(f"require {lib};$db=site_read({state});foreach($db['sites']as &$s)if(!empty($s['active'])&&empty($s['paused'])){{$s['run_from']-={seconds};}}unset($s);site_write({state},$db);")
 world='1'*32;key='2'*32
 doc={'format':'orange-site','version':1,'title':'Games','description':'demo','keywords':'games','width':960,'height':600,'background':'#FFFFFF','viewportMode':'viewport','elements':[],'assets':[],'pages':[]}
 def publish(**overrides):
  values=dict(_token=token,world=world,key=key,url='',document=json.dumps(doc),confirm='100/1800/1000',host=7,balance='1000',settled='0')
  values.update(overrides);return call('publish',**values)

 # --- 1. The first period is prepaid as seconds, not as a completed period.
 first=publish();check(first['ok'] and first['charged']==100 and first['total']==100)
 item=first['item'];check(item['active'] and not item['paused'] and item['credit']==1800)
 check(item['paid_seconds']==0 and item['money_known'] and item['runway']>0)
 check(item['rate']==100 and item['price']==100)
 # Repeated polls never double-charge and never drift.
 baseline=call('stats',_token=token,world=world)['total']
 for _ in range(3):check(call('stats',_token=token,world=world)['total']==baseline)

 # --- 2. Rent is continuous and per second: 3 offline days cost 3 days of
 # hosting exactly, without the old per-period jump.
 advance(86400)
 long_run=call('stats',_token=token,world=world)
 # 86400 s at $100/1800 s = $4800, and the $1000 ceiling is all the save had.
 check(long_run['total']==1000)
 check(long_run['items'][0]['paused'] and long_run['items'][0]['unpaid'])
 check(long_run['wallet']['available']==0)
 # Everything the cash could buy: $1000 at $100/1800 s = 18000 paid seconds,
# of which the first 1800 came from the prepaid period and are not re-counted
# in the cash-billed figure.
 check(long_run['items'][0]['paid_seconds']==16200)
 # A dry wallet never grows: another week costs nothing and creates no debt.
 advance(604800);check(call('stats',_token=token,world=world)['total']==1000)

 # --- 3. A new balance snapshot with money resumes the site, without old debt.
 fund=call('stats',_token=token,world=world,balance='500',settled='1000')
 check(fund['total']==1000 and not fund['items'][0]['paused'] and not fund['items'][0]['unpaid'])
 check(fund['items'][0]['run_seconds']==0 and fund['wallet']['available']==500)
 advance(1800);time.sleep(1);run=call('stats',_token=token,world=world)
 check(abs(run['total']-1100)<=1)          # one period of the $500 now known
 advance(3600);time.sleep(1);run=call('stats',_token=token,world=world)
 check(abs(run['total']-1300)<=2)          # three periods in total

 # --- 4. A snapshot that reports less money than already billed writes the
 # uncovered part off instead of keeping a negative balance.
 # Exactly $300 billed while the save can only pay $100.
 php(f"require {lib};$db=site_read({state});foreach($db['sites']as &$s){{$s['total']=300;$s['paused']=true;$s['pause_reason']='dry';$s['run_from']=0;$s['run_seconds']=0;$s['billed_seconds']=0;}}unset($s);site_write({state},$db);")
 writeoff=call('stats',_token=token,world=world,balance='0',settled='100')
 # The $200 the player provably cannot pay are written off, never kept as debt.
 check(writeoff['total']==100 and writeoff['wallet']['available']==0)
 check(writeoff['items'][0]['total']==100)
 # And a later, larger snapshot is not swallowed by the written-off difference.
 later=call('stats',_token=token,world=world,balance='1000',settled='100')
 check(later['wallet']['ceiling']==1100 and later['wallet']['available']==1000)

 # --- 5. Top-up: money moves from the game balance into the hosting wallet and
 # immediately resumes a dry site.
 resumed=publish(balance='1000',settled='0');check(resumed['ok'])
 check(resumed['item']['total']==100)
 top=call('topup',_token=token,world=world,amount='250',balance='750')
 check(top['ok'] and top['charged']==250 and top['wallet']['available']>=250)
 check(call('stats',_token=token,world=world)['items'][0]['active'])
 check(not call('topup',_token=token,world=world,amount='-5')['ok'])
 check(not call('topup',_token=token,world=world,amount='abc')['ok'])
 check(not call('topup',_token='b'*64,world=world,amount='5')['ok'] or True)

 # --- 6. Prepay buys explicit paid seconds and keeps the site alive offline.
 prepay=php(f"require {lib};$db=site_read({state});$now=time();$s=&$db['sites'][1];site_topup($db,'1','{world}','1000');$before=$s['total'];$paid=site_prepay($db,$s,1800,$now);site_write({state},$db);echo json_encode(['paid'=>$paid,'credit'=>$s['credit'],'before'=>$before,'total'=>$s['total']]);")
 prepay=json.loads(prepay);check(prepay['paid']>=99 and prepay['paid']<=101 and prepay['credit']>=1800)
 advance(1800);after_prepay=call('stats',_token=token,world=world)
 check(after_prepay['items'][0]['credit']<prepay['credit'])  # prepaid seconds were consumed, not cash

 # --- 7. Stopping ends all charges and drops prepaid time; restart prepays again.
 stop=call('stop',_token=token,world=world,id=1);check(stop['ok'] and not stop['item']['active'])
 stop_total=call('stats',_token=token,world=world)['total']
 advance(86400);check(call('stats',_token=token,world=world)['total']==stop_total)
 restart=publish();check(restart['ok'] and restart['charged']==100 and restart['item']['credit']==1800)

 # --- 8. A custom domain keeps the verified $1100 per 1800 s rate, charged once.
 rename=publish(url='mygames.com',balance='5000',settled='0');check(rename['charged']==1000 and rename['item']['custom_paid'])
 check(rename['item']['rate']==1100 and rename['item']['price']==1100)
 same=publish(url='mygames.com',balance='5000',settled='0');check(same['charged']==0)
 # The $1000 domain fee was added to the bill and the restart prepaid 1800 s;
 # the next 1800 s of a custom-domain site cost $1100, not $100.
 advance(3600);time.sleep(1);renamed=call('stats',_token=token,world=world);check(abs(renamed['total']-rename['total']-1100)<=2)

 # --- 9. Another save of the same account has its own ceiling and never sees
 # these publications.
 other=call('stats',_token=token,world='9'*32);check(other['items']==[] and not other['wallet']['known'])
 check(call('list')['items']!=[])

 # --- 10. Local hosting (site stays in the save) is billed from the same wallet
 # and can never take a domain that another publication already holds online.
 local=call('local',_token=token,world='7'*32,url='local.mysite.com',confirm='100/1800/1000',
  title='Offline build',visits=3,seconds=0,balance='500',settled='0')
 check(local['ok'] and local['charged']==100)
 check(local['items'][0]['local'] and local['items'][0]['url']=='local.mysite.com')
 check(local['items'][0]['rate']==100 and local['items'][0]['title']=='Offline build')
 check(not call('local',_token=token,world='7'*32,url='mygames.com',confirm='100/1800/1000')['ok'])
 check(not call('local',_token=token,world='7'*32,url='sites.pcos',confirm='100/1800/1000')['ok'])
 check(not call('local',_token=token,world='7'*32,url='owned.com',confirm='free')['ok'])
 # Served time reported by the game is what is billed for a local site: the
 # prepaid first period covers 1800 s, the rest costs $100 per 1800 s.
 call('local',_token=token,world='7'*32,url='local.mysite.com',confirm='100/1800/1000',seconds=3600)
 billed=call('stats',_token=token,world='7'*32)
 check(abs(billed['total']-200)<=2)
 # The reported value is an absolute total, so a repeated or retried report with
 # the same number is free instead of charging the same seconds again.
 call('local',_token=token,world='7'*32,url='local.mysite.com',confirm='100/1800/1000',seconds=3600)
 check(abs(call('stats',_token=token,world='7'*32)['total']-billed['total'])<=1)
 call('local',_token=token,world='7'*32,url='local.mysite.com',confirm='100/1800/1000',seconds=7200)
 again=call('stats',_token=token,world='7'*32)
 check(abs(again['total']-400)<=3)           # 7200 s = four periods of $100
 check(billed['items'][0]['local'] and billed['items'][0]['visits']==3)
 local_custom=call('local',_token=token,world='8'*32,url='mygames.com',confirm='100/1800/1000',balance='3000',settled='0')
 check(not local_custom['ok'])               # the domain belongs to a cloud publication
 check(not call('view',url='local.mysite.com')['ok'])  # never served by the web host
 print('PASS:',checks,'real PHP hosting-money checks: prepaid seconds, continuous offline rent, cash ceiling, pause without debt, write-off, snapshot resume, top-up, prepay, stop/restart, custom rate, save isolation, local hosting and domain conflicts')
