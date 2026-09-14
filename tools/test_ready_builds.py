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
import unity_asset_tool as uat  # noqa: E402
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
    box_at = crate.find(f"guid: {uat.READY_BOX_SCRIPT_GUID}")
    check(box_at != -1,
          f"{spec.key}: в ящике стоит ReadyBuildBox (разводит груз и обломки)")
    check(SCRIPT_GUIDS["Box"] not in crate,
          f"{spec.key}: штатный Box заменён — он ронял раму внутрь обломков")
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
# Кинематика ДЕТАЛЕЙ запрещена: это ломало FixedJoint и приём в слот —
# проверено дважды. Заморозка ОСНОВЫ на время сборки — другое дело: у неё
# нет входящего джойнта, и она отпускается сразу после последней детали.
_attach_body = spawner_cs.split("private IEnumerator Build()")[1].split(
    "private bool Attach")[0]
for _line in _attach_body.splitlines():
    if "isKinematic" not in _line:
        continue
    check("baseBody" in _line or "baseWasKinematic" in _line,
          f"кинематика меняется только у основы, не у детали: {_line.strip()}")
check("spawned.isKinematic" not in spawner_cs
      and "item.isKinematic" not in spawner_cs
      and "rb.isKinematic" not in spawner_cs,
      "спавнер не трогает кинематику деталей (ломало FixedJoint и приём в слот)")

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

