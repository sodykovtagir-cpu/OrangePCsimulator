#!/usr/bin/env python3
"""Unity-source guards for the device manager, scripts, and staged BSOD.

Does not replace a Unity editor compile or an in-game boot/login/USB smoke test.
"""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]
code = root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
read = lambda path: (root / path).read_text()
os = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/OperatingSystem.cs')
config = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/AutorunConfig.cs')
manager = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/DeviceManager.cs')
terminal = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/Terminal.cs')
miner = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/Miner.cs')
automation = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/DeviceAutomation.cs')
assert automation.count('global::PC.Component.Software.OS.OperatingSystem') == 6
assert 'using OperatingSystem' not in automation and 'using System;' not in automation
editor = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/TextEditor.cs')
installer = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/Installer.cs')
ready = read('Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs')
old_my_devices = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/MyDevices.cs')
prefab = read('Assets/Resources/apps/DeviceManager.prefab')
assert 'appName: Device Manager' in prefab and 'appName: My Devices' not in prefab
assert 'guid: f47a1261e8294beaaa969284b739411c' in prefab
assert 'm_SizeDelta: {x: 620, y: 540}' in prefab
assert "EnsureDeviceManager();" in os and '"Device Manager.exe"' in os
assert 'sys.RequireDevices()' in old_my_devices and 'system.RequireDevices()' in manager
assert all(x in config for x in ('System/Autorun.conf', 'System/autorun.initialized',
    '//Initialize system DO NOT REMOVE!!!!', 'boot boot.bin\\ninit devices\\ninit user\\n',
    'HasLine', 'UserCommands', 'AppendCommand'))
assert 'AutorunConfig.Ensure(FileManager)' in os
assert os.index('Boot not specified') < os.index('if (user != null) user.SetActive(true);') < os.index('"init user"')
assert 'FreezeThenFault()' in os and 'WaitForSecondsRealtime(1.3f)' in os
assert 'if (hardware is Storage) RequireDevices()' in os
assert 'OnHardwareAdded(hardware)' in read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/ComputerSystem.cs')
assert all(x in installer for x in ('AutorunConfig.Path', 'AutorunConfig.Default', 'AutorunConfig.Marker'))
assert all(x in ready for x in ('AutorunConfig.Path', 'AutorunConfig.Default', 'AutorunConfig.Marker'))
# .conf, default Text Editor, and explicit Open With all bind the document.
assert '".conf"' in os and os.count('editor.OpenDocument(file)') >= 2
assert 'disks[n].files.Contains(openedFile)' in editor
assert 'openedFile != null ? filePath : null, disk, saved =>' in editor
assert 'openedFile = saved;' in editor and 'filePath = saved.path;' in editor
assert '"bitcoin"' in miner and 'ChangeMode(bitcoinMode)' in miner and 'public override void Open(string content)' in miner
for required in ('RunAll', 'FindObjectsOfType<Motherboard>()', 'board.Boot()',
                 'source.StartCoroutine(WhenReady', 'os.TryLaunchApp("EZ Mining", action)',
                 'os.TryLaunchApp("Video Player")'):
    assert required in automation, required
for required in ('remote-all', 'cmd == "launch"', 'cmd == "open" || cmd == "run"',
                 'scriptDepth >= 4', 'DeviceAutomation.Run(this.system, mb, next, argument)',
                 'os.SystemId == id', 'system.RunLua(file.content, null)'):
    assert required in terminal, required
# Layout and controls must be real, editable objects in the Unity prefab.
from test_localization import docs, references
objects = docs(root / 'Assets/Resources/apps/DeviceManager.prefab')
by_name = {v['m_Name']: (fid, v) for fid, (typ, v) in objects.items() if typ == 1}
manager_component = next(v for typ, v in objects.values()
    if typ == 114 and v.get('m_Script', {}).get('guid') ==
    'f47a1261e8294beaaa969284b739411c')
required_fields = ('body', 'homePage', 'targetPage', 'actionPage', 'exportPage',
    'homeAll', 'homePCs', 'homeMiners', 'selectVisibleButton', 'homeScroll',
    'homeRows', 'homeStatus', 'targetAll', 'targetPCs', 'targetMiners',
    'targetSelectVisible', 'targetRows', 'targetCount', 'actionButtons',
    'worldToggle', 'argumentInput', 'autorun', 'scriptNameInput', 'rowTemplate')
for field in required_fields:
    value = manager_component[field]
    links = value if isinstance(value, list) else [value]
    assert links and all(link['fileID'] in objects for link in links), field
assert len(manager_component['actionButtons']) == 6
for field in ('homeAll','homePCs','homeMiners','buildButton','selectVisibleButton',
              'discoverButton','removeButton','targetSelectVisible','targetBack',
              'actionBack','exportButton'):
    assert objects[manager_component[field]['fileID']][1]['m_Script']['guid'] == '4e29b1a8efbd4b44bb3f3716e73f07ff', field
for field in ('idInput','argumentInput','scriptNameInput'):
    assert objects[manager_component[field]['fileID']][1]['m_Script']['guid'] == 'd199490a83bb2b844b9695cbf13b01ef', field
for field in ('worldToggle','autorun'):
    assert objects[manager_component[field]['fileID']][1]['m_Script']['guid'] == '9085046f02f69544eb97fd06b6048fe2', field
