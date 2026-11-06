#!/usr/bin/env python3
"""Static contracts for the authored Oggle catalog and cable-free networking."""
import yaml
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
assets = root / "Assets"
browser_prefab = (assets / "Resources/apps/Browser.prefab").read_text()
browser_code = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/Browser.cs").read_text()
network = (assets / "Scripts/Assembly-CSharp/PC/Component/Networking/InternetService.cs").read_text()
router = (assets / "Scripts/Assembly-CSharp/PC/Component/Networking/RouterDevice.cs").read_text()
web = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/RouterWebPages.cs").read_text()
tray = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/OS/WifiTaskbarController.cs").read_text()
entry = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/OS/WifiNetworkEntry.cs").read_text()
panel = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/RouterPanelExtras.cs").read_text()
shared_prefab = (assets / "Scripts/Assembly-CSharp/PC/Component/WifiPanelPrefab.prefab").read_text()

catalog = browser_prefab.split("  websites:\n", 1)[1].split("\n  addressBar:", 1)[0]
entries = re.findall(r"(?m)^  - url: ([^\n]+)\n(.*?)(?=^  - url: |\Z)", catalog, re.S | re.M)
assert len(entries) == 8
urls = {url for url, _ in entries}
assert len(urls) == len(entries)
assert {"www.typolink.com/tariffs", "192.168.1.1", "www.dakimakuraexpert.com"} <= urls
for url, body in entries:
    assert "    visible: 1\n" in body, url
    # Unity YAML can wrap quoted keyword strings across lines.
    keywords = yaml.safe_load(body)['keywords']
    assert len(keywords) >= 20 and len(keywords) == len(set(keywords)), url
    assert all(word and word.strip() == word for word in keywords), url
assert "description: Welcome to TypoLink router panel.\n" in dict(entries)["192.168.1.1"]
assert "site.description ?? \"\"" in browser_code
assert "StringComparison.OrdinalIgnoreCase" in browser_code
assert "if (!site.visible) continue;" in browser_code

assert "row.GetComponent<Image>()" in tray and "Destroy(background);" in tray
assert "InitializeRepeater(current, router, network" in panel
# The shared asset keeps its background for the router website; only PCOS strips it.
root_object = re.search(r'^--- !u!1 &8731971552345706499\nGameObject:.*?(?=^--- !u!|\Z)', shared_prefab, re.S | re.M).group(0)
assert "  - component: {fileID: 7449284432648735008}" in root_object
assert "InitializeLan" not in tray and "InitializeLan" not in entry
assert "else if (board.LanRouterId != 0)" not in network
assert "if (!CableMode.Enabled || board == null) return false;" in network
assert "if (!CableMode.Enabled ||" in router
assert "parent.WithinLanRange(transform)" not in router
assert "Подключить #\" + child.Id + \" через LAN" not in web
assert "Нет включённых роутеров для подключения" not in panel

for path in [assets / "GameObject/Tariffs.prefab", assets / "GameObject/RouterPanel.prefab"]:
    text = path.read_text()
    expected = "www.typolink.com/tariffs" if path.name == "Tariffs.prefab" else "192.168.1.1"
    assert f"  websiteName: {expected}\n" in text
for text in (browser_prefab, web, network):
    assert "192.168.1.1/panel" not in text
    assert "typolink.com/tarrifs" not in text
    assert "www.www.typolink.com" not in text
assert 'url == "192.168.1.1" || url == "www.typolink.com/tariffs"' in web
print("PASS: all eight Oggle sites indexed, addresses/keywords and cable-free LAN removal")
