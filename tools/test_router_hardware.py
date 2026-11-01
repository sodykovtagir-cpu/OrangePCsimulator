#!/usr/bin/env python3
"""Static safeguards for authored sockets / activity; Unity Play Mode still required."""
from pathlib import Path
from check_prefab_yaml import main as check_prefab
root=Path(__file__).resolve().parents[1]
cs=root/'Assets/Scripts/Assembly-CSharp'
read=lambda x:(cs/x).read_text()
router=read('PC/Component/Networking/RouterDevice.cs')
ind=read('PC/Component/Networking/RouterIndicators.cs')
net=read('PC/Component/Networking/InternetService.cs')
port=read('PC/Component/Cabling/CablePort.cs')
rope=read('PC/Component/Cabling/CableItem.cs')
board=read('PC/Component/Motherboard.cs')
mode=read('PC/Component/Cabling/CableMode.cs')
web=read('PC/Component/Software/Browser.cs')
remote=read('PC/Component/Software/RemoteDesk.cs')
saver=read('SaveManager.cs')
pref=root/'Assets/Resources/components/TypolinkRouter.prefab'
cable=root/'Assets/Resources/components/LAN Cable (48m).prefab'
assert check_prefab(str(pref)) == 0
assert check_prefab(str(cable)) == 0
r=pref.read_text();c=cable.read_text()
assert 'spawnId: TypoLinkRouter' in r and 'ssid: TypoLink_AP' in r
assert all('  m_Name: '+x+'\n' in r for x in ('Power','WAN','WIFI','LAN1','LAN2','LAN3','LAN4'))
assert 'Router/ports/Power' in router
assert all('"LAN ('+str(i)+')"' in router for i in (1,2,3))
assert 'CablePort.AddAuthored(item, PortKind.RouterPower' in router
assert 'CablePort.AddAuthored(item, PortKind.RouterLan' in router
assert 'string[] names = { "LAN (3)", "LAN (2)", "LAN (1)", "LAN" };' in router
assert '(!CableMode.Enabled || HasMainsCable)' in router
assert 'CablePower.Energized((powerPort ?? BindAuthoredPowerPort())?.plug)' in router
assert 'candidate.kind != PortKind.RouterPower' in router and 'candidate.GetComponentInParent<Item>() != item' in router
assert 'port.owner != board || port.kind != PortKind.PcLan' in net
assert 'port.plug.Cable.Kind != CableKind.LAN' in net
assert 'PhysicalLanTo(parent, out _, out _)' in router
assert 'public enum CableKind { Power, VGA, HDMI, LAN }' in port
assert 'RouterPower, RouterLan, PcLan' in port
assert 'opposite.kind == PortKind.RouterPower' in port
assert 'public static CablePort AddAuthored' in port
assert 'materials = new Material[4]' in rope
assert 'lanPlugModel' in rope and 'Instantiate(lanPlugModel, go.transform)' in rope
assert '  kind: 3\n  length: 48' in c
# The player's latest prefab removes the old solid Power/Trigger box entirely;
# a script-driven fallback still works when the imported FBX hierarchy moves.
import re
power_port=re.search(r'--- !u!1 &8334559102428477866\nGameObject:.*?(?=--- !u!|\Z)',r,re.S)
assert power_port and '  - component: {fileID: 1804551114551547151}' not in power_port.group(0)
assert 'm_GameObject: {fileID: 8334559102428477866}' in r
assert '  kind: 7\n  owner: {fileID: 6213257954193014605}' in r
assert 'BindAuthoredPowerPort();' in router
# Each SKU is physical, has its own length/coil, and contains two
# serialized imported RJ45 instances; no HDMI visual remains in Prefab Mode.
import re
fbx='25abe6272ecacd641a52d6b37b16b6f9'
icon=re.search(r'^guid: ([a-f0-9]{32})$',
    (root/'Assets/Texture2D/icon_cable_lan.png.meta').read_text(), re.M).group(1)
mat=re.search(r'^guid: ([a-f0-9]{32})$',
    (root/'Assets/Material/Cable LAN.mat.meta').read_text(), re.M).group(1)
