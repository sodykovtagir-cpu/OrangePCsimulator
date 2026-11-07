#!/usr/bin/env python3
"""Factory elevator buttons must visibly change color when pressed.

Old behavior only toggled the _EMISSION keyword on Button.mat; that variant can
be stripped from player builds, leaving the button unchanged. Platform now also
swaps the button renderers to an assigned 'on' material, and the Factory scenes
assign OnButton.mat.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
platform = (root / 'Assets/Scripts/Assembly-CSharp/Platform.cs').read_text()
on_guid = re.search(r'^guid: ([0-9a-f]{32})$',
                    (root / 'Assets/Material/OnButton.mat.meta').read_text(), re.M).group(1)
button_guid = re.search(r'^guid: ([0-9a-f]{32})$',
                        (root / 'Assets/Material/Button.mat.meta').read_text(), re.M).group(1)
PLATFORM_GUID = 'f372cf32e25b4d59bb1d9b6fc3596e6b'

assert 'public Material buttonOnMaterial;' in platform
assert 'private void CacheButtonRenderers()' in platform
assert 'private void SetButtonLit(bool lit)' in platform
assert 'r.sharedMaterial = mat;' in platform
assert 'SetButtonLit(true);' in platform
assert 'SetButtonLit(false);' in platform
# No unguarded buttonMaterial dereference left in OnDestroy.
destroy = platform.split('private void OnDestroy()')[1].split('public override void ToData')[0]
assert 'buttonMaterial.DisableKeyword' not in destroy

for scene_name in ('Main 4', 'Main 5'):
    raw = (root / 'Assets/Scenes' / (scene_name + '.unity')).read_text()
    m = re.search(r'^--- !u!114 &\d+\n(?:(?!--- ).)*?m_Script: \{fileID: 11500000, guid: '
                  + PLATFORM_GUID + r', type: 3\}(.*?)(?=^--- |\Z)', raw, re.M | re.S)
    assert m, f'{scene_name}: Platform component missing'
    body = m.group(1)
    assert f'buttonMaterial: {{fileID: 2100000, guid: {button_guid}, type: 2}}' in body
    assert f'buttonOnMaterial: {{fileID: 2100000, guid: {on_guid}, type: 2}}' in body, (
        f'{scene_name}: elevator buttonOnMaterial must be OnButton.mat')

print('PASS: Factory elevator buttons swap to OnButton.mat while moving')
