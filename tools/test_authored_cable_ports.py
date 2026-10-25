#!/usr/bin/env python3
"""Static prefab hierarchy/port-copy guards. Unity Play Mode is still required."""
from pathlib import Path
import re
from replicate_authored_cable_ports import (ROOT, FAMILIES, PORT_GUID, blocks,
                                             root_and_owner, authored_ports,
                                             reference, field, child_transforms)


def validate(name):
    path = ROOT / (name + '.prefab')
    text = path.read_text()
    docs = blocks(text)
    assert len(docs) == len(re.findall(r'^--- !u!', text, re.M)), name
    root, owner = root_and_owner(docs, name)
    for fid, (kind, doc) in docs.items():
        for ref in re.findall(r'\{fileID: (-?\d+)\}', doc):
            if ref != '0':
                assert ref in docs, (name, fid, ref)
        if kind == 1:
            for component in re.findall(r'^  - component: \{fileID: (-?\d+)\}$', doc, re.M):
                assert component in docs and reference(docs[component][1], 'm_GameObject') == fid, (name, fid, component)
        if kind not in (4, 224) or not re.search(r'^  m_Father:', doc, re.M):
            continue  # stripped prefab-instance components have no local hierarchy
        parent = reference(doc, 'm_Father')
        if parent != '0':
            assert parent in docs and docs[parent][0] in (4, 224), (name, fid, parent)
            assert '{fileID: ' + fid + '}' in field_children(docs[parent][1]), (name, fid, parent)
        else:
            assert fid == root, (name, fid, root)
    for fid, (kind, doc) in docs.items():
        if kind != 1: continue
        for component in re.findall(r'^  - component: \{fileID: (-?\d+)\}$', doc, re.M):
            assert component in docs, (name, fid, component)
    return docs, root, owner


def field_children(doc):
    m = re.search(r'^  m_Children:(.*?)(?=^  m_Father:)', doc, re.M | re.S)
    assert m, 'missing m_Children'
    return m.group(1)


for template, targets in FAMILIES.items():
    source, _, _ = validate(template)
    source_ports = authored_ports(source)
    assert source_ports, template
    for target in targets:
        docs, root, owner = validate(target)
        target_ports = authored_ports(docs)
        for name, (source_go, source_port) in source_ports.items():
            assert name in target_ports, (template, target, name)
            port_go, port = target_ports[name]
            src = source[source_port][1]
            dst = docs[port][1]
            assert field(src, 'kind') == field(dst, 'kind'), (template, target, name)
            assert reference(dst, 'owner') == owner, (target, name)
            tr = next(fid for fid in re.findall(r'^  - component: \{fileID: (-?\d+)\}$', docs[port_go][1], re.M)
                      if docs[fid][0] in (4, 224))
            src_tr = next(fid for fid in re.findall(r'^  - component: \{fileID: (-?\d+)\}$', source[source_go][1], re.M)
                          if source[fid][0] in (4, 224))
            assert reference(docs[tr][1], 'm_Father') == root, (target, name)
            for key in ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale'):
                assert field(docs[tr][1], key) == field(source[src_tr][1], key), (template, target, name, key)
            src_pivot = reference(src, 'snapPivot')
            pivot = reference(dst, 'snapPivot')
            assert reference(docs[pivot][1], 'm_Father') == tr, (target, name)
            for key in ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale'):
                assert field(docs[pivot][1], key) == field(source[src_pivot][1], key), (template, target, name, key)
            sphere = next(fid for fid in re.findall(r'^  - component: \{fileID: (-?\d+)\}$', docs[port_go][1], re.M)
                          if docs[fid][0] == 135)
            assert field(docs[sphere][1], 'm_IsTrigger') == '1', (target, name)
            assert field(docs[port_go][1], 'm_Layer') == '2', (target, name)
            def marker_children(entries, parent, label):
                matches = []
                for child in child_transforms(entries[parent][1]):
                    body = entries[child][1]
                    if '  m_GameObject:' not in body: continue
                    go = reference(body, 'm_GameObject')
                    if field(entries[go][1], 'm_Name') == label:
                        matches.append((child, go))
                return matches
            for marker in ('Conector', 'Cube'):
                source_marker = marker_children(source, src_tr, marker)
                if not source_marker: continue
                result = marker_children(docs, tr, marker)
                assert len(source_marker) == len(result) == 1, (template, target, name, marker)
                src_marker, src_go = source_marker[0]
                dst_marker, dst_go = result[0]
                for key in ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale'):
                    assert field(source[src_marker][1], key) == field(docs[dst_marker][1], key), (target, marker, key)
                def components(entries, go):
                    return [entries[fid][0] for fid in re.findall(
                        r'^  - component: \{fileID: (-?\d+)\}$', entries[go][1], re.M)]
                assert components(source, src_go) == components(docs, dst_go), (target, marker)
            if template == 'ATX':
                # ATX indicator is an FBX prefab instance under Snap Pivot.
                children = child_transforms(docs[pivot][1])
                assert len(children) == 1 and ' stripped' in docs[children[0]][1].splitlines()[0], target
                instance = reference(docs[children[0]][1], 'm_PrefabInstance')
                assert 'guid: e09ef6a9ef036134bbd0d8ba3ea4cc55' in docs[instance][1], target
                parent = re.search(r'^    m_TransformParent: \{fileID: (-?\d+)\}$',
                                   docs[instance][1], re.M)
                assert parent and parent.group(1) == pivot, target

# The mounting ray must point behind the authored socket face for all three
# outlet variants; their trigger/pivot geometry remains untouched.
for sockets in (1, 2, 3):
    text = (ROOT / f'Wall Outlet {sockets}.prefab').read_text()
    assert '  hookOffset: {x: 0, y: 0, z: 0.07}' in text
    assert '  hookDirection: {x: 0, y: 0, z: 1}' in text
    assert text.count('guid: ' + PORT_GUID) == sockets

# Floor/ceiling contacts cannot mount a wall outlet; generic Hook behaviour
# for other items is unchanged. RTX 2080/2080Ti and unique samples stay authored.
hook = (ROOT.parents[1] / 'Scripts/Assembly-CSharp/Hook.cs').read_text()
assert 'GetComponent<PC.Component.Cabling.OutletStrip>() != null' in hook
assert 'Mathf.Abs(hit.normal.y) > .35f' in hook
assert 'if (savedHooked && GetComponent<PC.Component.Cabling.OutletStrip>() != null)' in hook
assert 'savedHooked = IsWall();' in hook  # reject old floor-mounted outlets on load
assert 'hit.transform.CompareTag("Wall") || (hit.collider.attachedRigidbody == null' in hook
print('Authored cable ports and prefab hierarchy: OK')
