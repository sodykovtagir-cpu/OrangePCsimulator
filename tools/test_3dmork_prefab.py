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

LEADERBOARD_ROWS = 8
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
if not PREFAB.exists():
    raise SystemExit(1)

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
    "leaderboardSelfRank", "leaderboardSelfName", "leaderboardSelfSpec",
    "leaderboardSelfScore", "leaderboardSelfFps", "leaderboardAverage",
    "historyBest", "historyEmpty",
    "hardwareTitle", "hardwareCpu", "hardwareGpu", "hardwareRam", "hardwareBoard",
    "viewportImage", "fpsText", "sceneInfoText", "testProgressBar",
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
    ok = len(refs) == count and all(
        r in by_id and component_kind(r) == kind and names.get(owner_of(r)) for r in refs
    )
    check(ok, f"массив {name} содержит {count} компонентов {kind}")

check(scalars.get("stagePrefab", "").startswith("{fileID: 6000000000000001, guid:"),
      "отдельная сцена/комната бенчмарка подключена через stagePrefab")
check(STAGE.exists(), "префаб отдельной комнаты бенчмарка на месте")
check(SCENE.exists(), "отдельная сцена бенчмарка на месте")

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
# 6. Генератор префаба воспроизводим
# ---------------------------------------------------------------------------
before = PREFAB.read_text(encoding="utf-8")
subprocess.run([sys.executable, "tools/build_3dmork_prefab.py"],
               cwd=str(ROOT), capture_output=True, text=True)
check(PREFAB.read_text(encoding="utf-8") == before,
      "повторный запуск генератора не меняет префаб")

# ---------------------------------------------------------------------------
print()
print(f"Проверок выполнено: {checks}")
if failures:
    print(f"ПРОВАЛЕНО проверок: {len(failures)}")
    for item in failures:
        print(f"  - {item}")
    raise SystemExit(1)
print("Все проверки пройдены.")
