#!/usr/bin/env python3
"""Source guards for wired-case drag. Unity physics playback is still required."""
from pathlib import Path

scripts = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
grid = (scripts / 'PlacementGrid.cs').read_text()
ray = (scripts / 'Raycast.cs').read_text()
rope = (scripts / 'PC/Component/Cabling/CableItem.cs').read_text()
impact = (scripts / 'Impact.cs').read_text()

# Collect PC parts through their slot joints, but never walk through a plug
# into a rope, monitor, outlet or a second PC. Nor may loose cables become
# protected "internal" joints with infinite breakForce on a case drag.
assembly = grid[grid.index('private static bool IsCableBody('):
                grid.index('public void OnDragEnded()')]
assert 'if (IsCableBody(start)) return new[] { start };' in assembly
assert 'IsCableBody(a) || IsCableBody(b)' in assembly
assert '!IsCableBody(children[i])' in assembly
protect = grid[grid.index('private void ProtectAssemblyJoints()'):
               grid.index('private void RestoreCaseJoints()')]
assert 'js[j].GetComponentInParent<PC.Component.Cabling.CableItem>() == null' in protect
assert 'j.breakForce = Mathf.Infinity;' in protect  # internal slots remain protected

# No permanent room scan or force modification: find attached cables once on
# case pickup and check actual physical path only while a case is being held.
assert 'if (hitRb.GetComponent<Case>() != null)' in ray
assert 'cable.HasPortOnDraggedAssembly(hitRb.transform)' in ray
assert 'if (drag == null || drag.wiredCables == null || spring == null ||' in ray
assert 'cable.ReleaseWhenDraggedTaut(root)' in ray
assert 'currentDrag.wiredCables != null) return;' in ray  # no forced auto-rotation
assert 'public bool ReleaseWhenDraggedTaut(Transform root)' in rope
release = rope[rope.index('public bool ReleaseWhenDraggedTaut('):
               rope.index('private bool ReleaseOverstretchedEnd()')]
assert 'if (span < length * .95f && path < length * .98f) return false;' in release
assert 'BelongsToAssembly(a.Connected, root) ? a' in release
assert 'release.Unplug();' in release
assert 'pin.breakForce = PullOutForce;' in (scripts / 'PC/Component/Cabling/CablePlug.cs').read_text()

# Audio clipping is addressed separately, never instead of physical release.
assert 'nextSoundTime = Time.time + .12f;' in impact
assert 'Mathf.Clamp01(velocityMagnitude * volumeMultiplier)' in impact
assert 'source.volume = velocityMagnitude * volumeMultiplier;' not in impact
print('PASS: drag graph excludes cables; taut case-side unplug; impact audio limited (Unity run still required)')
