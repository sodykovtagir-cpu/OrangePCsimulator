#!/usr/bin/env python3
"""Generate the Android-only layered OPC icon; legacy PC icon stays untouched.

Needs Pillow. Each layer is 432x432 (108dp at xxxhdpi). Foreground artwork
stays within the central 288px safe area so circular/squircle masks won't crop
OPC. Unity resizes these two source textures for the other adaptive slots.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
out = root / 'Assets' / 'AndroidIcons'
out.mkdir(exist_ok=True)
scale = 3  # antialias typography and rounded corners
size = 432
orange = (255, 120, 0, 255)
white = (255, 255, 255, 255)

def rect(draw, bounds, radius, fill):
    draw.rounded_rectangle(tuple(v * scale for v in bounds), radius=radius * scale, fill=fill)

background = Image.new('RGBA', (size * scale, size * scale), (43, 43, 43, 255))
background.resize((size, size), Image.Resampling.LANCZOS).save(out / 'OPCAdaptiveBackground.png')

fg = Image.new('RGBA', background.size, (0, 0, 0, 0))
draw = ImageDraw.Draw(fg)
# Same orange tile, two right blocks and rows as the original logo, but with
# room for three letters. Do not use the original PC JPEG as a foreground: its
# square charcoal backdrop would prevent adaptive masking and parallax.
rect(draw, (82, 91, 246, 278), 22, orange)
rect(draw, (260, 91, 347, 153), 18, orange)
rect(draw, (82, 292, 219, 343), 17, orange)
for y in (196, 234, 272, 310):
    rect(draw, (261, y, 347, y + 17), 8, orange)
font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 48 * scale)
# Center the OPC mark within the main tile, not within the launcher mask.
bbox = draw.textbbox((0, 0), 'OPC', font=font)
width = bbox[2] - bbox[0]
height = bbox[3] - bbox[1]
draw.text(((164 * scale - width / 2) - bbox[0],
           (185 * scale - height / 2) - bbox[1]), 'OPC', font=font, fill=white)
fg.resize((size, size), Image.Resampling.LANCZOS).save(out / 'OPCAdaptiveForeground.png')
print('Generated separate OPC foreground and charcoal background; legacy icon unchanged')
