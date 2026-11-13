#!/usr/bin/env python3
"""Generate shop assets and ReadyBuildSpawner plans, like ordinary ready PCs.
Run from any cwd. Images are rendered separately by render_server_covers.py.
The user's geometry, transforms and existing component GUIDs are preserved.
"""
from pathlib import Path
import json, math, re
from server_assets import ROOT, COMP, Prefab, guid, read_guid, meta, ref
from unity_asset_tool import ShopItemAsset, shop_add_page, shop_pages, write_crate_prefab, prefab_bounds, write_spawner_prefab, ReadyBuild, PartRef

CPU_SOURCE='CPU RMD Ryzen Threadripper 3960X'
CPUS=[
 ('RMD Ryzen Threadripper 1920X',850,3900,180,3.5,40),
 ('RMD Ryzen Threadripper 2920X',1000,4400,180,3.5,44),
 ('RMD Ryzen Threadripper 3960X',1250,5200,280,3.8,55),
 ('RMD Ryzen Threadripper 3970X',1700,6800,280,3.7,60),
 ('RMD Ryzen Threadripper 3990X',2500,9500,280,2.9,65),
 ('RMD Ryzen Threadripper PRO 5975WX',2100,8200,280,3.6,60),
 ('RMD Ryzen Threadripper PRO 5995WX',3300,12200,280,2.7,70),
]
# Price references: desktop Ryzen 7950X=800/3725 score; 64 GB=760;
# HDD 8/16/20 TB=190/340/450; 2kW PSU=300; EXATX=400; large miner frame=500.
RECORDS=[]
ASSETS=ROOT/'Assets/MonoBehaviour/Servers'
IMAGES=ROOT/'Assets/Texture2D/Servers'
READY=COMP/'servers'
ASSETS.mkdir(parents=True,exist_ok=True);meta(ASSETS,'DefaultImporter')
IMAGES.mkdir(parents=True,exist_ok=True);meta(IMAGES,'DefaultImporter')
READY.mkdir(parents=True,exist_ok=True);meta(READY,'DefaultImporter')

def clone(source,name,values):
    p=Prefab(COMP/(source+'.prefab'));item,data=p.item();go,tr=p.root()
    p.set(item,'spawnId',name);p.set(item,'info',values.pop('info',name.removeprefix('CPU ')))
    if 'm_Name:' in p.blocks[go]:p.set(go,'m_Name',name)
    else:
        for i,v in p.objects(1001):
            b=p.blocks[i]
            source_root=p.data(go)['m_CorrespondingSourceObject']['fileID']
            pattern=r'(    - target: \{fileID: '+str(source_root)+r', guid: \w+, type: 3\}\n      propertyPath: m_Name\n      value:) [^\n]+'
            b=re.sub(pattern,lambda m:m[1]+' '+name,b,count=1)
            p.blocks[i]=b
    for k,v in values.items():p.set(item,k,str(v))
    out=COMP/(name+'.prefab');p.save(out);meta(out);return out

def cover_guid(icon):
    if icon=='RMD':return read_guid(ROOT/'Assets/Texture2D/RmdServer.png')
    if icon=='Cooler':return read_guid(ROOT/'Assets/Texture2D/servercooler.png')
    if icon=='Chassis':return read_guid(ROOT/'Assets/Texture2D/server.png')
    return guid('cover:'+icon)

def delivery(name,content):
    """The closed crate contains no live server; hammer activates the standard ready-PC spawner."""
    crate_name='Crate_'+name
    path=READY/(crate_name+'.prefab')
    bounds=prefab_bounds(str(COMP/'Server.prefab'),str(ROOT))
    # Box position is at the crate centre: keep the lowest server collider
    # above the bottom of the standard 4.5-high crate, even on floor opening.
    lift=round(-2.25+.15-bounds[0][1],3) if bounds else 0
    g,root=write_crate_prefab(str(path),crate_name,
        str(COMP/'Crate_Case_ATX 2(Black).prefab'),read_guid(content),Prefab(content).root()[0],lift=lift)
    return g,root,str(path.relative_to(ROOT))

def add(name,price,page,icon,description,large=0):
    p=Prefab(COMP/(name+'.prefab'));root,_=p.root()
    spawn_guid,spawn_root,crate=delivery(name,COMP/(name+'.prefab')) if name=='Server' else (read_guid(COMP/(name+'.prefab')),root,None)
    entry=ShopItemAsset(name='ServerShop_'+name.replace('/','_'),item_name=name.removeprefix('CPU '),price=price,
        spawn_guid=spawn_guid,spawn_file_id=spawn_root,sprite_guid=cover_guid(icon),sprite_type=3,
        bitcoin=0,large=large,description=description,translate_description=0)
    path=ASSETS/(entry.name+'.asset');g=entry.write(str(path),guid('shop:'+name))
    RECORDS.append(dict(name=name,price=price,page=page,icon=icon,asset=str(path.relative_to(ROOT)),shop_guid=g,
        prefab=str((COMP/(name+'.prefab')).relative_to(ROOT)),description=description,crate=crate))

