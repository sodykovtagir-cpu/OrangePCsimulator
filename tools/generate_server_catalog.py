#!/usr/bin/env python3
"""Generate shop assets and fully authored ready-server PREFABS, not spawners.
Run from any cwd. Images are rendered separately by render_server_covers.py.
The user's geometry, transforms and existing component GUIDs are preserved.
"""
from pathlib import Path
import json, math, re
from server_assets import ROOT, COMP, Prefab, guid, read_guid, meta, ref
from unity_asset_tool import ShopItemAsset, shop_add_page, shop_pages, render_os_files

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
    return guid('cover:'+icon)

def add(name,price,page,icon,description,large=0):
    p=Prefab(COMP/(name+'.prefab'));root,_=p.root()
    entry=ShopItemAsset(name='ServerShop_'+name.replace('/','_'),item_name=name.removeprefix('CPU '),price=price,
        spawn_guid=read_guid(COMP/(name+'.prefab')),spawn_file_id=root,sprite_guid=cover_guid(icon),sprite_type=3,
        bitcoin=0,large=large,description=description,translate_description=0)
    path=ASSETS/(entry.name+'.asset');g=entry.write(str(path),guid('shop:'+name))
    RECORDS.append(dict(name=name,price=price,page=page,icon=icon,asset=str(path.relative_to(ROOT)),shop_guid=g,
        prefab=str((COMP/(name+'.prefab')).relative_to(ROOT)),description=description))

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

COOLERS=[('ServerCooler',85,85,15),('ServerCooler Pro',110,105,18),('ServerCooler Max',145,130,22)]
for name,price,cooling,watts in COOLERS:
    if name!='ServerCooler':clone('ServerCooler',name,dict(cooling=cooling,wattage=watts,info=name,score=1))
    add(name,price,'Cooler','Cooler',f'Server socket cooler / cooling {cooling} / {watts} W')
for gb,price in [(32,420),(64,840),(128,1680)]:
    name=f'RAM Server ECC {gb}GB'
    clone('RAM 64GB',name,dict(capacity=gb*1000,score=int(gb*156.25),wattage=4 if gb<128 else 6,info=f'Server ECC {gb}GB'))
    add(name,price,'Memory','Memory',f'{gb} GB / server DIMM / ECC product line / removable module')
for tb,price in [(8,230),(16,410),(20,540)]:
    name=f'HDD Enterprise {tb}TB'
    clone(f'HDD {tb}TB',name,dict(info=name,storageName=f'Enterprise {tb}TB',score={8:1700,16:1900,20:2100}[tb]))
    add(name,price,'Storage','Disk',f'{tb} TB / 3.5-inch HDD / removable server tray / no preinstalled OS')

# Existing RMD processor uses the user's standard RMD icon too. Do not use
# the shop icon as a UV texture; keep the authored Threadripper material.
p=ROOT/'Assets/MonoBehaviour/RMD Ryzen 9 7950X.asset';s=p.read_text()
s=re.sub(r'^  sprite: .*$',f'  sprite: {{fileID: 21300000, guid: {cover_guid("RMD")}, type: 3}}',s,flags=re.M);p.write_text(s)

