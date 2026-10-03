"""Emitters for hand-authored Unity UI (prefab YAML) blocks.

The 3DMork app must not build its UI procedurally at runtime: every panel,
row, label and button is serialized inside the prefab, and the C# script only
fills in the text values. These helpers exist purely to make the authored
prefab file readable and maintainable while generating the YAML - they are
offline tools, they never run inside Unity.
"""

FONT_GUID = "2644e223131232f41a7199fc4aef1e34"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
TEXT_GUID = "5f7201a12d95ffc409449d95f23cf332"
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"

# TextAnchor values
ALIGN_UPPER_LEFT = 0
ALIGN_UPPER_CENTER = 1
ALIGN_UPPER_RIGHT = 2
ALIGN_MIDDLE_LEFT = 3
ALIGN_MIDDLE_CENTER = 4
ALIGN_MIDDLE_RIGHT = 5

# FontStyle values
FONT_NORMAL = 0
FONT_BOLD = 1
FONT_ITALIC = 2
FONT_BOLD_ITALIC = 3


def _yaml_scalar(value):
    """Quotes a scalar so that ':', '#' or leading spaces stay safe in YAML."""
    text = str(value)
    text = text.replace("'", "''")
    return "'" + text + "'"


def _color(value):
    """Turns '0.12, 0.12, 0.16, 0.92' into a Unity colour mapping."""
    if isinstance(value, (list, tuple)):
        parts = [str(p) for p in value]
    else:
        parts = [p.strip() for p in str(value).split(",")]
    while len(parts) < 4:
        parts.append("1" if len(parts) == 3 else "0")
    r, g, b, a = parts[:4]
    return "{r: %s, g: %s, b: %s, a: %s}" % (r, g, b, a)


def _clamp01(value):
    value = round(float(value), 4)
    return max(0.0, min(1.0, value))


