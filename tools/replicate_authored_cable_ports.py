#!/usr/bin/env python3
"""Copy hand-authored CablePort trigger/pivot geometry to agreed prefab families.

Idempotent: existing CablePorts are never replaced. Copies only the trigger,
CablePort and Snap Pivot, not the source device's decorative socket mesh.
Run when reference prefabs have been authored; if a target already has a port,
edit that target manually instead of expecting this script to overwrite it.
Unity Play Mode is still needed to visually confirm fit on different models.
"""
from pathlib import Path
import hashlib
import re

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/components'
PORT_GUID = '1f65810bece34620913cb00d28b91845'
# RTX 3080, not RTX 3090, is the authored RTX reference in this project.
FAMILIES = {
    'RTX3080': ('RTX3080Ti',),  # 2080/2080Ti already have authored ports
    'GTX1060': ('GTX1070', 'GTX1070Ti', 'GTX1080', 'GTX1080Ti', 'Titan V'),
    'GT1030': ('GT1031',),
    'PSU 300W': ('PSU 500W', 'PSU 1kW', 'PSU 1.1kW', 'KSAS PSU'),
    'ATX': ('ATX 2', 'EATX', 'EXATX', 'Micro_ATX', 'MSG_ATX_Black', 'MSG_ATX_White'),
    'Monitor(Black)': ('Monitor(White)',),
    'Monitor 2(White)': ('Monitor 2(Blue)', 'Monitor 2(Green)',
                         'Monitor 2(Red)', 'Monitor 2(Yellow)'),
}
HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?$', re.M)
REFERENCE = re.compile(r'\{fileID: (-?\d+)\}')


def blocks(text):
    starts = list(HEADER.finditer(text))
    return {m.group(2): (int(m.group(1)), text[m.start():starts[i+1].start() if i+1 < len(starts) else len(text)])
            for i, m in enumerate(starts)}


def field(text, key):
    found = re.search(r'^  ' + re.escape(key) + r': (.*)$', text, re.M)
    if found is None:
        raise ValueError('missing ' + key)
    return found.group(1)


def reference(text, key):
    found = re.fullmatch(r'\{fileID: (-?\d+)\}', field(text, key))
    if found is None:
        raise ValueError('bad reference ' + key)
    return found.group(1)


def root_and_owner(docs, name):
    roots = [(fid, text) for fid, (cid, text) in docs.items()
             if cid in (4, 224) and re.search(r'^  m_Father: \{fileID: 0\}$', text, re.M)]
    if len(roots) != 1:
        raise ValueError(name + ': expected one prefab root')
    root_id, root_text = roots[0]
    go_id = reference(root_text, 'm_GameObject')
    components = re.findall(r'^  - component: \{fileID: (-?\d+)\}$', docs[go_id][1], re.M)
    owners = [fid for fid in components if fid in docs and
              re.search(r'^  spawnId: (.*)$', docs[fid][1], re.M)]
    if len(owners) != 1:
        raise ValueError(name + ': expected exactly one root Item')
    return root_id, owners[0]


def authored_ports(docs):
    result = {}
    for fid, (cid, text) in docs.items():
        if cid != 114 or f'guid: {PORT_GUID}' not in text:
            continue
        go_id = reference(text, 'm_GameObject')
        go_text = docs[go_id][1]
        result[field(go_text, 'm_Name')] = (go_id, fid)
    return result


def new_ids(used, target, name):
    ids = []
    for part in ('go', 'transform', 'trigger', 'port', 'pivot-go', 'pivot-transform'):
        salt = 0
        while True:
            digest = hashlib.sha256(f'{target}/{name}/{part}/{salt}'.encode()).digest()
            fid = str(int.from_bytes(digest[:8], 'big') % (2**62 - 1) + 10**16)
            if fid not in used:
                used.add(fid)
                ids.append(fid)
                break
            salt += 1
    return ids


def clone_doc(doc, old_id, new_id, mappings):
    result = re.sub(r'(?m)^--- !u!(\d+) &' + re.escape(old_id) + r'(?=\s*$)',
                    lambda m: '--- !u!' + m.group(1) + ' &' + new_id, doc, count=1)
    if result == doc:
        raise ValueError('missing document header ' + old_id)
    result = REFERENCE.sub(lambda m: '{fileID: ' + mappings.get(m.group(1), m.group(1)) + '}', result)
    # Unity source YAML often has blank scalar fields with a trailing space.
    # Avoid introducing new whitespace errors in the copied blocks.
    return re.sub(r'(?m) +$', '', result)


