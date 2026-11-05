#!/usr/bin/env python3
"""Static guards for early contacts and bounded short-cable solver budget.

Does not claim Unity physics/Play Mode passed.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
rope = (root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Cabling/CableItem.cs').read_text()
start = rope.index('for (int i = 0; i < count; i++)\n            {\n                var body')
end = rope.index('SetupJacket(count);', start)
creation = rope[start:end]
assert 'col.contactOffset = Mathf.Min(.025f, col.radius * .5f);' in creation
assert 'physicalColliders.Add(col);' in creation
assert 'rb.solverIterations = sweepCcd ? 24 : 12;' in rope
assert 'rb.solverVelocityIterations = sweepCcd ? 12 : 8;' in rope
assert 'rb.maxDepenetrationVelocity = sweepCcd ? 2.5f : 1.5f;' in rope
assert 'UpdateRotationalCcd();' in rope
assert 'tipSpeed = rb.angularVelocity.magnitude * capsule.height * .5f' in rope
assert 'rotationalCcdUntil[i] = Time.time + .12f;' in rope
assert 'CollisionDetectionMode.ContinuousSpeculative' in rope
assert 'CollisionDetectionMode.ContinuousDynamic' in rope
assert 'joint.projectionDistance = .02f;' in rope
assert 'CollisionDetectionMode.ContinuousDynamic' in rope
assert 'RestoreCableCcd(rb);' in rope
assert 'SettleEndAtSocket' in rope
# This change must not affect authored cable plug BoxCollider YAML.
for name in ('LAN Cable (5m)', 'Power Cable (15m)', 'HDMI Cable 6m'):
    prefab = (root / 'Assets/Resources/components' / (name + '.prefab')).read_text()
    assert 'BoxCollider:' in prefab
print('PASS: predictive runtime capsule contacts, 24/12 short-chain solver; Unity run needed')
