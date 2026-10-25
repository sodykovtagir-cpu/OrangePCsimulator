#!/usr/bin/env python3
"""Static wiring/asset integrity guards; Unity Play Mode is still required."""
from pathlib import Path
import re
from check_prefab_yaml import main as check_prefab

root=Path(__file__).resolve().parents[1]
code=root/'Assets/Scripts/Assembly-CSharp'
read=lambda path:(code/path).read_text()
ports=read('PC/Component/Cabling/CablePort.cs')
rope=read('PC/Component/Cabling/CableItem.cs')
plug=read('PC/Component/Cabling/CablePlug.cs')
power=read('PC/Component/Cabling/CablePower.cs')
outlet=read('PC/Component/Cabling/OutletStrip.cs')
assert 'PortKind.Outlet' in ports and 'PortKind.PcPower' in ports and 'PortKind.MonitorPower' in ports
assert 'PortKind.VgaInput' in ports and 'PortKind.HdmiInput' in ports
assert 'cable.Kind == CableKind.VGA' in ports and 'cable.Kind == CableKind.HDMI' in ports
assert 'plug != null' in ports and 'plug = this' in plug
assert 'cable.End(1 - candidate.End)' in ports and 'input.Cable.End(1 - input.End)' in power
assert 'cable.End(1 - input.plug.End)' in power
assert 'Physics' not in rope or 'CapsuleCollider' in rope
assert all(x in rope for x in ('CapsuleCollider','CharacterJoint','Rigidbody','ContinuousSpeculative','Physics.IgnoreCollision', 'LineRenderer', 'Length', 'JObject', 'RestorePorts()'))
assert 'Vector3.Distance(other.Connected.Snap.position, port.Snap.position) > Cable.Length * .95f' in plug
assert 'FixedJoint' in plug and 'connectedBody' in plug and 'cooldown' in plug
assert 'Hooked' in outlet and 'sockets' in outlet and 'CablePort.Add' in outlet
assert all(x in power for x in ('Energized', 'BoardHasPower', 'VideoPort', 'cpu.HasIntegratedGraphics', 'display.VideoGpu', 'board.PowerOff(false)', 'ShowNoSignal'))
assert 'CablePower.BoardHasPower(this)' in read('PC/Component/Motherboard.cs')
assert 'CablePower.RefreshAll()' in read('PC/Component/Motherboard.cs')
assert 'ConnectMonitor(selectedMonitor);' not in read('Raycast.cs')
raycast=read('Raycast.cs')
assert 'plug.Unplug();' in raycast
assert 'plug.SetHeld(true)' in raycast and 'releasedPlug.SetHeld(false)' in raycast
assert '(!restoring && IsHeld)' in plug  # dragger must release before snapping
assert 'CablePorts.Install(this)' not in read('PC/Component/Case.cs')
assert 'Case AC input' not in read('PC/Component/Cabling/CablePorts.cs')
assert 'Case board video' not in read('PC/Component/Cabling/CablePorts.cs')
assert 'Case GPU video' not in read('PC/Component/Cabling/CablePorts.cs')
assert 'RestoreCableConnections' in read('SaveManager.cs')
assert 'VideoFromGpu' in read('PC/Component/Software/OS/OperatingSystem.cs')
assert 'display.VideoGpu' in read('GpuArtifacts.cs')
# CS0136 in Board() prevented Unity from compiling CableItem/OutletStrip,
# which made every new prefab appear to have a missing script.
assert 'var pcCase = connector.slot.GetComponentInParent<Case>();' not in read('PC/Component/Cabling/CablePorts.cs')
assert 'solidCase.enabled = false' in read('PC/Component/Cabling/CableCaseOpening.cs')
assert all(side in read('PC/Component/Cabling/CableCaseOpening.cs')
           for side in ('"Left"','"Right"','"Below"','"Above"'))
editor_sockets=(root/'Assets/Editor/PrepareCableSockets.cs').read_text()
assert 'Prepare sockets in selected prefab' in editor_sockets
assert 'GetComponentsInChildren<PC.Component.Display>' in editor_sockets  # UnityEngine.Display is ambiguous
assert 'Case AC input' not in editor_sockets and 'Case GPU video' not in editor_sockets

def script_guid(relative):
    meta=(code/(relative+'.meta')).read_text()
    return re.search(r'^guid: ([a-f0-9]{32})$',meta,re.M).group(1)

cable_script=script_guid('PC/Component/Cabling/CableItem.cs')
outlet_script=script_guid('PC/Component/Cabling/OutletStrip.cs')
plug_script=script_guid('PC/Component/Cabling/CablePlug.cs')
port_script=script_guid('PC/Component/Cabling/CablePort.cs')
opening_script=script_guid('PC/Component/Cabling/CableCaseOpening.cs')
hook_script=script_guid('Hook.cs')
def prefab_scripts(prefab):
    return re.findall(r'^  m_Script: \{fileID: 11500000, guid: ([a-f0-9]{32}), type: 3\}$',prefab,re.M)

