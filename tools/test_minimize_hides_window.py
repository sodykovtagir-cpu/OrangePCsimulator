#!/usr/bin/env python3
"""Regression: minimizing an app window must actually hide it.

The old minimize animation could be slowed by UI responsiveness and a taskbar
click could be misclassified as 'raise' when a non-window overlay was the top
sibling. These checks lock the guaranteed-hide behavior.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/App.cs"
OS = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/OS/OperatingSystem.cs"


def test_minimize_has_guaranteed_hide():
    text = APP.read_text(encoding="utf-8")
    assert "HideMinimizedNow" in text
    assert "gameObject.SetActive(false)" in text
    assert "if (closing || minimized) return;" not in text
    assert "if (gameObject.activeSelf) HideMinimizedNow();" in text


def test_minimize_animation_not_lag_dependent():
    text = APP.read_text(encoding="utf-8")
    start = text.index("private IEnumerator MinimizeRoutine()")
    end = text.index("private void HideMinimizedNow()", start)
    routine = text[start:end]
    assert "UiResponsiveness" not in routine
    assert "Time.unscaledDeltaTime" in routine
    assert "duration + 0.5f" in routine


def test_topmost_only_counts_real_apps():
    text = OS.read_text(encoding="utf-8")
    start = text.index("private bool IsTopmostWindow(App app)")
    end = text.index("\n        }", start) + len("\n        }")
    body = text[start:end]
    assert "GetComponent<App>()" in body
    assert "other.IsMinimized" in body
    assert "app.IsMinimized" in body


if __name__ == "__main__":
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            fn()
            print("PASS", name)
