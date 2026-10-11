#!/usr/bin/env python3
"""Regression guards for save/load slot links and Unity 2022 texture imports.

These are source-level checks; the actual physics/trigger smoke test still needs
Unity 2022.3 (save and reopen an assembled PC).
"""

from pathlib import Path
import re

root = Path(__file__).resolve().parent.parent
scripts = root / 'Assets/Scripts/Assembly-CSharp'
save = (scripts / 'SaveManager.cs').read_text(encoding='utf-8')
data = (scripts / 'ItemData.cs').read_text(encoding='utf-8')
slot = (scripts / 'Slot.cs').read_text(encoding='utf-8')
meta = (root / 'Assets/Texture2D/3DMork.png.meta').read_text(encoding='utf-8')

assert 'public int hostId;' in data and 'public string slotPath;' in data
writing = save.split('public bool SaveData()')[1].split('public int LoadData()')[0]
reading = save.split('public int LoadData()')[1].split('private static bool HasSavedHost(')[0]
assert 'connector.slot' in writing and 'GetSlotPath(host.transform, slot.transform)' in writing
assert 'hostId = hostId' in writing and 'slotPath = slotPath' in writing
assert 'restoredById.TryGetValue(saved.hostId' in reading
assert 'FindSavedSlot(host, saved.slotPath)' in reading
assert 'slot.TryAttach(item)' in reading
assert 'TryRestoreLegacySlot(pair.Item2, restored)' in reading
assert 'slot.InsertPosition.position' in save
assert 'public Transform InsertPosition => insertPos;' in slot
assert float(re.search(r'DuplicateRadius = ([\d.]+)f', save).group(1)) < 0.005
assert re.search(r'^  serializedVersion: 12$', meta, re.M)
assert 'flipGreenChannel:' not in meta and 'swizzle:' not in meta
assert 'ignoreMasterTextureLimit:' in meta
print('PASS: explicit slot links, legacy fallback, adjacent RAM slots and Unity 2022 texture meta')
