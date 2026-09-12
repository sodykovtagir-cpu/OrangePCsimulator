#!/usr/bin/env python3
"""Проверки готовых ПК и майнеров + отключения Lua Editor в установщике.

Unity Editor не требуется: всё валидируется по YAML-ассетам. Запуск:
    python3 tools/test_ready_builds.py
"""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from unity_asset_tool import (  # noqa: E402
    SCRIPT_GUIDS,
    PrefabResolver,
    ReadyBuild,
    UnityDoc,
    quat_to_euler,
    read_meta_guid,
    shop_pages,
)
import generate_ready_builds as gen  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
SPAWNER_GUID = "01aba92678a70fdad133966614ce5f35"

failures: list[str] = []


def _guid_exists(guid: str) -> bool:
    cache = getattr(_guid_exists, "_cache", None)
    if cache is None:
        cache = set()
        for dirpath, _dirs, files in os.walk(ROOT / "Assets"):
            for fn in files:
                if not fn.endswith(".meta"):
                    continue
                with open(os.path.join(dirpath, fn), encoding="utf-8",
                          errors="replace") as fh:
                    for line in fh:
                        if line.startswith("guid:"):
                            cache.add(line.split(":", 1)[1].strip())
                            break
        _guid_exists._cache = cache
    return guid in cache




def check(condition: bool, message: str) -> None:
    if condition:
        print(f"  ok  {message}")
    else:
        print(f"FAIL  {message}")
        failures.append(message)


# ---------------------------------------------------------------------------
print("Lua Editor не ставится вместе с PCOS")

installer = (ROOT / "Assets/GameObject/Installer.prefab").read_text(encoding="utf-8")
pre = re.search(r"preinstalledApps:\n((?:  - \{.*\}\n)*)", installer)
pre_block = pre.group(1) if pre else ""
lua_guid = read_meta_guid(str(ROOT / "Assets/Resources/apps/LuaEditor.prefab"))
check(lua_guid not in pre_block,
      "LuaEditor нет в preinstalledApps установщика")
check(pre_block.strip() != "", "список preinstalledApps не опустел полностью")

os_cs = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/"
         "OperatingSystem.cs").read_text(encoding="utf-8")
active_ensure = [
    line for line in os_cs.splitlines()
    if "EnsureLuaEditor();" in line and not line.strip().startswith("//")
]
check(not active_ensure, "EnsureLuaEditor() не вызывается при загрузке системы")

# ---------------------------------------------------------------------------
print("\nСборки собираются и слоты сходятся")

prices = gen.load_price_table()

for spec in gen.BUILDS:
    build = ReadyBuild(name=spec.key, case_prefab=spec.case, parts=spec.parts)
    try:
        resolved = build.resolve(str(ROOT))
    except Exception as exc:  # noqa: BLE001
        check(False, f"{spec.key}: сборка не резолвится — {exc}")
        continue

    check(len(resolved) == len(spec.parts),
          f"{spec.key}: все {len(spec.parts)} деталей размещены")

    # Ни одна деталь не должна занимать ту же позу, что и другая.
    poses = [tuple(round(v, 3) for v in r["pos"]) for r in resolved]
    check(len(poses) == len(set(poses)),
          f"{spec.key}: позиции деталей не пересекаются")

    # Все компоненты существуют и имеют guid.
    check(all(r["guid"] for r in resolved),
          f"{spec.key}: у всех деталей есть guid")

    # Цена должна быть больше суммы комплектующих (наценка за сборку).
    case_name = os.path.splitext(os.path.basename(spec.case))[0]
    total = prices.get(case_name, 0) + sum(
        prices.get(r["name"], 0) for r in resolved
    )
    asset_path = ROOT / gen.OUT_ASSETS / f"{spec.key}.asset"
    check(asset_path.exists(), f"{spec.key}: ShopItem создан")
    if asset_path.exists():
        text = asset_path.read_text(encoding="utf-8")
        price = int(re.search(r"^\s*price: (\d+)", text, re.M).group(1))
        check(price > total,
              f"{spec.key}: цена {price}$ выше суммы комплектующих {total}$")
        check(SCRIPT_GUIDS["ShopItem"] in text,
              f"{spec.key}: ShopItem ссылается на правильный скрипт")

    # Префаб-спавнер валиден и ссылается на существующие префабы.
    prefab_path = ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab"
    check(prefab_path.exists(), f"{spec.key}: префаб-спавнер создан")
    if prefab_path.exists():
        doc = UnityDoc.load(str(prefab_path))
        mb = doc.find(class_id=114)
        check(len(mb) == 1 and mb[0].script_guid == SPAWNER_GUID,
              f"{spec.key}: висит ReadyBuildSpawner")
        body = prefab_path.read_text(encoding="utf-8")
        refs = re.findall(r"guid: (\w+), type: 3\}", body)
        missing = [g for g in refs if not _guid_exists(g)]
        check(not missing, f"{spec.key}: все ссылки на префабы разрешаются")
        check(body.count("- prefab:") == len(resolved),
              f"{spec.key}: в префабе {len(resolved)} деталей")