# Ящик НЕ обязан вмещать груз: содержимое появляется только после того, как
# ящик уничтожен (Box на BrokenCrate). Требование "коробка не мельче груза"
# отменено — из-за него ящик BigMiner раздувался до 11 единиц и упирался в
# потолок. Проверяем обратное: масштаб выбран ровно тот, что считает генератор.
for spec in gen.BUILDS:
    _resolved = ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT))
    _want = gen.crate_scale_for(spec.case, _resolved)
    _got = root_scale(f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab", str(ROOT))[0]
    check(abs(_got - _want) < 1e-9,
          f"{spec.key}: масштаб ящика x{_got:.2f} совпадает с расчётным x{_want:.2f}")

# Содержимое выдаётся уничтожаемым ящиком, а не лежит в нём: компонент Box
# обязан висеть на BrokenCrate, иначе предмет появится внутри стенок.
for spec in gen.BUILDS:
    _doc = UnityDoc.load(ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab")
    _names = {o.file_id: o.get("m_Name") for o in _doc.objects if o.class_id == 1}
    _owner = None
    for _o in _doc.objects:
        if _o.class_id != 114:
            continue
        if uat.READY_BOX_SCRIPT_GUID not in (_o.get("m_Script") or ""):
            continue
        _m = re.search(r"fileID: (-?\d+)", _o.get("m_GameObject") or "")
        if _m:
            _owner = _names.get(int(_m.group(1)))
    check(_owner == "BrokenCrate",
          f"{spec.key}: Box висит на BrokenCrate (сейчас '{_owner}')")

# ---------------------------------------------------------------------------
print("\nГруз не сталкивается с обломками вскрытого ящика")

_box_cs_path = ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildBox.cs"
check(_box_cs_path.exists(), "ReadyBuildBox.cs на месте")
_box_cs = _box_cs_path.read_text(encoding="utf-8")
_box_meta = ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildBox.cs.meta"
check(_box_meta.exists(), "у ReadyBuildBox есть .meta")
check("MonoImporter" in _box_meta.read_text(encoding="utf-8"),
      "ReadyBuildBox.cs.meta — MonoImporter, а не PrefabImporter")
check(read_meta_guid(str(_box_cs_path)) == uat.READY_BOX_SCRIPT_GUID,
      "guid ReadyBuildBox совпадает с тем, что тулза пишет в ящики")

# Ящик к моменту выдачи груза — шесть обломков Part с Rigidbody. Рама
# BigMiner (10.54 в высоту) рождается внутри них, и без развода коллизий
# обломки выпихивают её вместе с видеокартами.
check("Physics.IgnoreCollision" in _box_cs,
      "ReadyBuildBox разводит коллизии груза и обломков")

# Сборка появляется не мгновенно — спавнер ставит детали по одной за
# несколько кадров. Разовый проход пропустил бы видеокарты, приехавшие позже.
check("for (int pass" in _box_cs and "WaitForSeconds" in _box_cs,
      "развод коллизий повторяется, пока спавнер доставляет детали")

# Штатный Box общий для 31 ванильного ящика — его трогать нельзя.
_vanilla_box = (ROOT / "Assets/Scripts/Assembly-CSharp/Box.cs").read_text(encoding="utf-8")
check("Physics.IgnoreCollision" not in _vanilla_box,
      "штатный Box.cs не тронут — он общий для всех ванильных ящиков")

# ---------------------------------------------------------------------------
print("\nОснова тяжелее навески и решатель усилен")

_spawner_src = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs").read_text(
    encoding="utf-8")

# Рама BigMiner весит 2.5, а деталей на неё вешается до 29.5 — основа в
# двенадцать раз легче навески. Решатель PhysX (в проекте 6 итераций) такую
# перевёрнутую пирамиду не удерживает: связка расползается и выплёвывает
# видеокарты. Cheap/Medium/Titan (10-16.5) собирались, Ultra/RTX5090 - нет.
def _rb_mass(rel):
    _p = ROOT / rel
    if not _p.exists():
        return 0.0
    _t = _p.read_text(encoding="utf-8")
    _m = re.search(r"--- !u!54 &\d+\nRigidbody:(.*?)(?=--- !u!|\Z)", _t, re.S)
    if not _m:
        return 0.0
    _mm = re.search(r"m_Mass: ([\d.]+)", _m.group(1))
    return float(_mm.group(1)) if _mm else 0.0


check(uat.BASE_MASS_FACTOR >= 2.0,
      f"основа тяжелее навески минимум вдвое (x{uat.BASE_MASS_FACTOR})")
check(uat.BASE_SOLVER_ITERATIONS > 6,
      f"решателю основы дано больше итераций, чем 6 по умолчанию "
      f"({uat.BASE_SOLVER_ITERATIONS})")

for spec in gen.BUILDS:
    _resolved = ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT))
    _parts_mass = sum(_rb_mass(r["path"]) for r in _resolved)
    _base = max(uat.BASE_MIN_MASS, _parts_mass * uat.BASE_MASS_FACTOR)
    check(_parts_mass == 0 or _base >= _parts_mass * 2.0,
          f"{spec.key}: основа {_base:.1f} тяжелее навески {_parts_mass:.1f} "
          f"в {_base / _parts_mass if _parts_mass else 0:.1f} раза")

# Поля обязаны доехать до префаба, иначе в игре останутся значения по
# умолчанию из C# и правка ничего не изменит.
for spec in gen.BUILDS:
    _text = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    check("baseMassFactor:" in _text, f"{spec.key}: baseMassFactor записан")
    check("baseSolverIterations:" in _text,
          f"{spec.key}: baseSolverIterations записан")

# Основа кинематическая на время сборки и обязательно отпускается потом,
# иначе готовый майнер навсегда зависнет в воздухе.
check("isKinematic = true" in _spawner_src,
      "основа замораживается на время сборки")
check("isKinematic = baseWasKinematic" in _spawner_src,
      "основа возвращается в исходное состояние после сборки")
check(_spawner_src.index("isKinematic = true")
      < _spawner_src.index("isKinematic = baseWasKinematic"),
      "заморозка идёт до разморозки, а не наоборот")

# ---------------------------------------------------------------------------
print("\nВ рамы майнеров ставится только низкий кулер")

# В раме над сокетом мало места: башенный кулер (1.25 в высоту) и водянка
# (1.45 плюс радиатор 3.06 в длину) не помещаются — упираются в конструкцию
# рамы. Влезает только низкий Cooler (0.63). Вертикальные тоже не годятся.
for _problem in gen.check_miner_coolers():
    check(False, _problem)
