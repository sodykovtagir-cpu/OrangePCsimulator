#!/usr/bin/env python3
"""Report 3DMork reference PCs from the same ReadyPC shop prefabs used by Unity.

There is no second handwritten list of GPU/CPU/RAM/drive configurations.
Run: python3 tools/3dmork_builds.py
"""
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent


def load(name):
    spec = importlib.util.spec_from_file_location(name, HERE / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


model = load('3dmork_score_model')
ready = load('ready_pc_reference')


def reference_benchmarks(fps_cap=240.0):
    return model.reference_benchmarks(fps_cap)


def main():
    rows = reference_benchmarks()
    if len(rows) != len(ready.MODELS) or {r['pc'] for r in rows} != {m + ' PC' for m in ready.MODELS}:
        raise SystemExit('Missing Ready PC prefab in comparison table')
    for row in rows:
        result = model.scores(row['gpuScore'], row['cpuScore'], row['ramScore'],
                              row['driveScore'], 240.0)
        print('%-18s %-19s %-28s G %6d P %8.1f M %6d S %6d -> %d' %
              (row['pc'], row['cpu'], row['gpu'], row['gpuScore'],
               row['cpuScore'], row['ramScore'], row['driveScore'], result['total']))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
