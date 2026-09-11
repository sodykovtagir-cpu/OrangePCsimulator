# Desktop file import

NativeFilePicker and NativeGallery originally returned a cancelled result in non-Editor desktop players. Both now use `OrangePC.NativeDialogs` for selection:

- Windows: Unicode Win32 open-file dialog (`comdlg32.dll` already supplied by Windows).
- macOS: the system file chooser through `osascript`.
- Linux: `zenity`, with `kdialog` as a fallback. At least one must be installed.
- Android/iOS permission and native picker calls are unchanged. Editor dialogs are unchanged.

This fixes the common selection path for world-save import, Paint images, wallpapers, video, Lua icons and workshop covers. Export behaviour is not changed by this patch.

Dialogs are modal and run on the calling Unity thread. Cursor lock/visibility is temporarily released and restored, callbacks remain on the same thread, and cancellation returns `null`. Paths with spaces and Unicode are supported. MIME/UTI filters from mobile callers are normalized for desktop use.

## Regression checks

`python3 tools/test_desktop_file_picker.py` compiles the backend and real picker methods for Windows/macOS/Linux and tests filter conversion, Unicode/multiple selection parsing, Win32 struct layout, routing, cancellation, busy state and cursor restoration.

Before releasing, test an actual Windows player (not just Unity Editor): import a `.pc`/`.opc`, choose a PNG/JPEG in Paint/wallpapers/Lua icon picker, choose a video, select a workshop cover, cancel and reopen a picker. Native desktop GUI interaction is not covered by the headless Linux tests.
