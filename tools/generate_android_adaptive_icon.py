#!/usr/bin/env python3
"""Generate Android Adaptive layers from Roundadapted.png and a separate themed glyph.

Normal launcher icons use the supplied gray background, orange PC mark and
long shadow. The shadow is part of the transparent foreground, so Android's
round/squircle masks clip it naturally. The no-letter white glyph is ONLY
used for themed Android 13+ icons. Requires Pillow and ImageMagick or CairoSVG.
"""
from pathlib import Path
import shutil
import subprocess
from PIL import Image

root = Path(__file__).resolve().parents[1]
svg = root / 'tools' / 'artwork' / 'android_adaptive_foreground.svg'
artwork = root / 'tools' / 'artwork' / 'Roundadapted.png'
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

# Extract the supplied artwork and its shadow from its perfectly flat gray
# background. We do not redraw the shadow separately at each density: it and
# the PC mark scale together as a single foreground. Retaining the original
# antialiased edge colors gives the exact image when composited on #5c5c5c.
with Image.open(artwork) as im:
    supplied = im.convert('RGB')
assert supplied.size == (size, size), 'Roundadapted.png must be 432x432'
background_color = supplied.getpixel((0, 0))
assert background_color == (92, 92, 92), 'Unexpected Roundadapted.png background'
foreground = supplied.convert('RGBA')
alpha = Image.new('L', (size, size), 0)
src = supplied.load()
a = alpha.load()
for y in range(size):
    for x in range(size):
        if src[x, y] != background_color:
            a[x, y] = 255
foreground.putalpha(alpha)
background = Image.new('RGBA', (size, size), (*background_color, 255))

for density in (432, 324, 216, 162, 108, 81):
    suffix = '' if density == size else f'_{density}'
    foreground.resize((density, density), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveForeground{suffix}.png')
    background.resize((density, density), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveBackground{suffix}.png')
print('Generated Round artwork Adaptive layers at six sizes and unchanged themed glyph')