assert objects[manager_component['body']['fileID']][0] == 224
assert all(by_name[page][1]['m_IsActive'] == 0 for page in ('Targets','Action','Export','Row Template — edit for all list entries'))
assert by_name['Home'][1]['m_IsActive'] == 1
assert 'Scroll View' not in by_name and 'Refresh' not in by_name  # retired wireless UI
for fid, (typ, value) in objects.items():
    # No two Graphic components (Text/Image) on the same GameObject.
    if typ != 1: continue
    scripts = [objects[c['component']['fileID']][1].get('m_Script', {}).get('guid')
               for c in value['m_Component'] if objects[c['component']['fileID']][0] == 114]
    assert not {'fe87c0e1cc204ed48ad3b37840f39efc',
                '5f7201a12d95ffc409449d95f23cf332'} <= set(scripts), value['m_Name']
for typ, value in objects.values():
    for link in references(value):
        if link.get('fileID', 0) != 0 and not link.get('guid'):
            assert link['fileID'] in objects, link
assert 'm_Name: Manager UI' in prefab and 'm_Name: Row Template' in prefab
assert 'All: Bitcoin' not in prefab and 'All: Money' not in prefab and 'All: Video' not in prefab
# Lists pin their content to the top; changing content height cannot jump its first row.
for name in ('Content for Home list', 'Content for Targets list'):
    content = by_name[name][1]
    rt = objects[content['m_Component'][0]['component']['fileID']][1]
    assert rt['m_Pivot']['y'] == 1 and rt['m_AnchoredPosition']['y'] == 0
home_children = objects[manager_component['homePage']['fileID']][1]['m_Component'][0]['component']['fileID']
home_rect = objects[home_children][1]
home_names = {objects[objects[c['fileID']][1]['m_GameObject']['fileID']][1]['m_Name']
              for c in home_rect['m_Children']}
assert {'buildButton', 'selectVisibleButton', 'discoverButton', 'removeButton', 'refreshButton'} <= home_names
assert all('EZ:' not in name and 'Power on' not in name for name in home_names)
row_component = objects[manager_component['rowTemplate']['fileID']][1]
assert row_component['m_Script']['guid'] == 'a6797268eb4f4cfdaf3af7584db313e0'
assert all(row_component[field]['fileID'] in objects for field in ('background', 'label'))
assert 'Button(' not in manager and 'NewRect(' not in manager and 'MakeList(' not in manager
assert 'Instantiate(rowTemplate, parent)' in manager and 'selection.Add(id)' in manager
assert 'if (Visible(id, DeviceAutomation.Find(id)))' in manager
assert 'DeviceAutomation.RunAll(system, what)' in manager
assert 'body == null || rowTemplate == null' in manager
assert 'remote-all ' in manager and 'AutorunConfig.AppendCommand' in manager
row = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/DeviceManagerRow.cs')
pane = read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/DeviceManagerPane.cs')
menu = read('Assets/Scripts/Assembly-CSharp/DesktopContextMenu.cs')
assert 'IPointerClickHandler' in row and 'PointerInput.LongPress' in row
assert 'class DeviceManagerPane' in pane and 'PointerInput.LongPress' in pane
assert 'ShowDeviceManagerMenu' in menu and 'AddMenuItemWithSubmenu(menuPanel, "All suitable devices"' in menu
for action_name in ('startup','bitcoin','cash','video','shutdown'):
    assert 'manager.RunSelected("'+action_name+'")' in menu
assert all('manager.RunGlobal("'+x+'")' in menu for x in ('bitcoin','cash','video'))
# World actions never depend on registry, visible tab or current selection.
run_all = automation.split('public static int RunAll(')[1].split('public static bool Run(')[0]
assert 'foreach (var board in All())' in run_all and 'Supports(board, app)' in run_all
assert 'filter' not in manager.split('public void RunGlobal(')[1].split('private void Export(')[0]
kind = automation.split('public static bool IsMiner(')[1].split('public static bool Supports(')[0]
assert 'GetComponentInParent<Case>()' in kind and 'HardwareType.GPU' in kind
assert '"Miner"' in kind and '"BigMiner"' in kind and 'board.name' not in kind
case_guid = re.search(r'^guid: ([0-9a-f]{32})$',
    (root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Case.cs.meta').read_text(), re.M).group(1)
for chassis in ('Miner', 'BigMiner'):
    assert case_guid in read(f'Assets/Resources/components/{chassis}.prefab')
# No prefab link ambiguity: the cloned chrome has a unique script GUID; originals unchanged.
assert 'appName: My Devices' in read('Assets/Resources/apps/MyDevices.prefab')
for new in ('DeviceManager.cs', 'DeviceManagerRow.cs', 'DeviceManagerPane.cs', 'DeviceAutomation.cs', 'OS/AutorunConfig.cs'):
    source = code / new
    guid = re.search(r'^guid: ([0-9a-f]{32})$', source.with_suffix('.cs.meta').read_text(), re.M).group(1)
    assert len(guid) == 32
    if source.name == 'DeviceManager.cs':
        assert guid in prefab
print('PASS: prefab-editable Device Manager, multiselection/context actions, world presets, .tmn/autorun')
