#!/usr/bin/env python3
"""Server catalog, authored assembly, economics and real-model render guards.
No Unity Editor required; PlayMode smoke test is still required separately.
"""
from pathlib import Path
import hashlib,json,math,re,subprocess,sys,tempfile
import numpy as np
from PIL import Image
from server_assets import ROOT,COMP,Prefab,read_guid
from unity_asset_tool import shop_pages, prefab_bounds
from export_server_cover_scenes import matrix

manifest=json.loads((ROOT/'tools/server_catalog_manifest.json').read_text())
index={}
for p in (ROOT/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)
    if m:index.setdefault(m[1],[]).append(Path(str(p)[:-5]))

def validate(path,plan=False):
    p=Prefab(path);text=p.render()
    ids=re.findall(r'^--- !u!\d+ &(-?\d+)',text,re.M);assert len(ids)==len(set(ids)),path
    for m in re.finditer(r'\{fileID: (-?\d+)\}',text):
        fid=int(m[1]);assert fid==0 or fid in p.blocks,(path,'dangling local reference',fid)
    for m in re.finditer(r'guid: (\w+)',text):assert m[1] in index,(path,'missing external GUID',m[1])
    owners=[v['m_GameObject']['fileID'] for _,v in p.objects(54)]
    assert len(owners)==len(set(owners)),(path,'duplicate Rigidbody')
    for go,v in p.objects(1):
        for c in v.get('m_Component',[]):assert p.data(c['component']['fileID'])['m_GameObject']['fileID']==go
    root,tr=p.root()
    if plan:return p
    it,iv=p.item();assert iv['spawnId']==path.stem,(path,'save identity')
    assert any(v['m_GameObject']['fileID']==root for _,v in p.objects(54)),(path,'missing root physics')
    return p

def tag(p,go):
    v=p.data(go)
    if 'm_TagString' in v:return v['m_TagString']
    src=v['m_CorrespondingSourceObject']['fileID'];pi=p.data(v['m_PrefabInstance']['fileID'])
    return next((x['value'] for x in pi['m_Modification']['m_Modifications'] if x['target']['fileID']==src and x['propertyPath']=='m_TagString'),'Untagged')

def parent(p,tr):
    v=p.data(tr)
    if 'm_Father' in v:return v['m_Father']['fileID']
    return p.data(v['m_PrefabInstance']['fileID'])['m_Modification']['m_TransformParent']['fileID']

def world(p,tr):
    v=p.data(tr);par=parent(p,tr)
    out=matrix(v) if 'm_LocalPosition' in v else np.eye(4)
    return world(p,par)@out if par else out

pages=dict(shop_pages(str(ROOT/'Assets/MonoBehaviour/Shop.asset')))
assert len(manifest['records'])==13 and len(manifest['builds'])==4
expected_pages={'Processor','Case','Motherboard','Power Supply','Cooler','Storage'}
assert {x['page'] for x in manifest['records']}==expected_pages
assert len(pages['Ready Server'])==4
# The old catalog must remain intact, in the same order within every page.
base=subprocess.check_output(['git','show',manifest['base_commit']+':Assets/MonoBehaviour/Shop.asset'],cwd=ROOT,text=True)
with tempfile.NamedTemporaryFile(mode='w',suffix='.asset') as f:
    f.write(base);f.flush();old=dict(shop_pages(f.name))
for page,items in old.items():assert [g for g in pages[page] if g in set(items)]==items,(page,'old items deleted/reordered')

rmd_guid=read_guid(ROOT/'Assets/Texture2D/RmdServer.png')
for record in manifest['records']:
    path=ROOT/record['prefab'];p=validate(path);asset=Prefab(ROOT/record['asset']);entry=next(v for _,v in asset.objects(114))
    assert record['shop_guid'] in pages[record['page']]
    assert entry['price']==record['price'] and entry['price']>0 and entry['bitcoin']==0
    spawn=ROOT/record['crate'] if record.get('crate') else path
    assert entry['spawn']['fileID']==Prefab(spawn).root()[0] and entry['spawn']['guid']==read_guid(spawn)
    assert entry['sprite']['guid'] in index
    if record['name'].startswith('CPU RMD'):
        assert entry['sprite']['guid']==rmd_guid
        assert p.item()[1]['match']==1 and tag(p,p.root()[0])=='CPU'
    if record['name'].startswith('ServerCooler'):
        assert entry['sprite']=={'fileID':21300000,'guid':read_guid(ROOT/'Assets/Texture2D/servercooler.png'),'type':3}
        assert tag(p,p.root()[0])=='Cooler'
        assert any(v['m_GameObject']['fileID']==p.root()[0] for _,v in p.objects(65)), 'manual cooler insertion needs collider on root Item'
    for _,v in p.objects(114):
        if 'spawnId' in v:
            for k in ['score','capacity']:
                if k in v:assert isinstance(v[k],int),(path,k,'must be serialized integer')

