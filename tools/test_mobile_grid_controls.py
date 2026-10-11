#!/usr/bin/env python3
"""Regression guards for mobile grid UI events and pause visibility.

Run an Android smoke test to check EventSystem/canvas sorting in the real scene.
"""
from pathlib import Path
from itertools import combinations
import re

scripts = Path(__file__).resolve().parent.parent / 'Assets/Scripts/Assembly-CSharp'
controls = (scripts / 'DragRotateControls.cs').read_text(encoding='utf-8')
ray = (scripts / 'Raycast.cs').read_text(encoding='utf-8')
main = (scripts / 'Main.cs').read_text(encoding='utf-8')
custom = (scripts / 'MobileCustomizeManager.cs').read_text(encoding='utf-8')

layer = controls.split('private static void SetHudLayer(')[1].split('private static void EnsureRaycaster(')[0]
assert 'own.overrideSorting = true' in layer
assert 'EnsureRaycaster(own)' in layer  # parent raycaster cannot service nested canvas
assert 'SetHudLayer(panel, HudSortingOrder);' in controls
assert 'SetHudLayer(panel, HudSortingOrder + 1);' in controls
assert 'drag.Init(LayoutId, parent)' in controls
assert 'DragRotateControls.SetPreviewCatcher(controlsRoot, value)' in custom
preview = controls.split('public static GameObject BuildPreview(')[1].split('public static void SetPreviewCatcher(')[0]
assert 'text.raycastTarget = false' in preview
assert 'Player.Instance.pauseControls' in ray.split('private void Update()')[1].split('private void LateUpdate()')[0]
assert 'DragRotateControls.Instance.SetVisible(false)' in main.split('public void StopAllControl()')[1].split('public void ResumeAllControl()')[0]
assert 'Player.Instance.pauseControls' in controls.split('private void Update()')[1].split('private void StopHold()')[0]
# Enter game -> menu -> enter game again, without restarting the app. The
# destroyed Unity Instance must be enough to allow a new panel; a separate
# process-lifetime "already checked" flag must never block this path.
create = controls.split('public static void EnsureExists(Raycast owner)')[1].split('private static bool ShouldBuild()')[0]
destroy = controls.split('private void OnDestroy()')[1].split('private void Update()')[0]
assert 'if (Instance != null) return;' in create
assert 'Instance = controls;' in create
assert 'if (Instance == this) Instance = null;' in destroy
assert 'platformChecked' not in controls
assert 'DragRotateControls.EnsureExists(this)' in ray.split('private void Update()')[1].split('private void LateUpdate()')[0]

# Same geometry in the actual panel and the draggable preview; test all nine
# touch rectangles, not just the adjacent arrows. A shared positive scale
# preserves both clearances and the panel bounds on every screen size.
game = controls.split('private void Build(Canvas canvas)')[1].split('private void RefreshLabel()')[0]
for part in (game, preview):
    assert 'new Vector2(PanelSize * s, PanelSize * s)' in part
    for name, expected in [('big', 'BigArrowSize'), ('gap', 'BigArrowOffset'),
                           ('fine', 'FineArrowSize'), ('fineGap', 'FineArrowOffset')]:
        assert f'float {name} = {expected} * s;' in part
    assert 'ToggleSize * s' in part
    for direction in ('Left', 'Right', 'Up', 'Down'):
        assert f'"Arrow{direction}"' in part and f'"Fine{direction}"' in part

values = {key: float(value) for key, value in re.findall(
    r'private const float (PanelSize|BigArrowSize|BigArrowOffset|FineArrowSize|FineArrowOffset|ToggleSize) = ([0-9.]+)f;', controls)}
assert len(values) == 6
panel, big, gap, fine, fine_gap, toggle = (values[key] for key in (
    'PanelSize', 'BigArrowSize', 'BigArrowOffset', 'FineArrowSize', 'FineArrowOffset', 'ToggleSize'))
buttons = [('toggle', 0, 0, toggle)]
for size, offset, label in ((big, gap, 'big'), (fine, fine_gap, 'fine')):
    for direction, x, y in (('left', -offset, 0), ('right', offset, 0),
                            ('up', 0, offset), ('down', 0, -offset)):
        buttons.append((label + direction, x, y, size))
for name, x, y, size in buttons:
    assert abs(x) + size / 2 < panel / 2, f'{name} outside panel X'
    assert abs(y) + size / 2 < panel / 2, f'{name} outside panel Y'
for (a, x, y, s1), (b, X, Y, s2) in combinations(buttons, 2):
    # Square backgrounds are the raycast targets. They may touch corners but
    # must never overlap with positive area (which steals arrow clicks).
    assert abs(x - X) >= (s1 + s2) / 2 or abs(y - Y) >= (s1 + s2) / 2, f'{a} overlaps {b}'
assert 'Mathf.Min(scale, fit)' in controls.split('public static float UiScale()')[1].split('private void Build(')[0]
print('PASS: panel recreates on scene re-entry; game/preview hitboxes do not overlap; touch and pause guards hold')
