#!/usr/bin/env python3
"""Check shop descriptions against actual ready-build parts and physical slots."""
from pathlib import Path
import re

import add_ready_build_i18n as i18n
import generate_ready_builds as generator
from unity_asset_tool import PrefabResolver

ROOT = Path(__file__).resolve().parents[1]
COMPONENTS = ROOT / "Assets/Resources/components"
SHOP = ROOT / "Assets/MonoBehaviour/Ready"

rows = {}
for line in (ROOT / "Assets/Resources/Translate.txt").read_text(encoding="utf-8").splitlines():
    if "\t" in line:
        cells = line.split("\t")
        rows[cells[0]] = cells[1:]

# GPU slots belong to a physical board or mining frame, not merely to a case.
mini = PrefabResolver(COMPONENTS / "Mini_ITX.prefab", ROOT)
assert not any(slot["target"] == "GPU" for slot in mini.slots)
frame_capacity = {
    name: sum(s["target"] == "GPU" for s in
              PrefabResolver(COMPONENTS / (name + ".prefab"), ROOT).slots)
    for name in ("Miner", "BigMiner")
}
assert frame_capacity == {"Miner": 8, "BigMiner": 16}

GPU = {
    "GTX1060": "GTX 1060", "RTX4080": "RTX 4080",
    "RTX4080Ti": "RTX 4080 Ti", "Titan V": "Titan V",
    "RTX5090": "RTX 5090", "GT1030": "GT 1030",
    "RX570": "RX 570", "RTX3080": "RTX 3080",
}
SUPPLY = {
    "PSU 300W": "300 W", "PSU 500W": "500 W",
    "PSU 1kW": "1 kW", "PSU 1.1kW": "1.1 kW", "PSU 2kW": "2 kW",
}


def name(part):
    return Path(part.prefab_path).stem


expected_rows = i18n.build_strings()
for spec in generator.BUILDS:
    board = [name(p) for p in spec.parts if p.slot_target == "Motherboard"]
    assert len(board) == 1, spec.key
    gpus = [p for p in spec.parts if p.slot_target == "GPU"]
    power = [name(p) for p in spec.parts if p.slot_target == "Supply"]
    assert power and len(set(power)) == 1, spec.key
    assert spec.description in rows, (spec.key, spec.description)
    assert rows[spec.description] == expected_rows[spec.description], spec.key
    assert len(rows[spec.description]) == len(i18n.LANGS), spec.key

    # ShopItem references the translated description key, not a stale literal.
    item = (SHOP / (spec.key + ".asset")).read_text(encoding="utf-8")
    assert "translateDescription: 1" in item, spec.key
    assert re.search(r"^  description: ['\"]?" + re.escape(spec.description) + r"['\"]?$",
                     item, re.M), spec.key
    prefab = (COMPONENTS / "ready" / (spec.key + ".prefab")).read_text(encoding="utf-8")
    assert prefab.count("    slotTarget: GPU") == len(gpus), spec.key
    case_name = Path(spec.case).stem

    if spec.page == "Ready Miner":
        assert board == ["Mini_ITX"] and case_name in frame_capacity, spec.key
        assert all(p.host is None for p in gpus), spec.key
        assert len(gpus) <= frame_capacity[case_name], spec.key
        assert len(set(name(p) for p in gpus)) == 1, spec.key
        expected_gpu = f"{len(gpus)} x {GPU[name(gpus[0])]}"
        gpu_segment = 0
    else:
        # ITX desktop PCs have no GPU slot; mining rigs are the exception
        # because their cards are installed on the separate frame.
        if board == ["Mini_ITX"]:
            assert case_name.startswith("Case_ITX") and not gpus, spec.key
            assert "    slotTarget: GPU" not in prefab, spec.key
        else:
            assert gpus, spec.key
        expected_gpu = " + ".join(GPU[name(p)] for p in gpus)
        gpu_segment = 2

    expected_power = ((str(len(power)) + " x ") if len(power) > 1 else "") + SUPPLY[power[0]]
    for lang, description in zip(i18n.LANGS, rows[spec.description]):
        segments = description.split(" / ")
        if not gpus:
            expected = {"RU": "Встроенная графика", "UA": "Вбудована графіка"}.get(
                lang, "Integrated graphics")
        else:
            expected = expected_gpu
        assert segments[gpu_segment] == expected, (spec.key, lang, "GPU", segments[gpu_segment], expected)
        assert expected_power in segments, (spec.key, lang, "PSU", segments, expected_power)

print("PASS: Office ITX has no discrete GPU, rig GPUs occupy frame slots, and all 42 shop specs match GPU/PSU parts")
