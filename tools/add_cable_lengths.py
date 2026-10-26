#!/usr/bin/env python3
"""Add non-conflicting cable SKUs while preserving old prefab/spawn identifiers.

Only creates missing files. Author-made plug meshes, pivots, materials and
colliders are copied from the shortest reference cable of the same type.
Run from anywhere; existing user assets are never overwritten.
"""
from pathlib import Path
import math
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
COMPONENTS = ROOT / 'Assets/Resources/components'
ITEMS = ROOT / 'Assets/MonoBehaviour'
SHOP = ITEMS / 'Shop.asset'
# Source prefab, new lengths, and linear shop prices matching existing prices.
FAMILIES = {
    'Power': ('Power Cable 1.5m', (1, 5, 15), 30, 7.5),
    'VGA': ('VGA Cable 1.5m', (1, 5, 15), 29.5, 7.5),
    'HDMI': ('HDMI Cable 2m', (2, 6, 16), 30, 7.5),
}


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'OrangePCsimulator/cable-length-options/' +
                      path.relative_to(ROOT).as_posix()).hex


def replace_one(text, before, after):
    if text.count(before) != 1:
        raise ValueError(f'expected one occurrence of {before!r}, found {text.count(before)}')
    return text.replace(before, after, 1)


def main():
    for kind, (reference, lengths, base_price, rate) in FAMILIES.items():
        template = COMPONENTS / (reference + '.prefab')
        original = template.read_text()
        item_template = (ITEMS / (reference + '.asset')).read_text()
        for meters in lengths:
            name = f'{kind} Cable ({meters}m)'  # legacy names use other lengths
            prefab = COMPONENTS / (name + '.prefab')
            item = ITEMS / (name + '.asset')
            files = (prefab, prefab.with_suffix('.prefab.meta'),
                     item, item.with_suffix('.asset.meta'))
            if all(path.exists() for path in files):
                print('already exists:', name)
                continue
            if any(path.exists() for path in files):
                raise ValueError(f'{name}: partial asset set, inspect manually')
            count = max(7, min(26, round(meters / .45)))
            turns = 3 if count >= 24 and meters > 12 else 2 if count >= 11 else 1
            spread = min(.5, meters * .15)
            radius = (meters / count) / (2 * math.sin(math.pi * turns / count))
            points = []
            for i in range(count + 1):
                angle = 2 * math.pi * turns * i / count
                x = radius * math.cos(angle) + spread * (i / count - .5)
                z = radius * math.sin(angle)
                points.append(f'  - {{x: {x:.7f}, y: 0.45, z: {z:.7f}}}\n')

            body = replace_one(original, f'  m_Name: {reference}\n', f'  m_Name: {name}\n')
            body = replace_one(body, f'  spawnId: {reference}\n', f'  spawnId: {name}\n')
            body = re.sub(r'(?m)^  info: .*$', f'  info: {kind} Cable {meters}m', body, count=1)
            body, n = re.subn(r'(?m)^  length: [0-9.]+$', f'  length: {meters}', body, count=1)
            if n != 1: raise ValueError(f'{name}: missing prefab length')
            body, n = re.subn(r'(?m)^  segments: \d+$', f'  segments: {count}', body, count=1)
            if n != 1: raise ValueError(f'{name}: missing segment count')
            body, n = re.subn(r'(?ms)^  m_Positions:\n.*?(?=^  m_Parameters:)',
                              '  m_Positions:\n' + ''.join(points), body, count=1)
            if n != 1: raise ValueError(f'{name}: missing jacket positions')
            # Some legacy templates have trailing spaces on empty YAML fields.
            body = re.sub(r'(?m)[ \t]+$', '', body)
            prefab.write_text(body)
            prefab_meta = template.with_suffix('.prefab.meta').read_text()
            prefab.with_suffix('.prefab.meta').write_text(
                re.sub(r'(?m)^guid: [0-9a-f]{32}$', 'guid: ' + guid(prefab), prefab_meta, count=1))

            item_body = replace_one(item_template, f'  m_Name: {reference}\n',
                                    f'  m_Name: {name}\n')
            item_body, n = re.subn(r"(?m)^  itemName: .*?$", f"  itemName: '{{{kind} Cable}} {meters}m'",
                                   item_body, count=1)
            if n != 1: raise ValueError(f'{name}: missing shop name')
            item_body, n = re.subn(r'(?m)^  price: \d+$',
                                   f'  price: {round(base_price + rate * meters)}', item_body, count=1)
            if n != 1: raise ValueError(f'{name}: missing shop price')
            item_body, n = re.subn(r'(?m)^(  spawn: \{fileID: 100000, guid: )[0-9a-f]{32}',
                                   lambda m: m.group(1) + guid(prefab), item_body, count=1)
            if n != 1: raise ValueError(f'{name}: missing spawn link')
            description = (f'Plug A goes to a mounted outlet; Plug B goes to the device. Length {meters} m.'
                           if kind == 'Power' else
                           f'Drag either plug to a compatible socket. Length {meters} m.')
            item_body, n = re.subn(r'(?m)^  description: .*$',
                                   '  description: ' + description, item_body, count=1)
            if n != 1: raise ValueError(f'{name}: missing shop description')
            item.write_text(item_body)
            item_meta = (ITEMS / (reference + '.asset.meta')).read_text()
            item.with_suffix('.asset.meta').write_text(
                re.sub(r'(?m)^guid: [0-9a-f]{32}$', 'guid: ' + guid(item), item_meta, count=1))
            print('added:', name)

    shop_text = SHOP.read_text()
    page = re.search(r'(?ms)^  - pageName: Cables & Power\n    item:\n(.*?)(?=^  - pageName:|\Z)', shop_text)
    if page is None: raise ValueError('missing Cables & Power shop page')
    old_guids = re.findall(r'^    - \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}$',
                           page.group(1), re.M)
    if len(old_guids) not in (12, 21):
        raise ValueError(f'unexpected shop page size: {len(old_guids)}')
    entries = []
    for kind, (reference, lengths, _, _) in FAMILIES.items():
        for length in lengths:
            entries.append((kind, length, guid(ITEMS / f'{kind} Cable ({length}m).asset')))
        for path in ITEMS.glob(kind + ' Cable *.asset'):
            if path.name.startswith(kind + ' Cable ('): continue
            content = path.read_text()
            match = re.search(rf"^  itemName: '\{{{kind} Cable\}} (\d+)m'$", content, re.M)
            if not match: continue
            item_guid = re.search(r'^guid: ([0-9a-f]{32})$',
                                  path.with_suffix('.asset.meta').read_text(), re.M).group(1)
            entries.append((kind, int(match.group(1)), item_guid))
    new_guids = [g for kind in FAMILIES for _, _, g in sorted(
        (entry for entry in entries if entry[0] == kind), key=lambda entry: entry[1])]
    # The three author-made outlet products retain their existing links/order.
    outlet_guids = [g for g in old_guids if g not in new_guids]
    if len(new_guids) != 18 or len(set(new_guids)) != 18 or len(outlet_guids) != 3:
        raise ValueError('wrong cable/outlet catalog composition')
    new_page = '  - pageName: Cables & Power\n    item:\n' + ''.join(
        f'    - {{fileID: 11400000, guid: {g}, type: 2}}\n' for g in new_guids + outlet_guids)
    shop_text = shop_text[:page.start()] + new_page + shop_text[page.end():]
    SHOP.write_text(shop_text)
    print('shop: 18 cable lengths and 3 outlet variants')


if __name__ == '__main__':
    main()
