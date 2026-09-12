#!/usr/bin/env python3
"""
generate_ready_builds.py — генератор готовых ПК и готовых майнеров.

Что делает:
  1. описывает сборки списком существующих компонентов проекта;
  2. считает позу каждой детали через unity_asset_tool (слоты корпуса/платы);
  3. пишет префаб-спавнер на каждую сборку в Assets/Resources/components/ready/;
  4. пишет ShopItem-ассеты в Assets/MonoBehaviour/Ready/;
  5. добавляет страницы «Ready PC» и «Ready Miner» в Shop.asset;
  6. считает цену как сумму комплектующих + наценка.

Запуск (идемпотентно, повторный прогон даёт тот же результат):
    python3 tools/generate_ready_builds.py
    python3 tools/generate_ready_builds.py --check   # только проверка
"""

from __future__ import annotations

import argparse
import glob
import os
import re
import sys
from dataclasses import dataclass, field
from typing import Dict, List, Optional

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from unity_asset_tool import (  # noqa: E402
    SCRIPT_GUIDS,
    PartRef,
    ReadyBuild,
    ShopItemAsset,
    UnityDoc,
    make_guid,
    quat_to_euler,
    read_meta_guid,
    shop_add_page,
)

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COMP = "Assets/Resources/components/"
OUT_PREFABS = "Assets/Resources/components/ready"
OUT_ASSETS = "Assets/MonoBehaviour/Ready"
SHOP = "Assets/MonoBehaviour/Shop.asset"

# Наценка за сборку: игрок платит за то, что ПК приезжает готовым.
MARKUP = 1.10


# ---------------------------------------------------------------------------
# Прайс-лист: сопоставляем префаб компонента с его ShopItem
# ---------------------------------------------------------------------------

def load_price_table() -> Dict[str, int]:
    """Цены компонентов, ключ — имя префаба в Resources/components."""
    by_spawn_guid: Dict[str, int] = {}
    by_name: Dict[str, int] = {}

    for path in glob.glob(os.path.join(REPO, "Assets/MonoBehaviour/*.asset")):
        text = open(path, encoding="utf-8", errors="replace").read()
        if SCRIPT_GUIDS["ShopItem"] not in text:
            continue
        price_m = re.search(r"^\s*price: (\d+)", text, re.M)
        spawn_m = re.search(r"^\s*spawn: \{fileID: -?\d+, guid: (\w+)", text, re.M)
        name_m = re.search(r"^\s*m_Name: (.*)$", text, re.M)
        if not price_m or not name_m:
            continue
        price = int(price_m.group(1))
        by_name[name_m.group(1).strip()] = price
        if spawn_m:
            by_spawn_guid[spawn_m.group(1)] = price

    prices: Dict[str, int] = {}
    for prefab in glob.glob(os.path.join(REPO, COMP, "*.prefab")):
        name = os.path.splitext(os.path.basename(prefab))[0]
        guid = read_meta_guid(prefab)
        if guid in by_spawn_guid:
            prices[name] = by_spawn_guid[guid]
        elif name in by_name:
            prices[name] = by_name[name]
        else:
            # CPU/GPU/RAM продаются под коротким именем: "CPU i5-8400" -> "i5-8400".
            # Плюс у RGB-планок в ассете есть пробел: "RAM 32GB(RGB)" -> "32GB (RGB)".
            for prefix in ("CPU ", "RAM ", "PSU "):
                if not name.startswith(prefix):
                    continue
                short = name[len(prefix):]
                for candidate in (short, short.replace("(", " (")):
                    if candidate in by_name:
                        prices[name] = by_name[candidate]
                        break
                break
    # Блоки питания названы по мощности: "PSU 500W" -> "500W", "PSU 1kW" -> "1000W"
    psu_alias = {
        "PSU 300W": "300W",
        "PSU 500W": "500W",
        "PSU 1kW": "1000W",
        "PSU 1.1kW": "1100W",
        "PSU 2kW": "2000W",
        "KSAS PSU": "KCAS",
    }
    for prefab_name, asset_name in psu_alias.items():
        if asset_name in by_name:
            prices[prefab_name] = by_name[asset_name]
    # Корпуса продаются в ящиках: цена лежит на Crate_*
    for prefab in glob.glob(os.path.join(REPO, COMP, "Crate_*.prefab")):
        crate = os.path.splitext(os.path.basename(prefab))[0]
        inner = crate[len("Crate_"):]
        guid = read_meta_guid(prefab)
        if guid in by_spawn_guid and inner not in prices:
            prices[inner] = by_spawn_guid[guid]
    return prices


# ---------------------------------------------------------------------------
# Описание сборок
# ---------------------------------------------------------------------------

@dataclass
class BuildSpec:
    key: str                 # техническое имя (файлы, spawnId)
    title: str               # ключ локализации для магазина
    case: str                # префаб корпуса / рамы
    parts: List[PartRef]
    page: str                # страница магазина
    sprite_from: str         # у какого ShopItem позаимствовать иконку
    description: str = ""
    extra_markup: float = 0.0


