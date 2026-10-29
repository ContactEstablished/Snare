# Phase 1: Capture and library

Cut from [the build plan](../../build-plan.md): Image capture, Data and storage, Library window and Roadmap → Phase 1. It builds on the Phase 0 freeze-frame, hotkeys and tray.

## Goal

`PrtSc` freezes every monitor. Matt selects a region, a window or a whole monitor on the frozen image. The capture is saved to the library as an original, a database row and a flattened PNG export, in that order. It is copied to the clipboard, and a quick bar appears. The tray's Library entry opens a thumbnail grid of every capture, newest first.

## Capture flow

1. A trigger fires: `PrtSc`, tray → Capture, tray → Capture in 3/5/10 s, or `Ctrl+PrtSc` to repeat the last region.
2. Before any UI appears, Snare records a snapshot:
   - `WindowSnapshot` (Interop) lists every visible, uncloaked, non-minimized top-level window in z-order, with DWM extended frame bounds, title, process name and handle. Snare's own windows are left out.
   - The window under the cursor is recorded as the capture's source app and window title.
3. `GdiScreenFreezer` grabs every monitor. If `IncludeCursor` is on, the cursor is drawn into the grab.
4. One `CaptureOverlayWindow` per monitor shows the frozen frame, dimmed outside the selection. The windows are placed in physical pixels and kept there on DPI changes. All overlays share one `CaptureOverlayViewModel`, which wraps a `SelectionSession` (Capture) that holds all selection logic in physical pixels.
5. Selection:
   - With no selection, hovering highlights the window under the cursor. A click (under 4 px of movement) takes that window.
   - A drag draws a region. After release, the region shows 8 resize handles. Dragging inside moves it; dragging a handle resizes it.
   - Arrow keys nudge the region 1 px, Shift+arrow 10 px (`SelectionNudge`).
   - `F` takes the monitor under the cursor. `A` takes all monitors (the virtual-desktop bounds).
   - Enter or a double-click inside the region accepts it. Esc cancels.
   - The chrome shows a crosshair, a loupe (8× zoom of 15×15 px), the physical cursor position, the selection size, and the hex color under the cursor.
6. The result is cropped from the frozen frames (`DesktopCropper`), never re-grabbed. Areas off every monitor are black.
7. `CaptureService` (App) hands the crop to `ICaptureStore.SaveImageAsync`. In order, the store writes:
   1. the original PNG to `originals\<id>.png`, before it touches the database at all;
   2. the `Capture` row;
   3. the thumbnail at `thumbs\<id>.webp`;
   4. the flattened export at `<LibraryRoot>\yyyy\MM\yyyy-MM-dd_HHmmss[_n].png`, using the next free suffix.

   It returns the record. Saves are serialized, so two captures in the same second can't take the same name. Every file is written to a uniquely named temp file and then moved into place. If the export fails (Pictures blocked, disk full), the original, row and thumbnail are kept and the error is reported; Phase 3's startup reconciliation re-exports.
8. The image goes on the clipboard, as both a bitmap and `PNG` data, even if the save failed. In that case, a tray notification says so. Any previous quick bar is closed before each freeze and excluded from capture, so it never appears in a screenshot.
9. `QuickBarWindow` appears near the selection, on its monitor. It offers Copy, Reveal in Explorer, Open library and Delete (to Trash). It closes itself after 6 s unless the mouse is over it. Phase 2 adds Edit; Phase 3 adds the tag box.

**Delayed capture:** a small topmost countdown (3, 2, 1) near the cursor, excluded from capture (`CaptureExclusion`) and closed before the freeze.

**Repeat last region:** `Ctrl+PrtSc` freezes, crops the previous region (clipped to the current desktop), and saves with no overlay. With no previous region in this session, it acts like `PrtSc`.

## Library

- **Schema:** SQLite through Microsoft.Data.Sqlite + EF Core. The schema comes from ordered SQL scripts tracked by `PRAGMA user_version` (`SchemaMigrator`), not EF migrations, so Phase 3's FTS5 virtual table can live in the same scripts. Phase 1 creates `Capture`, `Tag` and `CaptureTag`, as in the build plan.
- **`SqliteCaptureStore`** (Library) implements `ICaptureStore`: `SaveImageAsync`, `ListAsync` (not deleted, newest first), `GetAsync`, `MoveToTrashAsync` (soft delete) and `ThumbnailPath`.
- **Encoding:** SkiaSharp in Rendering (`ImageCodec`). PNG and JPG exports carry no metadata, and JPG quality comes from settings (default 90). Thumbnails are WebP, at most 320×240.
- **Library window** (App, MVVM):
  - a `LibraryViewModel` with `CaptureTileViewModel`s, shown as a wrapping thumbnail grid, newest first;
  - double-click opens the export in the default app;
  - a context menu offers Copy, Reveal in Explorer and Move to Trash;
  - the grid refreshes when a capture is saved or deleted (`WeakReferenceMessenger`).
  - Thumbnails are decoded with SkiaSharp, not WIC, so WebP support in Windows doesn't matter.

## Other items in this phase

- **Hotkeys:** `Ctrl+PrtSc` is added for repeat last region. It is configurable in Phase 6.
- **Tray:** Capture, Capture in 3 s / 5 s / 10 s, Library, Spikes, Exit. The Phase 0 spikes stay until their manual checks are ticked; the freeze-overlay spike gives way to the real capture on `PrtSc`.
- **HDR:** at startup, `AdvancedColor` (Interop) checks every display. If any has HDR on, Snare logs a warning.
- **`tools/publish.ps1`:** stops a running Snare, then publishes a self-contained win-x64 Release build to `%LOCALAPPDATA%\Programs\Snare`.

## Out of scope

The editor (Phase 2). Tags, descriptions, search and OCR (Phase 3). Startup reconciliation of files changed outside the app (Phase 3). Trash view (Phase 3).

## Done when

Builds clean, all tests pass, CI is green, and the Awaiting Matt checks in `task.md` are listed.
