#!/usr/bin/env python3
"""Task Manager: genuinely prefab-authored, masked, neutral PCOS layout;
Viewer: enlarged navigation centered on the left/right edges."""
from pathlib import Path
import re
import yaml
root = Path(__file__).resolve().parents[1]
pref = (root/'Assets/Resources/apps/TaskManager.prefab').read_text()
code = (root/'Assets/Scripts/Assembly-CSharp/PC/Component/Software/TaskManager.cs').read_text()
viewer = (root/'Assets/Scripts/Assembly-CSharp/PC/Component/Software/Viewer.cs').read_text()
row = (root/'Assets/Scripts/Assembly-CSharp/PC/Component/Software/TaskManagerRow.cs').read_text()

docs = list(yaml.compose_all(re.sub(r'!u!\d+', '', pref)))
ids = re.findall(r'^--- !u!\d+ &(\d+)$', pref, re.M)
assert len(docs) == len(ids) and len(ids) == len(set(ids)), 'duplicate/malformed fileIDs'
refs = set(re.findall(r'\{fileID: (\d+)\}', pref))
assert not refs - set(ids) - {'0','11500000','21300000','12800000'}
names = re.findall(r'(?m)^  m_Name: (.+)$', pref)
for name in ('WorkArea','Navigation','Processes tab','Performance tab','PageArea',
             'ProcessesPage','PerformancePage','Process scroll','Viewport',
             'Process rows','Row template','End task','CPU card','GPU card','RAM card',
             'CPU graph','GPU graph','RAM graph'):
    assert names.count(name) == 1, (name, names.count(name))
assert len(names) >= 50, 'Visible UI must be authored as independent Prefab elements'
assert pref.count('m_ShowMaskGraphic: 1') >= 5, 'Native buttons and viewport require UI masks'
assert pref.count('guid: 4d77c5aaf2654d642920f5f73758647f') >= 4, 'File Manager sliced button sprite'
assert 'm_Color: {r: 1, g: 1, b: 1, a: 1}' in pref
for key in ('processesTab','performanceTab','processesIndicator','performanceIndicator',
            'processesPage','performancePage','rows','rowTemplate','endButton','status',
            'stats','bars','graphs'):
    assert f'  {key}:' in pref, key
    assert f'private ' in code and key in code, key
assert 'SYSTEM MONITOR' not in pref + code and 'МОНИТОР СИСТЕМЫ' not in pref + code
assert 'Orange' not in code and 'Mint' not in code and 'new GameObject(' not in code
assert 'Instantiate(rowTemplate, rows)' in code and 'background.color' in row
assert 'selectedColor:' in pref and 'evenColor:' in pref
assert 'Color grid = new Color(.42f, .42f, .42f, .12f);' in (root/'Assets/Scripts/Assembly-CSharp/PC/Component/Software/PerformanceGraph.cs').read_text()
# Arrows must not be children of the bottom bar, which is hidden for static images.
assert 'MakeButton(viewRt, "Previous"' in viewer
assert 'MakeButton(viewRt, "Next"' in viewer
assert 'new Vector2(58f, 74f)' in viewer
assert 'new Vector2(right ? 1f : 0f, .5f)' in viewer
assert 'new Vector2(right ? -12f : 12f, 0f)' in viewer
assert 'barGo.SetActive(anim);' in viewer
assert 'previousButton.gameObject.SetActive(navigation)' in viewer
print('PASS: editable masked neutral Prefab and centered Viewer navigation')
