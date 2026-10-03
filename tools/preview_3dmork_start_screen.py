#!/usr/bin/env python3
"""Собирает превью стартового экрана 3DMork с примерами данных.

Скрипт берёт настоящий префаб, подставляет в текстовые поля те же значения,
которые ThreeDMork записывает в рантайме (эталонная таблица + история
прогонов + сводка по железу), и рендерит PNG через preview_prefab_ui.
Нужен только для визуальной проверки вёрстки, в сборку игры не попадает.

Запуск:
    python3 tools/preview_3dmork_start_screen.py [out.png]
"""

from __future__ import annotations

import os
import re
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import preview_prefab_ui  # noqa: E402
import importlib.util  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "dm3_score", os.path.join(os.path.dirname(os.path.abspath(__file__)), "3dmork_score_model.py"))
score_model = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(score_model)

ROOT = Path(__file__).resolve().parents[1]
PREFAB = ROOT / "Assets/Resources/apps/3DMork.prefab"

# Эталонные машины и их счёт берутся из ThreeDMork.cs той же формулой,
# что и в игре (tools/3dmork_score_model.py), чтобы превью не расходилось.
FPS_CAP = 240.0

def reference_rows():
    return [(entry["pc"], entry["cpu"], entry["gpu"],
             str(score_model.scores(entry["gpuScore"], entry["cpuScore"],
                                    entry["ramScore"], entry["driveScore"],
                                    FPS_CAP)["total"]))
            for entry in score_model.reference_benchmarks()]


HISTORY = [
    ("03.10.26 14:22", "113072", 240),
    ("01.10.26 19:05", "112884", 240),
    ("28.09.26 11:47", "111668", 60),
    ("21.09.26 20:12", "110975", 60),
    ("14.09.26 09:30", "108430", 60),
]

def translated(language: str):
    """Тот же ключ + string.Format, что использует ThreeDMork в рантайме."""
    table_path = ROOT / "Assets/Resources/Translate.txt"
    rows = [line.split("\t") for line in table_path.read_text(encoding="utf-8").splitlines() if line.strip()]
    languages = rows[0][1:]
    column = languages.index(language) if language in languages else 0
    return {row[0]: row[1 + column] for row in rows[1:] if len(row) > column + 1 and row[1 + column].strip()}


def hardware_sample(language: str = "EN") -> dict:
    """Пример сводки по железу: две видеокарты, два накопителя, без "(Clone)"."""
    tr = translated(language)
    one = lambda key, value: tr[key].format(value)
    board = "ATX (Black)"
    return {
        "SelfName": board,
        "SelfSpec": "i7-14700K  /  RTX 5090 + RTX 4080",
        "SelfScore": "113072",
        "SelfFps": "240",
        "Average": tr["3DMork average"].format(8, 54727, 113072),
        "BestScore": one("3DMork best score", 113072),
        "HwTitle": one("3DMork hardware title", board),
        "HwCpu": one("3DMork hardware cpu", "i7-14700K"),
        "HwGpu": one("3DMork hardware gpu", "RTX 5090 + RTX 4080"),
        "HwRam": one("3DMork hardware ram", "128 GB"),
        "HwDrive": one("3DMork hardware drive", "SSD 16TB + SSD 8TB"),
        "EmptyHint": "",
    }


def set_label(text: str, name: str, value: str) -> str:
    game_object = re.search(
        r"--- !u!1 &(\d+)\nGameObject:\n(?:(?!--- ).)*?  m_Name: %s\n" % re.escape(name), text, re.S)
    if not game_object:
        return text
    go_id = game_object.group(1)
    component = re.search(
        r"--- !u!114 &(\d+)\nMonoBehaviour:\n(?:(?!--- ).)*?  m_GameObject: \{fileID: %s\}\n(?:(?!--- ).)*?  m_Text: .*\n"
        % go_id, text, re.S)
    if not component:
        return text
    block = re.search(r"--- !u!114 &%s\nMonoBehaviour:\n(?:(?!--- ).)*\n" % component.group(1), text, re.S)
    if not block:
        return text
    updated = re.sub(r"  m_Text: .*\n", "  m_Text: '%s'\n" % value.replace("'", "''"), block.group(0))
    return text.replace(block.group(0), updated, 1)


def main(out_path: str, language: str = "EN") -> int:
    text = PREFAB.read_text(encoding="utf-8")
    # колонка FPS в таблицах содержит только число, подпись вынесена в заголовок
    reference_fps = [str(score_model.scores(entry["gpuScore"], entry["cpuScore"],
                                            entry["ramScore"], entry["driveScore"],
                                            FPS_CAP)["achievedFps"])
                     for entry in score_model.reference_benchmarks()]

    for index, (pc, cpu, gpu, score) in enumerate(reference_rows()):
        for key, value in (("LbName_%d" % index, pc), ("LbCpu_%d" % index, cpu),
                           ("LbGpu_%d" % index, gpu), ("LbScore_%d" % index, score),
                           ("LbFps_%d" % index, reference_fps[index])):
            text = set_label(text, key, value)

    for index, (date, score, fps) in enumerate(HISTORY):
        for key, value in (("HistDate_%d" % index, date), ("HistScore_%d" % index, score),
                           ("HistFps_%d" % index, str(fps)),
                           ("HistIndex_%d" % index, "#%d" % (len(HISTORY) - index))):
            text = set_label(text, key, value)

    for key, value in hardware_sample(language).items():
        text = set_label(text, key, value)

    with tempfile.NamedTemporaryFile("w", suffix=".prefab", delete=False, encoding="utf-8") as handle:
        handle.write(text)
        temp_path = handle.name

    try:
        preview_prefab_ui.main(temp_path, "3DMork", out_path, panel="StartPanel",
                                language=language)
    finally:
        os.unlink(temp_path)
    return 0


if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else str(ROOT / "docs/3dmork_start_screen.png")
    language = sys.argv[2] if len(sys.argv) > 2 else "EN"
    Path(target).parent.mkdir(parents=True, exist_ok=True)
    sys.exit(main(target, language))