def p(path: str, target: str, host: Optional[str] = None, index: Optional[int] = None) -> PartRef:
    return PartRef(prefab_path=COMP + path + ".prefab", slot_target=target,
                   host=host, slot_index=index)


def office_pc() -> List[PartRef]:
    """Офисный: минимум для работы. Без дискретной видеокарты — дёшево."""
    return [
        p("Micro_ATX", "Motherboard"),
        p("CPU Celeron G3920", "CPU", host="Micro_ATX"),
        p("Cooler", "Cooler", host="Micro_ATX"),
        p("RAM 4GB", "RAM", host="Micro_ATX"),
        p("RAM 4GB", "RAM", host="Micro_ATX"),
        p("GT440", "GPU", host="Micro_ATX"),
        p("PSU 300W", "Supply"),
        p("HDD 500GB", "Drive"),
        p("CaseFan", "Fan"),
    ]


def home_pc() -> List[PartRef]:
    """Домашний: универсальный середняк для учёбы, кино и нетяжёлых игр."""
    return [
        p("ATX", "Motherboard"),
        p("CPU i5-8400", "CPU", host="ATX"),
        p("TowerCooler", "Cooler", host="ATX"),
        p("RAM 8GB", "RAM", host="ATX"),
        p("RAM 8GB", "RAM", host="ATX"),
        p("GTX1060", "GPU", host="ATX"),
        p("PSU 500W", "Supply"),
        p("SSD 512GB", "Drive"),
        p("HDD 1TB", "Drive"),
        p("CaseFan", "Fan"),
    ]


def gaming_pc() -> List[PartRef]:
    """Игровой: топовое железо, RGB и стеклянная крышка."""
    return [
        p("ATX", "Motherboard"),
        p("CPU i9-12900K", "CPU", host="ATX"),
        p("WaterCooler", "Cooler", host="ATX"),
        p("RAM 32GB(RGB)", "RAM", host="ATX"),
        p("RAM 32GB(RGB)", "RAM", host="ATX"),
        p("RTX4080", "GPU", host="ATX"),
        p("PSU 1kW", "Supply"),
        p("SSD 2TB", "Drive"),
        p("SSD 1TB", "Drive"),
        p("CaseFan(RGB)", "Fan"),
    ]


def miner_cheap() -> List[PartRef]:
    """Дешёвый майнер: 4 бюджетные карты в стандартной раме."""
    parts = [
        p("Mini_ITX", "Motherboard"),
        p("CPU Celeron G3920", "CPU", host="Mini_ITX"),
        p("Cooler", "Cooler", host="Mini_ITX"),
        p("RAM 4GB", "RAM", host="Mini_ITX"),
        p("PSU 500W", "Supply"),
        p("SSD 128GB", "Drive"),
    ]
    parts += [p("GT1030", "GPU") for _ in range(4)]
    return parts


def miner_medium() -> List[PartRef]:
    """Средний майнер: 8 карт среднего уровня, рама заполнена целиком."""
    parts = [
        p("Mini_ITX", "Motherboard"),
        p("CPU i3-8300", "CPU", host="Mini_ITX"),
        p("TowerCooler", "Cooler", host="Mini_ITX"),
        p("RAM 8GB", "RAM", host="Mini_ITX"),
        p("PSU 1kW", "Supply"),
        p("PSU 1kW", "Supply"),
        p("SSD 256GB", "Drive"),
    ]
    parts += [p("RX570", "GPU") for _ in range(8)]
    return parts


def miner_ultra() -> List[PartRef]:
    """Ультра-майнер: большая рама, все 24 слота GPU забиты RTX 3080.

    BigMiner — это инстанс обычного Miner с надстроенными стойками, поэтому
    слотов GPU у него 24: 16 своих плюс 8 унаследованных.
    """
    parts = [
        p("Mini_ITX", "Motherboard"),
        p("CPU i7-8700K", "CPU", host="Mini_ITX"),
        p("TowerCooler", "Cooler", host="Mini_ITX"),
        p("RAM 16GB", "RAM", host="Mini_ITX"),
        p("RAM 16GB", "RAM", host="Mini_ITX"),
    ]
    parts += [p("PSU 2kW", "Supply") for _ in range(6)]
    parts += [p("SSD 512GB", "Drive") for _ in range(2)]
    parts += [p("RTX3080", "GPU") for _ in range(24)]
    return parts


