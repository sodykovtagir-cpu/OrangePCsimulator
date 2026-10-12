#!/usr/bin/env python3
"""Build color adaptive layers plus a separate Android 13+ themed glyph.

The ordinary PC launcher icon remains untouched. We use the user's text-free
block geometry, inset into the circular safe zone. Normal: orange-on-black;
monochrome: white-on-transparent, used ONLY for themed launcher icons.
Requires Pillow and either ImageMagick (magick) or cairosvg.
"""
from pathlib import Path
import shutil
import subprocess
from PIL import Image

root = Path(__file__).resolve().parents[1]
svg = root / 'tools' / 'artwork' / 'android_adaptive_foreground.svg'
out = root / 'Assets' / 'AndroidIcons'
out.mkdir(exist_ok=True)
size = 432
monochrome = out / 'AdaptiveMonochrome.png'

# Render the original text-free geometry with transparent surroundings.
if shutil.which('magick'):
    subprocess.run(['magick', '-background', 'none', '-density', '216',
                    str(svg), '-resize', f'{size}x{size}', '-depth', '8',
                    'PNG32:' + str(monochrome)], check=True)
else:
    import cairosvg
    cairosvg.svg2png(url=str(svg), write_to=str(monochrome),
                     output_width=size, output_height=size)

# The original blocks nearly filled the round mask. Keep ALL opaque pixels
# within Android's central ~66dp safe circle (108dp icon): radius <= 0.61 of
# the icon radius. Original maximal radius was ~0.95; use 0.60 for padding.
source = Image.open(monochrome).convert('RGBA')
inset = round(size * 0.60)
scaled = source.resize((inset, inset), Image.Resampling.LANCZOS)
mono = Image.new('RGBA', (size, size), (0, 0, 0, 0))
mono.alpha_composite(scaled, ((size - inset) // 2, (size - inset) // 2))
mono.save(monochrome)

# The system colors the monochrome glyph when themed icons are enabled; the
# normal adaptive icon must remain orange. Keep both masks identical.
foreground = Image.new('RGBA', (size, size), (253, 121, 2, 255))
foreground.putalpha(mono.getchannel('A'))
foreground.save(out / 'AdaptiveForeground.png')
Image.new('RGBA', (size, size), (0, 0, 0, 255)).save(out / 'AdaptiveBackground.png')

source_bg = Image.open(out / 'AdaptiveBackground.png').convert('RGBA')
for density in (324, 216, 162, 108, 81):
    foreground.resize((density, density), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveForeground_{density}.png')
    source_bg.resize((density, density), Image.Resampling.LANCZOS).save(
        out / f'AdaptiveBackground_{density}.png')
print('Generated inset orange adaptive artwork and separate white themed glyph')
