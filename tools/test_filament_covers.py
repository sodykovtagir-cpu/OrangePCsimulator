#!/usr/bin/env python3
"""Static/image regressions for the actual-FBX filament shop covers."""
from pathlib import Path
import hashlib
import importlib.util
import json
import re
import tempfile
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
ICONS = ROOT / 'Assets/Texture2D/Filaments'


def items():
    return sorted((ROOT / 'Assets/MonoBehaviour').glob('*Filament*.asset'))


def test_all_twenty_five_shop_items_use_their_own_imported_png_sprite():
    assert len(items()) == 25
    seen = set()
    for item in items():
        image = ICONS / (item.stem + '.png')
        assert image.exists(), image
        meta = Path(str(image) + '.meta').read_text()
        guid = re.search(r'^guid: (\w+)$', meta, re.M).group(1)
        assert len(guid) == 32 and guid not in seen
        seen.add(guid)
        assert 'spriteMode: 1' in meta
        assert 'textureType: 8' in meta
        assert 'enableMipMap: 0' in meta
        assert 'spriteGenerateFallbackPhysicsShape: 0' in meta
        assert 'sprite: {fileID: 21300000, guid: ' + guid + ', type: 3}' in item.read_text()
    assert len(list(ICONS.glob('*.png'))) == 25


def test_icons_are_finished_square_white_background_renders():
    hashes = set()
    for item in items():
        image = Image.open(ICONS / (item.stem + '.png')).convert('RGBA')
        assert image.size == (256, 256)
        assert image.getextrema()[3] == (255, 255)
        for corner in ((0, 0), (255, 0), (0, 255), (255, 255)):
            assert image.getpixel(corner) == (255, 255, 255, 255)
        rgb = image.convert('RGB')
        bbox = ImageChops.difference(rgb, Image.new('RGB', rgb.size, 'white')).getbbox()
        assert bbox and bbox[0] >= 4 and bbox[1] >= 4 and bbox[2] <= 252 and bbox[3] <= 252, (item.stem, bbox)
        assert max(bbox[2] - bbox[0], bbox[3] - bbox[1]) >= 190, (item.stem, bbox)
        digest = hashlib.sha256(image.tobytes()).hexdigest()
        assert digest not in hashes, 'Matte/glossy/color covers must not be duplicates: ' + item.stem
        hashes.add(digest)


def test_covers_use_real_fbx_geometry_not_placeholder_spools():
    script = (ROOT / 'tools/render_filament_covers.py').read_text()
    assert 'bpy.ops.import_scene.fbx(filepath=str(source))' in script
    assert "source = root / 'Assets/Models/Filament.fbx'" in script
    assert 'bpy.ops.mesh.' not in script
    assert "camera_data.type = 'ORTHO'" in script
    manifest = json.loads((ROOT / 'tools/filament_cover_render_manifest.json').read_text())
    assert {c['name'] for c in manifest['covers']} == {p.stem for p in items()}
    assert manifest['source_sha256'] == hashlib.sha256((ROOT / 'Assets/Models/Filament.fbx').read_bytes()).hexdigest()
    assert manifest['final_size'] == 256
    assert manifest['geometry']['Filament'] == {'vertices': 64, 'faces': 34}
    assert manifest['geometry']['Reel'] == {'vertices': 192, 'faces': 102}


def test_variant_generator_preserves_cover_and_asset_ids():
    spec = importlib.util.spec_from_file_location('filament_generator', ROOT / 'tools/generate_filament_variants.py')
    generator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(generator)
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder)
        generator.SHOP_ITEMS = path
        name = 'TestFilamentMatte'
        target = path / (name + '.asset')
        icon_guid = 'a' * 32
        asset_guid = 'b' * 32
        target.write_text('  sprite: {fileID: 21300000, guid: ' + icon_guid + ', type: 3}\n')
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + asset_guid + '\n')
        variant = {'prefab': name, 'key': 'Test Filament Matte', 'price': 45}
        returned_guid = generator.write_shop_item(variant, 'c' * 32)
        assert returned_guid == asset_guid
        assert 'sprite: {fileID: 21300000, guid: ' + icon_guid + ', type: 3}' in target.read_text()


if __name__ == '__main__':
    for name, fn in sorted(globals().copy().items()):
        if name.startswith('test_') and callable(fn):
            fn()
            print('PASS', name)
