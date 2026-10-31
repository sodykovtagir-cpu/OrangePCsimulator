#!/usr/bin/env python3
"""Editor-only stress harness / fixture integrity, not a Unity/PhysX test."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]
scripts = root/'Assets/Scripts/Assembly-CSharp'
s = (scripts/'CableStressMode.cs').read_text()
restore = (scripts/'SaveManager.cs').read_text()
plug = (scripts/'PC/Component/Cabling/CablePlug.cs').read_text()
rope = (scripts/'PC/Component/Cabling/CableItem.cs').read_text()
assert s.startswith('#if UNITY_EDITOR') and s.rstrip().endswith('#endif')
assert 'KeyCode.F8' in s and 'private const int Lanes = 8;' in s
assert 'private const int Reloads = 5;' in s
assert 'new ItemData[0]' in s and 'JsonUtility.FromJson<GameData>(JsonUtility.ToJson(original.GameData))' in s
assert 'Path.Combine(Application.persistentDataPath, "CableStress")' in s
assert 'SaveManager.Loader = new DataLoader(savePath, data)' in s
assert 'if (expected.Count != 48)' in s and 'if (!Observe("baseline, before settling"))' in s
assert s.index('if (!Observe("baseline, before settling"))') < s.index('SaveManager.Instance.SaveData()')
assert s.count('SceneManager.LoadSceneAsync(roomScene)') == 2
assert 'if (!Observe("reload step " + step))' in s and 'Recording = false;' in s
assert 'finally' in s and 'all five reloads completed' in s
assert 'ReadyBuildSpawner' in s and 'OwnPorts' in s and 'CablePower.BoardHasPower' in s
assert 'outlet.IsMounted' in s and 'CablePower.Energized' in s
assert 'PrepareStressTestSpan();' in s and '#if UNITY_EDITOR' in rope
assert 'port.owner != item' in rope and 'owner not found' in rope and 'port missing on owner' in rope
assert 'body.detectCollisions = false;' in restore and 'if (pair.Item2 == null || pair.Item2 is PC.Component.Cabling.CableItem) continue;' in restore
assert 'yield return new WaitForFixedUpdate();' in restore
assert 'pin.breakForce = Mathf.Infinity;' in plug and 'restoredPin.breakForce = PullOutForce;' in plug
assert 'CableStressMode.Recording' in (scripts/'Connector.cs').read_text()
for prefab in ('ready/ReadyPC_Home_Black', 'Monitor(Black)', 'Wall Outlet 3',
               'Power Cable (5m)', 'Power Cable 5m', 'HDMI Cable (6m)'):
    assert (root/'Assets/Resources/components'/f'{prefab}.prefab').is_file(), prefab
build = (root/'Assets/Resources/components/ready/ReadyPC_Home_Black.prefab').read_text()
names = {}
for meta in (root/'Assets/Resources/components').rglob('*.prefab.meta'):
    match = re.search(r'^guid: ([0-9a-f]+)$', meta.read_text(), re.M)
    if match: names[match.group(1)] = meta.name[:-12]
parts = re.findall(r'  - prefab: [^\n]*guid: ([0-9a-f]+), type: 3\}[^\n]*\n(?:[^\n]*\n)*?    slotTarget: ([^\n]+)', build)
assert ('GTX1060', 'GPU') in [(names.get(g), slot) for g, slot in parts]
assert ('PSU 500W', 'Supply') in [(names.get(g), slot) for g, slot in parts]
print('Cable stress editor fixture, isolation, 5 cycles, diagnostics: OK (Unity run still required)')
