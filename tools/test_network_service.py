#!/usr/bin/env python3
"""Source contracts for network integration; not a substitute for Unity Play Mode."""
from pathlib import Path
from math import floor
base=Path(__file__).resolve().parents[1]/'Assets/Scripts/Assembly-CSharp'
def src(x): return (base/x).read_text()
net=src('PC/Component/Networking/InternetService.cs')
router=src('PC/Component/Networking/RouterDevice.cs')
board=src('PC/Component/Motherboard.cs')
browser=src('PC/Component/Software/Browser.cs')
web=src('PC/Component/Software/RouterWebPages.cs')
bind=src('PC/Component/Software/TypoLinkWebsite.cs')
game=src('GameData.cs'); saver=src('SaveManager.cs'); main=src('Main.cs'); grid=src('ElectricityGrid.cs')
assert 'gameObject.AddComponent<PC.Component.Networking.InternetService>()' in main
assert all(f'public {kind} {name};' in game for kind,name in [
    ('int','internetPlan'),('int','internetGatewayId'),('double','internetPendingDollars'),
    ('double','internetTotalMegabytes')])
assert 'InternetService.Instance.Load(Loader.GameData)' in saver
assert 'InternetService.Instance.Save(game)' in saver
assert saver.index('InternetService.Instance.Load(Loader.GameData)') < saver.index('if (string.IsNullOrEmpty(Loader.Content))')
assert 'PricePerGB = { 0, 19, 79, 499 }' in net
assert 'StableHash(url) % 4u == 1u' in net
# At least one of the five existing Browser prefabs is permanently blocked on the cheapest plan.
def fnv(url):
    h=2166136261
    for c in url.lower(): h=((h^ord(c))*16777619)&0xffffffff
    return h
assert fnv("www.freecoins.com") % 4 == 1
assert 'router.PowerWatts' in grid
assert 'mb * RatePerGB / 1024d' in net and 'Math.Floor(cost' in net
assert 'main.SetMoney(main.Money - due, true)' in net
assert 'class TypoLinkWebsite : Website' in bind and 'public void ChooseOgryzok()' in bind
assert 'public void AttachRepeaterWifi()' in bind and 'public void JoinWifi()' in bind
assert 'service.JoinWifi' in web and 'service.JoinLan' in web
assert 'service.SetPlan(id)' in web and 'child.ConnectUpstream(router.Id, true' in web
assert 'child.ConnectUpstream(router.Id, false)' in web
assert 'RouterWebPages.IsLocalAddress(url)' in browser
assert 'LoadOnline(site.url, Mathf.Max(.1f, site.trafficMB)' in browser
assert 'WaitForSecondsRealtime(step)' in browser and 'SignalTraffic(system' in browser
# With `service == null || ...`, short-circuit skips the out parameters.
# CS0165: error must still be definitely assigned on the null-service path.
assert 'float delay = 0f;' in browser and 'string error = null;' in browser
assert 'out delay, out error' in browser and 'out var error' not in browser
assert 'if (site != null && site.page != null && NormalizeUrl(site.url) == url)' in browser
assert 'WifiSignal(board.transform, board.WifiRange)' in net
assert 'service.Router(board.LanRouterId)' not in net # actual local Router() resolution
assert 'router.WithinLanRange(board.transform)' in net
assert 'private float wifiRange = 30f;' in board and 'private float wifiRange = 55f;' in router
assert 'private float maxLanLength = 500f;' in router
assert 'new HashSet<int>()' in net and 'seen.Add(step.Id)' in router
assert 'parent.Password != upstreamPassword' in router
assert 'board.Running' in net and 'ElectricityGrid.PowerAvailable' in net
assert 'routerSsid' in router and 'routerUpstream' in router
assert 'jObject["wifiSsid"]' in board and 'jObject["lanRouterId"]' in board
assert 'jObject.Value<string>("wifiSsid") ?? ""' in board
assert 'data.Value<int?>("routerUpstream") ?? 0' in router
assert '[RequireComponent(typeof(Item))]' in router
# 1 GB = 1024 MB, whole dollars are charged while the rest accumulates.
for price in (19,79,499):
    owed=0
    paid=0
    for _ in range(1024):
        owed+=price/1024
        whole=floor(owed+1e-9)
        paid+=whole
        owed-=whole
    assert paid==price and abs(owed)<1e-6
print('PASS: tariffs, fractional billing, Wi-Fi/LAN, router chains, saves, browser and electricity')
