#!/usr/bin/env python3
"""Починить FBX: убрать Camera/Light и запечь масштабы в вершины.

Зачем. Модель дакимакуры пришла из Sketchfab с грязным FBX: в файле есть
Camera и Light (они завозятся в Unity как полноценные объекты в иерархии
модели), а у узла меша масштаб 0.01 — незапечённый трансформ, классическая
история из docs/FBX_IMPORT_RU.md. В игре это и есть «моделька с камерой и
фонариком внутри, и размеры не 1x1x1».

Скрипт читает FBX напрямую (без Unity): убирает узлы Camera/Light вместе с
потомками, запёкивает неединичные масштабы в вершины меша (позиции — 'f'
или 'd' массивы; normals не трогаются — они и так единичные) и переwrites
файл целиком, пересчитав все end-поля узлов. После него Unity импортирует
чистую модель: все Lcl Scaling = 1, никаких камер/светов.

Использование:
    python3 tools/fix_fbx_transforms.py <файл.fbx> [--dry-run]
    python3 tools/fix_fbx_transforms.py --all [--dry-run]

После фикса проверьте:
    python3 tools/check_fbx_transforms.py Assets/Models/dakimakura.fbx
"""

import argparse
import glob
import os
import struct
import sys
import zlib

# Допуск на числа с плавающей точкой: экспортёры пишут 0.9999999 вместо 1.
EPS = 1e-4


class Node:
    __slots__ = ("kind", "name", "start", "end", "props", "children", "parent")

    def __init__(self, kind, name, start, end):
        self.kind = kind
        self.name = name
        self.start = start
        self.end = end
        self.props = []
        self.children = []
        self.parent = None


def parse_fbx(data):
    """Разобрать бинарный FBX в дерево узлов.

    props: список (kind, value, raw, offset) — raw для S/R и массивов,
    offset — позиция первого байта свойства в файле.
    """
    if not data.startswith(b"Kaydara FBX Binary"):
        raise ValueError("не бинарный FBX (текстовый формат не поддержан)")
    version = struct.unpack("<I", data[23:27])[0]
    wide = version >= 7500

    def read_node(pos):
        header_start = pos
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
        node = Node(name, name, header_start, end)
        props = []
        for _ in range(nprops):
            prop_start = pos
            kind = chr(data[pos])
            pos += 1
            if kind in "SR":
                length = struct.unpack("<I", data[pos:pos + 4])[0]
                pos += 4
                raw = data[pos:pos + length]
                pos += length
                props.append((kind, raw, raw, prop_start))
            elif kind in "YCIFDL":
                size = {"Y": 2, "C": 1, "I": 4, "F": 4, "D": 8, "L": 8}[kind]
                raw = data[pos:pos + size]
                pos += size
                if kind == "L":
                    value = struct.unpack("<q", raw)[0]
                elif kind == "I":
                    value = struct.unpack("<i", raw)[0]
                elif kind == "D":
                    value = struct.unpack("<d", raw)[0]
                elif kind == "F":
                    value = struct.unpack("<f", raw)[0]
                else:
                    value = raw
                props.append((kind, value, raw, prop_start))
            elif kind in "fdlbic":
                arr_len, enc, comp_len = struct.unpack("<III", data[pos:pos + 12])
                pos += 12
                raw = data[pos:pos + comp_len]
                pos += comp_len
                props.append((kind, (arr_len, enc, comp_len), raw, prop_start))
            else:
                props.append((kind, None, None, prop_start))
        node.props = props
        cursor = pos
        while cursor < end:
            child, cursor = read_node(cursor)
            if child is None:
                break
            child.parent = node
            node.children.append(child)
        return node, end

    tops = []
    tail = b""
    pos = 27
    last_end = 27
    while pos < len(data) - 100:
        node, pos = read_node(pos)
        if node is None:
            break
        last_end = node.end
        tops.append(node)
    tail = data[last_end:]
    return tops, tail


def find_props70(node):
    for child in node.children:
        if child.kind == "Properties70":
            return child
    return None


def prop_entry(node, key):
    """Вернуть P-запись Properties70 по ключу (S == key)."""
    p70 = find_props70(node)
    if p70 is None:
        return None
    for prop in p70.children:
        for kind, value, raw, _off in prop.props:
            if kind == "S" and value:
                k = value.decode("ascii", "replace").split("\x00")[0]
                if k == key:
                    return prop
    return None


def lcl_scaling(node):
    entry = prop_entry(node, "Lcl Scaling")
    if entry is None:
        return (1.0, 1.0, 1.0)
    vals = [v for k, v, _r, _o in entry.props if k == "D"]
    if len(vals) >= 3:
        return (float(vals[0]), float(vals[1]), float(vals[2]))
    return (1.0, 1.0, 1.0)


