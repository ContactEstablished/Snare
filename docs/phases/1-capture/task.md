# Phase 1 tasks

See [spec.md](spec.md).

## Tasks

- [x] Library store: schema scripts, EF Core context, `SqliteCaptureStore` with save order, unique export names, thumbnails, soft delete, plus tests
- [x] Rendering `ImageCodec` (PNG, JPG, WebP thumbnail), plus tests
- [x] Capture: `WindowSnapshot` (Interop), `DesktopCropper`, `SelectionSession`, cursor drawing, plus tests
- [x] App: capture overlay (MVVM) with region, window and full-screen selection, loupe and readout
- [x] App: `CaptureService`, clipboard, quick bar, delayed capture, repeat last region, tray menu
- [x] App: library window
- [x] HDR warning, `tools/publish.ps1`
- [x] `Snare.App.Tests`: off-screen smoke tests that load each window's XAML and render the overlay
- [x] Code review, merge, push

## Checked by the agent on Matt's PC (Oct 29, 2026)

- The app starts and registers `Shift+PrtSc`, `Ctrl+Shift+PrtSc` and `Ctrl+PrtSc`. `PrtSc` is still held by Snagit.
- A real freeze (`Snare.Capture.Tests.exe -explicit only`) listed the visible windows in z-order, with process names and titles, and found the source app under the cursor. The cursor was drawn into the grab.
- `AdvancedColor` reports 0 HDR displays (`Snare.Interop.Tests.exe -explicit only -diagnostics`).
- `tools/publish.ps1 -Destination <scratch folder>` produced a working self-contained build (about 300 MB) that starts.

## Awaiting Matt

Turn off Snagit's `PrtSc` hotkey first (or quit Snagit), then run `dotnet run --project src/Snare.App`. To use the installed copy instead, run `./tools/publish.ps1` and then `%LOCALAPPDATA%\Programs\Snare\Snare.exe`.

- [ ] **Region:** press `PrtSc` and drag a region that spans both monitors.
  - Both monitors dim, with the region clear.
  - The size label and the loupe readout match what you expect.
  - Resize from a corner and an edge, move it by dragging inside, nudge it with the arrow keys and Shift+arrows, then press Enter.
  - The quick bar appears below the region.
  - Pasting into Paint (or Slack) gives exactly that region, with the black gap where no monitor is.
- [ ] **Window:** press `PrtSc`, hover a window (it highlights with a thick border), then click. Only that window's frame is captured, without the invisible resize border. Try a window that hangs off a screen edge.
- [ ] **Full screen:** press `PrtSc`, then `F` over the upper monitor: you get 3440×1440. Press `PrtSc`, then `A`: you get 5120×2880.
- [ ] **Menus survive:** open a right-click menu in any app, press `PrtSc`, and capture it. The menu is in the image.
- [ ] **Cancel:** Esc, a right-click, and Alt+F4 each cancel without saving anything.
- [ ] **Delayed:** tray → Capture after delay → 5 seconds, then open a menu during the countdown. The countdown bubble is not in the capture. Once the overlay appears, check that Esc, F and the arrow keys work straight away. Another app was in front, so the overlay may not get keyboard focus; if it doesn't, note it here.
- [ ] **Repeat:** after a region capture, press `Ctrl+PrtSc`. The same region is saved straight away, with no overlay.
- [ ] **Saved files:**
  - `Pictures\Snare\2026\10\` has `yyyy-MM-dd_HHmmss.png`, with `_2` added for two captures in the same second.
  - `%LOCALAPPDATA%\Snare\originals` and `thumbs` each have a file per capture.
  - Opening the PNG in Explorer's Properties → Details shows no extra metadata.
- [ ] **Quick bar:** press `PrtSc` then `F` twice in a row: the first quick bar is not in the second capture. Copy, Show in folder (Explorer opens with the file selected), Library and Delete all work. The bar closes after about 6 s unless the mouse is over it.
- [ ] **Library:** tray → Library shows thumbnails, newest first, with dates and the source app.
  - Double-click opens the file. Right-click → Copy, Show in folder and Move to Trash work, and so do Del and Ctrl+C.
  - A new capture appears at the top while the window is open.
- [ ] **Mouse pointer setting:** set `"IncludeCursor": true` in `appsettings.json` next to the exe, restart, and capture. The pointer is in the image.
- [ ] **Mixed DPI (optional):** set the upper monitor to 125%, restart Snare, and repeat the region and window checks.
