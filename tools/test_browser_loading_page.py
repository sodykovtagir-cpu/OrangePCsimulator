#!/usr/bin/env python3
"""Prefab/source checks for the authored browser loader (not a Unity Play Mode test)."""
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
browser=(root/'Assets/Scripts/Assembly-CSharp/PC/Component/Software/Browser.cs').read_text()
prefab=(root/'Assets/Resources/apps/Browser.prefab').read_text()
pcos=(root/'Assets/GameObject/PCOS.prefab').read_text()
def section(t,fid):
    match=re.search(r'^--- !u!\d+ &'+str(fid)+r'\n(.*?)(?=^--- !u!|\Z)',t,re.S|re.M)
    assert match,fid
    return match.group(1)
assert 'loadingPage.SetActive(true)' in browser
assert 'loadingPage.SetActive(false)' in browser
assert 'loadingText.text = "Загрузка... " + url' in browser
assert browser.index('loadingPage.SetActive(true)') < browser.index('StartCoroutine(FinishLoad(delay, completed))')
assert 'if (pendingLoad != null)' in browser and 'StopCoroutine(pendingLoad)' in browser
assert 'loadingPage: {fileID: 6712345600000000001}' in prefab
assert 'loadingText: {fileID: 6712345600000000008}' in prefab
assert 'm_Name: Browser Loading Page' in prefab
assert 'm_Name: Loading Text' in prefab
assert 'm_IsActive: 0' in section(prefab,6712345600000000001)
assert 'm_IsActive: 1' in section(prefab,1142015082165268)
assert 'm_Father: {fileID: 6712345600000000002}' in section(prefab,224606781054035087)
assert 'm_Father: {fileID: 8720871988770074814}' in section(prefab,6712345600000000002)
assert 'm_Color: {r: 1, g: 1, b: 1, a: 1}' in section(prefab,6712345600000000004)
assert 'm_Color: {r: 0, g: 0, b: 0, a: 1}' in section(prefab,6712345600000000008)
assert 'm_Sprite: {fileID: 21300000, guid: 6cb8502ce08fd19489e2ae1bcad1d791, type: 2}' in section(prefab,114349093820024704)
assert 'guid: 6cb8502ce08fd19489e2ae1bcad1d791' in pcos
assert 'm_UpdateMode: 2' in section(prefab,95395185920935229)
ids=re.findall(r'^--- !u!\d+ &(\d+)',prefab,re.M)
assert len(ids)==len(set(ids)), 'duplicate Unity fileID'
print('PASS: white browser loading page, PCOS spinner, realtime animation and cleanup')