def node_class(node):
    """Класс узла по второму S-пропу (Camera/Light/Mesh/...)."""
    ss = [v for k, v, _r, _o in node.props if k == "S"]
    if len(ss) >= 2 and ss[1]:
        return ss[1].decode("ascii", "replace").split("\x00")[0]
    return ""


def obj_name(node):
    ss = [v for k, v, _r, _o in node.props if k == "S"]
    if ss and ss[0]:
        return ss[0].decode("ascii", "replace").split("\x00")[0]
    return node.name


def node_id(node):
    for kind, value, _raw, _off in node.props:
        if kind == "L":
            return value
    return None


def serialize_prop(prop):
    """Преобразовать свойство обратно в байты."""
    kind, value, raw, _off = prop
    out = bytearray(bytes([ord(kind)]))
    if kind in "SR":
        out += struct.pack("<I", len(raw))
        out += raw
    elif kind in "YCIFDL":
        out += raw
    elif kind in "fdlbic":
        arr_len, enc, comp_len = value
        out += struct.pack("<III", arr_len, enc, comp_len)
        out += raw
    return out


def serialize_node(node, out, start=None):
    """Отserialize узел со свежим end-полем в header.

    end — это абсолютный смещение END узла (start + размер узла), а не
    длина узла — так лежит в FBX. start — абсолютная позиция узла в файле
    (пробрасывается в детей, т.к. они serialize в тело родителя).
    """
    if start is None:
        start = len(out)
    props_bytes = bytearray()
    for prop in node.props:
        props_bytes += serialize_prop(prop)
    body = bytearray(props_bytes)
    # тело (props + children) идёт после header(12) + name_len(1) + name(N)
    cursor = start + 12 + 1 + len(node.name.encode("ascii")) + len(props_bytes)
    for child in node.children:
        before = len(body)
        serialize_node(child, body, cursor)
        cursor += len(body) - before
    name_bytes = node.name.encode("ascii")
    node_size = 12 + 1 + len(name_bytes) + len(body)
    header = struct.pack("<III", start + node_size, len(node.props),
                         len(props_bytes))
    out += header + bytes([len(name_bytes)]) + name_bytes + body


def scale_vertices(geo, scale, report):
    """Запечь scale в массив позиций Vertices-узла меша."""
    for child in geo.children:
        if child.name != "Vertices":
            continue
        for i, (kind, value, raw, off) in enumerate(child.props):
            if kind not in ("f", "d"):
                continue
            arr_len, enc, comp_len = value
            elem = 4 if kind == "f" else 8
            src = raw
            if comp_len > 0:
                try:
                    src = zlib.decompress(raw)
                except Exception:
                    report.append("  пропущены вершины: не удалось разжать массив")
                    return False
            if len(src) % (elem * 3) != 0:
                report.append("  пропущены вершины: длина массива не кратна 3")
                return False
            scaled = bytearray(src)
            fmt = "<" + kind * 3  # вершина = 3 координаты (fff или ddd)
            for p in range(0, len(scaled), elem * 3):
                x, y, z = struct.unpack_from(fmt, scaled, p)
                struct.pack_into(fmt, scaled, p, x * scale[0], y * scale[1],
                                 z * scale[2])
            out = bytes(scaled)
            if comp_len > 0:
                out = zlib.compress(out, 6)
            child.props[i] = (kind, (arr_len, enc, len(out)), out, off)
            return True
    return False