# ---------------------------------------------------------------------------
print("\nМагазин")

pages = dict(shop_pages(str(ROOT / gen.SHOP)))
check("Ready PC" in pages, "страница 'Ready PC' есть в Shop.asset")
check("Ready Miner" in pages, "страница 'Ready Miner' есть в Shop.asset")

expected_pc = sum(1 for b in gen.BUILDS if b.page == "Ready PC")
expected_miner = sum(1 for b in gen.BUILDS if b.page == "Ready Miner")
check(len(pages.get("Ready PC", [])) == expected_pc,
      f"на странице 'Ready PC' {expected_pc} сборок")
check(len(pages.get("Ready Miner", [])) == expected_miner,
      f"на странице 'Ready Miner' {expected_miner} майнеров")

for page, guids in pages.items():
    check(len(guids) == len(set(guids)), f"страница '{page}' без дублей")

# Ассеты страниц действительно существуют.
all_ready = pages.get("Ready PC", []) + pages.get("Ready Miner", [])
check(all(_guid_exists(g) for g in all_ready),
      "все готовые сборки в магазине ссылаются на существующие ассеты")

# ---------------------------------------------------------------------------
print("\nЛокализация")

translate = (ROOT / "Assets/Resources/Translate.txt").read_text(encoding="utf-8")
rows = [r.split("\t") for r in translate.splitlines() if r]
header = rows[0]
keys = {r[0] for r in rows}
en, ru = header.index("EN"), header.index("RU")

for key in ["Ready PC", "Ready Miner", "Office PC", "Home PC", "Gaming PC",
            "Cheap Miner", "Medium Miner", "Ultra Miner"]:
    check(key in keys, f"ключ '{key}' есть в Translate.txt")

widths = {len(r) for r in rows}
check(widths == {len(header)},
      f"все строки Translate.txt имеют {len(header)} колонок")

import add_ready_build_i18n as i18n  # noqa: E402

# tools/test_localization.py требует перевод на все 42 языка у каждого ключа —
# проверяем это сразу для новых строк, чтобы не ловить провал в общем тесте.
new_keys = set(i18n.NAMES) | set(i18n.SPECS)
by_key = {r[0]: r for r in rows}
for key in sorted(new_keys):
    row = by_key.get(key)
    if row is None:
        check(False, f"'{key}' отсутствует в Translate.txt")
        continue
    empty = [header[i] for i in range(1, len(header)) if not row[i].strip()]
    check(not empty,
          f"'{key}' заполнен на всех {len(header) - 1} языках"
          + (f" (пусто: {', '.join(empty[:5])})" if empty else ""))

check(all(by_key[k][en] != by_key[k][ru] for k in i18n.NAMES),
      "названия сборок действительно переведены на русский, а не скопированы")

# ---------------------------------------------------------------------------
print("\nДоставка в ящике")

# Готовая сборка обязана приезжать в ящике, как и любой крупный товар.
# Если ShopItem ссылается прямо на спавнер, ПК вываливается из портала
# в воздухе, падает и разбивается ещё до того, как игрок его увидит.
from unity_asset_tool import _find_root_game_object  # noqa: E402

seen_file_ids: dict[str, str] = {}

