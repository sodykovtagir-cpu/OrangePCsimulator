#!/usr/bin/env python3
"""Exercise real PHP role/admin/auth/API code in an isolated fixture. Requires php + mbstring."""
from pathlib import Path
import json,subprocess,tempfile,shutil
ROOT=Path(__file__).resolve().parents[1]

def php_value(value):
    return "json_decode("+json.dumps(json.dumps(value,ensure_ascii=False))+", true)"

def run_request(folder,script,method='GET',get=None,post=None,session=None):
    code='''<?php
ini_set('session.save_path', __DIR__);
session_id('test' . bin2hex(random_bytes(10)));
session_start();
$_SESSION = '''+php_value(session or {})+''';
session_write_close();
$_SERVER['REQUEST_METHOD'] = '''+json.dumps(method)+''';
$_SERVER['REMOTE_ADDR'] = '127.0.0.1';
$_GET = '''+php_value(get or {})+''';
$_POST = '''+php_value(post or {})+''';
require __DIR__ . '/'''+script+"';\n"
    wrapper=folder/'request.php';wrapper.write_text(code)
    proc=subprocess.run(['php',str(wrapper)],capture_output=True,text=True)
    assert proc.returncode==0,(proc.stdout,proc.stderr)
    assert not proc.stderr,proc.stderr
    return proc.stdout

def main():
    with tempfile.TemporaryDirectory(prefix='workshop-admin-tests-') as tmp:
        folder=Path(tmp);(folder/'uploads').mkdir()
        for name in ['account.php','api.php','admin.php','game_roles.php']:
            shutil.copy2(ROOT/'server'/name,folder/name)
            subprocess.run(['php','-l',str(folder/name)],check=True,capture_output=True)
        hashed=subprocess.check_output(['php','-r',"echo password_hash('fixture-password', PASSWORD_DEFAULT);"],text=True)
        users=[{'id':1,'name':'Goose','verified':True,'email':'goose@example.test','token':'a','pass_hash':hashed,'tg_username':'','tg_bonus':False},
               {'id':2,'name':'Semyalol','verified':True,'email':'sem@example.test','token':'b','pass_hash':hashed,'tg_username':'','tg_bonus':False},
               {'id':3,'name':'Player','verified':True,'email':'player@example.test','token':'c','pass_hash':hashed,'tg_username':'','tg_bonus':False},
               {'id':4,'name':'Unverified','verified':False,'email':'none@example.test','token':'','pass_hash':hashed,'tg_username':'','tg_bonus':False}]
        (folder/'users.json').write_text(json.dumps(users))
        (folder/'game_admins.json').write_text(json.dumps({'version':1,'admin_user_ids':[1]}))
        (folder/'admin_config.json').write_text(json.dumps({'hash':hashed,'changed':True}))
        (folder/'uploads/index.json').write_text(json.dumps([
            {'id':1,'title':'Admin save','author':'Goose','owner_user_id':1,'owner_key':'private','filename':'one.opc','size_bytes':1},
            {'id':2,'title':'Spoof attempt','author':'Goose','owner_user_id':3,'owner_key':'private2','filename':'two.opc','size_bytes':1},
            {'id':3,'title':'Legacy spoof','author':'Semyalol','filename':'three.opc','size_bytes':1}]))
        def ids():return json.loads((folder/'game_admins.json').read_text())['admin_user_ids']
        admin={'workshop_admin':True,'csrf':'correct'}
        post={'act':'game_admin_set','user_id':'2','enabled':'1','csrf':'correct'}
        run_request(folder,'admin.php','POST',post=post,session={'csrf':'correct'})
        assert ids()==[1],'unauthenticated role change'
        run_request(folder,'admin.php','POST',post=dict(post,csrf='wrong'),session=admin)
        assert ids()==[1],'CSRF bypass'
        run_request(folder,'admin.php','GET',get=post,session=admin)
        assert ids()==[1],'GET changed roles'
        run_request(folder,'admin.php','POST',post=post,session=admin)
        assert ids()==[1,2],'grant failed'
        run_request(folder,'admin.php','POST',post=dict(post,user_id='4'),session=admin)
        assert ids()==[1,2],'unverified account became admin'
        run_request(folder,'admin.php','POST',post=dict(post,user_id='999'),session=admin)
        assert ids()==[1,2],'nonexistent account became admin'
        html=run_request(folder,'admin.php','GET',get={'tab':'accounts'},session=admin)
        assert 'Аккаунты / админы' in html and 'Goose' in html and 'Semyalol' in html and 'game_admin_set' in html
        assert 'pass_hash' not in html and 'fixture-password' not in html and '@example.test' not in html
        result=json.loads(run_request(folder,'account.php','POST',get={'action':'me'},post={'token':'b'}))
        assert result['ok'] and result['is_admin'] is True
        public=json.loads(run_request(folder,'api.php',get={'action':'list'}))['items']
        assert public[0]['author_is_admin'] is True
        assert public[1]['author_is_admin'] is False and public[2]['author_is_admin'] is False,'author text grants role'
        assert all('owner_user_id' not in item and 'owner_key' not in item for item in public)
        result=json.loads(run_request(folder,'account.php','POST',get={'action':'login'},post={'login':'Player','password':'fixture-password','is_admin':'1'}))
        assert result['ok'] and result['is_admin'] is False,'client forged admin role'
        result=json.loads(run_request(folder,'account.php','POST',get={'action':'login'},post={'login':'Goose','password':'fixture-password'}))
        assert result['ok'] and result['is_admin'] is True
        run_request(folder,'admin.php','POST',post=dict(post,enabled='0'),session=admin)
        assert ids()==[1],'revocation failed'
        result=json.loads(run_request(folder,'account.php','POST',get={'action':'me'},post={'token':'b'}))
        assert result['is_admin'] is False,'role removal not reflected in game profile'
        print('PASS: real PHP admin auth/CSRF, grant/revoke, verified account checks, profile/login role reads, author-spoof resistance and private-field filtering')
    print('Server-role tests passed in isolated data, without modifying production accounts.')

if __name__=='__main__':main()
