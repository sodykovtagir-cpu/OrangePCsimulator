#!/usr/bin/env python3
"""Проверки приложения 3DMork: префаб, обвязка полей и запрет процедурного UI.

Unity Editor не требуется: всё валидируется по YAML-префабу и исходнику скрипта.
Запуск:
    python3 tools/test_3dmork_prefab.py
"""

from __future__ import annotations

import os
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from check_prefab_yaml import load_blocks  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
PREFAB = ROOT / "Assets/Resources/apps/3DMork.prefab"
SCRIPT = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/ThreeDMork.cs"
STAGE = ROOT / "Assets/Resources/3DMork_Stage.prefab"
SCENE = ROOT / "Assets/Scenes/3DMork_Room.unity"
SCRIPT_GUID = "48ab01ab22fd4bc78201ca8ad4b6d729"
LOCALIZER_GUID = re.search(r"^guid: (\w+)$",
                           (ROOT / "Assets/Scripts/Assembly-CSharp/LocalizationText.cs.meta")
                           .read_text(encoding="utf-8"), re.M).group(1)

LEADERBOARD_ROWS = 6
HISTORY_ROWS = 5

failures: list[str] = []
checks = 0


def check(condition: bool, message: str) -> bool:
    global checks
    checks += 1
    if condition:
        print(f"  ok  {message}")
    else:
        print(f"FAIL  {message}")
        failures.append(message)
    return bool(condition)


# ---------------------------------------------------------------------------
# 1. Префаб существует и структурно корректен
# ---------------------------------------------------------------------------
check(PREFAB.exists(), "префаб 3DMork на месте")
if not PREFAB.exists():    raise SystemExit(1)

result = subprocess.run(
    [sys.executable, str(ROOT / "tools/check_prefab_yaml.py"), str(PREFAB)],
    capture_output=True, text=True,
)
check(result.returncode == 0, "префаб 3DMork проходит проверку ссылок и иерархии")

blocks = load_blocks(str(PREFAB))
by_id = {}
names = {}
active = {}
for class_id, file_id, body in blocks:
    by_id[file_id] = (class_id, body)
    if class_id == 1:
        match = re.search(r"^  m_Name: (.*)$", body, flags=re.M)
        names[file_id] = match.group(1).strip() if match else "?"
        act = re.search(r"^  m_IsActive: (\d)", body, flags=re.M)
        active[file_id] = act.group(1) == "1" if act else True


def game_object(name: str) -> str:
    for fid, value in names.items():
        if value == name:
            return fid
    return ""


def components_of(go_id: str) -> list[str]:
    return re.findall(r"^  - component: \{fileID: (\d+)\}$", by_id[go_id][1], flags=re.M)


def component_kind(fid: str) -> str:
    class_id, body = by_id[fid]
    if class_id != 114:
        return {1: "GameObject", 224: "RectTransform", 222: "CanvasRenderer"}.get(class_id, str(class_id))
    guid = re.search(r"guid: ([0-9a-f]+)", body)
    return {
        "fe87c0e1cc204ed48ad3b37840f39efc": "Image",
        "5f7201a12d95ffc409449d95f23cf332": "Text",
        "4e29b1a8efbd4b44bb3f3716e73f07ff": "Button",
        "67db9e8f0e2ae9c40bc1e2b64352a6b4": "Slider",
        "1344c3c82d62a2a41a3576d8abb8e3ea": "RawImage",
    }.get(guid.group(1), "MonoBehaviour")


def owner_of(fid: str) -> str:
    class_id, body = by_id[fid]
    if class_id == 1:
        return fid
    match = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", body, flags=re.M)
    return match.group(1) if match else ""


# ---------------------------------------------------------------------------
# 2. Все три экрана собраны в префабе (ничего не создаётся скриптом)
# ---------------------------------------------------------------------------
for panel in ("StartPanel", "TestPanel", "ResultsPanel"):
    go = game_object(panel)
    check(bool(go), f"экран {panel} собран в префабе")
    if go:
        check(active[go] == (panel == "StartPanel"),
              f"{panel} активен в префабе: {'да' if panel == 'StartPanel' else 'нет'}")

