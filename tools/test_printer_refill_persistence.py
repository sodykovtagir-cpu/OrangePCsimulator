#!/usr/bin/env python3
"""Source-contract and numeric regressions. These do not replace Unity play tests."""
from pathlib import Path
import json
import math

ROOT = Path(__file__).resolve().parents[1]
COMP = ROOT / 'Assets/Scripts/Assembly-CSharp/PC/Component'
PRINTER = (COMP / 'ThreeDPrinter.cs').read_text()
ITEM = (COMP / 'PrintedModelItem.cs').read_text()
SAVE = (ROOT / 'Assets/Scripts/Assembly-CSharp/SaveManager.cs').read_text()


def test_short_spool_can_start_and_empty_spool_waits_not_fails():
    start = PRINTER[PRINTER.index('public bool StartPrint(PrintableModel model)'):PRINTER.index('public void CancelPrint()')]
    assert 'Not enough filament' not in start
    assert 'requiredFilament > AttachedSpool.RemainingMeters' not in start
    routine = PRINTER[PRINTER.index('private IEnumerator PrintRoutine()'):PRINTER.index('private void FinishPrint()')]
    assert 'if (AttachedSpool == null || AttachedSpool.IsEmpty)' in routine
    assert 'SetWaitingForFilament(true);' in routine
    assert 'yield return null;\n\t\t\t\t\tcontinue;' in routine
    assert 'Fail(' not in routine
    assert 'step = Mathf.Min(step, AttachedSpool.RemainingMeters /' in routine
    assert 'AttachedSpool.Consume(step * requiredFilament)' in routine
    assert 'Progress01 + step' in routine
    assert 'CanSwapSpool => !IsPrinting || WaitingForFilament || AttachedSpool == null || AttachedSpool.IsEmpty' in PRINTER
    assert 'if (!CanSwapSpool || spool == null)' in PRINTER
    assert 'if (!CanSwapSpool || AttachedSpool == null)' in PRINTER
    detached = PRINTER[PRINTER.index('public void OnSpoolPhysicallyDetached'):PRINTER.index('// ----- Import / scale')]
    assert 'Fail(' not in detached
    assert 'SetWaitingForFilament(true)' in detached


def tick(progress, remaining, required, total, dt):
    if remaining <= 0.0001:
        return progress, remaining
    step = min(1 - progress, dt / max(0.001, total))
    step = min(step, remaining / max(0.000001, required))
    return min(1, progress + step), max(0, remaining - step * required)


def test_three_spools_conserve_material_and_progress_survives_pause_and_save():
    # A job needs more than any single 5 m spool; no artificial job-size cap.
    required, total = 12.5, 100
    p, consumed = 0.0, 0.0
    for capacity in (5.0, 5.0, 5.0):
        remaining = capacity
        while p < 1 and remaining > 0.0001:
            p, remaining = tick(p, remaining, required, total, 0.7)
        consumed += capacity - remaining
        before = p
        for _ in range(100):
            # There is no spool mounted during replacement.
            p, _ = tick(p, 0, required, total, 30)
        assert p == before
        # Numeric snapshot reload, no reconstruction from wall-clock elapsed.
        p = json.loads(json.dumps({'tdpProgress': p}))['tdpProgress']
    assert math.isclose(p, 1, abs_tol=1e-12)
    assert math.isclose(consumed, required, abs_tol=0.0002)


def test_large_frame_never_gives_free_printing_or_negative_filament():
    p, remaining = tick(0, 0.25, 10, 60, 10000)
    assert p == 0.025
    assert remaining == 0
    assert tick(p, remaining, 10, 60, 10000) == (p, remaining)
    # A nearly finished job only consumes the exact remaining fraction.
    p, remaining = tick(0.9, 5, 10, 60, 10000)
    assert p == 1
    assert math.isclose(remaining, 4)


