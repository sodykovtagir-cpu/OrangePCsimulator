#!/usr/bin/env python3
"""Static contracts for per-PC default favorites, site icons and hold-to-remove tiles."""
from pathlib import Path
import re
import yaml

root = Path(__file__).resolve().parents[1]
assets = root / "Assets"
browser = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/Browser.cs").read_text()
tile = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/FavoriteTile.cs").read_text()
meta = (assets / "Scripts/Assembly-CSharp/PC/Component/Software/FavoriteTile.cs.meta").read_text()
item = (assets / "GameObject/FavoritesItemPrefab.prefab").read_text()
ui = (assets / "Resources/apps/Browser.prefab").read_text()


def blocks(text):
    result = {}
    for chunk in re.split(r"(?=^--- !u!\d+ &\d+\n)", text, flags=re.M):
        match = re.match(r"--- !u!\d+ &(\d+)\n", chunk)
        if not match:
            continue
        assert match.group(1) not in result, match.group(1)
        yaml.safe_load(re.sub(r"^--- !u!\d+ &\d+", "---", chunk, count=1))
        result[match.group(1)] = chunk
    return result


prefab = blocks(item)
blocks(ui)
assert 'private const string TariffsUrl = "www.typolink.com/tariffs";' in browser
assert 'private const string RouterUrl = "192.168.1.1";' in browser
assert 'favoriteUrls.Add(TariffsUrl);' in browser and 'favoriteUrls.Add(RouterUrl);' in browser
assert browser.index('if (!system.FileManager.TryGetFile(0, FavoritesPath') < browser.index('favoriteUrls.Add(TariffsUrl);')
assert 'if (string.IsNullOrEmpty(saved.content)' in browser
assert 'favoriteUrls.Remove(url)' in browser and 'SaveUrls(FavoritesPath, favoriteUrls)' in browser
assert 'favoriteUrls.AddRange(previous)' in browser
assert 'item.Initialize(url, SiteIcon(url), OpenRecentSite, RemoveFavorite)' in browser
assert 'image.sprite = SiteIcon(url);' in browser  # history icons too
assert 'return fallbackSiteIcon;' in browser
assert 'fallbackSiteIcon: {fileID: 21300000, guid: ccea911e70e5ff549b5d44da8f8688aa, type: 2}' in ui
assert 'favoriteUrls.Count > MaxRecentSites' in browser

script_guid = re.search(r'^guid: ([0-9a-f]{32})$', meta, re.M).group(1)
assert f'm_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}' in prefab['8877010000000000009']
assert '  - component: {fileID: 8877010000000000009}' in prefab['7439697941606578863']
assert '  - {fileID: 8877010000000000002}' in prefab['6107092998949551158']
assert 'm_IsActive: 0' in prefab['8877010000000000001']
assert 'deleteButton: {fileID: 8877010000000000005}' in prefab['8877010000000000009']
assert 'iconImage: {fileID: 3392004610799462980}' in prefab['8877010000000000009']
assert 'm_TargetGraphic: {fileID: 8877010000000000004}' in prefab['8877010000000000005']
assert 'm_RaycastTarget: 0' in prefab['8877010000000000010']
for gesture in ('IPointerDownHandler', 'IPointerUpHandler', 'IPointerExitHandler',
                'IPointerClickHandler', 'PointerInput.LongPress', 'PointerInput.Slop',
                'TryGetHeldPosition(pointerId', 'Time.unscaledTime',
                'suppressClick', 'deleteButton.gameObject.SetActive(true)',
                'deleteButton.onClick.AddListener(Remove);'):
    assert gesture in tile, gesture
for site in ('Tariffs', 'RouterPanel'):
    text = (assets / f'GameObject/{site}.prefab').read_text()
    assert 'icon: {fileID: 21300000, guid: 7868701f29c3d2d4c97bb57c11ef59fd, type: 3}' in text

print('PASS: per-PC first-use favorites, site icons, prefab cross and hold-to-delete wiring')