shop=(root/'Assets/MonoBehaviour/Shop.asset').read_text()
assert 'pageName: Cables & Power' in shop
assert 'guid: 78348b2c142bcb88381354c04ae3fce7' in shop  # keep first ready PC
ids=[]
for kind in ('Power','VGA','HDMI'):
    paths=list((root/'Assets/Resources/components').glob(f'{kind} Cable *.prefab'))
    assert len(paths)==3,kind
    expected={'Power':{3,6,10},'VGA':{3,7,11},'HDMI':{4,8,12}}[kind]
    lengths=set()
    for p in paths:
        text=p.read_text()
        assert check_prefab(str(p))==0
        assert f'spawnId: {p.stem}' in text
        assert prefab_scripts(text)==[cable_script,plug_script,plug_script],p
        assert text.count('  m_Name: Plug ') == 2
        assert '  plugA: {fileID: 1140010}' in text and '  plugB: {fileID: 1140020}' in text
        assert '  jacket: {fileID: 1200000}' in text and 'LineRenderer:' in text
        assert '  cableDiameter: .14' in text
        material=root/f'Assets/Material/Cable {kind}.mat.meta'
        mat_guid=re.search(r'^guid: ([a-f0-9]{32})$',material.read_text(),re.M).group(1)
        assert mat_guid in text,p
        lengths.add(float(re.search(r'^  length: ([0-9.]+)$',text,re.M).group(1)))
        prefab_guid=re.search(r'^guid: ([a-f0-9]{32})$',p.with_suffix('.prefab.meta').read_text(),re.M).group(1)
        item=root/f'Assets/MonoBehaviour/{p.stem}.asset'
        assert prefab_guid in item.read_text(),p
        shop_guid=re.search(r'^guid: ([a-f0-9]{32})$',item.with_suffix('.asset.meta').read_text(),re.M).group(1)
        assert shop_guid in shop,p
        ids.append(prefab_guid)
    assert lengths==expected
for sockets in (1,2,3):
    p=root/f'Assets/Resources/components/Wall Outlet {sockets}.prefab'
    assert check_prefab(str(p))==0
    assert f'  sockets: {sockets}' in p.read_text()
    assert prefab_scripts(p.read_text())==[hook_script,outlet_script],p
    prefab_guid=re.search(r'^guid: ([a-f0-9]{32})$',p.with_suffix('.prefab.meta').read_text(),re.M).group(1)
    item=root/f'Assets/MonoBehaviour/Wall Outlet {sockets}.asset'
    assert prefab_guid in item.read_text(),p
    shop_guid=re.search(r'^guid: ([a-f0-9]{32})$',item.with_suffix('.asset.meta').read_text(),re.M).group(1)
    assert shop_guid in shop,p
    ids.append(prefab_guid)
assert len(set(ids))==12
for template,script_name in (("Cable Socket TEMPLATE",port_script),("PSU Opening TEMPLATE",opening_script)):
    p=root/f"Assets/Resources/components/{template}.prefab"
    assert check_prefab(str(p))==0
    assert prefab_scripts(p.read_text())==[script_name],p
rows=(root/'Assets/Resources/Translate.txt').read_text().splitlines()
header=rows[0].split('\t')
for key in ('Cables & Power','Power Cable','VGA Cable','HDMI Cable','Wall Outlet'):
    row=next(r.split('\t') for r in rows if r.startswith(key+'\t'))
    assert row[header.index('RU')] and row[header.index('EN')]
assert len(re.findall(r'^    - \{fileID: 11400000, guid: [a-f0-9]{32}, type: 2\}$',
    shop.split('  - pageName: Cables & Power\n')[1].split('  - pageName:',1)[0],re.M))==12
for kind in ('power','vga','hdmi','outlet'):
    image=root/f'Assets/Texture2D/icon_cable_{kind}.png'
    assert image.exists() and image.with_suffix('.png.meta').exists()
    from PIL import Image
    with Image.open(image) as graphic: graphic.verify()
    sprite_guid=re.search(r'^guid: ([a-f0-9]{32})$',image.with_suffix('.png.meta').read_text(),re.M).group(1)
    assets=(root/'Assets/MonoBehaviour').glob((kind.upper() if kind in ('vga','hdmi') else 'Power' if kind=='power' else 'Wall Outlet')+'*.asset')
    assert any(sprite_guid in asset.read_text() for asset in assets)
print('Physical cables, power/video routes, persistence, 12 shop items: OK')
