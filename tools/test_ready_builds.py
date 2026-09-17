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

    # Материнка обязана вставать раньше СВОИХ деталей (её слоты нужны CPU и
    # RAM), но не раньше всех вообще: плата держится на единственном джойнте,
    # и если поставить её первой, каждая следующая деталь трясёт раму и бьёт
    # по этому креплению. Поэтому плата и её группа идут последними.
    mb = next((r for r in resolved if r["target"] == "Motherboard"), None)
    if mb is not None:
        mb_order = mb["order"]
        mb_kids = [r for r in resolved if r["host"] == mb["name"]]
        check(all(k["order"] > mb_order for k in mb_kids),
              f"{spec.key}: материнская плата ставится раньше своих деталей")

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
print("\nСтенки ящика подогнаны под груз")

# Неравномерный масштаб когда-то поехал, и запрет казался оправданным. Но
# ломался не тот объект: семь Rigidbody-стенок — это BrokenCrate, груда
# обломков ПОСЛЕ удара молотком. Целый ящик, который едет к игроку, это один
# объект с пятью BoxCollider'ами, и вращение у всех его узлов единичное — а
# при единичном вращении оси коллайдера совпадают с осями меша, и растяжение
# по осям их не перекашивает.
_crate_src = (ROOT / "Assets/Resources/components/Crate.prefab").read_text(
    encoding="utf-8")
check(_crate_src.count("--- !u!4 &") == 1,
      "целый ящик — ОДИН Transform, а не семь Rigidbody-стенок")
check(_crate_src.count("--- !u!65 &") == 5,
      "у целого ящика пять BoxCollider'ов")
check("m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}" in _crate_src,
      "вращение ящика единичное — растяжение по осям безопасно")

for spec in gen.BUILDS:
    _crate_rel = f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab"
    _scale = root_scale(_crate_rel, str(ROOT))
    for _ax, _v in zip("xyz", _scale):
        check(_v >= 1.0 - 1e-9,
              f"{spec.key}: ящик не ужимается по {_ax} ({_v:.2f})")

    # Высота ограничена комнатой: портал на y=8.0, лампа на 5.69. Ящик выше
    # пяти метров начнёт цеплять потолок по дороге к игроку.
    # Высоту мерим по РЕАЛЬНОМУ габариту шаблона (4.5), а не по коллайдеру:
    # коллайдер по Y даёт 1.78 — это толщина боковой стенки, и на ней легко
    # ошибиться втрое. Порог берём не из константы генератора, иначе проверка
    # поедет вместе с ней: лампа сцены на 5.69 — физический потолок комнаты.
    _tpl_h = uat.prefab_bounds(
        f"{gen.COMP}/{gen.crate_template_for(os.path.basename(spec.case)[:-7])}.prefab",
        str(ROOT))
    _height = (_tpl_h[1][1] - _tpl_h[0][1]) * _scale[1]
    if os.path.basename(spec.case)[:-len(".prefab")] in gen.CRATE_READY_MADE:
        # Ящик нарисован вручную специально под раму: он ВЫШЕ лампы и это
        # осознанно — иначе рама торчала бы наружу. Он не едет через портал
        # под потолком, а появляется у точки выдачи.
        check(_height > 10.0,
              f"{spec.key}: ручной ящик {_height:.2f} накрывает раму целиком")
    else:
        check(_height <= 5.69,
              f"{spec.key}: ящик высотой {_height:.2f} проходит под лампой (5.69)")

# Ящик НЕ обязан вмещать груз: содержимое появляется только после того, как
# ящик уничтожен (Box на BrokenCrate). Требование "коробка не мельче груза"
# отменено — из-за него ящик BigMiner раздувался до 11 единиц и упирался в
# потолок. Проверяем обратное: масштаб выбран ровно тот, что считает генератор.
for spec in gen.BUILDS:
    _resolved = ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT))
    _want = gen.crate_scale_for(spec.case, _resolved)
    _got = root_scale(f"{gen.OUT_PREFABS}/Crate_{spec.key}.prefab", str(ROOT))
    check(all(abs(_got[i] - _want[i]) < 1e-9 for i in range(3)),
          f"{spec.key}: масштаб ящика {tuple(round(v, 2) for v in _got)} "
          f"совпадает с расчётным {_want}")

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
print("\nПроизводительность: нет мусора в каждом кадре")

# Жалобы: 20 FPS на Huawei Nova y91 и Redmi 10X, бэкенд пометил игру как
# power-hungry. Это нагрузка на процессор, а не на видео: на низких
# настройках тени уже выключены.

# Часы ОС собирали строку КАЖДЫЙ кадр ради значения, которое меняется раз в
# секунду. Интерполяция $"{x:00}" — это boxing трёх int плюс склейка, то есть
# мусор в куче на каждый кадр каждого компьютера в мире.
_os_src = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS"
           / "OperatingSystem.cs").read_text(encoding="utf-8")
_os_update = _os_src.split("void Update()")[1].split("private int lastClockStamp")[0]
# Комментарии выкидываем: объяснение "$" внутри текста — не вызов.
_os_code = "\n".join(l for l in _os_update.splitlines()
                     if not l.strip().startswith("//"))
check("$\"" not in _os_code,
      "часы ОС не собирают интерполированную строку каждый кадр")
check("lastClockStamp" in _os_update,
      "часы обновляются только при смене секунды")
check("twoDigitCache" in _os_src,
      "двузначные числа берутся из готовой таблицы, без ToString в кадре")

# Поле ввода каждый кадр склеивало имя каретки и звало Transform.Find.
# Для поля, которого игрок не касался, это продолжалось всю партию.
_ifm = (ROOT / "Assets/Scripts/Assembly-CSharp/InputFieldMod.cs").read_text(
    encoding="utf-8")
_ifm_update = _ifm.split("private void Update()")[1]
check("cachedCaretName" in _ifm_update,
      "имя каретки считается один раз, а не каждый кадр")
check("childCount == 0" in _ifm_update,
      "пока у поля нет детей, Find не вызывается вовсе")
check('+ " Input Caret"' not in _ifm_update.split("cachedCaretName == null")[-1][:200]
      or _ifm_update.count('+ " Input Caret"') == 1,
      "строка имени склеивается максимум один раз за жизнь поля")

# Спираль смерти физики: Maximum Allowed Timestep задаёт, сколько шагов
# физики Unity разрешает догнать за один кадр. При 0.333 и шаге 0.02 это до
# 16 шагов подряд — чем медленнее устройство, тем больше работы, тем
# медленнее кадр.
_time = (ROOT / "ProjectSettings/TimeManager.asset").read_text(encoding="utf-8")
_fixed = float(re.search(r"Fixed Timestep: ([\d.]+)", _time).group(1))
_maxstep = float(re.search(r"Maximum Allowed Timestep: ([\d.]+)", _time).group(1))
_catchup = _maxstep / _fixed
check(_catchup <= 5.0,
      f"физика догоняет максимум {_catchup:.0f} шагов за кадр — нет спирали смерти")
check(_maxstep >= _fixed * 2,
      "но запас на догон остаётся: физика не встаёт при просадке")

print("\nПроизводительность: ничего дорогого не остаётся навсегда")

_sp_perf = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs").read_text(
    encoding="utf-8")

# Solver 40 против штатных 6 и ContinuousDynamic — самый дорогой режим
# коллизий. Нужны на время сборки, но если оставить их навсегда, каждый
# купленный ПК до конца партии считается в разы дороже штатного. На слабом
# телефоне это заметно проседающий FPS.
_dyn = (ROOT / "ProjectSettings/DynamicsManager.asset").read_text(encoding="utf-8")
_default_solver = int(re.search(r"m_DefaultSolverIterations: (\d+)", _dyn).group(1))
_used_solver = int(re.search(r"baseSolverIterations = (\d+)", _sp_perf).group(1))
check(_used_solver > _default_solver,
      f"солвер сборки ({_used_solver}) выше штатного ({_default_solver}) — "
      "потому и обязан возвращаться")

