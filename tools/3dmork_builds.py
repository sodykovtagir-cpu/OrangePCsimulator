#!/usr/bin/env python3
"""Эталонные сборки 3DMork из настоящих деталей игры.

Каждая строка - реальная сборка из деталей, которые есть в игре
(Assets/Resources/components). Детали подобраны так, чтобы видеокарты,
процессор, память и накопители соответствовали друг другу и шли по
убыванию сверху вниз; сборка мечты берёт самую мощную деталь в каждой
категории. Порядок строк - это и есть порядок таблицы сравнения.

Запуск: python3 tools/3dmork_builds.py
"""
import importlib.util
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location("model", os.path.join(ROOT, "tools/3dmork_score_model.py"))
model = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(model)

# Счёт деталей - из их префабов (tools/dump_hardware.py). У видеокарт
# верхние строчки лежат в переопределениях вложенных префабов.
PART_SCORE = {
    "RTX5090": 13500, "RTX4080Ti": 11000, "RTX4080": 10000, "RTX3080Ti": 7000,
    "RTX3080": 6000, "RTX2080Ti": 5500, "GTX1080Ti": 4500, "GTX1060": 3000,
    "RAM 64GB(RGB)": 12000, "RAM 64GB": 10000, "RAM 32GB(RGB)": 6000, "RAM 32GB": 5000,
    "RAM 16GB(RGB)": 5000, "RAM 16GB": 4500, "RAM 8GB": 3000, "RAM 4GB": 2000,
    "SSD 16TB": 9000, "SSD_M.2 8TB": 9000, "SSD 8TB": 8000, "SSD 4TB": 7000,
    "SSD_M.2 2TB": 7000, "SSD 2TB": 6000, "SSD_M.2 1TB": 6000, "SSD 1TB": 5000,
    "SSD_M.2 512GB": 5000, "SSD 512GB": 4000, "SSD_M.2 256GB": 4500,
    "HDD 2TB": 1500, "FlashDrive": 200,
}

# Процессоры: название, счёт детали, штатная частота в ГГц.
CPU_PART = {
    "i7-14700K": (4250, 3.4), "i7-13700K": (4090, 3.4), "i9-12900K": (3800, 3.2),
    "RMD Ryzen 9 7950X": (3725, 3.0), "i7-8700K": (2600, 3.7), "i5-8400": (2400, 2.8),
    "i3-8300": (2200, 3.7), "Celeron G3920": (2000, 2.9),
}

# Пара видеокарт в сборке всегда одинаковая: "RTX 5090 + RTX 4080 Ti" -
# это не собранная в жизни машина, а набор случайных деталей.

# Сборка мечты, дальше - ступени вниз. Названия идут ключами перевода,
# чтобы таблица читалась на любом языке (см. ThreeDMork.cs).
BUILDS = [
    {"key": "3DMork build 1", "cpu": "i7-14700K",
     "gpus": ["RTX5090", "RTX5090"], "rams": ["RAM 64GB(RGB)", "RAM 64GB(RGB)"],
     "drives": ["SSD 16TB", "SSD_M.2 8TB"]},
    {"key": "3DMork build 2", "cpu": "i7-13700K",
     "gpus": ["RTX4080Ti", "RTX4080Ti"], "rams": ["RAM 64GB", "RAM 64GB"],
     "drives": ["SSD 8TB", "SSD_M.2 8TB"]},
    {"key": "3DMork build 3", "cpu": "i9-12900K",
     "gpus": ["RTX4080", "RTX4080"], "rams": ["RAM 32GB(RGB)", "RAM 32GB(RGB)"],
     "drives": ["SSD 4TB", "SSD_M.2 2TB"]},
    {"key": "3DMork build 4", "cpu": "RMD Ryzen 9 7950X",
     "gpus": ["RTX3080Ti", "RTX3080Ti"], "rams": ["RAM 32GB", "RAM 32GB"],
     "drives": ["SSD 2TB", "SSD_M.2 2TB"]},
    {"key": "3DMork build 5", "cpu": "i7-8700K",
     "gpus": ["RTX3080", "RTX3080"], "rams": ["RAM 16GB(RGB)", "RAM 16GB(RGB)"],
     "drives": ["SSD 1TB", "SSD_M.2 1TB"]},
    {"key": "3DMork build 6", "cpu": "i5-8400",
     "gpus": ["RTX2080Ti", "RTX2080Ti"], "rams": ["RAM 16GB", "RAM 16GB"],
     "drives": ["SSD 1TB", "SSD_M.2 512GB"]},
    {"key": "3DMork build 7", "cpu": "i3-8300",
     "gpus": ["GTX1080Ti", "GTX1080Ti"], "rams": ["RAM 8GB", "RAM 8GB"],
     "drives": ["SSD 512GB", "SSD_M.2 256GB"]},
    {"key": "3DMork build 8", "cpu": "Celeron G3920",
     "gpus": ["GTX1060", "GTX1060"], "rams": ["RAM 4GB", "RAM 4GB"],
     "drives": ["HDD 2TB", "FlashDrive"]},
]


