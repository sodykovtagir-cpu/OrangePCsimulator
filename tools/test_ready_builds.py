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
    item_info,
    app_info,
    build_bounds,
    prefab_bounds,
    prefab_spawn_ref,
    root_scale,
    BOOT_FILE_SIZE as gen_boot_size,
    quat_to_euler,
    read_meta_guid,
    shop_pages,
)
import generate_ready_builds as gen  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
SPAWNER_GUID = "01aba92678a70fdad133966614ce5f35"

failures: list[str] = []


def _guid_is_crate(guid: str) -> bool:
    """Ссылается ли guid на префаб деревянного ящика."""
    for meta in (ROOT / "Assets/Resources/components").rglob("*.prefab.meta"):
        if read_meta_guid(str(meta)[: -len(".meta")]) == guid:
            return meta.name.startswith("Crate_")
    return False


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

# Готовый ПК обязан приезжать в ящике, как и любой крупный товар: если
# ShopItem ссылается прямо на спавнер, ПК вываливается из портала в воздухе,
# падает и разбивается ещё до того, как игрок его увидит.
#
# Габарит сборки значения не имеет: содержимое не лежит внутри коробки.
# Компонент Box висит на BrokenCrate, который активируется только при ударе
# молотком, когда сам ящик уничтожается. Поэтому в ванили и восьмиметровый
# Table приезжает в том же ящике 3 x 4.5 x 5.
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
print("\nСборка через прямое подключение в слот")

spawner_cs = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs").read_text(
    encoding="utf-8")
slot_cs = (ROOT / "Assets/Scripts/Assembly-CSharp/Slot.cs").read_text(encoding="utf-8")

# Деталь подключается тем же кодом, что и при ручной сборке, но напрямую:
# OnTriggerEnter ненадёжен, если деталь создана сразу внутри триггера.
check("public bool TryAttach(Item item)" in slot_cs,
      "Slot.TryAttach существует")
check("SetComponent(item)" in slot_cs.split("TryAttach")[1],
      "TryAttach вызывает тот же SetComponent, что и обычная установка")
check("IsMatch(item.Match)" in slot_cs.split("TryAttach")[1],
      "TryAttach проверяет match (M.2 не попадёт в слот корпуса)")
check("item.CompareTag(target)" in slot_cs.split("TryAttach")[1],
      "TryAttach проверяет тег слота")
check("public bool IsUsing" in slot_cs, "Slot.IsUsing доступен сборщику")

check("TryAttach" in spawner_cs, "спавнер подключает детали через TryAttach")
check("GetComponentsInChildren<Slot>" in spawner_cs,
      "спавнер ищет слоты по всей иерархии (включая слоты материнки)")
check("slotTarget" in spawner_cs, "спавнер учитывает целевой слот детали")

# Позу и поворот задаёт слот из своего insertPos. Раньше спавнер выставлял
# их сам по заранее вычисленным координатам, и ошибка в кватернионе давала
# видеокарты, стоящие вверх ногами.
build_body = spawner_cs.split("private IEnumerator Build()")[1].split(
    "private bool Attach")[0]
check("Quaternion.Euler(part.localEuler)" not in build_body,
      "спавнер не выставляет поворот детали вручную (поворот берётся из слота)")
check("TransformPoint(part.localPosition)" not in build_body,
      "спавнер не выставляет позицию детали вручную (позицию берёт слот)")

# ImpactGuard больше не нужен: деталь подключается мгновенно и не успевает
# ни упасть, ни получить урон.
check(not (ROOT / "Assets/Scripts/Assembly-CSharp/ImpactGuard.cs").exists(),
      "ImpactGuard удалён — деталь подключается сразу, ломаться нечему")
check("ImpactGuard" not in spawner_cs, "спавнер не ссылается на ImpactGuard")
check("isKinematic" not in spawner_cs,
      "спавнер не трогает кинематику (ломало FixedJoint и приём в слот)")

# Каждая деталь должна нести тег своего слота, иначе спавнер её не пристроит.
for spec in gen.BUILDS:
    prefab = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    targets = re.findall(r"slotTarget: (\S+)", prefab)
    check(len(targets) == len(spec.parts),
          f"{spec.key}: у всех {len(spec.parts)} деталей задан slotTarget")

# Слотов в корпусе и на плате должно хватать на все детали сборки.
from collections import Counter  # noqa: E402
from unity_asset_tool import PrefabResolver  # noqa: E402