def test_saved_mesh_uses_root_coordinates_not_duplicate_root_pose():
    write = ITEM[ITEM.index('public static void WriteGeometry'):ITEM.index('public override void FromData')]
    assert 'root.transform.worldToLocalMatrix * filters[i].transform.localToWorldMatrix' in write
    assert 'toRoot.MultiplyPoint3x4(vertices[v])' in write
    assert '["pos"] = ToJson(Vector3.zero)' in write
    assert '["scale"] = ToJson(Vector3.one)' in write
    assert 'printedGeometryVersion' in write
    assert '["pos"] = ToJson(filters[i].transform.localPosition)' not in write
    assert 'vertices.Length > 65535' in ITEM
    assert 'IndexFormat.UInt32' in ITEM
    assert 'Id = Main.Instance.GetNewId(this)' in ITEM
    # Example of the old issue: a root at y=1 with scale=.05 became a child
    # at y=1 with scale=.05, doubling translation and squaring its scale.
    y, scale, vertex = 1.0, 0.05, 2.0
    expected = y + scale * vertex
    old = y + scale * (y + scale * vertex)
    restored = y + scale * (0 + 1 * vertex)
    assert restored == expected
    assert old != expected


def test_runtime_prints_have_reliable_load_path_and_are_not_prefab_duplicates():
    assert 'bool runtimePrint = it.spawnId == PrintedModelItem.PrintedSpawnId' in SAVE
    assert 'if (exists || runtimePrint)' in SAVE
    assert 'prefab.AddComponent<PrintedModelItem>()' in SAVE
    assert 'if (item is PrintedModelItem) return false;' in SAVE
    assert 'if (it.spawnId == PrintedModelItem.PrintedSpawnId) return false;' in SAVE


def test_pending_and_active_printer_geometry_and_job_metrics_saved():
    for token in ('tdpGeometry', 'tdpFitScale', 'tdpTotalSeconds', 'tdpRequiredFilament', 'tdpMassKg', 'tdpProgress'):
        assert PRINTER.count('"' + token + '"') >= 2, token
    assert 'PrintedModelItem.WriteGeometry(model.Instance, snapshot)' in PRINTER
    assert 'PrintedModelItem.ReadGeometry(go, snapshot)' in PRINTER
    assert 'StartCoroutine(ResumeLoadedPrint())' in PRINTER
    assert 'PendingModel = model;' in PRINTER
    assert 'yield return PrintRoutine();' in PRINTER
    assert 'RemainingSeconds = totalSeconds * (1f - Progress01)' in PRINTER
    assert 'ApplyJobLook(model)' in PRINTER
    assert 'jobFinish, jobSmoothness, jobGlowColor, jobGlowIntensity' in PRINTER


def test_build_volume_is_nonphysical_but_keeps_size_guide():
    prefab = (ROOT / 'Assets/Resources/components/3D Printer.prefab').read_text()
    volume = prefab.split('--- !u!65 &6706736101266666507')[1].split('--- !u!')[0]
    assert 'm_Enabled: 0' in volume
    assert 'm_Size: {x:' in volume  # User-authored dimensions may change.
    assert 'printVolumeCollider.enabled = false' in PRINTER
    assert 'Vector3 size = printVolumeCollider.size;' in PRINTER


def test_only_wattage_removed_from_printer_ui_not_simulation():
    app = (COMP / 'Software/ThreeDPrinterApp.cs').read_text()
    screen = (COMP / 'ThreeDPrinterScreen.cs').read_text()
    assert 'CurrentWatts.ToString' not in app
    assert 'EstimateFilamentMeters(model)' in app
    assert 'spool.RemainingMeters.ToString' in app
    assert 'Replace filament spool' in app
    assert 'ShowWaitingForFilament' in screen
    assert 'filamentText.gameObject.SetActive(true)' in screen
    assert 'AttachedSpool.Consume(step * requiredFilament)' in PRINTER
    assert 'public float CurrentWatts' in PRINTER


if __name__ == '__main__':
    for name, fn in sorted(globals().copy().items()):
        if name.startswith('test_') and callable(fn):
            fn()
            print('PASS', name)