BUILD_SPECS=[
 dict(id='ReadyServer_Entry',title='Orange Rack S1',cpu=CPUS[0][0],cpus=1,ram=32,dimms=2,disk='HDD 2TB',disks=2,cooler='ServerCooler'),
 dict(id='ReadyServer_Storage',title='Orange Rack Storage',cpu=CPUS[2][0],cpus=1,ram=32,dimms=4,disk='HDD Enterprise 8TB',disks=4,cooler='ServerCooler'),
 dict(id='ReadyServer_Compute',title='Orange Rack Compute',cpu=CPUS[3][0],cpus=2,ram=64,dimms=8,disk='HDD Enterprise 16TB',disks=2,cooler='ServerCooler Pro'),
 dict(id='ReadyServer_Ultra',title='Orange Rack Ultra',cpu=CPUS[6][0],cpus=2,ram=128,dimms=14,disk='HDD Enterprise 20TB',disks=4,cooler='ServerCooler Max'),
]
price_by_name={x['name']:x['price'] for x in RECORDS};price_by_name['HDD 2TB']=80
OS_APPS=['Info','Disk','Downloader','Browser','Benchmark']
for spec in BUILD_SPECS:
    p=Prefab(COMP/'Server.prefab');root,tr=p.root();item,_=p.item()
    name=spec['id'];p.set(root,'m_Name',name);p.set(item,'spawnId',name);p.set(item,'info',spec['title'])
    chassis,ch=p.mono('ServerChassis')[0];case=p.mono('Case')[0][1];entries=[];bill=[];assembly=[]
    def put(source,slot,os=False):
        dest=p.data(slot)['insertPos']['fileID']
        it,mapping,part_tr=p.clone_into(COMP/(source+'.prefab'),dest)
        entries.append((it,slot));bill.append(dict(name=source,price=price_by_name[source]))
        assembly.append(dict(name=source,item=it,slot=slot,transform=part_tr))
        if os:
            block,total=render_os_files(OS_APPS,str(ROOT))
            p.set(it,'files',block);assert total<p.data(it)['capacity']
        return mapping
    board_map=put('Server ATX',case['motherboard']['fileID'])
    put('PSUThin_2000kW',case['external']['supply'][0]['fileID'])
    b=Prefab(COMP/'Server ATX.prefab');external=b.mono('Motherboard')[0][1]['external']
    for i in range(spec['cpus']):
        put('CPU '+spec['cpu'],board_map[external['CPU'][i]['fileID']])
        put(spec['cooler'],board_map[external['CPU_fan'][i]['fileID']])
    for r in external['RAM'][:spec['dimms']]:put(f'RAM Server ECC {spec["ram"]}GB',board_map[r['fileID']])
    # Order drive bays physically left to right. This is also boot disk order.
    trays=[]
    for trayid,tv in p.mono('DriveTray'):
        go=tv['m_GameObject']['fileID'];tt=next(v for _,v in p.objects(4) if v.get('m_GameObject',{}).get('fileID')==go)
        trays.append((tt['m_LocalPosition']['x'],tv['driveSlot']['fileID']))
    for i,(_,slot) in enumerate(sorted(trays)[:spec['disks']]):put(spec['disk'],slot,os=i==0)
    text=''.join(f'  - item: {ref(it)}\n    slot: {ref(sl)}\n' for it,sl in entries)
    p.blocks[chassis]+='  factoryParts:\n'+text
    p.save(READY/(name+'.prefab'));meta(READY/(name+'.prefab'))
    total=350+sum(v['price'] for v in bill);price=math.ceil(total*1.10/5)*5
    desc=f'{spec["cpus"]}x {spec["cpu"]} / {spec["dimms"]}x {spec["ram"]} GB ECC ({spec["dimms"]*spec["ram"]} GB) / {spec["disks"]}x {spec["disk"]} / 2000 W / PCOS'
    entry=ShopItemAsset(name=name,item_name=spec['title'],price=price,spawn_guid=read_guid(READY/(name+'.prefab')),
        spawn_file_id=root,sprite_guid=cover_guid('Chassis'),sprite_type=3,large=1,description=desc,translate_description=0)
    path=ASSETS/(name+'.asset');g=entry.write(str(path),guid('shop:'+name))
    spec.update(prefab=str((READY/(name+'.prefab')).relative_to(ROOT)),asset=str(path.relative_to(ROOT)),shop_guid=g,
        parts_cost=total,price=price,assembly=assembly,bill_of_materials=[dict(name='Server',price=350)]+bill,description=desc)

# Append only our entries to the existing standard pages. Never reorder/remove
# the user's current shop or re-run the old PC/miner generator.
shop=ROOT/'Assets/MonoBehaviour/Shop.asset'
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
    covers={key:dict(guid=cover_guid(key),source=source) for key,source in [('Chassis','Server'),('Board','Server ATX'),('PSU','PSUThin_2000kW'),('Cover','Server Cover'),('Tray','Server HDD Tray'),('Cooler','ServerCooler'),('Memory','RAM Server ECC 64GB'),('Disk','HDD Enterprise 16TB')]})
(ROOT/'tools/server_catalog_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print(f'Generated {len(RECORDS)} component shop entries, {len(CPUS)} server CPUs and {len(BUILD_SPECS)} authored ready-server prefabs.')
for b in BUILD_SPECS:print(f'{b["title"]}: parts {b["parts_cost"]} + 10%, rounded up to 5 => {b["price"]}')
