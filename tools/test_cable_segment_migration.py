#!/usr/bin/env python3
"""Source + numeric guards for the 11->22 section migration of saved LAN 5m."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
prefab = (root / 'Assets/Resources/components/LAN Cable (5m).prefab').read_text()
rope = (root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Cabling/CableItem.cs').read_text()
assert re.search(r'^  length: 5\n  segments: 22\n  cableDiameter: 0\.08$', prefab, re.M)
assert 'kind == CableKind.LAN && length == 5f &&' in rope
assert 'bodies.Length == 22 && poses != null && poses.Count == 11' in rope
assert 'resampleLan && restorePositions' in rope
assert 'SavedBodyPosition(first), SavedBodyPosition(next), t' in rope
assert 'ReanchorChainFromLoadedPose();' in rope
assert 'AnchorPlugEnds();' in rope
assert 'rb.detectCollisions = false;' in rope

# In a straight old 11-section rope with ends at 0 and 11, the saved body
# centres sit at 0.5..10.5. New 22-section centres must be 0.25..10.75,
# including the endpoints; otherwise the first/last capsule spans an open gap.
old = [i + .5 for i in range(11)]
new = []
for i in range(22):
    source = (i + .5) * len(old) / 22 - .5
    lo = int(source // 1)
    if lo < 0:
        x = 0 + (old[0] - 0) * max(0, min(1, 1 + source * 2))
    elif lo >= 10:
        x = old[-1] + (11 - old[-1]) * max(0, min(1, (source - 10) * 2))
    else:
        x = old[lo] + (old[lo + 1] - old[lo]) * (source - lo)
    new.append(x)
assert all(abs(x - (i + .5) * .5) < 1e-9 for i, x in enumerate(new))
print('PASS: LAN 5m 22 physical sections; old 11-pose save stays curved/endpoint-anchored')