shop=(root/'Assets/MonoBehaviour/Shop.asset').read_text()
for length,segments in ((5,11),(15,26),(48,26),(100,52)):
    prefab=root/f'Assets/Resources/components/LAN Cable ({length}m).prefab'
    product=root/f'Assets/MonoBehaviour/LAN Cable ({length}m).asset'
    assert check_prefab(str(prefab)) == 0
    text=prefab.read_text(); item=product.read_text()
    assert f'  kind: 3\n  length: {length}\n  segments: {segments}\n' in text
    assert '  lanPlugVisualScale: 1\n' in text
    assert len(re.findall(r'propertyPath: m_LocalScale\.[xyz]\n      value: 1\n',text)) == 6
    assert text.count(f'm_SourcePrefab: {{fileID: 100100000, guid: {fbx}, type: 3}}') == 2
    assert f'lanPlugModel: {{fileID: 919132149155446097, guid: {fbx}, type: 3}}' in text
    assert 'MeshRenderer:' not in text and 'MeshFilter:' not in text
    assert f'guid: {mat}, type: 2' in text
    assert f'guid: {icon}, type: 3' in item
    guid=re.search(r'^guid: ([a-f0-9]{32})$',product.with_suffix('.asset.meta').read_text(),re.M).group(1)
    assert shop.count(f'guid: {guid}, type: 2') == 1
assert 'Mathf.Clamp(segments, 7, 64)' in rope
# Cable and plug GameObject roots already have identity scale; only the FBX
# visual overrides (and the runtime fallback) needed normalization.
for cable_path in (root/'Assets/Resources/components').glob('*Cable*.prefab'):
    if not cable_path.name.startswith(('Power Cable','VGA Cable','HDMI Cable','LAN Cable')):
        continue
    yaml=cable_path.read_text()
    for suffix in ('000','010','020'):
        transform=re.search(r'--- !u!4 &400'+suffix+r'\nTransform:.*?(?=--- !u!|\Z)',yaml,re.S)
        assert transform and '  m_LocalScale: {x: 1, y: 1, z: 1}\n' in transform.group(0),cable_path
assert 'go.transform.Find("RJ45 model")' in rope
# The two artist-authored board sockets and seven duplicated variants have
# the BoxCollider, real pivot, sphere trigger and PcLan serialized in YAML.
port_guid=re.search(r'^guid: ([a-f0-9]{32})$',
    (cs/'PC/Component/Cabling/CablePort.cs.meta').read_text(),re.M).group(1)
for board_name in ('ATX','Mini_ITX','ATX 2','Micro_ATX','EATX',
                   'EXATX','MSG_ATX_Black','MSG_ATX_White','x32box'):
    path=root/f'Assets/Resources/components/{board_name}.prefab'
    assert check_prefab(str(path)) == 0
    text=path.read_text()
    assert text.count('  m_Name: LAN\n') == 1 and '  m_Name: Pivot\n' in text
    assert f'guid: {port_guid}, type: 3' in text and '  kind: 9\n' in text
    assert 'SphereCollider:' in text and '  m_IsTrigger: 1' in text
assert 'candidate.name == "LAN"' in board and 'authoredLan.Find("Pivot")' in board
assert 'guid: 25abe6272ecacd641a52d6b37b16b6f9' in c
assert 'LAN Cable ' in mode and 'LAN Cable ' in saver
assert 'router.Indicators?.PulseLan(link.LanPort)' in net
assert 'router.Indicators?.PulseWifi()' in net
assert 'SignalTraffic(system != null ? system.Board : null)' in web
assert 'network?.SignalTraffic(system.Board)' in remote
assert 'network?.SignalTraffic(session.board)' in remote
assert 'service.Plan != 0' in ind and 'router.UpstreamQuality' in ind
assert 'Router/Indicators' in ind and 'PulseWifi' in ind and 'PulseLan' in ind
assert 'child.name == "Indicators"' in ind
assert 'new Material(all[i].sharedMaterial)' in ind
assert 'router.IsPowered' in read('ElectricityGrid.cs')
probe=(root/'Assets/Editor/RouterPowerDiagnostic.cs').read_text()
assert 'Tools/Typo-Link/Diagnose router power' in probe
assert 'port.owner == item' in probe and 'CablePower.Energized(plug)' in probe
print('PASS: authored power/LAN sockets, physical LAN prefab, save modes, traffic LEDs')
