#!/usr/bin/env python3
"""Inspect Ready PC shop prefabs, not a duplicate handwritten benchmark roster.

Unity C# reads the same ReadyBuildSpawner.parts at runtime. This Python reader
checks the shipped YAML and is used only by static tests/reporting.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
COMPONENTS = ROOT / 'Assets/Resources/components'
READY = COMPONENTS / 'ready'
MODELS = ('Dream', 'Workstation', 'Aquarium', 'Gaming', 'Home', 'Office')


def guid_map():
    result = {}
    for meta in COMPONENTS.glob('*.prefab.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(), re.M)
        if match:
            result[match.group(1)] = Path(str(meta)[:-5])
    return result


def prefab_number(path, name, lookup, seen=None):
    """Read a direct or variant override for a Hardware component property."""
    seen = set() if seen is None else seen
    if path in seen:
        raise ValueError(f'Prefab inheritance loop: {path}')
    seen.add(path)
    text = path.read_text()
    direct = re.search(r'^  ' + name + r': ([\d.]+)$', text, re.M)
    override = re.search(r'^      propertyPath: ' + name + r'\n      value: ([\d.]+)$', text, re.M)
    if override:
        return float(override.group(1))
    if direct:
        return float(direct.group(1))
    source = re.search(r'^  m_SourcePrefab: \{fileID: \d+, guid: ([a-f0-9]{32}),', text, re.M)
    if source and source.group(1) in lookup:
        return prefab_number(lookup[source.group(1)], name, lookup, seen)
    raise ValueError(f'{name} not found in {path} or its source')


def pretty(name):
    name = name.removeprefix('CPU ')
    m = re.fullmatch(r'([A-Za-z]{2,4})(\d{2,4})([A-Za-z]{0,3})', name)
    return (m[1] + ' ' + m[2] + (' ' + m[3] if m[3] else '')) if m else name


def read_builds():
    lookup = guid_map()
    result = []
    for model in MODELS:
        path = READY / f'ReadyPC_{model}_Black.prefab'
        text = path.read_text()
        parts = re.findall(r'^  - prefab: \{fileID: \d+, guid: ([a-f0-9]{32}), type: 3\}\n'
                           r'(?:(?!^  - prefab: |^  stepDelay: ).*\n)*?'
                           r'    slotTarget: (GPU|CPU|RAM|Drive)$', text, re.M)
        if not parts:
            raise ValueError(f'No benchmark parts in {path}')
        result_entry = {'pc': model + ' PC', 'cpu': '', 'gpu': '', 'gpuScore': 0,
                        'cpuScore': 0.0, 'ramScore': 0, 'driveScore': 0}
        gpus = []
        for guid, kind in parts:
            if guid not in lookup:
                raise ValueError(f'Missing component {guid} ({model}/{kind})')
            component = lookup[guid]
            score = prefab_number(component, 'score', lookup)
            if kind == 'GPU':
                result_entry['gpuScore'] += int(score)
                gpus.append(pretty(component.stem))
            elif kind == 'CPU':
                freq = prefab_number(component, 'frequency', lookup)
                factor = .70 + .30 * min(1.2, max(.5, freq / 3.5))
                result_entry['cpuScore'] += score * factor
                result_entry['cpu'] = pretty(component.stem)
            elif kind == 'RAM':
                result_entry['ramScore'] += int(score)
            elif kind == 'Drive':
                result_entry['driveScore'] += int(score)
        if not gpus:
            result_entry['gpuScore'] = 200
            result_entry['gpu'] = 'Integrated'
        else:
            result_entry['gpu'] = ' + '.join(gpus)
        if not result_entry['cpu'] or not result_entry['ramScore']:
            raise ValueError(f'Incomplete {model} PC')
        result.append(result_entry)
    return result


if __name__ == '__main__':
    for entry in read_builds():
        print(entry)
