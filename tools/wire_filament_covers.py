#!/usr/bin/env python3
"""Finalize rendered FBX covers and link them to every filament ShopItem.
Requires Pillow. Does not draw or synthesize geometry: all input PNGs must
already have been rendered by Blender from Assets/Models/Filament.fbx.
"""
from pathlib import Path
import re
import math
import hashlib
import json
import uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ICONS = ROOT / 'Assets/Texture2D/Filaments'
NAMESPACE = uuid.UUID('7bd869be-1770-4f60-8da6-28bce91e1d41')


def guid(name):
    return uuid.uuid5(NAMESPACE, name).hex


def main():
    template = (ROOT / 'Assets/Texture2D/GTX1070Ti.png.meta').read_text()
    folder_meta = Path(str(ICONS) + '.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + guid('Filaments folder') + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    items = sorted((ROOT / 'Assets/MonoBehaviour').glob('*Filament*.asset'))
    assert len(items) == 25, 'Review range changes before updating the icon set'
    for item in items:
        path = ICONS / (item.stem + '.png')
        assert path.exists(), 'Missing render: ' + str(path)
        image = Image.open(path).convert('RGBA')
        if image.size != (256, 256) and image.getextrema()[3][0] == 0:
            # Frame the actual silhouette, not the empty projected box corners.
            # Same model/camera -> same crop for all 25 material variants.
            bounds = image.getchannel('A').getbbox()
            side = math.ceil(max(bounds[2] - bounds[0], bounds[3] - bounds[1]) / 0.90)
            left = round((bounds[0] + bounds[2] - side) / 2)
            top = round((bounds[1] + bounds[3] - side) / 2)
            image = image.crop((left, top, left + side, top + side))
        # Identical opaque white backdrop to the existing 256x256 shop covers.
        background = Image.new('RGBA', image.size, (255, 255, 255, 255))
        background.alpha_composite(image)
        background = background.resize((256, 256), Image.Resampling.LANCZOS)
        background.save(path, optimize=True)
        meta = Path(str(path) + '.meta')
        icon_guid = re.search(r'^guid: (\w+)', meta.read_text(), re.M).group(1) if meta.exists() else guid(item.stem)
        if not meta.exists():
            text = re.sub(r'^guid: \w+', 'guid: ' + icon_guid, template, count=1, flags=re.M)
            text = re.sub(r'(spriteID:) \w+', r'\1 ' + guid(item.stem + ' Sprite'), text)
            text = text.replace('maxTextureSize: 2048', 'maxTextureSize: 256')
            text = text.replace('spriteGenerateFallbackPhysicsShape: 1', 'spriteGenerateFallbackPhysicsShape: 0')
            text = text.replace('textureCompression: 2', 'textureCompression: 0')
            meta.write_text(text)
        text = item.read_text()
        text, count = re.subn(r'^  sprite: \{[^}]+\}',
            '  sprite: {fileID: 21300000, guid: ' + icon_guid + ', type: 3}', text, count=1, flags=re.M)
        assert count == 1, item
        item.write_text(text)
        print('COVER_WIRED', item.stem, icon_guid)
    manifest_path = ROOT / 'tools/filament_cover_render_manifest.json'
    manifest = json.loads(manifest_path.read_text())
    manifest['source_sha256'] = hashlib.sha256((ROOT / 'Assets/Models/Filament.fbx').read_bytes()).hexdigest()
    manifest['final_size'] = 256
    manifest['framing'] = 'same silhouette crop, centered, 90 percent occupancy'
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')


if __name__ == '__main__':
    main()
