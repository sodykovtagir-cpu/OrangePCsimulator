#!/usr/bin/env python3
"""Validate bundled language coverage, UI wiring and the real C# localization implementation.
Requires PyYAML, mcs and mono. No Unity Editor/Play Mode is used.
"""
from collections import Counter
from pathlib import Path
import re
import subprocess
import tempfile

import yaml

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
CS = ASSETS / "Scripts/Assembly-CSharp"


def docs(path):
    result = {}
    for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", path.read_text(), re.M | re.S):
        fid = int(m[2])
        assert fid not in result, (path, fid)
        result[fid] = (int(m[1]), next(iter(yaml.safe_load(m[3]).values())))
    return result


def guid(path):
    return re.search(r"^guid: (\w+)$", Path(str(path) + ".meta").read_text(), re.M)[1]


def references(value):
    if isinstance(value, dict):
        if "fileID" in value:
            yield value
        for child in value.values():
            yield from references(child)
    elif isinstance(value, list):
        for child in value:
            yield from references(child)


def main():
    table_path = ASSETS / "Resources/Translate.txt"
    rows = [line.split("\t") for line in table_path.read_text().splitlines()]
    assert all(row[0] for row in rows), "orphaned TSV continuation"
    table = {row[0]: row[1:] for row in rows}
    assert len(table) == len(rows), "duplicate keys"
    languages = table["*Short Form"]
    assert len(languages) == len(set(languages)) == 42
    for key, values in table.items():
        if key.startswith("*"):
            continue
        assert len(values) == len(languages) and all(value.strip() for value in values), key
        expected = Counter(re.findall(r"\{\d+\}", values[0]))
        for language, value in zip(languages, values):
            assert Counter(re.findall(r"\{\d+\}", value)) == expected, (key, language)
            assert not re.search(r"(?:ZZ|ЗЗ|ΖΖ)(?:BRAND|БРАНД)", value, re.I), (key, language)
    for path in CS.rglob("*.cs"):
        for match in re.finditer(r'\b(?:Localization.GetText|Tr)\(\s*"((?:\\.|[^"\\])*)"\s*(?=[,)])', path.read_text(errors="replace")):
            assert match[1] in table, (path, match[1])
    print("PASS: every runtime literal lookup is present; all", len(rows) - 6, "text rows have 42 nonempty translations and matching format arguments")

    localizers = {guid(CS / "LocalizationText.cs"), guid(CS / "LocalizationTextBracket.cs")}
    targets = {
        "Scenes/Menu.unity": {"Username", "Email", "Enter code", "Search saves..."},
        "Resources/apps/Paint.prefab": {"Enter text...", "HD", "HD Banner"},
        "Resources/apps/LuaEditor.prefab": {"Lua Editor", "Compile settings", "Select Icon", "Compile", "Output", "Enter name...", "Name", "From device"},
        "Resources/apps/Browser.prefab": {"Search in Oggle..."},
        "Resources/apps/Viewer.prefab": {"Viewer"},
        "GameObject/ModForge.prefab": {"Products", "Checkout", "Build a PC that is uniquely yours.", "No resolution limit"},
        "Resources/apps/Personalization.prefab": {"Select wallpaper"},
    }
    for name, labels in targets.items():
        objects = docs(ASSETS / name)
        seen = set()
        for fid, (kind, body) in objects.items():
            for ref in references(body):
                if ref["fileID"] and "guid" not in ref:
                    assert ref["fileID"] in objects, (name, fid, ref)
            if kind == 1 and "m_Component" in body:
                assert objects[body["m_Component"][0]["component"]["fileID"]][0] in (4, 224)
            if "m_FontData" in body and body.get("m_Text") in labels:
                seen.add(body["m_Text"])
                go = objects[body["m_GameObject"]["fileID"]][1]
                assert any(objects[c["component"]["fileID"]][1].get("m_Script", {}).get("guid") in localizers for c in go["m_Component"]), (name, body["m_Text"])
                assert body["m_Text"] in table
        assert seen == labels, (name, labels - seen)
    explorer = docs(ASSETS / "Resources/apps/FileManager.prefab")
    path_go = explorer[4093872087696978051][1]
    assert not any(explorer[c["component"]["fileID"]][1].get("m_Script", {}).get("guid") in localizers for c in path_go["m_Component"]), "do not translate or overwrite actual file paths"
    for name in ["StandMonitor", "PortableMonitor"]:
        item = docs(ASSETS / "MonoBehaviour" / (name + ".asset"))[11400000][1]
        assert item["m_Name"] == name and item["bitcoin"] == 5
        assert item["translateDescription"] == 1 and item["description"] in table
        assert item["itemName"].startswith("{") and item["itemName"][1:-1] in table
    rotating = docs(ASSETS / "MonoBehaviour/RotatingDisplayStand.asset")[11400000][1]
    assert rotating["m_Name"] == "RotatingDisplayStand" and rotating["price"] == 150 and rotating["bitcoin"] == 0
    assert rotating["itemName"] == "{Rotating Display Stand}" and rotating["translateDescription"] == 1
    assert rotating["description"] in table
    assert table["Rotating Display Stand"][languages.index("RU")] == "Вращающийся стенд"
    assert "128x720" not in (ASSETS / "Resources/apps/Paint.prefab").read_text()
    assert "up to 1024x1024" not in (ASSETS / "GameObject/PrintExpert.prefab").read_text()
    assert "up to 1024x1024" not in (ASSETS / "GameObject/ModForge.prefab").read_text()
    print("PASS: static labels connected, paths/user data not rebound, HD captions accurate, product IDs and unlocks retained")

    with tempfile.TemporaryDirectory(prefix="orange-localization-") as directory:
        exe = Path(directory) / "localization-tests.exe"
        subprocess.run(["mcs", "-langversion:latest", "-out:" + str(exe),
                        str(CS / "Localization.cs"), str(CS / "LocalizationText.cs"), str(CS / "LocalizationTextBracket.cs"),
                        str(ROOT / "tools/tests/localization_tests.cs")], check=True)
        subprocess.run(["mono", str(exe), str(table_path)], check=True)
    print("Localization checks passed. Unity scene rendering and native-speaker review of every language were not performed.")


if __name__ == "__main__":
    main()