BUILDS: List[BuildSpec] = [
    BuildSpec(
        key="ReadyPC_Office",
        title="{Office PC}",
        case=COMP + "Case_ITX(Black).prefab",
        parts=office_pc(),
        page="Ready PC",
        sprite_from="Case_ITX(Black)",
        description="Office PC Description",
    ),
    BuildSpec(
        key="ReadyPC_Home",
        title="{Home PC}",
        case=COMP + "Case_ATX(Black).prefab",
        parts=home_pc(),
        page="Ready PC",
        sprite_from="Case_ATX(Black)",
        description="Home PC Description",
    ),
    BuildSpec(
        key="ReadyPC_Gaming",
        title="{Gaming PC}",
        case=COMP + "Case_ATX 2(Black).prefab",
        parts=gaming_pc(),
        page="Ready PC",
        sprite_from="Case_ATX 2(Black)",
        description="Gaming PC Description",
    ),
    BuildSpec(
        key="ReadyMiner_Cheap",
        title="{Cheap Miner}",
        case=COMP + "Miner.prefab",
        parts=miner_cheap(),
        page="Ready Miner",
        sprite_from="Miner",
        description="Cheap Miner Description",
    ),
    BuildSpec(
        key="ReadyMiner_Medium",
        title="{Medium Miner}",
        case=COMP + "Miner.prefab",
        parts=miner_medium(),
        page="Ready Miner",
        sprite_from="Miner",
        description="Medium Miner Description",
    ),
    BuildSpec(
        key="ReadyMiner_Ultra",
        title="{Ultra Miner}",
        case=COMP + "BigMiner.prefab",
        parts=miner_ultra(),
        page="Ready Miner",
        sprite_from="Big Miner",
        description="Ultra Miner Description",
    ),
]


# ---------------------------------------------------------------------------
# Вспомогательное
# ---------------------------------------------------------------------------

def sprite_of(asset_name: str) -> str:
    path = os.path.join(REPO, "Assets/MonoBehaviour", asset_name + ".asset")
    text = open(path, encoding="utf-8", errors="replace").read()
    return re.search(r"sprite: \{fileID: \d+, guid: (\w+)", text).group(1)


def base_ref(prefab_rel: str):
    """(guid, fileID корневого GameObject) базового префаба."""
    full = os.path.join(REPO, prefab_rel)
    doc = UnityDoc.load(full)
    root = doc.root_game_object()
    return read_meta_guid(full), root.file_id


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="только проверить, не писать")
    args = ap.parse_args()

    prices = load_price_table()
    missing_price: List[str] = []

    from unity_asset_tool import write_spawner_prefab

    os.makedirs(os.path.join(REPO, OUT_PREFABS), exist_ok=True)
    os.makedirs(os.path.join(REPO, OUT_ASSETS), exist_ok=True)

    pages: Dict[str, List[str]] = {}

    for spec in BUILDS:
        build = ReadyBuild(name=spec.key, case_prefab=spec.case, parts=spec.parts)
        resolved = build.resolve(REPO)  # тут же валидация слотов

        case_name = os.path.splitext(os.path.basename(spec.case))[0]
        total = prices.get(case_name, 0)
        if case_name not in prices:
            missing_price.append(case_name)
        for r in resolved:
            if r["name"] in prices:
                total += prices[r["name"]]
            else:
                missing_price.append(r["name"])
        price = int(round(total * (MARKUP + spec.extra_markup) / 5.0) * 5)

        print(f"{spec.key:20s} деталей={len(resolved):2d}  "
              f"комплектующие={total:>6}$  цена={price}$")
        for r in resolved:
            e = quat_to_euler(r["rot"])
            pos = tuple(round(v, 3) for v in r["pos"])
            print(f"    {r['name']:22s} -> {r['host']:18s} {r['target']:12s} "
                  f"pos={pos} euler={e}")

        if args.check:
            continue

        base_guid, base_fid = base_ref(spec.case)
        prefab_path = os.path.join(REPO, OUT_PREFABS, spec.key + ".prefab")
        prefab_guid, go_id = write_spawner_prefab(
            path=prefab_path,
            build_name=spec.key,
            base_prefab_guid=base_guid,
            base_prefab_file_id=base_fid,
            parts=resolved,
        )

        asset_path = os.path.join(REPO, OUT_ASSETS, spec.key + ".asset")
        item = ShopItemAsset(
            name=spec.key,
            item_name=spec.title,
            price=price,
            spawn_guid=prefab_guid,
            spawn_file_id=go_id,
            sprite_guid=sprite_of(spec.sprite_from),
            bitcoin=0,
            large=1,  # готовый ПК приезжает порталом, а не в коробке
            description=spec.description,
        )
        asset_guid = item.write(asset_path, guid=make_guid("asset:" + spec.key))
        pages.setdefault(spec.page, []).append(asset_guid)

    if missing_price:
        print("\nНЕТ ЦЕНЫ (считаем как 0): " + ", ".join(sorted(set(missing_price))))

    if not args.check:
        for page, guids in pages.items():
            changed = shop_add_page(os.path.join(REPO, SHOP), page, guids)
            print(f"Shop.asset: страница '{page}' — "
                  f"{'обновлена' if changed else 'без изменений'} ({len(guids)} шт.)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
