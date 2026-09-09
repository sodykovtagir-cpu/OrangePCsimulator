#!/usr/bin/env python3
"""Headless desktop regression tests (not a Unity Play Mode/rendering test).

Requires mcs/mono and Python packages pyyaml, tree-sitter, tree-sitter-c-sharp.
Run: python3 tools/test_desktop_layout.py

Compile the real grid/dragger plus desktop methods extracted verbatim from the OS.
Only unrelated Unity rendering/game services are substituted by small test stubs.
"""
from pathlib import Path
import re
import subprocess
import tempfile

import yaml
import tree_sitter_c_sharp
from tree_sitter import Language, Parser

ROOT = Path(__file__).resolve().parents[1]
CS = ROOT / "Assets/Scripts/Assembly-CSharp"
PARSER = Parser(Language(tree_sitter_c_sharp.language()))


def walk(node):
    yield node
    for child in node.children:
        yield from walk(child)


def source_members(path):
    tree = PARSER.parse(path.read_bytes())
    assert not tree.root_node.has_error, f"C# parse error: {path}"
    return list(walk(tree.root_node))


def generate_os_fixture():
    nodes = source_members(CS / "PC/Component/Software/OS/OperatingSystem.cs")
    wanted_methods = set("""
        GetIconPositionsKey GetIconSortModeKey GetIconGridColumnsKey GetDesktopIconGrid
        EnsureIconLayoutLoaded ResetIconGridColumns LoadCurrentSortMode SaveCurrentSortMode
        PersistIconPositions ParseIconPositions SaveIconPosition LoadIconPositions
        GetDesktopCanvas GetFullscreenDesktopSize GetScaledDesktopSize GetLegacyMonitorDesktopSize
        FindFreeSpawnPosition AutoArrangeIcons SortDesktopIcons RefreshDesktopIcon CollectDesktopFiles
        AddFileIcon OpenDesktopFile IsIconSpawnOccupied Update LateUpdate
        TransferRenamedIconPosition ReserveCanonicalIconCells UpdateDesktopIconView
        RequestFileRefresh GetDesktopStorageFiles RefreshChangedFiles
    """.split())
    wanted_fields = set("""
        fileIconPrefab iconParent desktop folderSprite fileIcons iconPositions desktopFileKeys
        iconLayoutInitialized iconGridColumns SharedIconLayoutContext DefaultSortMode currentSortMode clockText
        fullscreenIconPositions iconViewInitialized iconViewDirty lastIconViewFullscreen lastIconViewCanvas
        lastIconViewportSize lastIconViewportPadding desktopFileSnapshot fileRefreshPending nextFileSystemScan FileSystemScanInterval
    """.split())
    members, found_methods, found_fields = [], set(), set()
    for node in nodes:
        if node.type == "method_declaration":
            name = node.child_by_field_name("name").text.decode()
            if name in wanted_methods:
                members.append(node.text.decode())
                found_methods.add(name)
        elif node.type == "field_declaration":
            names = {n.child_by_field_name("name").text.decode() for n in walk(node)
                     if n.type == "variable_declarator"}
            if names & wanted_fields:
                assert names <= wanted_fields
                members.append(node.text.decode())
                found_fields |= names
        elif node.type == "property_declaration" and node.child_by_field_name("name").text == b"SystemId":
            members.append(node.text.decode())
    assert found_methods == wanted_methods, wanted_methods - found_methods
    assert found_fields == wanted_fields, wanted_fields - found_fields
    return """
using System;
using System.Collections.Generic;
using PC.Component;
using UnityEngine;
using UnityEngine.UI;
namespace PC.Component.Software.OS {
public class OperatingSystem : ComputerSystem {
    public bool Ready = true;
    public int FileViewRefreshCount;
    public int ErrorMessageCount;
    public Action OnRefreshFileViews;
    public void ShowMessageBox(string title, string message) { ErrorMessageCount++; }
    private void RefreshRunningFileManagers() { FileViewRefreshCount++; if (OnRefreshFileViews != null) OnRefreshFileViews(); }
    private Sprite GetFileSprite(string path) { return null; }
    private void OpenFolder(File file) { }
    private void OpenFile(File file) { }
""" + "\n\n".join(members) + "\n}\n}\n"


def generate_storage_fixture():
    wanted = {"Usage", "Write", "AddFile", "ContainsFile", "TryGetFile"}
    members = []
    for node in source_members(CS / "PC/Component/Storage.cs"):
        if node.type == "method_declaration" and node.child_by_field_name("name").text.decode() in wanted:
            members.append(node.text.decode())
    assert len(members) == len(wanted)
    return """
using System.Collections.Generic;
using System.Linq;
using PC.Component.Software;
namespace PC.Component {
public class Storage {
    public int Capacity = 100000000;
    public List<File> files = new List<File>();
""" + "\n\n".join(members) + "\n}\n}\n"