def fix_fbx(path, dry_run=False, verbose=False):
    data = open(path, "rb").read()
    tops, tail = parse_fbx(data)

    objects = next(t for t in tops if t.kind == "Objects")
    connections = next((t for t in tops if t.kind == "Connections"), None)

    id_map = {}
    for obj in objects.children:
        nid = node_id(obj)
        if nid is not None:
            id_map[nid] = obj

    # OO-связи: (child, parent) — так лежит в Sketchfab-FBX.
    parent_of = {}
    children_of = {}
    conn_entries = []
    if connections is not None:
        for c in connections.children:
            if c.kind != "C":
                continue
            ids = [v for k, v, _r, _o in c.props if k == "L"]
            conn_entries.append((c, ids))
            if len(ids) >= 2:
                parent_of[ids[0]] = ids[1]
                children_of.setdefault(ids[1], []).append(ids[0])

    report = []
    changed = False

    # 1) Camera/Light: Model-узлы с классом Camera/Light (или именем).
    camera_ids = set()
    for obj in objects.children:
        if obj.kind != "Model":
            continue
        cls = node_class(obj)
        name = obj_name(obj)
        if cls in ("Camera", "Light") or name in ("Camera", "Light"):
            nid = node_id(obj)
            if nid is not None:
                camera_ids.add(nid)
                report.append(f"  удаляем {cls or name}(id={nid})")

    deleted_ids = set()
    stack = list(camera_ids)
    while stack:
        nid = stack.pop()
        if nid in deleted_ids:
            continue
        deleted_ids.add(nid)
        for child in children_of.get(nid, []):
            if child not in deleted_ids:
                stack.append(child)

    # 2) Запекать масштабы в вершины (только от корней — родитель 0).
    baked = set()
    changed = False

    def bake(node, inherited):
        nonlocal changed
        nid = node_id(node)
        if nid is None or nid in baked or nid in deleted_ids:
            return
        baked.add(nid)
        own = lcl_scaling(node)
        total = tuple(a * b for a, b in zip(inherited, own))

        if any(abs(s - 1.0) > EPS for s in own):
            report.append(f"  запекаем {obj_name(node)}: scale {own} -> 1")
            changed = True

        # геометрия прямо под узлом (через связи или вложенность)
        geo_children = [id_map[c] for c in children_of.get(nid, [])
                        if c in id_map and id_map[c].kind == "Geometry"]
        geo_children += [c for c in node.children if c.kind == "Geometry"]
        for geo in geo_children:
            if any(abs(s - 1.0) > EPS for s in total):
                scale_vertices(geo, total, report)

        for child in children_of.get(nid, []):
            child_node = id_map.get(child)
            if child_node is not None and child_node.kind == "Model":
                bake(child_node, total)

        if any(abs(s - 1.0) > EPS for s in own):
            entry = prop_entry(node, "Lcl Scaling")
            if entry is not None:
                di = 0
                for j, (kind, value, raw, off) in enumerate(entry.props):
                    if kind == "D" and di < 3:
                        entry.props[j] = (kind, 1.0, struct.pack("<d", 1.0), off)
                        di += 1

    for obj in objects.children:
        if obj.kind != "Model":
            continue
        nid = node_id(obj)
        if nid is None or parent_of.get(nid) != 0:
            continue
        bake(obj, (1.0, 1.0, 1.0))

    changed = bool(deleted_ids)
    for obj in objects.children:
        nid = node_id(obj)
        if nid in deleted_ids:
            report.append(f"  удаляем узел id={nid} ({obj_name(obj)})")
            changed = True
    for c, ids in conn_entries:
        if any(i in deleted_ids for i in ids):
            report.append(f"  удаляем связь ids={ids}")
            changed = True

    if not changed:
        print(f"[fix] {path}")
        print("  всё уже чисто — Nothing to fix")
        return 0

    if dry_run:
        print(f"[dry-run] {path}")
        print("\n".join(report))
        return 0

    # 3) Переserialize: header + верхние узлы (Objects/Connections без
    # удалённых детей) + tail.
    out = bytearray(data[:27])
    for top in tops:
        if top.kind == "Objects":
            keep = [c for c in top.children if node_id(c) not in deleted_ids]
            node = Node(top.kind, top.name, 0, 0)
            node.props = top.props
            node.children = keep
            serialize_node(node, out)
        elif top.kind == "Connections":
            keep = [c for c, ids in conn_entries
                    if not any(i in deleted_ids for i in ids)]
            # Connections может содержать не только C-записи — сохранить все
            keep_all = []
            for c in top.children:
                ids = [v for k, v, _r, _o in c.props if k == "L"]
                if c.kind == "C" and any(i in deleted_ids for i in ids):
                    continue
                keep_all.append(c)
            node = Node(top.kind, top.name, 0, 0)
            node.props = top.props
            node.children = keep_all
            serialize_node(node, out)
        else:
            serialize_node(top, out)
    out += tail
    with open(path, "wb") as fh:
        fh.write(out)
    print(f"[fix] {path}")
    print("\n".join(report))
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description="Убрать Camera/Light и запечь масштабы в FBX")
    ap.add_argument("files", nargs="*", help="FBX файлы")
    ap.add_argument("--all", action="store_true", help="все FBX в Assets")
    ap.add_argument("--dry-run", action="store_true", help="только отчёт")
    args = ap.parse_args(argv)

    if args.all:
        targets = sorted(glob.glob("Assets/**/*.fbx", recursive=True))
    else:
        targets = args.files
    if not targets:
        ap.error("укажите файлы или --all")

    problems = 0
    for path in targets:
        if not os.path.exists(path):
            print(f"НЕТ ФАЙЛА  {path}")
            problems += 1
            continue
        try:
            if fix_fbx(path, dry_run=args.dry_run) != 0:
                problems += 1
        except Exception as exc:  # noqa: BLE001
            print(f"ПРОПУСК    {path}: {exc}")
            problems += 1
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
