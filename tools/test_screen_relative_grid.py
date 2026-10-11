#!/usr/bin/env python3
"""Static guard: arrows follow cardinal camera side without breaking snap offset."""
from pathlib import Path
from math import radians, sin, cos
ray = (Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp/Raycast.cs').read_text()
move = ray.split('public void ArrowInput(')[1].split('private void NudgeSelected(')[0]
nudge = ray.split('private void NudgeSelected(')[1].split('public void RotateDragged(')[0]
drag = ray.split('private IEnumerator DragObject()')[1].split('public void End()')[0]
rotate = ray.split('public void RotateDragged(')[1].split('private bool TryRotateAssembly(')[0]
directions = ray.split('private Vector3 GridForward()')[1].split('/// <summary>R / кнопка панели:')[0]
assert 'Mathf.Abs(f.x) > Mathf.Abs(f.z)' in directions
assert 'Vector3.Cross(Vector3.up, GridForward())' in directions
assert 'GridStep(xDir, yDirUp)' in move and 'GridStep(xDir, yDirUp)' in nudge
assert 'SnapPosition(com,' not in nudge and 'SnapPosition(com,' not in drag
assert 'bodySnapped = com + pendingGridMove;' in drag
assert 'gridMoveInit' not in ray
assert 'TumbleAxis(qBase, screenRight)' in rotate
assert 'target = Quaternion.AngleAxis(-yawDir * step, Vector3.up)' in rotate
assert 'target = Quaternion.AngleAxis(-pitchDir * step, axis)' in rotate
# For near-cardinal yaw the desired up/right remain unit cardinal vectors.
def forward(yaw):
    x, z = sin(radians(yaw)), cos(radians(yaw))
    return (1 if x >= 0 else -1, 0) if abs(x) > abs(z) else (0, 1 if z >= 0 else -1)
def right(f):
    return f[1], -f[0]
for a, b in [(89, 90), (-89, -90), (179, 180), (269, 270)]:
    assert forward(a) == forward(b), (a, b)
for yaw in (0, 89, 180, 269):
    f = forward(yaw)
    r = right(f)
    assert sum(abs(n) for n in f) == 1 and sum(abs(n) for n in r) == 1
    assert f[0]*r[0] + f[1]*r[1] == 0
assert forward(0) == (0, 1) and forward(180) == (0, -1)
print('PASS: camera-side cardinal steps, rotation axes and neighbor-snap offset')
