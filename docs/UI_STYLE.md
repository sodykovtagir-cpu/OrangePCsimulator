# UI style for new in-game applications

Use the stock PCOS window chrome and its existing button, toggle and slider visual assets (for example the CPU slider in `Assets/Resources/apps/Overclock.prefab`). Build additional controls from matching prefabs/sprites and neutral grey states; do not invent a branded orange accent or draw bespoke button/slider skins. Keep the game's existing light surface, dark text and normal Unity `Selectable` feedback. Keep warning/error colours only where they communicate a real state, not as decoration.

Overclock's additional GPU/RAM controls copy the stock CPU slider sprites without duplicating its persistent CPU-frequency listener. Its action buttons use that same stock handle sprite, neutral tint and the default `Button` transition. Test ordinary and maximized windows (especially above the taskbar), keyboard/mouse interaction and the physical monitor in Unity Play Mode.

Do not recolour logos, authored pictures, equipment, world lighting or the title/name of Orange PC. `tools/test_neutral_ui_style.py` guards serialized UI colours and script-level accents; when adding a new application, prefer an existing prefab's visuals to a new palette.
