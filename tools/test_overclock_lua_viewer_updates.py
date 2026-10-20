#!/usr/bin/env python3
"""Guards for stress/power, stock recovery, editor caret and media navigation."""
from pathlib import Path
import ast
import json

root=Path(__file__).resolve().parents[1]
base=root/'Assets/Scripts/Assembly-CSharp/PC/Component'
read=lambda name:(base/name).read_text()
boot=read('Software/OS/OverclockBoot.cs')
app=read('Software/Overclock.cs')
os=read('Software/OS/OperatingSystem.cs')
explorer=read('Software/FileManager.cs')
gpu=read('GPU.cs')
cpu=read('CPU.cs')
viewer=read('Software/Viewer.cs')
lua=read('Software/LuaEditor.cs')
track=read('Software/LuaCaretTrack.cs')
hook=read('Software/LuaSnippetButton.cs')
prefab=(root/'Assets/Resources/apps/Overclock.prefab').read_text()
assert 'Time.realtimeSinceStartup - startedAt < 30f' in app
assert '30-second test complete' in app and 'Test 30 seconds' in prefab
assert 'Test optional' not in app+prefab and '(real time)' not in app+prefab
assert 'OverclockBoot.ResetToStock(system.FileManager)' in app
assert 'fm.Delete(0, Path)' in boot and 'DisableLine(autorun.content)' in boot
assert 'file.path == OS.OverclockBoot.Path' in explorer
assert 'OverclockBoot.Recover(disk)' in os and 'FileManager.Delete(0, OverclockBoot.Path)' in os
assert 'OverclockBoot.FatalRatio' in gpu and 'OverclockRatio - 1.15f' not in gpu
assert 'SetStressTest(true)' in app and 'SetStressTest(false)' in app
assert 'RegisterWork(.40f, 1f)' in os and 'UpdateWorkload();' in os
assert 'CurrentDrawWatts' in os and cpu.count('Workload') >= 3
assert 'heat * load * (.35f + 1.35f * Mathf.Clamp01(Workload))' in cpu
assert 'touchScreenKeyboard' in track and 'IPointerDownHandler' in hook
assert 'CaptureSnippetCaret()' in lua and 'track.HasSelection' in lua
assert 'btn.onClick.AddListener(hook.Click);' in lua
assert 'LuaDocumentationPath = "Lua Documentation.txt"' in os
assert 'Localization.LanguageChanged += OnDocumentationLanguageChanged;' in os
assert 'LuaDocs.Text()' in os
assert 'public void PreviousFile()' in viewer and 'public void NextFile()' in viewer
assert 'siblings[(i + direction + siblings.Count) % siblings.Count]' in viewer
assert 'OpenDocument(file);' in os and 'System.IO.Path.GetFileName(currentFile.path)' in viewer
assert 'ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)' in viewer
gif=read('Software/GifFrames.cs')
assert 'GifFrames.TryDecode(bytes, out loaded, out frameIntervals)' in viewer
assert 'DecodeLzw(' in gif and 'interlaced' in gif and 'ReadPalette(' in gif
assert 'frameIntervals[index]' in viewer
resources=root/'Assets/Resources'
manuals={p.name for p in resources.glob('LuaDocs_*.txt')}
assert manuals == {'LuaDocs_EN.txt','LuaDocs_RU.txt'}, manuals
for name in manuals:
    content=(resources/name).read_text()
    assert 'fs.write' in content and 'ui.button' in content and 'gfx.flip' in content
    # Every exposed UI/graphics/OS/files/window function is covered by the manual.
    import re
    api_base=base/'Software/Lua'
    for file, table, prefix in (
        ('LuaUi.cs','ui','ui'),('LuaGfx.cs','g','gfx'),
        ('PcosLuaHost.cs','osTbl','os'),('PcosLuaHost.cs','fs','fs'),
        ('PcosLuaHost.cs','win','win')):
        exported=re.findall(r'\b'+table+r'\.Set\(LuaValue\.String\("([a-z]+)"',
                            (api_base/file).read_text())
        assert all(f'{prefix}.{fn}' in content for fn in exported),(name,file)
assert 'Resources.Load<TextAsset>("LuaDocs_" + lang)' in lua
assert '"RU" : "EN"' in lua
assert 'LuaDocs.Text()' in os
print('PASS: workload, Overclocker recovery/test, Lua caret and EN/RU docs, Viewer navigation')