for spec in gen.BUILDS:
    build = ReadyBuild(name=spec.key, case_prefab=spec.case, parts=spec.parts)
    resolved = build.resolve(str(ROOT))
    need = Counter(r["target"] for r in resolved)

    have = Counter(s["target"] for s in PrefabResolver(str(ROOT / spec.case),
                                                       str(ROOT)).slots)
    for r in resolved:
        if r["target"] == "Motherboard":
            mb_path = ROOT / gen.COMP / f"{r['name']}.prefab"
            have.update(s["target"] for s in PrefabResolver(str(mb_path),
                                                            str(ROOT)).slots)

    short = {k: (v, have.get(k, 0)) for k, v in need.items() if v > have.get(k, 0)}
    check(not short, f"{spec.key}: слотов хватает на все детали"
          + (f" (нехватка: {short})" if short else ""))

    # Материнка обязана вставать первой: её слоты нужны CPU, RAM и GPU.
    mb_index = next((i for i, r in enumerate(resolved)
                     if r["target"] == "Motherboard"), None)
    check(mb_index in (None, 0), f"{spec.key}: материнская плата ставится первой")

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
print("\nСлоты ищутся не только в иерархии корпуса")

# Регресс: слот Motherboard во ВСЕХ корпусах и рамах объявлен с setParent: 0 —
# плата не становится потомком корпуса, её держит только FixedJoint. Спавнер
# искал слоты через root.GetComponentsInChildren<Slot>() от корпуса и потому
# не видел CPU/Cooler/RAM/GPU самой платы: в игре это давало
# «не нашлось слота 'CPU' для детали 'CPU Celeron G3920'».
for _case in sorted({spec.case for spec in gen.BUILDS}):
    _doc = UnityDoc.load(ROOT / _case)
    _mb = [o for o in _doc.objects
           if o.get("target") == "Motherboard" and o.get("setParent") is not None]
    if not _mb:
        continue
    _detached = [o for o in _mb if str(o.get("setParent")).strip() == "0"]
    if _detached:
        check("hosts" in spawner_cs and "hosts.Add" in spawner_cs,
              f"{Path(_case).name}: слот Motherboard с setParent:0 — спавнер "
              f"обязан собирать слоты и с подключённых деталей")

check("private bool Attach(List<GameObject> hosts" in spawner_cs,
      "Attach ищет слоты по списку хостов, а не только в корпусе")
check("hosts.Add(spawned)" in spawner_cs,
      "подключённая деталь добавляется в список хостов (плата приносит свои слоты)")
check("root.GetComponentsInChildren<Slot>" not in spawner_cs,
      "нет поиска слотов только от корпуса")

# ---------------------------------------------------------------------------
print("\nbasePrefab спавнера указывает на реальный корень рамы")

# Регресс: жадный разбор m_RemovedGameObjects захватывал соседний блок
# m_AddedGameObjects и «удалял» корневой Transform рамы. prefab_spawn_ref
# начинал возвращать чужой fileID, спавнер создавал не тот объект — и в игре
# не находилось НИ ОДНОГО слота, даже Motherboard.
#
# Эталон — ShopItem ванильной рамы: он ссылается на её настоящий корень.
_VANILLA_ROOT = {
    "Miner": "Miner",
    "BigMiner": "Big Miner",
}
for _rig, _asset_name in _VANILLA_ROOT.items():
    _asset = (ROOT / "Assets/MonoBehaviour" / f"{_asset_name}.asset").read_text(
        encoding="utf-8", errors="replace")
    _want = re.search(r"spawn: \{fileID: (\d+)", _asset).group(1)
    _guid, _got = prefab_spawn_ref(
        f"Assets/Resources/components/{_rig}.prefab", str(ROOT))
    check(str(_got) == _want,
          f"{_rig}: корень {_got} совпадает с ванильным ShopItem ({_want})")

for spec in gen.BUILDS:
    _text = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    _m = re.search(r"basePrefab: \{fileID: (\d+), guid: (\w+)", _text)
    _guid, _root = prefab_spawn_ref(spec.case, str(ROOT))
    check(_m is not None and _m.group(1) == str(_root) and _m.group(2) == _guid,
          f"{spec.key}: basePrefab ссылается на корень своей рамы/корпуса")

# ---------------------------------------------------------------------------
print("\nВариант префаба может удалять унаследованные слоты")

