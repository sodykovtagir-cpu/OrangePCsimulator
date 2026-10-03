"""Dump the 3DMork prefab layout: hierarchy, rects, anchors, font sizes.

Read-only helper used while tuning the window size and the font sizes.
Usage:  python3 tools/inspect_3dmork_prefab.py [name-filter]
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
PREFAB = os.path.join(REPO, "Assets", "Resources", "apps", "3DMork.prefab")
BLOCK = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", re.M | re.S)


def field(body, name, indent="  "):
    m = re.search(r"^%s%s: (.*)$" % (indent, re.escape(name)), body, re.M)
    return m.group(1) if m else None


def vec(body, name, indent="  "):
    raw = field(body, name, indent)
    if not raw:
        return None
    m = re.match(r"\{x: (-?[\d.eE+]+), y: (-?[\d.eE+]+)\}", raw)
    return (float(m.group(1)), float(m.group(2))) if m else None


def load():
    text = open(PREFAB, encoding="utf-8").read()
    docs, order = {}, []
    for m in BLOCK.finditer(text):
        docs[int(m.group(2))] = (int(m.group(1)), m.group(3))
        order.append((int(m.group(1)), int(m.group(2))))
    return text, docs, order


def main():
    want = sys.argv[1] if len(sys.argv) > 1 else ""
    text, docs, _ = load()

    name_of, comps, parent, rect_of = {}, {}, {}, {}
    for fid, (cls, body) in docs.items():
        if cls == 1:
            name_of[fid] = field(body, "m_Name")
            comps[fid] = [int(c) for c in
                          re.findall(r"^  - component: \{fileID: (\d+)\}$", body, re.M)]
        elif cls == 224:
            parent[fid] = int(re.search(r"m_Father: \{fileID: (-?\d+)\}", body).group(1))
            rect_of[fid] = body

    go_of = {}
    for go, cids in comps.items():
        for c in cids:
            if c in rect_of:
                go_of[c] = go
                break

    def path(rect):
        parts, cur = [], rect
        while cur in go_of:
            parts.append(name_of.get(go_of[cur], "?"))
            cur = parent.get(cur, 0)
            if cur == 0:
                break
        return "/".join(reversed(parts))

    def walk(rect, depth, show=True):
        node_name = name_of.get(go_of.get(rect, -1), "?")
        line = "  " * depth + node_name
        if want:
            if not show and want.lower() not in line.lower():
                for child in [r for r, p in parent.items() if p == rect]:
                    walk(child, depth + 1, False)
                return
            show = show or want.lower() in line.lower()
        body = rect_of[rect]
        pos, size = vec(body, "m_AnchoredPosition"), vec(body, "m_SizeDelta")
        amin, amax = field(body, "m_AnchorMin"), field(body, "m_AnchorMax")
        pivot = field(body, "m_Pivot")
        text_fid = None
        for c in comps.get(go_of.get(rect, -1), []):
            if c in docs and docs[c][0] == 114 and "m_FontData" in docs[c][1]:
                text_fid = c
        info = ""
        if text_fid is not None:
            tb = docs[text_fid][1]
            info = " font=%s%s text=%r" % (
                field(tb, "m_FontSize", "    "),
                " bestfit<=%s" % field(tb, "m_MaxSize", "    ")
                if field(tb, "m_BestFit", "    ") == "1" else "",
                field(tb, "m_Text"))
        print("%-52s pos=%-22s size=%-20s a=%s..%s pv=%s%s"
              % (line, pos, size, amin, amax, pivot, info))
        children = [r for r, p in parent.items() if p == rect]
        for child in sorted(children, key=lambda r: -(rect_of[r].find("m_Children"))):
            walk(child, depth + 1, show)

    for rect in [r for r, p in parent.items() if p == 0]:
        walk(rect, 0)


if __name__ == "__main__":
    main()
