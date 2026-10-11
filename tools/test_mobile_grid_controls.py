#!/usr/bin/env python3
"""Regression guards for mobile grid UI events and pause visibility.

Run an Android smoke test to check EventSystem/canvas sorting in the real scene.
"""
from pathlib import Path

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
print('PASS: nested Canvas receives touches; preview can drag; gameplay arrows hide on pause')