def rect_of(go_id: str) -> tuple[float, float]:
    """(width, height) of the RectTransform of a GameObject."""
    rect_id = components_of(go_id)[0]
    body = by_id[rect_id][1]
    size = re.search(r"^  m_SizeDelta: \{x: (-?[\d.]+), y: (-?[\d.]+)\}$", body, re.M)
    return float(size.group(1)), float(size.group(2))


def descendants(go_id: str) -> list[str]:
    rect = [c for c in components_of(go_id) if by_id[c][0] == 224]
    if not rect:
        return []
    out = []
    for child in re.findall(r"^  - \{fileID: (\d+)\}$", by_id[rect[0]][1], flags=re.M):
        child_go = owner_of(child)
        out.append(child_go)
        out.extend(descendants(child_go))
    return out


start = game_object("StartPanel")
start_buttons = [
    names[g] for g in descendants(start) if g in names
    if any(component_kind(c) == "Button" for c in components_of(g))
]
check(sorted(start_buttons) == ["ButtonStart", "ButtonStartClose"],
      "на стартовом экране есть кнопки START TEST и CLOSE (%s)" % start_buttons)

# Таблица сравнения: 8 строк × 6 колонок
for index in range(LEADERBOARD_ROWS):
    row = game_object("LbRow_%d" % index)
    check(bool(row), f"строка таблицы сравнения #{index} собрана в префабе")
leaderboard = game_object("LeaderboardPanel")
check(bool(leaderboard), "панель сравнения с другими ПК собрана в префабе")
history = game_object("HistoryPanel")
check(bool(history), "панель истории тестов собрана в префабе")
hardware = game_object("HardwarePanel")
check(bool(hardware), "панель сводки по железу собрана в префабе")
for index in range(HISTORY_ROWS):
    check(bool(game_object("HistRow_%d" % index)), f"строка истории #{index} собрана в префабе")

# ---------------------------------------------------------------------------
# 3. Цвета сериализованы в нормализованном виде Unity
# ---------------------------------------------------------------------------
bad_colors = 0
for fid, (class_id, body) in by_id.items():
    for line in body.split("\n"):
        match = re.match(r"^\s*m_\w*Color: \{r: ([\d.\-]+), g: ([\d.\-]+), b: ([\d.\-]+), a: ([\d.\-]+)\}$", line)
        if match:
            for value in match.groups():
                if not 0.0 <= float(value) <= 1.0:
                    bad_colors += 1
        elif re.match(r"^\s*m_\w*Color: \{r: ", line):
            bad_colors += 1
check(bad_colors == 0, f"все цветовые поля записаны в формате Unity (ошибок: {bad_colors})")

# ---------------------------------------------------------------------------
# 4. Поля скрипта в префабе заполнены
# ---------------------------------------------------------------------------
script_block = None
for fid, (class_id, body) in by_id.items():
    if class_id == 114 and SCRIPT_GUID in body:
        script_block = body
        break
check(script_block is not None, "скрипт ThreeDMork лежит на корне префаба")

fields: dict[str, list[str]] = {}
scalars: dict[str, str] = {}
if script_block:
    body = script_block.split("  m_EditorClassIdentifier:", 1)[1]
    current = None
    for line in body.split("\n"):
        if line.startswith("  - {fileID:"):
            if current:
                fields[current].append(re.search(r"\{fileID: (\d+)\}", line).group(1))
        elif re.match(r"^  [A-Za-z_][A-Za-z0-9_]*:", line):
            current = line.split(":")[0].strip()
            value = line.split(":", 1)[1].strip()
            scalars[current] = value
            ref = re.search(r"\{fileID: (\d+)\}", line)
            fields[current] = [ref.group(1)] if ref and ref.group(1) != "0" else []

