#!/usr/bin/env python3
"""Source-level guards for per-process PCOS admin and the Lua bridge.

These checks cannot replace a Unity compile/Play Mode test.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
base = root / 'Assets/Scripts/Assembly-CSharp'
read = lambda name: (base / name).read_text()
os = read('PC/Component/Software/OS/OperatingSystem.cs')
host = read('PC/Component/Software/Lua/PcosLuaHost.cs')
app = read('PC/Component/Software/App.cs')
menu = read('DesktopContextMenu.cs')
explorer = read('PC/Component/Software/FileManager.cs')
terminal = read('PC/Component/Software/Terminal.cs')
editor = read('PC/Component/Software/LuaEditor.cs')
luaapp = read('PC/Component/Software/LuaApp.cs')
prompt = read('PC/Component/Software/OS/AdministratorPrompt.cs')
registry = read('PC/Component/Software/DeviceManager.cs')

assert 'storage.password' in os and 'string.IsNullOrEmpty(storage.password)' in os
assert 'administratorPrompt.Cancel()' in os and 'SessionToken++' in os
assert 'candidate.IsAdministrator == elevated' in os
assert 'return existing;' in os
assert 'a.SetAdministrator(false)' in os and 'app.SetAdministrator(elevated)' in os
assert 'private bool administrator;' in app and 'system.Ready' in app
assert 'OpenFileAsAdministrator(file)' in menu and '".tmn"' in menu
assert 'ContentType.Password' in prompt and 'Callback' not in prompt
assert 'OnDestroy()' in prompt and 'private void Confirm()' in prompt
assert 'OpenFileInternal(file, true)' in os and 'IsSystemPath(file.path) && !elevated' in os
assert 'OpenFileFromExplorer(file, explorer)' in menu
assert 'runningApps.Contains(explorer)' in os and 'explorer.IsAdministrator' in os
assert 'IsSystemPath(file.path)' in explorer and 'OpenFolderFromPath' in explorer
assert 'IsSystemPath(path) && !IsAdministrator' in luaapp
assert 'PcosLuaHost.Bind(vm, system, this)' in luaapp
assert 'PcosLuaHost.Bind(vm, this, null, administrator)' in os
assert 'system.RunLua(pack.ToJson(), AppendOut)' in editor  # no privilege inheritance
assert 'system.RunLua(file.content, null)' in terminal  # same
assert 'IsSystemPath(path) && !IsAdministrator' in terminal
assert 'name.EndsWith(".tmn", StringComparison.OrdinalIgnoreCase)' in host
assert 'Saved.ids' not in os  # JSON is parsed via its own DTO
for field in ('public List<int> ids', 'public List<int> minerIds', 'public List<int> pcIds'):
    assert field in os and field in registry
assert 'saved.pcIds.Contains(id)' in os and 'saved.minerIds.Contains(id)' in os
assert 'RegisteredPcIds().Contains(id)' in os
assert 'DeviceAutomation.Find(id)' in os and 'DeviceAutomation.Run(this, board, action, argument)' in os
assert 'TryLaunchApp("RemoteDesk", id.ToString("X8"))' in os
assert 'Instantiate(prefab, appParent) as PC.Component.Software.Terminal' in os
assert 'terminal.SetAdministrator(true)' in os
assert 'hasOwner ? owner != null && owner.IsAdministrator : headlessAdministrator' in host
assert 'os.SessionToken == session' in host and 'os.RequestAdministrator(granted =>' in host
assert 'vm.Call(callback' in host and 'owner.SetAdministrator(true)' in host
assert 'storage.password' not in host and 'input.text' not in host
for table, functions in {'admin': ('status', 'request'),
                         'pc': ('ids', 'scan', 'command', 'connect'),
                         'terminal': ('run',),
                         'fs': ('list', 'read', 'write', 'delete', 'exists')}.items():
    for function in functions:
        assert f'{table}.Set(LuaValue.String("{function}")' in host, (table, function)
for operation in ('read', 'write', 'delete', 'exists'):
    piece = re.search(r'fs\.Set\(LuaValue\.String\("' + operation +
                      r'"\).*?\}\)\);', host, re.S)
    assert piece and 'IsSystemPath(path)' in piece.group(), operation
assert 'ListUserFiles(authorized())' in host
for language in ('EN', 'RU'):
    manual = (root / f'Assets/Resources/LuaDocs_{language}.txt').read_text()
    for name in ('admin.status', 'admin.request', 'pc.ids', 'pc.scan',
                 'pc.command', 'pc.connect', 'terminal.run', 'System/'):
        assert name in manual, (language, name)
print('PASS: admin process scope, registry, System/ gates, Lua API, EN/RU docs')
