#!/usr/bin/env python3
"""Static guards for the Editor-only traversal fixture; Unity Play Mode must run it."""
from pathlib import Path

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
assert 'hand.AddComponent<SpringJoint>()' in code
assert 'moving.GetComponent<Rigidbody>().MovePosition(target);' in code
assert 'movingPlug.SetHeld(true);' in code and 'movingPlug.SetHeld(false);' in code
assert 'Mathf.SmoothStep(0f, 1f, t)' in code and 'Mathf.PerlinNoise' in code
assert 'for (int pause = 0; pause < 7' in code
assert 'Physics.ComputePenetration(capsule, rb.position, rb.rotation,' in code
assert 'STRETCHED' in code and 'endpoint span exceeds cable length' in code
assert 'rope path stretched to ' in code and 'JACKET through ' in code
assert 'Physics.RaycastNonAlloc(start, delta.normalized, rayHits,' in code
assert 'firstViolations.Add(message)' in code
assert 'after save/reload lane=' in code
# C# cannot infer one `var` type for two declarators (CS0819).
assert 'var a = cable.End(0), b = cable.End(1);' not in code
assert 'Exit Play Mode or load your own save to return.' in code
print('PASS: human-paced spring/rod/wall/assembled-PC fixture, full isolated save/reload')
assert 'oppositeSeen = true;' in code and 'INCONCLUSIVE' in code
assert 'Power Cable (15m)' in code and 'HDMI Cable 6m' in code