REQUIRED_SINGLE = [
    "startPanel", "testPanel", "resultsPanel",
    "buttonStart", "buttonStartClose", "buttonMenu",
    "leaderboardSelfRank", "leaderboardSelfName", "leaderboardSelfCpu",
    "leaderboardSelfGpu",
    "leaderboardSelfScore", "leaderboardSelfFps", "leaderboardAverage",
    "historyBest", "historyEmpty",
    "hardwareTitle", "hardwareCpu", "hardwareGpu", "hardwareRam", "hardwareDrive",
    "viewportImage", "fpsText", "resolutionText", "testProgressBar",
    "textTotalScore", "markCircle", "buttonClose", "buttonRun",
]
REQUIRED_ARRAYS = [
    ("leaderboardRank", LEADERBOARD_ROWS, "Text"),
    ("leaderboardName", LEADERBOARD_ROWS, "Text"),
    ("leaderboardCpu", LEADERBOARD_ROWS, "Text"),
    ("leaderboardGpu", LEADERBOARD_ROWS, "Text"),
    ("leaderboardScore", LEADERBOARD_ROWS, "Text"),
    ("leaderboardFps", LEADERBOARD_ROWS, "Text"),
    ("historyIndex", HISTORY_ROWS, "Text"),
    ("historyDate", HISTORY_ROWS, "Text"),
    ("historyScore", HISTORY_ROWS, "Text"),
    ("historyFps", HISTORY_ROWS, "Text"),
    ("marks", 4, "Slider"),
    ("text_marks", 4, "Text"),
]

for name in REQUIRED_SINGLE:
    refs = fields.get(name, [])
    ok = len(refs) == 1 and refs[0] in by_id
    check(ok, f"поле {name} связано с объектом префаба")

for name, count, kind in REQUIRED_ARRAYS:
    refs = fields.get(name, [])
    ok = len(refs) >= count and all(
        r in by_id and component_kind(r) == kind and names.get(owner_of(r)) for r in refs
    )
    check(ok, f"массив {name} содержит {count} компонентов {kind}")

check(scalars.get("stagePrefab", "").startswith("{fileID: 6000000000000001, guid:"),
      "отдельная сцена/комната бенчмарка подключена через stagePrefab")
check(STAGE.exists(), "префаб отдельной комнаты бенчмарка на месте")
check(SCENE.exists(), "отдельная сцена бенчмарка на месте")

# ---------------------------------------------------------------------------
# 4б. Сетка таблиц: подпись стоит ровно на месте своей колонки
# ---------------------------------------------------------------------------
# Панели растягиваются вместе с окном, поэтому растянутая по ширине ячейка
# заголовка (m_AnchorMax.x = 1) уносила центрированную подпись в середину
# пустого места, а "#"history попадал на колонку процессора. Проверяем, что
# у заголовка и его данных совпадают якоря, позиция и ширина.
rect_of_go: dict[str, str] = {}
rect_parent: dict[str, int] = {}
for file_id, (class_id, body) in by_id.items():
    if class_id != 224:
        continue
    go_id = owner_of(file_id)
    rect_of_go[go_id] = file_id
    match = re.search(r"^  m_Father: \{fileID: (\d+)\}$", body, flags=re.M)
    rect_parent[file_id] = match.group(1) if match else "0"


def transform(go_id: str) -> dict:
    body = by_id[rect_of_go[go_id]][1]
    def pair(field: str) -> tuple[float, float]:
        match = re.search(r"^  %s: \{x: (-?[\d.]+), y: (-?[\d.]+)\}$" % field, body, re.M)
        return float(match.group(1)), float(match.group(2))
    return {"pos": pair("m_AnchoredPosition"), "size": pair("m_SizeDelta"),
            "anchor_min": pair("m_AnchorMin"), "anchor_max": pair("m_AnchorMax")}


def inside_panel(go_id: str) -> tuple[float, float]:
    """Позиция объекта относительно своей панели (родители могут быть любыми)."""
    x = y = 0.0
    current = go_id
    while current and names.get(current) not in ("LeaderboardPanel", "HistoryPanel"):
        entry = transform(current)
        x += entry["pos"][0]
        y += entry["pos"][1]
        parent = rect_parent.get(rect_of_go.get(current, ""), "0")
        current = owner_of(parent) if parent in by_id else ""
    return round(x, 2), round(y, 2)


def objects_named(name: str) -> list[str]:
    return [go for go, value in names.items() if value == name]


# заголовок и его колонка: (подпись в таблице, ячейка данных)
COLUMNS = [("Header_#", "Rank"), ("Header_PC", "Name"), ("Header_CPU", "Cpu"),
           ("Header_GPU", "Gpu"), ("Header_SCORE", "Score"), ("Header_FPS", "Fps")]