def replicate(source, target):
    source_text = (ROOT / (source + '.prefab')).read_text()
    path = ROOT / (target + '.prefab')
    text = path.read_text()
    src = blocks(source_text)
    dst = blocks(text)
    src_root, src_owner = root_and_owner(src, source)
    dst_root, dst_owner = root_and_owner(dst, target)
    if reference(dst[dst_owner][1], 'm_GameObject') != reference(dst[dst_root][1], 'm_GameObject'):
        raise ValueError(target + ': root Item not on root GameObject')
    source_script = field(src[src_owner][1], 'm_Script')
    target_script = field(dst[dst_owner][1], 'm_Script')
    if source_script != target_script:
        raise ValueError(f'{source} and {target}: different Item types')
    old = authored_ports(src)
    existing = authored_ports(dst)
    used = set(dst)
    appended = []
    added = []
    root_text = dst[dst_root][1]
    for name, (go, port) in old.items():
        if name in existing:
            continue  # preserve all ports already manually authored in target
        if re.search(r'^  m_Name: ' + re.escape(name) + r'$', text, re.M):
            raise ValueError(f'{target}: {name} exists without CablePort; inspect manually')
        go_doc = src[go][1]
        comps = re.findall(r'^  - component: \{fileID: (-?\d+)\}$', go_doc, re.M)
        transforms = [fid for fid in comps if src[fid][0] in (4, 224)]
        colliders = [fid for fid in comps if src[fid][0] == 135]
        if len(comps) != 3 or len(transforms) != 1 or len(colliders) != 1 or port not in comps:
            raise ValueError(f'{source}/{name}: expected transform, sphere, CablePort only')
        tr, sphere = transforms[0], colliders[0]
        if reference(src[tr][1], 'm_Father') != src_root:
            raise ValueError(f'{source}/{name}: port is not a direct child of the Item')
        pivot_tr = reference(src[port][1], 'snapPivot')
        pivot_go = reference(src[pivot_tr][1], 'm_GameObject')
        if reference(src[pivot_tr][1], 'm_Father') != tr:
            raise ValueError(f'{source}/{name}: Snap Pivot is not a child of the port')
        if re.findall(r'^  - component: \{fileID: (-?\d+)\}$', src[pivot_go][1], re.M) != [pivot_tr]:
            raise ValueError(f'{source}/{name}: pivot contains extra components')
        replacements = dict(zip((go, tr, sphere, port, pivot_go, pivot_tr),
                                new_ids(used, target, name)))
        replacements[src_owner] = dst_owner
        replacements[src_root] = dst_root
        # Do not copy the decorative socket model: the target has its own mesh.
        for fid in (go, tr, sphere, port, pivot_go, pivot_tr):
            doc = clone_doc(src[fid][1], fid, replacements[fid], replacements)
            if fid == tr:
                doc, count = re.subn(r'(?m)^  m_Children:(?: \[\]\n|\n(?:  - \{fileID: -?\d+\}\n)*)',
                                     '  m_Children:\n  - {fileID: ' + replacements[pivot_tr] + '}\n', doc, count=1)
                if count != 1:
                    raise ValueError(source + '/' + name + ': missing child list')
            elif fid == pivot_tr:
                # A designer's pivot can itself contain marker meshes; only
                # its transform belongs to the portable socket geometry.
                doc, count = re.subn(r'(?m)^  m_Children:(?: \[\]\n|\n(?:  - \{fileID: -?\d+\}\n)*)',
                                     '  m_Children: []\n', doc, count=1)
                if count != 1:
                    raise ValueError(source + '/' + name + ': missing pivot child list')
            appended.append(doc)
        if '  m_Children: []\n' in root_text:
            root_text = root_text.replace('  m_Children: []\n',
                                          '  m_Children:\n  - {fileID: ' + replacements[tr] + '}\n', 1)
        else:
            root_text, count = re.subn(r'(?m)^  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*',
                                      lambda m: m.group(0) + '  - {fileID: ' + replacements[tr] + '}\n',
                                      root_text, count=1)
            if count != 1:
                raise ValueError(target + ': missing root children')
        added.append(name)
    if not added:
        return []
    text = text.replace(dst[dst_root][1], root_text, 1)
    path.write_text(text.rstrip() + '\n' + ''.join(appended))
    return added


def main():
    for source, targets in FAMILIES.items():
        for target in targets:
            names = replicate(source, target)
            print(f'{source} -> {target}: ' + (', '.join(names) if names else 'already authored'))


if __name__ == '__main__':
    main()
