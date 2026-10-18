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
assert 'System/boot.bin' not in boot.replace('System/boot.bin = pcos', '')  # never write BIOS file
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
assert 'Time.realtimeSinceStartup - startedAt < 60f' in app
assert 'Save without test' in app and 'OverclockBoot.Save(system.FileManager, profile)' in app
assert 'OverclockBoot.Recover(disk)' in manager
assert 'Repair overclock boot (selected PCs)' in menu
assert 'board == system.Board' in manager  # another PC

# Previous lossless PNG repack kept its GUID and did not solve Unity's lazy
# asset-loading error from Image.sprite. Avoid that specific asset/decoder:
# a new RGBA TGA sprite, a new GUID, and no remaining old PNG reference.
icon = root / 'Assets/Texture2D/3DMorkIcon.tga'
assert icon.exists() and not (icon.parent / '3DMork.png').exists()
with Image.open(icon) as image:
    image.verify()
with Image.open(icon) as image:
    assert image.size == (512, 512) and image.mode == 'RGBA'
meta = icon.with_suffix('.tga.meta').read_text()
prefab = (root / 'Assets/Resources/apps/3DMork.prefab').read_text()
import re
guid = re.search(r'(?m)^guid: ([0-9a-f]{32})$', meta).group(1)
assert guid != '96035d719d554591ac698214dd94ed9c'
assert 'icon: {fileID: 21300000, guid: ' + guid + ', type: 2}' in prefab
assert 'textureCompression: 0' in meta and 'spriteMode: 1' in meta
assert '96035d719d554591ac698214dd94ed9c' not in prefab
print('Overclock guards and 3DMork TGA asset/reference guards: OK')
