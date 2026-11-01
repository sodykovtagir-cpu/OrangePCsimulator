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
assert 'CablePower.Energized(powerPort.plug)' in router
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
assert 'guid: 25abe6272ecacd641a52d6b37b16b6f9' in c
assert 'LAN Cable ' in mode and 'LAN Cable ' in saver
assert 'router.Indicators?.PulseLan(link.LanPort)' in net
assert 'router.Indicators?.PulseWifi()' in net
assert 'SignalTraffic(system != null ? system.Board : null)' in web
assert 'network?.SignalTraffic(system.Board)' in remote
assert 'network?.SignalTraffic(session.board)' in remote
assert 'service.Plan != 0' in ind and 'router.UpstreamQuality' in ind
assert 'Router/Indicators' in ind and 'PulseWifi' in ind and 'PulseLan' in ind
assert 'new Material(all[i].sharedMaterial)' in ind
assert 'router.IsPowered' in read('ElectricityGrid.cs')
print('PASS: authored power/LAN sockets, physical LAN prefab, save modes, traffic LEDs')
