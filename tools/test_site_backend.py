#!/usr/bin/env python3
"""Execute the real PHP API against isolated fixture accounts and storage.
Never connects to production, never loads real account data.
"""
import json,subprocess,tempfile,time
from pathlib import Path
R=Path(__file__).resolve().parents[1]
checks=0
def check(value):
 global checks
 checks+=1
 assert value,checks
with tempfile.TemporaryDirectory() as td:
 t=Path(td);data=t/'data';users=t/'users.json';token='a'*64;other='b'*64
 users.write_text(json.dumps([{'id':1,'token':token,'verified':True},{'id':2,'token':other,'verified':True}]))
 runner=t/'runner.php';runner.write_text("<?php define('SITE_DATA_DIR',"+json.dumps(str(data))+");define('SITE_USERS_FILE',"+json.dumps(str(users))+");$_POST=json_decode(stream_get_contents(STDIN),true);$_SERVER['REQUEST_METHOD']='POST';$_SERVER['REMOTE_ADDR']='127.0.0.1';$_SERVER['HTTP_X_AUTH_TOKEN']=$_POST['_token']??'';require "+json.dumps(str(R/'Hosting/sites/api.php'))+";")
 def call(action,**values):
  values['action']=action
  p=subprocess.run(['php',str(runner)],input=json.dumps(values),text=True,capture_output=True,check=True)
  assert not p.stderr,p.stderr
  return json.loads(p.stdout)
 world='1'*32;key='2'*32;doc={'format':'orange-site','version':1,'title':'Cool game','description':'Example site','keywords':'games','width':960,'height':600,'background':'#FFFFFF','viewportMode':'viewport','elements':[],'assets':[],'pages':[{'path':'/about','title':'About','width':960,'height':600,'background':'#FFFFFF','elements':[]}]}
 def publish(**overrides):
  vals=dict(_token=token,world=world,key=key,url='www.mycoolsite.com',document=json.dumps(doc),confirm='100/1800/1000',host=123);vals.update(overrides);return call('publish',**vals)
 check(call('health')['service']=='orange-sites');check(call('list')['items']==[]);check(not publish(_token='')['ok']);check(not publish(confirm='free')['ok'])
 first=publish();check(first['ok']);check(first['charged']==1100 and first['total']==1100);check(first['owner']=='1');id=first['item']['id']
 doc['elements']=[{'id':'nav','kind':'button','actions':[{'command':'GoToPage','arguments':['/about']}]}]
 update=publish();check(update['ok']and update['charged']==0 and update['item']['id']==id)
 check(call('view',url='www.mycoolsite.com/about')['item']['id']==id)
 check(len(call('list')['items'])==1)
 badnav=json.loads(json.dumps(doc));badnav['elements'][0]['actions'][0]['arguments']=['https://another.com'];check(not publish(document=json.dumps(badnav))['ok'])
 check(publish()['charged']==0);renamed=publish(url='another.com');check(renamed['ok']and renamed['charged']==0 and renamed['item']['id']==id);check(not call('view',url='www.mycoolsite.com/about')['ok']);check(publish()['charged']==0);check(not publish(_token=other,key='3'*32)['ok'])
 listing=call('list',query='cool');check(len(listing['items'])==1);check('publication_key'not in listing['items'][0] and 'owner'not in listing['items'][0] and 'document'not in listing['items'][0]);check('mycoolsite' in listing['items'][0]['url'])
 loaded=call('view',url='www.mycoolsite.com/about');check(loaded['ok']);check(json.loads(loaded['document'])['pages'][0]['path']=='/about');ticket=loaded['ticket'];session='4'*32
 check(not call('visit',id=id,ticket='forged',session=session)['ok']);visit=call('visit',id=id,ticket=ticket,session=session);check(visit['ok']and visit['item']['visits']==1 and visit['item']['online']==1)
 check(call('visit',id=id,ticket=visit['ticket'],session=session)['item']['visits']==1)
 check(call('leave',id=id,session=session)['item']['online']==0)
 mine=call('stats',_token=token,world=world);check(mine['items'][0]['publication_key']==key);check(call('stats',_token=other,world=world)['items']==[]);check(not call('stop',_token=other,world=world,id=id)['ok'])
 # Simulate 61 real minutes offline without sleeping or accepting client time.
 lib=json.dumps(str(R/'Hosting/sites/lib.php'));state=json.dumps(str(data/'state.php'))
 script=f"require {lib};$s=site_read({state});foreach($s['sites']as &$x)$x['billed_at']-=3660;unset($x);site_write({state},$s);"
 subprocess.run(['php','-r',script],check=True)
 check(call('stats',_token=token,world=world)['total']==1300)
 check(call('stats',_token=token,world=world)['total']==1300)
 check(not call('stop',_token=token,world='9'*32,id=id)['ok'])
 stopped=call('stop',_token=token,world=world,id=id);check(stopped['ok']and not stopped['item']['active']and stopped['item']['online']==0 and stopped['item']['next_bill']==0)
 check(call('stop',_token=token,world=world,id=id)['total']==1300)
 check(call('list')['items']==[]);check(not call('view',url='www.mycoolsite.com/about')['ok'])
 check(not call('visit',id=id,ticket=ticket,session=session)['ok'])
 subprocess.run(['php','-r',script],check=True) # elapsed time while stopped does not accrue rent
 check(call('stats',_token=token,world=world)['total']==1300)
 check(publish()['charged']==100)
 check(not call('visit',id=id,ticket=ticket,session=session)['ok']) # pre-stop ticket stays invalid after restart
 fresh=call('view',url='www.mycoolsite.com');check(call('visit',id=id,ticket=fresh['ticket'],session=session)['ok'])

 bad=dict(doc);bad['assets']=[{'name':'../secret','media':'application/x-pcos-file','data':''}];check(not publish(document=json.dumps(bad))['ok'])
 bad=dict(doc);bad['pages']=doc['pages']*2;check(not publish(document=json.dumps(bad))['ok'])
 bad=dict(doc);bad['backgroundImage']='missing.png';check(not publish(document=json.dumps(bad))['ok'])
 reserved=json.loads((R/'Hosting/sites/reserved.json').read_text());check(not publish(key='5'*32,url=reserved[0])['ok'])
 check(not publish(key='6'*32,url='s999.sites.pcos')['ok'])
 # A generated-domain publication can gain a custom domain once, then rename
 # without losing the publication, pages, visit count or paid rent period.
 key2='7'*32;free=publish(key=key2,url='');id2=free['item']['id'];check(free['charged']==100 and not free['item']['custom_paid']);nextbill=free['item']['next_bill']
 origin=free['item']['url'];seen=call('view',url=origin+'/about');check(call('visit',id=id2,ticket=seen['ticket'],session='8'*32)['ok'])
 change=publish(key=key2,url='new-domain.com');check(change['charged']==1000 and change['item']['id']==id2 and change['item']['custom_paid'] and change['item']['next_bill']==nextbill and change['item']['visits']==1 and change['item']['online']==0)
 check(not call('view',url=origin+'/about')['ok']);check(not call('visit',id=id2,ticket=seen['ticket'],session='8'*32)['ok'])
 check(json.loads(call('view',url='new-domain.com/about')['document'])['pages'][0]['path']=='/about')
 change=publish(key=key2,url='second-domain.com');check(change['charged']==0 and change['item']['id']==id2 and change['item']['visits']==1)
 check(not call('view',url='new-domain.com')['ok']);check(len(call('list')['items'])==2)
 check(not publish(key=key2,url='www.mycoolsite.com')['ok'])
 check(call('view',url='second-domain.com')['ok'])
 check(publish(key=key2,url='')['item']['url']=='second-domain.com')
 # Old deployed records without custom_paid must not charge custom-domain
 # setup again: infer the paid state from the existing URL.
 migrate=f"require {lib};$s=site_read({state});unset($s['sites'][{id2}]['custom_paid']);site_write({state},$s);"
 subprocess.run(['php','-r',migrate],check=True)
 check(publish(key=key2,url='migrated-domain.com')['charged']==0)
 # The private storage file is valid non-executable PHP even if titles contain
 # PHP tags. It returns no JSON or secrets when requested/executed directly.
 p=subprocess.run(['php',str(data/'state.php')],capture_output=True,check=True);check(p.stdout==b'')
 print('PASS:',checks,'real PHP API checks: authentication/isolation, publication, routes, URL reservation, idempotent tariff, offline UTC billing, visits/online/leave, stop/restart, validation and private storage')
