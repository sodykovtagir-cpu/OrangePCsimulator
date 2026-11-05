#!/usr/bin/env python3
"""Static regression guards for cable-only, bounded flexible grip.

Unity Play Mode is still needed for jitter, tension and unplug behaviour.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
ray = (root / 'Assets/Scripts/Assembly-CSharp/Raycast.cs').read_text()
mode = (root / 'Assets/Scripts/Assembly-CSharp/CableTraversalMode.cs').read_text()
assert 'if (plug != null) plug.SetHeld(true, ReleaseHeldPlug);' in ray
assert 'grabbedCable.HoldSection(hitRb);' in ray
assert 'bool freeCableGrip = grabbedCable != null' in ray
assert '(plug == null || plug.Connected == null)' in ray
assert 'spring.spring = freeCableGrip ? (plug == null ? 22f : 45f) : targetSpring;' in ray
assert 'spring.damper = freeCableGrip ? 5f : targetDamper;' in ray
assert 'rb.drag = freeCableBody ? (heldPlug == null ? 2f : 4f) : targetDrag;' in ray
assert 'rb.angularDrag = freeCableBody ? 2f : targetAngularDrag;' in ray
assert 'Vector3.MoveTowards(spring.transform.position,' in ray
assert 'Vector3 grip = rb.position + rb.rotation * spring.connectedAnchor;' in ray
assert 'Vector3.ClampMagnitude(follow - grip, reach)' in ray
assert 'float reach = heldPlug == null ? .16f : .28f;' in ray
assert 'spring.transform.position = point;' in ray  # original path for plugged/unrelated objects
assert 'releasedPlug.SetHeld(false);' in ray
assert 'currentDrag.grabbedCable.HoldSection(null);' in ray
assert 'rb.drag = d.oldDrag;' in ray and 'rb.angularDrag = d.oldAngularDrag;' in ray
assert 'var moving = Hand(movingEnd, 45f, 5f);' in mode
assert 'Vector3.ClampMagnitude(follow - grip, .28f)' in mode
assert 'movingEnd.drag = 4f;' in mode and 'movingEnd.angularDrag = 2f;' in mode
print('PASS: bounded free-cable grip, local section drag, plugged physical pull-out unchanged')
