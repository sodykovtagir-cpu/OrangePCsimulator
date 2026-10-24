#!/usr/bin/env python3
"""Static guards for shared PCOS Save As; in-game UI still needs Unity Play Mode."""
from pathlib import Path
import re
import yaml

root = Path(__file__).resolve().parents[1]
src = root / 'Assets/Scripts/Assembly-CSharp'
software = src / 'PC/Component/Software'
read = lambda name: (software / name).read_text()
dialog = read('OS/SaveDialog.cs')
os = read('OS/OperatingSystem.cs')
storage = (src / 'PC/Component/Storage.cs').read_text()
prefab = (root / 'Assets/GameObject/PCOS.prefab').read_text()

def method(text, signature):
    i = text.index(signature)
    start = text.index('{', i)
    depth = 0
    for end in range(start, len(text)):
        depth += (text[end] == '{') - (text[end] == '}')
        if not depth:
            return text[start:end + 1]
    raise AssertionError('unbalanced braces in ' + signature)

# Save As is authored in PCOS.prefab, not constructed from an OpenDialog
# clone. Its own scroll view, texts, input and buttons must remain editable.
blocks = {}
for match in re.finditer(r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)',
                         prefab, re.M | re.S):
    blocks[int(match.group(2))] = (int(match.group(1)), next(iter(yaml.safe_load(match.group(3)).values())))
assert len(blocks) == len(list(re.finditer(r'^--- !u!', prefab, re.M)))
by_name = {v['m_Name']: (id, v) for id, (typ, v) in blocks.items() if typ == 1}
for name in ('SaveDialog', 'OpenDialog', 'Save file list', 'Save location',
             'Save message', 'File name label'):
    assert name in by_name, name
save_id, save_go = by_name['SaveDialog']
save_comp = next(v for typ, v in blocks.values() if typ == 114 and
                 v.get('m_Script', {}).get('guid') == '7e83518496a8af3c61eac3de50c20b32')
assert save_comp['m_GameObject']['fileID'] == save_id
for key in ('fileNameInput', 'extensionText', 'listView', 'locationText',
            'messageText', 'saveButtonText'):
    assert save_comp[key]['fileID'] in blocks, key
list_id, list_go = by_name['Save file list']
assert list_go['m_IsActive'] == 1
list_rect = blocks[list_go['m_Component'][0]['component']['fileID']][1]
assert list_rect['m_AnchorMin'] == {'x': 0, 'y': 0}
assert list_rect['m_AnchorMax'] == {'x': 1, 'y': 1}
assert list_rect['m_SizeDelta'] == {'x': -20, 'y': -232}
assert list_rect['m_AnchoredPosition'] == {'x': 0, 'y': 23}
panel_rect = blocks[list_rect['m_Father']['fileID']][1]
assert panel_rect['m_SizeDelta'] == {'x': 600, 'y': 480}
assert list_rect['m_Father']['fileID'] in [c['fileID'] for c in
    blocks[save_go['m_Component'][0]['component']['fileID']][1]['m_Children']]
assert list_go['m_Component'][-1]['component']['fileID'] == save_comp['listView']['fileID']
# The gray Image and scrollable viewport are authored on this very prefab
# object; resizing only a runtime copy would not fix the reported white bands.
list_components = [blocks[c['component']['fileID']][1] for c in list_go['m_Component']]
assert any(c.get('m_Color') == {'r': 0.9411765, 'g': 0.9411765,
                               'b': 0.9411765, 'a': 1} for c in list_components)
assert any(c.get('m_Content', {}).get('fileID') in blocks and
           c.get('m_Viewport', {}).get('fileID') in blocks
           for c in list_components if 'm_Content' in c)
list_view = blocks[save_comp['listView']['fileID']][1]
assert list_view['itemParent']['fileID'] in blocks
for key, name in [('locationText', 'Save location'), ('messageText', 'Save message')]:
    assert blocks[save_comp[key]['fileID']][1]['m_GameObject']['fileID'] == by_name[name][0]
