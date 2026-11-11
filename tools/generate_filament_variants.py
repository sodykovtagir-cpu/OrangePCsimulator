#!/usr/bin/env python3
"""Generate the coloured filament spool prefabs + their shop entries.

The white spool prefab authored in Unity is used as the template, so every
variant keeps the same model, collider, Rigidbody, Item and Slot setup.
Run from the repository root:  python3 tools/generate_filament_variants.py
"""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
COMPONENTS = ROOT / "Assets/Resources/components"
SHOP_ITEMS = ROOT / "Assets/MonoBehaviour"
TEMPLATE = COMPONENTS / "WhiteFilament.prefab"
SHOP_ASSET = SHOP_ITEMS / "Shop.asset"
TRANSLATE = ROOT / "Assets/Resources/Translate.txt"
SHOP_ITEM_SCRIPT_GUID = "484ef020c3930db604de1872bd7b83d7"

# name, hex, russian name
COLORS = [
    ("White", "FFFFFF", "Белый"),
    ("Black", "202020", "Чёрный"),
    ("Gray", "8C9196", "Серый"),
    ("Red", "E23B2E", "Красный"),
    ("Orange", "F58220", "Оранжевый"),
    ("Yellow", "F5D21E", "Жёлтый"),
    ("Green", "33B54A", "Зелёный"),
    ("Blue", "2266E0", "Синий"),
    ("Cyan", "22C6D8", "Голубой"),
    ("Purple", "8B4FD4", "Фиолетовый"),
    ("Pink", "F272A8", "Розовый"),
    ("Brown", "8A5A2E", "Коричневый"),
]
GLOW = ("Glow", "B8FF59", "Светящийся")
FINISHES = [("Matte", 0, "матовый", 45), ("Glossy", 1, "глянцевый", 60)]


def hex_to_rgb(value):
    return tuple(int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def color_yaml(value):
    r, g, b = hex_to_rgb(value)
    return "{r: %.6g, g: %.6g, b: %.6g, a: 1}" % (r, g, b)


def write_meta(path, guid, importer):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    meta.write_text("fileFormatVersion: 2\nguid: %s\n%s" % (guid, importer), encoding="utf-8")


PREFAB_IMPORTER = ("PrefabImporter:\n  externalObjects: {}\n  userData: \n"
                   "  assetBundleName: \n  assetBundleVariant: \n")
ASSET_IMPORTER = ("NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def build_variants():
    template = TEMPLATE.read_text(encoding="utf-8")
    variants = []
    for name, hex_value, ru in COLORS:
        for finish, finish_id, ru_finish, price in FINISHES:
            variants.append({
                "prefab": "%sFilament%s" % (name, finish),
                "key": "%s Filament %s" % (name, finish),
                "ru": "%s филамент (%s)" % (ru, ru_finish),
                "hex": hex_value,
                "finish": finish_id,
                "glow_hex": "000000",
                "glow_intensity": 0,
                "price": price,
            })
    variants.append({
        "prefab": "GlowFilament",
        "key": "Glow Filament",
        "ru": "Светящийся филамент",
        "hex": GLOW[1],
        "finish": 2,
        "glow_hex": GLOW[1],
        "glow_intensity": 2.4,
        "price": 180,
    })
    return template, variants


def write_prefab(template, variant):
    text = template
    text = text.replace("      value: WhiteFilament\n", "      value: %s\n" % variant["prefab"])
    text = text.replace("  spawnId: WhiteFilament\n", "  spawnId: %s\n" % variant["prefab"])
    text = text.replace("  info: '{White Filament}'\n", "  info: '{%s}'\n" % variant["key"])
    glow_r, glow_g, glow_b = hex_to_rgb(variant["glow_hex"])
    finish_block = (
        "  filamentColor: %s\n"
        "  finish: %d\n"
        "  matteSmoothness: 0.12\n"
        "  glossySmoothness: 0.85\n"
        "  glowColor: {r: %.6g, g: %.6g, b: %.6g, a: 1}\n"
        "  glowIntensity: %.6g\n"
        "  glowLight: 1\n"
        "  glowLightIntensity: 0.7\n"
        "  glowLightRange: 1.4\n"
    ) % (color_yaml(variant["hex"]), variant["finish"], glow_r, glow_g, glow_b, variant["glow_intensity"])
    text = re.sub(r"  filamentColor: \{[^}]*\}\n", finish_block, text, count=1)
    path = COMPONENTS / (variant["prefab"] + ".prefab")
    path.write_text(text, encoding="utf-8")
    meta_path = Path(str(path) + ".meta")
    guid = re.search(r"^guid: (\w+)", meta_path.read_text(), re.M).group(1) if meta_path.exists() else uuid.uuid4().hex
    write_meta(path, guid, PREFAB_IMPORTER)
    return guid


def write_shop_item(variant, prefab_guid):
    path = SHOP_ITEMS / (variant["prefab"] + ".asset")
    # Preserve the authored/rendered cover when regenerating prices/variants.
    existing_sprite = re.search(r"^  sprite: (.+)$", path.read_text(), re.M) if path.exists() else None
    path.write_text("""%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: %s
  m_EditorClassIdentifier: 
  itemName: '{%s}'
  price: %d
  bitcoin: 0
  sprite: {fileID: 0}
  spawn: {fileID: 8984346000712170146, guid: %s, type: 3}
  large: 0
  translateDescription: 1
  description: 
""" % (SHOP_ITEM_SCRIPT_GUID, variant["prefab"], variant["key"], variant["price"], prefab_guid), encoding="utf-8")
    if existing_sprite:
        text = path.read_text().replace("  sprite: {fileID: 0}", "  sprite: " + existing_sprite.group(1), 1)
        path.write_text(text, encoding="utf-8")
    meta_path = Path(str(path) + ".meta")
    guid = re.search(r"^guid: (\w+)", meta_path.read_text(), re.M).group(1) if meta_path.exists() else uuid.uuid4().hex
    write_meta(path, guid, ASSET_IMPORTER)
    return guid


def add_shop_page(item_guids):
    text = SHOP_ASSET.read_text(encoding="utf-8")
    if "pageName: Filament" in text:
        print("shop page already exists")
        return
    page = "  - pageName: Filament\n    item:\n"
    page += "".join("    - {fileID: 11400000, guid: %s, type: 2}\n" % g for g in item_guids)
    if not text.endswith("\n"):
        text += "\n"
    SHOP_ASSET.write_text(text + page, encoding="utf-8")


def add_translations(variants):
    text = TRANSLATE.read_text(encoding="utf-8")
    lines = text.splitlines()
    header = lines[0].split("\t")
    cols = len(header) - 1
    ru_index = header.index("RU") - 1
    existing = {line.split("\t")[0].lstrip("\ufeff") for line in lines if line}
    added = []
    for variant in variants:
        if variant["key"] in existing:
            continue
        values = [""] * cols
        values[0] = variant["key"]
        values[ru_index] = variant["ru"]
        added.append(variant["key"] + "\t" + "\t".join(values))
    if added:
        if not text.endswith("\n"):
            text += "\n"
        TRANSLATE.write_text(text + "\n".join(added) + "\n", encoding="utf-8")
    return len(added)


def main():
    template, variants = build_variants()
    item_guids = []
    for variant in variants:
        prefab_guid = write_prefab(template, variant)
        item_guids.append(write_shop_item(variant, prefab_guid))
    add_shop_page(item_guids)
    count = add_translations(variants)
    print("prefabs:", len(variants), "shop items:", len(item_guids), "translations:", count)


if __name__ == "__main__":
    main()
