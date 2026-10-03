"""Модель очков 3DMork на Python - точный порт формул из ThreeDMork.cs.

Нужна, чтобы эталонная таблица, превью и тесты считались одной и той же
формулой, а не расходились с игрой. Значения железа берутся из ассетов:
    GPU   RTX 5090 = 13500, RTX 4080 Ti = 11000, RTX 3080 Ti = 7000 ...
    CPU   i7-14700K = 3.4 ГГц * 4250 = 14450
    RAM   64 ГБ = 10000 (64 ГБ RGB = 12000)
    Drive SSD 16 ТБ = 9000
"""
from __future__ import annotations

import math
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
SCRIPT = os.path.join(REPO, "Assets", "Scripts", "Assembly-CSharp", "PC", "Component",
                      "Software", "ThreeDMork.cs")

REF_GPU = 27000.0
REF_CPU = 14450.0
REF_RAM = 24000.0
REF_DRIVE = 18000.0

GRAPHICS_CEILING = 160000.0
PHYSICS_CEILING = 50000.0
MEMORY_CEILING = 32000.0
FPS_SCORE_FACTOR = 130.0
WEIGHTS = (0.60, 0.24, 0.10, 0.06)
REFERENCE_FPS_CAP = 240.0
REFERENCE_PIXELS = 1280.0 * 720.0
RESOLUTIONS = ((1920, 1080), (1600, 900), (1280, 720), (960, 540), (640, 360))


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def machine_fps(gpu: int, cpu: float, ram: int) -> float:
    """Сколько кадров выдаёт ПК сам по себе (на 1280x720)."""
    gpu_power = clamp01(gpu / REF_GPU)
    cpu_power = clamp01(cpu / REF_CPU)
    ram_power = clamp01(ram / REF_RAM)
    throughput = 18.0 + 780.0 * gpu_power
    return throughput * (0.55 + 0.45 * cpu_power) * (0.70 + 0.30 * ram_power)


def scores(gpu: int, cpu: float, ram: int, drive: int, fps_cap: float) -> dict:
    """Очки по категориям, общий счёт и FPS - порт ComputeScores."""
    gpu_power = clamp01(gpu / REF_GPU)
    cpu_power = clamp01(cpu / REF_CPU)
    ram_power = clamp01(ram / REF_RAM)
    drive_power = clamp01(drive / REF_DRIVE)

    graphics = round(GRAPHICS_CEILING * gpu_power ** 1.10 * (0.82 + 0.18 * cpu_power))
    physics = round(PHYSICS_CEILING * cpu_power ** 1.05 * (0.85 + 0.15 * ram_power))
    memory = round(MEMORY_CEILING * ram_power ** 1.05 * (0.85 + 0.15 * drive_power))
    raw_fps = machine_fps(gpu, cpu, ram)
    achieved = max(1, round(min(raw_fps, fps_cap)))
    fps = round(achieved * FPS_SCORE_FACTOR)
    total = max(1, round(graphics * WEIGHTS[0] + physics * WEIGHTS[1]
                         + memory * WEIGHTS[2] + fps * WEIGHTS[3]))
    return {"graphics": graphics, "physics": physics, "memory": memory, "fps": fps,
            "total": total, "machineFps": raw_fps, "achievedFps": achieved}


def render_resolution(gpu: int, cpu: float, ram: int, fps_cap: float) -> tuple:
    """Разрешение рендера теста - порт PickRenderResolution."""
    raw = machine_fps(gpu, cpu, ram)
    for width, height in RESOLUTIONS:
        if raw * (REFERENCE_PIXELS / (width * float(height))) >= fps_cap:
            return (width, height)
    return RESOLUTIONS[-1]


def reference_benchmarks() -> list:
    """Эталонные машины прямо из ThreeDMork.cs, в порядке объявления."""
    text = open(SCRIPT, encoding="utf-8").read()
    block = re.search(r"ReferenceBenchmarks =\s*\{(.*?)\n\t\t\};", text, re.S)
    assert block, "ReferenceBenchmarks not found in " + SCRIPT
    rows = []
    for match in re.finditer(
            r'new LeaderboardEntry\("([^"]+)", "([^"]+)", "([^"]+)", '
            r'(\d+), (\d+)f, (\d+), (\d+)\)', block.group(1)):
        rows.append({
            "pc": match.group(1), "cpu": match.group(2), "gpu": match.group(3),
            "gpuScore": int(match.group(4)), "cpuScore": float(match.group(5)),
            "ramScore": int(match.group(6)), "driveScore": int(match.group(7)),
        })
    assert len(rows) == 8, "expected 8 reference machines, got %d" % len(rows)
    return rows


if __name__ == "__main__":
    for entry in reference_benchmarks():
        for cap in (60.0, 240.0):
            result = scores(entry["gpuScore"], entry["cpuScore"], entry["ramScore"],
                            entry["driveScore"], cap)
            print("cap %3d  %-20s G%7d P%6d M%6d FPS%5d  TOTAL %7d  %s"
                  % (cap, entry["pc"], result["graphics"], result["physics"],
                     result["memory"], result["achievedFps"], result["total"],
                     "%dx%d" % render_resolution(entry["gpuScore"], entry["cpuScore"],
                                                entry["ramScore"], cap)))
