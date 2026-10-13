#!/usr/bin/env python3
"""Scale the user's separate Adaptive background/foreground and themed glyph.

Normal launcher icons: the supplied gray background contains the long shadow;
the transparent foreground contains the orange PC mark. Android masks and
moves these two layers separately. The text-free white glyph is ONLY used for
Android 13+ themed icons. Requires Pillow and ImageMagick or CairoSVG.
"""
from pathlib import Path
import shutil
import subprocess
from PIL import Image

root = Path(__file__).resolve().parents[1]
svg = root / 'tools' / 'artwork' / 'android_adaptive_foreground.svg'
background_artwork = root / 'tools' / 'artwork' / 'Adaptive background.png'
foreground_artwork = root / 'tools' / 'artwork' / 'Adaptive foreground.png'
out = root / 'Assets' / 'AndroidIcons'
out.mkdir(exist_ok=True)
size = 432
monochrome = out / 'AdaptiveMonochrome.png'

# Theme-only monochrome layer: keep the previous inset, text-free silhouette.
if shutil.which('magick'):
    subprocess.run(['magick', '-background', 'none', '-density', '216',
                    str(svg), '-resize', f'{size}x{size}', '-depth', '8',
                    'PNG32:' + str(monochrome)], check=True)
else:
    import cairosvg
    cairosvg.svg2png(url=str(svg), write_to=str(monochrome),
                     output_width=size, output_height=size)
source = Image.open(monochrome).convert('RGBA')
inset = round(size * 0.60)
scaled = source.resize((inset, inset), Image.Resampling.LANCZOS)
mono = Image.new('RGBA', (size, size), (0, 0, 0, 0))
mono.alpha_composite(scaled, ((size - inset) // 2, (size - inset) // 2))
mono.save(monochrome)

# Keep the original layer boundary and antialiasing: the background contains
# the silhouette + long shadow, the foreground holds the translucent PC mark.
# A single resampling operation per density automatically scales both layers.
with Image.open(background_artwork) as im:
    background = im.convert('RGBA')
with Image.open(foreground_artwork) as im:
    foreground = im.convert('RGBA')
assert background.size == foreground.size == (size, size), 'Adaptive sources must be 432x432'
assert background.getchannel('A').getextrema() == (255, 255), 'Background must be opaque'
assert foreground.getpixel((0, 0))[3] == 0, 'Foreground must be transparent'

for density in (432, 324, 216, 162, 108, 81):
    suffix = '' if density == size else f'_{density}'
    foreground.resize((density, density), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveForeground{suffix}.png')
    background.resize((density, density), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveBackground{suffix}.png')
print('Generated six Adaptive sizes from supplied separate layers; themed glyph unchanged')
