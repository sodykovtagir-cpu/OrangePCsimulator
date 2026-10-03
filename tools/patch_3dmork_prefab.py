#!/usr/bin/env python3
"""Patch the hand edited Assets/Resources/apps/3DMork.prefab in place.

The prefab is the user's own Unity re-save, it is never regenerated. This
patcher only performs the three edits the app needs and is idempotent:

1. shrink   - every RectTransform and every font size is scaled by SCALE so the
              window becomes ~15% smaller (root 850x480.6 -> 722.5x408.5), the
              relative layout, the manual anchors and the pivot setup stay as
              the user left them, and best fit labels may shrink but never grow;
2. repair   - nothing else, the hand edited layout is taken as it is;
3. localize - static labels get their translation key as authored text plus a
              LocalizationText component, dynamic values are translated by
              ThreeDMork.cs with Localization.GetText.

Usage: python3 tools/patch_3dmork_prefab.py [--dry-run]
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
PREFAB = os.path.join(REPO, "Assets", "Resources", "apps", "3DMork.prefab")
META = os.path.join(REPO, "Assets", "Scripts", "Assembly-CSharp", "LocalizationText.cs.meta")

SCALE = 0.85
ORIGINAL_ROOT = (850.0, 480.6282)

BLOCK = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", re.M | re.S)


# static prefab labels: object path -> translation key
LABELS = {
    "3DMork/Title/Text": "3DMork",
    "3DMork/StartPanel/LeaderboardPanel/Title": "3DMork comparison title",
    "3DMork/StartPanel/LeaderboardPanel/Subtitle": "3DMork comparison subtitle",
    "3DMork/StartPanel/LeaderboardPanel/Header_PC": "3DMork column pc",
    "3DMork/StartPanel/LeaderboardPanel/Header_CPU": "3DMork column cpu",
    "3DMork/StartPanel/LeaderboardPanel/Header_GPU": "3DMork column gpu",
    "3DMork/StartPanel/LeaderboardPanel/Header_SCORE": "3DMork column score",
    "3DMork/StartPanel/LeaderboardPanel/Header_FPS": "3DMork column fps",
    "3DMork/StartPanel/LeaderboardPanel/Hint": "3DMork comparison hint",
    "3DMork/StartPanel/HistoryPanel/Title": "3DMork history title",
    "3DMork/StartPanel/HistoryPanel/Subtitle": "3DMork history subtitle",
    "3DMork/StartPanel/HistoryPanel/EmptyHint": "3DMork history empty",
    "3DMork/StartPanel/HistoryPanel/Header_DATE": "3DMork column date",
    "3DMork/StartPanel/HistoryPanel/Header_SCORE": "3DMork column score",
    "3DMork/StartPanel/HistoryPanel/Header_FPS": "3DMork column fps",
    "3DMork/StartPanel/ButtonStart/Text": "3DMork start test",
    "3DMork/StartPanel/ButtonStartClose/Text": "Close",
    "3DMork/ResultsPanel/ButtonRun/Text": "3DMork run again",
    "3DMork/ResultsPanel/ButtonMenu/Text": "3DMork to menu",
    "3DMork/ResultsPanel/ButtonClose/Text": "Close",
    "3DMork/ResultsPanel/Header": "3DMork results title",
    "3DMork/ResultsPanel/ScoreCircle/ScoreLabel": "3DMork score label",
    "3DMork/ResultsPanel/Category (0)/Label": "3DMork graphics score",
    "3DMork/ResultsPanel/Category (1)/Label": "3DMork physics score",
    "3DMork/ResultsPanel/Category (2)/Label": "3DMork memory score",
    "3DMork/ResultsPanel/Category (3)/Label": "3DMork fps score",
}

TEXT_SCRIPT = "5f7201a12d95ffc409449d95f23cf332"


def number(value):
    rounded = round(value, 4)
    if abs(rounded - round(rounded)) < 1e-6:
        return str(int(round(rounded)))
    return ("%.4f" % rounded).rstrip("0").rstrip(".")


def read_blocks(text):
    return BLOCK.findall(text)


def main():
    dry = "--dry-run" in sys.argv
    text = open(PREFAB, encoding="utf-8").read()
    original = text
    blocks = BLOCK.findall(text)
    header = text[:text.index("--- !u!")]
    assert header.strip().startswith("%YAML"), "unexpected prefab header"

    # docs[fid] = [class, body] so a body can be replaced while patching
    docs = {int(fid): [int(cls), body] for cls, fid, body in blocks}
    order = [(int(cls), int(fid)) for cls, fid, _ in blocks]

    names, parent, game_object = {}, {}, {}
    for fid, (cls, body) in docs.items():
        if cls == 1:
            match = re.search(r"^  m_Name: (.*)$", body, re.M)
            names[fid] = match.group(1).strip() if match else "?"
        else:
            match = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            if match:
                game_object[fid] = int(match.group(1))
            if cls == 224:
                parent[fid] = int(re.search(r"m_Father: \{fileID: (\d+)\}", body).group(1))

    def object_name(fid):
        return names.get(game_object.get(fid), "?")

    def path_of(fid):
        out = []
        while fid:
            out.append(object_name(fid))
            fid = parent.get(fid)
        return "/".join(reversed(out))

    root = [fid for fid in docs if docs[fid][0] == 224 and parent.get(fid) == 0]
    assert len(root) == 1, "expected a single root RectTransform"
    root = root[0]
    by_path = {}
    for fid in docs:
        if docs[fid][0] == 224:
            by_path.setdefault(path_of(fid), fid)

    changed = []

    def set_field(fid, field, new_line, indent="  "):
        body = docs[fid][1]
        match = re.search(r"^%s%s: .*$" % (indent, re.escape(field)), body, re.M)
        if not match:
            return False
        line = "%s%s: %s" % (indent, field, new_line)
        if line == match.group(0):
            return False
        docs[fid][1] = body[:match.start()] + line + body[match.end():]
        return True

    def field(fid, name_, indent="  "):
        match = re.search(r"^%s%s: (.+)$" % (indent, re.escape(name_)), docs[fid][1], re.M)
        return match.group(1) if match else None

    root_size = re.search(r"m_SizeDelta: \{x: (-?[\d.]+), y: (-?[\d.]+)\}", docs[root][1])
    root_size = (float(root_size.group(1)), float(root_size.group(2)))
    if (abs(root_size[0] - ORIGINAL_ROOT[0]) > 0.01
            or abs(root_size[1] - ORIGINAL_ROOT[1]) > 0.01):
        print("geometry already applied (root %sx%s), scaling pass skipped" % root_size)
    else:
        for fid, (cls, _) in list(docs.items()):
            if cls != 224:
                continue
            # the window keeps the position the user gave it, only its size shrinks
            names_ = ("m_SizeDelta",) if fid == root else ("m_AnchoredPosition", "m_SizeDelta")
            for name_ in names_:
                values = re.search(r"\{x: (-?[\d.]+), y: (-?[\d.]+)\}", field(fid, name_))
                x, y = float(values.group(1)) * SCALE, float(values.group(2)) * SCALE
                if set_field(fid, name_, "{x: %s, y: %s}" % (number(x), number(y))):
                    changed.append("%s %s" % (path_of(fid), name_))

        for fid, (cls, _) in list(docs.items()):
            if cls != 114 or "m_FontData" not in docs[fid][1]:
                continue
            old_size = int(field(fid, "m_FontSize", "    "))
            size = max(8, int(round(old_size * SCALE)))
            set_field(fid, "m_FontSize", str(size), "    ")
            if field(fid, "m_BestFit", "    ") == "1":
                # best fit may shrink the label but must never grow it
                set_field(fid, "m_MaxSize", str(size), "    ")
            changed.append("%s font %d -> %d" % (path_of(fid), old_size, size))

    # ---- localization -----------------------------------------------------
    guid = re.search(r"^guid: (\w+)$", open(META, encoding="utf-8").read(), re.M).group(1)
    used = set(docs)
    next_fid = max(used) + 1

    def has_localizer(go):
        for fid in re.findall(r"^  - component: \{fileID: (\d+)\}$", docs[go][1], re.M):
            fid = int(fid)
            if fid not in docs or docs[fid][0] != 114:
                continue
            script = re.search(r"m_Script: \{fileID: \d+, guid: (\w+)", docs[fid][1])
            if script and script.group(1) == guid:
                return True
        return False

    for wanted, key in LABELS.items():
        assert wanted in by_path, "missing object " + wanted
        rect = by_path[wanted]
        go = game_object[rect]
        text_fid = None
        for fid in re.findall(r"^  - component: \{fileID: (\d+)\}$", docs[go][1], re.M):
            fid = int(fid)
            if fid in docs and docs[fid][0] == 114 and "\n  m_Text: " in docs[fid][1]:
                text_fid = fid
        assert text_fid, "no Text component on " + wanted

        current = field(text_fid, "m_Text").strip().strip("'\"")
        if current != key:
            quote = "'" if (":" in key or key.startswith("[")) else ""
            if set_field(text_fid, "m_Text", "%s%s%s" % (quote, key, quote)):
                changed.append("%s text -> %s" % (wanted, key))

        if not has_localizer(go):
            while next_fid in used:
                next_fid += 1
            used.add(next_fid)
            docs[next_fid] = [114, (
                "MonoBehaviour:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                "  m_GameObject: {fileID: %d}\n"
                "  m_Enabled: 1\n"
                "  m_EditorHideFlags: 0\n"
                "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
                "  m_Name: \n"
                "  m_EditorClassIdentifier: \n" % (go, guid))]
            # Unity keeps the transform first, localizers are appended at the end
            marker = None
            for line in re.finditer(r"^  - component: \{fileID: \d+\}$\n?", docs[go][1], re.M):
                marker = line
            assert marker, "no component list on " + wanted
            docs[go][1] = (docs[go][1][:marker.end()]
                           + "  - component: {fileID: %d}\n" % next_fid
                           + docs[go][1][marker.end():])
            changed.append("%s + LocalizationText" % wanted)
            next_fid += 1

    # ---- write back -------------------------------------------------------
    extra = sorted(fid for fid in docs if (docs[fid][0], fid) not in order)
    pieces = [header]
    for cls, fid in order + [(docs[fid][0], fid) for fid in extra]:
        pieces.append("--- !u!%d &%d\n%s" % (cls, fid, docs[fid][1]))
    out = "".join(pieces)
    if text.endswith("\n") and not out.endswith("\n"):
        out += "\n"

    if out == original:
        print("prefab already patched, nothing to do")
        return
    if not dry:
        open(PREFAB, "w", encoding="utf-8").write(out)
    print("patched %d values%s" % (len(changed), " (dry run)" if dry else ""))
    for line in changed:
        print("   ", line)


if __name__ == "__main__":
    main()