# Регресс: BigMiner — это вариант обычного Miner, но он УДАЛЯЕТ унаследованный
# объект Slots целиком и ставит свои. Резолвер не читал m_RemovedGameObjects
# и видел фантомные слоты: 6 Supply / 24 GPU / 10 Drive вместо 4 / 16 / 8.
# Генератор раскладывал детали по несуществующим слотам, и в игре это давало
# «не нашлось слота 'Supply' для детали 'PSU 2kW'», а лишние видеокарты
# вываливались из рамы при сборке.
_RIG_SLOTS = {
    "Miner": {"Motherboard": 1, "Supply": 2, "GPU": 8, "Drive": 2},
    "BigMiner": {"Motherboard": 1, "Supply": 4, "GPU": 16, "Drive": 8},
}
for _rig, _want in _RIG_SLOTS.items():
    _res = PrefabResolver(ROOT / "Assets/Resources/components" / f"{_rig}.prefab", ROOT)
    _got = {}
    for _slot in _res.slots:
        _got[_slot["target"]] = _got.get(_slot["target"], 0) + 1
    check(_got == _want, f"{_rig}: слотов {_got}, ожидалось {_want}")

# Ни одна сборка не должна просить слот с номером больше, чем есть в раме.
for spec in gen.BUILDS:
    try:
        ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT))
        _ok, _err = True, ""
    except ValueError as exc:
        _ok, _err = False, str(exc)
    check(_ok, f"{spec.key}: хватает реальных слотов" + (f" — {_err}" if _err else ""))

# ---------------------------------------------------------------------------
print("\nMatch-совместимость деталей со слотами")

# Регресс на баг, из-за которого офисный ПК не собирался: item_info() искал
# поле match строго по guid компонента Item, но на реальных деталях висит
# наследник (Motherboard, Hardware, CPU, Storage), и match читался как None.
# Вся match-валидация молча пропускалась, а в игре Slot.TryAttach отклонял
# плату Micro_ATX (match=1) в корпусе Case_ITX (matches=00).
_MATCH_REF = {
    "Micro_ATX": 1,      # компонент Motherboard, не Item
    "Mini_ITX": 0,
    "EXATX": 1,
    "PSU 2kW": 1,        # компонент Supply
    "PSU 1.1kW": 0,
    "SSD_M.2 1TB": 1,    # компонент Storage
    "SSD 2TB": 0,
    "GT440": 0,          # компонент Hardware
}
for _name, _want in _MATCH_REF.items():
    _doc = UnityDoc.load(ROOT / "Assets/Resources/components" / f"{_name}.prefab")
    _got = item_info(_doc)["match"]
    check(_got == _want,
          f"{_name}: match читается у наследника Item (ожидали {_want}, получили {_got})")

# Каждая деталь каждой сборки обязана проходить фильтр matches своего слота,
# иначе Slot.TryAttach вернёт false и деталь просто не встанет.
for spec in gen.BUILDS:
    try:
        ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT))
        _ok, _err = True, ""
    except ValueError as exc:
        _ok, _err = False, str(exc)
    check(_ok, f"{spec.key}: все детали проходят match слотов" + (f" — {_err}" if _err else ""))

# ---------------------------------------------------------------------------
print("\nСсылки на иконки совпадают с донорскими")

# Регресс: генератор жёстко писал 'type: 2', хотя доноры Big_Miner и
# AquariumCaseAtx* ссылаются на свой спрайт с 'type: 3'. Unity отвечал на это
# "Unknown error occurred while loading" прямо в ShopUI.Init/ShopPanel.Awake.
_SPRITE_RE = re.compile(r"sprite: \{fileID: \d+, guid: (\w+), type: (\d+)")
for spec in gen.BUILDS:
    _donor = (ROOT / "Assets/MonoBehaviour" / f"{spec.sprite_from}.asset").read_text(
        encoding="utf-8", errors="replace")
    _mine = (ROOT / gen.OUT_ASSETS / f"{spec.key}.asset").read_text(encoding="utf-8")
    _d, _m = _SPRITE_RE.search(_donor), _SPRITE_RE.search(_mine)
    check(_d is not None and _m is not None and _d.groups() == _m.groups(),
          f"{spec.key}: ссылка на иконку идентична донору {spec.sprite_from} "
          f"({_d.groups() if _d else None} vs {_m.groups() if _m else None})")

# ---------------------------------------------------------------------------
print("\nЯщик масштабируется только равномерно")

