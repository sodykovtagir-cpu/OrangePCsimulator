#!/usr/bin/env python3
"""Dream PC shop spec, price, localized description and power-budget guard."""
from pathlib import Path
import importlib.util
import re

ROOT = Path(__file__).resolve().parents[1]
COMP = ROOT / 'Assets/Resources/components'
GUID = 'dceea9be82b729f43ad08523eaef8b21'
PART_REF = 'prefab: {fileID: 5024337473304829655, guid: ' + GUID + ', type: 3}'
OLD_GPU = '8cedbac4d3d448e4ab65da578350baca'
assert 'guid: ' + GUID in (COMP / 'RTX5090.prefab.meta').read_text()

source = (ROOT / 'tools/generate_ready_builds.py').read_text()
body = source.split('def dream_pc(', 1)[1].split('def miner_cheap(', 1)[0]
assert body.count('p("RTX5090", "GPU", host=board)') == 2
assert 'p("RTX4080Ti", "GPU", host=board)' not in body

spec = importlib.util.spec_from_file_location('ready_reader', ROOT / 'tools/ready_pc_reference.py')
reader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reader)
lookup = reader.guid_map()
for suffix in ('Black', 'White'):
    prefab = COMP / 'ready' / f'ReadyPC_Dream_{suffix}.prefab'
    text = prefab.read_text()
    # Match both real GPU slots, not a preceding part's prefab line.
    gpus = re.findall(r'^  - (prefab: \{[^\n]+\})\n'
                      r'(?:(?!^  - prefab: |^  stepDelay: ).*\n)*?'
                      r'    slotTarget: GPU$', text, re.M)
    assert gpus == [PART_REF, PART_REF], (suffix, gpus)
    assert OLD_GPU not in text, suffix
    part_rows = re.findall(r'^  - prefab: \{fileID: \d+, guid: ([a-f0-9]{32}), type: 3\}\n'
                           r'(?:(?!^  - prefab: |^  stepDelay: ).*\n)*?'
                           r'    slotTarget: ([^\n]+)$', text, re.M)
    watts = sum(reader.prefab_number(lookup[guid], 'wattage', lookup)
                for guid, slot in part_rows if slot in ('CPU', 'Cooler', 'GPU', 'RAM', 'Drive', 'Output'))
    supply = sum(reader.prefab_number(lookup[guid], 'wattage', lookup)
                 for guid, slot in part_rows if slot == 'Supply')
    assert 0 < watts <= supply, (suffix, watts, supply)
    shop = (ROOT / 'Assets/MonoBehaviour/Ready' / f'ReadyPC_Dream_{suffix}.asset').read_text()
    assert '  price: 21155\n' in shop, suffix

row = next(r for r in (ROOT / 'Assets/Resources/Translate.txt').read_text().splitlines()
           if r.startswith('Dream PC Description\t'))
assert len(row.split('\t')) == 43
assert row.count('RTX 5090 + RTX 5090') == 42
assert 'RTX 4080 Ti' not in row
assert row.count(' / 1.1 kW / MSG ATX / Aquarium') == 42
source = (ROOT / 'tools/add_ready_build_i18n.py').read_text()
assert 'RTX 5090 + RTX 5090' in source
spec_i18n = importlib.util.spec_from_file_location('ready_i18n', ROOT / 'tools/add_ready_build_i18n.py')
i18n = importlib.util.module_from_spec(spec_i18n)
spec_i18n.loader.exec_module(i18n)
assert row.split('\t')[1:] == [i18n.SPECS['Dream PC Description']] * 42
print('PASS: Dream Black/White = RTX 5090 + RTX 5090, 1146W <= 1200W, shop spec and price')
