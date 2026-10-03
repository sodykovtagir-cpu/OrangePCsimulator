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
import fnmatch
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
    "3DMork/TestPanel/TopOverlay/ResText": "3DMork resolution",
}

# Кегль шрифта по объектам префаба. Окно уменьшено до 722x408, но подписи
# не уменьшаются вместе с ним: иначе интерфейс становится нечитаемым.
# Шаблон сопоставляется с полным путём объекта (fnmatch), порядок важен -
# применяется первое совпадение.
FONTS = [
    ("3DMork/Title/Text", 16),
    ("3DMork/StartPanel/LeaderboardPanel/Title", 15),
    ("3DMork/StartPanel/LeaderboardPanel/Subtitle", 10),
    ("3DMork/StartPanel/LeaderboardPanel/Header_*", 11),
    ("*LbRank_*", 11),
    ("*LbName_*", 11),
    ("*LbCpu_*", 11),
    ("*LbGpu_*", 11),
    ("*LbScore_*", 11),
    ("*LbFps_*", 11),
    ("3DMork/StartPanel/LeaderboardPanel/SelfRank", 12),
    ("3DMork/StartPanel/LeaderboardPanel/SelfName", 11),
    ("3DMork/StartPanel/LeaderboardPanel/SelfSpec", 10),
    ("3DMork/StartPanel/LeaderboardPanel/SelfScore", 11),
    ("3DMork/StartPanel/LeaderboardPanel/SelfFps", 11),
    ("3DMork/StartPanel/LeaderboardPanel/Average", 10),
    ("3DMork/StartPanel/LeaderboardPanel/Hint", 10),
    ("3DMork/StartPanel/HistoryPanel/Title", 15),
    ("3DMork/StartPanel/HistoryPanel/Subtitle", 10),
    ("3DMork/StartPanel/HistoryPanel/Header_*", 11),
    ("*HistIndex_*", 11),
    ("*HistDate_*", 11),
    ("*HistScore_*", 11),
    ("*HistFps_*", 11),
    ("3DMork/StartPanel/HistoryPanel/EmptyHint", 11),
    ("3DMork/StartPanel/HistoryPanel/BestScore", 12),
    ("3DMork/StartPanel/HistoryPanel/HardwarePanel/HwTitle", 12),
    ("3DMork/StartPanel/HistoryPanel/HardwarePanel/HwCpu", 11),
    ("3DMork/StartPanel/HistoryPanel/HardwarePanel/HwGpu", 11),
    ("3DMork/StartPanel/HistoryPanel/HardwarePanel/HwRam", 11),
    ("3DMork/StartPanel/HistoryPanel/HardwarePanel/HwDrive", 11),
    ("3DMork/StartPanel/ButtonStart/Text", 17),
    ("3DMork/StartPanel/ButtonStartClose/Text", 15),
    ("3DMork/TestPanel/TopOverlay/FpsText", 26),
    ("3DMork/TestPanel/TopOverlay/ResText", 14),
    ("3DMork/ResultsPanel/ButtonMenu/Text", 15),
    ("3DMork/ResultsPanel/ButtonRun/Text", 15),
    ("3DMork/ResultsPanel/ButtonClose/Text", 15),
    ("3DMork/ResultsPanel/Header", 22),
    ("3DMork/ResultsPanel/ScoreCircle/ScoreText", 42),
    ("3DMork/ResultsPanel/ScoreCircle/ScoreLabel", 13),
    ("3DMork/ResultsPanel/Category (*)/Label", 15),
    ("3DMork/ResultsPanel/Category (*)/Value", 17),
]

# Подпись разрешения рендера в верхнем оверлее теста.
# Создаётся один раз, повторный запуск патчера её не трогает.
RES_TEXT_RECT = """RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: -15, y: 0}}
  m_SizeDelta: {{x: 200, y: 0}}
  m_Pivot: {{x: 1, y: 0.5}}
"""

