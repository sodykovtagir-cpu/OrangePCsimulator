"""Validates a Unity prefab/scene YAML file: structure, references and hierarchy."""

import re
import sys
from collections import defaultdict


def load_blocks(path):
    with open(path, encoding="utf-8") as handle:
        text = handle.read()

    docs = re.split(r"^--- !u!(\d+) &(\d+)(?: stripped)?\s*$", text, flags=re.M)
    blocks = []
    # docs[0] is the header, then triples of (classId, fileId, body)
    for i in range(1, len(docs) - 2, 3):
        class_id = docs[i]
        file_id = docs[i + 1]
        body = docs[i + 2]
        blocks.append((int(class_id), file_id, body))
    return blocks


def main(path):
    blocks = load_blocks(path)
    ids = {}
    for class_id, file_id, body in blocks:
        if file_id in ids:
            print("FAIL duplicate fileID %s" % file_id)
            return 1
        ids[file_id] = (class_id, body)

    errors = 0
    gos = {fid: body for fid, (cid, body) in ids.items() if cid == 1}
    rects = {fid: body for fid, (cid, body) in ids.items() if cid == 224}

    # 1. every component listed on a GameObject must exist and point back at it
    for go_id, body in gos.items():
        for comp in re.findall(r"^  - component: \{fileID: (\d+)\}$", body, flags=re.M):
            if comp not in ids:
                print("FAIL GameObject %s references missing component %s" % (go_id, comp))
                errors += 1
                continue
            owner = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", ids[comp][1], flags=re.M)
            if not owner or owner.group(1) != go_id:
                print("FAIL component %s is not owned by GameObject %s" % (comp, go_id))
                errors += 1

    # 2. hierarchy consistency
    for tr_id, body in rects.items():
        go = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", body, flags=re.M)
        if not go or go.group(1) not in gos:
            print("FAIL RectTransform %s has no valid GameObject" % tr_id)
            errors += 1
            continue
        if ("  - component: {fileID: %s}" % tr_id) not in gos[go.group(1)]:
            print("FAIL RectTransform %s missing from GameObject %s component list" % (tr_id, go.group(1)))
            errors += 1
        father = re.search(r"^  m_Father: \{fileID: (\d+)\}$", body, flags=re.M)
        if not father:
            print("FAIL RectTransform %s has no m_Father" % tr_id)
            errors += 1
            continue
        father_id = father.group(1)
        if father_id != "0":
            if father_id not in rects:
                print("FAIL RectTransform %s has unknown parent %s" % (tr_id, father_id))
                errors += 1
                continue
            if ("  - {fileID: %s}" % tr_id) not in ids[father_id][1]:
                print("FAIL parent %s does not list child %s" % (father_id, tr_id))
                errors += 1

    # 3. every fileID mentioned anywhere must exist
    known = set(ids.keys())
    for file_id, (class_id, body) in ids.items():
        for ref in re.findall(r"\{fileID: (\d+)\}", body):
            if ref != "0" and ref not in known:
                print("FAIL %s references unknown fileID %s" % (file_id, ref))
                errors += 1
        for ref in re.findall(r"^  - \{fileID: (\d+)\}$", body, flags=re.M):
            if ref not in known:
                print("FAIL %s lists unknown child %s" % (file_id, ref))
                errors += 1

    # 4. no cycles
    graph = defaultdict(list)
    for tr_id, body in rects.items():
        father = re.search(r"^  m_Father: \{fileID: (\d+)\}$", body, flags=re.M)
        if father and father.group(1) != "0":
            graph[father.group(1)].append(tr_id)
    roots = [t for t, b in rects.items()
             if re.search(r"^  m_Father: \{fileID: 0\}$", b, flags=re.M)]
    seen = set()

    def walk(node):
        if node in seen:
            print("FAIL cycle at %s" % node)
            errors += 1
            return
        seen.add(node)
        for child in graph.get(node, []):
            walk(child)

    for root in roots:
        walk(root)
    for tr_id in rects:
        if tr_id not in seen:
            print("FAIL RectTransform %s is not reachable from a root" % tr_id)
            errors += 1

    print("%s: %d GameObjects, %d components, %d errors"
          % (path, len(gos), len(blocks) - len(gos), errors))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