PREFIX = {"LeaderboardPanel": "Lb", "HistoryPanel": "Hist"}
ROWS = {"LeaderboardPanel": 8, "HistoryPanel": 5}


def panel_of(go_id: str) -> str:
    current = go_id
    while current and names.get(current) not in PREFIX:
        parent = rect_parent.get(rect_of_go.get(current, ""), "0")
        current = owner_of(parent) if parent in by_id else ""
    return names.get(current, "")


for header_name, column in COLUMNS:
    for header in objects_named(header_name):
        panel = panel_of(header)
        header_rect = transform(header)
        if header_rect["size"][0] <= 0:
            check(False, "%s не растянут (ширина %s)" % (header_name, header_rect["size"][0]))
            continue
        if header_rect["anchor_max"] != header_rect["anchor_min"]:
            check(False, "%s не привязан к левому краю, как его данные" % header_name)
            continue
        # первая строка этой же таблицы - эталон для колонки
        for index in range(ROWS.get(panel, 0)):
            wanted = ["%s%s_%d" % (PREFIX[panel], column, index),
                      "%sRank_%d" % (PREFIX[panel], index),
                      "%sIndex_%d" % (PREFIX[panel], index)]
            cells = [go for name in wanted for go in objects_named(name) if panel_of(go) == panel]
            if not cells:
                continue
            cell = cells[0]
            cell_rect = transform(cell)
            header_x = inside_panel(header)[0]
            cell_x = inside_panel(cell)[0]
            check(abs(header_x - cell_x) <= 0.05
                  and abs(header_rect["size"][0] - cell_rect["size"][0]) <= 0.05
                  and header_rect["anchor_max"] == cell_rect["anchor_max"],
                  "%s в %s совпадает со своей колонкой (x %s против %s, ширина %s против %s)"
                  % (header_name, panel, header_x, cell_x,
                     header_rect["size"][0], cell_rect["size"][0]))
            break

# строки таблиц идут с одинаковым шагом, шапка выше первой строки
for panel, prefix, count in (("LeaderboardPanel", "Lb", 8), ("HistoryPanel", "Hist", 5)):
    tops = []
    for index in range(count):
        wanted = ["%sRank_%d" % (prefix, index), "%sIndex_%d" % (prefix, index)]
        cell = [go for name in wanted for go in objects_named(name) if panel_of(go) == panel]
        check(bool(cell), "строка %d таблицы %s на месте" % (index, panel))
        if cell:
            tops.append(inside_panel(cell[0])[1])
    steps = [round(tops[i + 1] - tops[i], 2) for i in range(len(tops) - 1)]
    check(len(set(steps)) == 1 and steps[0] < 0,
          "шаг строк таблицы %s одинаков (%s)" % (panel, sorted(set(steps))))
    header_top = inside_panel([go for go in objects_named("Header_#") if panel_of(go) == panel][0])[1]
    check(header_top > tops[0], "шапка таблицы %s выше первой строки" % panel)

# своя строка повторяет колонки таблицы, иначе длинная сборка налезает на счёт
for name, column in (("SelfCpu", "Cpu"), ("SelfGpu", "Gpu")):
    cell = [go for go in objects_named(name) if panel_of(go) == "LeaderboardPanel"]
    check(bool(cell), "своя строка содержит ячейку %s" % name)
    if not cell:
        continue
    reference_cells = [go for index in range(LEADERBOARD_ROWS)
                       for go in objects_named("Lb%s_%d" % (column, index))]
    check(all(inside_panel(cell[0])[0] == inside_panel(go)[0]
              and transform(cell[0])["size"][0] == transform(go)["size"][0]
              for go in reference_cells),
          "%s стоит на месте колонки %s" % (name, column))

# длинная строка состава ПК не переносится на вторую строку
for name in ("SelfCpu", "SelfGpu", "SelfName"):
    for fid, (cls, body) in by_id.items():
        if cls != 114 or "m_FontData" not in body:
            continue
        if owner_of(fid) not in objects_named(name):
            continue
        overflow = re.search(r"^    m_HorizontalOverflow: (\d)$", body, re.M)
        check(overflow is not None and overflow.group(1) == "1",
              "%s не переносится на вторую строку" % name)

