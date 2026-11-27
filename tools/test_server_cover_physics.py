#!/usr/bin/env python3
"""Static regression checks; these do not replace Unity/PhysX play-mode tests."""
from pathlib import Path
import re, subprocess, sys, tempfile
import yaml
R=Path(__file__).resolve().parents[1]
C=R/'Assets/Resources/components'
def docs(p):
    return {int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',p.read_text(),re.M|re.S)}
for path, count in [(C/'Server.prefab',4),(C/'Server HDD Tray.prefab',1)]:
    d=docs(path)
    fronts=[v for k,v in d.values() if k==65 and v.get('m_Size',{}).get('z') in (0.0747112,0.074124336)]
    assert len(fronts)==count and all(v['m_Size']['z']==0.0747112 for v in fronts),path
    for v in fronts:
        assert abs(v['m_Center']['z']-0.71839017)<1e-8
s=(R/'Assets/Scripts/Assembly-CSharp/Raycast.cs').read_text()
assert s.index('coverConnector.Break();')<s.index('StartCoroutine("DragObject")')
assert '!grabbedItem.glue' in s and '!serverCoverReleased' in s
p=(R/'Assets/Scripts/Assembly-CSharp/PlacementGrid.cs').read_text()
assert 'serverLid ? new[] { startRb } : AddLooseStack(CollectAssembly(startRb))' in p
assert 'if (!isServerLid && pcBodies != null' in p
sp=(R/'Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs').read_text()
assert 'root.GetComponent<ServerChassis>() == null' in sp
assert 'if (baseBody != null && useBuildStabilization)' in sp
assert 'if (useBuildStabilization)\n\t\t\t\t\t\tStabilizeHost' in sp
# Real CLI: dry run is non-destructive, removal updates owner and references.
with tempfile.TemporaryDirectory() as td:
    p=Path(td)/'tray.prefab';p.write_bytes((C/'Server HDD Tray.prefab').read_bytes())
    tool=R/'tools/prefab_edit.py';d=docs(p);cid=next(i for i,(k,v) in d.items() if k==65 and v.get('m_IsTrigger')==0)
    original=p.read_bytes();args=[sys.executable,str(tool),'remove-collider',str(p),str(cid)]
    subprocess.run(args,check=True,capture_output=True);assert p.read_bytes()==original
    subprocess.run(args+['--write'],check=True,capture_output=True);d=docs(p)
    assert cid not in d and not re.search(r'\{fileID: '+str(cid)+r'\}',p.read_text())
print('PASS: server tray duplicate colliders removed, lid drag/grid paths, server-only stabilization bypass, prefab editor dry-run/write. Unity play-mode required.')
