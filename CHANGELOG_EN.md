# Orange PC Simulator — Changelog

## Added

### Hardware and devices

- MSG ATX motherboards in black and white.
- Manufacturer logo on the boot screen instead of the "Starting Computer" caption. It is set in the board prefab itself, so every brand can have its own.
- Slim IPS panels: a 480×320 desktop unit and a 320×240 case-mounted one. The pixel structure is rendered properly — each pixel is split into three RGB subpixel stripes like a real matrix, instead of a grid of black gaps.
- Screens app for managing the panels: displays images and animations in PIC, MOV, GIF, PNG and JPG. A panel is picked from the device list just like a printer.
- PC readouts on the IPS panel: CPU frequency, temperature, memory size, graphics card count and the clock. Four display modes, with readouts over the desktop wallpaper by default.
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
- Wallpaper layout modes: fill, fit, stretch, centre and tile.
- Pause, seeking and elapsed time in the video player. Video keeps playing while the window is minimised.
- Window minimising and full-screen maximising, plus free dragging of desktop icons.
- Download progress percentage for workshop saves.
- The PCOS clock shows in-game time instead of system time. An in-game day lasts 24 real minutes, time only advances during play and is stored with your progress.
- Connecting a monitor now also works by clicking the case, not just the motherboard itself.
- Translations for all new interface elements across the game's 42 languages.

## Changed

- Graphics cards are noticeably tougher: the damaging impact threshold is higher, and the burst of collisions from a single fall no longer counts several times. Previously a dropped card could pass every damage stage at once and die instantly.
- The flagship Dream PC build now ships with an MSG ATX motherboard. The board colour matches the case.
- The 2× and 4× LED matrix displays have been replaced by IPS panels.

## Fixed

- Monitor images are no longer stretched: previously every screen was forced to a 16:9 ratio, which distorted the picture on wide displays.
- Video and audio stop when the in-game computer is switched off or crashes.
- Improved sharpness of wallpapers made from in-game images.
- The desktop refreshes automatically after file operations, including saving an image in Paint.
- Icons keep their positions when files are renamed and the desktop refreshes.
- Files and folders created from the context menu on a second drive no longer appear on the system drive. The same applies to pasting, deleting and renaming.
- Items placed in the world are restored correctly from a save. Some of them used to disappear silently on load — including the figurine and the prebuilt PC crates.
- A monitor responds to being connected on the first try. Previously you had to leave connection mode, pick the monitor up and drop it again.
- Fixed the StandMonitor identifier: loading a save no longer replaces the new monitor with the old stand.
- Saves downloaded from the workshop can no longer be republished as your own.
- Fixed missing translations and parameter substitutions in the language table. Translations are no longer reloaded for every caption.

## Optimisation

- In-game monitor screens are no longer redrawn every frame. The image refreshes at the display rate, and a monitor outside the view is not rendered at all. Screen sharpness is unchanged.
- The system clock no longer rebuilds its string every frame on every running computer.
- The shop only builds item cards for the open page. Previously all 136 were created at once on first open, which caused a freeze.
- Prebuilt PCs no longer leave heavy physics behind: the temporary sturdiness settings revert to normal right after assembly.
- Lowered the physics catch-up limit: on weaker devices a frame rate dip no longer snowballs into a pile of calculations that slows the game down further.
- The input field and the monitor-under-cursor lookup no longer do redundant work every frame.
