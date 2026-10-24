#!/usr/bin/env python3
"""Static regression for visible System root and per-PC administrator access."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
explorer = (SRC / 'FileManager.cs').read_text()
os = (SRC / 'OS/OperatingSystem.cs').read_text()


def body(text, signature):
    begin = text.index(signature)
    opening = text.index('{', begin)
    depth = 0
    for i in range(opening, len(text)):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                return text[opening:i + 1]
    raise AssertionError(f'unclosed {signature}')


start = body(explorer, 'protected override void Start()')
assert start.index('SelectStorage(0);') < start.index('InitializeSystemFolders();')
assert start.index('InitializeSystemFolders();') < start.index('if (startStorageIndex > 0)')
create = body(explorer, 'private void InitializeSystemFolders()')
assert '"System"' in explorer and 'new File(folder, "", false, 0)' in create
assert 'folderFile.isFolder = true;' in create and 'storage.AddFile(folderFile)' in create

files = body(explorer, 'private List<File> GetFilesInCurrentFolder()')
assert 'if (file == null) continue;' in files
assert re.search(r'!IsAdministrator\s*&&\s*OS\.OperatingSystem\.IsSystemPath\(file\.path\)\s*&&\s*!OS\.OperatingSystem\.IsSystemRootFolder\(file\)', files)
assert 'if (fileFolder == currentFolder)' in files
assert 'result.OrderByDescending(f => f.isFolder)' in files

root = body(os, 'public static bool IsSystemRootFolder(File file)')
assert 'file.isFolder' in root
assert 'string.Equals(file.path, "System", StringComparison.OrdinalIgnoreCase)' in root
assert 'var segments = path.Replace' in body(os, 'public static bool IsSystemPath(string path)')

open_folder = body(explorer, 'private void OpenFolder(int index)')
assert 'if (!IsAdministrator && OS.OperatingSystem.IsSystemPath(file.path))' in open_folder
assert 'system.OpenFileFromExplorer(file, this)' in open_folder
assert open_folder.index('system.OpenFileFromExplorer(file, this)') < open_folder.index('currentFolder = file.path;')
open_from_menu = body(os, 'public bool OpenFileFromExplorer(File file, PC.Component.Software.FileManager explorer)')
assert 'if (IsElevatedExplorer(explorer)) return OpenFileInternal(file, true);' in open_from_menu
assert 'IsSystemRootFolder(file)' in open_from_menu
assert 'runningApps.Contains(explorer)' in open_from_menu
assert 'all[disk] != storage' in open_from_menu
assert 'OpenFileAsAdministrator(file);' in open_from_menu
assert 'return true;' in open_from_menu
assert 'RequestAdministrator(allowed =>' in body(os, 'public void OpenFileAsAdministrator(File file)')
assert 'string.Equals(typed, storage.password' in os
assert 'candidate.IsAdministrator == elevated' in os

# The elevated window must open the file's real storage, including a secondary
# drive, rather than silently redirecting into the boot drive.
assert 'IndexOfStorage(FindStorageOf(folder))' in body(os, 'private bool OpenFolder(File folder, bool elevated = false)')
open_path = body(explorer, 'public void OpenFolderFromPath(string path, int storageIndex = -1)')
assert 'if (!IsAdministrator && OS.OperatingSystem.IsSystemPath(path)) return;' in open_path
assert 'SelectStorage(storageIndex)' in open_path
assert 'currentFolder = startFolderPath;' in open_path

# Showing a folder is not a permission grant: contents and mutation are still
# blocked from normal Explorer, Save As and Lua/OS file operations.
assert 'bool protect = IsProtectedFile(file) && !IsAdministrator;' in explorer
assert 'if (!IsAdministrator && IsProtectedFile(file)) return;' in explorer
assert 'if (IsSystemPath(file.path)) return true;' in os
assert 'if (IsSystemPath(file.path)) return false;' in os
assert '(!administrator && OperatingSystem.IsSystemPath(currentFolder))' in (SRC / 'OS/SaveDialog.cs').read_text()
assert 'if (!administrator && OperatingSystem.IsSystemPath(path))' in (SRC / 'OS/SaveDialog.cs').read_text()
assert 'if (f.isFolder && f.path == "System") continue;' in os  # no desktop icon

# Model the intentional difference between root and protected children.
paths = [('System', True), ('System/boot.bin', False),
         ('System/Config', True), ('Documents', True), ('Documents/note.txt', False)]
def visible(path, is_folder, elevated):
    system_path = 'System' in path.split('/')
    return elevated or not system_path or (path == 'System' and is_folder)
assert [p for p, folder in paths if visible(p, folder, False)] == ['System', 'Documents', 'Documents/note.txt']
assert len([p for p, folder in paths if visible(p, folder, True)]) == len(paths)
print('PASS: System visible in drive root; its contents still require per-PC administrator confirmation')
print('NOTE: Clicks, password prompt and Explorer rendering still require Unity Play Mode')
