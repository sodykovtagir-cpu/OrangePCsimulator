#!/usr/bin/env python3
"""Execute the real PHP API against isolated fixture accounts and storage.
Never connects to production, never loads real account data.

Money model (wallet):
  * the game balance is never spent by the backend — only `action=topup` moves
    money into the hosting wallet;
  * rent = base ($100 / 1800 s, $1100 with a custom domain) + a traffic surcharge
    for the seconds the site really served somebody;
  * an empty wallet pauses the site without debt; publication itself is free,
    the only up-front charge is the one-time $1000 custom-domain fee.
"""
import json,subprocess,tempfile,time
from pathlib import Path
R=Path(__file__).resolve().parents[1]
checks=0
def check(value):
 global checks
 checks+=1
 if not value:
  import inspect
  raise AssertionError('%d at %s'%(checks,inspect.stack()[1].lineno))
with tempfile.TemporaryDirectory() as td:
 t=Path(td);data=t/'data';users=t/'users.json';token='a'*64;other='b'*64
 users.write_text(json.dumps([{'id':1,'token':token,'verified':True},{'id':2,'token':other,'verified':True}]))
 runner=t/'runner.php';runner.write_text("<?php define('SITE_DATA_DIR',"+json.dumps(str(data))+");define('SITE_USERS_FILE',"+json.dumps(str(users))+");$_POST=json_decode(stream_get_contents(STDIN),true);$_SERVER['REQUEST_METHOD']='POST';$_SERVER['REMOTE_ADDR']='127.0.0.1';$_SERVER['HTTP_X_AUTH_TOKEN']=$_POST['_token']??'';require "+json.dumps(str(R/'Hosting/sites/api.php'))+";")
 def call(action,**values):
  values['action']=action
  p=subprocess.run(['php',str(runner)],input=json.dumps(values),text=True,capture_output=True,check=True)
  assert not p.stderr,p.stderr
  return json.loads(p.stdout)
 def php(code):
  p=subprocess.run(['php','-r',code],capture_output=True,text=True,check=True);assert not p.stderr,p.stderr;return p.stdout
 world='1'*32;key='2'*32;lib=json.dumps(str(R/'Hosting/sites/lib.php'));state=json.dumps(str(data/'state.php'))
 doc={'format':'orange-site','version':1,'title':'Cool game','description':'Example site','keywords':'games','width':960,'height':600,'background':'#FFFFFF','viewportMode':'viewport','elements':[],'assets':[],'pages':[{'path':'/about','title':'About','width':960,'height':600,'background':'#FFFFFF','elements':[]}]}
 def wallet(amount,settled='0',own=token,target=world):
  return call('topup',_token=own,world=target,amount=str(amount),balance='1000000',settled=settled)
 def total(target=world,own=token):
  return call('stats',_token=own,world=target)['total']
 def advance(seconds,target=world):
  # Move the simulated clock back: the run start, the visitor heartbeats and the
  # traffic window all live in timestamps, so one shift keeps them consistent.
  php(f"require {lib};$db=site_read({state});foreach($db['sites']as &$s)if($s['world']==={json.dumps(target)}&&!empty($s['active'])){{$s['run_from']-={seconds};if(!empty($s['busy_at']))$s['busy_at']-={seconds};foreach($s['sessions']??[]as $k=>$r)$s['sessions'][$k]['at']-={seconds};foreach($s['transfers']??[]as $k=>$r)$s['transfers'][$k]['at']-={seconds};}}unset($s);site_write({state},$db);")
 def publish(**overrides):
  vals=dict(_token=token,world=world,key=key,url='www.mycoolsite.com',document=json.dumps(doc),confirm='100/1800/1000',host=123,balance='100000',settled='0');vals.update(overrides);return call('publish',**vals)

 check(call('health')['service']=='orange-sites');check(call('list')['items']==[]);check(not publish(_token='')['ok']);check(not publish(confirm='free')['ok'])
 # --- 1. Publishing never touches the game balance, and a paid domain needs the
 # wallet: without money the custom-domain fee cannot be paid, so nothing is stored.
 broke=publish();check(not broke['ok'] and 'кошел' in broke['error'])
 check(call('list')['items']==[])
 # A generated address is free to publish; it does not run until the wallet has money.
 free=publish(url='');check(free['ok'] and free['charged']==0 and free['total']==0)
 id=free["item"]["id"];check(free['item']['active'] and free['item']['credit']==0 and free['item']['rate']==100)
 # Without a wallet the rent cannot be collected: the site pauses, nothing is billed.
 advance(120)
 paused=call('stats',_token=token,world=world);check(paused['total']==0 and paused['items'][0]['paused'] and paused['items'][0]['pause_reason']=='dry')

 # --- 2. Top-up is the only source of hosting money.
 funded=wallet(1000,settled='0');check(funded['ok'] and funded['wallet']['available']==1000)
 check(not funded['items'][0]['paused'])
 doc['elements']=[{'id':'nav','kind':'button','actions':[{'command':'GoToPage','arguments':['/about']}]}]
 update=publish(url='');check(update['ok'] and update['charged']==0 and update['item']['id']==id)
 check(call('view',url=free['item']['url']+'/about')['item']['id']==id)
 check(len(call('list')['items'])==1)
 badnav=json.loads(json.dumps(doc));badnav['elements'][0]['actions'][0]['arguments']=['https://another.com'];check(not publish(url='',document=json.dumps(badnav))['ok'])
 # A custom domain costs the one-time $1000 fee from the wallet, once.
 wallet(1500,settled='0')
 upgrade=publish();assert upgrade['ok'],upgrade;check(upgrade['charged']==1000 and upgrade['item']['id']==id)
 check(upgrade['item']['custom_paid'] and upgrade['item']['rate']==1100 and upgrade['item']['price']==1100)
 again=publish();assert again['ok'],again;check(again['charged']==0)
 renamed=publish(url='second-domain.com');assert renamed['ok'],renamed;check(renamed['charged']==0)
 check(not any(s['url']=='www.mycoolsite.com' for s in call('list')['items']))

 # --- 3. Base rent is per second and comes out of the wallet only.
 advance(1800);time.sleep(1);run=call('stats',_token=token,world=world)
 # $1000 domain fee + one 1800 s period of the $1100 tariff = $2100 billed.
 check(abs(run['total']-2100)<=3 and abs(run['wallet']['available']-400)<=3)
 check(run['items'][0]['money_known'] and not run['items'][0]['paused'])
 # A second full period cannot be paid from the rest: the served time is paid
 # down to the last dollar, the site pauses, and no debt appears.
 advance(3600);time.sleep(1);dry=call('stats',_token=token,world=world)
 check(dry['total']==2500 and dry['wallet']['available']==0)
 check(dry['items'][0]['paused'] and dry['items'][0]['pause_reason']=='dry')
 frozen=dry['total'];advance(86400);check(total()==frozen)   # a dry wallet never grows

 # --- 4. A snapshot can never create money: only topup does.
 poor=call('stats',_token=token,world=world,balance='9999999',settled='999999')
 check(poor['wallet']['ceiling']==2500 and poor['wallet']['available']==0)
 # A save rollback (the player really lost that money) shrinks the wallet, but
 # never below what was actually paid into it.
 rollback=call('stats',_token=token,world=world,balance='0',settled='0')
 check(rollback['wallet']['ceiling']==2500 and rollback['wallet']['available']==0)
 # Fresh money resumes exactly where it stopped.
 wallet(2500,settled='2500');resumed=call('stats',_token=token,world=world)
 check(not resumed['items'][0]['paused'] and resumed['wallet']['available']==2500)

 # --- 5. Traffic surcharge: only seconds with a live visitor cost extra.
 before=total();url=call('list')['items'][0]['url']
 seen=call('view',url=url);visit=call('visit',id=id,ticket=seen['ticket'],session='4'*32)
 check(visit['ok'] and visit['item']['online']==1 and visit['item']['traffic_rate']==20)
 advance(60);time.sleep(1);busy=call('stats',_token=token,world=world)
 # 60 s of base rent ($1100/1800 = $36.7) plus 60 s of real serving.
 check(busy['items'][0]['traffic_seconds']>=58)
 check(abs(busy['total']-before-37)<=3)
 check(call('leave',id=id,session='4'*32)['item']['online']==0)
 # After the visitor left no traffic is invented, however long the site runs.
 served=busy['items'][0]['traffic_seconds'];advance(3600);time.sleep(1)
 quiet=call('stats',_token=token,world=world)
 check(quiet['items'][0]['traffic_seconds']-served<=90)

 # --- 6. Publications are isolated per account and per save.
 check(call('stats',_token=other,world=world)['items']==[]);check(not call('stop',_token=other,world=world,id=id)['ok'])
 check(not call('stop',_token=token,world='9'*32,id=id)['ok'])
 mine=call('stats',_token=token,world=world);check(mine['items'][0]['publication_key']==key)

 # --- 7. Stop ends all charges; restart publishes for free.
 stopped=call('stop',_token=token,world=world,id=id)
 check(stopped['ok'] and not stopped['item']['active'] and stopped['item']['next_bill']==0)
 stopped_total=total();advance(86400);check(total()==stopped_total)
 check(not call('view',url=url)['ok'])
 restart=publish(url='second-domain.com');check(restart['ok'] and restart['charged']==0 and restart['item']['active'])
 check(call('list')['items'][0]['url']=='second-domain.com')

 # --- 8. A local publication is never served by the web host, pays only for the
 # seconds the game really served, and gets the same one-time domain fee.
 lworld='7'*32
 loc=call('local',_token=token,world=lworld,url='local.mysite.com',confirm='100/1800/1000',title='Offline build',visits=3,seconds=0,balance='5000',settled='0')
 check(loc['ok'] and loc['charged']==0 and loc['items'][0]['local'] and loc['items'][0]['rate']==100)
 check(loc['items'][0]['title']=='Offline build')
 check(not call('local',_token=token,world=lworld,url='sites.pcos',confirm='100/1800/1000')['ok'])
 check(not call('local',_token=token,world=lworld,url='owned.com',confirm='free')['ok'])
 wallet(600,target=lworld)
 call('local',_token=token,world=lworld,url='local.mysite.com',confirm='100/1800/1000',seconds=3600)
 billed=call('stats',_token=token,world=lworld)
 check(abs(billed['total']-200)<=2)                        # 3600 s at $100/1800 s
 # An absolute, repeated report is idempotent.
 call('local',_token=token,world=lworld,url='local.mysite.com',confirm='100/1800/1000',seconds=3600)
 check(abs(call('stats',_token=token,world=lworld)['total']-billed['total'])<=1)
 call('local',_token=token,world=lworld,url='local.mysite.com',confirm='100/1800/1000',seconds=7200)
 check(abs(call('stats',_token=token,world=lworld)['total']-400)<=3)
 check(billed['items'][0]['visits']==3)
 check(not call('view',url='local.mysite.com')['ok'])
 # A custom domain on a local publication pays the one-time fee, from the wallet.
 wallet(1000,target=lworld)
 custom=call('local',_token=token,world=lworld,url='mygame.example.com',confirm='100/1800/1000')
 check(custom['ok'] and custom['charged']==1000)
 check(call('local',_token=token,world=lworld,url='mygame.example.com',confirm='100/1800/1000')['charged']==0)

 # --- 9. The traffic surcharge of a local publication counts only real serving.
 lworld2='6'*32
 call('local',_token=token,world=lworld2,url='local.quiet.com',confirm='100/1800/1000',seconds=0,balance='5000',settled='0')
 wallet(500,target=lworld2)
 quiet_local=call('stats',_token=token,world=lworld2)
 check(quiet_local['total']==0 and quiet_local['items'][0]['active'] and quiet_local['items'][0]['traffic_seconds']==0)

 # --- 11. The server reserves the temporary standard address before publishing,
 # and an address generated offline is accepted as a standard publication.
 rworld='8'*32;okey='d'*32
 res=call('reserve',_token=token,world=rworld,key=okey,title='Reserved',balance='1000',settled='0')
 check(res['ok'] and res['item']['reserved'] and not res['item']['active'])
 check(res['item']['url']=='s%d.sites.pcos'%res['item']['id'])
 again=call('reserve',_token=token,world=rworld,key=okey);check(again['item']['url']==res['item']['url'] and again['item']['id']==res['item']['id'])
 kept=publish(world=rworld,key=okey,url=res['item']['url'],balance='1000',settled='0')
 check(kept['ok'] and kept['item']['url']==res['item']['url'] and kept['charged']==0)
 check(kept['item']['rate']==100 and kept['item']['price']==100 and not kept['item']['reserved'])
 off='s424242.sites.pcos'
 offpub=call('publish',_token=token,world='5'*32,key=okey,url=off,document=json.dumps(doc),confirm='100/1800/1000',host=1,balance='1000',settled='0')
 check(offpub['ok'] and offpub['item']['url']==off and offpub['item']['rate']==100 and offpub['item']['price']==100)
 # A standard address may also be hosted from the save (offline mode).
 loc2=call('local',_token=token,world=rworld,url='s777777.sites.pcos',confirm='100/1800/1000',seconds=0,balance='1000',settled='0')
 check(loc2['ok'] and any(s['url']=='s777777.sites.pcos' and s['rate']==100 for s in loc2['items']))
 # --- 10. Reserved names, private storage and generated addresses.
 reserved=json.loads((R/'Hosting/sites/reserved.json').read_text())
 check(not publish(key='5'*32,url=reserved[0])['ok'])
 check(not publish(key='6'*32,url='www.byethost4.com')['ok'])
 check(not publish(key='6'*32,url='a..b.com')['ok'])
 p=subprocess.run(['php',str(data/'state.php')],capture_output=True,check=True);check(p.stdout==b'')
 print('PASS:',checks,'real PHP API checks: wallet-only rent, free publication, one-time domain fee, per-second base rent with traffic surcharge, pause without debt, snapshot cannot mint money, rollback, stop/restart, local hosting, validation and private storage')
