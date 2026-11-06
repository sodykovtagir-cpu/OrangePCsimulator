#!/usr/bin/env python3
"""Browser error screens follow its prefab loading-page palette; not Play Mode."""
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
code=(root/'Assets/Scripts/Assembly-CSharp/PC/Component/Software/Browser.cs').read_text()
pref=(root/'Assets/Resources/apps/Browser.prefab').read_text()
def block(fid):
    m=re.search(r'^--- !u!\d+ &'+str(fid)+r'\n(.*?)(?=^--- !u!|\Z)',pref,re.M|re.S)
    assert m,fid
    return m.group(1)
# Browser.prefab may use its authored error panel or the existing white
# RouterWebPages.Message fallback; never invent a second UI over the user's layout.
if 'errorPage: {fileID: 0}' in pref:
    web=(root/'Assets/Scripts/Assembly-CSharp/PC/Component/Software/RouterWebPages.cs').read_text()
    assert 'errorText: {fileID: 0}' in pref
    assert 'else networkPage = RouterWebPages.Message(pageContainer, message);' in code
    assert 'root.GetComponent<Image>().color = Color.white;' in web
    assert 'Label(body, text, 21).color = Color.black;' in web
else:
    assert 'errorPage: {fileID: 6612345600000000001}' in pref
    assert 'errorText: {fileID: 6612345600000000008}' in pref
    assert 'm_Name: Browser Error Page' in pref and 'm_Name: Error Text' in pref
    assert 'm_IsActive: 0' in block(6612345600000000001)
    assert 'm_Color: {r: 1, g: 1, b: 1, a: 1}' in block(6612345600000000004)
    assert 'm_Color: {r: 0, g: 0, b: 0, a: 1}' in block(6612345600000000008)
    assert 'm_Children:\n  - {fileID: 6612345600000000006}' in block(6612345600000000002)
    assert '224606781054035087' not in block(6612345600000000002)
assert 'errorPage.SetActive(true)' in code and 'errorPage.SetActive(false)' in code
assert 'ShowError(error ?? "Нет подключения к интернету")' in code
assert 'ShowError("Соединение прервано")' in code
assert 'ShowError("Сначала подключись к роутеру через Wi-Fi в панели задач PCOS или LAN")' in code
assert 'CanOpenLocalPanel(board, out var router)' in code
assert 'board.WifiEnabled' not in code, 'do not disable working LAN in Browser'
ids=re.findall(r'^--- !u!\d+ &(\d+)',pref,re.M)
assert len(ids)==len(set(ids))
print('PASS: authored white/black error pages for browser and local panels; LAN preserved')
