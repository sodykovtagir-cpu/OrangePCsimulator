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
assert 'private const int MaxImportDimension = 512;' in code
assert 'private const int MaxGifPixels = 12000000;' in code
# Clicking either existing import button must first show the resolution modal.
assert 'public void ImportGif() { ShowImportResolution(true); }' in code
assert 'public void ImportVideo() { ShowImportResolution(false); }' in code
assert 'if (gif) PickGifFromDevice(resolution);' in code
assert 'else PickVideoFromDevice(resolution);' in code
assert 'int.TryParse(importWidthInput.text, out width)' in code
assert 'int.TryParse(importHeightInput.text, out height)' in code
assert 'width > MaxImportDimension || height > MaxImportDimension' in code
assert 'ResizeGifFrames(pictures, resolution)' in code
assert 'Graphics.Blit(original, target);' in code
assert 'RenderTexture.ReleaseTemporary(target);' in code
assert 'new Texture2D(width, height, TextureFormat.RGBA32, false)' in code
assert 'MaxGifPixels / (size.x * size.y)' in code
assert 'ImportVideoFrames(path, resolution)' in code
assert 'int w = resolution.x;' in code and 'int h = resolution.y;' in code
assert 'CancelImportResolution();' in code
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
# Tiny sources must use point sampling in thumbnails, including GIFs enlarged
# to a user-selected output size. Large images keep normal sampling.
assert 'tex.width < thumbWidth || tex.height < thumbHeight' in code
assert 'tex.filterMode = FilterMode.Point;' in code
assert 'thumbnailSize.sizeDelta.x' in code and 'thumbnailSize.sizeDelta.y' in code
assert 'if (pixelArt && (width > original.width || height > original.height))' in code
assert 'original.filterMode = FilterMode.Point;' in code
assert 'result.filterMode = FilterMode.Point;' in code
# A real video import progress bar is presented during Prepare and decoding,
# derives percentage from media time/duration, and closes on every exit path.
assert 'ShowVideoProgress();' in code
assert 'private void SetVideoProgress(float fraction)' in code
assert 'duration = player.length;' in code
assert 'playbackTime / duration' in code
assert 'SetVideoProgress(1f);' in code
assert 'HideVideoProgress();' in code
assert 'public void CancelVideoImport()' in code
assert 'if (mediaImport != null) { StopCoroutine(mediaImport); mediaImport = null; }' in code
# Resolution dialog/controls must be prefab-owned, interactable and initially
# hidden, not improvised after the device file picker opens.
animator_go = by_name['Animator'][1]
animator_component = next(blocks[c['component']['fileID']][1] for c in animator_go['m_Component']
                          if 'importResolutionDialog' in blocks[c['component']['fileID']][1])
modal_id, modal_go = by_name['Import resolution dialog']
modal_rect = blocks[modal_go['m_Component'][0]['component']['fileID']][1]
assert animator_component['importResolutionDialog']['fileID'] == modal_id
assert modal_go['m_IsActive'] == 0
assert modal_rect['m_Father']['fileID'] == animator_go['m_Component'][0]['component']['fileID']
assert modal_rect['m_AnchorMin'] == {'x': 0, 'y': 0}
assert modal_rect['m_AnchorMax'] == {'x': 1, 'y': 1}
assert modal_rect['m_SizeDelta'] == {'x': 0, 'y': 0}
assert modal_rect['m_Children'] == [{'fileID': by_name['Resolution panel'][1]['m_Component'][0]['component']['fileID']}]
root_rect = blocks[animator_go['m_Component'][0]['component']['fileID']][1]
assert root_rect['m_Children'][-2]['fileID'] == modal_go['m_Component'][0]['component']['fileID']
progress_id, progress_go = by_name['Video import progress']
progress_rect = blocks[progress_go['m_Component'][0]['component']['fileID']][1]
assert animator_component['videoProgressDialog']['fileID'] == progress_id
assert progress_go['m_IsActive'] == 0
assert progress_rect['m_Father']['fileID'] == animator_go['m_Component'][0]['component']['fileID']
assert progress_rect['m_AnchorMin'] == {'x': 0, 'y': 0}
assert progress_rect['m_AnchorMax'] == {'x': 1, 'y': 1}
assert root_rect['m_Children'][-1]['fileID'] == progress_go['m_Component'][0]['component']['fileID']
fill_id = animator_component['videoProgressFill']['fileID']
fill_rect = blocks[fill_id][1]
assert fill_rect['m_AnchorMin'] == {'x': 0, 'y': 0}
assert fill_rect['m_AnchorMax'] == {'x': 0, 'y': 1}
assert fill_rect['m_Father']['fileID'] == by_name['Video progress track'][1]['m_Component'][0]['component']['fileID']
assert animator_component['videoProgressLabel']['fileID'] in [
    c['component']['fileID'] for c in by_name['Video progress status'][1]['m_Component']]
cancel_go = by_name['Cancel video import'][1]
cancel_buttons = [blocks[c['component']['fileID']][1] for c in cancel_go['m_Component']
                  if 'm_OnClick' in blocks[c['component']['fileID']][1]]
assert len(cancel_buttons) == 1
cancel_call = cancel_buttons[0]['m_OnClick']['m_PersistentCalls']['m_Calls'][0]
assert cancel_call['m_MethodName'] == 'CancelVideoImport'
assert cancel_call['m_Target']['fileID'] in blocks
for attr, go_name in [('importWidthInput', 'Import width'), ('importHeightInput', 'Import height')]:
    control_id = animator_component[attr]['fileID']
    assert control_id in [c['component']['fileID'] for c in by_name[go_name][1]['m_Component']]
    input_comp = blocks[control_id][1]
    assert input_comp['m_Text'] == 256
    assert input_comp['m_ContentType'] == 2 and input_comp['m_CharacterValidation'] == 1
    assert input_comp['m_OnValueChanged']['m_PersistentCalls']['m_Calls'] == []
    assert input_comp['m_TextComponent']['fileID'] in blocks
    assert input_comp['m_Placeholder']['fileID'] in blocks
message_id = animator_component['importResolutionMessage']['fileID']
assert message_id in [c['component']['fileID'] for c in by_name['Resolution message'][1]['m_Component']]
for name, method_name in [('Choose file', 'ConfirmImportResolution'),
                          ('Cancel', 'CancelImportResolution')]:
    go = by_name[name][1]
    buttons = [blocks[c['component']['fileID']][1] for c in go['m_Component']
               if 'm_OnClick' in blocks[c['component']['fileID']][1]]
    assert len(buttons) == 1
    call = buttons[0]['m_OnClick']['m_PersistentCalls']['m_Calls'][0]
    assert call['m_Target']['fileID'] in blocks
    assert call['m_MethodName'] == method_name
print('PASS: Animator prefab, crisp tiny thumbnails, device import progress, bounds and cleanup')
print('NOTE: Real media import and rendering still require Unity Play Mode on a supported device')
