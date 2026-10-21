#!/usr/bin/env python3
"""Static regression checks for per-PC test load, stage isolation and picker UI.
Unity Play Mode is still required to validate actual render frames and interaction.
"""
from pathlib import Path
import re
import yaml

root = Path(__file__).resolve().parents[1]
software = root / 'Assets/Scripts/Assembly-CSharp/PC/Component/Software'
os = (software / 'OS/OperatingSystem.cs').read_text()
benchmark = (software / 'Benchmark.cs').read_text()
mork = (software / 'ThreeDMork.cs').read_text()
task = (software / 'TaskManager.cs').read_text()
picker = (software / 'OS/OpenDialog.cs').read_text()
listview = (root / 'Assets/Scripts/Assembly-CSharp/ListView.cs').read_text()

assert 'SetBenchmarkPhase(this, phase)' in benchmark
assert benchmark.index('BenchmarkCpuFactor') < benchmark.index('SetPhase(i == 0')
for type_ in ('CPU', 'GPU', 'RAM'):
    assert 'HardwareType.' + type_ in benchmark
assert 'SetPhase(null);' in benchmark and 'benchmarkPhases.Clear();' in os
assert 'if (cpuBenchmark) cpuDemand = Mathf.Max(cpuDemand, capacity)' in os
assert 'CpuLoad = cpuBenchmark ? 1f' in os
assert 'if (gpuBenchmark) gpuDemand = 1f' in os
assert 'RamUsedMb = ramBenchmark ? RamTotalMb' in os
assert 'if (app is ThreeDMork) gpuDemand = 1f' in os
assert 'public bool IsRunningTest => benchmarkCoroutine != null && stageSlot >= 0' in mork
assert 'ActiveStageSlots' in mork and 'ReleaseStage();' in mork
assert 'Vector3.right * (stageSlot * 500f)' in mork
assert 'recordPcId.ToString("X8")' in mork and 'board.Id = id;' in mork
assert 'UiResponsiveness = Mathf.Lerp(1f, .30f, lag)' in os
assert 'RamLoad >= .96f && ShellRunning' in os
assert 'version != shellTerminationVersion' in os
assert 'AnimationDelta(window, OpenTime' in (software / 'WindowChrome.cs').read_text()
assert 'system.UiResponsiveness' in (software / 'App.cs').read_text()
assert 'system.UiResponsiveness' in (software / 'Viewer.cs').read_text()
assert 'os.UiResponsiveness' in (software / 'VideoLoadController.cs').read_text()
assert 'VideoLoadController' in (software / 'Video.cs').read_text()
assert 'gpuCard.gameObject.SetActive(hasGpu)' in task
assert 'Models(gpuModels)' in task and 'Models(cpuModels)' in task
assert 'оценка' not in task
assert 'SetSelectionTint(new Color(.72f, .72f, .72f, 1f))' in picker
assert 'element.graphic.color = selectionTint' in listview
assert 'UpdateSelection(index);' in listview
assert 'SelectedIndexChanged?.Invoke(selectedIndex);' in listview

downloader = (root / 'Assets/Resources/apps/Downloader.prefab').read_text()
assert 'EnsureTaskManager();' not in os and 'EnsureRemoteDesk();' not in os
for name, fileid in [('TaskManager', '1140000000000002'),
                     ('RemoteDesk', '114706950818030795')]:
    prefab = root / f'Assets/Resources/apps/{name}.prefab'
    guid = re.search(r'^guid: ([0-9a-f]{32})$', prefab.with_name(prefab.name + '.meta').read_text(), re.M).group(1)
    assert f'{{fileID: {fileid}, guid: {guid}, type: 3}}' in downloader

for name in ('TaskManager', 'Downloader'):
    contents = (root / f'Assets/Resources/apps/{name}.prefab').read_text()
    docs = list(yaml.compose_all(re.sub(r'!u!\d+', '', contents)))
    ids = re.findall(r'^--- !u!\d+ &(\d+)$', contents, re.M)
    assert len(docs) == len(ids) == len(set(ids)), name
print('PASS: isolated hardware phases/rooms/records, UI lag, Downloader and gray picker')
print('NOTE: Unity Play Mode is needed for visual layout, media playback and shell restart verification')
