#!/usr/bin/env python3
"""Regression: deterministic toolpath, vertical axis, finished physics, screen bar."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
COMP = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component"
PRINTER = COMP / "ThreeDPrinter.cs"
MODEL = COMP / "PrintableModel.cs"
SCREEN = COMP / "ThreeDPrinterScreen.cs"
PREFAB = ROOT / "Assets/Resources/components/3D Printer.prefab"


def test_deterministic_layer_toolpath_not_random():
    text = PRINTER.read_text(encoding="utf-8")
    for token in ("BuildToolpath", "SampleToolpath", "perimeterPasses", "infillLinesPerLayer",
                  "toolPathCumulative", "CollectVerticesInSpace"):
        assert token in text, token
    assert "Mathf.PingPong" not in text
    assert "Random." not in text
    # Serpentine infill alternates direction.
    assert "(i & 1) == 0 ? lx0 : lx1" in text


def test_vertical_axis_moves_with_print_and_bed_auto_direction():
    text = PRINTER.read_text(encoding="utf-8")
    assert "VerticalDirection" in text
    assert "axisY == printBed ? -1f : 1f" in text
    assert "float printedHeight = modelHeight * Mathf.Clamp01(progress);" in text
    assert "axisY.localPosition = axisYRest + Vector3.up * (VerticalDirection * (printedHeight + nozzleHeightOffset));" in text
    # If the bed itself is used as X/Z axis, invert so the model stays under nozzle.
    assert "motionX == printBed ? -1f : 1f" in text
    assert "axisZ == printBed ? -1f : 1f" in text


def test_finished_model_gets_rigidbody_and_island_box_colliders():
    text = MODEL.read_text(encoding="utf-8")
    assert "public void AddFinishedPhysics()" in text
    assert "AddComponent<Rigidbody>()" in text
    assert "AddComponent<BoxCollider>()" in text
    assert "AddIslandColliders" in text
    assert "weldMap" in text and "union(a, b)" in text
    printer = PRINTER.read_text(encoding="utf-8")
    assert "finished.AddFinishedPhysics(printedDensityKgPerCubicMeter, minPrintedMassKg, maxPrintedMassKg, jobMetrics.massKg);" in printer
    assert "finished.Instance.transform.SetParent(null, true);" in printer


def test_screen_progress_bar_is_horizontal_filled():
    text = SCREEN.read_text(encoding="utf-8")
    assert "progressFill.type = Image.Type.Filled;" in text
    assert "progressFill.fillMethod = Image.FillMethod.Horizontal;" in text
    assert "progressFill.fillAmount = Mathf.Clamp01(progress);" in text
    prefab = PREFAB.read_text(encoding="utf-8")
    m = re.search(r"--- !u!114 &2859272112378532549\nMonoBehaviour:.*?(?=\n--- |\Z)", prefab, re.S)
    assert m, "progress fill image missing"
    doc = m.group(0)
    assert "m_Type: 3" in doc
    assert "m_FillMethod: 0" in doc
    assert "m_FillAmount: 0" in doc
    assert "progressFill: {fileID: 2859272112378532549}" in prefab


def test_printer_prefab_exposes_toolpath_settings():
    prefab = PREFAB.read_text(encoding="utf-8")
    for token in ("perimeterPasses: 2", "infillLinesPerLayer: 12", "nozzleHeightOffset: 0.01",
                  "axisYDirection: 0", "toolpathScale: 1"):
        assert token in prefab, token


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
