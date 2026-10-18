#!/usr/bin/env python3
"""Static guards for icon prefab recovery and 3DMork prefab-owned text colors.

Unity Play Mode remains necessary to verify visibility on the player's monitor.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
OS = (ROOT / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/OperatingSystem.cs').read_text()
MORK = (ROOT / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software/ThreeDMork.cs').read_text()
PCOS = (ROOT / 'Assets/GameObject/PCOS.prefab').read_text()
ORIGINAL = (ROOT / 'Assets/GameObject/FileIcon.prefab').read_text()
ORIGINAL_META = (ROOT / 'Assets/GameObject/FileIcon.prefab.meta').read_text()
FALLBACK = (ROOT / 'Assets/Resources/DesktopFileIcon.prefab').read_text()
FALLBACK_META = (ROOT / 'Assets/Resources/DesktopFileIcon.prefab.meta').read_text()

original_guid = re.search(r'^guid: ([0-9a-f]{32})$', ORIGINAL_META, re.M).group(1)
fallback_guid = re.search(r'^guid: ([0-9a-f]{32})$', FALLBACK_META, re.M).group(1)
assert original_guid != fallback_guid
assert [line.rstrip() for line in ORIGINAL.splitlines()] == [line.rstrip() for line in FALLBACK.splitlines()]
assert re.search(r'fileIconPrefab: \{fileID: 114955570314456545, guid: ' + original_guid, PCOS)
assert 'Resources.Load<FileIcon>("DesktopFileIcon")' in OS
assert OS.index('fileIconPrefab = Resources.Load<FileIcon>("DesktopFileIcon")') < OS.index('Instantiate(fileIconPrefab, iconParent)')
assert 'eligible=' in OS and 'hidden=' in OS and 'nested=' in OS and 'prefab=' in OS
assert 'var candidates = CollectDesktopFiles();' in OS
assert 'if (f.hidden) continue;' in OS.split('private List<File> CollectDesktopFiles()')[1]

calls = re.findall(r'\bSetCell\((?:leaderboard|history)[^;]+;', MORK)
assert len(calls) == 10 and all('new Color' not in call for call in calls)
method = MORK.split('private static void SetCell(')[1].split('private static string FormatDate(', 1)[0]
assert 'cell.text = value;' in method and 'cell.color' not in method
assert not re.search(r'\b\w+Text\.color\s*=', MORK)  # no other text color writes
assert 'viewportImage.color = Color.white' in MORK  # unrelated render texture tint
print('PASS: icon fallback/reference, diagnostic counts and 3DMork prefab text colors')
