#!/usr/bin/env python3
"""Size the user's unmodified Roundadapted.png for Android 7.1 Round icon slots.

Legacy and Adaptive icons are intentionally untouched. Requires Pillow.
"""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = Image.open(root / 'tools/artwork/Roundadapted.png').convert('RGB')
assert source.size == (432, 432), 'Roundadapted.png must be the supplied 432x432 image'
out = root / 'Assets/AndroidIcons'
for size in (192, 144, 96, 72, 48, 36):
    source.resize((size, size), Image.Resampling.LANCZOS).save(out / f'RoundIcon_{size}.png')
print('Generated six Round icon densities from the supplied source')
