#!/usr/bin/env python3
"""Regression guards for mobile layout docking / off-screen clamping.

Bug: on Medium a panel docked to the screen side could slide off-screen in the
room. Two causes:
  * Smart anchor was chosen from the panel CENTER, so a wide panel pressed
    against the right edge stayed center-anchored with a large pixel offset.
  * LoadPosition / SnapAnchorKeepingPosition applied saved positions without a
    hard clamp, so an offset recorded in the settings preview (different size)
    or by the old bug pushed the panel outside the room canvas.

These checks guard the C# fixes and model the anchor/clamp math across a
preview -> room size change. They do not replace a Unity player build.
"""
from pathlib import Path

scripts = Path(__file__).resolve().parent.parent / 'Assets/Scripts/Assembly-CSharp'
mobile = (scripts / 'MobileFreeDraggable.cs').read_text(encoding='utf-8')
rig = (scripts / 'ControlRig.cs').read_text(encoding='utf-8')
adaptive = (scripts / 'MobileAdaptiveSlidePanel.cs').read_text(encoding='utf-8')

# --- source guards ---------------------------------------------------------
snap = mobile.split('private void SnapAnchorKeepingPosition()')[1].split('public void SavePosition()')[0]
assert 'ClampNormalized(LocalToNormalized(pivotLocal))' in snap, 'snap must clamp the pivot, not the center'
assert 'ClampAnchoredPosition();' in snap
assert 'GetSmartAnchor(normalizedPivot)' in snap

smart = mobile.split('private Vector2 GetSmartAnchor(')[1].split('private void SnapAnchorKeepingPosition()')[0]
assert 'minX' in smart and 'maxX' in smart, 'smart anchor must use panel edges, not only the pivot/center'
assert 'minY' in smart and 'maxY' in smart

load = mobile.split('public void LoadPosition()')[1].split('public void DeleteSavedPositions()')[0]
assert load.count('ClampAnchoredPosition();') >= 2, 'both saved formats must clamp on load'
assert 'SnapAnchorKeepingPosition();' in load

assert 'private void ClampAnchoredPosition()' in mobile
for section in ('public void SavePosition()', 'public void ResetPosition()'):
    body = mobile.split(section)[1].split('public void')[0]
    assert 'ClampAnchoredPosition();' in body or 'SnapAnchorKeepingPosition();' in body

# Factory/other rooms: bootstrap after Mobile is enabled + sliding hotbar.
assert 'MobileFreeDraggable.EnsureRoomHudLayout();' in rig.split('mobile.SetActive(true);')[1]
assert 'EnsureAdaptivePanel(root.Find("Functions/Panel"), "FunctionsPanel", bounds);' in mobile
assert 'public void Init(string id, RectTransform parent)' in adaptive


# --- math model ------------------------------------------------------------
def smart_anchor(pivot_norm, size_norm, pivot, zone=0.16):
    out = []
    for p, s, pv in zip(pivot_norm, size_norm, pivot):
        lo = p - s * pv
        hi = lo + s
        if lo <= zone:
            out.append(0.0)
        elif hi >= 1.0 - zone:
            out.append(1.0)
        else:
            out.append(0.5)
    return tuple(out)


def clamp_pos(parent, size, pivot, anchor, pos):
    # parent/size are (w, h); parent rect centered at 0.
    pw, ph = parent
    sw, sh = size
    px, py = pivot
    ax, ay = anchor
    anchor_local = ((ax - 0.5) * pw, (ay - 0.5) * ph)
    pivot_local = (anchor_local[0] + pos[0], anchor_local[1] + pos[1])
    min_x = -pw / 2 + sw * px
    max_x = pw / 2 - sw * (1 - px)
    min_y = -ph / 2 + sh * py
    max_y = ph / 2 - sh * (1 - py)
    cx = min(max(pivot_local[0], min_x), max_x)
    cy = min(max(pivot_local[1], min_y), max_y)
    return (cx - anchor_local[0], cy - anchor_local[1])


def edges(parent, size, pivot, anchor, pos):
    pw, ph = parent
    sw, sh = size
    px, py = pivot
    ax, ay = anchor
    pivot_local = ((ax - 0.5) * pw + pos[0], (ay - 0.5) * ph + pos[1])
    left = pivot_local[0] - sw * px
    right = left + sw
    bottom = pivot_local[1] - sh * py
    top = bottom + sh
    return left, right, bottom, top


# Wide arrows panel pressed to the right edge of a 1080-wide preview, then
# loaded on a 720-wide room canvas. Edge-based anchor keeps it on-screen; the
# old center anchor + pixel offset did not.
panel = (540.0, 540.0)
pivot = (1.0, 0.0)
preview = (1080.0, 1920.0)
room = (720.0, 1280.0)
size_norm = (panel[0] / preview[0], panel[1] / preview[1])
pivot_norm = (1.0, 0.2)  # right edge, lower area
anchor = smart_anchor(pivot_norm, size_norm, pivot)
assert anchor[0] == 1.0, f'right-docked panel must anchor to right edge, got {anchor}'
# offset from anchor after docking (pivot at preview right edge)
pos = (0.0, 0.0)
left, right, bottom, top = edges(room, panel, pivot, anchor, pos)
assert right <= room[0] / 2 + 1e-6 and left >= -room[0] / 2 - 1e-6

# A bad legacy save (center anchor + huge offset) is clamped back on-screen.
bad_anchor = (0.5, 0.5)
bad_pos = (540.0, 0.0)
clamped = clamp_pos(room, panel, pivot, bad_anchor, bad_pos)
left, right, bottom, top = edges(room, panel, pivot, bad_anchor, clamped)
assert right <= room[0] / 2 + 1e-6, f'clamped panel still off-screen: {right}'
assert left >= -room[0] / 2 - 1e-6

print('PASS: edge docking anchors to the screen side; saved/legacy positions clamp on-screen')
