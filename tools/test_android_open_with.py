#!/usr/bin/env python3
"""Source guards for normal-color and themed Android icons, plus save import.

These do not replace an actual Unity 2022.3 Android build/Files app smoke test.
"""
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageChops

root = Path(__file__).resolve().parents[1]
settings = (root / 'ProjectSettings/ProjectSettings.asset').read_text()
assets = root / 'Assets'
section = settings.split('  m_BuildTargetPlatformIcons:')[1].split('  m_BuildTargetBatching:')[0]
android = section.split('  - m_BuildTarget: Android\n')[1]
import re
slots = re.findall(r'    - m_Textures:\n((?:      - \{[^\n]+\}\n)+)      m_Width: (\d+)\n      m_Height: (\d+)\n      m_Kind: (\d+)', android)
assert len(slots) == 18, 'all six Legacy, Round and Adaptive slots must be filled'
legacy_guid = 'b7905c449a3be404ea0bd4e199dd22b5'
for textures, width, height, kind in slots:
    size = int(width)
    assert size == int(height)
    guids = re.findall(r'guid: ([0-9a-f]{32})', textures)
    if int(kind) == 0:
        assert guids == [legacy_guid], 'keep the original PC icon in Legacy'
    elif int(kind) == 1:
        path = assets / f'AndroidIcons/RoundIcon_{size}.png'
        assert len(guids) == 1 and 'guid: ' + guids[0] in path.with_suffix('.png.meta').read_text()
        with Image.open(path) as im:
            assert im.size == (size, size) and im.mode == 'RGB'
            with Image.open(root / 'tools/artwork/Roundadapted.png') as supplied:
                assert im.tobytes() == supplied.convert('RGB').resize(
                    (size, size), Image.Resampling.LANCZOS).tobytes()
    else:
        assert int(kind) == 2 and len(guids) == 2
        suffix = '' if size == 432 else '_' + str(size)
        # Unity Player Settings screenshot confirms: slot 0 Background, slot 1 Foreground.
        for layer, guid in [('Background', guids[0]), ('Foreground', guids[1])]:
            path = assets / ('AndroidIcons/Adaptive' + layer + suffix + '.png')
            assert path.exists()
            assert 'guid: ' + guid in path.with_suffix('.png.meta').read_text()
            with Image.open(path) as im:
                assert im.size == (size, size) and im.mode == 'RGBA'
                if layer == 'Foreground':
                    assert im.getpixel((round(size * 155 / 432), round(size * 155 / 432)))[0] > 240  # orange PC mark
                    assert im.getpixel((round(size * 150 / 432), round(size * 180 / 432)))[0] > 240  # white lettering
                    shadow = im.getpixel((round(size * 315 / 432), round(size * 300 / 432)))
                    assert max(shadow[:3]) < 65 and shadow[3] > 245, 'long shadow survives scaling'
                else:
                    assert im.getpixel((0, 0)) == (92, 92, 92, 255)
        with Image.open(assets / ('AndroidIcons/AdaptiveForeground' + suffix + '.png')) as fg:
            assert fg.getpixel((0, 0))[3] == 0
            if size == 432:
                with Image.open(assets / 'AndroidIcons/AdaptiveBackground.png') as bg:
                    combined = Image.alpha_composite(bg.convert('RGBA'), fg.convert('RGBA')).convert('RGB')
                with Image.open(root / 'tools/artwork/Roundadapted.png') as supplied:
                    assert ImageChops.difference(combined, supplied.convert('RGB')).getbbox() is None
assert [sum(int(slot[3]) == kind for slot in slots) for kind in (0, 1, 2)] == [6, 6, 6]
assert 'Roundadapted.png' in (root / 'tools/generate_android_round_icon.py').read_text()
mono = assets / 'AndroidIcons/AdaptiveMonochrome.png'
with Image.open(mono) as monochrome:
    monochrome = monochrome.convert('RGBA')
    assert monochrome.size == (432, 432)
    assert monochrome.getpixel((176, 176)) == (255, 255, 255, 255)
    # Entire glyph is inside the Android circular safe zone, even for motion.
    painted = [(x, y) for y in range(432) for x in range(432)
               if monochrome.getpixel((x, y))[3] > 10]
    assert max(((x - 216) ** 2 + (y - 216) ** 2) ** .5 / 216 for x, y in painted) < .60
