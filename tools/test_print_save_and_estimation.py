#!/usr/bin/env python3
"""Regression: printed-model save must not serialize Unity structs by
reflection, and print time / filament / mass must follow the real printed
plastic volume (so the scale slider actually changes them)."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
COMP = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component"
ITEM = COMP / "PrintedModelItem.cs"
PRINTER = COMP / "ThreeDPrinter.cs"
MODEL = COMP / "PrintableModel.cs"
APP = COMP / "Software/ThreeDPrinterApp.cs"
PREFAB = ROOT / "Assets/Resources/components/3D Printer.prefab"


def test_no_reflection_serialization_of_unity_structs():
    text = ITEM.read_text(encoding="utf-8")
    # Vector3.normalized returns a Vector3 -> Newtonsoft self referencing loop.
    assert "JToken.FromObject" not in text
    assert "JArray.FromObject" not in text
    assert ".ToObject<Vector3" not in text
    assert ".ToObject<Quaternion" not in text
    assert ".ToObject<int[]>" not in text
    for helper in ("private static JArray ToJson(Vector3 value)",
                   "private static JArray ToJson(Quaternion value)",
                   "private static JArray ToJson(Vector3[] values)",
                   "private static JArray ToJson(int[] values)",
                   "private static Vector3 ReadVector(JToken token, Vector3 fallback)",
                   "private static Quaternion ReadQuaternion(JToken token, Quaternion fallback)",
                   "private static Vector3[] ReadVectors(JToken token)",
                   "private static int[] ReadInts(JToken token)"):
        assert helper in text, helper
    assert "new JArray(value.x, value.y, value.z)" in text
    assert "new JArray(value.x, value.y, value.z, value.w)" in text
    # Legacy object-shaped values still load.
    assert 'token is JObject obj && obj["x"] != null' in text


def test_geometry_measured_from_meshes_not_renderer_bounds():
    text = MODEL.read_text(encoding="utf-8")
    assert "public float GetWorldSolidVolume()" in text
    assert "public float GetWorldSurfaceArea()" in text
    assert "public Vector3 GetWorldBoundsSize()" in text
    assert "Vector3.Dot(a, Vector3.Cross(b, c)) / 6f" in text
    assert "localToWorldMatrix" in text
    # Renderer.bounds is empty for a disabled pending import, so the preview
    # estimate must not depend on it.
    measure = text[text.index("private void EnsureGeometryCache()"):text.index("public Vector3 GetWorldBoundsSize()")]
    assert ".bounds" not in measure


def test_estimation_depends_on_scale_and_plastic_volume():
    text = PRINTER.read_text(encoding="utf-8")
    for token in ("unitToMeter", "infillFraction", "shellThicknessMm", "secondsPerPlasticUnit",
                  "filamentMetersPerPlasticUnit", "complexitySeconds",
                  "public float EstimatePrintedMassKg(PrintableModel model)",
                  "private PrintMetrics ComputeMetrics(PrintableModel model, float fitScale)",
                  "private float ComputeFitScale(PrintableModel model)"):
        assert token in text, token
    assert "metrics.plasticUnits = solid * k * k * k * Mathf.Clamp01(infillFraction) + area * k * k * shellUnits;" in text
    assert "metrics.massKg = Mathf.Clamp(" in text
    # Job cost is measured after the real fit scale is known.
    start = text[text.index("public bool StartPrint(PrintableModel model)"):text.index("public void CancelPrint()")]
    assert start.index("printFitScale = Mathf.Lerp") < start.index("jobMetrics = ComputeMetrics(model, printFitScale);")
    assert "totalSeconds = jobMetrics.seconds;" in start
    assert "requiredFilament = jobMetrics.filamentMeters;" in start
    # Mass measured by the job wins over the bounding-box fallback.
    model = MODEL.read_text(encoding="utf-8")
    assert "float mass = measuredMassKg > 0f ? measuredMassKg : volume * density;" in model


def test_minimum_print_time_lowered_in_code_and_prefab():
    text = PRINTER.read_text(encoding="utf-8")
    assert "minPrintSeconds = 10f" in text
    assert "basePrintSeconds = 20f" in text
    prefab = PREFAB.read_text(encoding="utf-8")
    for key, value in (("minPrintSeconds", "10"), ("basePrintSeconds", "20"),
                       ("unitToMeter", "0.18"), ("infillFraction", "0.15"),
                       ("secondsPerPlasticUnit", "900"), ("filamentMetersPerPlasticUnit", "6"),
                       ("printedDensityKgPerCubicMeter", "1240"),
                       ("minPrintedMassKg", "0.005"), ("maxPrintedMassKg", "25")):
        assert re.search(r"^  %s: %s$" % (key, re.escape(value)), prefab, re.M), key


def test_scale_slider_changes_time_filament_and_mass_numbers():
    # Mirrors ComputeMetrics for the printer's real build volume.
    solid = 1.979029 * 1.8380961 * 1.104413
    area = 2 * (1.979029 * 1.8380961 + 1.979029 * 1.104413 + 1.8380961 * 1.104413)
    unit, infill = 0.18, 0.15
    shell = 0.0008 / unit

    def metrics(k):
        plastic = solid * k ** 3 * infill + area * k * k * shell
        return (max(10.0, min(3600.0, 20.0 + plastic * 900.0)),
                max(0.02, plastic * 6.0),
                min(25.0, max(0.005, plastic * unit ** 3 * 1240.0)))

    previous = None
    for k in (1.0, 0.75, 0.5, 0.25, 0.1, 0.05):
        current = metrics(k)
        if previous is not None:
            assert current[0] < previous[0], (k, current, previous)
            assert current[1] <= previous[1]
            assert current[2] < previous[2]
        previous = current
    assert metrics(0.05)[0] < 30.0, "5% must not take three minutes"
    assert metrics(0.05)[2] <= 0.01, "5% must not weigh kilograms"
    assert 500.0 < metrics(1.0)[0] < 800.0
    assert metrics(1.0)[2] < 25.0, "full volume print must not clamp to max mass"
    assert metrics(1.0)[1] <= 5.0, "full volume print must fit a 5 m spool"


def test_app_shows_scale_aware_time_filament_and_weight():
    text = APP.read_text(encoding="utf-8")
    assert "EstimatePrintSeconds(model)" in text
    assert "EstimatePrintSeconds(model.TriangleCount)" not in text
    assert "EstimatePrintedMassKg(model)" in text
    assert "private static string FormatMass(float kilograms)" in text


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
