#!/usr/bin/env python3
"""Static guards for task manager, mining priority, thermal clock and native prefab.
Unity Play Mode must still be exercised in the editor for real interaction checks."""
from pathlib import Path
import re
import yaml

root = Path(__file__).resolve().parents[1]
comp = root/'Assets/Scripts/Assembly-CSharp/PC/Component'
os = (comp/'Software/OS/OperatingSystem.cs').read_text()
cpu = (comp/'CPU.cs').read_text()
gpu = (comp/'GPU.cs').read_text()
miner = (comp/'Software/Miner.cs').read_text()
bench = (comp/'Software/Benchmark.cs').read_text()
mork = (comp/'Software/ThreeDMork.cs').read_text()
task = (comp/'Software/TaskManager.cs').read_text()
pref = (root/'Assets/Resources/apps/TaskManager.prefab').read_text()

# Case 1: CPU score and temporarily throttled clock set demand/capacity.
assert 'capacity += cpu.Score * Mathf.Clamp(cpu.EffectiveFrequency' in os
assert 'cpuDemand / capacity' in os
assert 'public float EffectiveFrequency => frequency * ThermalThrottleRatio;' in cpu
assert 'Mathf.MoveTowards(ThermalThrottleRatio, targetClock' in cpu
assert 'temperature >= burnTemp' in cpu  # damage still possible if seriously overheated
# An actual iGPU belongs to CPU models, not Motherboard.Integrated (mini PC).
models = list((root/'Assets/Resources/components').glob('CPU *.prefab'))
assert len(models) >= 9  # full project has 12; sparse CI checkout can include only the nine iGPU models
assert all(('  integratedGraphics: 1' in p.read_text()) ==
           ('Xeon' not in p.name and '7900X' not in p.name) for p in models)
assert 'cpu.HasIntegratedGraphics' in os and 'Motherboard.Integrated' not in os[:os.index('public void EndProcess')]
# Case 2: only GPU OR CPU mines, GPU use does not throttle mining payout.
assert 'board.GetHardwares(hasGpu ? HardwareType.GPU : HardwareType.CPU)' in miner
assert 'system.StartCoroutine(Digging())' in miner and 'system.StopCoroutine(miningRoutine)' in miner
assert 'public override void OnSystemStop()' in miner
assert 'a.IsMinimized' in os and 'app.IsMinimized' in os
assert 'if (gpuMining) { gpuDemand = 1f;' in os
assert 'else if (mining) { cpuDemand = Mathf.Max(cpuDemand, capacity);' in os
assert 'Time.timeScale' not in os[os.index('private void UpdateWorkload()'):os.index('public string UserPicturePath')]
assert 'BenchmarkCpuFactor' in bench and 'BenchmarkGpuFactor' in bench
assert 'BenchmarkCpuFactor' in mork and 'BenchmarkGpuFactor' in mork
assert 'Mathf.Min(80f, burnTemp - 20f)' in cpu
assert 'public float Temperature' in gpu
assert 'public float RamTemperature' in os and 'оценка' in task
# Case 3: actual running apps + four system tasks, process termination policy.
assert 'RunningProcesses()' in os and 'system.RunningProcesses()' in task
for name in ('PCOS.exe','Drivers.dll','sys.dll','shell.exe'):
    assert name in os and name in task
assert re.search(r'process == "PCOS.exe".*?process == "Drivers.dll".*?process == "sys.dll"', os, re.S)
assert 'if (process == "shell.exe")' in os and 'background.enabled = false' in os
assert 'iconParent.gameObject.SetActive(false)' in os and 'taskbar.SetActive(false)' in os
assert 'app.Close(); return;' in os and 'Fault(); return;' in os
assert 'if (background != null) background.enabled = true;' in os
assert 'if (!ShellRunning) return;' in os
assert 'desktop.gameObject.SetActive(false)' not in os[os.index('public void EndProcess'):os.index('public void RegisterWork')]
# Case 4: installed on both existing/new desktops; prefab native chrome and sprite imports.
assert 'EnsureTaskManager();' in os and 'Task Manager.exe' in os
assert 'appName: Task Manager' in pref and 'm_Text: Task Manager' in pref
assert 'm_SizeDelta: {x: 720, y: 460}' in pref
assert 'm_MethodName: Close' in pref and 'm_MethodName: Minimize' in pref
assert 'm_MethodName: Maximize' in pref
assert 'Processes page' in task and 'Performance page' in task
assert 'ScrollRect' in task and 'RectMask2D' in task
assert 'PerformanceGraph[] graphs' in task and 'graphs[i].Sample(usage[i])' in task
chart = (comp/'Software/PerformanceGraph.cs').read_text()
assert 'private const int Samples = 72' in chart
assert 'protected override void OnPopulateMesh(VertexHelper mesh)' in chart
assert 'Color lower' in chart and 'Color upper' in chart and 'Color grid' in chart
meta = comp/'Software/TaskManager.cs.meta'
guid = re.search(r'(?m)^guid: ([0-9a-f]{32})$', meta.read_text()).group(1)
assert f'guid: {guid}' in pref
imeta = root/'Assets/Texture2D/TaskManager.png.meta'
iguid = re.search(r'(?m)^guid: ([0-9a-f]{32})$', imeta.read_text()).group(1)
assert f'guid: {iguid}' in pref and (imeta.with_suffix('')).exists()
# Unity multi-document YAML with tags stripped is valid, no duplicate fileIDs.
doc = re.sub(r'!u!\d+', '', pref)
assert len(list(yaml.compose_all(doc))) >= 20
ids = re.findall(r'^--- !u!\d+ &(\d+)$', pref, re.M)
assert len(ids) == len(set(ids))
for path in ('LuaDocs_EN.txt', 'LuaDocs_RU.txt'):
    text = (root/'Assets/Resources'/path).read_text()
    assert ('all other languages use English' not in text and
            'при остальных — английская' not in text)
print('PASS: task manager, native window, process termination, mining priority and telemetry')
