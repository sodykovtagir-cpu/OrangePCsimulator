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

ROOT = Path(__file__).resolve().parents[1]
PREFAB = ROOT / "Assets/Resources/apps/3DMork.prefab"

# Те же данные, что лежат в ReferenceBenchmarks в ThreeDMork.cs
REFERENCE = [
    ("Orange Workstation", "i9-13900K", "RTX 4090", "18420", 148),
    ("TITAN X rig", "Ryzen 9 7950X", "RTX 4080", "16980", 131),
    ("Gaming Beast", "Ryzen 7 7800X3D", "RX 7900 XTX", "15240", 119),
    ("Studio Pro", "i7-13700K", "RTX 4070 Ti", "12960", 97),
    ("Creator Mini", "i7-12700H", "RTX 4060", "9130", 74),
    ("Home Cinema PC", "Ryzen 5 3600", "GTX 1660 SUPER", "6480", 54),
    ("Office Workstation", "i5-10400", "GTX 1650", "4380", 38),
    ("Budget King", "i3-12100F", "GTX 1060 6GB", "3115", 27),
]

HISTORY = [
    ("03.10.26 14:22", "11240", 96),
    ("01.10.26 19:05", "11085", 95),
    ("28.09.26 11:47", "10990", 94),
    ("21.09.26 20:12", "10875", 93),
    ("14.09.26 09:30", "10710", 92),
]

def translated(language: str):
    """Тот же ключ + string.Format, что использует ThreeDMork в рантайме."""
    table_path = ROOT / "Assets/Resources/Translate.txt"
    rows = [line.split("\t") for line in table_path.read_text(encoding="utf-8").splitlines() if line.strip()]
    languages = rows[0][1:]
    column = languages.index(language) if language in languages else 0
    return {row[0]: row[1 + column] for row in rows[1:] if len(row) > column + 1 and row[1 + column].strip()}


def hardware_sample(language: str = "EN") -> dict:
    tr = translated(language)
    one = lambda key, value: tr[key].format(value)
    return {
        "SelfName": "B550M Pro",
        "SelfSpec": "Ryzen 5 5600 / GTX 1660",
        "SelfScore": "11085",
        "SelfFps": one("3DMork fps", "95"),
        "Average": tr["3DMork average"].format(8, 10801, 11240),
        "BestScore": one("3DMork best score", 11240),
        "HwTitle": one("3DMork hardware title", "B550M Pro"),
        "HwCpu": one("3DMork hardware cpu", "Ryzen 5 5600"),
        "HwGpu": one("3DMork hardware gpu", "GTX 1660"),
        "HwRam": one("3DMork hardware ram", "16 GB"),
        "HwBoard": one("3DMork hardware board", "B550M Pro"),
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
    tr = translated(language)

    for index, (pc, cpu, gpu, score, fps) in enumerate(REFERENCE):
        for key, value in (("LbName_%d" % index, pc), ("LbCpu_%d" % index, cpu),
                           ("LbGpu_%d" % index, gpu), ("LbScore_%d" % index, score),
                           ("LbFps_%d" % index, tr["3DMork fps"].format(fps))):
            text = set_label(text, key, value)

    for index, (date, score, fps) in enumerate(HISTORY):
        for key, value in (("HistDate_%d" % index, date), ("HistScore_%d" % index, score),
                           ("HistFps_%d" % index, tr["3DMork fps"].format(fps)),
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