for short,price,score,watts,clock,heat in CPUS:
    name='CPU '+short
    if name!=CPU_SOURCE:
        clone(CPU_SOURCE,name,dict(score=score,wattage=watts,defaultFrequency=clock,frequency=clock,heat=heat,match=1))
    add(name,price,'Processor','RMD',f'Server socket 1 / score {score} / {clock:g} GHz / {watts} W / Server ATX')

add('Server',350,'Case','Chassis','4 removable HDD trays + removable cover included. Server ATX / thin PSU. Empty chassis.',1)
add('Server ATX',650,'Motherboard','Board','2 server CPU sockets / 14 DIMM slots / 2 cooler slots / USB / Server chassis',1)
add('PSUThin_2000kW',350,'Power Supply','PSU','Thin server PSU / 2000 W / Server chassis')
add('Server Cover',40,'Case','Cover','Replacement removable cover / Server chassis')
add('Server HDD Tray',25,'Storage','Tray','Replacement removable HDD tray / one 3.5-inch HDD / Server chassis')

# Only the user's original cooler. No server-only RAM/HDD/cooler tiers.
add('ServerCooler',85,'Cooler','Cooler','Server socket cooler / cooling 85 / 15 W')

# Existing RMD processor uses the user's standard RMD icon too. Do not use
# the shop icon as a UV texture; keep the authored Threadripper material.
p=ROOT/'Assets/MonoBehaviour/RMD Ryzen 9 7950X.asset';s=p.read_text()
s=re.sub(r'^  sprite: .*$',f'  sprite: {{fileID: 21300000, guid: {cover_guid("RMD")}, type: 3}}',s,flags=re.M);p.write_text(s)

BUILD_SPECS=[
 dict(id='ReadyServer_Entry',title='Orange Rack S1',cpu=CPUS[0][0],cpus=1,ram=32,dimms=2,disk='HDD 2TB',disks=2,cooler='ServerCooler'),
 dict(id='ReadyServer_Storage',title='Orange Rack Storage',cpu=CPUS[2][0],cpus=1,ram=32,dimms=4,disk='HDD 8TB',disks=4,cooler='ServerCooler'),
 dict(id='ReadyServer_Compute',title='Orange Rack Compute',cpu=CPUS[3][0],cpus=2,ram=64,dimms=8,disk='HDD 16TB',disks=2,cooler='ServerCooler'),
 dict(id='ReadyServer_Ultra',title='Orange Rack Ultra',cpu=CPUS[6][0],cpus=2,ram=64,dimms=14,disk='HDD 20TB',disks=4,cooler='ServerCooler'),
]
price_by_name={x['name']:x['price'] for x in RECORDS}
# Exact existing shop prices, not parallel 'server' products.
for name,asset in [('RAM 32GB','32GB'),('RAM 64GB','64GB')]+[(f'HDD {tb}TB',f'HDD {tb}TB') for tb in [2,8,16,20]]:
    price_by_name[name]=next(v['price'] for _,v in Prefab(ROOT/'Assets/MonoBehaviour'/(asset+'.asset')).objects(114))
OS_APPS=['Info','Disk','Downloader','Browser','Benchmark']
for spec in BUILD_SPECS:
    name=spec['id'];parts=[]
    def put(source,target,host=None):
        parts.append(PartRef(str((COMP/(source+'.prefab')).relative_to(ROOT)),target,host=host))
    put('PSUThin_2000kW','Supply')
    for i in range(spec['disks']):put(spec['disk'],'Drive')
    put('Server ATX','Motherboard')
    for i in range(spec['cpus']):
        put('CPU '+spec['cpu'],'CPU','Server ATX')
        put(spec['cooler'],'Cooler','Server ATX')
    for i in range(spec['dimms']):put(f'RAM {spec["ram"]}GB','RAM','Server ATX')
    # Same resolver and runtime script used by ordinary ready PCs/miners.
    # Existing four factory caddies expose their Drive slots under the case.
    assembly=ReadyBuild(name,str((COMP/'Server.prefab').relative_to(ROOT)),parts).resolve(str(ROOT))
    bill=[dict(name=p['name'],price=price_by_name[p['name']]) for p in assembly]
    prefab_guid,prefab_root=write_spawner_prefab(str(READY/(name+'.prefab')),name,
        read_guid(COMP/'Server.prefab'),Prefab(COMP/'Server.prefab').root()[0],assembly,apps=OS_APPS,root_dir=str(ROOT))
    total=350+sum(v['price'] for v in bill);price=math.ceil(total*1.10/5)*5
    desc=f'{spec["cpus"]}x {spec["cpu"]} / {spec["dimms"]}x {spec["ram"]} GB ({spec["dimms"]*spec["ram"]} GB) / {spec["disks"]}x {spec["disk"]} / 2000 W / PCOS'
    spawn_guid,spawn_root,crate=delivery(name,READY/(name+'.prefab'))
    entry=ShopItemAsset(name=name,item_name=spec['title'],price=price,spawn_guid=spawn_guid,
        spawn_file_id=spawn_root,sprite_guid=cover_guid('Chassis'),sprite_type=3,large=1,description=desc,translate_description=0)
    path=ASSETS/(name+'.asset');g=entry.write(str(path),guid('shop:'+name))
    spec.update(prefab=str((READY/(name+'.prefab')).relative_to(ROOT)),asset=str(path.relative_to(ROOT)),shop_guid=g,
        parts_cost=total,price=price,crate=crate,assembly=assembly,bill_of_materials=[dict(name='Server',price=350)]+bill,description=desc)

