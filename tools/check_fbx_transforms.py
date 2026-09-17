#!/usr/bin/env python3
"""Проверка FBX на незапечённые трансформы.

Зачем это нужно. Слот в игре -- точка привязки: скрипт ставит в неё деталь и
берёт Pivot как позицию. Если у слота в модели остался масштаб 0.01 (обычное
дело после Blender без Apply All Transforms), то деталь наследует масштаб и
становится в сто раз мельче, Pivot уезжает, а коллайдер Trigger сжимается.
Плата при этом выглядит нормально -- у её меша масштаб единичный, -- поэтому
баг кажется загадочным: модель на месте, а слоты не работают.

Скрипт читает FBX напрямую, без Unity, и показывает объекты с масштабом,
отличным от единицы.

Использование:
    python3 tools/check_fbx_transforms.py <файл.fbx> [ещё.fbx ...]
    python3 tools/check_fbx_transforms.py --all
"""

import glob
import struct
import sys
import os

# Допуск на числа с плавающей точкой: экспортёры пишут 0.9999999 вместо 1.
EPS = 1e-4


def _parse(path):
    """Разобрать бинарный FBX в дерево узлов (имя, свойства, дети)."""
    with open(path, "rb") as fh:
        data = fh.read()

    if not data.startswith(b"Kaydara FBX Binary"):
        raise ValueError("не бинарный FBX (текстовый формат не поддержан)")

    version = struct.unpack("<I", data[23:27])[0]
    wide = version >= 7500  # с 7.5 длины полей 64-битные

    def read_node(pos):
        if wide:
            end, nprops, _plen = struct.unpack("<QQQ", data[pos:pos + 24])
            pos += 24
        else:
            end, nprops, _plen = struct.unpack("<III", data[pos:pos + 12])
            pos += 12

        name_len = data[pos]
        pos += 1
        if end == 0:
            return None, pos

        name = data[pos:pos + name_len].decode("ascii", "replace")
        pos += name_len

        props = []
        p = pos
        for _ in range(nprops):
            kind = chr(data[p])
            p += 1
            if kind in "SR":
                length = struct.unpack("<I", data[p:p + 4])[0]
                p += 4
                props.append(("S", data[p:p + length]))
                p += length
            elif kind in "YCIFDL":
                size = {"Y": 2, "C": 1, "I": 4, "F": 4, "D": 8, "L": 8}[kind]
                raw = data[p:p + size]
                p += size
                value = None
                if kind == "L":
                    value = struct.unpack("<q", raw)[0]
                elif kind == "I":
                    value = struct.unpack("<i", raw)[0]
                elif kind == "D":
                    value = struct.unpack("<d", raw)[0]
                elif kind == "F":
                    value = struct.unpack("<f", raw)[0]
                props.append((kind, value))
            elif kind in "fdlbic":
                _arr_len, _enc, comp_len = struct.unpack("<III", data[p:p + 12])
                p += 12 + comp_len
                props.append(("arr", None))
            else:
                props.append(("?", None))

        children = []
        cursor = p
        while cursor < end:
            child, cursor = read_node(cursor)
            if child is None:
                break
            children.append(child)

        return (name, props, children), end

    nodes = []
    pos = 27
    while pos < len(data) - 100:
        node, pos = read_node(pos)
        if node is None:
            break
        nodes.append(node)
    return nodes


def _properties70(node):
    """Достать словарь свойств Properties70 узла."""
    out = {}
    for child in node[2]:
        if child[0] != "Properties70":
            continue
        for prop in child[2]:
            if not prop[1] or prop[1][0][0] != "S":
                continue
            key = prop[1][0][1].decode("ascii", "replace").split("\x00")[0]
            out[key] = [v for t, v in prop[1] if t == "D"]
    return out


def scan(path):
    """Вернуть (плохие_масштабы, всего_моделей).

    ВАЖНО: ругаться на любой масштаб != 1 нельзя, иначе получаются ложные
    тревоги. В RX570.fbx у ВСЕХ объектов масштаб 100 -- это единицы измерения
    сцены, Unity их переводит сама (Convert Units), и карта работает в игре.

    Беда -- когда масштабы РАЗНЫЕ внутри одного файла: часть объектов
    единичная, а часть нет. Именно так выглядит забытый Apply All Transforms,
    и именно это ломает слоты, потому что вложенная деталь наследует чужой
    масштаб.
    """
    nodes = _parse(path)
    bad = []
    total = 0

    for top in nodes:
        if top[0] != "Objects":
            continue
        for obj in top[2]:
            if obj[0] != "Model":
                continue

            name = "?"
            for kind, value in obj[1]:
                if kind == "S":
                    name = value.decode("ascii", "replace").split("\x00")[0]
                    break

            total += 1
            scale = _properties70(obj).get("Lcl Scaling")
            if not scale:
                scale = [1.0, 1.0, 1.0]
            bad.append((name, [round(v, 5) for v in scale]))

    # Оставляем только случай смешанных масштабов.
    # Сравнивать масштабы точным равенством нельзя: экспортёр пишет
    # 100.00001 вместо 100, и объект ложно попадает в "выбивающиеся".
    # Округляем до относительной точности, а затем берём самый частый
    # масштаб файла за норму.
    def bucket(scale):
        return tuple(round(v, 3) for v in scale)

    counts = {}
    for _n, s in bad:
        counts[bucket(s)] = counts.get(bucket(s), 0) + 1

    if len(counts) <= 1:
        return [], total

    norm = max(counts, key=counts.get)

    # В файле из двух объектов "самый частый масштаб" не значит ничего:
    # в Untitled.fbx норма выпадала на служебный Button, и правильный
    # Case_ATX_Black_ объявлялся ошибкой. Нужна внятная опора: либо норму
    # подтверждает несколько объектов, либо это единичный масштаб.
    unit = (1.0, 1.0, 1.0)
    if counts[norm] < 2 and norm != unit:
        if unit in counts:
            norm = unit
        else:
            return [], total

    # Неравномерный масштаб (x != y != z) подозрителен сам по себе: по нему
    # нельзя ориентироваться как на норму файла.
    if len(set(norm)) != 1 and unit in counts:
        norm = unit

    def close(scale):
        return all(
            abs(a - b) <= max(1e-3, abs(b) * 1e-4)
            for a, b in zip(scale, norm))

    odd = [(n, s) for n, s in bad if not close(s)]
    return odd, total


def main(argv):
    if not argv or argv[0] == "--all":
        targets = sorted(glob.glob("Assets/**/*.fbx", recursive=True))
    else:
        targets = argv

    problems = 0
    for path in targets:
        if not os.path.exists(path):
            print(f"НЕТ ФАЙЛА  {path}")
            problems += 1
            continue

        try:
            bad, total = scan(path)
        except Exception as exc:  # noqa: BLE001
            print(f"ПРОПУСК    {path}: {exc}")
            continue

        if not bad:
            print(f"ok   {os.path.basename(path)}: моделей {total}, "
                  "масштабы единичные")
            continue

        problems += 1
        print(f"ПЛОХО {os.path.basename(path)}: "
              f"{len(bad)} из {total} объектов выбиваются по масштабу")
        for name, scale in bad[:20]:
            print(f"        {name:16} scale={scale}")
        if len(bad) > 20:
            print(f"        ... и ещё {len(bad) - 20}")
        print("        Лечится в Blender: Object -> Apply -> All Transforms, "
              "затем экспорт с Apply Scalings: FBX All.")
        print("        Подробно: docs/FBX_IMPORT_RU.md")

    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
