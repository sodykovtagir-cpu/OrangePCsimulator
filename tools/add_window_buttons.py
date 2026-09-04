#!/usr/bin/env python3
"""
Редактор префабов приложений Orange PC Simulator.

Добавляет в каждое окно-приложение (Assets/Resources/apps/*.prefab):
  - кнопку "Minimize" (сворачивание)  -> App.Minimize()
  - кнопку "Maximize" (разворот на весь экран) -> App.Maximize()
и назначает в корневом App-компоненте поля:
  maximizeSprite / normalSprite / windowState (для смены иконки разворота).

Кнопки ставятся в правый верхний угол рядом с уже существующей кнопкой Close
(same anchors/pivot/size), со смещением по X влево с отступом.
Использует готовые спрайты Assets/Sprite: WindowMaximize / WindowNormal /
WindowMinimize.

Запуск:  python3 tools/add_window_buttons.py [--apply]
(без --apply только печатает план изменений)
"""
import os
import re
import sys
import glob
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APPS_DIR = os.path.join(ROOT, "Assets", "Resources", "apps")

# Unity builtin script guids
GUID_IMAGE = "fe87c0e1cc204ed48ad3b37840f39efc"   # UnityEngine.UI.Image
GUID_BUTTON = "4e29b1a8efbd4b44bb3f3716e73f07ff"  # UnityEngine.UI.Button

# Sprite asset guids (Assets/Sprite/*.asset.meta)
SPRITE_MAXIMIZE = "8bee6c5e8f8f34343b72503754d62702"  # WindowMaximize.asset
SPRITE_NORMAL = "c0189c21ff4a19140a624ae91c3dc4f6"    # WindowNormal.asset
SPRITE_MINIMIZE = "bb0f5908d2d446f085cf605d2613be08"  # WindowMinimize.asset

# Layout: Close at x=-20 (center). Buttons 25px size => 40px pitch to left.
# Order right->left: Close (-20), Maximize (-60), Minimize (-100).
MAXIMIZE_X = -60.0
MINIMIZE_X = -100.0
BUTTON_Y = -20.0
BUTTON_SIZE = 25.0
BUTTON_GAP_PITCH = 40.0  # если Close не на -20, отсчитываем от его позиции


def new_id():
    # big positive fileID Unity-style (21300000 etc). Use large unique ints.
    return uuid.uuid4().int & 0x7FFFFFFFFFFFFFFF


class PrefabDoc:
    def __init__(self, text):
        self.text = text

    def blocks(self):
        """yield (header_match, class_id, file_id, body_start, body_end)"""
        for m in re.finditer(r'--- !u!(\d+) &(\d+)\n', self.text):
            cid, fid = m.group(1), m.group(2)
            start = m.end()
            nxt = self.text.find('\n--- !u!', start)
            end = len(self.text) if nxt == -1 else nxt
            yield cid, fid, m.start(), start, end

    def list(self):
        out = []
        for cid, fid, hstart, bstart, bend in self.blocks():
            out.append((cid, fid, self.text[bstart:bend], bstart, bend))
        return out


def get_field(body, field):
    m = re.search(r'^\s*' + re.escape(field) + r':\s*\{fileID:\s*(\d+)\}', body, re.M)
    return m.group(1) if m else None


def vec(body, field):
    m = re.search(field + r':\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+)\}', body)
    return (float(m.group(1)), float(m.group(2))) if m else None


