# Orange PC Simulator — Changelog

## Added

### Grid mode (Orange Grid)

- A visible birch grid. When the mode is on, it lies on the surface under the crosshair (floor, wall, table) and highlights the cell the part will land in. Before this, the grid only snapped the part and was invisible.

- Parts now move cell by cell while dragging: the grab point snaps to the world grid and the drawn lines match the snap points, so you always see where the part goes.

- Every fourth line is thicker, the grid fades softly towards the edges and lines never get thinner than a pixel — the grid stays visible from afar.

- Grid button in the hotbar, both on the mobile panel and the standalone panel. It has a new snap-to-grid icon; the icon turns orange while the mode is on.

- Bind setting: Menu → Settings → PC controls → Grid (G by default). The missing Auto rotation bind was added too.

- Height snapping is off by default, so parts no longer jump on the Y axis on tables and shelves.

- Fallback renderer: if the grid shader is unavailable, the grid is drawn with GL lines.

### Rotating items while dragging

- The dragged item can be rotated with arrows: a panel with four arrows (left/up/right/down) appears on screen, and on PC the regular arrow keys work. The step is 45° per press, a full turn is 8 presses. For a PC/miner the whole case turns at once. The panel is created automatically and needs no scene changes.

### Developer tools

- `tools/orange_forge.py` — a console tool for editing Unity scenes and prefabs without the editor: find nodes by name and path, deep-clone a subtree with all references, patch serialized fields and UnityEvent calls, insert new fields. It was used to add the grid button to every game scene and the bind row in the menu.

### Hardware and devices

- MSG ATX motherboards in black and white.

- Manufacturer logo on the boot screen instead of the "Starting Computer" caption. It is set in the board prefab itself, so every brand can have its own.

- StandMonitor floor display and Portable Monitor. Both are available in the shop, unlocked for 5 BTC.

- Black and white Aquarium ATX cases, a glass cover and boxes for the new items.

- Cameras with 512×512 and 1024×1024 resolution.

- Prebuilt PCs in the shop: 12 configurations from office machines to the flagship, plus six miners. They arrive assembled in a protective crate.

### Graphics card failures

- A graphics card damaged by a fall starts producing artifacts but keeps working. Damage accumulates: at the fourth stage the card fails for good.

- Nine kinds of visual failure: colour bars, thin white scanline comb, image jitter, rolling frame as on a bad signal, shifted colours, black squares of dead video memory, signal loss, buffer overflow garbage and a blue screen.

- Artifacts come in bursts rather than as a constant overlay: a card may spray garbage for a second and then run normally for a long while. The worse the damage, the more frequent and longer the bursts.

- The black squares of dead memory can be wiped away by dragging a window across them — like a stuck frame buffer that refreshes when something is redrawn over it.

- A damaged card sometimes prevents the computer from starting. The check runs on every attempt, so the next press of the power button may well succeed.

- A card can drop out under load — during a benchmark or video playback. The video then either fails to decode at all or plays back distorted.

### Interface and convenience

- CRT effect on the screen of the physical CRT monitor. The filter is not applied to the maximised desktop.

- 1024×1024 and 1024×2240 HD presets in Paint.

- 1024×2240 HD banners in PrintExpert and a rotating banner preview before purchase.

- HD mode in ModForge that lifts the 512 pixel limit, plus a rotating preview of the cover on the case.

- Wallpaper layout modes: fill, fit, stretch, centre and tile. Wallpapers can be set from in-game images.

- Pause, seeking and elapsed time in the video player. Video keeps playing while the window is minimised and stops when the computer is switched off or crashes.

- Window minimising and full-screen maximising, plus free dragging of desktop icons.

- Download progress percentage for workshop saves.

- The PCOS clock shows in-game time instead of system time. An in-game day lasts 24 real minutes, time only advances during play and is stored with your progress.

- Connecting a monitor now also works by clicking the case, not just the motherboard itself.

- Translations for all new interface elements across the game's 42 languages.

## Fixed

- The grid shader compiles again: a variable named `line` is a reserved HLSL keyword, which caused «syntax error: unexpected token 'line'» on d3d11 (shader lines 108 and 119). Renamed to `gridLine`.

- The grid no longer overlays the item you are dragging: an invisible occluder around the item marks its area, so the grid is not drawn over the item's transparent parts (glass panels) or where it is closer to the camera than the item itself.

- Dragged parts now actually move cell by cell. Only the grab point used to be snapped, so a case just hung off it by a corner and never lined up with the grid. Now the centre of mass of the part itself is snapped and the grab point is shifted along with it.

- With the grid on, the part snaps into the cell instantly. The soft drag spring (100/5) used to smear the 0.5 m step into a smooth slide, and the stiff spring added a bounce; now the spring is disabled in grid mode and the body is placed with its centre of mass exactly on the snapped cell — no gliding, no bounciness. The Grid Spring Frequency / Grid Spring Damping fields were removed as no longer needed.

- The grid is birch now: cream lines and a slightly lighter highlight for the cell under the cursor instead of the orange ones.

- A monitor responds to being connected on the first try. Previously you had to leave connection mode, pick the monitor up and drop it again.

- Items placed in the world are restored correctly from a save. Some of them used to disappear silently on load.

- Files and folders created from the context menu on a second drive no longer appear on the system drive. The same applies to pasting, deleting and renaming.

- Fixed missing translations and parameter substitutions in the language table. Translations are no longer reloaded for every caption.

## Optimisation

- In-game monitor screens are no longer redrawn every frame. The image refreshes at the display rate, and a monitor outside the view is not rendered at all. Screen sharpness is unchanged.

- Fixed screen proportions: previously every monitor was forced to a 16:9 ratio, which stretched the image on wide displays.

- The shop only builds item cards for the open page. Previously all 136 were created at once on first open, which caused a freeze.

- Lowered the physics catch-up limit: on weaker devices a frame rate dip no longer snowballs into a pile of calculations that slows the game down further.

- The input field and the monitor-under-cursor lookup no longer do redundant work every frame.