# ---------------------------------------------------------------------------
# 5. В скрипте нет процедурного UI
# ---------------------------------------------------------------------------
script_text = SCRIPT.read_text(encoding="utf-8")
ui_creation = [
    r"AddComponent\s*<\s*Text\s*>",
    r"AddComponent\s*<\s*Image\s*>",
    r"AddComponent\s*<\s*Button\s*>",
    r"AddComponent\s*<\s*Slider\s*>",
    r"AddComponent\s*<\s*RawImage\s*>",
    r"CreateInstance",
]
offenders = [p for p in ui_creation if re.search(p, script_text)]
check(not offenders, "скрипт не создаёт UI-компоненты в рантайме (найдено: %s)" % ", ".join(offenders))

# единственный new GameObject в скрипте - это 3D-камера, а не элемент интерфейса
new_objects = re.findall(r"new\s+GameObject\s*\(\s*\"([^\"]+)\"", script_text)
check(new_objects == ["3DMork_Camera"],
      "new GameObject используется только для камеры 3D (%s)" % new_objects)

# камера и RenderTexture — это 3D-часть, а не UI
check("new GameObject(\"3DMork_Camera\")" in script_text,
      "тестовая камера создаётся отдельно от UI")
check("AddComponent<Camera>()" in script_text, "камера бенчмарка создаётся программно")

check("PlayerPrefs.SetString" in script_text and "PlayerPrefs.GetString" in script_text,
      "история прогонов сохраняется в PlayerPrefs")
check("ReferenceBenchmarks" in script_text, "таблица сравнения наполняется эталонными данными")
check("ShowStartScreen" in script_text, "перед тестом открывается стартовый экран")

# ---------------------------------------------------------------------------
# 6. Патчер префаба идемпотентен, локализация строк на месте
# ---------------------------------------------------------------------------
# Префаб правится вручную в Unity, генератор tools/build_3dmork_prefab.py больше
# не используется: его проверяет только то, что он не запускается.
before = PREFAB.read_text(encoding="utf-8")
result = subprocess.run([sys.executable, "tools/patch_3dmork_prefab.py"],
                        cwd=str(ROOT), capture_output=True, text=True)
check(result.returncode == 0, "патчер префаба отрабатывает без ошибок")
check(PREFAB.read_text(encoding="utf-8") == before,
      "повторный запуск патчера не меняет префаб")

import importlib.util  # noqa: E402

spec = importlib.util.spec_from_file_location("dm3_i18n", ROOT / "tools/3dmork_i18n.py")
i18n = importlib.util.module_from_spec(spec)
spec.loader.exec_module(i18n)

table = {}
for row in (ROOT / "Assets/Resources/Translate.txt").read_text(encoding="utf-8").splitlines():
    cells = row.split("\t")
    if cells and cells[0]:
        table[cells[0]] = cells[1:]

LOCALIZED = {
    "Header_PC": "3DMork column pc", "Header_CPU": "3DMork column cpu",
    "Header_GPU": "3DMork column gpu", "Header_SCORE": "3DMork column score",
    "Header_DATE": "3DMork column date", "Header_FPS": "3DMork column fps",
    "Title": None, "Subtitle": None, "EmptyHint": "3DMork history empty",
    "Hint": "3DMork comparison hint", "ScoreLabel": "3DMork score label",
    "Label": None, "Text": None, "Header": "3DMork results title",
}

localized = 0
missing_localizer = []
missing_key = []
for file_id, (class_id, body) in by_id.items():
    if class_id != 114 or "\n  m_Text: " not in body:
        continue
    go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body).group(1)
    key = re.search(r"\n  m_Text: '?([^'\n]*)'?\n", body).group(1)
    if not key.startswith("3DMork") and key not in ("Close", "Benchmark", "Start", "Back"):
        continue
    localized += 1
    if key not in table:
        missing_key.append(key)
    has = any(by_id.get(c, (0, ""))[0] == 114 and LOCALIZER_GUID in by_id.get(c, (0, ""))[1]
              for c in components_of(go))
    if not has:
        missing_localizer.append(names.get(go, "?") + " -> " + key)
