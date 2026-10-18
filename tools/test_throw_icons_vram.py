#!/usr/bin/env python3
"""Fast source guards; the final verdict requires a Unity Play Mode smoke test."""
from pathlib import Path
root = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
ray = (root / 'Raycast.cs').read_text()
os = (root / 'PC/Component/Software/OS/OperatingSystem.cs').read_text()
fx = (root / 'GpuArtifacts.cs').read_text()
overclock = (root / 'PC/Component/Software/Overclock.cs').read_text()
# Match the Rebirth release path: keep the physics body's natural velocity,
# restore its drag/constraints, then detach the spring. Grid snaps still stop.
release = ray.split('public void End()')[1].split('spring.connectedBody = null;')[0]
assert 'SampleThrowVelocity' not in ray and 'throwPower' not in ray
assert 'aimVelocity' not in ray and 'lastFlickVelocity' not in ray
assert 'GridMode() || d.gridTouched || rb.isKinematic' in release
assert release.count('rb.velocity =') == 1 and 'rb.velocity = Vector3.zero;' in release
assert 'rb.drag = d.oldDrag' in release and 'rb.constraints = d.oldConstrains' in release
boot = os.split('private IEnumerator Boot()')[1].split('// Wait for canvas/iconParent')[0]
assert boot.index('ApplyBrand();') < boot.index('OverclockBoot.Enabled(autorun)')
fault = os.split('public override void Fault()')[1].split('public void StopProcess()')[0]
assert 'startupLabel.enabled = false' in fault and 'brandLogo.enabled = false' in fault
assert 'monitorIconPositions.Clear()' in os
assert 'FitOverflow(monitorIconPositions, size, padding)' in os
assert 'var visiblePositions = fullscreen ? fullscreenIconPositions : monitorIconPositions' in os
assert '"Monitor 2("' in os and 'mask.enabled = !coloredWorldMonitor' in os
assert 'windowMask.enabled = !coloredWorldMonitor' in os
assert 'maskGraphic.enabled = !coloredWorldMonitor' in os
assert 'clip.enabled = !coloredWorldMonitor' in fx
assert 'PersistIconPositions();' not in os.split('private void UpdateDesktopIconView(')[1].split('internal void RequestFileRefresh()')[0]
assert 'typeof(RectMask2D)' in fx and 'viewport.SetParent(screen, false)' in fx
assert 'viewport.SetAsLastSibling()' in fx
assert 'overrideSorting = true;' not in fx and 'sortingOrder = ArtifactSortingOrder;' not in fx
assert 'image.raycastTarget = false' in fx and 'FilterMode.Point' in fx
for kind in ('Speckles', 'BlueGlyphs', 'GreenRows', 'ColorBars', 'MemoryTiles', 'TornBands'):
    assert f'{kind}(strength)' in fx
assert 'System.Array.Clear(pixels, 0, pixels.Length)' in fx
assert 'texture.SetPixels32(pixels)' in fx
assert 'if (now < nextRefresh) return;' in fx
assert 'ProfileName(profile)' in overclock and '"Extreme"' in overclock
assert 'will BSOD' not in overclock and 'next boot!' not in overclock
print('PASS: vanilla release physics, bootloop startup cleanup, monitor UI and VRAM effects')
