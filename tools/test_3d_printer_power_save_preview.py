#!/usr/bin/env python3
"""Regression: printer power, saved prints, carriage parking, app reset/3D scale preview."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
COMP = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component"
PRINTER = COMP / "ThreeDPrinter.cs"
APP = COMP / "Software/ThreeDPrinterApp.cs"
PRINTED_ITEM = COMP / "PrintedModelItem.cs"
PRINTED_PREFAB = ROOT / "Assets/Resources/components/PrintedModel.prefab"
PRINTER_PREFAB = ROOT / "Assets/Resources/components/3D Printer.prefab"
APP_PREFAB = ROOT / "Assets/Resources/apps/3DPrinter.prefab"
GRID = ROOT / "Assets/Scripts/Assembly-CSharp/ElectricityGrid.cs"
PORT = COMP / "Cabling/CablePort.cs"
ITEM = ROOT / "Assets/Scripts/Assembly-CSharp/Item.cs"


def test_printer_power_idle_and_job_dependent():
    text = PRINTER.read_text(encoding="utf-8")
    for token in ("idleWatts", "minPrintWatts", "maxPrintWatts", "public float CurrentWatts",
                  "public bool IsPowered", "JobLoad01", "PortKind.PrinterPower", "Printer AC input"):
        assert token in text, token
    assert "if (!IsPrinting) return Mathf.Max(0f, idleWatts);" in text
    assert "Mathf.Lerp(Mathf.Max(0f, minPrintWatts), Mathf.Max(minPrintWatts, maxPrintWatts), JobLoad01)" in text
    grid = GRID.read_text(encoding="utf-8")
    assert "FindObjectsOfType<PC.Component.ThreeDPrinter>()" in grid
    assert "printer.CurrentWatts" in grid
    port = PORT.read_text(encoding="utf-8")
    assert "PrinterPower" in port
    assert "opposite.kind == PortKind.PrinterPower" in port


def test_slower_print_and_carriage_parking():
    text = PRINTER.read_text(encoding="utf-8")
    assert "basePrintSeconds = 20f" in text
    assert "minPrintSeconds = 10f" in text
    assert "grabClearance" in text
    assert "ParkCarriage()" in text
    assert "VerticalDirection * (printedHeight + grabClearance)" in text
    # Finish resets the printer/screen instead of leaving a Finished screen up.
    finish = text[text.index("private void FinishPrint()"):text.index("private void AttachPrintedItem")]
    assert "SetState(PrinterState.Idle);" in finish
    assert "UpdateScreen();" in finish
    prefab = PRINTER_PREFAB.read_text(encoding="utf-8")
    assert "minPrintSeconds: 10" in prefab
    assert "basePrintSeconds: 20" in prefab
    assert "grabClearance: 0.08" in prefab


def test_finished_prints_are_saved_items_named_like_file():
    text = PRINTED_ITEM.read_text(encoding="utf-8")
    for token in ("class PrintedModelItem : Item, ISave", "PrintedSpawnId", "printedParts",
                  "printedScale", "printedMass", "public override string GetInfo()", "modelName"):
        assert token in text, token
    assert PRINTED_PREFAB.exists()
    assert "spawnId: PrintedModel" in PRINTED_PREFAB.read_text(encoding="utf-8")
    item = ITEM.read_text(encoding="utf-8")
    assert "protected set => info = value;" in item
    printer = PRINTER.read_text(encoding="utf-8")
    assert "AttachPrintedItem(finished);" in printer
    assert "item.Initialize(model.Name, color," in printer


def test_lighter_finished_mass():
    model = (COMP / "PrintableModel.cs").read_text(encoding="utf-8")
    assert "AddFinishedPhysics(float densityKgPerCubicMeter, float minMassKg, float maxMassKg)" in model
    assert "GetWorldBoundsVolume(root)" in model
    assert "float mass = measuredMassKg > 0f ? measuredMassKg : volume * density;" in model
    assert "rb.mass = Mathf.Clamp(mass, minMass, maxMass);" in model
    printer = PRINTER.read_text(encoding="utf-8")
    assert "printedDensityKgPerCubicMeter = 1240f" in printer
    assert "maxPrintedMassKg = 25f" in printer


def test_app_resets_and_has_3d_scale_preview():
    text = APP.read_text(encoding="utf-8")
    for token in ("public void ResetAppState()", "OnPrintFinished", "public override void Close()",
                  "public override void OnSystemStop()", "Slider scaleSlider", "BoxCollider previewMaxBounds",
                  "PreviewBuildPlate", "LightType.Directional", "fieldOfView", "ApplyPreviewScale"):
        assert token in text, token
    assert "scaleSlider.onValueChanged.AddListener(OnScaleSlider);" in text
    assert "selectedPrinter.SetPrintScale01(scaleSlider.value);" in text
    prefab = APP_PREFAB.read_text(encoding="utf-8")
    # The slider is authored in Unity by hand; only the wiring is asserted.
    assert "m_Name: Slider" in prefab
    ref = re.search(r"scaleSlider: \{fileID: (\d+)\}", prefab)
    assert ref and ref.group(1) != "0", "scaleSlider must be assigned"
    slider = re.search(r"--- !u!114 &" + ref.group(1) + r"\nMonoBehaviour:.*?(?=\n--- |\Z)", prefab, re.S)
    assert slider, "slider component missing"
    assert "m_FillRect:" in slider.group(0) and "m_HandleRect:" in slider.group(0)


def test_extruder_pivot_created():
    text = PRINTER.read_text(encoding="utf-8")
    assert "EnsureExtruderPivot()" in text
    assert "Extruder Pivot" in text
    assert "axisX.SetParent(pivot.transform, true);" in text
    assert "pivot.transform.position = bounds.center;" in text


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