# Регресс: я растягивал ящик по осям под габарит сборки (1.61, 2.6, 1) — и
# физика поехала. Ящик собран из семи отдельных Rigidbody-стенок; при
# неравномерном масштабе их коллайдеры меняли видимый размер при смене
# ракурса, проваливались внутрь рамы майнера и выталкивали видеокарты.
#
# Равномерный масштаб Unity обрабатывает корректно, поэтому размер коробки
# под раму BigMiner подогнан именно так — иначе она выглядит втрое меньше
# своего груза.
for spec in gen.BUILDS:
    _crate_rel = f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab"
    _scale = root_scale(_crate_rel, str(ROOT))
    check(abs(_scale[0] - _scale[1]) < 1e-9 and abs(_scale[1] - _scale[2]) < 1e-9,
          f"{spec.key}: масштаб ящика равномерный "
          f"(scale={tuple(round(v, 2) for v in _scale)})")
    check(_scale[0] >= 1.0 - 1e-9,
          f"{spec.key}: ящик не ужимается (scale={_scale[0]:.2f})")

# Коробка должна быть не мельче своего груза — это чисто про внешний вид.
for spec in gen.BUILDS:
    _resolved = ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT))
    _lo, _hi = build_bounds(spec.case, _resolved, str(ROOT))
    _content = tuple(_hi[i] - _lo[i] for i in range(3))
    _k = root_scale(f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab", str(ROOT))[0]
    _inner = tuple(v * _k for v in gen.CRATE_INNER)
    check(all(_inner[i] >= _content[i] - 1e-6 for i in range(3)),
          f"{spec.key}: ящик ({_inner[0]:.2f}, {_inner[1]:.2f}, {_inner[2]:.2f}) "
          f"не мельче груза ({_content[0]:.2f}, {_content[1]:.2f}, {_content[2]:.2f})")

# Содержимое выдаётся уничтожаемым ящиком, а не лежит в нём: компонент Box
# обязан висеть на BrokenCrate, иначе предмет появится внутри стенок.
for spec in gen.BUILDS:
    _doc = UnityDoc.load(ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab")
    _names = {o.file_id: o.get("m_Name") for o in _doc.objects if o.class_id == 1}
    _owner = None
    for _o in _doc.objects:
        if _o.class_id != 114:
            continue
        if SCRIPT_GUIDS["Box"] not in (_o.get("m_Script") or ""):
            continue
        _m = re.search(r"fileID: (-?\d+)", _o.get("m_GameObject") or "")
        if _m:
            _owner = _names.get(int(_m.group(1)))
    check(_owner == "BrokenCrate",
          f"{spec.key}: Box висит на BrokenCrate (сейчас '{_owner}')")

# ---------------------------------------------------------------------------
print("\nPCOS предустановлена с нужным набором приложений")

_spawner_src = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs").read_text(
    encoding="utf-8")
check("System/boot.bin" in _spawner_src and '"pcos"' in _spawner_src,
      "спавнер пишет загрузчик System/boot.bin с содержимым pcos")
check("Resources.LoadAll<App>(\"apps\")" in _spawner_src,
      "размер .exe берётся из каталога приложений, как в Installer")

for spec in gen.BUILDS:
    _text = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    _m = re.search(r"^  preinstalledApps:\n((?:  - .*\n)*)", _text, flags=re.M)
    _listed = re.findall(r"^  - (.+)$", _m.group(1), flags=re.M) if _m else []
    _want = [app_info(a, str(ROOT))[0] for a in spec.apps]
    check(_listed == _want,
          f"{spec.key}: список приложений {_listed} совпадает с ожидаемым {_want}")

# Приложение должно существовать в Resources/apps, иначе ОС выдаст
# "App (...) not found!" и иконка не появится.
_catalog = {}
for _p in sorted((ROOT / "Assets/Resources/apps").glob("*.prefab")):
    try:
        _n, _s = app_info(_p.stem, str(ROOT))
    except Exception:
        continue
    _catalog[_n] = _s
for _name in sorted({app_info(a, str(ROOT))[0] for spec in gen.BUILDS for a in spec.apps}):
    check(_name in _catalog, f"приложение '{_name}' есть в Resources/apps")

# Система плюс программы обязаны влезть на диск сборки: Storage.AddFile
# молча откажет, если места не хватает.
for spec in gen.BUILDS:
    _resolved = ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT))
    _caps = []
    for _part in _resolved:
        _txt = (ROOT / _part["path"]).read_text(encoding="utf-8", errors="replace")
        _cm = re.search(r"^\s*capacity:\s*(\d+)", _txt, flags=re.M)
        if _cm:
            _caps.append(int(_cm.group(1)))
    if not _caps or not spec.apps:
        continue
    _need = gen_boot_size + sum(app_info(a, str(ROOT))[1] for a in spec.apps)
    check(max(_caps) >= _need,
          f"{spec.key}: PCOS и приложения ({_need}) влезают на диск ({max(_caps)})")

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
