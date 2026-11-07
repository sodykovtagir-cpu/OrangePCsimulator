#!/usr/bin/env python3
"""Motherboards must not ship the HDMI dust-cap at Board video/Snap Pivot/hdmi.

The cap is a nested prefab of Assets/Models/hdmi.fbx. HDMI cables legitimately
use the same model, so only prefabs containing a Motherboard component are
required to be clean.
"""
from pathlib import Path
import re
import yaml

root = Path(__file__).resolve().parents[1]
comp = root / 'Assets/Resources/components'
HDMI_GUID = 'e09ef6a9ef036134bbd0d8ba3ea4cc55'
MB_GUID = 'e9b819df0994a58cece03a6c09fa7092'

motherboards = 0
for p in sorted(comp.glob('*.prefab')):
    raw = p.read_text()
    if MB_GUID not in raw:
        continue
    motherboards += 1
    assert HDMI_GUID not in raw, f'{p.name}: HDMI cap still present'
    assert 'm_Name: Board video' in raw and 'm_Name: Snap Pivot' in raw, (
        f'{p.name}: video snap pivot must remain after cap removal')
    for b in re.split(r'(?=^--- !u!\d+ &\d+)', raw, flags=re.M):
        if b.startswith('---'):
            yaml.safe_load(re.sub(r'^--- !u!\d+ &\d+', '---', b, count=1))

assert motherboards >= 8, f'expected the motherboard set, found {motherboards}'

cables = list(comp.glob('HDMI Cable*.prefab'))
assert cables and all(HDMI_GUID in p.read_text() for p in cables), (
    'HDMI cable prefabs must keep their hdmi.fbx connector')

print(f'PASS: {motherboards} motherboards have no HDMI cap; {len(cables)} HDMI cables intact')
