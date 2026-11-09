#!/usr/bin/env python3
"""Regression: print volume limit, bed/extruder kinematics, no physics while printing."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
PRINTER = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/ThreeDPrinter.cs"
PREFAB = ROOT / "Assets/Resources/components/3D Printer.prefab"


def test_print_volume_limit_and_autofit():
    text = PRINTER.read_text(encoding="utf-8")
    for token in ("maxPrintSize", "autoFitModel", "rejectOversizedModels", "printFitScale",
                  "Model too large for print volume"):
        assert token in text, token
    assert "printProgressRoot.transform.localScale = new Vector3(printFitScale, y, printFitScale);" in text
    prefab = PREFAB.read_text(encoding="utf-8")
    assert "maxPrintSize: {x: 0.8, y: 0.8, z: 0.8}" in prefab
    assert "autoFitModel: 1" in prefab


def test_extruder_travels_along_beam_local_axis():
    text = PRINTER.read_text(encoding="utf-8")
    assert "CacheExtruderAxis()" in text
    assert "Transform beam = mover.parent;" in text
    assert "extruderTravelAxis" in text
    assert "extruderTravelLimit" in text
    assert "motionX.localPosition = motionXRest + extruderTravelAxis * x;" in text
    # Bed forward/back is inverted when the bed itself is the Z axis.
    assert "axisZ == printBed ? -1f : 1f" in text


def test_no_collider_or_rigidbody_while_printing():
    text = PRINTER.read_text(encoding="utf-8")
    assert "StripPhysicsForPrint(model.Instance);" in text
    assert "private static void StripPhysicsForPrint(GameObject root)" in text
    assert "GetComponentsInChildren<Collider>(true)" in text
    assert "GetComponentsInChildren<Rigidbody>(true)" in text
    assert "colliders[i].enabled = false;" in text
    assert "bodies[i].detectCollisions = false;" in text
    # Physics is only added after the print finishes.
    finish = text[text.index("private void FinishPrint()"):text.index("private void Fail(")]
    assert "finished.AddFinishedPhysics(printedDensityKgPerCubicMeter, minPrintedMassKg, maxPrintedMassKg, jobMetrics.massKg);" in finish
    start = text[text.index("public bool StartPrint(PrintableModel model)"):text.index("public void CancelPrint()")]
    assert "AddFinishedPhysics" not in start


def test_progress_fill_prefab_still_filled():
    prefab = PREFAB.read_text(encoding="utf-8")
    m = re.search(r"--- !u!114 &2859272112378532549\nMonoBehaviour:.*?(?=\n--- |\Z)", prefab, re.S)
    assert m
    doc = m.group(0)
    assert "m_Type: 3" in doc
    assert "m_FillMethod: 0" in doc


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