def parse_prefab(path):
    docs = {}
    for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)", path.read_text(), re.M | re.S):
        fid = int(m[2])
        assert fid not in docs, f"Duplicate fileID {fid} in {path}"
        docs[fid] = (int(m[1]), next(iter(yaml.safe_load(m[3]).values())))
    return docs


def references(value):
    if isinstance(value, dict):
        if "fileID" in value:
            yield value
        for child in value.values():
            yield from references(child)
    elif isinstance(value, list):
        for child in value:
            yield from references(child)


def validate_prefabs():
    pcos = parse_prefab(ROOT / "Assets/GameObject/PCOS.prefab")
    icon = parse_prefab(ROOT / "Assets/GameObject/FileIcon.prefab")
    for docs in [pcos, icon]:
        for fid, (cls, body) in docs.items():
            for ref in references(body):
                if ref["fileID"] and "guid" not in ref:
                    assert ref["fileID"] in docs, (fid, ref)
            if cls == 1:
                assert docs[body["m_Component"][0]["component"]["fileID"]][0] in (4, 224)
                for component in body["m_Component"]:
                    assert docs[component["component"]["fileID"]][1]["m_GameObject"]["fileID"] == fid
            if cls in (4, 224):
                for child in body["m_Children"]:
                    assert docs[child["fileID"]][1]["m_Father"]["fileID"] == fid
    icons_rect = pcos[224694525888043024][1]
    assert icons_rect["m_Pivot"] == {"x": 0, "y": 1}
    assert icons_rect["m_AnchorMin"] == {"x": 0, "y": 0}
    assert icons_rect["m_AnchorMax"] == {"x": 1, "y": 1}
    assert icons_rect["m_SizeDelta"] == {"x": 0, "y": 0}
    assert icon[224343753145155588][1]["m_AnchorMin"] == {"x": 0, "y": 1}
    assert icon[224343753145155588][1]["m_AnchorMax"] == {"x": 0, "y": 1}
    assert icon[224343753145155588][1]["m_SizeDelta"] == {"x": 70, "y": 70}
    masks = [b for cls, b in pcos.values() if cls == 114 and
             b.get("m_Script", {}).get("guid") == "3312d7739989d2b4e91e6319e9a96d76" and
             b["m_GameObject"]["fileID"] == 1583803054014249]
    assert len(masks) == 1 and masks[0]["m_Enabled"] == 1
    assert masks[0]["m_Padding"] == {"x": 0, "y": 40, "z": 0, "w": 0}
    assert masks[0]["m_Softness"] == {"x": 0, "y": 0}
    dragger = icon[1495262664511532085][1]
    assert [dragger[k] for k in ["cellWidth", "cellHeight", "spacingX", "spacingY", "padding"]] == [70, 70, 20, 20, 20]
    assert "bottomPadding" not in dragger
    text = (CS / "PC/Component/Software/OS/OperatingSystem.cs").read_text()
    for removed in ["CheckForLayoutContextChange", "FitPositionToCurrentLayout", "ApplyLayoutContextChange", "TrackIconParentSize"]:
        assert removed not in text
    assert "AddComponent<RectMask2D>" not in text
    print("PASS: prefab anchors, clipping mask, original icon size/grid, local references and hierarchy", flush=True)


def main():
    for name in ["DesktopIconGrid.cs", "DesktopIconDragger.cs", "PC/Component/Display.cs"]:
        source_members(CS / name)
    fixture = generate_os_fixture()
    validate_prefabs()
    with tempfile.TemporaryDirectory(prefix="desktop-layout-tests-") as folder:
        folder = Path(folder)
        generated = folder / "OperatingSystemDesktop.cs"
        generated.write_text(fixture)
        storage = folder / "StorageFiles.cs"
        storage.write_text(generate_storage_fixture())
        exe = folder / "tests.exe"
        subprocess.run(["mcs", "-langversion:latest", "-out:" + str(exe),
                        str(CS / "DesktopIconGrid.cs"), str(CS / "DesktopIconDragger.cs"),
                        str(CS / "DesktopFileSnapshot.cs"), str(CS / "PC/Component/Software/File.cs"),
                        str(CS / "PC/Component/Software/OS/FileManager.cs"), str(CS / "PC/Component/Software/OS/SaveDialog.cs"),
                        str(storage), str(generated), str(ROOT / "tools/tests/desktop_layout_stubs.cs"),
                        str(ROOT / "tools/tests/desktop_layout_tests.cs")], check=True)
        subprocess.run(["mono", str(exe)], check=True)
    print("C# desktop tests and prefab checks passed. Unity rendering/Play Mode was not run.")


if __name__ == "__main__":
    main()
