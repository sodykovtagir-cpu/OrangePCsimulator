#!/usr/bin/env python3
"""Static links for the new pillow print site. Unity import and Play Mode remain required."""
from pathlib import Path
import re
import yaml

root = Path(__file__).resolve().parents[1]
a = root / 'Assets'
shop = a / 'GameObject/DakimakuraExpert.prefab'
item = a / 'Resources/components/Dakimakura.prefab'
model = a / 'Models/dakimakura.fbx'
site = shop.read_text()
product = item.read_text()
browser = (a / 'Resources/apps/Browser.prefab').read_text()
code = (a / 'Scripts/Assembly-CSharp/PC/Component/Software/PrintExpert.cs').read_text()
visual = (a / 'Scripts/Assembly-CSharp/PC/Component/Software/DakimakuraVisual.cs').read_text()
loader = (a / 'Scripts/Assembly-CSharp/TextureLoader.cs').read_text()


def guid(path):
    return re.search(r'^guid: ([0-9a-f]{32})$', path.with_suffix(path.suffix + '.meta').read_text(), re.M).group(1)


def blocks(text):
    result = {}
    for b in re.split(r'(?=^--- !u!\d+ &\d+\n)', text, flags=re.M):
        m = re.match(r'^--- !u!\d+ &(\d+)\n', b)
        if not m: continue
        assert m.group(1) not in result
        yaml.safe_load(re.sub(r'^--- !u!\d+ &\d+', '---', b, count=1))
        result[m.group(1)] = b
    return result


site_objects = blocks(site)
item_objects = blocks(product)
blocks(browser)
assert model.is_file() and b'LayerElementUV' in model.read_bytes()
assert '    importCameras: 0' in model.with_suffix('.fbx.meta').read_text()
assert '    importLights: 0' in model.with_suffix('.fbx.meta').read_text()
assert f'modelPrefab: {{fileID: 100000, guid: {guid(model)}, type: 3}}' in product
assert f'm_Script: {{fileID: 11500000, guid: {guid(a / "Scripts/Assembly-CSharp/PC/Component/Software/DakimakuraVisual.cs")}, type: 3}}' in product
assert '  spawnId: Dakimakura' in product and '  info: Dakimakura' in product
assert 'rend: {fileID: 0}' in product and 'BindRenderer(renderer)' in visual
assert 'public void BindRenderer(Renderer target)' in loader
assert 'return new Bounds((min + max) * .5f, max - min);' in visual
assert 'GetComponentInChildren<MeshFilter>(true)' in visual
assert 'public bool Ready { get; private set; }' in visual
assert 'if (dakimakura != null && !dakimakura.Ready)' in code
assert code.index('if (dakimakura != null && !dakimakura.Ready)') < code.index('m.Spend(price);')
assert 'm_Name: DakimakuraExpert' in site
assert 'websiteName: Dakimakura Expert' in site
assert f'bannerPrefab: {{fileID: 855660000000000001, guid: {guid(item)}, type: 3}}' in site
assert f'icon: {{fileID: 21300000, guid: {guid(a / "Texture2D/dakimakura_expert.png")}, type: 3}}' in site
assert 'm_MethodName: Purchase' in site and 'm_MethodName: SelectFile' in site
assert 'm_Text: HD +500$' in site and 'm_Text: 1024x2240' in site
assert 'm_Text: Print a custom dakimakura from your picture' in site
assert 'bannerW = 32' in code and 'bannerH = 70' in code
assert 'bannerHdW = 1024' in code and 'bannerHdH = 2240' in code
assert 'hdSurcharge = 500' in code and 'bannerPrice = 200' in code
assert 'm.InstantDelivery(bannerPrefab.gameObject)' in code
assert 'loader.SetTexture(tex, data)' in code and 'loader.SetTexture(previewTexture, null)' in code
assert 'previewRigPrefab:' in site and 'previewImage:' in site
assert f'page: {{fileID: 114021143164813130, guid: {guid(shop)}, type: 3}}' in browser
assert '  - url: www.dakimakuraexpert.com' in browser
assert '  - url: www.printexpert.com' in browser
print('PASS: dakimakura .pic/HD/preview/delivery/save wiring and distinct catalog/item prefabs')