assert Image.open(assets / 'AndroidIcons/AdaptiveForeground.png').getpixel((150, 180))[:3] == (255, 255, 255)
assert 'Roundadapted.png' in (root / 'tools/generate_android_adaptive_icon.py').read_text()
postprocess = (assets / 'Editor/AndroidThemedIconPostprocess.cs').read_text()
for required in ('IPostGenerateGradleAndroidProject', 'launcher", "src", "main", "res',
                 'doc.Root.Name.LocalName != "adaptive-icon"',
                 'new XElement("monochrome"', 'folder.Substring(0, folder.Length - 3) + "v33"',
                 'orange_pc_monochrome.png'):
    assert required in postprocess, required
assert 'doc.Save(Path.Combine(v33Folder, Path.GetFileName(file)))' in postprocess
editor = (assets / 'Editor/EnsureAndroidIcons.cs').read_text()
for required in ('IPreprocessBuildWithReport', 'AndroidPlatformIconKind.Legacy',
                 'AndroidPlatformIconKind.Round', 'AndroidPlatformIconKind.Adaptive',
                 'PlayerSettings.SetPlatformIcons', 'icons[i].SetTextures(new[] { background, foreground })'):
    assert required in editor, required
assert 'AssignRoundIcons();' in editor and '"Assets/AndroidIcons/RoundIcon_" + size + ".png"' in editor
assert 'AssignSingleLayer(AndroidPlatformIconKind.Round, original)' not in editor
artwork = ET.parse(root / 'tools/artwork/android_adaptive_foreground.svg').getroot()
svg = '{http://www.w3.org/2000/svg}'
assert artwork.attrib['viewBox'] == '0 0 640 640'
assert len(artwork.findall(svg + 'path')) == 0  # no lettering on adaptive icon
assert len(artwork.findall(svg + 'rect')) == 7  # original tile and bars
assert all(node.get('fill') == 'white' for node in artwork.findall(svg + 'rect'))
assert 'android_adaptive_foreground.svg' in (root / 'tools/generate_android_adaptive_icon.py').read_text()

manifest = ET.parse(assets / 'Plugins/Android/AndroidManifest.xml').getroot()
ns = '{http://schemas.android.com/apk/res/android}'
activity = manifest.find('./application/activity')
assert activity.get(ns + 'exported') == 'true'
assert activity.get(ns + 'launchMode') == 'singleTask'
assert activity.get(ns + 'name') == 'com.unity3d.player.UnityPlayerActivity'
filters = activity.findall('intent-filter')
# UnityWebView's postprocessor searches the unityLibrary manifest for this
# exact MAIN + LAUNCHER pair; without it SetHardwareAccelerated dereferences
# null at UnityWebViewPostprocessBuild.cs:300, then Unity deploy can't launch.
assert len(filters) == 2  # both intents on ONE activity, not a second icon
launch, view = filters
assert [a.get(ns + 'name') for a in launch.findall('action')] == ['android.intent.action.MAIN']
assert [c.get(ns + 'name') for c in launch.findall('category')] == ['android.intent.category.LAUNCHER']
assert any(el.get(ns + 'name') == 'android.intent.action.VIEW' for el in view.findall('action'))
assert 'application/octet-stream' in [el.get(ns + 'mimeType') for el in view.findall('data')]
plugin = (assets / 'Plugins/Editor/UnityWebViewPostprocessBuild.cs').read_text()
assert 'GetActivityWithLaunchIntent()' in plugin and 'SetHardwareAccelerated(true)' in plugin
assert 'useCustomMainManifest: 1' in settings
java = (assets / 'Plugins/Android/OrangeSaveOpen.java').read_text()
for guard in ('activity.getIntent()', 'activity.setIntent(new Intent(Intent.ACTION_MAIN))',
              'OpenableColumns.DISPLAY_NAME', 'openInputStream(uri)', '".opc"', '".pc"'):
    assert guard in java, guard
importer = (assets / 'Scripts/Assembly-CSharp/SaveManagement/SaveFileImport.cs').read_text()
assert importer.index('loader.LoadFromPath()') < importer.index('File.Copy(source, destination)')
assert 'SaveUtility.GetNewPath(baseName)' in importer
file_menu = (assets / 'Scripts/Assembly-CSharp/FileMenu.cs').read_text()
bridge = (assets / 'Scripts/Assembly-CSharp/AndroidSaveOpen.cs').read_text()
assert 'SaveFileImport.Import(path, System.IO.Path.GetFileName(path))' in file_menu
assert 'SaveFileImport.Import(cached, name)' in bridge
assert 'FileMenu.Instance.RefreshFromDisk()' in bridge
print('PASS: supplied Round and matching shadowed Adaptive at six sizes, separate themed glyph, Legacy and Open with guards')
