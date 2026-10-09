# Orange PC Simulator — Changelog

## Added

### Grid mode (Orange Grid)

- A visible birch grid. When the mode is on, it lies on the surface under the crosshair (floor, wall, table) and highlights the cell the part will land in. Before this, the grid only snapped the part and was invisible.

- Parts now move cell by cell while dragging: the grab point snaps to the world grid and the drawn lines match the snap points, so you always see where the part goes.

- Every fourth line is thicker, the grid fades softly towards the edges and lines never get thinner than a pixel — the grid stays visible from afar.

- Grid button in the hotbar, both on the mobile panel and the standalone panel. It has a new snap-to-grid icon; the icon turns orange while the mode is on.

- Bind setting: Menu → Settings → PC controls → Grid (G by default). The missing Auto rotation bind was added too.

- Height snapping is back on: in grid mode the part snaps on the Y axis too (up/down), not only horizontally.

- Fallback renderer: if the grid shader is unavailable, the grid is drawn with GL lines.

### Rotating items while dragging

- The dragged item can be rotated with arrows — only in grid mode: on mobile a panel with four arrows (left/up/right/down) appears, on PC the regular arrow keys work (there is no panel on PC). The step is 90° per press and the angle is always a multiple of the step: e.g. 87° → press → 180°, reverse → 90°. For a PC/miner the whole case turns at once. In grid mode the orientation changes only via the arrows: auto-rotation and collision wobble are disabled. The panel is created automatically and needs no scene changes.

- Orientation lock in grid mode: when you grab an item with the grid on, all its bodies get FreezeRotation — bumping the item won't flip or tilt it; only the arrows can rotate it. On release (or when the grid is turned off) the constraints are restored.

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

- In grid mode a held item no longer drifts or flies off on its own. The snap target was computed from the pivot instead of the centre of mass, so every frame the item was shifted by the offset between the two — light and off-centre items were dragged to one side. Everything now uses the centre of mass: without arrow presses the item stays put, and an arrow step is no longer lost between physics steps.

- Turning the grid off during a drag releases the item instead of pulling it towards the crosshair: the spring no longer yanks an item (especially a light one) right after the grid is switched off.

- PC/miner rotation in grid mode: components now rotate TOGETHER with the case — the whole assembly (case, motherboard, glass, boards) turns around a single pivot. Before, each part spun around its own axis, joints got stretched, and everything flew apart on release.

- Up/down arrows: no more falling "onto the edge". Rotation is now decomposed into true yaw/pitch: up is a clean 90° tumble backwards (standing → facing up → upside down → facing down → standing). Alignment is preserved: the angle is always a multiple of the step (87° → press → 180° → reverse → 90°).

- A flipped item no longer gets stuck in the floor: if after a flip its bottom is below the surface, it is lifted back in one go (the per-frame step limit is disabled for that case).

- The item now drives over small shards/debris on the floor: if a cell is blocked by a low obstacle, the target is raised (a step of up to 0.5 m) and the item keeps going. Tall obstacles (walls, other PCs, slabs) still block.

- No more "trail" of parts when dragging/rotating in grid mode: during the drag the whole assembly becomes kinematic — MovePosition/MoveRotation apply instantly, parts move in perfect sync with the case. isKinematic and constraints are restored on release.

- The item no longer flies through walls and piles even when the aim is pulled far past them: not only the target cell but the WHOLE path is now checked (continuous box sweep) — thin walls no longer slip through either.

- Flipping no longer sinks the item below the floor or clips neighbours: before rotating, the assembly's post-rotation bounds are predicted, the bottom is auto-raised to the level the item stood on, and if the predicted bounds would touch anything the rotation is cancelled entirely.

- Neighbours no longer explode while you drag: the penetration tolerance is reduced from 1.5 cm to 5 mm, and the gates now tell "standing on it" (floor/desk — not a block), "grazing" and "ramming into it" (block) apart. The kinematic assembly no longer ploughs into other PCs.

- The item no longer launches on release: residual kinematic velocity from MovePosition is zeroed before isKinematic/constraints are restored (and in plain drag as well).

- The gates now see the WHOLE world (surfaceMask): before, they only looked at the items layer, so walls "didn't exist" — pull the aim past a wall and the item launched through it. The player and the dragged item are still ignored.

- No more tripping on small stuff: m2 covers and debris up to ~6 cm are simply driven over without blocking.

- Adaptive grid: if the target cell is blocked by a tall obstacle, the item slides along the direction and leans FLUSH against it (a couple of mm) — you can lean it against a wall or another object.

- Movement in grid mode is ARROWS-ONLY now: the item no longer follows the crosshair. On grab it snaps to the nearest cell, then every press is a cell step relative to the camera (up = away from you). The same gates apply: it won't enter a wall and can lean flush against walls/PCs.

- R on PC and the center panel button on the phone toggle the arrows mode: "move / rotate" (the button label shows the current mode).

- Ctrl on PC and the small edge arrows on the phone panel — reduced step: movement is a fifth of a cell, rotation is 15° at the 90° step.

- Two-stage grab (grid mode only): the first press only SELECTS the item — a teal outline appears around it (inverted hull over all meshes); the second press on the selected item starts the drag. Light items no longer get flung away by an accidental touch. Pressing empty space deselects. Without the grid, grabbing works in one press as before.

- The grid no longer turns on by itself: PlayerPrefs persistence removed (a teardown bug inverted the saved setting, so the next launch started with the grid on). The grid is now always off by default and is enabled with G or the hotbar button.

- In grid mode the item moves cell by cell SHARPLY without breaking anything around: a cell is occupied instantly, but only if it is free — walls, floor slabs and other objects block it (it is "afraid" of them). No forces are applied at all, so the item doesn't wreck PCs on touch and never flies through anything. Height: the item's bottom is placed on the snapped plane and never sinks into the floor.

- A PC no longer falls apart when snapped/rotated in grid mode: the assembly is now collected through the joint graph, not only hierarchy children — the motherboard (sits in a setParent: 0 slot, held only by a FixedJoint), glass and boards move and rotate together with the case, and their joints are protected during the drag. Before, the motherboard stayed behind and tore its joints ("first it doesn't move, then it bangs"). Vertically the snap advances one cell per frame — no weird slam to the ground.

- A PC/miner no longer falls apart when you grab it: the whole case moves as one (grid snap and rotation move all its bodies at once), and the case joints become unbreakable for the duration of the drag. Without grid mode a part can still be pulled out of the case by force — unless you grabbed the case itself, then the build holds together.

- The grid no longer climbs onto the player or the dragged item: the surface under the crosshair is found through them, so the grid always lies on the floor/wall/table.

- The grid shader compiles again: a variable named `line` is a reserved HLSL keyword, which caused «syntax error: unexpected token 'line'» on d3d11 (shader lines 108 and 119). Renamed to `gridLine`.

- The grid no longer overlays the item you are dragging: an invisible occluder around the item marks its area, so the grid is not drawn over the item's transparent parts (glass panels) or where it is closer to the camera than the item itself.

- Dragged parts now actually move cell by cell. Only the grab point used to be snapped, so a case just hung off it by a corner and never lined up with the grid. Now the centre of mass of the part itself is snapped and the grab point is shifted along with it.

- The grid no longer pulls the item with a spring (the spring force is disabled in grid mode): the position is set by the sharp cell snap — no smeared steps and no pulling into walls.

- The grid is «birch» now: the line colour is #30D5C8 (turquoise, as set by the author), the highlight of the cell under the cursor is a lighter tint of it.

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
