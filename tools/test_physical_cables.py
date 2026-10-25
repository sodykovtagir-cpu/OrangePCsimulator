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
assert all(x in rope for x in ('CapsuleCollider','CharacterJoint','Rigidbody','ContinuousSpeculative','sleepThreshold', 'Length', 'JObject', 'RestorePorts()'))
assert 'Vector3.Distance(other.Connected.transform.position, port.transform.position) > Cable.Length * .95f' in plug
assert 'FixedJoint' in plug and 'connectedBody' in plug and 'cooldown' in plug
assert 'Hooked' in outlet and 'sockets' in outlet and 'CablePort.Add' in outlet
assert all(x in power for x in ('Energized', 'BoardHasPower', 'VideoPort', 'cpu.HasIntegratedGraphics', 'display.VideoGpu', 'board.PowerOff(false)', 'ShowNoSignal'))
assert 'CablePower.BoardHasPower(this)' in read('PC/Component/Motherboard.cs')
assert 'CablePower.RefreshAll()' in read('PC/Component/Motherboard.cs')
assert 'ConnectMonitor(selectedMonitor);' not in read('Raycast.cs')
assert 'plug.Unplug();' in read('Raycast.cs')
assert 'RestoreCableConnections' in read('SaveManager.cs')
assert 'VideoFromGpu' in read('PC/Component/Software/OS/OperatingSystem.cs')
assert 'display.VideoGpu' in read('GpuArtifacts.cs')

shop=(root/'Assets/MonoBehaviour/Shop.asset').read_text()
assert 'pageName: Cables & Power' in shop
assert 'guid: 78348b2c142bcb88381354c04ae3fce7' in shop  # keep first ready PC
ids=[]
for kind in ('Power','VGA','HDMI'):
    paths=list((root/'Assets/Resources/components').glob(f'{kind} Cable *.prefab'))
    assert len(paths)==3,kind
    lengths=set()
    for p in paths:
        text=p.read_text()
        assert check_prefab(str(p))==0
        assert f'spawnId: {p.stem}' in text
        lengths.add(float(re.search(r'^  length: ([0-9.]+)$',text,re.M).group(1)))
        ids.append(re.search(r'^guid: ([a-f0-9]{32})$',p.with_suffix('.prefab.meta').read_text(),re.M).group(1))
    assert len(lengths)==3
for sockets in (2,3):
    p=root/f'Assets/Resources/components/Wall Outlet {sockets}.prefab'
    assert check_prefab(str(p))==0
    assert f'  sockets: {sockets}' in p.read_text()
    ids.append(re.search(r'^guid: ([a-f0-9]{32})$',p.with_suffix('.prefab.meta').read_text(),re.M).group(1))
assert len(set(ids))==11
rows=(root/'Assets/Resources/Translate.txt').read_text().splitlines()
header=rows[0].split('\t')
for key in ('Cables & Power','Power Cable','VGA Cable','HDMI Cable','Wall Outlet'):
    row=next(r.split('\t') for r in rows if r.startswith(key+'\t'))
    assert row[header.index('RU')] and row[header.index('EN')]
assert len(re.findall(r'^    - \{fileID: 11400000, guid: [a-f0-9]{32}, type: 2\}$',
    shop.split('  - pageName: Cables & Power\n')[1].split('  - pageName:',1)[0],re.M))==11
for kind in ('power','vga','hdmi','outlet'):
    image=root/f'Assets/Texture2D/icon_cable_{kind}.png'
    assert image.exists() and image.with_suffix('.png.meta').exists()
    from PIL import Image
    with Image.open(image) as graphic: graphic.verify()
    sprite_guid=re.search(r'^guid: ([a-f0-9]{32})$',image.with_suffix('.png.meta').read_text(),re.M).group(1)
    assets=(root/'Assets/MonoBehaviour').glob((kind.upper() if kind in ('vga','hdmi') else 'Power' if kind=='power' else 'Wall Outlet')+'*.asset')
    assert any(sprite_guid in asset.read_text() for asset in assets)
print('Physical cables, power/video routes, persistence, 11 shop items: OK')