check(not gen.check_miner_coolers(),
      "ни в одну раму майнера не поставлен крупный кулер")

_COOLER_H = {}
for _c in ("Cooler", "Cooler(RGB)", "TowerCooler", "WaterCooler"):
    _cb = prefab_bounds(f"Assets/Resources/components/{_c}.prefab", str(ROOT))
    _COOLER_H[_c] = _cb[1][1] - _cb[0][1]
check(_COOLER_H["Cooler"] < _COOLER_H["TowerCooler"] < _COOLER_H["WaterCooler"],
      f"низкий кулер {_COOLER_H['Cooler']:.2f} ниже башенного "
      f"{_COOLER_H['TowerCooler']:.2f} и водянки {_COOLER_H['WaterCooler']:.2f}")

for spec in gen.BUILDS:
    _case = os.path.basename(spec.case)[:-len(".prefab")]
    if _case not in gen.MINER_FRAMES:
        continue
    for _part in spec.parts:
        if _part.slot_target != "Cooler":
            continue
        _nm = os.path.basename(_part.prefab_path)[:-len(".prefab")]
        check(_nm in ("Cooler", "Cooler(RGB)"),
              f"{spec.key}: кулер {_nm} помещается в раму {_case}")

# ---------------------------------------------------------------------------
print("\nЯщик пролезает в помещение, детали склеены")