check(localized >= 25, f"статические подписи хранят ключ перевода (найдено {localized})")
check(not missing_key, f"все ключи префаба есть в таблице перевода ({missing_key[:3]})")
check(not missing_localizer,
      f"на каждой статической подписи висят LocalizationText ({missing_localizer[:3]})")

# размер окна уменьшен и совпадает с SetDefaultSize в скрипте
root = game_object("3DMork")
root_rect = rect_of(root)
check(abs(root_rect[0] - 722.5) < 0.01 and abs(root_rect[1] - 408.534) < 0.01,
      f"окно уменьшено до 722.5x408.5 (сейчас {root_rect[0]:.1f}x{root_rect[1]:.1f})")
match = re.search(r"SetDefaultSize\(new Vector2\(([\d.]+)f, ([\d.]+)f\)\)", script_text)
check(bool(match) and abs(float(match.group(1)) - 722.5) < 0.01,
      "SetDefaultSize в скрипте совпадает с размером префаба")

# best fit не должен увеличивать подпись выше авторского кегля
grown = 0
for file_id, (class_id, body) in by_id.items():
    if class_id != 114 or "m_FontData" not in body:
        continue
    size = re.search(r"m_FontSize: (\d+)", body)
    best = re.search(r"m_BestFit: (\d)", body)
    top = re.search(r"m_MaxSize: (\d+)", body)
    if best and best.group(1) == "1" and top and int(top.group(1)) > int(size.group(1)):
        grown += 1
check(grown == 0, f"best fit может только уменьшать шрифт (растущих подписей: {grown})")

# удалённые пользователем объекты не возвращаются
for deleted in ("FooterNote", "SceneInfoText"):
    check(game_object(deleted) == "", f"удалённый объект {deleted} не возвращён")
check("sceneInfoText" in fields and not fields["sceneInfoText"],
      "поле sceneInfoText осталось пустым после удаления подписи")

# ---------------------------------------------------------------------------
# 7. Очки, лимит кадров и разрешение считаются по реальному железу
# ---------------------------------------------------------------------------
spec = importlib.util.spec_from_file_location("dm3_score",
                                              ROOT / "tools/3dmork_score_model.py")
score_model = importlib.util.module_from_spec(spec)
spec.loader.exec_module(score_model)

check("GetFpsCap" in script_text and "Application.targetFrameRate" in script_text,
      "потолок кадров берётся из настроек игры (Application.targetFrameRate)")
check("GraphicsBootstrap.TargetFps" in script_text,
      "потолок кадров подстрахован значением из настроек")
check("Mathf.Clamp(currentFps, 5f, fpsCap)" in script_text or
      "Mathf.Clamp(baseFps * seg.loadMultiplier + jitter, 5f, fpsCap)" in script_text,
      "счётчик FPS теста не превышает потолок из настроек")
check("new RenderTexture(renderSize.x, renderSize.y" in script_text and
      "new RenderTexture(960, 540" not in script_text,
      "разрешение рендера теста подбирается по мощности ПК, а не задано константой")
check("PickRenderResolution" in script_text,
      "выбор разрешения вынесен в отдельную функцию")

# эталонная таблица и прогоны считаются одной формулой
check("ScoresAt(fpsCap)" in script_text and
      "ComputeScores(gpuScore, cpuScore, ramScore, driveScore, fpsCap)" in script_text,
      "эталонные машины считаются той же формулой, что и прогоны игрока")
entries = re.findall(r"new LeaderboardEntry\((.*?)\)", script_text)
check(entries and all(line.count(",") == 6 for line in entries),
      "эталонная строка описывает железо, а не готовый результат")
references = score_model.reference_benchmarks()
check(all(entry["gpuScore"] <= 27000 and entry["ramScore"] <= 24000
          and entry["driveScore"] <= 18000 and entry["cpuScore"] <= 4213.0
          for entry in references),
      "эталонные сборки не превосходят максимум игрового железа")
for cap in (30.0, 60.0, 144.0, 240.0):
    totals = [score_model.scores(e["gpuScore"], e["cpuScore"], e["ramScore"],
                                 e["driveScore"], cap)["total"] for e in references]
    check(all(a > b for a, b in zip(totals, totals[1:])),
          "эталонная таблица отсортирована по убыванию счёта (потолок %d)" % cap)
    check(all(0 < t <= 130000 for t in totals),
          "счёт эталонных машин правдоподобен (потолок %d)" % cap)