# Append only our entries to the existing standard pages. Never reorder/remove
# the user's current shop or re-run the old PC/miner generator.
shop=ROOT/'Assets/MonoBehaviour/Shop.asset'
# Unwanted experimental variants from the first catalog revision.
removed_names=['ServerCooler Pro','ServerCooler Max']+[f'RAM Server ECC {n}GB' for n in [32,64,128]]+[f'HDD Enterprise {n}TB' for n in [8,16,20]]
removed_shop_guids={guid('shop:'+name) for name in removed_names}
for page,items in shop_pages(str(shop)):
    if any(g in removed_shop_guids for g in items):
        shop_add_page(str(shop),page,[g for g in items if g not in removed_shop_guids])
for name in removed_names:
    for path in [COMP/(name+'.prefab'),ASSETS/('ServerShop_'+name+'.asset')]:
        path.unlink(missing_ok=True);Path(str(path)+'.meta').unlink(missing_ok=True)
for name in ['Memory','Disk','Cooler','Chassis']:
    path=IMAGES/(name+'.png');path.unlink(missing_ok=True);Path(str(path)+'.meta').unlink(missing_ok=True)
existing=dict(shop_pages(str(shop)))
for page in sorted({r['page'] for r in RECORDS}):
    merged=list(dict.fromkeys(existing.get(page,[])+[r['shop_guid'] for r in RECORDS if r['page']==page]))
    shop_add_page(str(shop),page,merged)
shop_add_page(str(shop),'Ready Server',[b['shop_guid'] for b in BUILD_SPECS])

# One fully translated tab; model/spec names intentionally remain product names.
translations=['Ready Servers','Kész szerverek','Kant-en-klare servers','Servidores montados','Servere gata','Gotowe serwery','Hotové servery','Gotovi serveri','Servidores prontos','Servidores prontos','Valmis serverid','Serveurs prêts','Serveurs prêts','완제품 서버','整机服务器','整機伺服器','Готови сервери','Готовые серверы','Hazır sunucular','Valmiit palvelimet','Готови сървъри','Готові сервери','Fertige Server','Servidors muntats','Server pronti','完成品サーバー','Paruošti serveriai','خوادم جاهزة','Дайын серверлер','Ferdige servere','Handang server','तैयार सर्वर','เซิร์ฟเวอร์สำเร็จรูป','Server siap pakai','მზა სერვერები','Gatavi serveri','Máy chủ dựng sẵn','Έτοιμοι διακομιστές','Гатовыя серверы','Hotové servery','Färdiga servrar','سرورهای آماده']
assert len(translations)==42
p=ROOT/'Assets/Resources/Translate.txt';lines=p.read_text().splitlines();lines=[l for l in lines if not l.startswith('Ready Server\t')];lines.append('Ready Server\t'+'\t'.join(translations));p.write_text('\n'.join(lines)+'\n')

manifest=dict(base_commit='ed3df93b5a7536e65c2325531af263f64c1ad853',markup=1.10,round_up_to=5,records=RECORDS,builds=BUILD_SPECS,
    covers={key:dict(guid=cover_guid(key),source=source) for key,source in [('Board','Server ATX'),('PSU','PSUThin_2000kW'),('Cover','Server Cover'),('Tray','Server HDD Tray')]})
(ROOT/'tools/server_catalog_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print(f'Generated {len(RECORDS)} component shop entries, {len(CPUS)} server CPUs and {len(BUILD_SPECS)} standard ready-PC server plans.')
for b in BUILD_SPECS:print(f'{b["title"]}: parts {b["parts_cost"]} + 10%, rounded up to 5 => {b["price"]}')
