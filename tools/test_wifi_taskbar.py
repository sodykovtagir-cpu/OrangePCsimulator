#!/usr/bin/env python3
"""Contracts for PCOS Wi-Fi tray and the user's authored prefabs (not Play Mode)."""
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
def read(p):return (root/p).read_text()
board=read('Assets/Scripts/Assembly-CSharp/PC/Component/Motherboard.cs')
net=read('Assets/Scripts/Assembly-CSharp/PC/Component/Networking/InternetService.cs')
grid=read('Assets/Scripts/Assembly-CSharp/ElectricityGrid.cs')
os=read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/OperatingSystem.cs')
tray=read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/WifiTaskbarController.cs')
entry=read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/WifiNetworkEntry.cs')
routerpanel=read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/RouterPanelExtras.cs')
web=read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/RouterWebPages.cs')
assert 'jObject["wifiEnabled"] = wifiEnabled' in board
assert 'jObject.Value<bool?>("wifiEnabled") ?? true' in board
assert 'board.WifiEnabled &&' in net and '!board.WifiEnabled' in net
assert 'hardware is PC.Component.Motherboard board && board.Running && board.WifiEnabled' in grid
assert 'watts += 2f' in grid and 'Board.WifiEnabled' in os
assert 'WifiNetworkEntry wifiNetworkPrefab' in os and 'AddComponent<WifiTaskbarController>' in os
assert 'board.SetWifiEnabled' in tray and 'service.JoinLan' in entry
assert 'new GameObject' not in tray and 'ActionButton(' not in tray
assert 'InitializeLan' in entry and 'Instantiate(rowPrefab, list)' in tray
assert 'CableMode.Enabled' in tray and 'router.WifiSignal' in tray
assert 'router.CredentialsMatch(router.Ssid, password.text)' in entry
assert 'passwordMessage.text = "Неверный пароль"' in entry
assert 'description.text = connected ? "Connected"' in entry
assert 'connectLabel.text = connected ? "Disconnect" : "Connect"' in entry
assert 'ssid.gameObject.SetActive(!enteringPassword)' in entry
assert 'icon.SetActive(!enteringPassword && !isLan)' in entry
assert 'description.gameObject.SetActive(!enteringPassword)' in entry
assert 'url == "wifi-settings"' not in web and 'url == "wifi"' not in web
assert 'scanButton.onClick.AddListener(Scan)' in routerpanel
assert 'InitializeRepeater(current, router, network' in routerpanel
assert 'entry => SelectEntry(entry, router.Id)' in routerpanel
assert 'SelectEntry)' in tray and 'selectedEntry.CancelPasswordEntry()' in tray
assert 'repeaterRowPrefab: {fileID: 1231231231231231231, guid: 0995d0db164924d46baa0f4f4978ec98' in read('Assets/GameObject/RouterPanel.prefab')
# YAML fileIDs and references must point at each other (no scene object refs).
wifipref=read('Assets/Scripts/Assembly-CSharp/PC/Component/WifiPanelPrefab.prefab')
pcpref=read('Assets/GameObject/PCOS.prefab')
routerpref=read('Assets/GameObject/RouterPanel.prefab')
for prefab in (wifipref,pcpref,routerpref):
    ids=re.findall(r'^--- !u!\d+ &(\d+)',prefab,re.M)
    assert len(ids)==len(set(ids))
assert 'wifiNetworkPrefab: {fileID: 1231231231231231231, guid: 0995d0db164924d46baa0f4f4978ec98' in pcpref
for name in ('Wi-Fi Button','Wi-Fi Menu','Networks Scroll View','Wi-Fi Module','Scan'):
    assert '  m_Name: '+name+'\n' in pcpref
assert '  m_Name: Wi-Fi Menu\n' in pcpref and '  m_IsActive: 0' in pcpref
assert 'wifiTrayIconSprite:' not in pcpref
for ref in ('wifiTrayButton','wifiPanel','wifiScanButton','wifiPowerButton',
            'wifiList','wifiStatus','wifiPowerLabel','wifiTrayIcon'):
    assert re.search(r'^  '+ref+r': \{fileID: (?!0\})\d+\}',pcpref,re.M),ref
assert 'Нет включённых роутеров для подключения' in routerpanel
assert 'text.color = Color.white' in routerpanel and '.3372549f' in routerpanel
assert '1231231231231231231' in wifipref
assert '1231231231231231232' in routerpref
assert 'm_MethodName: ApplyWifiSettings' in routerpref
for script,pref in [('WifiNetworkEntry',wifipref),('RouterPanelExtras',routerpref)]:
    meta=next((root/'Assets/Scripts/Assembly-CSharp').rglob(script+'.cs.meta'))
    guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M).group(1)
    assert 'guid: '+guid in pref
print('PASS: Wi-Fi tray, prefab links, two-stage password, LAN and 2 W save migration')
