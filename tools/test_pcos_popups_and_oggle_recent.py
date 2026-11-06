#!/usr/bin/env python3
"""Static safeguards for dismissible PCOS menus and per-PC Oggle recents."""
from pathlib import Path

root = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
def read(relative): return (root / relative).read_text()
chrome = read('WindowChrome.cs')
tray = read('OS/WifiTaskbarController.cs')
os = read('OS/OperatingSystem.cs')
browser = read('Browser.cs')
web = read('RouterWebPages.cs')

assert 'ClickedOutside(RectTransform panel, RectTransform trigger)' in chrome
assert 'TouchPhase.Began' in chrome and 'Input.GetMouseButtonDown(0)' in chrome
assert 'RectangleContainsScreenPoint(panel, point, camera)' in chrome
assert 'RectangleContainsScreenPoint(trigger, point, camera)' in chrome
assert 'panel.gameObject.SetActive(false);' in tray
assert 'WindowChrome.ClickedOutside(panel,' in tray
assert 'WindowChrome.ClickedOutside(' in os and 'ToggleStartMenu();' in os
assert 'startMenuOpened && ShellRunning' in os
assert 'System/oggle_recent.json' in browser and 'System/oggle_favorites.json' in browser
assert 'system.FileManager.TryGetFile(0, RecentSitesPath' in browser
assert 'system.FileManager.TryGetFile(0, FavoritesPath' in browser
assert 'system.FileManager.Write(0, path, payload)' in browser
assert 'saved.hidden = true;' in browser
assert 'MaxRecentSites = 5;' in browser
assert 'recentUrls.RemoveRange(MaxRecentSites' in browser
assert 'favoriteUrls.RemoveRange(MaxRecentSites' in browser
assert 'RememberSite(site.url);' in browser and 'RememberSite(url);' in browser
assert 'var url = NormalizeUrl(favoriteAddressInput.text);' in browser
assert 'if (!ValidSiteAddress(url))' in browser
assert 'OpenRecentSite(capturedUrl)' in browser and 'пока недоступен в Oggle' in browser
assert 'new List<string>(favoriteUrls)' in browser and 'favoriteUrls.AddRange(previous)' in browser
assert 'favoriteAddressInput.ActivateInputField();' in browser
assert 'Instantiate(historyItemPrefab, historyContent)' in browser
assert 'Instantiate(favoritesItemPrefab, favoritesPanel)' in browser
assert 'row.AddComponent<Button>()' in browser
assert 'RenderRecentSites();' in browser and 'RenderFavorites();' in browser
assert 'oggleHome.SetActive(true);' in browser
assert 'Add site overlay' not in browser and 'Recent sites' not in browser

# The authored buttons and fields must actually be wired in the prefab.
import re
prefab = (root.parents[4] / 'Resources/apps/Browser.prefab').read_text()
for key, file_id in {
    'historyContent': '4329434395847748119',
    'favoritesPanel': '6510275321744835450',
    'addFavoritesPanel': '4975728224049382311',
    'favoriteAddressInput': '5843212555252329505',
    'addFavoritesTitle': '1413617616655323745',
}.items():
    assert f'{key}: {{fileID: {file_id}}}' in prefab, key
for method, component in {
    'OpenAddSiteDialog': '3274792720803512694',
    'AddFavorite': '377992477638285846',
    'CloseAddSiteDialog': '5245358844254449684',
}.items():
    block = re.search(rf'--- !u!114 &{component}\n(.*?)(?=--- !u!|\Z)', prefab, re.S)
    assert block and f'm_MethodName: {method}' in block.group(1)
    assert 'm_Target: {fileID: 114482999165124040}' in block.group(1)
for name, guid in {
    'historyItemPrefab': 'df583bcea98a2c14f9236f41c733d80c',
    'favoritesItemPrefab': '6288bce4648b83d4792c37f92991c766',
}.items():
    assert re.search(rf'{name}: {{fileID: \d+, guid: {guid}, type: 3}}', prefab)
assert 'root.GetComponent<Image>().color = Color.white;' in web
assert 'background.color = Color.white;' in web
assert 'Label(body, text, 21).color = Color.black;' in web
print('PASS: outside-click menus, per-PC history/favorites, authored Oggle UI and white messages')
