#!/usr/bin/env python3
"""Source-level regression guards for PCOS's shared file picker and associations."""
from pathlib import Path

root = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
picker = (root / 'PC/Component/Software/OS/OpenDialog.cs').read_text()
os = (root / 'PC/Component/Software/OS/OperatingSystem.cs').read_text()
viewer = (root / 'PC/Component/Software/Viewer.cs').read_text()
list_view = (root / 'ListView.cs').read_text()
menu = (root / 'DesktopContextMenu.cs').read_text()
remote = (root / 'PC/Component/Software/RemoteDesk.cs').read_text()

assert 'currentFolder = "";' in picker
assert 'f.isFolder != (pass == 0)' in picker
assert 'parent, currentFolder' in picker
assert 'entries.Add(null)' in picker and 'Back (' in picker
assert 'if (Enter(index)) return;' in picker
assert 'if (picked == null || picked.isFolder) return;' in picker
assert 'string.Equals(ext, filter, StringComparison.OrdinalIgnoreCase)' in picker
assert 'private int selectedIndex = -1' in list_view
assert 'e.gameObject.SetActive(false)' in list_view
assert 'ViewerSupports(ext) && IsAppInstalled("Viewer")' in os
assert 'installed != null && installed.CanOpenExtension(ext)' in os
assert '!IsAppInstalled(name)' in os
assert 'IsTextLikeExtension(ext) && IsAppInstalled("Text Editor")' in os
assert 'editor.OpenDocument(file)' in os
for ext in ('.pic', '.mov', '.png', '.jpg', '.jpeg'):
    assert 'ext.Equals("' + ext + '"' in viewer, ext
assert 'public override bool CanOpenExtension' in viewer
assert 'RemoteDesk.IsControlled(operatingSystem)' in menu
assert 'CloseIfOutside(GameObject hit)' in menu
assert 'remoteMenu.CloseIfOutside(hit)' in remote
# A transferred known .exe must be registered immediately, not demand its own
# installation until the next OS boot. Packaged Lua executables remain separate.
assert 'private void RegisterInstalledExecutables()' in os
assert 'file.path.IndexOf(\'/\') >= 0' in os
assert 'LuaAppPackage.IsPackage(file.content)) continue' in os
assert 'appPrefabs.ContainsKey(name) && !installedApps.Contains(name)' in os
assert 'RegisterInstalledExecutables();\n            RefreshDesktopIcon();' in os
assert 'if (!IsAppInstalled(name)) RegisterInstalledExecutables();' in os
assert 'if (!installedApps.Contains(name)) installedApps.Add(name);' in os
print('PASS: navigable picker, file associations, Viewer defaults, transferred apps and remote context menu')
print('NOTE: Unity Play Mode required for dialog/navigation and pointer UI verification')
