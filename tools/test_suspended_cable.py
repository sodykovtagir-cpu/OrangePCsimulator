#!/usr/bin/env python3
"""Source guards for passive suspended-device release; needs Unity physics test."""
from pathlib import Path

scripts = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
rope = (scripts/'PC/Component/Cabling/CableItem.cs').read_text()
plug = (scripts/'PC/Component/Cabling/CablePlug.cs').read_text()
ports = (scripts/'PC/Component/Cabling/CablePort.cs').read_text()

# Do not change ordinary pin break force or fake the power/signal outcome.
assert 'pin.breakForce = PullOutForce;' in plug
assert 'public bool ReleaseWhenDraggedTaut(Transform root)' in rope
assert 'if (!hardLimit && !movingApart)\n                return ReleaseSustainedTautLoad(path);' in rope
suspension = rope[rope.index('private bool ReleaseSustainedTautLoad('):
                  rope.index('private CablePlug StrainedEnd()')]
assert 'path < length * .96f' in suspension
assert 'Physics.gravity.y >= -.5f' in suspension
assert 'Time.time < SaveManager.IgnoreLoadImpactsUntil' in suspension
assert 'other.Connected == null' in suspension  # never release a lone free end
assert 'CanPull(rb, 0)' in suspension
assert 'plug.PinLoad < Mathf.Max(2.5f, weight * .4f)' in suspension
assert 'HasSolidSupport(rb)' in suspension
assert 'Time.time - loadedSince < .5f' in suspension
assert 'candidate.Unplug();' in suspension
assert 'private bool HasSolidSupport(Rigidbody rb)' in suspension
assert 'Physics.OverlapBoxNonAlloc' in suspension
assert 'other.GetComponentInParent<CableItem>() != null' in suspension
assert 'internal float PinLoad => pin != null ? pin.currentForce.magnitude : 0f;' in plug

# Leaving the tiny inner core is insufficient: while still in the trigger the
# same plug would immediately be inserted again on OnTriggerStay.
assert 'candidate.TryInsert(this);' in ports
assert 'if (!restoring && port == yankedPort) return false;' in plug
assert 'if (yankedPort != null && !NearPort(yankedPort, .10f)) yankedPort = null;' in plug
print('PASS: sustained loaded, unsupported taut cable releases; no premature reinsert (Unity run still required)')
