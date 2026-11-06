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
assert 'System/oggle_recent.json' in browser
assert 'system.FileManager.TryGetFile(0, RecentSitesPath' in browser
assert 'system.FileManager.Write(0, RecentSitesPath, payload)' in browser
assert 'saved.hidden = true;' in browser
assert 'MaxRecentSites = 5;' in browser
assert 'recentUrls.RemoveRange(MaxRecentSites' in browser
assert 'RememberSite(site.url);' in browser and 'RememberSite(url);' in browser
assert 'var url = NormalizeUrl(input.text);' in browser
assert 'if (!ValidSiteAddress(url))' in browser
assert 'OpenRecentSite(url)' in browser and 'пока недоступен в Oggle' in browser
assert 'new List<string>(recentUrls)' in browser and 'recentUrls.AddRange(previous)' in browser
assert 'WindowChrome.Apply(modal);' in browser and 'input.ActivateInputField();' in browser
assert 'RenderRecentSites();' in browser and 'oggleHome.SetActive(true);' in browser
assert 'root.GetComponent<Image>().color = Color.white;' in web
assert 'background.color = Color.white;' in web
assert 'Label(body, text, 21).color = Color.black;' in web
print('PASS: outside-click menus, per-PC saved five-site history, modal and white message pages')
