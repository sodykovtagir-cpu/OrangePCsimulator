# Orange PC Simulator — Changelog

## Added

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
