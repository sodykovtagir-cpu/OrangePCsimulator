# UI style for in-game applications

Use the stock PCOS window chrome and the game's default button, toggle and slider sprites/states. In the regular game UI, keep the established orange accent; do not replace it wholesale with grey. Preserve warning/error colors when they communicate a real state, and do not recolor logos, authored images, equipment or world lighting.

Exceptions requested by the project owner:
- `Assets/Resources/apps/3DMork.prefab` (and its dynamic score text) stays neutral, without orange accent.
- `Assets/Resources/apps/Overclock.prefab` stays neutral. The whole window is editable there: GPU/RAM sliders, captions, status and buttons are serialized prefab children wired to `Overclock.cs`. No controls are generated when the app opens. Adjust layout, colors and labels in the Unity prefab.
- The magnet/grid-mode hotbar icon stays neutral; its active tint is set by `Functions.RefreshGridIcon()`.

For future custom applications, reuse the default control style instead of inventing bespoke buttons or sliders. Follow the standard orange accent unless the owner requests an exception. Test the prefab both in an ordinary window and maximized above the taskbar, and check the physical monitor in Unity Play Mode. `tools/test_ui_accent_exceptions.py` guards this palette policy.
