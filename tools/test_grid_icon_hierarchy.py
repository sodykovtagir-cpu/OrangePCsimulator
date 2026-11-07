#!/usr/bin/env python3
"""Guard the Standalone Grid button hierarchy / serialized references.

Bug: Functions.gridImage on non-Medium rooms pointed at the child hotkey
backdrop Image (which must stay Circle). RefreshGridIcon() then overwrote that
child's sprite with GridIcon, drawing a tiny Grid inside the Grid button.
The reference must target the ROOT Grid Image, exactly like Medium.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parent.parent
SCENES = ROOT / 'Assets/Scenes'
IMAGE_GUID = 'fe87c0e1cc204ed48ad3b37840f39efc'
FUNC_GUID = '0671fa625bb5391ee788a103374b21e2'
TEXT_GUID = '5f7201a12d95ffc409449d95f23cf332'
GRID_SPRITE = '5cff8738753140a890699e9b85d2c249'   # GridIcon.png
CIRCLE_SPRITE = '98843b49250d924469e8c9e2828a4bf1'  # Circle.asset

HEADER = re.compile(r'^--- !u!(\d+) &(\d+)( stripped)?\s*$', re.M)


class Scene:
    def __init__(self, path):
        raw = Path(path).read_text(encoding='utf-8')
        matches = list(HEADER.finditer(raw))
        self.docs = []
        for i, m in enumerate(matches):
            end = matches[i + 1].start() if i + 1 < len(matches) else len(raw)
            self.docs.append({'classid': int(m.group(1)), 'fileid': int(m.group(2)),
                              'body': raw[m.end():end]})
        self.by_id = {d['fileid']: d for d in self.docs}

    def name(self, doc):
        m = re.search(r'^  m_Name: (.*)$', doc['body'], re.M)
        return m.group(1).strip() if m else None

    def gameobjects(self):
        return [d for d in self.docs if d['classid'] == 1]

    def components(self, go):
        ids = re.findall(r'component: \{fileID: (\d+)\}', go['body'])
        return [self.by_id[int(i)] for i in ids if int(i) in self.by_id]

    def script_guid(self, comp):
        m = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]+)', comp['body'])
        return m.group(1) if m else None

    def comp_by_guid(self, go, guid):
        return next((c for c in self.components(go) if self.script_guid(c) == guid), None)

    def transform(self, go):
        return next((c for c in self.components(go) if c['classid'] in (4, 224)), None)

    def go_of_transform(self, t):
        m = re.search(r'm_GameObject: \{fileID: (\d+)\}', t['body'])
        return self.by_id.get(int(m.group(1))) if m else None

    def children(self, go):
        t = self.transform(go)
        if not t:
            return []
        m = re.search(r'm_Children:\n((?:  - \{fileID: \d+\}\n)*)', t['body'])
        ids = re.findall(r'fileID: (\d+)', m.group(1)) if m else []
        return [self.go_of_transform(self.by_id[int(i)]) for i in ids if int(i) in self.by_id]

    def path(self, go):
        names = [self.name(go)]
        t = self.transform(go)
        seen = set()
        while t is not None:
            m = re.search(r'm_Father: \{fileID: (\d+)\}', t['body'])
            fid = int(m.group(1)) if m else 0
            if fid == 0 or fid in seen:
                break
            seen.add(fid)
            pt = self.by_id.get(fid)
            if pt is None:
                break
            pg = self.go_of_transform(pt)
            if pg is None:
                break
            names.append(self.name(pg))
            t = self.transform(pg)
        if any(n is None for n in names):
            return ''
        return '/'.join(reversed(names))


def sprite_guid(comp):
    m = re.search(r'm_Sprite: \{fileID: \d+, guid: ([0-9a-f]+)', comp['body'])
    return m.group(1) if m else None


for scene_name in ('Main', 'Main 2', 'Main 3', 'Main 4', 'Tutorial'):
    s = Scene(SCENES / (scene_name + '.unity'))
    panel = next((g for g in s.gameobjects() if s.path(g) == 'UI/Main/Standalone/Panel'), None)
    grid = next((g for g in s.gameobjects() if s.path(g) == 'UI/Main/Standalone/Panel/Grid'), None)
    assert panel is not None and grid is not None, f'{scene_name}: missing Standalone Grid'

    func = s.comp_by_guid(panel, FUNC_GUID)
    root_img = s.comp_by_guid(grid, IMAGE_GUID)
    assert func is not None and root_img is not None, f'{scene_name}: missing Functions/Image'

    ref = re.search(r'gridImage: \{fileID: (\d+)\}', func['body']).group(1)
    assert ref == str(root_img['fileid']), (
        f'{scene_name}: Functions.gridImage must reference the ROOT Grid Image '
        f'{root_img["fileid"]}, not the child Circle (got {ref})')

    assert sprite_guid(root_img) == GRID_SPRITE, f'{scene_name}: root Grid sprite must be GridIcon'

    child = next((c for c in s.children(grid) if c and s.name(c) == 'Image'), None)
    assert child is not None, f'{scene_name}: Grid must keep its child hotkey Image'
    child_img = s.comp_by_guid(child, IMAGE_GUID)
    assert child_img is not None and sprite_guid(child_img) == CIRCLE_SPRITE, (
        f'{scene_name}: child Image must stay Circle, never GridIcon')

    text_go = next((c for c in s.children(child) if c and s.name(c) == 'Text'), None)
    assert text_go is not None, f'{scene_name}: hotkey Text missing'
    text = s.comp_by_guid(text_go, TEXT_GUID)
    assert text is not None and re.search(r'^  m_Text: G$', text['body'], re.M), (
        f'{scene_name}: hotkey text must be G')

print('PASS: Grid button uses root GridIcon; child hotkey backdrop stays Circle with G')