name_go = by_name['File name label'][1]
name_rect = blocks[name_go['m_Component'][0]['component']['fileID']][1]
assert name_rect['m_Pivot'] == {'x': 0, 'y': 1}
assert name_rect['m_AnchoredPosition'] == {'x': 12, 'y': 137}
assert 'Instantiate(template.gameObject' not in dialog
assert 'new GameObject(' not in dialog
assert 'offsetMin =' not in dialog and 'sizeDelta =' not in dialog
assert 'listView.SelectedIndexChanged += SelectedIndexChanged;' in dialog
assert 'pickerInitialized = true;' in dialog
assert 'internal OpenDialog OpenPickerTemplate => fileDialog;' not in os
assert 'Enter();' in method(dialog, 'private void SelectedIndexChanged(')
assert 'fileNameInput.onValueChanged.AddListener(_ => ClearConfirmation())' in dialog
assert 'gameObject.SetActive(true)' in method(dialog, 'public void ShowDialog(')
assert 'transform.SetAsLastSibling();' in method(dialog, 'public void ShowDialog(')
for key in ('currentDisk = -1', 'currentFolder = entry.file.path',
            'currentFolder.LastIndexOf', 'all[d].storageName',
            'file.isFolder != (pass == 0)', 'file.hidden', 'MatchesExtension(file.Extension())'):
    assert key in dialog, key
assert 'OperatingSystem.IsSystemPath(file.path)' in dialog
assert 'OperatingSystem.IsSystemPath(path)' in method(dialog, 'public void Save()')
assert "name.IndexOfAny(new[] { '/', '\\\\', ':', '*', '?', '\"', '<', '>', '|' })" in dialog
save = method(dialog, 'public void Save()')
assert save.index('pendingReplace = path;') < save.index('fm.Write(currentDisk, path, content)')
assert 'messageText.text = "File exists. Click Replace' in save
assert 'fm.Create(currentDisk, result)' in save and 'result.content != content' in save
assert 'var callback = saved;' in save and 'if (callback != null) callback(result)' in save
assert 'saved = null;' in method(dialog, 'public void Cancel()')
# Last location belongs to the current in-game PC, not global PlayerPrefs. Use
# the stable storage hardware ID, not a removable disk's transient list index.
assert 'private const string LastLocationPath = "System/SaveDialog.location";' in dialog
assert 'diskId = all[currentDisk].Id' in method(dialog, 'private void RememberLocation()')
assert 'all[d].Id != saved.diskId' in method(dialog, 'private void LoadLastLocation()')
assert 'file.hidden = true;' in method(dialog, 'private void RememberLocation()')
show = method(dialog, 'public void ShowDialog(')
assert 'if (string.IsNullOrEmpty(initialPath) && preferredDisk < 0)' in show
assert show.index('LoadLastLocation();') < show.index('if (!string.IsNullOrEmpty(initialPath))')
assert 'TryGetFile(currentDisk, currentFolder, out folder)' in show
assert 'OperatingSystem.IsSystemPath(currentFolder)' in show
assert save.index('RememberLocation();') > save.index('fm.Create(currentDisk, result)')
assert 'RememberLocation();' in method(dialog, 'public void Cancel()')
assert 'file.StorageSize' in storage and 'file.size = content.Length' in storage
assert 'return AddFile(created) ? created : null;' in storage

# All document Save buttons and user-facing exports pass through the same
# destination/filename picker; service state/autosave remains noninteractive.
for app in ('TextEditor', 'LuaEditor', 'Paint', 'Animator', 'CameraApp', 'DeviceManager'):
    code = read(app + '.cs')
    assert '.ShowDialog(' in code, app
text_editor = read('TextEditor.cs')
assert 'openedFile != null ? filePath : null, disk, saved =>' in text_editor
assert 'openedFile = saved;' in text_editor and 'IsAdministrator);' in text_editor
assert 'FileManager.Write(disk, filePath, content)' not in text_editor
lua_editor = read('LuaEditor.cs')
assert 'fileDisk = DiskFor(file)' in lua_editor
assert 'new[] { ".exe" }' in lua_editor and 'new[] { ".lua", ".txt" }' in lua_editor
assert 'FileManager.Write(0, path, json)' not in lua_editor
manager = read('DeviceManager.cs')
assert 'new[] { ".tmn" }' in manager and 'owner.SaveDialog.ShowDialog(' in manager
assert 'onSystemDisk' in manager and 'AutorunConfig.AppendCommand' in manager
assert 'fm.Write(0, fileName, bodyText)' not in manager
print('PASS: Save As picker wiring, disks/folders, name validation, overwrite, admin gates, callbacks and app exports')
print('NOTE: Rendering, scrolling, clicks and disk changes require Unity Play Mode verification')