# каждая эталонная строка - реальная сборка из деталей игры
import importlib.util as _importlib  # noqa: E402

_spec = _importlib.spec_from_file_location("dm3_builds", ROOT / "tools/3dmork_builds.py")
builds_module = _importlib.module_from_spec(_spec)
_spec.loader.exec_module(builds_module)

for declared, built in zip(references, builds_module.reference_benchmarks()):
    check(declared["pc"] == built["key"] and declared["cpu"] == built["cpu"]
          and declared["gpu"] == built["gpu"] and declared["gpuScore"] == built["gpuScore"]
          and abs(declared["cpuScore"] - built["cpuScore"]) < 1.0
          and declared["ramScore"] == built["ramScore"]
          and declared["driveScore"] == built["driveScore"],
          "строка %s в ThreeDMork.cs совпадает со сборкой %s (%s + %s)"
          % (declared["pc"], built["key"], built["cpu"], built["gpu"]))

check(all("CPU " not in entry["cpu"] for entry in references),
      "в таблице нет названий процессоров с префиксом CPU")
# готовые ПК — это реальные товары: у Home/Gaming/Aquarium по одной карте, у Dream/Workstation — две разные
mixed = [entry["gpu"] for entry in references if not entry["gpu"] or entry["gpu"] == "--"]
check(not mixed, "у каждой эталонной сборки указана видеокарта (нарушители: %s)" % mixed)
check(len({entry["gpu"] for entry in references}) == len(references),
      "видеокарты в эталонных сборках не повторяются между строками")
check(len({entry["cpu"] for entry in references}) >= 5
      and len({entry["gpu"] for entry in references}) == len(references),
      "в эталонных сборках нет повторов и случайных сочетаний видеокарт")
check(all(references[index]["gpuScore"] > references[index + 1]["gpuScore"]
          and references[index]["ramScore"] >= references[index + 1]["ramScore"]
          and references[index]["driveScore"] >= references[index + 1]["driveScore"]
          for index in range(len(references) - 1)),
      "сборки идут по убыванию: видеокарты, память и накопители (готовые ПК)")
# Сборка мечты: две самые мощные видеокарты, самая большая память и два
# самых ёмких накопителя. Процессор - Ryzen 9 7950X: по счёту детали он чуть
# слабее i7-14700K, но именно он стоит в сборке мечты (выбор владельца игры),
# и вся лестница ниже идёт по убыванию, чтобы AMD оставался наверху.
strongest_gpu = max(value for part, value in builds_module.PART_SCORE.items()
                    if part.startswith(("RTX", "GTX", "RX")))
strongest_ram = max(value for part, value in builds_module.PART_SCORE.items()
                    if part.startswith("RAM"))
strongest_drives = sorted((value for part, value in builds_module.PART_SCORE.items()
                           if part.startswith(("SSD", "HDD", "FlashDrive"))), reverse=True)[:2]
check(references[0]["gpuScore"] == 24500
      and references[0]["ramScore"] == 2 * strongest_ram
      and references[0]["driveScore"] == sum(strongest_drives)
      and references[0]["cpu"] == "RMD Ryzen 9 7950X",
      "сборка мечты (Dream PC): RTX 5090 + RTX 4080 Ti (24500), "
      "2 x самая большая память и два самых ёмких накопителя")
check(builds_module.CPU_PART["RMD Ryzen 9 7950X"][0] ==
      builds_module.CPU_PART["RMD Ryzen 9 7950X"][0] and
      references[0]["cpuScore"] == round(
          score_model.cpu_power(*builds_module.CPU_PART["RMD Ryzen 9 7950X"]), 0),
      "мощность Ryzen 9 7950X в таблице посчитана той же формулой, что и у игрока")
# у готовых ПК процессор не обязан идти строго по убыванию: Dream и Workstation на одном Ryzen,
# а Aquarium на i7-14700K сильнее обоих — таблица сортируется по итоговому счёту, а не по CPU
check(references[0]["cpu"] == "RMD Ryzen 9 7950X",
      "первая строка — Dream PC на Ryzen 9 7950X")
