# Desktop file import

NativeFilePicker and NativeGallery originally returned a cancelled result in non-Editor desktop players. Both now use `OrangePC.NativeDialogs` for selection:

- Windows: Unicode Win32 open-file dialog (`comdlg32.dll` already supplied by Windows).
- macOS: the system file chooser through `osascript`.
- Linux: `zenity`, with `kdialog` as a fallback. At least one must be installed.
- Android/iOS permission and native picker calls are unchanged. Editor dialogs use the same normalized type filters.

This fixes the common selection path for world-save import, Paint images, wallpapers, video, Lua icons and workshop covers. World-save export now has a native desktop save dialog. It preserves the actual source extension (`.opc` or legacy `.pc`), rather than renaming one format into the other. The old Editor `.sav` default was removed.

Dialogs are modal and run on the calling Unity thread. Cursor lock/visibility is temporarily released and restored, callbacks remain on the same thread, and cancellation returns `null`. Paths with spaces and Unicode are supported. MIME/UTI filters from mobile callers are normalized for desktop use. Explicit type filters have no “All files” escape, and returned paths are rechecked so typed filenames cannot bypass them. Paint/wallpaper/icons allow PNG/JPG/JPEG; video allows the configured video containers; world import allows only `.pc`/`.opc`. Unknown explicit filters fail closed. Mobile custom-save providers retain their broad picker because they do not register PC/OPC MIME types; the import callback still rejects every other extension.

## Regression checks

`python3 tools/test_desktop_file_picker.py` compiles the backend and real picker methods for Windows/macOS/Linux and tests filter conversion, Unicode/multiple selection parsing, Win32 struct layout, routing, cancellation, busy state and cursor restoration.

Before releasing, test an actual Windows player (not just Unity Editor): import a `.pc`/`.opc`, choose a PNG/JPEG in Paint/wallpapers/Lua icon picker, choose a video, select a workshop cover, cancel and reopen a picker. Native desktop GUI interaction is not covered by the headless Linux tests.

`python3 tools/test_save_format_export.py` checks actual export logic, cancellation, byte-preserving copies and prevention of accidental format changes.
