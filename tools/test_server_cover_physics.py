#!/usr/bin/env python3
"""Static checks only; run Tools/OrangePC/Server physics diagnostic in Unity."""
from pathlib import Path
import re, yaml
R=Path(__file__).resolve().parents[1]
C=R/'Assets/Resources/components'
def read(p):
    return p.read_text(encoding='utf-8')
def docs(p):
    return {int(m[2]):(int(m[1]),next(iter(yaml.safe_load(m[3]).values()))) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',read(p),re.M|re.S)}
for path,count in [(C/'Server.prefab',4),(C/'Server HDD Tray.prefab',1)]:
    d=docs(path)
    for size in (0.0747112,0.074124336):
        assert sum(1 for k,v in d.values() if k==65 and v.get('m_Size',{}).get('z')==size)==count
ray=read(R/'Assets/Scripts/Assembly-CSharp/Raycast.cs')
# Клик берёт ровно ту деталь, в которую попал: ни подмены на корпус, ни
# автоматического разрыва слота при захвате.
assert 'target = hit.transform' in ray
assert 'hit.transform.TryGetComponent<Connector>(out var conn)' in ray
assert 'mount.connectedBody.GetComponent<PC.Component.Case>()' not in ray
assert 'serverConnector.slot.GetComponentInParent<ServerChassis>()' not in ray
assert 'serverPartReleased' not in ray
# Дрожь собранного сервера лечится итерациями решателя у тел самой сборки.
chassis=read(R/'Assets/Scripts/Assembly-CSharp/PC/Component/ServerChassis.cs')
assert 'public static void Stabilize(Rigidbody body)' in chassis
assert 'AssemblySolverIterations = 12' in chassis
assert 'AssemblySolverVelocityIterations = 8' in chassis
assert 'StabilizeIfServerPart' in chassis
slot=read(R/'Assets/Scripts/Assembly-CSharp/Slot.cs')
assert 'ServerChassis.StabilizeIfServerPart' in slot
grid=read(R/'Assets/Scripts/Assembly-CSharp/PlacementGrid.cs')
assert 'AddLooseStack(CollectAssembly(startRb))' in grid
assert 'serverLid ?' not in grid
sp=read(R/'Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs')
assert 'root.GetComponent<ServerChassis>() == null' in sp
print('PASS: plain collider targeting, server assembly solver stabilization, authored colliders; Play Mode required')
# The attachable runner must compile into the runtime assembly, not Editor.
runner=read(R/'Assets/Scripts/Assembly-CSharp/ServerPhysicsDiagnostic.cs')
menu=read(R/'Assets/Editor/ServerPhysicsDiagnosticMenu.cs')
assert 'class ServerPhysicsDiagnostic : MonoBehaviour' in runner
assert 'using UnityEditor;' not in runner
assert 'ServerPhysicsDiagnostic.RequestRun();' in menu
assert '#if UNITY_EDITOR' in runner and '#if UNITY_EDITOR' in menu
assert not (R/'Assets/Editor/ServerPhysicsDiagnostic.cs').exists()
print('PASS: editor menu delegates to attachable runtime-assembly runner')
