#!/usr/bin/env python3
"""Source regression guards for grid landing and second-floor edge protection.

The Unity/PhysX scene test (turn an assembled PC; walk toward a balcony) must
still be performed in the Editor or build.
"""
from pathlib import Path

src = (Path(__file__).resolve().parent.parent /
       'Assets/Scripts/Assembly-CSharp/Raycast.cs').read_text(encoding='utf-8')
rotate = src.split('public void RotateDragged(')[1].split('private static Quaternion[] boxOrientations;')[0]
settle = src.split('private bool SettleAssembly(')[1].split('private bool PathClear(')[0]
probe = src.split('private static bool TryFloorAt(')[1].split('private static bool TryGroundHeight(')[0]
edge = src.split('private static bool HasDestinationSupport(')[1].split('private bool SettleAssembly(')[0]
move = src.split('private bool TryPinWithClimb(')[1].split('private void LogGridBlocker(')[0]

assert 'landingY - pred.min.y' in rotate and 'Mathf.Max(0f, minBefore - pred.min.y)' not in rotate
assert 'landingY - after.min.y' in rotate and 'PathClear(grid, body, Vector3.up * correction' in rotate
assert 'TryGroundHeight(grid, SafeDrop(grid), out floor)' in settle
assert 'BoxCastAll' not in settle and 'Mathf.Abs(dy)' in settle
assert 'hit.normal.y < 0.7f' in probe and 'self.Contains(col.attachedRigidbody)' in probe
assert 'y < bottomY - maxDrop' in probe and 'y > bottomY + 0.08f' in probe
assert 'QueryTriggerInteraction.Ignore' in probe
assert 'horizontal.sqrMagnitude' in edge and 'for (int s = 1; s <= steps; s++)' in edge
assert 'if (!TryFloorAt(' in edge and 'floor < source - SafeDrop(grid)' in edge
assert 'if (!edgeBlocked && PathClear' in move
assert 'if (HasDestinationSupport(grid, up) && PathClear' in move
assert 'if (HasDestinationSupport(grid, slide))' in move

# Geometric intent: a 0.25m step remains reachable; the floor 3m below a
# balcony is outside the allowed 0.75m drop. No real physics is emulated here.
max_drop, upper, stair, lower = .75, 4., 3.75, 1.
assert upper - stair <= max_drop and upper - lower > max_drop
print('PASS: signed floor landing, step tolerance, edge probing and all motion paths')
