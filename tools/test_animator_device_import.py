#!/usr/bin/env python3
"""Static wiring/resource guards; playback needs an actual Unity device test."""
from pathlib import Path
import re
import yaml

root = Path(__file__).resolve().parents[1]
code = (root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software/Animator.cs').read_text()
prefab = (root / 'Assets/Resources/apps/Animator.prefab').read_text()
blocks = {}
for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', prefab, re.M | re.S):
    blocks[int(m.group(2))] = (int(m.group(1)), next(iter(yaml.safe_load(m.group(3)).values())))
assert len(blocks) == len(list(re.finditer(r'^--- !u!', prefab, re.M)))
by_name = {v['m_Name']: (id, v) for id, (typ, v) in blocks.items() if typ == 1}
toolbar_id, toolbar = by_name['Toolbar']
toolbar_rect = blocks[toolbar['m_Component'][0]['component']['fileID']][1]
assert toolbar_rect['m_SizeDelta']['x'] == -14
ordered = [blocks[blocks[c['fileID']][1]['m_GameObject']['fileID']][1]['m_Name']
           for c in toolbar_rect['m_Children']]
assert ordered.index('AddFrame') < ordered.index('Import GIF') < ordered.index('Import Video')
# Existing six 35-pixel buttons + two native-media buttons + seven 10-pixel gaps
# must fit inside the 499-pixel-wide toolbar at its default prefab size.
widths = [blocks[by_name[name][1]['m_Component'][0]['component']['fileID']][1]['m_SizeDelta']['x']
          for name in ordered]
assert sum(widths) + 10 * (len(widths) - 1) <= 513 - 14
for name, method_name in [('Import GIF', 'ImportGif'), ('Import Video', 'ImportVideo')]:
    go_id, go = by_name[name]
    rect = blocks[go['m_Component'][0]['component']['fileID']][1]
    assert rect['m_Father']['fileID'] == toolbar['m_Component'][0]['component']['fileID']
    label = blocks[blocks[rect['m_Children'][0]['fileID']][1]['m_GameObject']['fileID']][1]
    assert any(blocks[c['component']['fileID']][1].get('m_Text') == name for c in label['m_Component'])
    buttons = [blocks[c['component']['fileID']][1] for c in go['m_Component']
               if 'm_OnClick' in blocks[c['component']['fileID']][1]]
    assert len(buttons) == 1
    click = buttons[0]['m_OnClick']['m_PersistentCalls']['m_Calls'][0]
    assert click['m_TargetAssemblyTypeName'] == 'PC.Component.Software.Animator, Assembly-CSharp'
    assert click['m_Target']['fileID'] in blocks
    assert click['m_MethodName'] == method_name
    assert 'public void ' + method_name + '()' in code

assert 'NativeFilePicker.PickFile(' in code
assert 'NativeFilePicker.ConvertExtensionToFileType("gif")' in code
assert 'GifFrames.TryDecode(System.IO.File.ReadAllBytes(path)' in code
assert 'info.Length > 24000000L' in code
assert 'NativeGallery.GetVideoFromGallery(' in code
assert '"video/*"' in code
assert 'private const int MaxImportFrames = 96;' in code
assert 'private const int MaxVideoDimension = 512;' in code
assert 'private const int ImportFps = 8;' in code
assert 'new WaitForEndOfFrame()' in code and 'TryReadVideoFrame(' in code
assert 'player.audioOutputMode = VideoAudioOutputMode.None;' in code
assert 'player.renderMode = VideoRenderMode.RenderTexture;' in code
assert 'player.errorReceived += ImportVideoError' in code
assert 'player.errorReceived -= ImportVideoError' in code or 'importPlayer.errorReceived -= ImportVideoError' in code
assert 'if (!committed) DestroyImported(pictures.ToArray())' in code
assert 'DestroyImported(pictures);' in code
assert 'importRender.Release();' in code and 'Destroy(importRender);' in code
assert 'Destroy(importHost);' in code
assert 'CancelVideoImport();' in code and 'public override void Close()' in code
assert 'public override void OnSystemStop()' in code
assert 'private void OnDestroy()' in code
assert 'ClearTimeline();' in code
assert 'FormatConverter.MovToString' in code and 'dlg.ShowDialog' in code
assert 'FormatConverter.StringToMov' in code
print('PASS: Animator prefab native GIF/video controls, device pickers, bounded decoding and cleanup')
print('NOTE: Real media import and rendering still require Unity Play Mode on a supported device')
