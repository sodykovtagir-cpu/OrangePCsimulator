#!/usr/bin/env python3
"""Показать реальные характеристики комплектующих из префабов игры.

Нужен, чтобы эталонные сборки в 3DMork состояли из настоящих деталей,
которые игрок может купить, а не из выдуманных названий.

Запуск: python3 tools/dump_hardware.py
"""
import glob
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COMPONENTS = os.path.join(ROOT, "Assets", "Resources", "components")


def fields(path: str) -> dict:
    with open(path, encoding="utf-8", errors="ignore") as handle:
        text = handle.read()
    out: dict = {}
    for match in re.finditer(r"^  (\w+): (.+)$", text, flags=re.M):
        out.setdefault(match.group(1), match.group(2))
    return out


def main() -> int:
    rows = []
    for path in sorted(glob.glob(os.path.join(COMPONENTS, "*.prefab"))):
        data = fields(path)
        score = data.get("score") or data.get("Score")
        if score is None or not score.lstrip("-").isdigit():
            continue
        rows.append((os.path.basename(path)[:-7], int(score),
                     data.get("frequency"), data.get("capacity")))
    for name, score, frequency, capacity in sorted(rows, key=lambda row: -row[1]):
        print("%-36s %6d  freq=%-6s cap=%s" % (name, score, frequency, capacity))
    print("всего деталей со счётом: %d" % len(rows))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