check("public int solver;" in _sp_perf,
      "в бэкапе есть поле для итераций решателя")
check("public CollisionDetectionMode collision;" in _sp_perf,
      "в бэкапе есть поле для режима коллизий")
check("solverIterations = backup.solver" in _sp_perf,
      "итерации решателя возвращаются к исходным")
check("collisionDetectionMode = backup.collision" in _sp_perf,
      "режим коллизий возвращается к исходному")

# Бэкап обязан писаться БЕЗУСЛОВНО. Раньше запись стояла внутри проверки
# массы: если масса уже была достаточной, солвер менялся, но в бэкап не
# попадал — и оставался задранным навсегда.
_base_block = _sp_perf.split("var baseBody = root.GetComponent<Rigidbody>();")[1]
_base_block = _base_block.split("yield return null;")[0]
# Проверять один лишь порядок мало: условие можно навесить прямо на саму
# строку Add, и порядок останется прежним. Требуем, чтобы Add начинался с
# начала строки без всякого if.
_add_line = None
for _ln in _base_block.splitlines():
    if "restoreMass.Add" in _ln:
        _add_line = _ln.strip()
        break
check(_add_line is not None and _add_line.startswith("restoreMass.Add"),
      f"бэкап основы пишется безусловно (строка: {_add_line!r})")

_add_at = _base_block.find("restoreMass.Add")
_if_at = _base_block.find("if (baseBody.mass < wanted)")
check(_add_at != -1 and _if_at != -1 and _add_at < _if_at,
      "бэкап основы пишется ДО проверки массы, а не внутри неё")

# Готовый ПК должен засыпать: спящие тела PhysX не считает. У флагмана это
# 28 Rigidbody, которые иначе крутятся в расчёте до самоуспокоения.
_restore = _sp_perf.split("private void RestoreMasses()")[1].split("private void")[0]
check(".Sleep()" in _restore,
      "после сборки тела усыпляются, а не будятся")
check(".WakeUp()" not in _restore,
      "WakeUp в RestoreMasses больше нет — он мешал связке заснуть")

# Магазин: карточки строятся по требованию, а не все разом.
_panel = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Shop/ShopPanel.cs").read_text(
    encoding="utf-8")
check("private void BuildPage(int index)" in _panel,
      "у панели магазина есть отложенная сборка страницы")
_awake = _panel.split("private void Awake()")[1].split("private void BuildPage")[0]
check("ui.Init(" not in _awake and "selectionPrefab" not in _awake,
      "Awake магазина больше не создаёт карточки всех страниц")
check("BuildPage(i)" in _panel or "BuildPage(index)" in _panel,
      "страница строится при показе")

# Сколько карточек это экономит на старте — считаем по реальному Shop.asset.
_shop_txt = (ROOT / "Assets/MonoBehaviour/Shop.asset").read_text(encoding="utf-8")
_pages = re.split(r"- pageName: ", _shop_txt)[1:]
_counts = [len(re.findall(r"\{fileID: -?\d+, guid: [0-9a-f]{32}, type: 2\}", b))
           for b in _pages]
_total_cards = sum(_counts)
_worst_page = max(_counts) if _counts else 0
check(_total_cards > _worst_page * 2,
      f"отложенная сборка экономит: {_total_cards} карточек разом против "
      f"{_worst_page} на самой большой странице")

print("\nНазвания сборок переведены целиком")

# Названия задаёт пользователь, и живут они только в Translate.txt — ключи
# вида {Office PC} не меняются. Проверяем, что ни один язык не остался с
# английской заглушкой и что строка TSV не поехала по колонкам.
_tr_rows = [l.rstrip("\n").split("\t")
            for l in (ROOT / "Assets/Resources/Translate.txt").read_text(
                encoding="utf-8").splitlines() if l.strip()]
_tr_width = len(_tr_rows[0])
_tr = {r[0]: r for r in _tr_rows}

_title_keys = set()
for spec in gen.BUILDS:
    for _k in re.findall(r"\{([^}]*)\}", spec.title):
        _title_keys.add(_k)

for _k in sorted(_title_keys):
    check(_k in _tr, f"название '{_k}' есть в Translate.txt")
    if _k not in _tr:
        continue
    check(len(_tr[_k]) == _tr_width,
          f"'{_k}': строка не поехала по колонкам ({len(_tr[_k])} из {_tr_width})")
    _vals = [v for v in _tr[_k][1:] if v.strip()]
    check(len(_vals) == _tr_width - 1,
          f"'{_k}': переведён на все языки, пустых нет")

# Название и описание — разные вещи: в описании лежат характеристики, и
# переименование не должно было их задеть.
for spec in gen.BUILDS:
    _d = spec.description
    if _d in _tr:
        check("/" in _tr[_d][1], f"'{_d}': описание осталось перечнем железа")

print("\nДоски ручного ящика не превращаются в обычные")

# При загрузке сейва SaveManager воссоздаёт предмет из Resources/Components/
# по его spawnId. Доски ручного ящика BigMiner несли spawnId "Part".."Part_5"
# — те же, что у обычного ящика, — и после перезахода игра подставляла им
# ОБЫЧНЫЕ доски: коробка теряла блендер-геометрию.
_bm_tpl = (ROOT / gen.COMP / "Crate_BigMiner.prefab").read_text(encoding="utf-8")
_bm_ids = re.findall(r"spawnId: (.*)", _bm_tpl)
_bm_planks = [i.strip() for i in _bm_ids if i.strip().startswith(("Part", "BigMinerPart"))]
check(_bm_planks and all(i.startswith("BigMinerPart") for i in _bm_planks),
      "у досок ручного ящика собственные spawnId, а не общие Part*")

# Каждому spawnId обязан соответствовать префаб в Resources/Components,
# иначе SaveManager пишет "Prefab of ... not found!" и доска пропадает.
for _sid in _bm_planks:
    check((ROOT / gen.COMP / f"{_sid}.prefab").exists(),
          f"префаб {_sid}.prefab существует — SaveManager его найдёт")
    _pl = (ROOT / gen.COMP / f"{_sid}.prefab").read_text(encoding="utf-8")
    check(f"spawnId: {_sid}" in _pl,
          f"{_sid}: spawnId внутри префаба совпадает с именем файла")

# Геометрия досок — из блендер-модели, а не от обычного ящика.
_fbx_guid = re.search(
    r"guid: ([0-9a-f]{32})",
    (ROOT / gen.COMP / "box for bigminer.fbx.meta").read_text(encoding="utf-8")).group(1)
for _sid in _bm_planks:
    _pl = (ROOT / gen.COMP / f"{_sid}.prefab").read_text(encoding="utf-8")
    check(_fbx_guid in _pl, f"{_sid}: меш взят из блендер-модели")

# А у обычных ящиков доски остаются обычными — их трогать не надо.
for spec in gen.BUILDS:
    if os.path.basename(spec.case)[:-len(".prefab")] in gen.CRATE_READY_MADE:
        continue
    _ct = (ROOT / gen.OUT_PREFABS / f"Crate_{spec.key}.prefab").read_text(encoding="utf-8")
    check("BigMinerPart" not in _ct,
          f"{spec.key}: обычный ящик не тянет доски от большой коробки")

print("\nСборка устойчива к помехам")

# App Downloader обязан быть на каждой машине: без него систему нечем
# пополнять и предустановленный набор становится потолком. В обычной игре
# его приносит мастер установки, а у готовых сборок мастера нет.
for spec in gen.BUILDS:
    check("Downloader" in spec.apps,
          f"{spec.key}: App Downloader предустановлен")
    _t = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    check("App Downloader" in _t,
          f"{spec.key}: App Downloader.exe попал в префаб спавнера")


_sp = (ROOT / "Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs").read_text(
    encoding="utf-8")