def pretty(part: str) -> str:
    """Та же запись, что делает C#: "RTX5090Ti" -> "RTX 5090 Ti"."""
    match = re.match(r"^([A-Za-z]{2,4})(\d{2,4})([A-Za-z]{0,3})$", part)
    if not match:
        return part
    head, digits, tail = match.groups()
    return "%s %s%s" % (head, digits, (" " + tail) if tail else "")


def gpu_label(parts: list) -> str:
    names = [pretty(part) for part in parts]
    if len(names) == 1:
        return names[0]
    if names[0] == names[1]:
        return "2x " + names[0]
    return names[0] + " + " + names[1]


def drive_label(parts: list) -> str:
    names = [part.replace("SSD_M.2 ", "M.2 ").replace("FlashDrive", "USB")
             for part in parts]
    return names[0] if len(names) == 1 else names[0] + " + " + names[1]


def entry(build: dict) -> dict:
    cpu_score, frequency = CPU_PART[build["cpu"]]
    return {
        "key": build["key"],
        "pc": build["key"],
        "cpu": build["cpu"],
        "gpu": gpu_label(build["gpus"]),
        "gpuScore": sum(PART_SCORE[g] for g in build["gpus"]),
        "cpuScore": model.cpu_power(cpu_score, frequency),
        "ramScore": sum(PART_SCORE[r] for r in build["rams"]),
        "driveScore": sum(PART_SCORE[d] for d in build["drives"]),
    }


def reference_benchmarks() -> list:
    return [entry(build) for build in BUILDS]


def main() -> int:
    problems = []
    for build in BUILDS:
        gpus = build["gpus"]
        if len(set(gpus)) != 1:
            problems.append("в сборке %s две разные видеокарты: %s" % (build["key"], " + ".join(gpus)))
        if len(build["rams"]) != 2 or len(build["drives"]) != 2:
            problems.append("в сборке %s не два накопителя и не две планки памяти" % build["key"])
    for cap in (240.0, 60.0):
        print("=== потолок %d кадров" % cap)
        previous = None
        previous_gpu = previous_cpu = None
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
            if previous_cpu is not None and item["cpuScore"] > previous_cpu + 0.01:
                note += "  <-- CPU НЕ ПАДАЕТ"
                problems.append("процессор %s сильнее предыдущего" % item["key"])
            previous, previous_gpu, previous_cpu = (result["total"], item["gpuScore"],
                                                    item["cpuScore"])
            print("%-18s %-17s %-19s G %6d P %8.1f M %6d S %6d FPS %3d  %6d  %dx%d%s"
                  % (item["key"], item["cpu"], item["gpu"], item["gpuScore"], item["cpuScore"],
                     item["ramScore"], item["driveScore"], result["achievedFps"],
                     result["total"], width, height, note))
    if problems:
        print("ПРОБЛЕМЫ:")
        for problem in problems:
            print("  " + problem)
        return 1
    print("порядок таблицы строго убывает по счёту, видео и CPU")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
