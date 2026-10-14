# Device Manager / Autorun.conf

## Device Manager

A separate app from **My Devices**. Installed automatically when an existing or newly installed PCOS boots. Its interface is now composed of actual UGUI objects in `Assets/Resources/apps/DeviceManager.prefab`: expand **Manager UI → Home / Targets / Action / Export** to edit buttons, tabs, labels, inputs, scroll regions, anchors and colours in the Unity Editor. **Row Template — edit for all list entries** is the editable visual copied for each registered device; only the rows and File Manager-style context menu are instantiated at runtime. Script actions and data bindings stay in `DeviceManager.cs`. The old borrowed wireless `Scroll View` and `Refresh` have been removed from this prefab; **My Devices** is unchanged. The body stretches under the PCOS title bar when maximized or restored; the wizard forms scroll on shorter screens.

Use **All devices / PCs / Miners** to filter registered IDs (hexadecimal, as in Terminal's `system-id`). **Discover** adds only machines in the selected tab; **Add ID** rejects IDs from another category. The type comes from the chassis (including Miner and BigMiner), not the motherboard name. Previously saved offline IDs with no known category appear only under All devices until discovered online. On the main page, click/tap rows to toggle multiple selections, use **Select visible** to add every displayed row to that selection, and **Remove** to unregister the selected IDs. Right-click a row (or long-press it on touch) to use **Power on, EZ: Bitcoin, EZ: Money, Video Player, Power off** for the selected set; right-click/long-press empty list space to open the same menu. The **All suitable devices** submenu always targets *all* matching devices in the world, regardless of registration, selection or active tab. Global presets still boot suitable offline machines first.

**Build script**: 1) choose PCs or miners and select IDs (Discover/Select visible); switching tabs retains earlier targets, so mixed scripts are possible. 2) Choose power, mining mode, video, app/file; for Bitcoin/Money/Video Player, enable **Run on all suitable devices in the world** to export a dynamic `remote-all` preset without selecting targets. 3) Name/export a `.tmn` file and optionally tick autorun. Exported scripts appear on the desktop. A world preset is dynamic (`remote-all`); individual selections save specific IDs, so scripts targeting destroyed/replaced motherboards will need re-exporting. Running Video Player opens the app; it does not select a video file.

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

Static guards: `python tools/test_device_manager_autorun.py` and `python tools/test_save_connections.py`. This workspace has no Unity editor or Android SDK, so manually open `DeviceManager.prefab` in the Editor to verify all four pages and the inactive row template are visible/editable without missing scripts; in Play Mode test maximize/restore and short screens, multiple row selection, Select visible, Remove, right-click and touch long-press on both a selected row and empty list space, each selected action and the global submenu (including unregistered world devices); Discover by PC/miner tab, including BigMiner and old offline IDs; mixed-target and global-toggle `.tmn` export; autorun and the existing boot/BSOD flows.
