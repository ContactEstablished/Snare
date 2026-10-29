# Phase 6 tasks

See [spec.md](spec.md).

## Tasks

- [x] Core: `HotkeyGesture`, `HotkeyOptions`, new `SnareOptions` fields, validation; plus tests
- [x] Library: library root follows settings; `LibraryBackup` (online backup, zip, prune); plus tests
- [x] Interop: `StartupRegistration` (Run key); hotkey key names to virtual keys; plus tests
- [x] App: settings store (settings.json layered with reload), hotkeys re-register on change, Start with Windows sync, tray Settings… and Back up library, Settings window; plus tests
- [x] Rendering and editor: Callout tool; golden images; plus tests
- [x] App: exit flush for editors, exit prompt while recording, editor Resize… dialog; plus tests
- [ ] Code review, merge, push

## Checked by the agent (Oct 29, 2026)

- Snare starts with the hotkeys read from settings. Snagit still holds PrtSc, and the notice now also points at Settings. The Debug build logs that it skipped the Run key.
- A Release build compiles the Start with Windows code.
- The Callout golden image was reviewed: one outline around the bubble and tail, each tail leaving from the nearest side, and the text padded inside.
- The backup test opens the zipped database and finds every capture row, plus every original.

## Known gaps

- No live mic level meter (see spec.md, Out of scope).
- Restoring a backup is done by hand (steps below).
- Resizing a callout's bubble doesn't reflow its text; long lines are clipped at the bubble's edge.
- Exiting during a scrolling capture doesn't save it.

## Awaiting Matt

Settings and Start with Windows act on the real machine, so check them with a Release build. Quit any running Snare, run `./tools/publish.ps1` (it publishes to `%LOCALAPPDATA%\Programs\Snare`), then start `Snare.exe` from there.

- [ ] **Settings opens:** tray → Settings… opens one window, and a second click brings it forward. While it's open, PrtSc does nothing.
- [ ] **Change a hotkey:**
  - Click the Grab text box, press `Ctrl+Alt+G`, then Save.
  - The tray menu shows Ctrl+Alt+G, `Ctrl+Alt+G` grabs text, and `Ctrl+Alt+PrtSc` no longer does.
  - Set it back.
  - Set Record video to `PrtSc`: Save is disabled, with "PrtSc is set for both Capture and Record video".
  - Backspace in a box shows "Off".
- [ ] **Library folder:**
  - Set a new folder with Browse…, Save, and take a capture: it lands in the new folder.
  - Older captures still open from the library.
- [ ] **Capture defaults:** switch to JPG at quality 80 and tick "Open the editor after a capture". The next capture is a .jpg and opens in the editor.
- [ ] **Start with Windows:**
  - With the box ticked, `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` has a `Snare` value pointing at the published `Snare.exe`.
  - Sign out and back in: Snare is in the tray.
  - Untick it and Save: the value is gone.
- [ ] **Backup:**
  - Tray → Back up library shows a notification naming the zip in `Documents\Snare Backups`.
  - The zip holds `library.db` and `originals\`.
  - Settings shows "Last backup: …".
- [ ] **Restore, by hand:**
  - Quit Snare.
  - Rename `%LOCALAPPDATA%\Snare` to `Snare.old`, create an empty `%LOCALAPPDATA%\Snare`, and unzip a backup into it.
  - Start Snare: the library, tags and edits are all there.
  - Then delete `Snare.old`, or put it back.
- [ ] **Callout:**
  - In the editor press `K`, drag a bubble, type two lines and click away.
  - In Select, drag the tail's handle around the bubble: the tail moves to the nearest side.
  - Double-click to edit the text, and change the fill and outline width in the style panel.
  - Save: the export matches the editor.
- [ ] **Resize…:**
  - In the editor, press Resize…, set 50% and press Resize. The export is half size.
  - Undo restores it, and "Natural size" clears it.
- [ ] **Exit while recording:**
  - Start a recording, then tray → Exit.
  - Cancel keeps recording.
  - Yes stops it, adds it to the library and quits.
  - On another run, No saves nothing.
- [ ] **Exit with edits:**
  - Draw an arrow in an editor and at once tray → Exit.
  - Reopen Snare and the capture: the arrow is there, in the editor and in the export.