for spec in gen.BUILDS:
    crate_path = ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab"
    check(crate_path.exists(), f"{spec.key}: ящик доставки создан")
    if not crate_path.exists():
        continue

    crate = crate_path.read_text(encoding="utf-8")
    crate_guid = read_meta_guid(str(crate_path))

    # ShopItem должен указывать на ящик, а не на спавнер.
    asset = (ROOT / gen.OUT_ASSETS / f"{spec.key}.asset").read_text(encoding="utf-8")
    spawn = re.search(r"spawn: \{fileID: (\d+), guid: (\w+)", asset)
    check(spawn is not None and spawn.group(2) == crate_guid,
          f"{spec.key}: ShopItem спавнит ящик")

    root = _find_root_game_object(crate)
    check(spawn is not None and root is not None and spawn.group(1) == root,
          f"{spec.key}: ShopItem ссылается на корень ящика")

    # Корень ящика должен иметь тег Crate, иначе молоток его не вскроет.
    root_doc = next(
        (d for d in re.split(r"^--- ", crate, flags=re.M)[1:]
         if re.match(rf"!u!1 &{root}\b", d)), "")
    check("m_TagString: Crate" in root_doc, f"{spec.key}: ящик имеет тег Crate")

    # SaveManager грузит предметы по Resources.Load($"Components/{spawnId}"),
    # поэтому spawnId обязан совпасть с именем файла.
    check(f"spawnId: Crate_{spec.key}" in crate,
          f"{spec.key}: spawnId ящика совпадает с именем префаба")

    # Внутри ящика лежит спавнер именно этой сборки.
    spawner_guid = read_meta_guid(str(ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab"))
    box_at = crate.find(f"guid: {SCRIPT_GUIDS['Box']}")
    check(box_at != -1, f"{spec.key}: в ящике есть компонент Box")
    if box_at != -1:
        content = re.search(r"prefab: \{fileID: \d+, guid: (\w+)", crate[box_at:])
        check(content is not None and content.group(1) == spawner_guid,
              f"{spec.key}: ящик содержит спавнер своей сборки")

    # Клонирование ящика обязано перенумеровать все fileID: одинаковые
    # идентификаторы в разных префабах Unity воспринимает как один объект.
    for fid in re.findall(r"^--- !u!\d+ &(\d+)", crate, flags=re.M):
        owner = seen_file_ids.get(fid)
        if owner is not None and owner != spec.key:
            check(False, f"{spec.key}: fileID {fid} уже занят ящиком {owner}")
        seen_file_ids[fid] = spec.key

check(True, f"fileID ящиков уникальны ({len(seen_file_ids)} объектов)")

# ---------------------------------------------------------------------------
print("\nЛокализация названий со скобками")

# Названия готовых ПК содержат два токена: "{Office PC} ({Black})".
# Старый разбор в ShopUI брал первую '{' и последнюю '}' и выдавал мусор.
shop_ui = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Shop/ShopUI.cs").read_text(
    encoding="utf-8")
check("LastIndexOf('}')" not in shop_ui,
      "ShopUI больше не разбирает скобки вручную")
check("Item.TranslateBracket(item.itemName)" in shop_ui,
      "ShopUI переводит название через Item.TranslateBracket")

for spec in gen.BUILDS:
    for token in re.findall(r"\{([^}]+)\}", spec.title):
        check(token in by_key, f"ключ '{token}' из '{spec.key}' есть в переводе")

# ---------------------------------------------------------------------------
print("\nShopItem'ы видеокарт спавнят свои коробки")

# Предсуществующий баг проекта: RTX4080 и RTX4080Ti ссылались на Box_RTX5090,
# поэтому за деньги игрок получал не ту карту.
_guid_to_path: dict[str, str] = {}
for dirpath, _dirs, files in os.walk(ROOT / "Assets"):
    for fn in files:
        if not fn.endswith(".meta"):
            continue
        full = os.path.join(dirpath, fn)
        with open(full, encoding="utf-8", errors="replace") as fh:
            for line in fh:
                if line.startswith("guid:"):
                    _guid_to_path[line.split(":", 1)[1].strip()] = full[:-5]
                    break

for gpu in ("RTX4080", "RTX4080Ti", "RTX5090", "RTX3080", "GTX1060",
            "GT1030", "GT440", "RX570", "Titan V"):
    item_path = ROOT / "Assets/MonoBehaviour" / f"{gpu}.asset"
    if not item_path.exists():
        continue
    text = item_path.read_text(encoding="utf-8")
    ref = re.search(r"spawn: \{fileID: \d+, guid: (\w+)", text)
    box = _guid_to_path.get(ref.group(1), "") if ref else ""
    check(os.path.basename(box) == f"Box_{gpu}.prefab",
          f"{gpu}: ShopItem спавнит Box_{gpu} (сейчас {os.path.basename(box)})")

# ---------------------------------------------------------------------------
print("\nЗащита деталей при установке")

spawner_cs = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs").read_text(
    encoding="utf-8")