for spec in manifest['builds']:
    p=validate(ROOT/spec['prefab'],plan=True)
    sp=p.mono('ReadyBuildSpawner');assert len(sp)==1 and len(p.objects(114))==1
    sp=sp[0][1]
    assert sp['basePrefab']=={'fileID':Prefab(COMP/'Server.prefab').root()[0],'guid':read_guid(COMP/'Server.prefab'),'type':3}
    assert not p.objects(54) and not p.objects(33), 'plan must not contain a second embedded assembly'
    assert sp['preinstallOS']==1 and sp['systemSize']==60000 and sp['destroyAfterBuild']==1
    assert sp['preinstalledApps']==['System Info','Disk Management','App Downloader','Browser','Benchmark']
    assert sp['stepDelay']==0 and sp['attachRetries']>=20
    assert len(sp['parts'])==len(spec['assembly'])
    assert [r['order'] for r in sp['parts']]==list(range(len(sp['parts'])))
    assert sum(a['target']=='CPU' for a in spec['assembly'])==spec['cpus']
    assert sum(a['target']=='Cooler' for a in spec['assembly'])==spec['cpus']
    assert sum(a['target']=='RAM' for a in spec['assembly'])==spec['dimms']
    assert sum(a['target']=='Drive' for a in spec['assembly'])==spec['disks']
    targets={a['target'] for a in spec['assembly']};assert targets=={'Motherboard','Supply','CPU','Cooler','RAM','Drive'}
    used=set();power=0;ram=0;supply=None;board_order=next(a['order'] for a in spec['assembly'] if a['target']=='Motherboard')
    for a,part in zip(spec['assembly'],sp['parts']):
        src=validate(ROOT/a['path']);iv=src.item()[1]
        assert part['prefab']==dict(fileID=src.root()[0],guid=read_guid(ROOT/a['path']),type=3)
        assert part['slotTarget']==a['target'] and part['hostPrefabName']==a['host']
        host=Prefab(COMP/(a['host']+'.prefab'));slots=[v for _,v in host.mono('HardwareSlot') if v['target']==a['target']]
        assert a['index']<len(slots)
        key=(a['host'],a['target'],a['index']);assert key not in used;used.add(key)
        assert tag(src,src.root()[0])==a['target']
        if a['host']=='Server ATX':assert a['order']>board_order
        power+=iv.get('wattage',0)
        if a['target']=='Supply':supply=iv['wattage']
        if a['target']=='RAM':ram+=iv['capacity']
        if a['target']=='Drive':assert not iv['files'], 'OS goes on the spawned disk, not shared source'
    assert supply and power-supply<supply
    assert ram==spec['ram']*spec['dimms']*1000
    cpus=[a for a in spec['assembly'] if a['target']=='CPU'];coolers=[a for a in spec['assembly'] if a['target']=='Cooler']
    radius=float(re.search(r'CoolerSearchRadius = ([.\d]+)f',(ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/CPU.cs').read_text())[1])
    assert all(np.linalg.norm(np.array(a['pos'])-np.array(b['pos']))<radius for a,b in zip(cpus,coolers))
    assert sum(x['price'] for x in spec['bill_of_materials'])==spec['parts_cost']
    assert spec['price']==math.ceil(spec['parts_cost']*1.10/5)*5
    asset=Prefab(ROOT/spec['asset']).objects(114)[0][1]
    assert asset['sprite']['guid']==read_guid(ROOT/'Assets/Texture2D/server.png')
    assert spec['shop_guid'] in pages['Ready Server']

source=(ROOT/'Assets/Scripts/Assembly-CSharp/PC/Component/ServerChassis.cs').read_text()
builder=(ROOT/'Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs').read_text()
box_code=(ROOT/'Assets/Scripts/Assembly-CSharp/PC/ReadyBuildBox.cs').read_text()
assert 'builder.DeliveryDebris = GetComponentsInChildren<Collider>(true)' in box_code
assert 'IgnoreDeliveryDebris(root);' in builder and 'IgnoreDeliveryDebris(spawned);' in builder
assert 'Physics.IgnoreCollision(own, debris, true);' in builder
assert 'Instantiate(' not in source and 'AddComponent<' not in source
assert 'factoryParts.Length - 1' in source and 'part.slot.TryAttach(part.item)' in source
assert not (COMP/'Server.fbx').exists() and (ROOT/'Assets/Models/LegacyServer.fbx').exists()
assert read_guid(ROOT/'Assets/Models/LegacyServer.fbx')=='01ac59a24545e704ab738cfe54d2ae53'
for folder in ['Assets/MonoBehaviour/Servers','Assets/Texture2D/Servers','Assets/Resources/components/servers']:
    assert 'folderAsset: yes' in Path(str(ROOT/folder)+'.meta').read_text()
for g,paths in index.items():
    if any('/Servers/' in str(p) or '/components/servers/' in str(p) for p in paths):assert len(paths)==1,(g,'duplicate asset GUID')
line=next(s for s in (ROOT/'Assets/Resources/Translate.txt').read_text().splitlines() if s.startswith('Ready Server\t'))
assert len(line.split('\t'))==43 and 'Готовые серверы' in line

renders=json.loads((ROOT/'tools/server_cover_render_manifest.json').read_text())
for path,sha in renders['sources'].items():assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==sha,('render source changed',path)
for name,record in renders['covers'].items():
    path=ROOT/record['file'];im=Image.open(path).convert('RGBA');assert im.size==(256,256)
    assert im.getextrema()[3]==(255,255)
    assert hashlib.sha256(path.read_bytes()).hexdigest()==record['sha256']
    pixels=np.array(im)[:,:,:3];mask=(pixels.min(axis=2)<242);ys,xs=np.where(mask)
    assert len(xs)>300 and xs.min()>=5 and xs.max()<=250 and ys.min()>=5 and ys.max()<=250,(name,'blank/cropped image')
    assert (pixels[0,0]==255).all() and (pixels[-1,-1]==255).all()
    meta=Path(str(path)+'.meta').read_text();assert 'textureType: 8' in meta and 'spriteMode: 1' in meta
    assert read_guid(path)==manifest['covers'][name]['guid']
assert len({v['sha256'] for v in renders['covers'].values()})==4
assert set(renders['covers'])=={'Board','PSU','Cover','Tray'}
# No duplicate RAM/HDD/cooler product families, even as hidden resource files.
for pattern in ['RAM Server ECC*','HDD Enterprise*','ServerCooler Pro*','ServerCooler Max*']:
    assert not list(COMP.glob(pattern)),('unwanted new family',pattern)
for spec in manifest['builds']:
    assert all(not any(t in a['name'] for t in ['Enterprise','ECC','Cooler Pro','Cooler Max']) for a in spec['assembly'])
    assert all(a['name'] in ['RAM 32GB','RAM 64GB'] for a in spec['assembly'] if a['name'].startswith('RAM '))
    for part in spec['bill_of_materials']:
        if part['name'].startswith('RAM '):asset=part['name'].removeprefix('RAM ')
        elif part['name'].startswith('HDD '):asset=part['name']
        else:continue
        assert part['price']==Prefab(ROOT/'Assets/MonoBehaviour'/(asset+'.asset')).objects(114)[0][1]['price']

# Shop -> closed crate -> inactive broken part -> standard ready-PC plan (or empty case).
# The live server cannot be damaged by delivery physics before hammer opening.
crate_paths=[]
for record in [r for r in manifest['records'] if r.get('crate')]+manifest['builds']:
    content=ROOT/record['prefab'];crate=ROOT/record['crate'];crate_paths.append(crate)
    cp=validate(crate);asset=Prefab(ROOT/record['asset']).objects(114)[0][1]
    assert asset['spawn']['guid']==read_guid(crate) and asset['spawn']['fileID']==cp.root()[0]
    assert asset['large']==1
    assert len(cp.mono('Crate'))==1 and len(cp.mono('ReadyBuildBox'))==1
    assert not cp.mono('ServerChassis') and not cp.mono('Case') and not cp.mono('Motherboard')
    box=cp.mono('ReadyBuildBox')[0][1]
    assert box['prefab']['guid']==read_guid(content) and box['prefab']['fileID']==Prefab(content).root()[0]
    go=box['m_GameObject']['fileID'];assert cp.data(go)['m_IsActive']==0
    assert cp.mono('Crate')[0][1]['broken']['fileID']==go
    assert cp.data(cp.root()[1])['m_LocalScale']==dict(x=1,y=1,z=1)
    master=Prefab(COMP/'servers/Crate_ReadyServer_Compute.prefab').mono('ReadyBuildBox')[0][1]
    assert box['position']==master['position'] and box['rotation']==master['rotation']
    assert box['ignoreDebrisCollisions']==0, 'server debris must interact with the payload'
assert len(crate_paths)==5
# Repeat generation must not delete old products or change any generated bytes.
files=[ROOT/r['prefab'] for r in manifest['records']]+[ROOT/r['asset'] for r in manifest['records']]+[ROOT/b['prefab'] for b in manifest['builds']]+[ROOT/b['asset'] for b in manifest['builds']]+crate_paths+[ROOT/'Assets/MonoBehaviour/Shop.asset',ROOT/'tools/server_catalog_manifest.json']
before={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
subprocess.run([sys.executable,str(ROOT/'tools/generate_server_catalog.py')],check=True,stdout=subprocess.PIPE)
assert all(hashlib.sha256(p.read_bytes()).hexdigest()==sha for p,sha in before.items()),'non-idempotent generation'
print('PASS: 13 components + 4 standard ReadyBuildSpawner plans; all original shop products retained; valid GUID/fileID/physics; compatible tags/sockets; both CPUs cooled; one PCOS disk; exact BOM + 10% prices; 4 retained images + user-authored server/cooler icons; no duplicate RAM/HDD/cooler tiers; 5 Compute-sized delivery crates with normal debris collisions; idempotent generation.')