class UIEmitter:
    """Writes GameObject/RectTransform/CanvasRenderer/Image/Text/Button blocks."""

    def __init__(self, builder):
        self.b = builder
        self.children = {}

    # --- low level ---------------------------------------------------
    def _game_object(self, go_id, name, component_ids, active=1):
        comp_lines = "\n".join("  - component: {fileID: %s}" % c for c in component_ids)
        self.b.add(
            """--- !u!1 &%s
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
%s
  m_Layer: 5
  m_Name: %s
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: %d"""
            % (go_id, comp_lines, name, active)
        )

    def _rect_top_left(self, tr_id, go_id, father, x, y, w, h):
        """Child anchored to the top-left corner of its parent (pixels)."""
        self.b.add(
            """--- !u!224 &%s
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: %s}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: %s, y: %s}
  m_SizeDelta: {x: %s, y: %s}
  m_Pivot: {x: 0, y: 1}"""
            % (tr_id, go_id, father, x, -y, w, h)
        )

    def _rect_bottom_center(self, tr_id, go_id, father, x, y, w, h):
        """Child anchored to the bottom-center edge of its parent (pixels)."""
        self.b.add(
            """--- !u!224 &%s
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: %s}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0}
  m_AnchorMax: {x: 0.5, y: 0}
  m_AnchoredPosition: {x: %s, y: %s}
  m_SizeDelta: {x: %s, y: %s}
  m_Pivot: {x: 0.5, y: 0.5}"""
            % (tr_id, go_id, father, x, y, w, h)
        )

    def _rect_fill(self, tr_id, go_id, father):
        """Child stretched over the full rect of its parent."""
        self.b.add(
            """--- !u!224 &%s
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: %s}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}"""
            % (tr_id, go_id, father)
        )

    def _rect_stretch(self, tr_id, go_id, father, y_offset=-18, y_delta=-36, children=None):
        """Panel stretched over the whole window content area."""
        if children:
            child_block = "  m_Children:\n" + "\n".join(
                "  - {fileID: %s}" % c for c in children
            ) + "\n"
        else:
            child_block = "  m_Children: []\n"
        self.b.add(
            """--- !u!224 &%s
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
%s  m_Father: {fileID: %s}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: %d}
  m_SizeDelta: {x: 0, y: %d}
  m_Pivot: {x: 0.5, y: 0.5}"""
            % (tr_id, go_id, child_block, father, y_offset, y_delta)
        )

    def _canvas_renderer(self, cr_id, go_id):
        self.b.add(
            """--- !u!222 &%s
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_CullTransparentMesh: 1"""
            % (cr_id, go_id)
        )

    def _image(self, img_id, go_id, color, raycast=0, image_type=0):
        self.b.add(
            """--- !u!114 &%s
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {fileID: 0}
  m_Color: %s
  m_RaycastTarget: %d
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 0}
  m_Type: %d
  m_PreserveAspect: 0
  m_FillCenter: 1"""
            % (img_id, go_id, IMAGE_GUID, _color(color), raycast, image_type)
        )

    def _text(
        self,
        txt_id,
        go_id,
        content,
        color,
        size,
        align,
        style,
        best_fit,
        min_size,
        max_size,
    ):
        self.b.add(
            """--- !u!114 &%s
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_Material: {fileID: 0}
  m_Color: %s
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {fileID: 12800000, guid: %s, type: 3}
    m_FontSize: %d
    m_FontStyle: %d
    m_BestFit: %d
    m_MinSize: %d
    m_MaxSize: %d
    m_Alignment: %d
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: %s"""
            % (
                txt_id,
                go_id,
                TEXT_GUID,
                _color(color),
                FONT_GUID,
                size,
                style,
                best_fit,
                min_size,
                max_size,
                align,
                _yaml_scalar(content),
            )
        )

    def _button(self, btn_id, go_id, target_img, color):
        parts = [float(v) for v in color.split(",")[:3]]
        normal = _color([str(_clamp01(v)) for v in parts] + ["1"])
        highlight = _color([str(_clamp01(v + 0.15)) for v in parts] + ["1"])
        pressed = _color([str(_clamp01(v - 0.1)) for v in parts] + ["1"])
        self.b.add(
            """--- !u!114 &%s
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {fileID: 0}
    m_SelectOnDown: {fileID: 0}
    m_SelectOnLeft: {fileID: 0}
    m_SelectOnRight: {fileID: 0}
  m_Transition: 1
  m_Colors:
    m_NormalColor: %s
    m_HighlightedColor: %s
    m_PressedColor: %s
    m_SelectedColor: %s
    m_DisabledColor: {r: 0.5, g: 0.5, b: 0.5, a: 0.5}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {fileID: 0}
    m_PressedSprite: {fileID: 0}
    m_SelectedSprite: {fileID: 0}
    m_DisabledSprite: {fileID: 0}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {fileID: %s}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []"""
            % (btn_id, go_id, BUTTON_GUID, normal, highlight, pressed, normal, target_img)
        )

    # --- composites --------------------------------------------------
    def _track(self, parent_tr, child_tr):
        self.children.setdefault(parent_tr, []).append(child_tr)

    def finalize(self):
        """Fills the m_Children lists of every panel created through this emitter."""
        for tr_id, kids in self.children.items():
            marker = "--- !u!224 &%s" % tr_id
            for i, chunk in enumerate(self.b.chunks):
                if chunk.startswith(marker):
                    if "  m_Children: []" not in chunk:
                        raise ValueError("RectTransform %s already has children" % tr_id)
                    self.b.chunks[i] = chunk.replace(
                        "  m_Children: []",
                        "  m_Children:\n" + "\n".join("  - {fileID: %s}" % k for k in kids),
                    )
                    break
            else:
                raise KeyError("RectTransform %s not found" % tr_id)

    def stretch_panel(self, name, father, y_offset=-18, y_delta=-36, active=1):
        """Full-window content panel (same layout rules as TestPanel)."""
        go_id = self.b.next_id()
        tr_id = self.b.next_id()
        self._rect_stretch(tr_id, go_id, father, y_offset=y_offset, y_delta=y_delta)
        self._game_object(go_id, name, [tr_id])
        return go_id, tr_id

    def background(self, name, father, x, y, w, h, color, raycast=0, active=1, track=True):
        go_id = self.b.next_id()
        tr_id = self.b.next_id()
        cr_id = self.b.next_id()
        img_id = self.b.next_id()
        self._rect_top_left(tr_id, go_id, father, x, y, w, h)
        self._canvas_renderer(cr_id, go_id)
        self._image(img_id, go_id, color, raycast=raycast)
        self._game_object(go_id, name, [tr_id, cr_id, img_id], active)
        if track:
            self._track(father, tr_id)
        return go_id, tr_id, img_id

    def text(
        self,
        name,
        father,
        x,
        y,
        w,
        h,
        content,
        color="1, 1, 1, 1",
        size=12,
        align=ALIGN_MIDDLE_LEFT,
        style=FONT_NORMAL,
        best_fit=0,
        min_size=8,
        max_size=30,
        active=1,
        fill=False,
        track=True,
    ):
        go_id = self.b.next_id()
        tr_id = self.b.next_id()
        cr_id = self.b.next_id()
        txt_id = self.b.next_id()
        if fill:
            self._rect_fill(tr_id, go_id, father)
        else:
            self._rect_top_left(tr_id, go_id, father, x, y, w, h)
        self._canvas_renderer(cr_id, go_id)
        self._text(txt_id, go_id, content, color, size, align, style, best_fit, min_size, max_size)
        self._game_object(go_id, name, [tr_id, cr_id, txt_id], active)
        if track:
            self._track(father, tr_id)
        return txt_id

    def button(
        self,
        name,
        father,
        x,
        y,
        w,
        h,
        label,
        color,
        label_size=16,
        label_color="1, 1, 1, 1",
        anchor="topleft",
        track=True,
        ids=None,
    ):
        if ids is None:
            go_id = self.b.next_id()
            tr_id = self.b.next_id()
            cr_id = self.b.next_id()
            img_id = self.b.next_id()
            btn_id = self.b.next_id()
        else:
            go_id, tr_id, cr_id, img_id, btn_id = ids
        if anchor == "bottomcenter":
            self._rect_bottom_center(tr_id, go_id, father, x, y, w, h)
        else:
            self._rect_top_left(tr_id, go_id, father, x, y, w, h)
        self._canvas_renderer(cr_id, go_id)
        self._image(img_id, go_id, color, raycast=1)
        self._button(btn_id, go_id, img_id, color)
        self._game_object(go_id, name, [tr_id, cr_id, img_id, btn_id])
        self.text(
            "Text",
            tr_id,
            0,
            0,
            w,
            h,
            label,
            color=label_color,
            size=label_size,
            align=ALIGN_MIDDLE_CENTER,
            style=FONT_BOLD,
            fill=True,
        )
        if track:
            self._track(father, tr_id)
        return btn_id, img_id, tr_id