# Вся сборка идёт за один кадр. Пауза 0.05 c на деталь давала 1.35 секунды
# живой физики у флагмана — всё это время недособранная машина стояла в мире
# и её мог задеть игрок, обломки ящика или собственная деталь.
for spec in gen.BUILDS:
    _t = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    _sd = re.search(r"stepDelay: ([-\d.e]+)", _t)
    check(_sd is not None and abs(float(_sd.group(1))) < 1e-9,
          f"{spec.key}: stepDelay = 0, сборка укладывается в один кадр")

# Деталь до подключения — обычное физическое тело. Гасим её движение, иначе
# она успевает отскочить от обломков ящика.
check("spawnedBody.velocity = Vector3.zero" in _sp,
      "скорость детали гасится до установки в слот")

# Но кинематической деталь делать нельзя — это ломало FixedJoint и приём
# в слот, проверено дважды.
check("spawnedBody.isKinematic" not in _sp,
      "деталь НЕ делается кинематической (это ломало приём в слот)")

# Одна неудачная попытка не должна оставлять сборку неполной навсегда:
# Slot.Start держит preparing целую секунду, и первая попытка может прийтись
# ровно на это окно.
check("attachRetries" in _sp, "у спавнера есть повтор попытки подключения")
_slot_cs = (ROOT / "Assets/Scripts/Assembly-CSharp/Slot.cs").read_text(encoding="utf-8")
check("WaitForSeconds(1f)" in _slot_cs,
      "Slot.Start действительно держит preparing секунду — повтор оправдан")
for spec in gen.BUILDS:
    _t = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    _ar = re.search(r"attachRetries: (\d+)", _t)
    check(_ar is not None and int(_ar.group(1)) >= 5,
          f"{spec.key}: повторов подключения хватит переждать preparing")

# Неполная сборка обязана быть заметной, а не молча уезжать к игроку.
check("сборка неполная" in _sp,
      "спавнер сообщает, если часть деталей не встала")

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

# Пирамида двухуровневая: утяжелить одну раму мало. Материнская плата весит
# 1.0 и несёт CPU, кулер и память — ещё 2.5. Её слот объявлен setParent: 0,
# поэтому плата НЕ становится потомком рамы и держится на одном FixedJoint.
# Пока в раму приезжали 16 видеокарт, рама дёргалась, джойнт рвался — и плата
# отваливалась вместе с процессором и кулером.
check("private void StabilizeHost" in _spawner_src,
      "у спавнера есть StabilizeHost для детали-опоры")
# Метод мало объявить — его должны ВЫЗЫВАТЬ в цикле сборки, сразу после
# того как деталь добавлена в hosts.
_build_part = _spawner_src.split("private IEnumerator Build()")[1].split(
    "private void StabilizeHost")[0]
check("StabilizeHost(" in _build_part,
      "спавнер вызывает StabilizeHost в цикле сборки, а не просто объявляет")
check("hostPrefabName" in _spawner_src,
      "у детали записан хозяин слота — по нему считается нагрузка на опору")

# Материнскую плату утяжелять НЕЛЬЗЯ. Нагрузка на её крепление это F = m*a,
# а держится она на ЕДИНСТВЕННОМ FixedJoint: слот объявлен setParent: 0,
# потомком рамы плата не становится. Утяжеление в десять раз во столько же
# увеличивает силу на этом джойнте — при breakForce 800 хватает ускорения 80,
# и плата срывается вместе с процессором и кулером.
_stab_body = _spawner_src.split("private void StabilizeHost")[1].split(
    "private bool Attach")[0]
# Массу опоры менять нельзя, но ЗАПОМНИТЬ её в бэкапе нужно: там же лежат
# итерации решателя, которые StabilizeHost поднимает и обязан вернуть.
# Поэтому ищем не упоминание body.mass вообще, а именно присваивание.
check(not re.search(r"body\.mass\s*=", _stab_body),
      "StabilizeHost НЕ меняет массу детали-опоры (плату это срывало)")
check("mass = body.mass" in _stab_body,
      "но исходную массу опоры он запоминает вместе с остальной физикой")
check("solverIterations" in _stab_body,
      "StabilizeHost всё же поднимает итерации решателя — они ничего не весят")
# Бэкапов теперь два: основа и опора. Опору не утяжеляем, но ей поднимают
# итерации решателя — значит и её нужно вернуть в исходное состояние.
check(_spawner_src.count("restoreMass.Add") == 2,
      "бэкап пишется и для основы, и для опоры (солвер возвращают обоим)")
_mass_writes = re.findall(r"^\s*(?:base)?[Bb]ody\.mass\s*=", _spawner_src, re.M)
check(len(_mass_writes) == 1,
      f"массу меняем ровно в одном месте — только основе (нашлось {len(_mass_writes)})")

# Плата и всё, что стоит на ней, ставятся ПОСЛЕДНИМИ: иначе каждая из
# шестнадцати видеокарт трясёт раму, и удар приходится по единственному
# креплению уже собранной платы.
for spec in gen.BUILDS:
    _resolved = sorted(
        ReadyBuild(spec.key, spec.case, spec.parts).resolve(str(ROOT)),
        key=lambda r: r["order"])
    _board = [r for r in _resolved if r["target"] == "Motherboard"]
    if not _board:
        continue
    _bo = _board[0]["order"]
    _bn = _board[0]["name"]

    _after = [r for r in _resolved
              if r["order"] > _bo and r["host"] != _bn]
    check(not _after,
          f"{spec.key}: после платы (#{_bo}) в раму больше ничего не ставится")

    # Но сама плата обязана встать раньше своих процессора и памяти.
    _kids = [r for r in _resolved if r["host"] == _bn]
    check(all(k["order"] > _bo for k in _kids),
          f"{spec.key}: плата встаёт раньше своих {len(_kids)} деталей")

# КРИТИЧНО: тяжесть — временный костыль на время сборки, а НЕ свойство
# готовой сборки. Игра таскает предметы SpringJoint'ом (Raycast.cs,
# targetSpring = 100), провисание пружины это m*g/k — рама массой 108
# провисла бы на 10.6 метра, её физически невозможно поднять и перенести.
_raycast = (ROOT / "Assets/Scripts/Assembly-CSharp/Raycast.cs").read_text(encoding="utf-8")
_spring = re.search(r"targetSpring\s*=\s*([\d.]+)f", _raycast)
check(_spring is not None,
      "в Raycast.cs есть жёсткость пружины переноса — от неё зависит предел массы")
if _spring:
    _k = float(_spring.group(1))
    _sag = 108.0 * 9.81 / _k
    check(_sag > 1.5,
          f"при жёсткости {_k:.0f} масса 108 давала бы провисание {_sag:.1f} м — "
          f"поэтому массу обязательно возвращать")

check("RestoreMasses" in _spawner_src,
      "спавнер умеет возвращать исходные массы")
# Считать вхождения мало: объявление метода и вызов в OnDestroy дают два
# совпадения даже когда возврат из цикла сборки удалён — то есть ровно при
# том баге, из-за которого готовый майнер весил 108 и его нельзя было поднять.
_build_only = _spawner_src.split("private IEnumerator Build()")[1].split(
    "private struct MassBackup")[0].split("private void RestoreMasses")[0]
check("RestoreMasses()" in _build_only,
      "массы возвращаются В САМОМ методе сборки, а не только в OnDestroy")
_ondestroy = _spawner_src.split("private void OnDestroy()")[1][:200] \
    if "private void OnDestroy()" in _spawner_src else ""
check("RestoreMasses()" in _ondestroy,
      "OnDestroy тоже возвращает массы")
check("private void OnDestroy" in _spawner_src,
      "массы возвращаются даже если спавнер уничтожат посреди сборки")
check("restoreMass.Add" in _spawner_src,
      "перед утяжелением спавнер запоминает настоящую массу")

