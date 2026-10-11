#!/usr/bin/env python3
"""Source guards for Android adaptive OPC icon and external save import.

These do not replace an actual Unity 2022.3 Android build/Files app smoke test.
"""
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image

root = Path(__file__).resolve().parents[1]
settings = (root / 'ProjectSettings/ProjectSettings.asset').read_text()
assets = root / 'Assets'
fg = assets / 'AndroidIcons/AdaptiveForeground.png'
bg = assets / 'AndroidIcons/AdaptiveBackground.png'
fid = fg.with_suffix('.png.meta').read_text().split('guid: ')[1].splitlines()[0]
bid = bg.with_suffix('.png.meta').read_text().split('guid: ')[1].splitlines()[0]
section = settings.split('  m_BuildTargetPlatformIcons:')[1].split('  m_BuildTargetBatching:')[0]
android = section.split('  - m_BuildTarget: Android\n')[1]
assert android.count('m_Kind: 2') == 6
assert android.count(f'guid: {fid}') == android.count(f'guid: {bid}') == 6
assert 'b7905c449a3be404ea0bd4e199dd22b5' in settings  # legacy PC stays
for path in (fg, bg):
    with Image.open(path) as image:
        assert image.size == (432, 432) and image.mode == 'RGBA'
assert Image.open(fg).getpixel((0, 0))[3] == 0  # true separated foreground
artwork = ET.parse(root / 'tools/artwork/android_adaptive_foreground.svg').getroot()
svg = '{http://www.w3.org/2000/svg}'
assert artwork.attrib['viewBox'] == '0 0 640 640'
assert len(artwork.findall(svg + 'path')) == 8  # user's pixel-letter outlines
assert len(artwork.findall(svg + 'rect')) == 7  # original tile and bars
assert all(node.get('fill') == 'white' for node in artwork.findall(svg + 'path'))
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
print('PASS: legacy icon retained, layered source SVG icon configured, Android Open with imports validated .pc/.opc')