RES_TEXT_COMPONENT = """MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.7, g: 0.85, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: 2644e223131232f41a7199fc4aef1e34, type: 3}}
    m_FontSize: 14
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 8
    m_MaxSize: 20
    m_Alignment: 5
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: '3DMork resolution'
"""

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

    # ---- font pass: окно уменьшено, подписи остаются читаемыми -------------
    def rebuild_paths():
        by_path.clear()
        for fid in docs:
            if docs[fid][0] == 224:
                by_path.setdefault(path_of(fid), fid)

    def text_of(rect):
        go = game_object[rect]
        for fid in re.findall(r"^  - component: \{fileID: (\d+)\}$", docs[go][1], re.M):
            fid = int(fid)
            if fid in docs and docs[fid][0] == 114 and "\n  m_Text: " in docs[fid][1]:
                return fid
        return None

    # ---- строка накопителей вместо дублирующей строки материнской платы ----
    board_path = "3DMork/StartPanel/HistoryPanel/HardwarePanel/HwBoard"
    drive_path = "3DMork/StartPanel/HistoryPanel/HardwarePanel/HwDrive"
    if board_path in by_path and drive_path not in by_path:
        go = game_object[by_path[board_path]]
        names[go] = "HwDrive"
        if set_field(go, "m_Name", "HwDrive"):
            changed.append("HwBoard -> HwDrive")
        text_fid = text_of(by_path[board_path])
        assert text_fid, "no Text component on " + board_path
        if set_field(text_fid, "m_Text", "'STORAGE: --'"):
            changed.append("HwDrive text -> STORAGE")

    rebuild_paths()

    for pattern, size in FONTS:
        pattern = pattern if pattern.startswith("*") else "*" + pattern
        for current_path in sorted(by_path):
            if not fnmatch.fnmatch(current_path, pattern):
                continue
            text_fid = text_of(by_path[current_path])
            if text_fid is None:
                continue
            old_size = int(field(text_fid, "m_FontSize", "    "))
            if set_field(text_fid, "m_FontSize", str(size), "    "):
                changed.append("%s font %d -> %d" % (current_path, old_size, size))
            if field(text_fid, "m_BestFit", "    ") == "1":
                # best fit только уменьшает подпись, но никогда не увеличивает
                set_field(text_fid, "m_MaxSize", str(size), "    ")

    # ---- подпись разрешения рендера в оверлее теста ------------------------
    res_path = "3DMork/TestPanel/TopOverlay/ResText"
    overlay_path = "3DMork/TestPanel/TopOverlay"
    res_text_fid = None
    if res_path in by_path:
        res_text_fid = text_of(by_path[res_path])
        assert res_text_fid, "ResText has no Text component"
    else:
        assert overlay_path in by_path, "missing object " + overlay_path
        overlay_rect = by_path[overlay_path]
        overlay_go = game_object[overlay_rect]
        used = set(docs)
        next_fid = max(used) + 1
        free = [next_fid + step for step in range(4) if next_fid + step not in used]
        res_go, res_rect, res_canvas, res_text_fid = free
        used.update(free)
        docs[res_go] = [1, (
            "GameObject:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  serializedVersion: 6\n"
            "  m_Component:\n"
            "  - component: {fileID: %d}\n"
            "  - component: {fileID: %d}\n"
            "  - component: {fileID: %d}\n"
            "  m_Layer: 5\n"
            "  m_Name: ResText\n"
            "  m_TagString: Untagged\n"
            "  m_Icon: {fileID: 0}\n"
            "  m_NavMeshLayer: 0\n"
            "  m_StaticEditorFlags: 0\n"
            "  m_IsActive: 1\n" % (res_rect, res_canvas, res_text_fid))]
        docs[res_rect] = [224, RES_TEXT_RECT.format(go=res_go, father=overlay_rect)]
        docs[res_canvas] = [222, (
            "CanvasRenderer:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: %d}\n"
            "  m_CullTransparentMesh: 1\n" % res_go)]
        docs[res_text_fid] = [114, RES_TEXT_COMPONENT.format(go=res_go)]
        names[res_go] = "ResText"
        game_object[res_rect] = res_go
        game_object[res_canvas] = res_go
        game_object[res_text_fid] = res_go
        parent[res_rect] = overlay_rect
        # Transform нового объекта уже стоит первым в его собственном списке
        # компонентов, в оверлее достаточно записи в m_Children.
        children = re.search(r"^  m_Children:\n(?:  - \{fileID: \d+\}\n)+",
                             docs[overlay_rect][1], re.M)
        assert children, "no children list on " + overlay_path
        docs[overlay_rect][1] = (docs[overlay_rect][1][:children.end()]
                                 + "  - {fileID: %d}\n" % res_rect
                                 + docs[overlay_rect][1][children.end():])
        changed.append("+ 3DMork/TestPanel/TopOverlay/ResText")

    rebuild_paths()

    # ---- поля компонента ThreeDMork: накопители и разрешение ---------------
    script_fid = None
    for fid in docs:
        if docs[fid][0] == 114 and "\n  leaderboardRank:" in docs[fid][1]:
            script_fid = fid
            break
    assert script_fid, "ThreeDMork component not found"
    board_field = re.search(r"^  hardwareBoard: \{fileID: (\d+)\}$", docs[script_fid][1], re.M)
    if board_field:
        docs[script_fid][1] = docs[script_fid][1][:board_field.start()] + \
            "  hardwareDrive: {fileID: %s}" % board_field.group(1) + docs[script_fid][1][board_field.end():]
        changed.append("ThreeDMork.hardwareBoard -> hardwareDrive")
    if field(script_fid, "resolutionText") is None:
        match = re.search(r"^  fpsText: \{fileID: (\d+)\}$", docs[script_fid][1], re.M)
        assert match, "fpsText field not found"
        docs[script_fid][1] = (docs[script_fid][1][:match.end() + 1]
                               + "  resolutionText: {fileID: %d}\n" % res_text_fid
                               + docs[script_fid][1][match.end() + 1:])
        changed.append("ThreeDMork.+ resolutionText")

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
