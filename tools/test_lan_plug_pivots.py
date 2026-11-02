#!/usr/bin/env python3
"""LAN exit-pivot and save migration contracts (static; Unity Play Mode required)."""
from pathlib import Path
import re
from check_prefab_yaml import main as validate_yaml

root = Path(__file__).resolve().parents[1]
parts = root / 'Assets/Resources/components'
code = root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Cabling'
plug = (code / 'CablePlug.cs').read_text()
rope = (code / 'CableItem.cs').read_text()
assert '[SerializeField] private Transform cableExitPivot;' in plug
assert 'transform.Find("Cable Pivot")' in plug
assert 'new Vector3(-.02f, 0f, 0f)' in rope
assert 'body.position + body.rotation * CableExitLocalPosition' in plug
assert 'Vector3.Distance(other.CableExitPhysicsPosition,' in plug
assert 'port.Snap.position + port.Snap.rotation * CableExitLocalPosition' in plug
assert 'Vector3.Distance(other.Connected.Snap.position, port.Snap.position) :' in plug
assert 'plug.EnsureCableExitPivot(exit);' in rope
assert 'points[0] = plugA.CableExitPosition;' in rope
assert 'points[count] = plugB.CableExitPosition;' in rope
assert 'last.anchor = plugB.CableExitLocalPosition;' in rope
assert 'jacket.SetPosition(0, plugA.CableExitPosition);' in rope
assert 'jacket.SetPosition(bodies.Length + 1, plugB.CableExitPosition);' in rope
assert 'Vector3 start = plugs[0].CableExitPhysicsPosition;' in rope
assert 'Vector3 end = plugs[1].CableExitPhysicsPosition;' in rope
assert 'private void AnchorPlugEnds()' in rope
assert 'first.connectedAnchor = plugs[0].CableExitLocalPosition;' in rope
assert 'last.anchor = plugs[1].CableExitLocalPosition;' in rope
assert rope.count('AnchorPlugEnds();') >= 2 # after FromData and after port restoration
assert 'data["cableJoints"] = anchors;' in rope and 'RestoreJoint(' in rope
assert 'ReanchorChainFromLoadedPose();' in rope
for length in (5, 15, 48, 100):
    path = parts / f'LAN Cable ({length}m).prefab'
    content = path.read_text()
    assert validate_yaml(str(path)) == 0
    ids = re.findall(r'^--- !u!\d+ &(-?\d+)', content, re.M)
    assert len(ids) == len(set(ids))
    assert '  lanPlugVisualScale: 1\n' in content
    assert '  kind: 3\n' in content
    assert 'm_Size: {x: 0.15, y: 0.10, z: 0.20}' in content or \
        'm_Size: {x: 0.15, y: 0.1, z: 0.2}' in content
    assert len(re.findall(r'^  m_Name: Cable Pivot$', content, re.M)) == 2
    for prefab_id, root_id, pivot_go, pivot_transform in (
        (1140010, 400010, 100011, 400011),
        (1140020, 400020, 100021, 400021),
    ):
        plug_body = content.split(f'--- !u!114 &{prefab_id}\n', 1)[1].split('--- !u!', 1)[0]
        assert f'  cableExitPivot: {{fileID: {pivot_transform}}}' in plug_body
        root_body = content.split(f'--- !u!4 &{root_id}\n', 1)[1].split('--- !u!', 1)[0]
        assert f'  - {{fileID: {pivot_transform}}}' in root_body
        pivot_body = content.split(f'--- !u!4 &{pivot_transform}\n', 1)[1].split('--- !u!', 1)[0]
        assert f'  m_GameObject: {{fileID: {pivot_go}}}' in pivot_body
        assert f'  m_Father: {{fileID: {root_id}}}' in pivot_body
        assert '  m_LocalPosition: {x: -0.02, y: 0, z: 0}' in pivot_body
        # Pivot must sit just inside the back face of the authored plug
        # collider, not 12 cm outside it as the old -0.15 marker did.
        collider = content.split(f'--- !u!65 &{650010 if root_id == 400010 else 650020}\n', 1)[1].split('--- !u!', 1)[0]
        center = re.search(r'm_Center: \{x: ([^,]+),', collider)
        size = re.search(r'm_Size: \{x: ([^,]+),', collider)
        assert center and size
        back = float(center.group(1)) - float(size.group(1)) / 2
        assert back < -0.02 < back + 0.02, (path, root_id, back)
        assert '  m_LocalScale: {x: 1, y: 1, z: 1}' in pivot_body
print('PASS: four LAN prefab pairs, rear exit pivots, jacket/joints and old-save anchors')