def analyze(path):
    text = open(path, encoding='utf-8').read()
    doc = PrefabDoc(text)

    go_by_id = {}       # fileID -> body (GameObject, class 1)
    rt_by_id = {}       # fileID -> body (RectTransform 224/222)
    mono_by_id = {}     # fileID -> body (MonoBehaviour 114)
    rt_go = {}          # rt fid -> go fid
    go_components = {}  # go fid -> [component fid]

    for cid, fid, body, bs, be in doc.list():
        if cid == '1':
            go_by_id[fid] = body
            go_components[fid] = re.findall(r'component:\s*\{fileID:\s*(\d+)\}', body)
        elif cid in ('224', '222'):
            rt_by_id[fid] = body
            g = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', body)
            if g:
                rt_go[fid] = g.group(1)
        elif cid == '114':
            mono_by_id[fid] = body

    def go_name(go_fid):
        b = go_by_id.get(go_fid, '')
        m = re.search(r'm_Name:\s*(.*)', b)
        return m.group(1).strip() if m else '?'

    # root RectTransform = one whose m_Father fileID is 0 (находим заранее)
    root_go = None
    root_rt = None
    for fid, body in rt_by_id.items():
        father = re.search(r'm_Father:\s*\{fileID:\s*(\d+)\}', body)
        if father and father.group(1) == '0':
            root_rt = fid
            root_go = rt_go.get(fid)
            break

    def is_title_button(go_fid):
        # true если RT кнопки прижата к верхнему-правому углу и предок — корень окна
        for rf, gf in rt_go.items():
            if gf != go_fid:
                continue
            rb = rt_by_id[rf]
            amin = vec(rb, 'm_AnchorMin'); amax = vec(rb, 'm_AnchorMax')
            father = re.search(r'm_Father:\s*\{fileID:\s*(\d+)\}', rb)
            fa_rt = father.group(1) if father else None
            fa_go = rt_go.get(fa_rt)
            if (amin and amax and abs(amax[0]-1) < 0.01 and abs(amax[1]-1) < 0.01
                    and fa_go == root_go):
                return rf
        return None

    # find Close button: Button with onClick Close, прижата к верху-правому углу окна
    close_btn = None
    root_app_mono = None

    for fid, body in mono_by_id.items():
        if GUID_BUTTON in body:
            method = re.search(r'm_MethodName:\s*(\w+)', body)
            if method and method.group(1) == 'Close':
                tgt = re.search(r'm_OnClick:.*?m_Target:\s*\{fileID:\s*(\d+)\}', body, re.S)
                go = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', body)
                tgtgraphic = re.search(r'm_TargetGraphic:\s*\{fileID:\s*(\d+)\}', body)
                go_fid = go.group(1) if go else None
                title_rt = is_title_button(go_fid)
                if title_rt is None:
                    continue  # это кнопка внутри диалога, не оконный крестик
                close_btn = dict(btn=fid, go=go_fid, rt=title_rt,
                                 img=tgtgraphic.group(1) if tgtgraphic else None,
                                 target=tgt.group(1) if tgt else None)
                break

    # root App MonoBehaviour = a mono on root_go whose onClick isn't image/button
    if root_go:
        for comp in go_components.get(root_go, []):
            b = mono_by_id.get(comp)
            if b and GUID_BUTTON not in b and GUID_IMAGE not in b:
                root_app_mono = comp
                break

    return dict(text=text, go_by_id=go_by_id, rt_by_id=rt_by_id, mono_by_id=mono_by_id,
                rt_go=rt_go, go_components=go_components, close_btn=close_btn,
                root_go=root_go, root_rt=root_rt, root_app_mono=root_app_mono)


def make_button(name, sprite_guid, method, x, y, size, go_id, rt_id, cr_id, img_id, btn_id, parent_rt):
    go = f"""--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt_id}}}
  - component: {{fileID: {cr_id}}}
  - component: {{fileID: {img_id}}}
  - component: {{fileID: {btn_id}}}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""

    rt = f"""--- !u!224 &{rt_id}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: {x}, y: {y}}}
  m_SizeDelta: {{x: {size}, y: {size}}}
  m_Pivot: {{x: 0.5, y: 0.5}}
"""

    cr = f"""--- !u!222 &{cr_id}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_CullTransparentMesh: 1
"""

    img = f"""--- !u!114 &{img_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_IMAGE}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 2}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

    btn = f"""--- !u!114 &{btn_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_BUTTON}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_PressedColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}}
    m_SelectedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_DisabledColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Highlighted
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {img_id}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: __APP__}}
        m_TargetAssemblyTypeName:
        m_MethodName: {method}
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
"""
    return go, rt, cr, img, btn


def set_app_fields(body, maximize_img_fid):
    """Вставляем/обновляем поля maximizeSprite/normalSprite/windowState в App-компоненте."""
    # Удаляем старые строки этих полей, если есть
    for fld in ('maximizeSprite', 'normalSprite', 'windowState'):
        body = re.sub(r'^\s*' + fld + r':.*\n', '', body, flags=re.M)
    # Добавим перед закрытием (после m_EditorClassIdentifier блока) — просто в конец тела.
    add = (
        f"maximizeSprite: {{fileID: 21300000, guid: {SPRITE_MAXIMIZE}, type: 2}}\n"
        f"normalSprite: {{fileID: 21300000, guid: {SPRITE_NORMAL}, type: 2}}\n"
        f"windowState: {{fileID: {maximize_img_fid}}}\n"
    )
    body = body.rstrip('\n') + '\n' + add
    return body


