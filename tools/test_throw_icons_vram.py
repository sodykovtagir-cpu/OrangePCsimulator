#!/usr/bin/env python3
"""Fast source guards; the final verdict requires a Unity Play Mode smoke test."""
from pathlib import Path
root = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
ray = (root / 'Raycast.cs').read_text()
os = (root / 'PC/Component/Software/OS/OperatingSystem.cs').read_text()
fx = (root / 'GpuArtifacts.cs').read_text()
overclock = (root / 'PC/Component/Software/Overclock.cs').read_text()
assert 'SampleThrowVelocity(currentDrag, point)' in ray
assert 'SampleThrowVelocity(releaseDrag, ray.GetPoint(releaseDrag.distance))' in ray
assert 'throwPower = 3.2f' in ray and 'maxThrowSpeed = 30f' in ray
assert 'aim * Mathf.Max(0f, throwPower) * lightness' in ray
assert 'GridMode() || d.gridTouched || rb.isKinematic' in ray
assert 'rb.drag = d.oldDrag' in ray and 'rb.constraints = d.oldConstrains' in ray
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
print('PASS: energetic throws, colored-monitor mask handling, VRAM effects and neutral OC labels')