# Возврат массы должен идти ПОСЛЕ установки деталей, иначе смысла нет.
# Резкая смена массы без гашения скорости даёт рывок и срывает детали.
_restore_body = _spawner_src.split("private void RestoreMasses()")[1][:700]
check("velocity = Vector3.zero" in _restore_body,
      "перед сменой массы гасится скорость — иначе рывок сорвёт детали")
check("angularVelocity = Vector3.zero" in _restore_body,
      "угловая скорость тоже гасится")

# Хозяин обязан доехать до префаба, иначе спавнер не поймёт, что укреплять.
for spec in gen.BUILDS:
    _text = (ROOT / gen.OUT_PREFABS / f"{spec.key}.prefab").read_text(encoding="utf-8")
    _n_parts = _text.count("    slotTarget:")
    check(_text.count("    hostPrefabName:") == _n_parts,
          f"{spec.key}: у всех {_n_parts} деталей записан hostPrefabName")

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
# Полувысоту берём у ШАБЛОНА КОНКРЕТНОЙ сборки, а не у одного корпусного
# ящика: у BigMiner ящик собственный, нарисованный в Blender под раму, и его
# пивот лежит совсем на другой высоте.
for spec in gen.BUILDS:
    _tpl_name = gen.crate_template_for(os.path.basename(spec.case)[:-len(".prefab")])
    _tb = prefab_bounds(f"{gen.COMP}/{_tpl_name}.prefab", str(ROOT))
    _half = -_tb[0][1]
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
    # Порог "не выше 2.0" был привязан к прежнему размеру ящика и сломался,
    # как только стенки подогнали под груз: подъём считается от полувысоты
    # ящика, и более высокая коробка честно требует большего подъёма.
    #
    # Проверяем не число, а СМЫСЛ: дно груза обязано оказаться ровно на
    # CRATE_GROUND_CLEARANCE над полом. Это ловит и утопление в пол, и
    # задранный подъём (5.24 загонял раму внутрь обломков).
    _bottom = uat.prefab_bounds(spec.case, str(ROOT))[0][1] \
        * uat.root_scale(spec.case, str(ROOT))[1]
    _tpl = f"{gen.COMP}/{gen.crate_template_for(os.path.basename(spec.case)[:-7])}.prefab"
    _pivot = -uat.prefab_bounds(_tpl, str(ROOT))[0][1] * _k
    _clear = _pivot + _bottom + _got
    # Груз не должен родиться в полу...
    check(_clear >= gen.CRATE_GROUND_CLEARANCE - 0.02,
          f"{spec.key}: дно груза на {_clear:.2f} над полом — не утоплено")
    # ...и не должен быть задран: 5.24 загоняло раму внутрь обломков ящика.
    # Подъём даём только тем, кому он нужен, и ровно минимальный.
    if _got > 0:
        check(abs(_clear - gen.CRATE_GROUND_CLEARANCE) < 0.02,
              f"{spec.key}: подъём {_got:.2f} ровно минимальный "
              f"(дно на {_clear:.2f})")

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

# ---------------------------------------------------------------------------
print("\nПроизводительность: экраны мониторов")

# Каждый экран внутриигрового ПК — это отдельная камера, снимающая Canvas в
# RenderTexture. Пока камера включена, Unity рисует её каждый кадр, независимо
# от того, изменилось ли содержимое и видит ли игрок этот монитор.


def _strip_comments(text):
    """Убрать комментарии, чтобы не ловить паттерны в собственных пояснениях."""
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return "\n".join(ln for ln in text.splitlines()
                     if not ln.strip().startswith("//"))


_dm_path = ROOT / "Assets/Scripts/Assembly-CSharp/DisplayManager.cs"
_dm_src = _strip_comments(_dm_path.read_text(encoding="utf-8"))

_create = _dm_src.split("public RenderTexture CreateDisplay")[1].split("\n\tpublic ")[0]

# Главная находка: монитор просит текстуру 256x256, а Mathf.Max поднимал её до
# 1280x720 — в четырнадцать раз больше пикселей, всегда, на любом устройстве.
# Смотрим ВЕСЬ файл, а не тело CreateDisplay: расчёт размера вынесен в
# отдельный метод, и проверка только вызывающего кода мутацию пропускает.
check(not re.search(r"Mathf\.Max\(\s*width\s*,\s*1280\s*\)", _dm_src),
      "экран не растягивается принудительно до 1280 по ширине")
check(not re.search(r"Mathf\.Max\(\s*height\s*,\s*720\s*\)", _dm_src),
      "экран не растягивается принудительно до 720 по высоте")
# Заодно запрещаем любую жёсткую нижнюю границу в размере экрана.
check(not re.search(r"Mathf\.Max\(\s*(?:width|height)\s*,\s*\d{3,}\s*\)", _dm_src),
      "нижняя граница размера экрана не задана константой напрямую")

# Резкость экранов возвращена на максимум: понижать её ради кадров нельзя,
# текст на мониторе читают вблизи. Экономию даёт частота перерисовки, а не
# размер текстуры, поэтому здесь проверяем ТОЛЬКО пропорции и потолок.

# Пропорции. Префабы просят 512x256 (2:1) и 256x128, а форсированное
# 1280x720 — это 16:9: старый код растягивал картинку.
check(re.search(r"Mathf\.Min\(\(float\)MaxWidth / width, \(float\)MaxHeight / height\)",
                _dm_src) is not None,
      "экран вписывается в рамку по обеим сторонам, сохраняя пропорции")
check(re.search(r"width = Mathf\.Max\(64, Mathf\.RoundToInt\(width \* k\)\)", _dm_src)
      is not None and
      re.search(r"height = Mathf\.Max\(64, Mathf\.RoundToInt\(height \* k\)\)", _dm_src)
      is not None,
      "обе стороны экрана масштабируются одним коэффициентом")

# Потолок остался прежним — резкость не урезана.
_mw = re.search(r"MaxWidth = (\d+)", _dm_src)
_mh = re.search(r"MaxHeight = (\d+)", _dm_src)
check(_mw is not None and _mh is not None, "предельный размер экрана задан")
if _mw and _mh:
    _W, _H = int(_mw.group(1)), int(_mh.group(1))
    check(_W >= 1280 and _H >= 720,
          f"резкость экрана не урезана относительно исходной ({_W}x{_H})")

    # Пересчитываем сами, независимо от кода.
    def _fit(w, h):
        k = min(_W / w, _H / h)
        return round(w * k), round(h * k)

    _cw, _ch = _fit(512, 256)
    check(abs(_cw / _ch - 2.0) < 0.01,
          f"пропорции CurvedMonitor сохранены ({_cw}x{_ch})")
    check(_cw >= 512 and _ch >= 256,
          f"CurvedMonitor не мыльнее запрошенного префабом ({_cw}x{_ch})")

    # Ключевая ловушка: квадратный экран не должен стать 1280x1280, это
    # ДОРОЖЕ старого поведения — оптимизация превратилась бы в регрессию.
    _sw, _sh = _fit(256, 256)
    check(abs(_sw / _sh - 1.0) < 0.01,
          f"квадратный экран остаётся квадратным ({_sw}x{_sh})")
    check(_sw * _sh <= 1280 * 720,
          f"квадратный экран не дороже прежних 1280x720 ({_sw * _sh:,})")

    # Ни один вариант не должен превышать прежнюю цену кадра.
    for _pw, _ph in [(512, 256), (256, 128), (256, 256), (1920, 1080)]:
        _rw, _rh = _fit(_pw, _ph)
        check(_rw * _rh <= 1280 * 720,
              f"экран {_pw}x{_ph} не дороже прежних 1280x720 ({_rw}x{_rh})")

# MSAA x4 на экране умножает работу растеризатора вчетверо ради сглаживания,
# которого на тексте интерфейса почти не видно.
check("rt.antiAliasing = 4" not in _create,
      "сглаживание экрана не прибито к x4")
