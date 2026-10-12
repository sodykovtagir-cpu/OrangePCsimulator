#!/usr/bin/env python3
"""Render text-free white blocks on black for the Android adaptive icon.

The ordinary PC launcher icon is intentionally untouched. Source artwork:
tools/artwork/android_adaptive_foreground.svg. Export size: 432x432 px (xxxhdpi).
Requires Pillow and either ImageMagick (magick) or cairosvg for SVG rendering.
"""
from pathlib import Path
import shutil
import subprocess
from PIL import Image

root = Path(__file__).resolve().parents[1]
svg = root / 'tools' / 'artwork' / 'android_adaptive_foreground.svg'
out = root / 'Assets' / 'AndroidIcons'
out.mkdir(exist_ok=True)
foreground = out / 'AdaptiveForeground.png'

# Preserve the user's original block geometry and rounded corners.
# Lettering was removed from the foreground by request.
if shutil.which('magick'):
    subprocess.run(['magick', '-background', 'none', '-density', '216',
                    str(svg), '-resize', '432x432', '-depth', '8',
                    'PNG32:' + str(foreground)], check=True)
else:
    import cairosvg
    cairosvg.svg2png(url=str(svg), write_to=str(foreground),
                     output_width=432, output_height=432)

# Separate background is needed for Android's adaptive mask. Do not bake it
# into the foreground: that would make a rectangular tile under round masks.
Image.new('RGBA', (432, 432), (0, 0, 0, 255)).save(out / 'AdaptiveBackground.png')
source_fg = Image.open(foreground).convert('RGBA')
source_bg = Image.open(out / 'AdaptiveBackground.png').convert('RGBA')
for size in (324, 216, 162, 108, 81):
    source_fg.resize((size, size), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveForeground_{size}.png')
    source_bg.resize((size, size), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveBackground_{size}.png')
print('Generated white blocks on black at all six Android adaptive densities')
