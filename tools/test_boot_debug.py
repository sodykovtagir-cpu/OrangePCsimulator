#!/usr/bin/env python3
"""Static boot-contract guards. Verify actual screen behaviour in Unity Play Mode."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
base = root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS'
read = lambda name: (base / name).read_text()
autorun = read('AutorunConfig.cs')
os = read('OperatingSystem.cs')
bios = read('Bios.cs')
overclock = read('OverclockBoot.cs')
log = read('BootLog.cs')
terminal = (base.parent / 'Terminal.cs').read_text()

assert 'boot boot.bin\\ninit devices\\ninit user\\n' in autorun
assert 'MigrateBootCommands' in autorun and 'if (next == null) continue;' in autorun
assert 'if (!fm.Exists(0, Path)) fm.Write(0, Path, Default);' in autorun
assert 'if (fm == null || fm.Exists(0, Marker)) return;' in autorun
assert 'line == "boot " + file + " -debug"' in autorun
assert 'line.StartsWith("boot ", StringComparison.Ordinal)' in autorun
assert 'public const string Command = "boot overclockerd-boot.bin"' in overclock
assert 'lines.Add("//boot boot.bin" + (debug ? " -debug" : ""))' in overclock
assert 'lines.Add(Command + (debug ? " -debug" : ""))' in overclock
assert 'lines.Add("boot boot.bin" + (debug ? " -debug" : ""))' in overclock
assert 'public static bool Enabled(string autorun)' in overclock
assert 'bootLog = BootLog.Create(this, overclock ? overclockDebug : stockDebug)' in os
assert os.index('Kernel Panic! Can\'t find boot.bin') < os.index('Boot not specified') < os.index('Failed to init user')
assert 'OverclockFailure(profile)' in os and 'Kernel panic! GPU failure' in os and 'Kernel panic! RAM failure' in os
assert 'Kernel panic! Can\'t find overclockerd-boot.bin' in os
assert 'Kernel panic! Can\'t find Autorun.conf' in os
assert 'bootLog.Close(); bootLog = null;' in os
assert 'SystemDisk()' in bios and 'bootFailure = true;' in bios
assert 'Kernel Panic! Can\'t find boot.bin' in bios
assert 'BootLog.Create(this, debug)' in bios
assert 'rect.anchorMin = Vector2.zero' in log and 'rect.anchorMax = Vector2.one' in log
assert 'if (!verbose) lines = "";' in log and 'gameObject.SetActive(true);' in log
assert 'else if (cmd == "boot")' in terminal and 'required boot directive, already validated' not in terminal
print('Boot command, migration, fullscreen debug, and failure-stage guards: OK')