_aa = [int(x) for x in re.findall(r"if \(level <= \d+\) return (\d+);", _dm_src)]
check(_aa and min(_aa) == 1,
      "на слабых устройствах сглаживание экрана отключено полностью")

# Анизотропия на плоском экране, который смотрят фронтально, бесполезна.
check("rt.anisoLevel = 4" not in _create, "анизотропия экрана не выкручена в 4")

# Камера не должна оставаться включённой: это и есть рендер каждый кадр.
check(re.search(r"cam\.enabled\s*=\s*false", _create) is not None,
      "камера экрана создаётся выключенной")
check(re.search(r"cam\.enabled\s*=\s*true", _dm_src) is None,
      "камера экрана нигде не включается обратно на постоянный рендер")

# Раз камера выключена, кто-то обязан рисовать её по расписанию, иначе экран
# застынет навсегда. Это отдельная ловушка: отключить рендер легко, а вот
# забыть про перерисовку — значит сломать игру вместо оптимизации.
_pacer_path = ROOT / "Assets/Scripts/Assembly-CSharp/DisplayCameraPacer.cs"
check(_pacer_path.exists(), "есть компонент, обновляющий экран по расписанию")
check("DisplayCameraPacer" in _create,
      "камера экрана получает этот компонент при создании")

_pacer_src = _strip_comments(_pacer_path.read_text(encoding="utf-8"))
check("target.Render()" in _pacer_src, "расписание действительно рисует кадр")

# Частота обновления должна быть ниже кадровой (иначе смысла нет), но не
# настолько низкой, чтобы интерфейс выглядел зависшим. Порог независимый:
# сравниваем с targetFrameRate по умолчанию (30) из FpsSetting.
_rate = re.search(r"redrawsPerSecond = (\d+(?:\.\d+)?)f", _pacer_src)
check(_rate is not None, "частота перерисовки экрана задана явно")
if _rate:
    _r = float(_rate.group(1))
    check(_r <= 20.0, f"экран обновляется реже 20 раз в секунду (сейчас {_r})")
    check(_r >= 8.0, f"экран обновляется не реже 8 раз в секунду (сейчас {_r})")

# Невидимый монитор не должен рисоваться вообще.
_sda = _dm_src.split("public void SetDisplayActive")[1].split("\n\tpublic ")[0]
check("SetVisible" in _sda,
      "уход монитора из поля зрения останавливает перерисовку")
check("visible" in _pacer_src and re.search(r"if \(.*!visible.*\) return", _pacer_src),
      "невидимый экран пропускает отрисовку")

# ---------------------------------------------------------------------------
print("\nПроизводительность: поиск монитора под курсором")

_mr_src = _strip_comments(
    (ROOT / "Assets/Scripts/Assembly-CSharp/MonitorReceiver.cs")
    .read_text(encoding="utf-8"))
_mr_update = _mr_src.split("private void Update()")[1]

# Camera.main — поиск объекта по тегу среди всей сцены, каждый кадр.
# Один вызов допустим — как восстановление потерянной ссылки, но он обязан
# стоять под проверкой на null, иначе поиск по тегу снова идёт каждый кадр.
_bare_main = [ln.strip() for ln in _mr_update.splitlines()
              if "Camera.main" in ln
              and not re.match(r"if \(cachedCamera == null\)", ln.strip())]
check(not _bare_main,
      f"Camera.main не вызывается безусловно каждый кадр (нашлось: {_bare_main})")
check("cachedCamera" in _mr_src, "ссылка на камеру закеширована")
# Кеш обязан уметь восстанавливаться, иначе пересоздание камеры сломает ввод.
check(re.search(r"if \(cachedCamera == null\) cachedCamera = Camera\.main", _mr_src)
      is not None,
      "камера ищется заново, если ссылка потерялась")

# Луч без ограничений проверяет каждый коллайдер сцены, а их тут сотни.
_ray = re.search(r"Physics\.Raycast\(ray, out var hit([^)]*)\)", _mr_update)
check(_ray is not None, "Raycast на месте")
if _ray:
    check("RayDistance" in _ray.group(1),
          "у луча есть предельная дистанция")
    check("QueryTriggerInteraction.Ignore" in _ray.group(1),
          "луч не цепляет триггеры слотов")

_dist = re.search(r"RayDistance = (\d+(?:\.\d+)?)f", _mr_src)
check(_dist is not None, "дистанция луча задана константой")
if _dist:
    _d = float(_dist.group(1))
    check(_d >= 5.0, f"дистанции хватает, чтобы дотянуться до монитора ({_d})")
    check(_d <= 50.0, f"луч не идёт через всю сцену ({_d})")

# Пока мониторов нет, работать вообще незачем.
check(re.search(r"if \(targets == null \|\| targets\.Count == 0\) return", _mr_update)
      is not None,
      "без мониторов Update выходит сразу")


# ---------------------------------------------------------------------------
print("\nИгровые часы вместо системных")

# Часы в PCOS и на LED-дисплее показывали System.DateTime.Now — реальное время
# телефона. Теперь оба берут время из игры.

_gc_path = ROOT / "Assets/Scripts/Assembly-CSharp/GameClock.cs"
check(_gc_path.exists(), "есть общий источник игрового времени")
_gc_src = _strip_comments(_gc_path.read_text(encoding="utf-8"))
check((_gc_path.parent / "GameClock.cs.meta").exists(),
      "у GameClock есть .meta, иначе Unity его не увидит")

# Время обязано идти от игрового прогресса, а не от системных часов.
check("Main.Instance" in _gc_src and "playTime" in _gc_src,
      "игровое время считается от Main.playTime")
check("DateTime" not in _gc_src,
      "в источнике игрового времени нет обращений к системным часам")

# Длина игровых суток нужна и ниже, для проверки частоты обновления часов.
_dl_m = re.search(r"DayLengthSeconds = ([\d.]+)f \* ([\d.]+)f", _gc_src)
_day_len_ok = _dl_m is not None
_dl_val = float(_dl_m.group(1)) * float(_dl_m.group(2)) if _dl_m else 0.0

# playTime уже сохраняется — значит часы переживают перезаход.
_sm_src = (ROOT / "Assets/Scripts/Assembly-CSharp/SaveManager.cs").read_text(
    encoding="utf-8")
check("game.playtime = main.playTime" in _sm_src,
      "playTime записывается в сохранение")
check("Main.Instance.playTime = Loader.GameData.playtime" in _sm_src,
      "playTime читается из сохранения, часы не сбрасываются при перезаходе")

# Часы PCOS.
_os_now = _strip_comments(
    (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS"
     / "OperatingSystem.cs").read_text(encoding="utf-8"))
_clock_block = _os_now.split("if (clockText != null)")[1].split("lastClockStamp = -1")[0]
check("DateTime.Now" not in _clock_block,
      "часы PCOS не читают системное время")
check("GameClock." in _clock_block, "часы PCOS берут время из GameClock")

# Формат ЧЧ:ММ — секунды убраны по просьбе. Проверяем и наличие часов с
# минутами, и отсутствие секунд: иначе тест пропустит любой из двух промахов.
check("GameClock.Hour" in _clock_block and "GameClock.Minute" in _clock_block,
      "часы PCOS показывают часы и минуты")
check("GameClock.Second" not in _clock_block,
      "секунды в часах PCOS не показываются")
# Разделитель должен остаться ровно один: "ЧЧ:ММ", а не "ЧЧ:ММ:".
check(_clock_block.count('":"') == 1,
      f"в часах ровно один разделитель (нашлось {_clock_block.count(chr(34) + ':' + chr(34))})")

# Метка перерисовки обязана считаться по МИНУТАМ, а не по секундам. Игровая
# секунда идёт в 60 раз быстрее реальной, поэтому посекундная метка менялась бы
# почти каждый кадр и вся экономия на строке пропала бы.
_stamp = _gc_src.split("public static int Stamp")[1].split("\n")[0]
check("/ 60f" in _stamp,
      f"метка часов считается по игровым минутам, а не секундам ({_stamp.strip()})")

