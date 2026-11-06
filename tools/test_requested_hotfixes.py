#!/usr/bin/env python3
"""Guards for Nov user bug batch. Static checks only; Unity/device tests needed."""
from pathlib import Path
root = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
read = lambda name: (root / name).read_text(encoding='utf-8')
plug = read('PC/Component/Cabling/CablePlug.cs')
rope = read('PC/Component/Cabling/CableItem.cs')
explorer = read('PC/Component/Software/FileManager.cs')
paint = read('PC/Component/Software/Paint.cs')
menu = read('FileMenu.cs')
main_menu = read('MainMenu.cs')
workshop = read('WorkshopMenu.cs')
info = read('FileInformation.cs')
assert 'manualReinsertPort = old;' in plug and 'if (held) manualReinsertPort = null;' in plug
assert 'port == manualReinsertPort && !IsHeld' in plug
assert 'release.Unplug(true);' in rope and 'candidate.Unplug(true);' in rope
assert 'kind == CableKind.LAN && span < length * .95f' in rope
assert 'IsProtectedFile(file, false)' in explorer
assert 'hiddenToggle.SetIsOnWithoutNotify(file.hidden);' in explorer
assert 'pixels[i].a != 255' in paint and 'ImageConversion.EncodeToPNG(texture)' in paint
assert 'new Texture2D(2, 2, TextureFormat.RGBA32, false)' in read('FormatConverter.cs')
assert 'gridImage.color = on ? Color.red : gridOffColor;' in read('Functions.cs')
assert 'StartCoroutine(FileMenu.PreloadHeaders());' in main_menu
assert 'private void OnEnable()' in menu and 'RebuildList();' in menu
assert 'cached.size == info.Length && cached.modifiedUtc == info.LastWriteTimeUtc' in menu
assert 'headerCache.Remove(path);' in menu
assert 'ScreenshotCover(GameData data)' in workshop
assert 'CoverForSave(path)' in workshop and 'ScreenshotCover(load.loader.GameData)' in info
assert 'screenshotPreview != null ? screenshotPreview : CoverPlaceholder()' in info
marquee = read('PassiveDescriptionMarquee.cs')
assert 'editor != null && editor.isFocused' in marquee
assert 'Time.unscaledTime + .22f' in marquee
assert 'PassiveDescriptionMarquee.Attach(signNameText, signInput)' in info
assert 'PassiveDescriptionMarquee.Attach(description.GetComponent<Text>()' in workshop
print('Requested LAN/file/PNG/grid/save/cache/cover/description guards: OK (static only)')
