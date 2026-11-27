#!/usr/bin/env python3
"""Static checks only; run Tools/OrangePC/Server physics diagnostic in Unity."""
from pathlib import Path
import re, yaml
R=Path(__file__).resolve().parents[1]
C=R/'Assets/Resources/components'
def docs(p):
    return {int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',p.read_text(),re.M|re.S)}
for path,count in [(C/'Server.prefab',4),(C/'Server HDD Tray.prefab',1)]:
    d=docs(path)
    for size in (0.0747112,0.074124336):
        assert sum(1 for k,v in d.values() if k==65 and v.get('m_Size',{}).get('z')==size)==count
ray=(R/'Assets/Scripts/Assembly-CSharp/Raycast.cs').read_text()
assert 'target = hitRb.transform' in ray
assert 'hit.rigidbody.GetComponent<Connector>()' in ray
assert '!GridMode() && serverItem != null && !serverItem.glue' in ray
assert 'serverConnector.slot.GetComponentInParent<ServerChassis>()' in ray
grid=(R/'Assets/Scripts/Assembly-CSharp/PlacementGrid.cs').read_text()
assert 'AddLooseStack(CollectAssembly(startRb))' in grid
assert 'serverLid ?' not in grid
sp=(R/'Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs').read_text()
assert 'root.GetComponent<ServerChassis>() == null' in sp
print('PASS: restored authored colliders and grid assembly; remove mode uses collider body; Play Mode required')
