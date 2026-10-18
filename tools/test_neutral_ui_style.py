#!/usr/bin/env python3
"""Static guard for the PCOS UI palette; Play Mode is still needed for pixels."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
base = root / 'Assets'
ui_files = [*base.glob('Scenes/*.unity'), *base.glob('Resources/apps/*.prefab'),
            *base.glob('GameObject/*.prefab'),
            base / 'Scripts/Assembly-CSharp/PC/Component/OggleResultPrefab.prefab']
color = re.compile(r'\b(m_(?:Color|NormalColor|HighlightedColor|PressedColor|SelectedColor)):'
                   r' \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)')
for file in ui_files:
    if file.name == '3DMork_Room.unity' or not file.exists():
        continue  # world light, not UI
    for lineno, line in enumerate(file.read_text().splitlines(), 1):
        m = color.search(line)
        if m:
            r, g, b = map(float, m.groups()[1:])
            assert not (.8 < r <= 1.1 and .23 <= g < .85 and b < g * .65 and r > g + .18), (file, lineno)

scripts = base / 'Scripts/Assembly-CSharp'
for file in scripts.rglob('*.cs'):
    s = file.read_text()
    assert '<color=orange>' not in s.lower(), file
    assert '#ff8800' not in s.lower(), file
oc = (scripts / 'PC/Component/Software/Overclock.cs').read_text()
prefab = (base / 'Resources/apps/Overclock.prefab').read_text()
assert 'MatchStock(bg, StockChild(stock, "Background")' in oc
assert 'stock.handleRect.GetComponent<Image>()' in oc
assert 'slider.fillRect = fill;' in oc and 'slider.handleRect = handle;' in oc
assert 'MakeSlider(root, "GPU ratio",' in oc and 'controlBar' in oc
assert 'new Vector2(45f, -369f)' in oc and 'new Vector2(45f, -338f)' in oc
assert 'm_AnchoredPosition: {x: 140, y: -237}' in prefab
assert '<color=orange>' not in (base / 'Scenes/Menu.unity').read_text().lower()
print('PASS: neutral UI palette, stock Overclock controls, compact maximized layout')
