#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ORANGE FORGE  —  тулза для правки Unity-сцен и префабов из консоли.

Зачем: чтобы править .unity/.prefab (хотбар, панели биндов, префабы приложений)
не открывая Unity. Работает напрямую с YAML сцены, сохраняя формат 1-в-1.

Быстрый старт
-------------
  python3 tools/orange_forge.py tree   Assets/Scenes/Main.unity --name Functions
  python3 tools/orange_forge.py find   Assets/Scenes/Main.unity --name VisualWiring
  python3 tools/orange_forge.py show   Assets/Scenes/Main.unity --fileid 595
  python3 tools/orange_forge.py clone  Assets/Scenes/Main.unity --node 451 \
              --name Grid --sprite-field 596="{fileID: 21300000, guid: XXX, type: 3}"
  python3 tools/orange_forge.py set    Assets/Scenes/Menu.unity --fileid 1079218468 \
              --field action --value 16
  python3 tools/orange_forge.py call   Assets/Scenes/Main.unity --fileid 595 \
              --index 0 --method ToggleGrid
  python3 tools/orange_forge.py child  Assets/Scenes/Main.unity --parent 505 --child 900001

Файл перезаписывается на месте, рядом кладётся .bak (первый раз).
"""

from __future__ import annotations

import argparse
import os
import random
import re
import sys

BANNER = "\033[38;5;208m"   # оранжевый, как бренд Orange PC
RESET = "\033[0m"
DIM = "\033[2m"
OK = "\033[38;5;78m"
WARN = "\033[38;5;214m"
ERR = "\033[38;5;196m"

DOC_RE = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+)( stripped)?$")
FID_RE = re.compile(r"\{fileID: (-?\d+)")
ANY_FID_RE = re.compile(r"(-?\d+)")

# Классы Unity, которые тулза понимает по именам
CLASS_NAMES = {
    1: "GameObject", 4: "Transform", 21: "Material", 33: "MeshFilter",
    23: "MeshRenderer", 43: "Mesh", 64: "MeshCollider", 65: "BoxCollider",
    114: "MonoBehaviour", 222: "CanvasRenderer", 223: "Canvas",
    224: "RectTransform", 225: "CanvasGroup", 1001: "PrefabInstance",
}


def log(msg="", color=None):
    if color:
        print(color + msg + RESET)
    else:
        print(msg)


# ────────────────────────────────────────────────────────────────────────────
# Загрузка / сохранение
# ────────────────────────────────────────────────────────────────────────────
class Scene:
    """YAML-документ Unity: список (classid, fileid, raw_text)."""

    def __init__(self, path):
        self.path = path
        raw = open(path, encoding="utf-8", errors="replace").read()
        parts = DOC_RE.split(raw)
        self.header = parts[0]
        self.docs = []          # [(classid:int, fileid:int, text:str), ...]
        for i in range(1, len(parts), 4):
            self.docs.append([int(parts[i]), int(parts[i + 1]), parts[i + 3]])
        self.reindex()

    def reindex(self):
        self.by_id = {}
        for idx, (cid, fid, _t) in enumerate(self.docs):
            self.by_id.setdefault(fid, idx)
        # GameObject -> Transform и наоборот
        self.go_of = {}
        self.tr_of = {}
        self.children = {}
        self.parent = {}
        self.names = {}
        self.comps = {}
        for cid, fid, text in self.docs:
            if cid == 1:
                m = re.search(r"(?m)^\s*m_Name: (.*)$", text)
                self.names[fid] = m.group(1).strip() if m else ""
                self.comps[fid] = [int(x) for x in
                                   re.findall(r"(?m)^\s*- component: \{fileID: (-?\d+)\}$", text)]
            elif cid in (4, 224):
                g = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", text)
                f = re.search(r"m_Father: \{fileID: (-?\d+)\}", text)
                ch = [int(x) for x in re.findall(r"(?m)^\s*- \{fileID: (-?\d+)\}$", text)]
                if g:
                    self.tr_of[int(g.group(1))] = fid
                    self.go_of[fid] = int(g.group(1))
                self.parent[fid] = int(f.group(1)) if f else 0
                self.children[fid] = ch

    # ── доступ ──────────────────────────────────────────────────────────
    def index(self, fid):
        if fid not in self.by_id:
            raise KeyError("нет объекта с fileID %s" % fid)
        return self.by_id[fid]

    def get(self, fid):
        return self.docs[self.index(fid)]

    def text(self, fid):
        return self.get(fid)[2]

    def set_text(self, fid, text):
        self.docs[self.index(fid)][2] = text

    def path_of(self, tr):
        out = []
        cur = tr
        seen = set()
        while cur in self.parent and self.parent[cur] and cur not in seen:
            seen.add(cur)
            cur = self.parent[cur]
            out.append(self.names.get(self.go_of.get(cur, -1), "?"))
        return "/".join(reversed(out))

    def new_fid(self):
        while True:
            fid = random.randint(100000000, 1999999999)
            if fid not in self.by_id:
                return fid

    # ── поиск ───────────────────────────────────────────────────────────
    def find(self, name=None, path_contains=None, cls=None):
        res = []
        for cid, fid, text in self.docs:
            if cls is not None and cid != cls:
                continue
            if name is not None:
                if cid == 1 and self.names.get(fid) != name:
                    continue
                if cid != 1:
                    continue
            if path_contains is not None:
                tr = self.tr_of.get(fid)
                if tr is None or path_contains not in self.path_of(tr):
                    continue
            res.append(fid)
        return res

    # ── редактирование ──────────────────────────────────────────────────
    def set_field(self, fid, field, value, occurrence=0):
        """Ставит scalar-поле вида `  field: value` в документе fid."""
        cid, _f, text = self.get(fid)
        pat = re.compile(r"(?m)^(\s*)" + re.escape(field) + r":.*$")
        hits = list(pat.finditer(text))
        if not hits:
            raise KeyError("поле %s не найдено в %s" % (field, fid))
        m = hits[occurrence]
        new = text[:m.start()] + m.group(1) + field + ": " + str(value) + text[m.end():]
        self.set_text(fid, new)

    def insert_field(self, fid, after, name, value):
        """Вставляет новое поле сразу после поля `after` (для свежих сериализованных полей)."""
        text = self.text(fid)
        m = re.search(r"(?m)^(\s*)" + re.escape(after) + r":.*$", text)
        if not m:
            raise KeyError("поле %s (якорь) не найдено в %s" % (after, fid))
        line = "\n%s%s: %s" % (m.group(1), name, str(value))
        self.set_text(fid, text[:m.end()] + line + text[m.end():])

    def get_field(self, fid, field):
        m = re.search(r"(?m)^\s*" + re.escape(field) + r": (.*)$", self.text(fid))
        return m.group(1).strip() if m else None

    def set_call(self, fid, index, method=None, target=None, type_name=None):
        """Правит вызов в m_OnClick (UnityEvent)."""
        text = self.text(fid)
        block = re.search(r"(?ms)(      m_Calls:\n)((?:      - .*\n(?:        .*\n)*)*)", text)
        if not block:
            raise KeyError("m_OnClick.m_Calls не найден в %s" % fid)
        calls = re.findall(r"(?ms)      - m_Target:.*?(?=\n      - m_Target:|\Z)", text)
        if index >= len(calls):
            raise IndexError("вызов №%s не найден (всего %s)" % (index, len(calls)))
        call = calls[index]
        new = call
        if method is not None:
            new = re.sub(r"(?m)^(\s*)m_MethodName:.*$", r"\1m_MethodName: " + method, new)
        if target is not None:
            new = re.sub(r"(?m)^(\s*)m_Target: \{fileID: -?\d+\}",
                         r"\1m_Target: {fileID: " + str(target) + "}", new)
        if type_name is not None:
            new = re.sub(r"(?m)^(\s*)m_TargetAssemblyTypeName:.*$",
                         r"\1m_TargetAssemblyTypeName: " + type_name, new)
        self.set_text(fid, text.replace(call, new, 1))

    def add_child(self, parent_tr, child_tr, at=None):
        cid, fid, text = self.get(parent_tr)
        m = re.search(r"(?ms)^(  m_Children:)(.*?)(?=^  m_Father:)", text)
        if not m:
            raise KeyError("m_Children не найден в %s" % parent_tr)
        block = m.group(2)
        if block.strip() == "[]":
            new = "\n  - {fileID: %d}\n" % child_tr
        else:
            lines = [l for l in block.split("\n") if l.strip()]
            entry = "  - {fileID: %d}" % child_tr
            if at is None or at >= len(lines):
                lines.append(entry)
            else:
                lines.insert(at, entry)
            new = "\n" + "\n".join(lines) + "\n"
        self.set_text(parent_tr, text[:m.start(2)] + new + text[m.end(2):])
        self.reindex()

    def rename(self, go_fid, name):
        self.set_field(go_fid, "m_Name", name)
        self.reindex()

    # ── глубокое клонирование поддерева ─────────────────────────────────
    def collect_subtree(self, tr):
        """Возвращает список fileID всех доков поддерева (GO, трансформы, компоненты)."""
        ids = []
        stack = [tr]
        seen_tr = set()
        while stack:
            t = stack.pop()
            if t in seen_tr:
                continue
            seen_tr.add(t)
            go = self.go_of.get(t)
            if go is not None:
                ids.append(go)
                ids.extend(self.comps.get(go, []))
            ids.append(t)
            stack.extend(self.children.get(t, []))
        # убираем дубли, сохраняя порядок
        out, seen = [], set()
        for i in ids:
            if i not in seen:
                seen.add(i)
                out.append(i)
        return out

    def clone_subtree(self, tr, new_name=None, parent_tr=None, remap_refs=True):
        """Клонирует поддерево, возвращает (old_tr, new_tr, {old_id: new_id})."""
        ids = self.collect_subtree(tr)
        idmap = {old: self.new_fid() for old in ids}
        # Собираем множество всех fileID поддерева для подстановки ссылок
        subtree_ids = set(ids)
        for old in ids:
            cid, fid, text = self.get(old)
            new_text = text
            if remap_refs:
                def sub(m):
                    v = int(m.group(1))
                    return "{fileID: %d" % idmap[v] if v in idmap else m.group(0)
                new_text = FID_RE.sub(sub, new_text)
            nfid = idmap[fid]
            # имя меняем только у корня клона — дети сохраняют свои имена
            if cid == 1 and new_name and fid == self.go_of.get(tr):
                new_text = re.sub(r"(?m)^\s*m_Name: .*$", "  m_Name: " + new_name, new_text)
            self.docs.append([cid, nfid, new_text])
        self.reindex()
        new_tr = idmap[tr]
        if parent_tr is not None:
            self.add_child(parent_tr, new_tr)
            # явно прописываем отца корню клона
            self.set_field(new_tr, "m_Father", "{fileID: %d}" % parent_tr)
            self.reindex()
        return tr, new_tr, idmap

    # ── сохранение ──────────────────────────────────────────────────────
    def save(self, backup=True):
        if backup:
            bak = self.path + ".bak"
            if not os.path.exists(bak):
                open(bak, "w", encoding="utf-8").write(open(self.path, encoding="utf-8").read())
        out = [self.header]
        for cid, fid, text in self.docs:
            out.append("--- !u!%d &%s%s" % (cid, fid, ""))
            if not text.endswith("\n"):
                text += "\n"
            out.append(text)
        data = "".join(out)
        if not data.endswith("\n"):
            data += "\n"
        open(self.path, "w", encoding="utf-8", newline="").write(data)


# ────────────────────────────────────────────────────────────────────────────
# Команды
# ────────────────────────────────────────────────────────────────────────────
def cmd_tree(sc, args):
    roots = [t for t, p in sc.parent.items() if not p]
    seen = set()

    def walk(tr, depth):
        go = sc.go_of.get(tr)
        nm = sc.names.get(go, "?")
        cls = "RectTransform" if sc.get(tr)[0] == 224 else "Transform"
        comps = []
        for c in sc.comps.get(go, []):
            cid = sc.get(c)[0]
            comps.append(CLASS_NAMES.get(cid, str(cid)))
        print("%s%s- %s%s  %s(%s) [%s]%s" % (DIM, "  " * depth, RESET, nm, DIM,
                                             cls, ",".join(comps), RESET))
        if depth >= args.depth:
            return
        for c in sc.children.get(tr, []):
            walk(c, depth + 1)

    if args.name:
        for go in sc.find(name=args.name):
            tr = sc.tr_of.get(go)
            print("%s%s%s" % (BANNER, sc.path_of(tr) + "/" + sc.names[go], RESET))
            for c in sc.children.get(tr, []):
                walk(c, 1)
    else:
        for r in roots:
            walk(r, 0)


def cmd_find(sc, args):
    for go in sc.find(name=args.name):
        tr = sc.tr_of.get(go)
        comps = [CLASS_NAMES.get(sc.get(c)[0], str(sc.get(c)[0])) for c in sc.comps.get(go, [])]
        print("%-70s go=%-12s tr=%-12s %s" % (sc.path_of(tr) + "/" + sc.names[go], go, tr,
                                              DIM + ",".join(comps) + RESET))


def cmd_show(sc, args):
    cid, fid, text = sc.get(args.fileid)
    print("%s--- !u!%s &%s%s%s" % (BANNER, cid, CLASS_NAMES.get(cid, ""), fid, RESET))
    print(text.rstrip())


def cmd_set(sc, args):
    for spec in args.field:
        if "=" not in spec:
            log("  ! неверный --field %s (нужно key=value)" % spec, ERR)
            continue
        k, v = spec.split("=", 1)
        sc.set_field(args.fileid, k, v, args.occurrence)
        log("  ✓ %s.%s = %s" % (args.fileid, k, v), OK)
    sc.save(args.no_backup is False)


def cmd_call(sc, args):
    sc.set_call(args.fileid, args.index, args.method, args.target, args.type_name)
    log("  ✓ вызов №%s в %s обновлён" % (args.index, args.fileid), OK)
    sc.save(not args.no_backup)


def cmd_child(sc, args):
    sc.add_child(args.parent, args.child, args.at)
    log("  ✓ %s теперь ребёнок %s" % (args.child, args.parent), OK)
    sc.save(not args.no_backup)


def cmd_clone(sc, args):
    old, new, idmap = sc.clone_subtree(args.node, args.name, args.parent)
    log("  ✓ клон %s -> %s (%s доков)" % (old, new, len(idmap)), OK)
    for spec in args.field or []:
        if "=" not in spec:
            continue
        fid_s, rest = spec.split(" ", 1) if " " in spec else spec.split("=", 1)
        if "=" in fid_s:
            log("  ! формат: '<fileID> <поле>=<значение>'", ERR)
            continue
        key, val = rest.split("=", 1)
        target = idmap.get(int(fid_s), int(fid_s))
        sc.set_field(target, key, val)
        log("      %s.%s = %s" % (target, key, val))
    sc.save(not args.no_backup)


def cmd_guid(sc, args):
    """Показывает guid скрипта по имени файла (ищет по всему Assets)."""
    root = args.root or "Assets"
    for dirpath, _d, files in os.walk(root):
        for f in files:
            if f.lower().endswith(args.name.lower() + ".meta"):
                p = os.path.join(dirpath, f)
                g = re.search(r"(?m)^guid: (\w+)$", open(p, encoding="utf-8", errors="replace").read())
                print("%s  %s" % (g.group(1) if g else "?", p[:-5]))
                return
    log("  ! скрипт %s не найден" % args.name, ERR)


def main():
    ap = argparse.ArgumentParser(description="ORANGE FORGE — правка Unity-сцен и префабов")
    sub = ap.add_subparsers(dest="cmd")

    p = sub.add_parser("tree"); p.add_argument("file"); p.add_argument("--name")
    p.add_argument("--depth", type=int, default=3); p.set_defaults(fn=cmd_tree)

    p = sub.add_parser("find"); p.add_argument("file"); p.add_argument("--name", required=True)
    p.set_defaults(fn=cmd_find)

    p = sub.add_parser("show"); p.add_argument("file"); p.add_argument("--fileid", type=int, required=True)
    p.set_defaults(fn=cmd_show)

    p = sub.add_parser("set"); p.add_argument("file"); p.add_argument("--fileid", type=int, required=True)
    p.add_argument("--field", nargs="+", required=True)
    p.add_argument("--occurrence", type=int, default=0)
    p.add_argument("--no-backup", action="store_true"); p.set_defaults(fn=cmd_set)

    p = sub.add_parser("call"); p.add_argument("file"); p.add_argument("--fileid", type=int, required=True)
    p.add_argument("--index", type=int, default=0); p.add_argument("--method")
    p.add_argument("--target", type=int); p.add_argument("--type-name", dest="type_name")
    p.add_argument("--no-backup", action="store_true"); p.set_defaults(fn=cmd_call)

    p = sub.add_parser("child"); p.add_argument("file"); p.add_argument("--parent", type=int, required=True)
    p.add_argument("--child", type=int, required=True); p.add_argument("--at", type=int)
    p.add_argument("--no-backup", action="store_true"); p.set_defaults(fn=cmd_child)

    p = sub.add_parser("clone"); p.add_argument("file"); p.add_argument("--node", type=int, required=True)
    p.add_argument("--name"); p.add_argument("--parent", type=int)
    p.add_argument("--field", nargs="*")
    p.add_argument("--no-backup", action="store_true"); p.set_defaults(fn=cmd_clone)

    p = sub.add_parser("guid"); p.add_argument("--name", required=True); p.add_argument("--root")
    p.add_argument("file", nargs="?", default=None); p.set_defaults(fn=cmd_guid)

    args = ap.parse_args()
    if not args.cmd:
        print(__doc__)
        return 0
    log("\n%s╭──────────────────────────────────────────────╮%s" % (BANNER, RESET))
    log("%s│  O R A N G E   F O R G E   ·  %-22s│%s" % (BANNER, args.cmd.upper(), RESET))
    log("%s╰──────────────────────────────────────────────╯%s" % (BANNER, RESET))
    if args.cmd == "guid":
        args.fn(None, args)
        return 0
    sc = Scene(args.file)
    args.fn(sc, args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
