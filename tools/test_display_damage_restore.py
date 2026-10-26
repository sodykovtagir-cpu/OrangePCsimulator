#!/usr/bin/env python3
"""Broken-screen appearance survives a saved monitor's restore (static guard)."""
from pathlib import Path
import re

root=Path(__file__).resolve().parents[1]
code=(root/'Assets/Scripts/Assembly-CSharp/PC/Component/Display.cs').read_text()
hardware=(root/'Assets/Scripts/Assembly-CSharp/PC/Component/Hardware.cs').read_text()
assert 'Damaged = jObject["damaged"].ToObject<bool>();' in hardware
assert 'public override void FromData(JObject data)' in code
from_data=code.split('public override void FromData(JObject data)',1)[1].split('private void ApplyBrokenScreen()',1)[0]
assert 'base.FromData(data);' in from_data
assert 'if (Damaged) ApplyBrokenScreen();' in from_data
assert 'Damage();' not in from_data.replace('Damage() effects','')
assert 'if (screen != null && brokenScreenMaterial != null)' in code
assert 'screen.material = brokenScreenMaterial;' in code
assert 'ApplyBrokenScreen();' in code.split('public override void Damage()',1)[1].split('public override void FromData(',1)[0]
# Every saved TV/monitor that has a broken material must still point to its
# authored material. Projector has no broken-screen material in its prefab.
expected='7046ce1e30256b341bae76d7f90f2b4f'
asset=root/'Assets/Material/BrokenScreen.mat.meta'
assert re.search(r'^guid: '+expected+'$',asset.read_text(),re.M)
displays=[]
for path in (root/'Assets/Resources/components').glob('*.prefab'):
    text=path.read_text()
    if 'brokenScreenMaterial:' not in text: continue
    if path.name=='Projector.prefab':
        assert '  brokenScreenMaterial: {fileID: 0}' in text
        continue
    assert re.search(r'^  brokenScreenMaterial: \{fileID: 2100000, guid: '+expected+r', type: 2\}$',text,re.M),path
    displays.append(path.name)
assert len(displays)==13,displays
print('Broken monitor/TV material restoration: OK')
