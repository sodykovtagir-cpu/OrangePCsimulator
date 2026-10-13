# Device Manager / Autorun.conf

## Device Manager

A separate app from **My Devices**. Installed automatically when an existing or newly installed PCOS boots. It lists saved PC/miner system IDs (hexadecimal, as in Terminal's `system-id`), can discover all motherboards currently in the world, and offers Power on, EZ: BTC, EZ: $, Video Player and Power off. The three All presets schedule the action on every world computer with the corresponding app installed, even if it was not manually registered. Offline computers are powered on first. Failed or unpowered machines cannot run an action.

**Build script**: 1) select one or more saved IDs (Discover/Select all), 2) choose power, mining mode, video, app/file, or an all-world preset, 3) name/export a `.tmn` file and optionally tick autorun. Exported scripts appear on the desktop. A world preset is dynamic (`remote-all`); individual selections save specific IDs, so scripts targeting destroyed/replaced motherboards will need re-exporting. Running Video Player opens the app; it does not select a video file.

## PCOS commands

- `remote A1B2C3D4 startup` — power on a specific machine.
- `remote A1B2C3D4 bitcoin` / `cash` / `video` — power on if needed, then launch EZ Mining in BTC/money mode or Video Player.
- `remote A1B2C3D4 app "App Name"` / `file "path"` — launch an installed app or open a file on that PC.
- `remote-all bitcoin` / `cash` / `video` — target all capable computers in the active world.
- `launch "EZ Mining" bitcoin` — local EZ Mining with the BTC launch argument (`cash` for dollars).
- `run "script.tmn"` — run a saved terminal script; `run "app.lua"` runs Lua; `open "file"` opens it through its associated app.
- Existing Terminal commands such as `delay`, `remote`, `beep`, and `shutdown` still work. `//` starts a comment line. Scripts have a nesting limit of four to prevent recursive hangs.

## Boot file and repair

The user-editable file on the system disk is **`System/Autorun.conf`**. In-game Text Editor can open `.conf` files and saves changes to the original path. New installations and preassembled PCs include it; older saves get the default only on their first boot after updating. Afterwards deletion/corruption is *not* auto-repaired.

```text
//Initialize system DO NOT REMOVE!!!!
run boot.bin
init devices
init user
```

The comment is optional. Other Terminal commands can go on extra lines and execute once after login (or once after reaching the desktop with no password). Remove `run boot.bin` to freeze before loading and then show the existing blue-screen fault; remove `init user` to freeze during the OS-to-user transition; remove `init devices` to freeze and blue-screen when My Devices/Device Manager opens or a storage device is inserted. Deleting the whole file has the same early-boot failure. To repair a non-booting save, restore the missing line through an accessible disk or a save backup. Do **not** treat a missing file on an existing install as a fresh migration.

## Verification still needed in Unity

Static guards: `python tools/test_device_manager_autorun.py` and `python tools/test_android_open_with.py`. This workspace has no Unity editor or Android SDK, so manually test: fresh install and prebuilt miner boot; migration of an old save; manager controls (including offline and local PC), all-world presets and `.tmn` export; edit/save/restart Autorun.conf; each of the three crash timings; hot-plug a USB drive; and terminal launch parameter switching an already open EZ Mining instance.