def process(path, apply=False):
    info = analyze(path)
    cb = info['close_btn']
    name = os.path.basename(path)

    if not info['root_app_mono']:
        print(f"[SKIP] {name}: не найден корневой App-компонент")
        return False
    if not info['root_rt']:
        print(f"[SKIP] {name}: не найден корневой RectTransform")
        return False

    app_target = info['root_app_mono']
    parent_rt = info['root_rt']

    # позиция Close (если есть стандартный крестик) либо дефолт
    if cb:
        close_rt_body = info['rt_by_id'][cb['rt']]
        pos = vec(close_rt_body, 'm_AnchoredPosition')
        size = vec(close_rt_body, 'm_SizeDelta')
        cx = pos[0] if pos else -20.0
        cy = pos[1] if pos else -20.0
        bsz = size[0] if size else BUTTON_SIZE
    else:
        cx, cy, bsz = -20.0, -20.0, BUTTON_SIZE
    pitch = max(bsz + 8.0, BUTTON_GAP_PITCH)
    max_x = cx - pitch
    min_x = cx - pitch * 2.0

    # Уже есть Maximize?
    has_max = any('m_MethodName: Maximize' in b for b in info['mono_by_id'].values())
    has_min = any('m_MethodName: Minimize' in b for b in info['mono_by_id'].values())

    new_blocks = []
    max_img_fid = None

    if not has_max:
        ids = {k: new_id() for k in ('go', 'rt', 'cr', 'img', 'btn')}
        max_img_fid = ids['img']
        go, rt, cr, img, btn = make_button("Maximize", SPRITE_MAXIMIZE, "Maximize",
                                           max_x, cy, bsz, ids['go'], ids['rt'],
                                           ids['cr'], ids['img'], ids['btn'], parent_rt)
        btn = btn.replace("__APP__", app_target)
        new_blocks += [go, rt, cr, img, btn]
    else:
        # найдём существующий Image Maximize для windowState
        for fid, b in info['mono_by_id'].items():
            if GUID_BUTTON in b and 'm_MethodName: Maximize' in b:
                tg = re.search(r'm_TargetGraphic:\s*\{fileID:\s*(\d+)\}', b)
                if tg:
                    max_img_fid = tg.group(1)

    if not has_min:
        ids = {k: new_id() for k in ('go', 'rt', 'cr', 'img', 'btn')}
        if max_img_fid is None:
            max_img_fid = ids['img']  # на всякий случай
        go, rt, cr, img, btn = make_button("Minimize", SPRITE_MINIMIZE, "Minimize",
                                           min_x, cy, bsz, ids['go'], ids['rt'],
                                           ids['cr'], ids['img'], ids['btn'], parent_rt)
        btn = btn.replace("__APP__", app_target)
        new_blocks += [go, rt, cr, img, btn]

    text = info['text']

    # 1) добавить новые блоки в конец файла
    if new_blocks:
        addition = "\n".join(new_blocks)
        text = text.rstrip('\n') + "\n\n" + addition + "\n"

    # 2) прописать новые компоненты в корневой GameObject и детей в корневой RectTransform
    new_go_ids = re.findall(r'--- !u!1 &(\d+)\nGameObject:', addition) if new_blocks else []
    new_rt_ids = re.findall(r'--- !u!224 &(\d+)\nRectTransform:', addition) if new_blocks else []

    # rebuild root GameObject component list
    root_go = info['root_go']
    root_comps = info['go_components'][root_go]
    # добавляем в корень только Maximize/Minimize GameObject ссылки не нужны — это отдельные объекты.
    # Детей добавляем в корневой RT m_Children.
    root_rt_fid = info['root_rt']

    # Вставить m_Children записи в корневой RectTransform
    if new_rt_ids:
        m = re.search(r'(--- !u!224 &' + root_rt_fid + r'\n.*?m_Children:\n)(.*?)(  m_Father:)', text, re.S)
        if m:
            existing_children = m.group(2)
            add_children = "".join(f"  - {{fileID: {rid}}}\n" for rid in new_rt_ids)
            new_children = existing_children + add_children
            text = text[:m.start(2)] + new_children + text[m.end(2):]

    # 3) поля App-компонента
    # заменяем блок root_app_mono
    pat = re.compile(r'(--- !u!114 &' + app_target + r'\n)(.*?)(?=\n--- !u!|\Z)', re.S)
    mm = pat.search(text)
    if mm and max_img_fid:
        body = mm.group(2)
        body_new = set_app_fields(body, max_img_fid)
        text = text[:mm.start(2)] + body_new + text[mm.end(2):]

    if apply:
        open(path, 'w', encoding='utf-8').write(text)
        print(f"[OK] {name}: +Maximize={'нет' if not has_max else 'есть'} -> добавлен={not has_max}, "
              f"+Minimize={'нет' if not has_min else 'есть'} -> добавлен={not has_min}, "
              f"close_x={cx}, max_x={max_x:.0f}, min_x={min_x:.0f}")
    else:
        print(f"[PLAN] {name}: close_x={cx}, max_x={max_x:.1f}, min_x={min_x:.1f}, "
              f"will add max={not has_max} min={not has_min}")
    return True


def main():
    apply = '--apply' in sys.argv
    prefabs = sorted(glob.glob(os.path.join(APPS_DIR, '*.prefab')))
    print(f"apply={apply}, prefabs={len(prefabs)}\n")
    ok = 0
    for p in prefabs:
        if process(p, apply=apply):
            ok += 1
    print(f"\nГотово: {ok}/{len(prefabs)}")


if __name__ == '__main__':
    main()
