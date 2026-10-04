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

def reference_rows(language: str = "EN"):
    """Строки таблицы: название сборки переводится по ключу из ThreeDMork.cs."""
    tr = translated(language)
    return [(tr.get(entry["pc"], entry["pc"]), entry["cpu"], entry["gpu"],
             str(score_model.scores(entry["gpuScore"], entry["cpuScore"],
                                    entry["ramScore"], entry["driveScore"],
                                    FPS_CAP)["total"]))
            for entry in score_model.reference_benchmarks()]


def history_sample() -> list[tuple[str, str, int]]:
    """Пример истории: прогоны той же формулы, что и в игре.

    Раньше здесь стояли значения старой формулы (1059 FPS и 117669 очков),
    из-за чего превью выглядело как поломка. Смена формулы чистит историю
    в PlayerPrefs, и превью показывает уже чистую таблицу.
    """
    entries = score_model.reference_benchmarks()[:3]
    dates = [("04.10.26 20:14", 240.0), ("04.10.26 19:52", 180.0), ("03.10.26 21:05", 120.0)]
    rows = []
    for entry, (date, cap) in zip(entries, dates):
        result = score_model.scores(entry["gpuScore"], entry["cpuScore"],
                                    entry["ramScore"], entry["driveScore"], cap)
        rows.append((date, str(result["total"]), int(result["achievedFps"])))
    return rows

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
        "SelfCpu": "RMD Ryzen 9 7950X",
        "SelfGpu": "RTX 5090 + RTX 4080 Ti",
        "SelfRam": "64 GB",
        "SelfScore": "100228",
        "SelfFps": "180",
        "Average": tr["3DMork average"].format(8, 60958, 100228),
        "BestScore": one("3DMork best score", 100228),
        "HwTitle": one("3DMork hardware title", board),
        "HwCpu": one("3DMork hardware cpu", "RMD Ryzen 9 7950X"),
        "HwGpu": one("3DMork hardware gpu", "RTX 5090 + RTX 4080"),
        "HwRam": one("3DMork hardware ram", "64 GB"),
        "HwDrive": one("3DMork hardware drive", "SSD 1TB + M.2 1TB"),
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

    for index, (pc, cpu, gpu, score) in enumerate(reference_rows(language)):
        for key, value in (("LbName_%d" % index, pc), ("LbCpu_%d" % index, cpu),
                           ("LbGpu_%d" % index, gpu), ("LbScore_%d" % index, score),
                           ("LbFps_%d" % index, reference_fps[index])):
            text = set_label(text, key, value)

    history = history_sample()
    for index in range(5):
        # незаполненные строки остаются пустыми, а не "--", как в игре
        if index < len(history):
            date, score, fps = history[index]
            cells = (date, score, str(fps), "#%d" % (len(history) - index))
        else:
            cells = ("", "", "", "")
        for key, value in zip(("HistDate_%d" % index, "HistScore_%d" % index,
                               "HistFps_%d" % index, "HistIndex_%d" % index), cells):
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
