#!/usr/bin/env python3
"""Validate all five server crates against the artist-authored Compute master.
Includes meshes, materials, colliders, transforms, delivery offsets and IDs.
No Unity/PhysX simulation; use Play Mode for the physical opening smoke test.
"""
from pathlib import Path
import ast, hashlib, json, re, subprocess
from server_assets import ROOT,COMP,Prefab,read_guid
from sync_server_crates import MASTER,FOLDER,identities,sync_crate

manifest=json.loads((ROOT/'tools/server_catalog_manifest.json').read_text())
records=[r for r in manifest['records'] if r.get('crate')]+manifest['builds']
assert len(records)==5

def settings(p):
    ids=identities(p);root,_=p.root();box=p.mono('ReadyBuildBox')[0][0];item=p.item()[0]
    def normalize(value):
        if isinstance(value,dict):
            if set(value)=={'fileID'} and value['fileID'] in ids:return ('local',ids[value['fileID']])
            return {k:normalize(v) for k,v in value.items()}
        if isinstance(value,list):return [normalize(v) for v in value]
        return value
    result={}
    for i,v in p.objects():
        v=normalize(v)
        if i==root:v['m_Name']='$crate'
        if i==item:v['spawnId']='$crate'
        if i==box:v['prefab']='$content'
        result[ids[i]]=v
    return result

master=Prefab(MASTER);expected=settings(master)
assert 'ignoreDebrisCollisions' not in master.mono('ReadyBuildBox')[0][1]
assert master.mono('ReadyBuildBox')[0][1]['position']==dict(x=0,y=0,z=-1.67)
asset_guids=set()
for meta in (ROOT/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',meta.read_text(errors='ignore'),re.M)
    if m:asset_guids.add(m[1])
files=[]
for r in records:
    path=ROOT/r['crate'];p=Prefab(path);files.append(path)
    assert settings(p)==expected,(path,'settings diverge from Compute')
    assert p.item()[1]['spawnId']==path.stem and p.data(p.root()[0])['m_Name']==path.stem
    box=p.mono('ReadyBuildBox')[0][1];content=ROOT/r['prefab']
    assert box['prefab']==dict(fileID=Prefab(content).root()[0],guid=read_guid(content),type=3)
    shop=Prefab(ROOT/r['asset']).objects(114)[0][1]
    assert shop['spawn']==dict(fileID=p.root()[0],guid=read_guid(path),type=3)
    assert p.data(box['m_GameObject']['fileID'])['m_IsActive']==0
    assert len(p.objects(54))==7 and len(p.objects(65))==7
    assert all(v['m_Enabled'] and not v['m_IsTrigger'] for _,v in p.objects(65))
    assert all(v['m_Layer']==0 for _,v in p.objects(1))
    assert all(v['m_LocalScale']==dict(x=1,y=1,z=1) for _,v in p.objects(4))
    for ref in re.finditer(r'\{fileID: (-?\d+)\}',p.render()):
        assert int(ref[1])==0 or int(ref[1]) in p.blocks,(path,'dangling local ref',ref[1])
    assert set(re.findall(r'guid: (\w+)',p.render()))<=asset_guids
    # Existing shop/saved refs and prefab meta must survive copying settings.
    before=Prefab(text=subprocess.check_output(['git','show','8866bbf:'+r['crate']],cwd=ROOT,text=True))
    assert identities(p)==identities(before),(path,'local IDs changed')
    meta=Path(str(path)+'.meta')
    assert meta.read_bytes()==subprocess.check_output(['git','show','8866bbf:'+str(meta.relative_to(ROOT))],cwd=ROOT)

# Re-running the dedicated sync and the real catalog delivery() function must
# not revert to a generic PC crate, resize the artist model or change bytes.
before={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
for r in records:sync_crate(ROOT/r['crate'])
module=ast.parse((ROOT/'tools/generate_server_catalog.py').read_text())
node=next(n for n in module.body if isinstance(n,ast.FunctionDef) and n.name=='delivery')
ns=dict(ROOT=ROOT,READY=FOLDER,sync_crate=sync_crate)
exec(compile(ast.Module(body=[node],type_ignores=[]),'<catalog delivery>','exec'),ns)
for r in records:
    content=ROOT/r['prefab'];name=content.stem
    g,root,crate=ns['delivery'](name,content)
    assert crate==r['crate'] and g==read_guid(ROOT/crate) and root==Prefab(ROOT/crate).root()[0]
assert all(hashlib.sha256(p.read_bytes()).hexdigest()==sha for p,sha in before.items())
print('PASS: five server crates match Compute meshes/materials/transforms/colliders/offsets; correct content and stable GUID/fileIDs; normal debris collisions; sync/catalog delivery idempotent')
