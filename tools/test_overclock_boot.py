#!/usr/bin/env python3
"""Source and image guards; Unity Editor / Play Mode are still required for integration smoke tests."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
base = root / 'Assets/Scripts/Assembly-CSharp'
read = lambda path: (base / path).read_text()
boot = read('PC/Component/Software/OS/OverclockBoot.cs')
os = read('PC/Component/Software/OS/OperatingSystem.cs')
config = read('PC/Component/Software/OS/AutorunConfig.cs')
board = read('PC/Component/Motherboard.cs')
app = read('PC/Component/Software/Overclock.cs')
manager = read('PC/Component/Software/DeviceManager.cs')
menu = read('DesktopContextMenu.cs')
gpu = read('PC/Component/GPU.cs')
benchmark = read('PC/Component/Software/Benchmark.cs')
three = read('PC/Component/Software/ThreeDMork.cs')

assert 'System/overclockerd-boot.bin' in boot
assert 'run overclockerd-boot.bin' in boot
assert '"//run boot.bin"' in boot
assert '"run boot.bin"' in boot
assert 'fm.Write(0, "System/boot.bin"' not in boot and 'fm.Delete(0, "System/boot.bin"' not in boot  # never modify BIOS file
assert 'Save(FileManager fm' in boot and 'Recover(Storage disk)' in boot
assert 'fm.Write(0, AutorunConfig.Path, updated)' in boot
assert 'fm.TryGetFile(0, Path, out onDisk)' in boot
assert 'disk.Write(AutorunConfig.Path, body)' in boot
assert 'script.hidden = true' not in boot or 'onDisk.hidden = true' in boot
assert '1.55f' in boot and '1.85f' in boot
assert 'gpu.Damage()' in boot and 'item.Damage()' in boot
assert 'OverclockBoot.TryRead(FileManager, out profile)' in os
assert 'OverclockBoot.Unstable(profile)' in os
assert 'OverclockBoot.Fatal(profile)' in os
assert 'OverclockBoot.BreakExtreme(Board, profile)' in os
assert 'OverclockBoot.Apply(Board, profile)' in os
assert 'AutorunConfig.HasLine(autorun, OverclockBoot.Comment)' in os  # remove only bad line
assert 'line == OverclockBoot.Command' in config  # not passed to Terminal as user command
assert 'ScheduleOverclockReboot()' in board and 'WaitForSecondsRealtime(3f)' in board
assert 'OverclockBoot.Apply(this, null)' in board
assert 'OverclockRatio' in gpu and 'ArtifactStrength' in gpu
assert 'GpuMultiplier(b)' in benchmark and 'RamMultiplier(b)' in benchmark
assert 'GpuMultiplier(board)' in three and 'RamMultiplier(board)' in three
assert 'GpuMultiplier(board)' in read('PC/Component/Software/Miner.cs')
assert 'Time.realtimeSinceStartup - startedAt < 30f' in app
assert 'seconds remaining (real time)' not in app
assert 'Test optional' not in app
assert 'Save settings' in (root / 'Assets/Resources/apps/Overclock.prefab').read_text()
assert 'Test 30 seconds' in (root / 'Assets/Resources/apps/Overclock.prefab').read_text()
assert 'Test optional' not in (root / 'Assets/Resources/apps/Overclock.prefab').read_text()
assert 'OverclockBoot.ResetToStock(system.FileManager)' in app
assert 'fm.Delete(0, Path)' in boot
assert 'OverclockBoot.Recover(disk)' in os
assert 'saveButton.onClick.AddListener(SaveProfile)' in app
assert 'OverclockBoot.Save(system.FileManager, profile)' in app
assert 'OverclockBoot.Recover(disk)' in manager
assert 'Repair overclock boot (selected PCs)' in menu
assert 'board == system.Board' in manager  # another PC

# The 3DMork import may be the maintainer's PNG or the fallback TGA;
# require that its prefab points to a real Unity-imported sprite instead of
# encoding an outdated assumption about which import fix they selected.
import re
prefab = (root / 'Assets/Resources/apps/3DMork.prefab').read_text()
guid = re.search(r'(?m)^  icon: \{fileID: 21300000, guid: ([0-9a-f]{32}), type: [23]\}', prefab)
assert guid
meta = next((p for p in (root / 'Assets').rglob('*.meta')
             if p.name.lower().startswith('3dmork') and
             re.search(r'(?m)^guid: ' + guid.group(1) + '$', p.read_text(errors='replace'))), None)
assert meta is not None
icon = meta.with_suffix('')
assert icon.exists()
with Image.open(icon) as image:
    image.verify()
print('Overclock guards and 3DMork icon reference guards: OK')
