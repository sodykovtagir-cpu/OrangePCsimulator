#!/usr/bin/env python3
"""Render the user's exact pixel-letter SVG as the Android adaptive foreground.

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

# ImageMagick renders the supplied paths and round corners without converting
# the in-game-style pixel lettering into a system font or redrawing the logo.
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
Image.new('RGBA', (432, 432), (43, 43, 43, 255)).save(out / 'AdaptiveBackground.png')
print('Generated exact SVG foreground and separate dark adaptive background')
