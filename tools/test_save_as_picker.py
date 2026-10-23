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

# PCOS has BOTH serialized pickers; Save As reuses the actual OpenDialog
# scroll/list prefab, not a separate incompatible file representation.
raw = re.sub(r'--- !u!\d+ &\d+', '---', prefab.replace('%YAML 1.1\n', '').replace('%TAG !u! tag:unity3d.com,2011:\n', ''))
blocks = list(yaml.safe_load_all(raw))
assert {'SaveDialog', 'OpenDialog', 'Scroll View'} <= {b['GameObject']['m_Name'] for b in blocks if 'GameObject' in b}
assert 'internal OpenDialog OpenPickerTemplate => fileDialog;' in os
assert 'system.OpenPickerTemplate.transform.Find("Panel/Scroll View")' in dialog
assert 'Instantiate(template.gameObject, panel)' in dialog
assert 'listView.SelectedIndexChanged += SelectedIndexChanged;' in dialog
assert 'new GameObject("Open folder"' in dialog  # not a cloned persistent Save() event
assert 'button.onClick.AddListener(Enter)' in dialog
assert 'RemoveAllListeners()' not in dialog
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
