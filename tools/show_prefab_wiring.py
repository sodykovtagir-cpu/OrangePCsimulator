"""Prints the resolved wiring of the 3DMork app script inside the prefab."""

import re
import sys

SCRIPT_GUID = "48ab01ab22fd4bc78201ca8ad4b6d729"


def parse(path):
    text = open(path, encoding="utf-8").read()
    parts = re.split(r"^--- !u!(\d+) &(\d+)(?: stripped)?\n", text, flags=re.M)
    blocks = {}
    for i in range(1, len(parts) - 2, 3):
        blocks[parts[i + 1]] = (int(parts[i]), parts[i + 2])
    return blocks


def main(path):
    blocks = parse(path)

    names = {}
    for fid, (cid, body) in blocks.items():
        if cid == 1:
            match = re.search(r"^  m_Name: (.*)$", body, flags=re.M)
            names[fid] = match.group(1).strip() if match else "?"

    def describe(fid):
        if fid == "0":
            return "None"
        if fid not in blocks:
            return "MISSING:%s" % fid
        cid, body = blocks[fid]
        owner = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", body, flags=re.M)
        owner_name = names.get(owner.group(1), "?") if owner else "?"
        kind = {1: "GameObject", 114: "Component", 224: "RectTransform"}.get(cid, str(cid))
        if cid == 114:
            script = re.search(r"guid: ([0-9a-f]+)", body)
            names_map = {
                "fe87c0e1cc204ed48ad3b37840f39efc": "Image",
                "5f7201a12d95ffc409449d95f23cf332": "Text",
                "4e29b1a8efbd4b44bb3f3716e73f07ff": "Button",
                "67db9e8f0e2ae9c40bc1e2b64352a6b4": "Slider",
                "1344c3c82d62a2a41a3576d8abb8e3ea": "RawImage",
            }
            kind = names_map.get(script.group(1), "MonoBehaviour")
        return "%s %s" % (kind, owner_name)

    target = None
    for fid, (cid, body) in blocks.items():
        if cid == 114 and SCRIPT_GUID in body:
            target = fid
            break
    if target is None:
        print("script not found")
        return 1

    body = blocks[target][1]
    body = body.split("  m_EditorClassIdentifier:", 1)[1]
    field = None
    for line in body.split("\n"):
        if line.startswith("  - {fileID:"):
            ref = re.search(r"\{fileID: (\d+)\}", line).group(1)
            print("    - %-20s -> %s" % (field, describe(ref)))
        elif re.match(r"^  [A-Za-z_][A-Za-z0-9_]*:", line):
            field = line.split(":")[0].strip()
            ref = re.search(r"\{fileID: (\d+)\}", line)
            if ref:
                print("  %-22s -> %s" % (field, describe(ref.group(1))))
            else:
                print("  %-22s = %s" % (field, line.split(":", 1)[1].strip()[:40]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
