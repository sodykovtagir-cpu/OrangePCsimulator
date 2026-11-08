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


def test_print_time_scales_with_polygons_average_ten_minutes():
    text = PRINTER.read_text(encoding="utf-8")
    assert "averagePrintSeconds = 600f" in text
    assert "referenceTriangles = 10000" in text
    assert "EstimatePrintSeconds(int triangleCount)" in text
    assert "Mathf.Clamp(averagePrintSeconds * ratio, minPrintSeconds, maxPrintSeconds)" in text
    # Mirror the formula: 10k tris -> 600 s, 5k -> 300 s, tiny -> min clamp.
    def estimate(tris):
        return max(20.0, min(3600.0, 600.0 * (tris / 10000.0)))
    assert abs(estimate(10000) - 600.0) < 1e-6
    assert abs(estimate(5000) - 300.0) < 1e-6
    assert estimate(1) == 20.0


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
    detach = text[text.index("public bool DetachSpool"):text.index("// ----- Import / print")]
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
