#!/usr/bin/env python3
"""Эталонные сборки 3DMork — теперь это реальные готовые ПК из магазина.

Каждая строка — готовая сборка из магазина (Ready PC) в усечённом виде:
2 планки памяти, 2 накопителя (у Office — 1), видеокарты — как продаётся
(1 или 2, одинаковые или разные). Детали берутся из тех же префабов, что
и в игре, поэтому очки совпадают с прогоном купленного ПК. Порядок строк —
по убыванию итогового счёта (Dream на первом месте).

Запуск: python3 tools/3dmork_builds.py
"""
import importlib.util
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location("model", os.path.join(ROOT, "tools/3dmork_score_model.py"))
model = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(model)

# Счёт деталей - из их префабов (tools/dump_hardware.py).
PART_SCORE = {
    "RTX5090": 13500, "RTX4080Ti": 11000, "RTX4080": 10000, "RTX3080Ti": 7000,
    "RTX3080": 6000, "RTX2080Ti": 5500, "GTX1080Ti": 4500, "GTX1060": 3000,
    "GT440": 150, "Titan V": 5800,
    "RAM 64GB(RGB)": 12000, "RAM 64GB": 10000, "RAM 32GB(RGB)": 6000, "RAM 32GB": 5000,
    "RAM 16GB(RGB)": 5000, "RAM 16GB": 4500, "RAM 8GB": 3000, "RAM 4GB": 2000,
    "SSD 16TB": 9000, "SSD_M.2 8TB": 9000, "SSD 8TB": 8000, "SSD 4TB": 7000,
    "SSD_M.2 2TB": 7000, "SSD 2TB": 6000, "SSD_M.2 1TB": 6000, "SSD 1TB": 5000,
    "SSD_M.2 512GB": 5000, "SSD 512GB": 4000, "SSD_M.2 256GB": 4500,
    "HDD 1TB": 1200, "HDD 500GB": 1000, "HDD 2TB": 1500, "FlashDrive": 200,
}

# Процессоры: название, счёт детали, штатная частота в ГГц.
# Все CPU, что продаются в готовых ПК.
CPU_PART = {
    "RMD Ryzen 9 7950X": (3725, 3.0),
    "i7-14700K": (4250, 3.4),
    "i9-12900K": (3800, 3.2),
    "i9-9900K": (3200, 3.6), "i9-7900X": (3000, 3.3),
    "Xeon E5-2689": (2800, 3.3), "i7-8700K": (2600, 3.7), "i5-8400": (2400, 2.8),
    "i3-8300": (2200, 3.7), "Celeron G3920": (2000, 2.9),
}

# Готовые ПК — усечённые до 2 планок/2 накопителей, видеокарты как в магазине.
# Порядок — по убыванию итогового счёта, чтобы таблица всегда шла сверху вниз.
# Ключи — это ключи перевода из Translate.txt (Dream PC, Workstation PC …),
# поэтому таблица читается на любом языке.
BUILDS = [
    {"key": "Dream PC", "cpu": "RMD Ryzen 9 7950X",
     "gpus": ["RTX5090", "RTX4080Ti"], "rams": ["RAM 64GB(RGB)", "RAM 64GB(RGB)"],
     "drives": ["SSD 16TB", "SSD_M.2 8TB"]},
    {"key": "Workstation PC", "cpu": "RMD Ryzen 9 7950X",
     "gpus": ["RTX4080Ti", "Titan V"], "rams": ["RAM 64GB(RGB)", "RAM 64GB(RGB)"],
     "drives": ["SSD 2TB", "SSD_M.2 1TB"]},
    {"key": "Aquarium PC", "cpu": "i7-14700K",
     "gpus": ["RTX5090"], "rams": ["RAM 32GB(RGB)", "RAM 32GB(RGB)"],
     "drives": ["SSD 2TB", "SSD 1TB"]},
    {"key": "Gaming PC", "cpu": "i9-12900K",
     "gpus": ["RTX4080"], "rams": ["RAM 32GB(RGB)", "RAM 32GB(RGB)"],
     "drives": ["SSD 2TB", "SSD 1TB"]},
    {"key": "Home PC", "cpu": "i5-8400",
     "gpus": ["GTX1060"], "rams": ["RAM 8GB", "RAM 8GB"],
     "drives": ["SSD 512GB", "HDD 1TB"]},
    {"key": "Office PC", "cpu": "Celeron G3920",
     "gpus": ["GT440"], "rams": ["RAM 4GB", "RAM 4GB"],
     "drives": ["HDD 500GB"]},
]


