#!/usr/bin/env python3
"""Source guards for the restored accent and the three neutral exceptions."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
assets = root / 'Assets'
scripts = assets / 'Scripts/Assembly-CSharp'
read = lambda path: (assets / path).read_text()

# Regular UI keeps the original orange accent (without changing the game name).
assert '<color=orange>Orange PC</color>' in read('Scenes/Menu.unity')
assert '<color=#FF8800>Orange PC</color>' in read('Scripts/Assembly-CSharp/MainMenu.cs')
assert 'new Color(1f, 0.53f, 0f)' in read('Scripts/Assembly-CSharp/WorkshopMenu.cs')
assert '<color=orange>' in read('Scripts/Assembly-CSharp/LanguageMenu.cs')
assert 'm_Color: {r: 1, g: 0.75267804, b: 0.39999998, a: 1}' in read('GameObject/PCOS.prefab')

# 3DMork is exempt. Both serialized graphics and runtime score labels stay neutral.
mork = read('Resources/apps/3DMork.prefab')
assert 'm_Color: {r: 1, g: 0.72, b: 0.2, a: 1}' not in mork
assert 'new Color(1f, 0.72f, 0.2f)' not in read('Scripts/Assembly-CSharp/PC/Component/Software/ThreeDMork.cs')

# Overclock is neutral and entirely prefab-editable, not built again at runtime.
overclock = read('Scripts/Assembly-CSharp/PC/Component/Software/Overclock.cs')
prefab = read('Resources/apps/Overclock.prefab')
assert 'BindControls();' in overclock and 'new GameObject(' not in overclock
ids = re.findall(r'(?m)^--- !u!\d+ &(\d+)', prefab)
assert len(ids) == len(set(ids))
for field in ('gpuSlider', 'ramSlider', 'gpuText', 'ramText', 'statusText', 'testButton', 'saveButton'):
    m = re.search(r'(?m)^  ' + field + r': \{fileID: (\d+)\}', prefab)
    assert m and m.group(1) in ids, field
for node in ('GPU label', 'RAM label', 'GPU ratio', 'RAM ratio', 'Status', 'Test button', 'Save button'):
    assert 'm_Name: ' + node + '\n' in prefab
assert 'm_SizeDelta: {x: 700, y: 410}' in prefab

# Grid/magnet icon still uses its desaturated on-state; base UI tints can be orange.
functions = read('Scripts/Assembly-CSharp/Functions.cs')
assert 'gridImage.color = on ? new Color(.75f, .81f, .86f, 1f) : gridOffColor;' in functions
print('PASS: restored orange UI with neutral 3DMork, Overclock and magnet icon')