# Независимая проверка арифметики: как часто метка меняется в реальном времени.
if _day_len_ok:
    _per_real = 86400.0 / _dl_val          # игровых секунд за реальную секунду
    _stamp_hz = _per_real / 60.0           # смен метки в реальную секунду
    check(_stamp_hz <= 2.0,
          f"строка часов пересобирается не чаще 2 раз в секунду ({_stamp_hz:.2f} Гц)")
    check(_stamp_hz >= 0.2,
          f"часы всё же обновляются заметно для игрока ({_stamp_hz:.2f} Гц)")

# LED-дисплей.
_led_src = _strip_comments(
    (ROOT / "Assets/Scripts/Assembly-CSharp/LedDisplay.cs").read_text(
        encoding="utf-8"))
_led_clock = _led_src.split("class ClockAnimation")[1].split("class ")[0]
check("DateTime.Now" not in _led_clock,
      "LED-дисплей не читает системное время")
check("GameClock." in _led_clock, "LED-дисплей берёт время из GameClock")

# Оба показывают ОДНО И ТО ЖЕ время — иначе часы в комнате и на компьютере
# разойдутся, и это будет выглядеть как баг.
check("GameClock.Hour" in _led_clock and "GameClock.Hour" in _clock_block,
      "и дисплей, и PCOS считают час одинаково")

# Оптимизация часов не должна потеряться: строка пересобирается по метке.
check("GameClock.Stamp" in _clock_block,
      "строка часов пересобирается только при смене игровой минуты")