# Регресс: ящик x2.5 (11 единиц в высоту) упирался в потолок — товар приезжает
# порталом под крышей. Коробку зажимало, роняло, и видеокарты высыпались.
_VANILLA_CRATE_H = 4.5
# Абсолютный потолок высоты ящика, НЕ завязанный на константу генератора:
# иначе достаточно поднять CRATE_MAX_SCALE, и тест поедет следом. Ящик x2.5
# давал 11.25 и застревал в потолке помещения; 7 единиц — безопасный предел.
_MAX_CRATE_HEIGHT = 8.0
for spec in gen.BUILDS:
    _k = root_scale(f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab", str(ROOT))[0]
    _h = _VANILLA_CRATE_H * _k
    check(_h <= _MAX_CRATE_HEIGHT,
          f"{spec.key}: высота ящика {_h:.2f} не упирается в потолок "
          f"(предел {_MAX_CRATE_HEIGHT})")

# Ящик не обязан вмещать груз — прецедент прямо в игре: восьмиметровый Table
# приезжает в ящике высотой 4.5. Содержимое появляется только после того, как
# ящик уничтожен, поэтому упираться в потолок вреднее, чем быть меньше груза.
_table = prefab_bounds("Assets/Resources/components/Table.prefab", str(ROOT))
if _table is not None:
    _tlo, _thi = _table
    check(max(_thi[i] - _tlo[i] for i in range(3)) > _VANILLA_CRATE_H,
          "ванильный Table крупнее своего ящика — груз крупнее коробки это норма")

# Клея быть не должно: компьютеры обязаны ломаться. Item.glue делает
# FixedJoint неразрывным, и готовая сборка стала бы неуязвимой — это меняет
# правила игры, а не чинит доставку.
check("glue" not in _spawner_src,
      "спавнер НЕ склеивает детали — готовый ПК ломается как обычный")
for spec in gen.BUILDS:
    _text = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    check("glueParts" not in _text, f"{spec.key}: поля склейки нет")

# Груз не должен родиться ниже пола.
#
# Он появляется в ПИВОТЕ ящика, а пивот ящика — его центр: когда ящик стоит
# на полу, это 2.25 (умножить на масштаб). Дальше решает пивот самого груза.
# У рамы BigMiner дно на -5.09, и при ящике x1.75 (пивот 3.94) оно уходит на
# 1.15 НИЖЕ пола — рама спавнится в полу, физика выталкивает её рывком.
_tb = prefab_bounds(f"{gen.COMP}/Crate_Case_ATX 2(Black).prefab", str(ROOT))
_half = -_tb[0][1]
for spec in gen.BUILDS:
    _k = root_scale(f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab", str(ROOT))[1]
    _cb = prefab_bounds(spec.case, str(ROOT))
    _bottom = _cb[0][1] * root_scale(spec.case, str(ROOT))[1]

    _ct = (ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab").read_text(encoding="utf-8")
    _i = _ct.find(f"guid: {uat.READY_BOX_SCRIPT_GUID}")
    _lift = float(re.search(
        r"position: \{x: [-\d.e]+, y: ([-\d.e]+)", _ct[_i:_i + 400]).group(1))

    _ground = _half * _k + _bottom + _lift
    check(_ground >= 0,
          f"{spec.key}: дно груза на {_ground:+.2f} — не ниже пола "
          f"(масштаб x{_k:.2f}, подъём {_lift:.2f})")

# Подъём строго минимальный. Прошлая версия поднимала на всю высоту пивота
# (5.24) вслепую — это загоняло раму глубоко внутрь обломков вскрытого ящика.
for spec in gen.BUILDS:
    _k = root_scale(f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab", str(ROOT))[1]
    _want = gen.crate_lift_for(spec.case, _k)
    _ct = (ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab").read_text(encoding="utf-8")
    _i = _ct.find(f"guid: {uat.READY_BOX_SCRIPT_GUID}")
    _got = float(re.search(
        r"position: \{x: [-\d.e]+, y: ([-\d.e]+)", _ct[_i:_i + 400]).group(1))
    check(abs(_got - _want) < 1e-6,
          f"{spec.key}: подъём {_got:.2f} — ровно расчётный минимум {_want:.2f}")
    check(_got <= 2.0,
          f"{spec.key}: подъём {_got:.2f} не задран (5.24 загонял раму в обломки)")

# У большинства сборок подъём не нужен вовсе — как во всех 31 ванильном ящике.
_lifted = 0
for spec in gen.BUILDS:
    _k = root_scale(f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab", str(ROOT))[1]
    if gen.crate_lift_for(spec.case, _k) > 0:
        _lifted += 1
check(_lifted <= 2,
      f"подъём понадобился только {_lifted} сборкам на раме BigMiner")

_van = 0
for _f in sorted((ROOT / gen.COMP).glob("Crate_*.prefab")):
    _t = _f.read_text(encoding="utf-8")
    _i = _t.find(f"guid: {SCRIPT_GUIDS['Box']}")
    if _i == -1:
        continue
    _m = re.search(
        r"position: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", _t[_i:_i + 400])
    if _m and any(abs(float(v)) > 1e-9 for v in _m.groups()):
        check(False, f"ваниль {_f.name} смещает содержимое — пересмотреть вывод")
    _van += 1
check(_van > 20,
      f"проверено {_van} ванильных ящиков: ни один не смещает содержимое")

# ЛОВУШКА: подъём правится в том же срезе текста, что и ссылка Box.prefab.
# Отдельный проход по исходному тексту затирал ссылку, и ящик начинал везти
# ванильный корпус вместо готовой сборки — молча, без единой ошибки.
for spec in gen.BUILDS:
    _ct = (ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab").read_text(encoding="utf-8")
    _want = re.search(
        r"guid: (\w+)",
        (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab.meta").read_text(encoding="utf-8"),
    ).group(1)
    _i = _ct.find(f"guid: {uat.READY_BOX_SCRIPT_GUID}")
    _got = re.search(r"prefab: \{fileID: \d+, guid: (\w+)", _ct[_i:_i + 400]).group(1)
    check(_got == _want,
          f"{spec.key}: ящик везёт свою сборку, а не ванильный корпус")

# ---------------------------------------------------------------------------
print("\nPCOS предустановлена с нужным набором приложений")

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
