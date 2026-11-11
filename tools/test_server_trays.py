#!/usr/bin/env python3
"""Compile real tray/chassis/case/slot code and motherboard external methods
against small engine doubles, then run event/topology tests. Not a Unity test.
Also validate Unity YAML references, resource/save identities and all sockets.
Requires PyYAML; mono/mcs for the behavioural test (otherwise explicitly skips).
"""
from pathlib import Path
import re, shutil, subprocess, tempfile
import yaml
R=Path(__file__).resolve().parents[1]
S=R/'Assets/Scripts/Assembly-CSharp'
C=R/'Assets/Resources/components'
def docs(p):
    result={}
    for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',p.read_text(),re.M|re.S):
        obj=yaml.safe_load(m[3]);result[int(m[2])]=(int(m[1]),next(iter(obj.values())))
    return result
def script(n):
    p=next(S.rglob(n+'.cs.meta'));return re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]
def monos(d,n):return [(i,v) for i,(k,v) in d.items() if k==114 and v.get('m_Script',{}).get('guid')==script(n)]
def method(s,signature):
    a=s.index(signature);b=s.index('{',a);depth=1;i=b+1
    while depth:
        depth+=(s[i]=='{')-(s[i]=='}');i+=1
    return s[a:i]
# No missing local references, duplicate IDs or broken component ownership.
for n in ['Server','Server ATX','PSUThin_2000kW','Server Cover','Server HDD Tray']:
    path=C/(n+'.prefab');d=docs(path)
    ids=re.findall(r'^--- !u!\d+ &(-?\d+)',path.read_text(),re.M)
    assert len(ids)==len(set(ids)),n
    for ref in re.finditer(r'\{fileID: (-?\d+)\}',path.read_text()):
        assert int(ref[1])==0 or int(ref[1]) in d,(n,'dangling local reference',ref[1])
    for i,(k,v) in d.items():
        if k==1:
            for ref in v.get('m_Component',[]):
                cid=ref['component']['fileID'];assert d[cid][1]['m_GameObject']['fileID']==i,(n,i,cid)
    roots=[v for k,v in d.values() if k==4 and v.get('m_Father',{}).get('fileID')==0]
    assert len(roots)==1,n
    rootgo=roots[0]['m_GameObject']['fileID']
    assert any(v['m_GameObject']['fileID']==rootgo for _,v in monos(d,'Item')+monos(d,'Motherboard')+monos(d,'Supply')+monos(d,'DriveTray')),n
    assert any(k==54 and v['m_GameObject']['fileID']==rootgo for k,v in d.values()),n
server=docs(C/'Server.prefab');board=docs(C/'Server ATX.prefab');psu=docs(C/'PSUThin_2000kW.prefab')
assert len(monos(server,'DriveTray'))==4
case=monos(server,'Case')[0][1]
assert case['external']['drive']==[], 'tray drives must NOT be permanent case externals'
assert len(case['external']['supply'])==1
for target,count in [('Motherboard',1),('Supply',1),('Drive',4)]:
    slots=[v for _,v in monos(server,'HardwareSlot') if v['target']==target];assert len(slots)==count
    for v in slots:
        go=v['m_GameObject']['fileID']
        assert any(k==65 and x['m_GameObject']['fileID']==go and x['m_IsTrigger'] for k,x in server.values()),target
mb=monos(board,'Motherboard')[0][1];assert mb['match']==2
for target,key,count in [('RAM','RAM',14),('CPU','CPU',2),('Cooler','CPU_fan',2),('USB','usb',1)]:
    physical={i for i,v in monos(board,'HardwareSlot') if v['target']==target}
    registered={x['fileID'] for x in mb['external'][key]}
    assert physical==registered and len(physical)==count,(target,len(physical))
for n,tag in [('Server','Case'),('Server Cover','Cover'),('Server HDD Tray','DriveTray'),('PSUThin_2000kW','Supply')]:
    d=docs(C/(n+'.prefab'));roottr=next(v for k,v in d.values() if k==4 and v.get('m_Father',{}).get('fileID')==0)
    assert d[roottr['m_GameObject']['fileID']][1]['m_TagString']==tag
assert len([1 for k,v in server.values() if k==1001])==1, 'keep authored sample HDD'
chassis=(S/'PC/Component/ServerChassis.cs').read_text();save=(S/'SaveManager.cs').read_text()
assert 'loading = SaveManager.RestoringSave;' in chassis and 'if (loading) return;' in chassis
assert 'part.gameObject.SetActive(false)' in chassis
assert 'hostId = hostId' in save and 'FindSavedSlot(host, saved.slotPath)' in save
assert 'public Item AttachedItem => attachedItem;' in (S/'Slot.cs').read_text()
assert 'AllStorage.Contains(s)' in (S/'PC/Component/Software/OS/ComputerSystem.cs').read_text()
print('PASS: 5 prefab local-reference/ownership/save identities; 14 RAM + 2 CPU + 2 cooler; nested-only drives; real triggers and PSU compatibility')
if not (shutil.which('mcs') and shutil.which('mono')):
    print('SKIP: behaviour tests require mcs + mono (Unity PlayMode also still required)')
else:
    source=(S/'PC/Component/Motherboard.cs').read_text()
    extracted='\n'.join(method(source,sig) for sig in [
        'public void AddExternal(', 'public void RemoveExternal(', 'private static bool ContainsSlot(',
        'private static IEnumerable<HardwareSlot> ExternalSlots(', 'public List<HardwareSlot> GetSlots(', 'public List<Hardware> GetHardwares('])
    boardstub='''using System;using System.Collections.Generic;using UnityEngine;
namespace PC.Component { public class Motherboard:Hardware {
public class External {public HardwareSlot[] CPU,CPU_fan,GPU,RAM,drive,output,supply,usb;}
private List<External> externals=new List<External>();private bool running;
private void AddBuiltIn(HardwareType type,List<Hardware> result){}
public bool Running {get{return running;}} public TestSystem System=new TestSystem();
public void ForceDown(){}public void Fault(){}public void Switch(){TestPower(!running);}
public void PowerOff(bool restart){TestPower(false);}
private void AddHardware(Hardware h){if(running)System.AddHardware(h);}
private void RemoveHardware(Hardware h){if(running)System.RemoveHardware(h);}
public void TestPower(bool on){running=on;foreach(var e in externals)foreach(var s in ExternalSlots(e))s.Switch(on);if(on)foreach(var s in GetSlots(HardwareType.Drive))if(s.Ready)System.AddHardware(s.Hardware);}
'''+extracted+'\n}}'
    with tempfile.TemporaryDirectory() as td:
        p=Path(td);(p/'board.cs').write_text(boardstub)
        files=[R/'tools/tests/server_tray_stubs.cs',R/'tools/tests/server_tray_tests.cs',p/'board.cs',S/'HardwareSlot.cs',S/'PC/Component/DriveTray.cs',S/'PC/Component/Case.cs',S/'PC/Component/ServerChassis.cs',S/'PC/Component/Software/OS/ComputerSystem.cs']
        subprocess.run(['mcs','-out:'+str(p/'tests.exe')]+list(map(str,files)),check=True)
        subprocess.run(['mono',str(p/'tests.exe')],check=True)
