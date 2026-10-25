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
assert 'BootLog.Create(this, debug)' not in bios and 'BootLog.Create(this, false)' in bios
assert 'rect.anchorMin = Vector2.zero' in log and 'rect.anchorMax = Vector2.one' in log
assert 'if (!verbose) lines.Clear();' in log and 'gameObject.SetActive(true);' in log
assert 'background.raycastTarget = false;' in log and 'text.raycastTarget = false;' in log
assert 'new Color(0.015f, 0.02f, 0.03f, 0.68f)' in log
assert 'panelRect.anchorMin = new Vector2(0.02f, 0.55f)' in log
assert 'panelRect.anchorMax = new Vector2(0.37f, 0.97f)' in log
assert os.index('if (password != null) password.SetActive(false);') < os.index('if (user != null) user.SetActive(true);')
assert os.index('if (user != null) user.SetActive(true);') < os.index('if (password != null) password.SetActive(true);')
assert 'rect.anchorMin = Vector2.zero' in log and 'rect.anchorMax = Vector2.one' in log
assert 'bootLog.Stage("01 / KERNEL AND DEVICES")' in os
assert 'bootLog.Stage("02 / USER SESSION")' in os
assert os.index('bootLog.Stage("01 / KERNEL AND DEVICES")') < os.index('Starting process: device.enumerator') < os.index('bootLog.Stage("02 / USER SESSION")')
assert os.index('bootLog.Stage("02 / USER SESSION")') < os.index('Requesting account password') < os.index('Pre-rendering desktop resources') < os.index('Checking user directives in Autorun.conf')
assert 'while (awaitingPassword && !error)' in os and 'StartCoroutine(FinishLogin())' in os
assert 'awaitingPassword = false;' in os and 'awaitingPassword = true;' in os
assert 'while (!desktopPreRendered) yield return null;' in os
assert os.index('Checking user directives in Autorun.conf') < os.index('"init user"') < os.index('bootLog.Close(); bootLog = null;')
assert 'desktopPreRendered = true;' in os and 'bool hasPassword = false;' in os
assert 'if (bootLog != null) bootLog.Add("Password accepted; initializing user session")' in os
assert 'System/autorun.exec' not in os
assert 'else if (cmd == "boot")' in terminal and 'required boot directive, already validated' not in terminal
print('Boot command, PCOS overlay, two startup stages, password, and failure guards: OK')