check(builds_module.main() == 0, "порядок эталонных сборок сбалансирован (tools/3dmork_builds.py)")

dream = score_model.scores(24500, 3565.0, 24000, 18000, 240.0)
budget = score_model.scores(150, 1897.0, 4000, 1000, 240.0)
check(references[0]["gpuScore"] == 24500 and references[0]["pc"] == "Dream PC",
      "первая строка таблицы - Dream PC (RTX 5090 + RTX 4080 Ti + Ryzen 9 7950X)")
check(dream["total"] > 100000, "максимальный ПК набирает %d очков" % dream["total"])
check(6 * budget["total"] < dream["total"],
      "слабый ПК набирает в разы меньше максимального (%d против %d)"
      % (budget["total"], dream["total"]))
check(score_model.scores(24500, 3565.0, 24000, 18000, 60.0)["total"] <
      dream["total"], "потолок из настроек снижает FPS-составляющую счёта")
check(score_model.render_resolution(3000, 1897.0, 2000, 60.0) !=
      score_model.render_resolution(27000, 3565.0, 24000, 60.0),
      "слабый ПК рендерит тест в меньшем разрешении")
old_cpu = score_model.cpu_power(2200, 3.7)   # i3-8300
new_cpu = score_model.cpu_power(2400, 2.8)   # i5-8400
check(new_cpu > old_cpu,
      "новый i5-8400 мощнее разогнанного i3-8300 (%.0f против %.0f)" % (new_cpu, old_cpu))
check(score_model.cpu_power(4250, 3.4) > score_model.cpu_power(3725, 3.0),
      "i7-14700K мощнее Ryzen 9 7950X по игровым очкам компонента")
check("cpu.frequency * cpu.Score" not in script_text and "CpuPower(cpu)" in script_text,
      "мощность процессора больше не домножается на частоту")
check("FormulaVersion" in script_text and "ResetStaleResults" in script_text,
      "прогоны по прошлой формуле очищаются при запуске")

check("(Clone)" in script_text and 'Replace("(Clone)"' in script_text,
      "из названий деталей убирается суффикс (Clone)")
check("HardwareType.Drive" in script_text and "gpuName2" in script_text and
      "driveName2" in script_text,
      "сводка по железу перечисляет две видеокарты и два накопителя")

# ---------------------------------------------------------------------------
# 8. Интерфейс остался читаемым после уменьшения окна
# ---------------------------------------------------------------------------
sizes = {}
for file_id, (class_id, body) in by_id.items():
    if class_id != 114 or "m_FontData" not in body:
        continue
    size = re.search(r"m_FontSize: (\d+)", body)
    go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body).group(1)
    if size:
        sizes[names.get(go, go)] = int(size.group(1))
tables = [name for name in sizes if re.match(r"(Lb|Hist)(Rank|Name|Cpu|Gpu|Score|Fps|Index|Date)_", name)]
check(len(tables) >= 50 and min(sizes[name] for name in tables) >= 11,
      "ячейки таблиц не мельче 11 px (минимум %d)"
      % min(sizes[name] for name in tables))
check(sizes.get("FpsText", 0) >= 26, "счётчик FPS крупный (%d px)" % sizes.get("FpsText", 0))
check(sizes.get("ScoreText", 0) >= 40, "итоговый счёт крупный (%d px)" % sizes.get("ScoreText", 0))

# переводчики строк приложения
for key in ("3DMork start test", "3DMork hardware cpu", "3DMork average", "3DMork fps"):
    check(key in i18n.STRINGS, f"ключ {key} есть в таблице переводов")
check(len(i18n.LANGS) == 42 and
      all(len(values) == len(i18n.LANGS) for values in i18n.STRINGS.values()),
      f"на каждую строку 3DMork ({len(i18n.STRINGS)}) есть {len(i18n.LANGS)} переводов")

# ---------------------------------------------------------------------------
print()
print(f"Проверок выполнено: {checks}")
if failures:
    print(f"ПРОВАЛЕНО проверок: {len(failures)}")
    for item in failures:
        print(f"  - {item}")
    raise SystemExit(1)
print("Все проверки пройдены.")
