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
    '//Initialize system DO NOT REMOVE!!!!', 'run boot.bin\\ninit devices\\ninit user\\n',
    'HasLine', 'UserCommands', 'AppendCommand'))
assert 'AutorunConfig.Ensure(FileManager)' in os
assert os.index('"run boot.bin"') < os.index('"init user"') < os.index('if (user != null) user.SetActive(true);')
assert 'FreezeThenFault()' in os and 'WaitForSecondsRealtime(1.3f)' in os
assert 'if (hardware is Storage) RequireDevices()' in os
assert 'OnHardwareAdded(hardware)' in read('Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/ComputerSystem.cs')
assert all(x in installer for x in ('AutorunConfig.Path', 'AutorunConfig.Default', 'AutorunConfig.Marker'))
assert all(x in ready for x in ('AutorunConfig.Path', 'AutorunConfig.Default', 'AutorunConfig.Marker'))
assert '".conf"' in os and os.count('editor.OpenDocument(file)') == 2
assert 'disks[index].files.Contains(openedFile)' in editor and 'FileManager.Write(disk, filePath, content)' in editor
assert '"bitcoin"' in miner and 'ChangeMode(bitcoinMode)' in miner and 'public override void Open(string content)' in miner
for required in ('RunAll', 'FindObjectsOfType<Motherboard>()', 'board.Boot()',
                 'source.StartCoroutine(WhenReady', 'os.TryLaunchApp("EZ Mining", action)',
                 'os.TryLaunchApp("Video Player")'):
    assert required in automation, required
for required in ('remote-all', 'cmd == "launch"', 'cmd == "open" || cmd == "run"',
                 'scriptDepth >= 4', 'DeviceAutomation.Run(this.system, mb, next, argument)',
                 'os.SystemId == id', 'system.RunLua(file.content, null)'):
    assert required in terminal, required
for required in ('Build script  1/3', 'Build script  2/3', 'Build script  3/3',
                 'System ID (hex)', 'Saved', 'DeviceAutomation.All()', 'All: Bitcoin',
                 'All: Money', 'All: Video', 'remote-all ', '".tmn"', 'AutorunConfig.AppendCommand'):
    assert required in manager, required
# No prefab link ambiguity: the cloned chrome has a unique script GUID; originals unchanged.
assert 'appName: My Devices' in read('Assets/Resources/apps/MyDevices.prefab')
for new in ('DeviceManager.cs', 'DeviceAutomation.cs', 'OS/AutorunConfig.cs'):
    source = code / new
    guid = re.search(r'^guid: ([0-9a-f]{32})$', source.with_suffix('.cs.meta').read_text(), re.M).group(1)
    assert len(guid) == 32
    if source.name == 'DeviceManager.cs':
        assert guid in prefab
print('PASS: separate device manager, world presets, .tmn export/autorun, EZ mining args, staged BSOD')
