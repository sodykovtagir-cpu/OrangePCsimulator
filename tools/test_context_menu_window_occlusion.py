#!/usr/bin/env python3
"""Source-level guards for context menus behind PCOS windows.

Requires Play Mode for actual multi-canvas pointer/raycast verification.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
menu = (root / 'DesktopContextMenu.cs').read_text()
os = (root / 'PC/Component/Software/OS/OperatingSystem.cs').read_text()
files = (root / 'PC/Component/Software/FileManager.cs').read_text()
icons = (root / 'PC/Component/Software/FileIcon.cs').read_text()
row = (root / 'PC/Component/Software/DeviceManagerRow.cs').read_text()
pane = (root / 'PC/Component/Software/DeviceManagerPane.cs').read_text()
manager = (root / 'PC/Component/Software/DeviceManager.cs').read_text()


def body(source, signature):
    start = source.index(signature)
    left = source.index('{', start)
    depth = 0
    for index in range(left, len(source)):
        depth += (source[index] == '{') - (source[index] == '}')
        if depth == 0:
            return source[left + 1:index]
    raise AssertionError(signature)


hit = body(os, 'public App TopWindowAtScreenPoint(')
assert 'appParent.childCount - 1' in hit and 'i >= 0; i--' in hit
assert 'activeInHierarchy' in hit and 'app.IsMinimized' in hit
assert 'RectTransformUtility.RectangleContainsScreenPoint(' in hit
assert 'screenPosition, eventCamera' in hit
assert 'return app;' in hit

assert 'operatingSystem.TopWindowAtScreenPoint(screenPos, GetEventCamera())' in menu
assert 'WindowAt(screenPos) == app' in body(menu, 'public bool CanReceiveAppPointer(')
assert 'WindowAt(screenPos) == null' in body(menu, 'public bool CanReceiveDesktopPointer(')
assert 'if (WindowAt(screenPos) != null) return false;' in body(menu, 'private bool CanOpenMenuAt(')
for signature in ('private void OpenContextAt(', 'public void ShowDesktopMenu(Vector2 screenPos, bool skipDesktopCheck)'):
    assert 'if (WindowAt(screenPos) != null) return;' in body(menu, signature), signature
# The explicit check in ShowDesktopMenu covers touch long-press even when
# its caller skips the ordinary desktop-raycast check.
assert 'ShowDesktopMenu(current, true)' in menu
assert 'CanReceiveDesktopPointer' in icons
assert 'CanReceiveAppPointer(this, Input.mousePosition)' in files
assert 'CanReceiveAppPointer(explorer, eventData.position)' in files
assert 'CanReceiveAppPointer(GetComponentInParent<FileManager>(), eventData.position)' in files
assert 'CanReceiveAppPointer(manager, data.position)' in row
assert 'CanReceiveAppPointer(manager, data.position)' in pane
assert 'CanReceiveAppPointer(this, screenPos)' in manager
# File Manager and Device Manager retain their own context menus when they
# are in front; only the desktop/covered windows are blocked.
assert 'ShowExplorerMenu' in files and 'ShowFileMenu' in files
assert 'ShowDeviceManagerMenu(this, screenPos)' in manager
print('PASS: right-click/long-press desktop menus blocked by visible PCOS windows')
