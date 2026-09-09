#!/usr/bin/env python3
"""Static catalogue/delivery/save-ID checks for the two new monitors. Not a Unity Play Mode test.
Requires PyYAML. Run: python3 tools/test_new_monitor_shop.py
"""
from pathlib import Path
import re
import yaml

ROOT = Path(__file__).resolve().parents[1] / "Assets"


def guid(path):
    return re.search(r"^guid: (\w+)$", Path(str(path) + ".meta").read_text(), re.M)[1]


def docs(path):
    result = {}
    for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)", path.read_text(), re.M | re.S):
        fid = int(m[2])
        assert fid not in result, (path, fid)
        result[fid] = (int(m[1]), next(iter(yaml.safe_load(m[3]).values())))
    return result


def refs(value):
    if isinstance(value, dict):
        if "fileID" in value:
            yield value
        for child in value.values():
            yield from refs(child)
    elif isinstance(value, list):
        for child in value:
            yield from refs(child)


def local_links(documents):
    for fid, (kind, body) in documents.items():
        for ref in refs(body):
            if ref["fileID"] and "guid" not in ref:
                assert ref["fileID"] in documents, (fid, ref)
        if kind == 1:
            assert documents[body["m_Component"][0]["component"]["fileID"]][0] in (4, 224)
            for component in body["m_Component"]:
                assert documents[component["component"]["fileID"]][1]["m_GameObject"]["fileID"] == fid


def main():
    shop = docs(ROOT / "MonoBehaviour/Shop.asset")[11400000][1]
    monitor_page = next(p for p in shop["pages"] if p["pageName"] == "Monitor")
    market_docs = docs(ROOT / "Resources/apps/Market.prefab")
    local_links(market_docs)
    market = next(body for kind, body in market_docs.values() if kind == 114 and "items" in body and "randomItems" in body)
    keys = []
    new_guids = set()
    for name, photo, price in [("StandMonitor", "StandMonitor", 400), ("PortableMonitor", "portablemonitor", 250)]:
        path = ROOT / "MonoBehaviour" / (name + ".asset")
        item = docs(path)[11400000][1]
        item_guid = guid(path)
        new_guids.add(item_guid)
        assert item["m_Script"]["guid"] == "484ef020c3930db604de1872bd7b83d7"
        assert item["m_Name"] == name
        keys.append(item["m_Name"])
        assert item["price"] == price and item["bitcoin"] == 5
        assert item["large"] == 1 and item["translateDescription"] == 0
        assert sum(ref["guid"] == item_guid for ref in monitor_page["item"]) == 1
        assert sum(ref["guid"] == item_guid for ref in market["items"]) == 1
        assert not any(ref["guid"] == item_guid for ref in (market["randomItems"] or []))
        texture = ROOT / "Texture2D" / (photo + ".png")
        assert item["sprite"] == {"fileID": 21300000, "guid": guid(texture), "type": 3}
        importer = yaml.safe_load(Path(str(texture) + ".meta").read_text())["TextureImporter"]
        assert importer["spriteMode"] == 1 and importer["textureType"] == 8
        assert importer["mipmaps"]["enableMipMap"] == 0 and importer["alphaIsTransparency"] == 1
        assert importer["spriteSheet"]["spriteID"]
        crate = ROOT / "Resources/components" / ("Crate_" + name + ".prefab")
        crate_docs = docs(crate)
        local_links(crate_docs)
        assert item["spawn"] == {"fileID": 1526699019895958, "guid": guid(crate), "type": 3}
        assert crate_docs[1526699019895958][1]["m_Name"] == "Crate_" + name
        assert crate_docs[114856982077962643][1]["spawnId"] == crate.stem
        assert crate_docs[1284323535482697][1]["m_IsActive"] == 0, "contents must wait until the crate opens"
        box = crate_docs[114291363228966031][1]
        device_path = ROOT / "Resources/components" / (name + ".prefab")
        assert box["prefab"] == {"fileID": 1564520538106305, "guid": guid(device_path), "type": 3}
        device = docs(device_path)
        local_links(device)
        display = next(body for kind, body in device.values() if kind == 114 and body.get("m_Script", {}).get("guid") == "0a132b41b49b18a3078aa22fd0638b3e")
        assert display["spawnId"] == name, "saving must resolve to the new device, not an older item"
        assert display["screen"]["fileID"] in device and display["screenCanvas"]["fileID"] in device
        if name == "StandMonitor":
            new_guids.add(guid(crate))
            device_min = min(body["m_Center"]["y"] - body["m_Size"]["y"] / 2 for kind, body in device.values()
                             if kind == 65 and not body["m_IsTrigger"] and body["m_GameObject"]["fileID"] == 1564520538106305)
            collider = crate_docs[65119779663380389][1]
            crate_min = collider["m_Center"]["y"] - collider["m_Size"]["y"] / 2
            broken_y = crate_docs[4054956342393933][1]["m_LocalPosition"]["y"]
            assert -crate_min + broken_y + box["position"]["y"] + device_min >= 0.07
        print("PASS:", name, "$" + str(price), "/ 5 BTC; photo sprite, both catalogues, crate contents, delivery and reload ID")
    assert len(set(keys)) == 2 and "MonitorStand" not in keys
    old = docs(ROOT / "MonoBehaviour/MonitorStand.asset")[11400000][1]
    assert old["price"] == 150 and old["bitcoin"] == 0 and old["m_Name"] == "MonitorStand"
    assert any(ref["guid"] == guid(ROOT / "MonoBehaviour/MonitorStand.asset") for ref in monitor_page["item"])
    assert len(new_guids) == 3
    owners = {g: [] for g in new_guids}
    for meta in ROOT.rglob("*.meta"):
        text = meta.read_text(errors="replace")
        for g in new_guids:
            if "guid: " + g in text:
                owners[g].append(meta)
    assert all(len(paths) == 1 for paths in owners.values()), owners
    unlock = (ROOT / "Scripts/Assembly-CSharp/PC/Shop/ShopUI.cs").read_text()
    state = (ROOT / "Scripts/Assembly-CSharp/PC/Shop/ShopItem.cs").read_text()
    assert "BitcoinManager.Bitcoin = bitcoin - item.bitcoin" in unlock and "item.Unlock();" in unlock
    assert 'PlayerPrefs.SetString("Unlocked"' in state and "var itemName = name;" in state
    print("PASS: independent one-time unlock keys, unique new GUIDs and old MonitorStand preserved")
    print("Static monitor shop checks passed; Unity purchases/unpacking/Play Mode were not run.")


if __name__ == "__main__":
    main()
