#!/usr/bin/env python3
"""Power/VGA/HDMI plug rear exit geometry and legacy-save contracts (static)."""
from pathlib import Path
import re
from check_prefab_yaml import main as check_prefab

root = Path(__file__).resolve().parents[1]
folder = root / 'Assets/Resources/components'
plug = (root/'Assets/Scripts/Assembly-CSharp/PC/Component/Cabling/CablePlug.cs').read_text()
rope = (root/'Assets/Scripts/Assembly-CSharp/PC/Component/Cabling/CableItem.cs').read_text()
assert 'plug.EnsureCableExitPivot(exit);' in rope
assert 'new Vector3(-.115f, 0f, 0f)' in rope
assert 'new Vector3(0f, -.06f, 0f)' in rope
assert 'new Vector3(0f, 0f, -.112f)' in rope
assert 'if (kind == CableKind.VGA || kind == CableKind.HDMI)' not in rope # no runtime opt-out
assert 'CableExitLocalPosition == Vector3.zero' in plug
assert 'private void AnchorPlugEnds()' in rope
assert 'AnchorPlugEnds();' in rope[rope.index('public override void FromData('):rope.index('public float SavedPoseDrift(')]
assert 'AnchorPlugEnds();' in rope[rope.index('public void FinishRestore('):]
assert 'ReanchorChainFromLoadedPose();' in rope[rope.index('public void FinishRestore('):]
# The older end-joint arrays still load, but old endpoint anchors are updated
# in physics coordinates while interior body anchors/positions remain saved.
assert 'var savedJoints = data["cableJoints"] as JArray;' in rope
assert 'first.connectedAnchor = plugs[0].CableExitLocalPosition;' in rope
assert 'last.anchor = plugs[1].CableExitLocalPosition;' in rope
expected = {
    'Power Cable': (0, ('{x: 0, y: -0.06, z: 0}', '{x: 0, y: 0, z: -0.112}')),
    'VGA Cable': (1, ('{x: -0.115, y: 0, z: 0}',) * 2),
    'HDMI Cable': (2, ('{x: -0.115, y: 0, z: 0}',) * 2),
}
counts = {key:0 for key in expected}
for path in sorted(folder.glob('*.prefab')):
    name = next((key for key in expected if path.name.startswith(key+' ')), None)
    if name is None: continue
    counts[name] += 1
    kind, positions = expected[name]
    text = path.read_text()
    assert check_prefab(str(path)) == 0
    assert f'  kind: {kind}\n' in text
    ids=re.findall(r'^--- !u!\d+ &(-?\d+)',text,re.M)
    assert len(ids)==len(set(ids))
    assert text.count('  m_Name: Cable Pivot\n') == 2
    for plug_id, root_id, pivot_go, pivot_transform, pos in (
        (1140010,400010,100011,400011,positions[0]),
        (1140020,400020,100021,400021,positions[1])):
        component=text.split(f'--- !u!114 &{plug_id}\n',1)[1].split('--- !u!',1)[0]
        body=text.split(f'--- !u!4 &{root_id}\n',1)[1].split('--- !u!',1)[0]
        pivot=text.split(f'--- !u!4 &{pivot_transform}\n',1)[1].split('--- !u!',1)[0]
        assert f'  cableExitPivot: {{fileID: {pivot_transform}}}\n' in component
        assert f'  - {{fileID: {pivot_transform}}}\n' in body
        assert f'  m_GameObject: {{fileID: {pivot_go}}}\n' in pivot
        assert f'  m_Father: {{fileID: {root_id}}}\n' in pivot
        assert f'  m_LocalPosition: {pos}\n' in pivot
        assert '  m_LocalScale: {x: 1, y: 1, z: 1}\n' in pivot
        assert '  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n' in pivot
assert counts == {'Power Cable':4,'VGA Cable':4,'HDMI Cable':6},counts
print('PASS: 14 Power/VGA/HDMI prefabs, unique rear pivots, save migration')
