#!/usr/bin/env python3
"""Real PHP API + real admin actions with synthetic accounts in a temporary tree."""
import json,subprocess,tempfile,shutil,base64
from pathlib import Path
R=Path(__file__).resolve().parents[1];checks=0
with tempfile.TemporaryDirectory()as td:
 t=Path(td);sites=t/'sites';workshop=t/'workshop';sites.mkdir();workshop.mkdir()
 for name in ['api.php','lib.php','reserved.json']:shutil.copy(R/'Hosting/sites'/name,sites/name)
 for p in list((R/'server').glob('*.php'))+list((R/'server').glob('sites_preview.js')):shutil.copy(p,workshop/p.name)
 token='a'*64;world='1'*32;key='2'*32
 (workshop/'users.json').write_text(json.dumps([{'id':1,'token':token,'verified':True}]))
 runner=t/'api-runner.php';runner.write_text("<?php $_POST=json_decode(stream_get_contents(STDIN),true);$_SERVER['REQUEST_METHOD']='POST';$_SERVER['REMOTE_ADDR']='127.0.0.1';$_SERVER['HTTP_X_AUTH_TOKEN']=$_POST['_token']??'';require "+json.dumps(str(sites/'api.php'))+";")
 def check(v):
  global checks
  checks+=1;assert v,checks
 def call(action,**v):
  p=subprocess.run(['php',str(runner)],input=json.dumps(dict(action=action,**v)),text=True,capture_output=True,check=True);assert not p.stderr,p.stderr;return json.loads(p.stdout)
 def php(code):
  p=subprocess.run(['php','-r',code],capture_output=True,text=True,check=True);assert not p.stderr,p.stderr;return p.stdout
 doc={'format':'orange-site','version':1,'title':'Custom title','width':960,'height':600,'background':'#FFFFFF','icon':'icon.png','elements':[],'pages':[{'path':'/about','title':'About','elements':[]}],'assets':[{'name':'icon.png','media':'image/png','data':'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jQ9sAAAAASUVORK5CYII='},{'name':'test.txt','media':'application/x-pcos-file','data':base64.b64encode(b'hello').decode()}]}
 def publish(**v):
  values=dict(_token=token,world=world,key=key,url='custom.com',document=json.dumps(doc),confirm='100/1800/1000',host=123);values.update(v);return call('publish',**values)
 first=publish(balance='4100',settled='0');check(first['ok']and first['total']==1100);id=first['item']['id']
 info=call('info',url='custom.com/about');check(info['ok']and info['item']['title']=='Custom title'and info['item']['icon_data']and 'document'not in info)
 listing=call('list');check(listing['items'][0]['icon_data']==info['item']['icon_data'])
 loaded=call('view',url='custom.com/about');ticket=loaded['ticket'];check(call('transfer',id=id,ticket=ticket,file='test.txt')['ok']);check(not call('transfer',id=id,ticket='forged',file='test.txt')['ok']);check(not call('transfer',id=id,ticket=ticket,file='../test.txt')['ok'])
 # Active transfers are independent from visits and expire without a cancel.
 started=call('transfer',id=id,ticket=ticket,file='test.txt',phase='start');check(started['item']['transferring']==1 and started['item']['visits']==0)
 check(call('transfer',id=id,ticket=started['ticket'],file='test.txt',phase='pulse')['item']['transferring']==1)
 check(call('transfer',id=id,ticket=started['ticket'],file='test.txt',phase='end')['item']['transferring']==0)
 check(call('transfer',id=id,ticket=ticket,file='test.txt',phase='start')['item']['transferring']==1)
 hosts=json.dumps({'items':[{'host':123,'cpu_capacity':3456,'cpu_load':.95,'host_mbps':.45}]})
 stats=call('stats',_token=token,world=world,hosts=hosts,balance='3000',settled='1100');check(stats['items'][0]['cpu_capacity']==3456 and stats['items'][0]['cpu_load']==.95)
 lib=json.dumps(str(sites/'lib.php'));state=json.dumps(str(sites/'data/state.php'))
 php(f"require {lib};$db=site_read({state});foreach($db['sites']as &$s)foreach($s['transfers']??[]as $k=>$v)$s['transfers'][$k]['at']-=31;unset($s);site_write({state},$db);")
 check(call('info',url='custom.com')['item']['transferring']==0)
 check(call('info',url='custom.com')['item']['downloads']==2)
 def advance(seconds):php(f"require {lib};$db=site_read({state});foreach($db['sites']as &$s)if($s['active']){{$s['billed_at']-={seconds};$s['recurring_from']-={seconds};}}unset($s);site_write({state},$db);")
 advance(3660);check(call('stats',_token=token,world=world)['total']==3300)
 call('stats',_token=token,world=world,balance='0',settled='3300');advance(1800)
 unpaid=call('view',url='custom.com/about');check(unpaid['ok']and unpaid['item']['unpaid']and 'document'not in unpaid)
 check(not call('transfer',id=id,ticket=ticket,file='test.txt')['ok']);check(call('stats',_token=token,world=world)['total']==3300)
 advance(86400);check(call('stats',_token=token,world=world)['total']==3300)
 funded=call('stats',_token=token,world=world,balance='1200',settled='3300');check(funded['total']==4400 and not funded['items'][0]['unpaid'])
 check(call('view',url='custom.com')['document']);ledger=json.dumps({'items':[{'owner':'1','charged':4400},{'owner':'another-owner','charged':999999}]});snap=call('stats',_token=token,world=world,balance='100',ledgers=ledger);check(snap['total']==4400);advance(3660);check(call('stats',_token=token,world=world)['total']==4400);check(call('view',url='custom.com')['item']['unpaid'])
 check(not publish(key='3'*32,url='expensive.com',balance='0',settled='4400')['ok'])
 # One shared snapshot cannot fund an unlimited number of simultaneous sites.
 w2='4'*32;p2=publish(world=w2,key='5'*32,url='',balance='200',settled='0');p3=publish(world=w2,key='6'*32,url='',balance='100',settled='100');check(p2['ok']and p3['ok']);advance(1800)
 shared=call('stats',_token=token,world=w2);check(shared['total']==200 and all(s['unpaid']for s in shared['items']))
 # A physical PC stop affects all of its own publications, never another host/save/account.
 check(not call('host_stop',world=w2,host=123)['ok'])
 check(call('host_stop',_token=token,world='9'*32,host=123)['items']==[])
 stopped=call('host_stop',_token=token,world=w2,host=123);check(stopped['ok'] and all(not s['active'] for s in stopped['items']))
 check(call('view',url='custom.com')['item']['unpaid'])
 check(publish(world=w2,key='5'*32,url='',balance='100',settled='200')['charged']==100)
 retried=call('stats',_token=token,world=w2,stopped_hosts='123');check(all(not s['active'] for s in retried['items']))
 # Version migration: old overdue boundaries stay $100, next boundary costs $1100.
 code=f"require {lib};$now=time();$s=['id'=>88,'url'=>'legacy.com','owner'=>'1','world'=>'legacy','active'=>true,'total'=>1100,'billed_at'=>$now-3660];$db=['sites'=>[$s]];site_reconcile($db,$now);if($db['sites'][0]['total']!==1300)exit(2);site_reconcile($db,$now+1800);if($db['sites'][0]['total']!==2400)exit(3);echo 'ok';"
 check(php(code)=='ok')
 # Execute the real admin controller with a synthetic authenticated session.
 # Wrong CSRF must not change the stored site. No real password/account is used.
 admin=json.dumps(str(workshop/'admin.php'))
 def admin_action(csrf,act,reason='test moderation'):
  code="error_reporting(E_ERROR);session_start();$_SESSION['workshop_admin']=true;$_SESSION['csrf']='expected';$_SERVER['REQUEST_METHOD']='POST';$_POST="+"json_decode("+json.dumps(json.dumps({'act':act,'site_id':id,'csrf':csrf,'reason':reason}))+",true);require "+admin+";"
  return php(code)
 rendered=php("error_reporting(E_ERROR);session_start();$_SESSION['workshop_admin']=true;$_SESSION['csrf']='expected';$_SERVER['REQUEST_METHOD']='GET';$_GET=['tab'=>'sites','site_id'=>"+str(id)+"];require "+admin+";")
 check('sandbox="allow-scripts"'in rendered and 'srcdoc='in rendered and 'Файлы и изображения'in rendered)
 check('Посмотреть пакет и страницы'not in rendered and 'Бан закрывает все страницы'not in rendered)
 admin_action('bad','site_ban');check(not call('info',url='custom.com')['item']['banned'])
 admin_action('expected','site_ban','<script>bad</script> moderation')
 blocked=call('view',url='custom.com/about');check(blocked['item']['banned']and 'document'not in blocked);check(not publish(url='escape.com')['ok']);check(not any(s['id']==id for s in call('list')['items']))
 check(not call('transfer',id=id,ticket=ticket,file='test.txt')['ok'])
 admin_action('expected','site_unban');check(not call('view',url='custom.com')['ok']);stats=call('stats',_token=token,world=world);check(not stats['items'][0]['banned']and not stats['items'][0]['active'])
 adminlib=json.dumps(str(workshop/'sites_admin.php'));out=php(f"require {adminlib};echo 'not protected';");check(out=='')
 print('PASS:',checks,'real PHP followup checks: metadata/icons, transfer activity/expiry, host stop/isolation/retry, tariff migration, shared money snapshot, offline unpaid suspension/resume, no unpaid debt, CSRF-gated admin ban/unban and no rename bypass')
