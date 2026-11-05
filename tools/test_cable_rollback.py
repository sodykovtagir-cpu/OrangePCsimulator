#!/usr/bin/env python3
"""Static guards for the cable feel rollback; NOT a Unity Play Mode test."""
from pathlib import Path
import math
import re

root = Path(__file__).resolve().parents[1]
code = (root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Cabling/CableItem.cs').read_text()
plug = (root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Cabling/CablePlug.cs').read_text()
raycast = (root / 'Assets/Scripts/Assembly-CSharp/Raycast.cs').read_text()
lan = (root / 'Assets/Resources/components/LAN Cable (5m).prefab').read_text()

# Keep the confirmed 11-body physics, but draw only one point per joint
# to avoid the LineRenderer's doubled-back anchor triangles.
assert re.search(r'^  segments: 11$', lan, re.M)
assert 'jacket.positionCount = count + 1;' in code
assert 'var joint = links[i];' in code
assert 'Vector3 previous = bodies[i - 1].transform.TransformPoint(joint.connectedAnchor);' in code
assert 'jacket.SetPosition(i, (current + previous) * .5f);' in code
assert 'jacket.SetPosition(bodies.Length, plugB.CableExitPosition);' in code
assert 'jacket.SetPosition(i * 2 + 2,' not in code
assert 'UpdateRotationalCcd();' not in code
assert 'CableTraversalMode' not in code
assert 'CableTraversalMode' not in raycast
assert 'UpdateCableCcd' not in plug
assert 'FreeGrip' not in plug  # the later heavy/limited hand is gone

# But saved 22-body LAN curves should be sampled at the centres of PAIRS,
# then reanchored instead of using incompatible 23-joint anchor arrays.
assert 'bodies.Length != 11 || poses == null || poses.Count != 22 ||' in code
assert '!restorePositions) return false;' in code
assert 'pose.Count < 7' in code
assert 'bool resampleLan = IsResampledLanSave(poses);' in code
assert 'else if (IsResampledLanSave(poses))' in code  # first-cable pose drift repair
assert 'Vector3.Lerp(SavedBodyPosition(a), SavedBodyPosition(b), t)' in code
assert 'Quaternion.Slerp(SavedBodyRotation(a), SavedBodyRotation(b), t)' in code
assert 'savedJoints.Count == bodies.Length + 1' in code
assert 'if (!exact && (poses != null || restorePositions))\n                ReanchorChainFromLoadedPose();' in code
for i in range(11):
    source = (i + .5) * 22 / 11 - .5
    low = math.floor(source)
    assert (low, low + 1, source - low) == (2*i, 2*i + 1, .5)

assert not (root / 'Assets/Editor/CableTraversalMenu.cs').exists()
assert not (root / 'Assets/Scripts/Assembly-CSharp/CableTraversalMode.cs').exists()
print('Cable rollback, 22-to-11 saved-curve migration guards: OK (static only)')
