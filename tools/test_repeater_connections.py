#!/usr/bin/env python3
"""Static repeater/cable contracts; Play Mode is not available here."""
from pathlib import Path
root = Path(__file__).resolve().parents[1]
def source(name):
    return (root / 'Assets/Scripts/Assembly-CSharp/PC/Component' / name).read_text()
router = source('Networking/RouterDevice.cs')
network = source('Networking/InternetService.cs')
entry = source('Software/OS/WifiNetworkEntry.cs')
tray = source('Software/OS/WifiTaskbarController.cs')
panel = source('Software/RouterPanelExtras.cs')
website = source('Software/TypoLinkWebsite.cs')
fallback = source('Software/RouterWebPages.cs')
power = source('Cabling/CablePower.cs')
plug = source('Cabling/CablePlug.cs')
saver = (root/'Assets/Scripts/Assembly-CSharp/SaveManager.cs').read_text()
prefab = (root/'Assets/GameObject/RouterPanel.prefab').read_text()
# Only the real cable/power event, after endpoint assignment, or the final
# restore refresh evaluates network topology. No per-frame router scan.
assert plug.index('Connected = port;') < plug.index('if (!restoring) CablePower.RefreshAll();')
assert plug.index('Connected = null;') < plug.index('CablePower.RefreshAll();', plug.index('public void Unplug()'))
assert 'RouterDevice.RefreshAutoLanLinks();' in power
assert saver.index('RestoringSave = false;', saver.index('finally', saver.index('RestorePorts()') if 'RestorePorts()' in saver else 0)) < saver.rindex('CablePower.RefreshAll();')
assert 'if (!CableMode.Enabled || SaveManager.RestoringSave) return;' in router
assert 'var gateway = service.Router(service.GatewayId);' in router
assert 'if (onlyCandidate != null) return null;' in network
assert 'if (Time.unscaledTime < nextGatewayScan) return null;' in network
assert 'if (network.GatewayId == 0 && current.UpstreamId == 0)' in panel
assert 'network.SetGateway(current.Id)' in panel
assert 'RouterDevice.RefreshAutoLanLinks();' in network
assert 'if (gateway == null) return;' in router
assert 'else if (IsAncestor(a, b, service))' in router
assert 'else if (IsAncestor(b, a, service))' in router
assert 'if (aReachable && !bReachable)' in router and 'else if (bReachable && !aReachable)' in router
assert 'a.Id >= b.Id' in router and 'child.ConnectUpstream(parent.Id, false)' in router
assert 'for (int pass = 0; pass < routers.Length; pass++)' in router
assert 'if (!changed) break;' in router
assert 'parent.UpstreamQuality(service, new HashSet<int>()) <= 0f' in router
assert 'child.DisconnectUpstream();' in router and '!child.PhysicalLanTo(parent, out _, out _)' in router
assert 'seen.Add(current.Id)' in router and 'if (!seen.Add(step.Id)) return false' in router
assert 'private void Update()' not in router
# Both lists share the same prefab, but each has only one password form open.
assert 'selectedEntry.CancelPasswordEntry();' in tray and 'selectedEntry.CancelPasswordEntry();' in panel
assert 'row.Initialize(board, router, service, SelectEntry);' in tray
assert 'row.InitializeRepeater(current, router, network' in panel
assert 'repeaterRowPrefab: {fileID: 1231231231231231231, guid: 0995d0db164924d46baa0f4f4978ec98' in prefab
assert 'public void CancelPasswordEntry()' in entry and 'passwordPanel.SetActive(false)' in entry
assert 'repeaterParent.CredentialsMatch(repeaterParent.Ssid, key)' in entry
assert 'repeaterTarget.ConnectUpstream(repeaterParent.Id, true, key)' in entry
assert 'parent.Password)' not in website and 'router.Password)' not in fallback
assert 'parentKey.contentType = InputField.ContentType.Password' in fallback
assert 'if (!PC.Component.Cabling.CableMode.Enabled)' in fallback
assert 'if (!child.UpstreamWireless)' in panel
print('PASS: one password form, shared prefab, typed repeater key, physical LAN auto/restore/cycle guards')
