#!/usr/bin/env python3
"""Regression check for double-transforming nested rigidbodies during grid moves.

Unity physics must still be smoke-tested in the Editor. This check ensures all
manual assembly motion paths apply the transform only to top-level bodies.
"""
from pathlib import Path

root = Path(__file__).resolve().parent.parent
scripts = root / 'Assets/Scripts/Assembly-CSharp'
grid = (scripts / 'PlacementGrid.cs').read_text(encoding='utf-8')
ray = (scripts / 'Raycast.cs').read_text(encoding='utf-8')

roots = grid.split('public static Rigidbody[] AssemblyRoots(')[1].split('return roots.ToArray();')[0]
assert 'new HashSet<Transform>()' in roots
assert 'rb.transform.parent' in roots and 'parent = parent.parent' in roots
assert 'transforms.Contains(parent)' in roots

unstick = grid.split('private static void MoveBodiesUp(')[1].split('public static Rigidbody[] AssemblyRoots(')[0]
pin = ray.split('private void ApplyPin(')[1].split('private float NearestBlockDistance(')[0]
rotation = ray.split('// Поворот вокруг пивота — прямой записью трансформов.')[1].split('Physics.SyncTransforms();', 2)[0]
for name, section in [('unstick', unstick), ('pin', pin), ('rotate', rotation)]:
    assert 'AssemblyRoots(' in section, f'{name}: must move roots only'
    assert 'velocity = Vector3.zero' in section, f'{name}: must clear every body velocity'
assert 'roots[i].transform.position += new Vector3(0f, lift, 0f)' in ray

# Demonstrate the previous failure: a child receives the parent's move and
# then a second explicit move. Filtering roots gives exactly one delta.
parent_pos, child_pos, delta = 0.0, 1.0, 0.5
old_child = child_pos + delta + delta
new_child = child_pos + delta
assert old_child == 2.0 and new_child == 1.5
print('PASS: pin, rotation, lift and unstick move hierarchy roots only')