check("WaitUntilSettled" in spawner_cs,
      "спавнер ждёт приземления корпуса перед установкой деталей")
check("ImpactGuard" in spawner_cs, "спавнер защищает детали от урона")

guard_cs = ROOT / "Assets/Scripts/Assembly-CSharp/ImpactGuard.cs"
check(guard_cs.exists(), "ImpactGuard.cs существует")
check(_guid_exists("7c1f4b6ae2d84a1d9b3f5e08c7a26d41"),
      "у ImpactGuard.cs есть .meta с guid")

guard_src = guard_cs.read_text(encoding="utf-8")
# Стеклянные крышки — Glass : Destruction. Этот компонент не помечает
# деталь сломанной, а уничтожает её, поэтому кинематики мало: его надо
# гасить явно, иначе аквариумные сборки бьются прямо при установке.
check("Destruction" in guard_src, "ImpactGuard гасит Destruction (стекло)")
check("Breakable" in guard_src, "ImpactGuard гасит Breakable")
check("isKinematic" in guard_src, "ImpactGuard делает деталь кинематической")

# Защита обязана держаться до конца сборки: пока ставятся оставшиеся
# детали, уже установленные тоже могут получить импульс.
check(spawner_cs.count("Disarm") >= 2,
      "спавнер продлевает защиту уже установленным деталям")

# ---------------------------------------------------------------------------
print("\n.meta текстур совместимы с Unity 2022.3")

# .meta от Unity 6 (TextureImporter serializedVersion: 13) импортёр 2022.3
# не читает: «Unknown error occurred while loading ...», иконка не грузится.
import fix_texture_meta as texmeta  # noqa: E402

bad_meta = []
for dirpath, _dirs, files in os.walk(ROOT / "Assets"):
    for fn in files:
        if not fn.endswith(".meta"):
            continue
        full = os.path.join(dirpath, fn)
        with open(full, encoding="utf-8", errors="replace") as fh:
            content = fh.read()
        if "TextureImporter:" in content and texmeta.needs_fix(content):
            bad_meta.append(os.path.relpath(full, ROOT))

check(not bad_meta,
      "нет .meta текстур от новой Unity"
      + (f" (сломано: {len(bad_meta)}, напр. {bad_meta[0]})" if bad_meta else ""))

# Иконки готовых сборок обязаны существовать и импортироваться.
for spec in gen.BUILDS:
    asset = (ROOT / gen.OUT_ASSETS / f"{spec.key}.asset").read_text(encoding="utf-8")
    ref = re.search(r"sprite: \{fileID: \d+, guid: (\w+)", asset)
    check(ref is not None and _guid_exists(ref.group(1)),
          f"{spec.key}: иконка существует в проекте")

# ---------------------------------------------------------------------------
print("\nГенератор идемпотентен")

import subprocess  # noqa: E402

before = {}
for spec in gen.BUILDS:
    for path in [ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab",
                 ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab",
                 ROOT / gen.OUT_ASSETS / f"{spec.key}.asset"]:
        before[str(path)] = path.read_text(encoding="utf-8") if path.exists() else None

subprocess.run([sys.executable, str(ROOT / "tools/generate_ready_builds.py")],
               capture_output=True, check=True)

unchanged = all(
    (Path(p).read_text(encoding="utf-8") if Path(p).exists() else None) == v
    for p, v in before.items()
)
check(unchanged, "повторный запуск генератора не меняет файлы")

# ---------------------------------------------------------------------------
print()
if failures:
    print(f"ПРОВАЛЕНО проверок: {len(failures)}")
    for f in failures:
        print(f"  - {f}")
    raise SystemExit(1)
print("Все проверки пройдены.")