# Арифметика времени, проверенная независимо от кода.
_day_len = re.search(r"DayLengthSeconds = ([\d.]+)f \* ([\d.]+)f", _gc_src)
check(_day_len is not None, "длина игровых суток задана явно")
if _day_len:
    _dl = float(_day_len.group(1)) * float(_day_len.group(2))
    # Сутки должны быть заметно короче реальных, иначе смысла в игровом
    # времени нет, но не настолько, чтобы часы мелькали.
    check(_dl <= 3600.0, f"игровые сутки короче реального часа ({_dl:.0f}с)")
    check(_dl >= 300.0, f"игровые сутки не мельтешат ({_dl:.0f}с)")

    # Пересчитываем показания часов сами и сверяем с формулой из кода.
    _start = re.search(r"StartHour = (\d+)f", _gc_src)
    check(_start is not None, "час начала игры задан")
    if _start:
        _sh = float(_start.group(1))
        check(0 <= _sh <= 23, f"час начала в пределах суток ({_sh})")
        _per = 86400.0 / _dl
        # После полных игровых суток время обязано вернуться к старту.
        _total = _sh * 3600.0 + _dl * _per
        check(abs((_total % 86400.0) / 3600.0 - _sh) < 0.01,
              "через игровые сутки часы возвращаются к часу старта")
        # И номер дня увеличивается ровно на единицу.
        _d0 = 1 + int((_sh * 3600.0) // 86400.0)
        _d1 = 1 + int(_total // 86400.0)
        check(_d1 == _d0 + 1, f"за игровые сутки счётчик дней растёт на 1 ({_d0}->{_d1})")


# ---------------------------------------------------------------------------
print("\nАртефакты повреждённой видеокарты")

_gpu_path = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/GPU.cs"
check(_gpu_path.exists(), "есть отдельный класс видеокарты")
_gpu_src = _strip_comments(_gpu_path.read_text(encoding="utf-8"))
check((_gpu_path.parent / "GPU.cs.meta").exists(), "у GPU есть .meta")

check("class GPU : Hardware" in _gpu_src, "GPU наследует Hardware")

# Повреждение копится по ступеням, а не сразу убивает карту.
_lv = re.search(r"MaxArtifactLevel = (\d+)", _gpu_src)
check(_lv is not None, "число ступеней повреждения задано")
if _lv:
    _n = int(_lv.group(1))
    check(_n >= 2, f"ступеней больше одной, иначе это обычная поломка ({_n})")
    check(_n <= 6, f"ступеней не слишком много ({_n})")

# Ключевое: до последней ступени карта РАБОТАЕТ, но артефачит.
check("Artifacting" in _gpu_src and "ArtifactLevel < MaxArtifactLevel" in _gpu_src,
      "есть состояние «повреждена, но работает»")
check(re.search(r"if \(ArtifactLevel >= MaxArtifactLevel\) Damage\(\)", _gpu_src)
      is not None,
      "на последней ступени карта ломается совсем")

# Сила искажения нормирована, чтобы эффект не зависел от числа ступеней.
check("ArtifactStrength" in _gpu_src and "Mathf.Clamp01" in _gpu_src,
      "сила артефактов нормирована в диапазон 0..1")

# Падение должно повреждать карту. Раньше на префабах видеокарт не было даже
# Breakable, то есть уронить их было нельзя вообще.
check("OnCollisionEnter" in _gpu_src, "видеокарта реагирует на удар")
check('CompareTag("Pillow")' in _gpu_src,
      "подушка по-прежнему спасает от повреждений, как и для другого железа")

# Уровень повреждения обязан переживать перезаход, иначе артефакты исчезнут
# после загрузки сохранения.
check('jObject["artifactLevel"] = ArtifactLevel' in _gpu_src,
      "уровень повреждения сохраняется")
check(re.search(r"token != null \? token\.ToObject<int>\(\) : 0", _gpu_src)
      is not None,
      "старые сохранения без этого поля читаются без ошибки")

# Скрипт должен стоять на самих префабах видеокарт, иначе код мёртвый.
_gpu_guid = re.search(r"guid: ([a-f0-9]+)",
                      (_gpu_path.parent / "GPU.cs.meta").read_text(encoding="utf-8")).group(1)
_cards = ["GT440", "GT1030", "GT1031", "GTX1060", "GTX1070", "GTX1070Ti",
          "GTX1080", "GTX1080Ti", "RTX2080", "RTX2080Ti", "RTX3080",
          "RTX3080Ti", "RX570", "Titan V"]
_with_gpu = 0
for _c in _cards:
    _p = ROOT / f"Assets/Resources/components/{_c}.prefab"
    if _p.exists() and _gpu_guid in _p.read_text(encoding="utf-8", errors="ignore"):
        _with_gpu += 1
check(_with_gpu == len(_cards),
      f"скрипт GPU стоит на всех базовых видеокартах ({_with_gpu}/{len(_cards)})")

# Эффект на экране.
# ЛОВУШКА: в UnityEngine есть свой Display (физический экран устройства), и
# при одновременном using PC.Component + using UnityEngine простое имя Display
# неоднозначно (CS0104). Один такой промах роняет ВСЮ сборку Assembly-CSharp,
# после чего Unity считает пропавшими скрипты на всех префабах сразу.
# Список ограничен именами, которые РЕАЛЬНО есть в UnityEngine: иначе проверка
# ругается на любой тип проекта (Bios и подобные), у которого двойника нет.
_ambiguous = ["Display", "Camera", "Random", "Object", "Debug", "Light", "Animator"]
_my_scripts = [
    "Assets/Scripts/Assembly-CSharp/GpuArtifacts.cs",
    "Assets/Scripts/Assembly-CSharp/GameClock.cs",
    "Assets/Scripts/Assembly-CSharp/DisplayCameraPacer.cs",
    "Assets/Scripts/Assembly-CSharp/DisplayManager.cs",
    "Assets/Scripts/Assembly-CSharp/PC/Component/GPU.cs",
    "Assets/Scripts/Assembly-CSharp/PC/Component/Software/Ondex.cs",
]

# Какие типы объявлены в пространстве PC.Component.
_pc_types = set()
for _root, _dirs, _files in os.walk(ROOT / "Assets/Scripts/Assembly-CSharp/PC"):
    for _f in _files:
        if not _f.endswith(".cs"):
            continue
        _t = Path(_root, _f).read_text(encoding="utf-8", errors="ignore")
        for _m in re.finditer(r"\b(?:class|struct|interface|enum)\s+(\w+)", _t):
            _pc_types.add(_m.group(1))

for _rel in _my_scripts:
    _p = ROOT / _rel
    if not _p.exists():
        continue
    _t = _p.read_text(encoding="utf-8")
    if "using PC.Component;" not in _t or "using UnityEngine;" not in _t:
        continue
    _body = _strip_comments(_t)
    _bad = [
        _n for _n in _ambiguous
        if _n in _pc_types
        and re.search(rf"\b{_n}\b", _body)
        and f"using {_n} =" not in _t
    ]
    check(not _bad,
          f"{Path(_rel).name}: неоднозначные имена разрешены явно (мешают: {_bad})")

_art_path = ROOT / "Assets/Scripts/Assembly-CSharp/GpuArtifacts.cs"
check(_art_path.exists(), "есть эффект артефактов на экране")
_art_src = _strip_comments(_art_path.read_text(encoding="utf-8"))
check("raycastTarget = false" in _art_src,
      "полосы артефактов не перехватывают нажатия по рабочему столу")
check("gpu.Damaged) continue" in _art_src,
      "мёртвая карта не рисует артефакты — она вообще не даёт сигнала")

# Артефакты должны быть видны ВЕЗДЕ, а не только на загрузке.
check("is Bios" not in _art_src,
      "эффект не ограничен экраном BIOS")

# ПРИЧИНА, по которой полос не было видно в первой версии: и загрузка системы
# (Display.ApplyScreen), и окна, и меню «Пуск» зовут SetAsLastSibling, вставая
# последними среди потомков — то есть поверх всего, что добавлено раньше.
# Поэтому эффекту нужен собственный Canvas с перекрытием сортировки.
# КРИТИЧНО: камера экрана снимает только слой UI, а new GameObject создаёт
# объект на слое Default. Из-за этого артефакты существовали, но были не видны
# вообще — камера их просто не рендерила. Объекты обязаны наследовать слой.
# Нажатие на монитор (Display.ZoomIn) переводит канвас в ScreenSpaceOverlay.
# Unity такому канвасу каждый кадр сама переписывает позицию и размер, а ZoomOut
# ещё и принудительно сбрасывает localPosition/localScale/sizeDelta. Двигать сам
# канвас нельзя — искажение затрётся именно в приближении.
_screenroot = _art_src.split("private RectTransform ScreenRoot()")[1].split("\n\t}")[0]
check("childCount" in _screenroot and "GetChild" in _screenroot,
      "искажение двигает содержимое экрана, а не сам канвас (переживает зум)")
check(re.search(r"child == layer\) continue", _screenroot) is not None,
      "слой артефактов не выбирается как картинка экрана")

# Зум перекладывает иерархию, окна зовут SetAsLastSibling при каждом касании.
_redraw_body = _art_src.split("private void Redraw(")[1].split("\n\t}")[0]
check("SetAsLastSibling" in _redraw_body,
      "слой артефактов подтверждает место наверху при каждой перерисовке")
check("sortingOrder = ArtifactSortingOrder" in _redraw_body,
      "порядок сортировки восстанавливается после смены режима канваса")

check("NewUiObject" in _art_src,
      "объекты артефактов создаются через помощник, задающий слой")
check(re.search(r"go\.layer = parent\.gameObject\.layer", _art_src) is not None,
      "созданный объект наследует слой родителя (иначе камера его не увидит)")
_raw_new = re.findall(r"new GameObject\(", _art_src)
check(len(_raw_new) == 1,
      f"прямых new GameObject не осталось, кроме помощника (нашлось {len(_raw_new)})")

check("overrideSorting = true" in _art_src,
      "у слоя артефактов своя сортировка")
_so = re.search(r"ArtifactSortingOrder = (\d+)", _art_src)
check(_so is not None, "порядок сортировки слоя задан")
if _so:
    check(int(_so.group(1)) >= 1000,
          f"слой артефактов заведомо выше окон и рабочего стола ({_so.group(1)})")
check(re.search(r"sortingOrder = ArtifactSortingOrder", _art_src) is not None,
      "порядок действительно применяется к Canvas слоя")
check(re.search(r"typeof\(Canvas\)", _art_src) is not None,
      "слой артефактов — отдельный Canvas, а не просто объект")

# Warp обязан двигать САМ экран, иначе он возит прозрачный слой и толку ноль.
# Срез строго по телу метода: "\n\t}" — закрывающая скобка на уровне класса.
# Резать по следующему комментарию нельзя, комментария может не оказаться, и
# тогда в срез попадёт соседний метод.
_warp = _art_src.split("private void DrawWarp(")[1].split("\n\t}")[0]
check("ScreenRoot()" in _warp,
      "искажение двигает картинку экрана, а не слой артефактов")
check("Parent()" not in _warp,
      "искажение не трогает слой артефактов")

# Виды сбоя. Однообразные полосы выглядели ненатурально — нужно несколько
# разных исходов, включая «всё обошлось» и синий экран.
for _g in ["None", "Stripes", "Warp", "ColorShift", "BlueScreen"]:
    check(re.search(rf"\b{_g}\b", _art_src) is not None,
          f"есть вид поведения карты: {_g}")
# Мало объявить вид сбоя — он должен и выпадать, и рисоваться. Проверяем обе
# стороны: ветку в switch и то, что PickGlitch вообще может его вернуть.
_redraw = _art_src.split("private void Redraw(")[1].split("\n\t// =")[0]
for _g, _fn in [("Stripes", "DrawStripes"), ("Warp", "DrawWarp"),
                ("ColorShift", "DrawColorShift"), ("BlueScreen", "DrawBlueScreen")]:
    check(re.search(rf"case Glitch\.{_g}:\s*\n\s*{_fn}\(", _redraw) is not None,
          f"вид сбоя {_g} разбирается в Redraw и рисуется через {_fn}")
    check(re.search(rf"return Glitch\.{_g};", _art_src) is not None,
          f"вид сбоя {_g} может выпасть при розыгрыше")
check(re.search(r"private void DrawBlueScreen\(\)", _art_src) is not None,
      "синий экран реализован отдельным методом")

# Вид сбоя выбирается ОДИН раз за загрузку. Если решать каждый кадр, виды
# замелькают вперемешку и это будет выглядеть мусором, а не поломкой.
check("private bool decided" in _art_src,
      "решение о сбое запоминается на всю загрузку")
check(re.search(r"if \(!decided\)", _art_src) is not None,
      "сбой разыгрывается только когда решение ещё не принято")
check(re.search(r"decided = false;", _art_src) is not None,
      "после загрузки решение сбрасывается, следующий запуск разыграет заново")

# Шанс, что всё обойдётся, обязан зависеть от степени повреждения.
check(re.search(r"if \(Random\.value > strength\) return Glitch\.None;", _art_src)
      is not None,
      "слегка задетая карта часто стартует нормально")
check(re.search(r"Glitch\.BlueScreen", _art_src) is not None
      and "strength" in _art_src.split("Glitch.BlueScreen")[0][-120:],
      "синий экран вероятнее у сильно разбитой карты")

# Эффект обязан убирать за собой: сдвинутый канвас нельзя оставить сдвинутым.
check("ResetVisuals" in _art_src,
      "экран возвращается в исходное состояние")
check(re.search(r"screen\.anchoredPosition = Vector2\.zero;", _art_src) is not None
      and re.search(r"screen\.localScale = Vector3\.one;", _art_src) is not None,
      "смещение и масштаб ЭКРАНА сбрасываются, иначе картинка останется кривой")

# Эффект не должен сам стать причиной лагов: он обновляется по таймеру.
check("refreshInterval" in _art_src and "Time.unscaledTime" in _art_src,
      "артефакты перерисовываются по таймеру, а не каждый кадр")
check(re.search(r"if \(now < nextRefresh\) return;", _art_src) is not None,
      "до истечения интервала кадр пропускается")
check(re.search(r"nextRefresh = now \+ ", _art_src) is not None,
      "следующая перерисовка планируется от текущего момента")
_lu = _art_src.split("private void LateUpdate()")[1].split("\n\tprivate ")[0]
check("Redraw(" in _lu and _lu.index("if (now < nextRefresh) return;") < _lu.index("Redraw("),
      "проверка таймера стоит ДО перерисовки, а не после")
check(re.search(r"if \(strength <= 0f\)", _art_src) is not None,
      "на целой видеокарте эффект ничего не делает")

# Компонент вешается автоматически, иначе пришлось бы править 13 префабов.
_disp_src = _strip_comments(
    (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Display.cs")
    .read_text(encoding="utf-8"))
check("AddComponent<GpuArtifacts>()" in _disp_src,
      "монитор сам заводит эффект артефактов")
check("ConnectedBoard" in _disp_src,
      "эффект может узнать подключённую плату")

# Локализация состояния.
_tr = (ROOT / "Assets/Resources/Translate.txt").read_text(encoding="utf-8")
_tr_lines = _tr.replace("\r\n", "\n").split("\n")
_cols = len(_tr_lines[0].split("\t"))
_art_row = [l for l in _tr_lines if l.startswith("Artifacting\t")]
check(len(_art_row) == 1, "строка перевода Artifacting добавлена один раз")
if _art_row:
    check(len(_art_row[0].split("\t")) == _cols,
          "в строке Artifacting столько же колонок, сколько в шапке")
    check(_art_row[0].split("\t")[18] == "Артефачит",
          "русский перевод состояния на месте")

# ---------------------------------------------------------------------------
print("\nOndex Browser")

_ond_path = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/Ondex.cs"
check(_ond_path.exists(), "есть скрипт Ondex")
_ond_src = _strip_comments(_ond_path.read_text(encoding="utf-8"))
check((_ond_path.parent / "Ondex.cs.meta").exists(), "у Ondex есть .meta")

check("class Ondex : Browser" in _ond_src,
      "Ondex — отдельный браузер на базе обычного")
check("protected override void OpenSite" in _ond_src,
      "заражение происходит при переходе на сайт")

# Базовый Browser должен разрешать переопределение.
_br_src = _strip_comments(
    (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/Browser.cs")
    .read_text(encoding="utf-8"))
check("protected virtual void OpenSite" in _br_src,
      "OpenSite в базовом браузере переопределяемый")

# Шанс заражения высокий, но не стопроцентный: иначе приложением не пользуются.
_ch = re.search(r"infectionChance = ([\d.]+)f", _ond_src)
check(_ch is not None, "шанс заражения задан")
if _ch:
    _c = float(_ch.group(1))
    check(_c >= 0.3, f"шанс заметный, шутка работает ({_c})")
    check(_c < 1.0, f"шанс не стопроцентный ({_c})")

check("if (infected) return" in _ond_src,
      "уже заражённая система повторно не заражается")

# Префаб приложения.
_ond_prefab = ROOT / "Assets/Resources/apps/Ondex.prefab"
check(_ond_prefab.exists(), "есть префаб Ondex")
_op = _ond_prefab.read_text(encoding="utf-8", errors="ignore")
_ond_guid = re.search(r"guid: ([a-f0-9]+)",
                      (_ond_path.parent / "Ondex.cs.meta").read_text(encoding="utf-8")).group(1)
check(_ond_guid in _op, "префаб использует скрипт Ondex, а не Browser")
check("appName: Ondex" in _op,
      "приложение называется Ondex, а не унаследованным Browser")
check(re.search(r"virusPrefab: \{fileID: \d+, guid: [a-f0-9]+", _op) is not None,
      "вирус подключён к префабу, иначе заражать нечем")

# Приложение должно быть доступно игроку.
_dl = (ROOT / "Assets/Resources/apps/Downloader.prefab").read_text(
    encoding="utf-8", errors="ignore")
_ond_prefab_guid = re.search(
    r"guid: ([a-f0-9]+)",
    (ROOT / "Assets/Resources/apps/Ondex.prefab.meta").read_text(encoding="utf-8")
).group(1)
check(_ond_prefab_guid in _dl, "Ondex есть в App Downloader")


# ---------------------------------------------------------------------------
print("\nВертикальная синхронизация экрана монитора")

_pacer2 = _strip_comments(
    (ROOT / "Assets/Scripts/Assembly-CSharp/DisplayCameraPacer.cs")
    .read_text(encoding="utf-8"))

# Редкая перерисовка экономит кадры, но момент обновления не совпадает с кадром
# игры, и картинка на мониторе рвётся. Режим vsync рисует ровно раз в кадр.
check("vSync" in _pacer2, "у экрана есть режим вертикальной синхронизации")
check(re.search(r"private bool vSync = true;", _pacer2) is not None,
      "синхронизация включена по умолчанию")

_lu2 = _pacer2.split("private void LateUpdate()")[1]
check(re.search(r"if \(vSync\)\s*\{\s*target\.Render\(\);\s*return;", _lu2)
      is not None,
      "при включённой синхронизации кадр рисуется сразу и без таймера")

# Синхронизация обязана идти именно в LateUpdate: там вся логика кадра уже
# отработала, и на экран попадает окончательная картинка.
check("LateUpdate" in _pacer2, "рендер выполняется в конце кадра")

# Ветка с таймером должна остаться для случая, когда синхронизацию выключили.
check("redrawsPerSecond" in _lu2 and "nextRedraw" in _lu2,
      "без синхронизации по-прежнему работает ограничение частоты")


# ---------------------------------------------------------------------------
print("\nПрочность видеокарт")

_gpu_src = _strip_comments(
    (ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/GPU.cs")
    .read_text(encoding="utf-8"))

# Независимые пороги: сверяться с той же константой, что и код, бессмысленно.
_m = re.search(r"MaxArtifactLevel = (\d+)", _gpu_src)
check(_m is not None and int(_m.group(1)) >= 4,
      f"карта переживает не меньше 4 ступеней повреждения (найдено {_m.group(1)})")

_mi = re.search(r"damageImpulse = (\d+(?:\.\d+)?)f", _gpu_src)
check(_mi is not None and float(_mi.group(1)) >= 25,
      f"порог удара заметно выше прежних 12 (найдено {_mi.group(1)})")

# Падение — это серия столкновений: пол, отскок, стол. Без защиты карта
# проходила все ступени за одно падение и погибала мгновенно.
_oce = _gpu_src.split("private void OnCollisionEnter(")[1].split("\n\t\t}")[0]
check("nextDamageTime" in _oce,
      "повторные удары одного падения не считаются заново")
check(_oce.index("nextDamageTime") < _oce.index("AddArtifactLevel"),
      "защита от серии ударов стоит ДО начисления повреждения")
check("damageCooldown" in _gpu_src, "длительность защиты настраивается")

_prefabs = sorted((ROOT / "Assets/Resources/components").glob("*.prefab"))
_with_gpu = [f for f in _prefabs if "damageImpulse:" in f.read_text(encoding="utf-8")]
check(len(_with_gpu) == 14,
      f"скрипт видеокарты стоит на 14 базовых префабах (нашлось {len(_with_gpu)})")

_weak = [f.name for f in _with_gpu
         if re.search(r"damageImpulse: (\d+(?:\.\d+)?)", f.read_text(encoding="utf-8"))
         and float(re.search(r"damageImpulse: (\d+(?:\.\d+)?)",
                             f.read_text(encoding="utf-8")).group(1)) < 25]
check(not _weak, f"во всех префабах порог поднят (слабые: {_weak})")

_nocd = [f.name for f in _with_gpu
         if "damageCooldown:" not in f.read_text(encoding="utf-8")]
check(not _nocd, f"во всех префабах задана защита от серии ударов (без неё: {_nocd})")


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
