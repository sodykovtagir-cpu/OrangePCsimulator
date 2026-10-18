#!/usr/bin/env python3
"""Headless checks for the hand-edited 3DMork prefab's new record badge.
Not a Unity Play Mode screenshot; source and prefab structure only.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
PREFAB = (ROOT / 'Assets/Resources/apps/3DMork.prefab').read_text()
CS = (ROOT / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software/ThreeDMork.cs').read_text()
TRANSLATE = (ROOT / 'Assets/Resources/Translate.txt').read_text()
DOCS = {int(fid): (int(kind), body) for kind, fid, body in re.findall(
    r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', PREFAB, re.M | re.S)}
assert len(DOCS) == len(re.findall(r'^--- !u!', PREFAB, re.M))
assert 'm_Name: PersonalBestCircle' in DOCS[5000000000000730][1]
assert 'm_GameObject: {fileID: 5000000000000730}' in DOCS[5000000000000731][1]
assert 'm_Father: {fileID: 5000000000000310}' in DOCS[5000000000000731][1]
assert '{fileID: 5000000000000731}' in DOCS[5000000000000310][1]
assert 'm_Father: {fileID: 5000000000000731}' in DOCS[5000000000000446][1]
assert 'm_Sprite: {fileID: 21300000, guid: 09807ade07a9e244dabec7885cc0a17a, type: 2}' in DOCS[5000000000000733][1]
assert 'm_Text: 3DMork personal best title' in DOCS[5000000000000316][1]
assert 'm_Text: --' in DOCS[5000000000000448][1]
for fid, (kind, body) in DOCS.items():
    if kind != 1 or fid < 5000000000000310 or fid >= 5000000000000450: continue
    name = re.search(r'^  m_Name: (.*)$', body, re.M)
    if name and (name.group(1).startswith(('HistRow_', 'HistIndex_', 'HistDate_', 'HistScore_', 'HistFps_', 'Header_'))
                 or name.group(1) in ('HeaderRow', 'Subtitle', 'EmptyHint')):
        assert 'm_IsActive: 0' in body, (fid, name.group(1))
assert 'historyBest.text = best > 0 ? best.ToString() : "—";' in CS
assert 'RefreshHistory()' not in CS
assert 'PrefsKey(BestScorePrefsKey)' in CS and 'PrefsKey(HistoryPrefsKey)' in CS
assert 'system.SystemId.ToString("X8")' in CS
assert 'private void PushHistory(' in CS and 'private List<HistoryEntry> LoadHistory()' in CS
SPAWNER = (ROOT / 'Assets/Scripts/Assembly-CSharp/PC/ReadyBuildSpawner.cs').read_text()
assert 'IReadOnlyList<Part> BenchmarkParts' in SPAWNER
assert 'ReferenceBenchmarks = LoadReferenceBenchmarks()' in CS
assert 'Resources.Load<PC.ReadyBuildSpawner>' in CS
assert 'foreach (var part in build.BenchmarkParts)' in CS
assert 'gpu += hardware.Score' in CS and 'ram += hardware.Score' in CS and 'drive += hardware.Score' in CS
assert 'cpu += CpuPower(processor)' in CS
assert 'Array.Sort(ReferenceBenchmarks' in CS and 'RTX 4080 Ti' not in CS
for model in ('Dream','Workstation','Aquarium','Gaming','Home','Office'):
    assert (ROOT / f'Assets/Resources/components/ready/ReadyPC_{model}_Black.prefab').exists()
rows = {v[0]: v[1:] for v in (l.split('\t') for l in TRANSLATE.splitlines()) if v}
assert len(rows['3DMork personal best title']) == len(rows['3DMork best score'])
assert rows['3DMork personal best title'][0] == 'Best score'
assert rows['3DMork personal best title'][17] == 'Лучший результат'
for key in ('3DMork comparison title', '3DMork results title', '3DMork score label',
            '3DMork hardware title', '3DMork column score'):
    en, ru = rows[key][0], rows[key][17]
    assert not en.isupper() and not ru.isupper(), (key,en,ru)
import importlib.util
spec = importlib.util.spec_from_file_location('dmork_i18n', ROOT / 'tools/3dmork_i18n.py')
i18n = importlib.util.module_from_spec(spec)
spec.loader.exec_module(i18n)
for key in ('3DMork personal best title', '3DMork comparison title',
            '3DMork results title', '3DMork score label', '3DMork hardware title',
            '3DMork column score', '3DMork column date', '3DMork column gpu'):
    assert rows[key] == i18n.STRINGS[key], key
print('PASS: personal-best circle, per-PC records, ready-build references and localized headings')
