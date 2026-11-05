#!/usr/bin/env python3
"""Static guards for the Editor-only traversal fixture; Unity Play Mode must run it."""
from pathlib import Path
import math
import re

root = Path(__file__).resolve().parents[1]
code = (root / 'Assets/Scripts/Assembly-CSharp/CableTraversalMode.cs').read_text()
menu = (root / 'Assets/Editor/CableTraversalMenu.cs').read_text()
assert '#if UNITY_EDITOR' in code
assert 'Start cable traversal test' in menu
assert 'CableTraversalMode.RequestFromEditor();' in menu
assert 'KeyCode.F8' not in code + menu and 'KeyCode.F9' not in code + menu
assert 'new DataLoader(savePath, data)' in code
assert 'JsonUtility.FromJson<GameData>(JsonUtility.ToJson(original.GameData))' in code
assert 'SaveManager.Loader = loader;' in code
assert 'SceneManager.LoadSceneAsync(roomScene)' in code
assert 'SaveManager.Instance.SaveData()' in code
assert 'Path.Combine(Application.persistentDataPath, "CableTraversal")' in code
assert 'Player.Instance.LoadPlayer(' in code
assert 'Resources.Load<GameObject>("components/ready/ReadyPC_Home_Black")' in code
for prefab in ('LAN Cable (5m)', 'Power Cable (15m)', 'HDMI Cable 6m',
               'ready/ReadyPC_Home_Black'):
    assert (root / 'Assets/Resources/components' / (prefab + '.prefab')).exists(), prefab
assert 'Cable traversal thin pole' in code and '.045f, 2.7f, .045f' in code
assert 'Cable traversal thick wall' in code and '.38f, 2.7f, 3f' in code
assert 'FindNearestCase(pcCentre)' in code and 'caseItem.GetComponentsInChildren<Collider>()' in code
# A coil placed at a fixed offset was already inside the assembled PC in Play Mode.
assert 'pcBounds.Encapsulate(obstacles[i].bounds)' in code
assert 'pcBounds.min.x - 2.1f' in code and 'lane == 2 ? pcCableSpawn' in code
assert 'bounds.max.z + .45f' in code
assert 'PLUG through ' in code and 'plugSolid.attachedRigidbody != plugBody' in code
assert 'FIXTURE PC bounds=' in code
# Check the actual delivery-coil radius, not the misleading "HDMI 6m" name.
hdmi = (root / 'Assets/Resources/components/HDMI Cable 6m.prefab').read_text()
length = float(re.search(r'^  length: ([\d.]+)$', hdmi, re.M).group(1))
sections = int(re.search(r'^  segments: (\d+)$', hdmi, re.M).group(1))
turns = 3 if sections >= 24 and length > 12 else 2 if sections >= 11 else 1
radius = (length / sections) / (2 * math.sin(math.pi * turns / sections))
assert 2.1 > radius + .25 + .3, 'fixture must clear coil, plug body and shell'
assert 'hand.AddComponent<SpringJoint>()' in code
assert 'hand.transform.position = target.position;' in code
assert 'joint.autoConfigureConnectedAnchor = false;' in code
assert 'joint.connectedAnchor = Vector3.zero;' in code
assert 'stationary grasp before movement' in code
assert 'yield return RunStage("thick wall / opposing ends"' in code
assert 'yield return RunStage("assembled PC housing"' in code
assert 'monitoringPins && message.StartsWith("Cable HDMI: PIN BREAK"' in code
assert 'reload watch step=' in code
assert 'moving.GetComponent<Rigidbody>().MovePosition(target);' in code
assert 'movingPlug.SetHeld(true);' in code and 'movingPlug.SetHeld(false);' in code
assert 'Mathf.SmoothStep(0f, 1f, t)' in code and 'Mathf.PerlinNoise' in code
assert 'for (int pause = 0; pause < 4' in code
assert 'int frames = i % 3 == 0 ? 24 : i % 3 == 1 ? 17 : 29;' in code
assert 'for (int step = 0; step < 25; step++)' in code
assert 'Observe("before save PC", cables[2], obstacles);' in code
assert 'if (maxGap > .03f)' in code and 'maxGap.ToString("0.0000")' in code
assert 'Physics.ComputePenetration(capsule, rb.position, rb.rotation,' in code
assert 'STRETCHED' in code and 'endpoint span exceeds cable length' in code
assert 'rope path stretched to ' in code and 'JACKET through ' in code
assert 'Physics.RaycastNonAlloc(start, delta.normalized, rayHits,' in code
assert 'plug-contact=' in code and 'Physics.GetIgnoreCollision(plugSolid, solid)' in code
assert 'firstViolations.Add(message)' in code
assert 'after save/reload lane=' in code
# C# cannot infer one `var` type for two declarators (CS0819).
assert 'var a = cable.End(0), b = cable.End(1);' not in code
assert 'Exit Play Mode or load your own save to return.' in code
print('PASS: human-paced spring/rod/wall/assembled-PC fixture, full isolated save/reload')
assert 'oppositeSeen = true;' in code and 'INCONCLUSIVE' in code
assert 'PoleWindings(cable, centre)' in code
assert 'maxPoleTurns < .85f' in code and 'max-winding=' in code
assert 'Mathf.DeltaAngle(previous, next)' in code
# The spiral only passes after a real winding AND a separate tension pull that
# unwinds the physical rope without capsules or jacket crossing the pole.
assert 'pullStart = waypoints.Count;' in code
assert 'new Vector3(-3.5f, 0f, -.85f)' in code
assert 'WOUND pole before pull:' in code and 'woundBeforePull < .85f' in code
assert 'for (int hold = 0; hold < 20 && !failed; hold++)' in code
assert 'Observe(stage + " unwind hold=" + hold, cable, solids)' in code
assert 'finalPoleTurns > .25f' in code and 'UNWIND FAIL' in code
assert 'TOPOLOGY JUMP' in code and 'previous - current > .45f' in code
assert 'after-pull=' in code
assert 'Power Cable (15m)' in code and 'HDMI Cable 6m' in code
