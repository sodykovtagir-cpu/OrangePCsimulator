#!/usr/bin/env python3
"""Regression: user-authored 3D printer/filament/app prefabs are wired to logic."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
PRINTER = ROOT / "Assets/Resources/components/3D Printer.prefab"
FILAMENT = ROOT / "Assets/Resources/components/WhiteFilament.prefab"
APP = ROOT / "Assets/Resources/apps/3DPrinter.prefab"
DOWNLOADER = ROOT / "Assets/Resources/apps/Downloader.prefab"
MYDEVICES = ROOT / "Assets/Resources/apps/MyDevices1.prefab"
TAGS = ROOT / "ProjectSettings/TagManager.asset"
APP_SCRIPT = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/ThreeDPrinterApp.cs"
SPOOL_SCRIPT = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/FilamentSpool.cs"
PICKER = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/DevicePicker.cs"

APP_GUID = "913e3e8ae06e4a08a65cfe79c0abcf7e"
APP_COMP = "114300000000000001"
PIVOT = "1381611921864309695"


def test_filament_tag_and_item():
    assert "- Filament" in TAGS.read_text(encoding="utf-8")
    text = FILAMENT.read_text(encoding="utf-8")
    assert "propertyPath: m_TagString" in text and "value: Filament" in text
    assert "spawnId: WhiteFilament" in text
    assert "guid: c30496d6ae6d6f907456e10a94e01ec6" in text  # Item


def test_printer_slot_uses_pivot_and_trigger():
    text = PRINTER.read_text(encoding="utf-8")
    assert f"spoolMount: {{fileID: {PIVOT}}}" in text
    assert "target: Filament" in text
    assert f"insertPos: {{fileID: {PIVOT}}}" in text
    assert "breakForce: 250" in text
    assert "deviceType: 4" in text
    assert "screen: {fileID: 5162813022938634744}" in text


def test_spool_visual_color_and_thresholds():
    text = FILAMENT.read_text(encoding="utf-8")
    for token in ("filamentColor:", "minVisualScale: 0.35", "maxVisualScale: 1", "keepCentered: 1", "lowFilamentMeters:"):
        assert token in text
    # User-authored capacity is preserved.
    assert "capacityMeters: 5" in text
    assert "remainingMeters: 5" in text
    src = SPOOL_SCRIPT.read_text(encoding="utf-8")
    assert "SetColor(Color color)" in src
    assert "OnSlotConnected" in src and "OnSlotDisconnected" in src
    assert "initialVisualScale.y * k" in src and "initialVisualScale.z * k" in src


def test_app_buttons_text_preview_wired():
    text = APP.read_text(encoding="utf-8")
    assert f"guid: {APP_GUID}" in text
    assert f"fileID: {APP_COMP}" in text
    for method in ("Close", "Maximize", "Minimize", "SelectPrinter", "ImportModel", "StartPrint"):
        assert f"m_MethodName: {method}" in text
        assert f"m_Target: {{fileID: {APP_COMP}}}" in text
    for field in ("selectPrinterButton:", "importModelButton:", "startPrintButton:", "infoText:", "previewImage:"):
        assert field in text
    assert "previewImage: {fileID: 718801772354570638}" in text


def test_app_downloader_has_3d_printer_and_fixed_mydevices():
    text = DOWNLOADER.read_text(encoding="utf-8")
    assert f"{{fileID: {APP_COMP}, guid: 73e82dd027ab653439d83f2f8bef9d91, type: 3}}" in text
    assert "{fileID: 114706950818030795, guid: f080a7ee0eaef514184ee75a0fb5d04f, type: 3}" in text
    assert "{fileID: 114706950818030795, guid: 73e82dd027ab653439d83f2f8bef9d91, type: 3}" not in text
    assert "- type: 4" in MYDEVICES.read_text(encoding="utf-8")


def test_app_script_and_nearby_picker():
    src = APP_SCRIPT.read_text(encoding="utf-8")
    for token in ("public void SelectPrinter", "public void ImportModel", "public void StartPrint", "RawImage previewImage", "RenderTexture"):
        assert token in src
    picker = PICKER.read_text(encoding="utf-8")
    assert "bool discoverNearby" in picker
    assert "os.DiscoverDevices()" in picker


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