def pretty(part: str) -> str:
    """Та же запись, что делает C#: "RTX5090Ti" -> "RTX 5090 Ti"."""
    match = re.match(r"^([A-Za-z]{2,4})(\d{2,4})([A-Za-z]{0,3})$", part)
    if not match:
        return part
    head, digits, tail = match.groups()
    return "%s %s%s" % (head, digits, (" " + tail) if tail else "")


def gpu_label(parts: list) -> str:
    if not parts:
        return "Integrated"
    names = [pretty(part) for part in parts]
    if len(names) == 1:
        return names[0]
    if len(names) == 2 and names[0] == names[1]:
        return "2x " + names[0]
    return " + ".join(names)


def drive_label(parts: list) -> str:
    names = [part.replace("SSD_M.2 ", "M.2 ").replace("FlashDrive", "USB").replace("HDD ", "HDD ")
             for part in parts]
    if len(names) == 1:
        return names[0]
    return " + ".join(names)


def entry(build: dict) -> dict:
    cpu_score, frequency = CPU_PART[build["cpu"]]
    return {
        "key": build["key"],
        "pc": build["key"],
        "cpu": build["cpu"],
        "gpu": gpu_label(build["gpus"]),
        "gpuScore": sum(PART_SCORE[g] for g in build["gpus"]) if build["gpus"] else 200,
        "cpuScore": model.cpu_power(cpu_score, frequency),
        "ramScore": sum(PART_SCORE[r] for r in build["rams"]),
        "driveScore": sum(PART_SCORE[d] for d in build["drives"]),
    }


def reference_benchmarks() -> list:
    return [entry(build) for build in BUILDS]


def main() -> int:
    problems = []
    # В готовых ПК бывает 1 видеокарта, 2 разные или даже GT440 — это нормально,
    # главное, что каждая сборка — это реальный товар из магазина.
    for build in BUILDS:
        if not (1 <= len(build["gpus"]) <= 2):
            problems.append("в сборке %s неожиданное число видеокарт: %d" % (build["key"], len(build["gpus"])) )
        if len(build["rams"]) != 2:
            problems.append("в сборке %s не две планки памяти" % build["key"])
        if not (1 <= len(build["drives"]) <= 2):
            problems.append("в сборке %s не 1-2 накопителя" % build["key"])
    for cap in (240.0, 60.0):
        print("=== потолок %d кадров" % cap)
        previous = None
        previous_gpu = None
        for build in BUILDS:
            item = entry(build)
            result = model.scores(item["gpuScore"], item["cpuScore"],
                                  item["ramScore"], item["driveScore"], cap)
            width, height = model.render_resolution(item["gpuScore"], item["cpuScore"],
                                                   item["ramScore"], cap)
            note = ""
            if previous is not None and result["total"] >= previous:
                note = "  <-- ПОРЯДОК СБОИТ"
                problems.append("счёт %s не меньше предыдущего при потолке %d" % (item["key"], cap))
            if previous_gpu is not None and item["gpuScore"] >= previous_gpu:
                note += "  <-- ВИДЕО НЕ ПАДАЕТ"
                problems.append("видеокарты %s не слабее предыдущей" % item["key"])
            previous, previous_gpu = (result["total"], item["gpuScore"])
            print("%-18s %-17s %-25s G %6d P %8.1f M %6d S %6d FPS %3d  %6d  %dx%d%s"
                  % (item["key"], item["cpu"], item["gpu"], item["gpuScore"], item["cpuScore"],
                     item["ramScore"], item["driveScore"], result["achievedFps"],
                     result["total"], width, height, note))
    if problems:
        print("ПРОБЛЕМЫ:")
        for problem in problems:
            print("  " + problem)
        return 1
    print("порядок таблицы строго убывает по итоговому счёту и видеокартам (готовые ПК)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
