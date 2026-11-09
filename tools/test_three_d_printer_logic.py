#!/usr/bin/env python3
"""Source-level checks for the logic-only 3D printer scaffolding."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
COMP = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component"
PRINTER = COMP / "ThreeDPrinter.cs"
SPOOL = COMP / "FilamentSpool.cs"
SCREEN = COMP / "ThreeDPrinterScreen.cs"
IMPORTER = COMP / "ThreeDModelImporter.cs"
MODEL = COMP / "PrintableModel.cs"


def test_files_and_meta_exist():
    for path in (PRINTER, SPOOL, SCREEN, IMPORTER, MODEL):
        assert path.exists(), path
        meta = path.with_suffix(path.suffix + ".meta")
        assert meta.exists(), meta
        assert re.search(r"guid: [0-9a-f]{32}", meta.read_text(encoding="utf-8"))


def test_print_time_scales_with_printed_plastic_not_just_polygons():
    text = PRINTER.read_text(encoding="utf-8")
    assert "EstimatePrintSeconds(PrintableModel model)" in text
    assert "metrics.plasticUnits * secondsPerPlasticUnit" in text
    assert "solid * k * k * k * Mathf.Clamp01(infillFraction) + area * k * k * shellUnits" in text
    assert "minPrintSeconds = 10f" in text
    assert "basePrintSeconds = 20f" in text
    # The scale slider must reach the estimate, not only the visual wrapper.
    assert "previewMetricsScale = PrintScale01;" in text
    assert "jobMetrics = ComputeMetrics(model, printFitScale);" in text

    # Same model, different slider values -> different time, filament and mass.
    solid, area, unit = 4.017, 15.71, 0.18
    shell = 0.0008 / unit

    def metrics(k):
        plastic = solid * k ** 3 * 0.15 + area * k * k * shell
        return (max(10.0, 20.0 + plastic * 900.0),
                max(0.02, plastic * 6.0),
                min(25.0, max(0.005, plastic * unit ** 3 * 1240.0)))

    full, half, tiny = metrics(1.0), metrics(0.5), metrics(0.05)
    assert full[0] > half[0] > tiny[0]
    assert full[1] > half[1] >= tiny[1]
    assert full[2] > half[2] > tiny[2]
    # A 5% print is quick and light, a full-volume print is about ten minutes.
    assert tiny[0] < 30.0
    assert tiny[2] <= 0.01
    assert 500.0 < full[0] < 800.0
    assert full[2] < 25.0


def test_spool_one_meter_and_gradual_consumption():
    spool = SPOOL.read_text(encoding="utf-8")
    assert "capacityMeters = 1f" in spool
    assert "remainingMeters = 1f" in spool
    assert "TryConsume(float meters)" in spool
    printer = PRINTER.read_text(encoding="utf-8")
    assert "AttachedSpool.Consume(consumed)" in printer
    assert "deltaFilament = (progress - lastProgress) * requiredFilament" in printer
    assert "filamentPerTriangle = 0.00008f" in printer
    # 10k triangles uses 0.8 m, leaving filament on a 1 m spool.
    assert 10000 * 0.00008 <= 1.0


def test_spool_swap_blocked_while_printing():
    text = PRINTER.read_text(encoding="utf-8")
    attach = text[text.index("public bool AttachSpool"):text.index("public bool DetachSpool")]
    detach = text[text.index("public bool DetachSpool"):text.index("// ----- Import / scale")]
    assert "if (IsPrinting || spool == null) return false;" in attach
    assert "if (IsPrinting || AttachedSpool == null) return false;" in detach


def test_screen_shows_time_model_filament():
    text = SCREEN.read_text(encoding="utf-8")
    for token in ("modelNameText", "timeText", "filamentText", "ShowPrinting", "FormatTime", "FormatFilament"):
        assert token in text
    printer = PRINTER.read_text(encoding="utf-8")
    assert "screen.ShowPrinting(modelName" in printer
    assert "RemainingSeconds" in printer


def test_axes_and_model_hooks_serialized():
    text = PRINTER.read_text(encoding="utf-8")
    for token in ("[SerializeField] private Transform axisX", "[SerializeField] private Transform axisY",
                  "[SerializeField] private Transform axisZ", "spoolMount", "printBed", "AnimateAxes"):
        assert token in text


def test_runtime_import_formats_and_no_editor_dependency():
    text = IMPORTER.read_text(encoding="utf-8")
    for ext in (".fbx", ".glb", ".gltf", ".obj", ".stl", ".ply", ".3mf"):
        assert ext in text
    assert "using UnityEditor" not in text
    assert "ParseFbx" in text and "ParseGltf" in text
    assert "public static PrintableModel Import(string path" in text


def test_import_does_not_overwrite_model_localscale():
    text = PRINTER.read_text(encoding="utf-8")
    assert "printProgressRoot" in text
    assert "model.Instance.transform.SetParent(printProgressRoot.transform, false)" in text
    # The wrapper grows, not the imported model's own localScale.
    assert "model.Instance.transform.localScale" not in text


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
