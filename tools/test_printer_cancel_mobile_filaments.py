#!/usr/bin/env python3
"""Regression: cancel print handover, printer-driven scale limit, mobile file
picking, hideable system apps and the coloured filament range."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
COMP = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component"
PRINTER = COMP / "ThreeDPrinter.cs"
APP = COMP / "Software/ThreeDPrinterApp.cs"
IMPORTER = COMP / "ThreeDModelImporter.cs"
SPOOL = COMP / "FilamentSpool.cs"
LOOK = COMP / "FilamentLook.cs"
EXPLORER = COMP / "Software/FileManager.cs"
COMPONENTS = ROOT / "Assets/Resources/components"
SHOP = ROOT / "Assets/MonoBehaviour/Shop.asset"
SHOP_DIR = ROOT / "Assets/MonoBehaviour"
TRANSLATE = ROOT / "Assets/Resources/Translate.txt"

COLORS = ["White", "Black", "Gray", "Red", "Orange", "Yellow",
          "Green", "Blue", "Cyan", "Purple", "Pink", "Brown"]


def test_cancel_print_hands_over_partial_model_with_physics():
    text = PRINTER.read_text(encoding="utf-8")
    cancel = text[text.index("public void CancelPrint()"):text.index("private IEnumerator PrintRoutine()")]
    assert "partial.AddFinishedPhysics(printedDensityKgPerCubicMeter, minPrintedMassKg, maxPrintedMassKg," in cancel
    assert "jobMetrics.massKg * Mathf.Max(0.02f, reached)" in cancel
    assert "AttachPrintedItem(partial);" in cancel
    assert "partial.Instance.transform.SetParent(null, true);" in cancel
    assert "ParkCarriage(modelHeight * Mathf.Max(0.02f, reached));" in cancel
    assert "PrintCancelled?.Invoke(partial);" in cancel
    assert "SetState(PrinterState.Idle);" in cancel
    assert "public event Action<PrintableModel> PrintCancelled;" in text


def test_app_button_toggles_start_and_cancel():
    text = APP.read_text(encoding="utf-8")
    assert "if (selectedPrinter.IsPrinting)" in text
    assert "public void CancelPrint()" in text
    assert "selectedPrinter.CancelPrint();" in text
    assert 'Localization.GetText(printing ? "Cancel Print" : "Start Print")' in text
    assert "startPrintButton.interactable = printing || canStart;" in text
    assert "subscribedPrinter.PrintCancelled += OnPrintFinished;" in text
    assert "subscribedPrinter.PrintCancelled -= OnPrintFinished;" in text
    assert "Cancel Print\t" in TRANSLATE.read_text(encoding="utf-8")


def test_scale_limit_comes_from_printer_collider():
    text = PRINTER.read_text(encoding="utf-8")
    assert "public Vector3 PrintVolumeSize" in text
    assert "printVolumeCollider.transform.lossyScale" in text
    assert "private BoxCollider FindVolumeCollider()" in text
    assert 'n.Contains("printvolume")' in text
    assert "if (printVolumeCollider == null) printVolumeCollider = FindVolumeCollider();" in text
    app = APP.read_text(encoding="utf-8")
    preview = app[app.index("private Vector3 GetPreviewMaxSize()"):app.index("private void ApplyPreviewScale()")]
    assert "selectedPrinter.PrintVolumeSize" in preview
    # The printer collider must win over any app-side override.
    assert preview.index("selectedPrinter.PrintVolumeSize") < preview.index("previewMaxBounds")
    printer_prefab = (COMPONENTS / "3D Printer.prefab").read_text(encoding="utf-8")
    ref = re.search(r"printVolumeCollider: \{fileID: (\d+)\}", printer_prefab)
    assert ref and ref.group(1) != "0", "printer prefab must expose its build volume collider"


def test_mobile_picker_uses_mime_types_not_extensions():
    text = IMPORTER.read_text(encoding="utf-8")
    assert "public static string[] PickerFileTypes" in text
    assert "#if UNITY_ANDROID && !UNITY_EDITOR" in text
    assert '"application/octet-stream", "*/*"' in text
    assert "#elif UNITY_IOS && !UNITY_EDITOR" in text
    assert '"public.item", "public.data", "public.content", "public.3d-content"' in text
    printer = PRINTER.read_text(encoding="utf-8")
    assert "ThreeDModelImporter.SupportedExtensions);" not in printer
    assert printer.count("ThreeDModelImporter.PickerFileTypes);") == 2
    assert 'Fail("Unsupported model format");' in printer


def test_system_apps_can_be_hidden():
    text = EXPLORER.read_text(encoding="utf-8")
    assert "hiddenToggle.interactable = !file.isFolder;" in text
    toggle = text[text.index("public void OnValueChangedHidden(bool hidden)"):text.index("public void Cut()")]
    assert "IsProtectedFile" not in toggle
    assert "file.hidden = hidden;" in toggle


def test_filament_finish_and_glow_support():
    look = LOOK.read_text(encoding="utf-8")
    assert "public enum FilamentFinish" in look
    assert "Matte = 0" in look and "Glossy = 1" in look and "Glow = 2" in look
    assert 'EnableKeyword("_EMISSION")' in look
    assert "public static void EnsureGlowLight(" in look
    assert Path(str(LOOK) + ".meta").exists()
    spool = SPOOL.read_text(encoding="utf-8")
    assert "private FilamentFinish finish = FilamentFinish.Glossy;" in spool
    assert "FilamentLook.Apply(targets, filamentColor, Smoothness, EmissionColor);" in spool
    assert 'jObject["filamentFinish"] = (int)finish;' in spool
    # Unity forbids Unity-object construction in field initializers.
    assert not re.search(r"MaterialPropertyBlock\s+\w+\s*=\s*new MaterialPropertyBlock\([^;\n]*\)\s*;", spool)
    model = (COMP / "PrintableModel.cs").read_text(encoding="utf-8")
    assert "public void ApplyColor(Color color, float smoothness, Color emission)" in model


def test_filament_range_prefabs_and_shop_entries():
    expected = []
    for color in COLORS:
        expected.append(("%sFilamentMatte" % color, 0))
        expected.append(("%sFilamentGlossy" % color, 1))
    expected.append(("GlowFilament", 2))
    assert len(expected) == 25
    shop = SHOP.read_text(encoding="utf-8")
    assert "pageName: Filament" in shop
    page = shop[shop.index("  - pageName: Filament"):]
    entries = re.findall(r"guid: ([0-9a-f]{32})", page)
    assert len(entries) == 25, len(entries)
    translate = TRANSLATE.read_text(encoding="utf-8")
    for name, finish in expected:
        prefab = COMPONENTS / (name + ".prefab")
        assert prefab.exists(), name
        assert Path(str(prefab) + ".meta").exists(), name
        text = prefab.read_text(encoding="utf-8")
        assert "spawnId: %s\n" % name in text
        assert "finish: %d\n" % finish in text
        assert "capacityMeters: 5" in text
        assert "value: Filament" in text
        item = SHOP_DIR / (name + ".asset")
        assert item.exists(), name
        assert Path(str(item) + ".meta").exists(), name
        assert "price:" in item.read_text(encoding="utf-8")
    glow = (COMPONENTS / "GlowFilament.prefab").read_text(encoding="utf-8")
    assert "glowIntensity: 2.4" in glow
    assert "glowLight: 1" in glow
    assert "Glow Filament\t" in translate
    assert "White Filament Matte\t" in translate
    assert "Black Filament Glossy\t" in translate


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
