#!/usr/bin/env python3
"""Guard against player-build-only CS0103 in Raycast.cs.

Unity compiles editor and Android with different #if symbols. A field used by
RotateDragged outside the desktop block must be declared outside it as well.
This source check does not replace an actual Unity Android build.
"""
from pathlib import Path
import re

path = Path(__file__).resolve().parent.parent / 'Assets/Scripts/Assembly-CSharp/Raycast.cs'
src = path.read_text(encoding='utf-8')

# Strip the desktop-only sections as the Android C# compiler sees them.
without_desktop = re.sub(
    r'^#if UNITY_STANDALONE \|\| UNITY_EDITOR \|\| UNITY_WEBGL\s*$.*?^#endif\s*$',
    '', src, flags=re.MULTILINE | re.DOTALL)
for field, typ in [('rotationBlocker', 'Collider'),
                   ('rotationMissingSupport', 'bool'),
                   ('nextRotationBlockLog', 'float')]:
    decl = f'private {typ} {field};'
    assert src.count(decl) == 1, f'{field}: expected one declaration'
    assert decl in without_desktop, f'{field}: missing in Android player build'
    assert without_desktop.count(field) > 1, f'{field}: rotation does not use the field'
assert 'private void HoldArrow(' not in without_desktop
assert 'public void RotateDragged(' in without_desktop
print('PASS: Android sees all cross-platform rotation fields; desktop-only input stays guarded')
