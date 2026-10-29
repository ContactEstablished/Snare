# Snare build plan

Personal Snagit replacement for Windows. Status as of Oct 28, 2026.

## How to use this plan

This is the spec. The Grill Me pass on Oct 28, 2026 answered every **Open** item and confirmed every **Proposed** default; the answers are recorded in place and in [Decisions from the grill](#decisions-from-the-grill). Each phase is cut from this plan into a `spec.md` and `task.md` under `docs/phases/`.

- **Audience:** coding agents. One agent plays planner, implementer and reviewer; a separate code-review subagent reviews each phase before it merges.
- **Status markers:** **Decided** means Matt confirmed it. **Proposed** is a recommended default to challenge. **Open** needs Matt's answer before the phase that depends on it. A new question that comes up mid-phase is added as **Open** and put to Matt; it is never decided silently.
- **Scope of use:** one user, one Windows PC, no distribution. Prefer simple, robust choices over configurability.

## Matt's setup

Facts gathered from the machine on Oct 28, 2026. Design and manual checks target this setup.

| Item | Value |
| --- | --- |
| OS | Windows 11 Home, build 26300 |
| Primary monitor | Odyssey G95SC, 5120×1440, 100% scaling, at (0, 0) |
| Second monitor | GNV34DBE2, 3440×1440, 100% scaling, above the primary at (856, −1440) |
| HDR | Supported on both, off on both |
| GPU | NVIDIA GeForce RTX 4080 SUPER (NVENC) |
| Webcam | EMEET SmartCam C960 4K |
| Microphones | fifine Microphone, EMEET SmartCam |
| Browsers | Chrome, Edge |
| Snagit | Installed and running; owns `PrtSc` |
| Pictures folder | `C:\Users\matth\Pictures`, local (not redirected to OneDrive) |
| FFmpeg | 8.1.1 via winget, on PATH |

## Goals and scope

The goal is to replace Snagit for Matt's daily use on his own PC. Captures are saved as ordinary PNG, JPG and MP4 files, and found again by tag, description or the text on screen.

| Feature | Status | Phase | Notes |
| --- | --- | --- | --- |
| Region, window and full-screen capture | Decided | 1 | Freeze the screen, then select |
| Library of every capture | Decided | 1 | Auto-saved and browsable |
| Plain PNG/JPG files, no proprietary format | Decided | 1 | Editable layers live in the library database |
| Delayed capture (for menus and hovers) | Decided | 1 | Countdown before the freeze |
| Draw and highlight tools | Decided | 2 | Arrows, shapes, pen, highlighter, text, line, step numbers |
| Redaction and blur | Decided | 2 | Solid fill, blur, pixelate |
| Tags and descriptions, searchable | Decided | 3 | Full-text search |
| OCR text in search, plus Grab Text | Decided | 3 | Windows built-in OCR, English |
| Video with microphone and webcam | Decided | 4 | MP4, webcam as an overlay |
| System audio in video | Decided | 4 | Mixed with the mic |
| GIF export and video trim | Decided | 4 | Via FFmpeg |
| Scrolling capture | Decided | 5 | Manual first, then automatic |

**Non-goals:**

- Mac or Linux support, or Windows 10
- Cloud upload, share links or team features
- A proprietary project file like `.snagx`
- A full video editor (timeline, captions, transitions)
- Templates and Snagit's Simplify tool
- AI features in v1, including video transcripts
- Combining several captures into one image
- Named style presets

## Tech stack

The app is C# on .NET 10 with WPF, using Windows' own capture, OCR and media APIs. WinUI 3 was passed over because it handles transparent overlay windows poorly. Electron, Tauri and Avalonia were passed over because cross-platform support isn't needed and native interop would be constant.

| Area | Choice | Why | Status |
| --- | --- | --- | --- |
| Language and runtime | C# on .NET 10 (LTS) | Matt's strongest stack, long support window | Decided |
| Target OS | Windows 11 only, Per-Monitor V2 DPI awareness | Can hide the yellow capture border; no legacy code paths | Decided |
| UI shell | WPF + CommunityToolkit.Mvvm | Borderless, transparent, topmost overlays across monitors | Decided |
| Hosting | Microsoft.Extensions.Hosting | DI, config and logging Matt already knows | Decided |
| Editor canvas | SkiaSharp, hosted in WPF via SkiaSharp.Views.WPF | One renderer for on-screen preview and export | Decided |
| Still capture | GDI `BitBlt` per monitor, plus Windows.Graphics.Capture for single windows | Instant freeze-frame; windows captured even when partly covered | Decided |
| Win32 interop | CsWin32 source generator | Typed, generated P/Invoke | Decided |
| Video recording | [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib/wiki/Quickstart-guide-v5.x.x) | MP4 via Media Foundation; mic plus system audio; webcam overlay built in | Decided (pending Phase 0 spike) |
| Video tooling | FFmpeg (external exe) | Thumbnails, GIF export, trim | Decided |
| Scroll stitching | OpenCvSharp4 | Template matching to find frame overlap | Decided |
| OCR | Windows.Media.Ocr | Built in, offline, free | Decided |
| Data | SQLite via Microsoft.Data.Sqlite + EF Core; FTS5 through raw SQL | One file, fast full-text search | Decided |
| Tray icon | H.NotifyIcon.Wpf | WPF-native tray and context menu | Decided |
| Logging | Serilog, rolling file | Diagnose capture bugs after the fact | Decided |
| Tests | xUnit v3 | Unit, golden-image and fixture tests | Decided |
| Packaging | Self-contained `dotnet publish` into `%LOCALAPPDATA%\Programs\Snare` via `tools/publish.ps1`; no installer, no auto-update | Single user, no distribution | Decided |

Target framework: `net10.0-windows10.0.22621.0` for Windows-facing projects; Core and Rendering target plain `net10.0`. Package versions live in `Directory.Packages.props`.

**FFmpeg lookup order:** the path in settings, then `tools\ffmpeg\ffmpeg.exe` next to the app, then `PATH` (which finds Matt's winget copy).

## Architecture

One WPF app over six class libraries. Only the App project references WPF, and Core references nothing. Slow work (OCR, thumbnails, FFmpeg jobs) runs on a background queue built on `System.Threading.Channels`, so capture never waits on it.

| Project | Responsibility | References |
| --- | --- | --- |
| `Snare.App` | WPF shell: tray, hotkeys, capture overlay, editor and library windows, view models | All below |
| `Snare.Core` | Domain models (capture, tag, annotation objects), interfaces, no UI or I/O | None |
| `Snare.Interop` | CsWin32 bindings: hotkeys, monitors, windows, cursor, display affinity | None |
| `Snare.Capture` | Freeze-frame, window capture, scrolling stitcher | Core, Interop |
| `Snare.Rendering` | SkiaSharp renderer for annotations, effects and export | Core |
| `Snare.Recording` | ScreenRecorderLib wrapper, FFmpeg jobs | Core, Interop |
| `Snare.Library` | SQLite, file store, full-text search, OCR indexing, thumbnails | Core, Rendering |

Library references Rendering so it can flatten exports and build thumbnails; nothing else crosses sideways.

```
Snare/
  src/            the seven projects above
  tests/
    Snare.Core.Tests/
    Snare.Rendering.Tests/   golden-image tests
    Snare.Capture.Tests/     stitching fixtures
    Snare.Library.Tests/     SQLite + FTS tests
  tools/ffmpeg/   ffmpeg.exe, downloaded locally (not committed)
  tools/publish.ps1
  docs/           this plan; spec.md and task.md per phase
  Directory.Build.props      TFMs, nullable, warnings as errors
  Directory.Packages.props   central package versions
```

## Data and storage

Pixels live in plain files. Metadata, tags, OCR text and the editable annotation layers live in one SQLite database. The exported PNG or JPG is always flattened, so it is safe to share.

```
<LibraryRoot>\                      %USERPROFILE%\Pictures\Snare (Decided; local, not OneDrive)
  2026\10\2026-10-25_182301.png     exported, flattened image
  2026\10\2026-10-25_182301_2.png   second capture in the same second
  2026\10\2026-10-25_183010.mp4     recording
%LOCALAPPDATA%\Snare\
  library.db                        metadata, tags, search index, annotations
  originals\<captureId>.png         untouched capture pixels, for re-editing
  thumbs\<captureId>.webp
  logs\
```

| Table | Key columns |
| --- | --- |
| `Capture` | Id (`Guid.CreateVersion7`), Kind (Image, Video, Scrolling), FilePath, OriginalPath, Width, Height, DurationMs, CreatedUtc, ModifiedUtc, SourceApp, SourceWindowTitle, Title, Description, OcrText, AnnotationJson, IsDeleted |
| `Tag` | Id, Name (unique, case-insensitive), Color |
| `CaptureTag` | CaptureId, TagId |
| `CaptureSearch` | FTS5 virtual table over Title, Description, tag names, OcrText, SourceApp, SourceWindowTitle; updated by the app on every save |

Annotations are a versioned JSON document of vector objects drawn over the original (see `AnnotationDocument` and `AnnotationJson` in `Snare.Core`):

```json
{
  "schemaVersion": 1,
  "canvas": { "width": 1920, "height": 1080, "crop": null },
  "objects": [
    { "type": "arrow", "id": "a1", "from": { "x": 100, "y": 200 }, "to": { "x": 400, "y": 260 }, "stroke": "#E53935", "strokeWidth": 6 },
    { "type": "redact", "id": "r1", "bounds": { "x": 600, "y": 120, "width": 280, "height": 40 }, "mode": "solid", "fill": "#000000" }
  ]
}
```

**Rules**

- Every capture is auto-saved; there is no "keep?" prompt. Delete to Trash is the undo.
- Save order is original, then database row, then export, so a crash never loses pixels.
- Export file names are `yyyy\MM\yyyy-MM-dd_HHmmss.<ext>` under the library root, with `_2`, `_3` and so on for captures in the same second. Titles stay in the database, never in file names.
- Re-editing renders the original plus annotations again and overwrites the export. No old versions are kept; the original is enough.
- Export strips metadata chunks. JPG quality defaults to 90.
- Delete is soft, with a Trash view. Emptying Trash removes the files.
- Originals stay as plain PNGs in `%LOCALAPPDATA%`; no DPAPI. Each capture has a **Discard original** action that makes the export permanent and ends re-editing. The editor suggests it when a capture has a Redact object.
- Files added, moved or deleted outside the app are reconciled on startup (Phase 3).

## Image capture

Every still capture freezes all monitors first, then Matt selects on the frozen image. That is how open menus, tooltips and hover states survive the capture.

1. The hotkey fires (or the tray menu, or a delay countdown ends).
2. Every monitor is grabbed at physical resolution with `BitBlt` before any UI appears. The window and process under the cursor are recorded.
3. One borderless, topmost overlay per monitor shows the frozen image, dimmed outside the selection.
4. Matt drags a region, clicks a highlighted window, or presses a key for full screen.
5. The result is cropped from the frozen bitmap, never re-grabbed.
6. The capture is saved to the library and copied to the clipboard. A quick bar then appears near the selection (Decided): Edit, Copy, Reveal, Delete, plus a tag box from Phase 3. It dismisses itself after a few seconds. A setting opens the editor directly instead.

| Mode | How it's selected | Notes |
| --- | --- | --- |
| Region | Drag | Handles to adjust before accepting; arrow keys nudge 1 px, Shift+arrow 10 px (`SelectionNudge`) |
| Window | Hover highlights the window, click to take it | Bounds from `DWMWA_EXTENDED_FRAME_BOUNDS` (`WindowBounds`) so invisible borders are excluded. A plain rectangle cropped from the frozen frame; no rounded-corner transparency |
| Full screen | Key in the overlay | Monitor under the cursor; another key for all monitors |
| Repeat last region | `Ctrl+PrtSc` | Decided; key chosen in Phase 1, configurable in Phase 6 |
| Delayed | 3, 5 or 10 second countdown | Decided; catches menus that close on keypress |

**Overlay behavior**

- A crosshair with a magnifier loupe shows pixel coordinates, selection size and the color under the cursor.
- Esc cancels; Enter or double-click accepts.
- Regions may span monitors. The per-monitor grabs are combined into one virtual-desktop bitmap. Matt's virtual desktop has negative coordinates (the second monitor sits above the primary), and parts of its bounding box belong to no monitor; those areas are filled black and can't be selected.

**Hotkeys (Decided)**

- `PrtSc` takes an image, `Shift+PrtSc` starts video, `Ctrl+Shift+PrtSc` starts a scrolling capture, `Ctrl+PrtSc` repeats the last region.
- Snagit currently owns `PrtSc`. Matt turns off Snagit's hotkey, or quits Snagit, while using Snare.
- If registering a hotkey fails, the tray tooltip and log say so, naming the likely owner (Snagit or Snipping Tool's Windows setting). Hotkeys become configurable in Phase 6.

**Monitors and DPI**

- The app manifest declares Per-Monitor V2 DPI awareness.
- All capture math stays in physical pixels; DIPs are used only to draw overlay chrome.
- HDR is assumed off (Decided). If HDR is on for a monitor, Snare logs a warning; Windows.Graphics.Capture with tone mapping is a later fallback.

**Other details**

- The mouse cursor is left out by default; a setting includes it.
- Source process name and window title are saved for search. Reading a browser's current URL through UI Automation is a later nice-to-have.

## Editor

One SkiaSharp renderer draws the annotation model both on screen and on export, so the PNG matches exactly what Matt saw. WPF supplies only the toolbars and property panels. This single-renderer choice is Decided; the Phase 0 spike proves it on a 5120×2880 frame (both monitors).

| Tool | Behavior | Priority | Phase |
| --- | --- | --- | --- |
| Select | Move, resize, multi-select, z-order, delete | Must | 2 |
| Arrow | Straight, with head size and style; curved later | Must | 2 |
| Rectangle and ellipse | Outline or fill, corner radius | Must | 2 |
| Highlighter | Translucent multiply blend so text stays readable | Must | 2 |
| Pen | Freehand with smoothing | Must | 2 |
| Text | Font, size, color, optional background | Must | 2 |
| Redact | Solid fill box | Must | 2 |
| Blur | Gaussian, adjustable radius | Must | 2 |
| Pixelate | Mosaic, adjustable block size | Must | 2 |
| Crop and resize | Whole-image operations | Must | 2 |
| Line | Plain line with optional dash | Should | 2 |
| Step numbers | Auto-incrementing badges that renumber on delete | Should | 2 |
| Eyedropper and recent colors | Pick from the image, remember the last few | Should | 2 |
| Callout | Text bubble with a movable tail | Should | 6 |
| Magnify and spotlight | Zoom bubble; dim everything else | Could | After v1 |
| Border, shadow, torn edge | Image-frame effects | Could | After v1 |

**Editing model**

- Every change is a command object, giving unlimited undo and redo within a session.
- Edits autosave to `AnnotationJson` after a short pause.
- Each tool gets a single-letter shortcut and remembers its last style. There are no named presets.
- `Ctrl+wheel` zooms and `Space+drag` pans. Very tall scrolling captures render in tiles.

**Redaction rules**

- Redact is the only tool described as secure. Blur and pixelate are labeled as obscuring, not protecting, because blurred text can sometimes be recovered.
- Export always flattens. No layer data ever goes into the exported file.

**Output**

- Save overwrites the library export. Save As writes a PNG or JPG copy elsewhere.
- Copy puts the flattened image on the clipboard; the image can also be dragged straight into other apps.
- PNG is the default format.

## Library and search

The library window shows every capture as a thumbnail grid. One search box matches titles, descriptions, tags, source app and the text OCR found in the image.

**Library window**

- Thumbnails, newest first, with filters for kind, date range, tag and source app.
- A detail pane shows title, description, tag chips with autocomplete, source app and window, dimensions, date, and the OCR text (collapsed).
- Actions: open in editor, copy, reveal in Explorer, drag out to other apps, delete to Trash. Multi-select allows bulk tagging and deleting.
- The post-capture quick bar has a tag box, so tags go on while the context is fresh.

**Tags**

- Flat, with no hierarchy.
- Each new tag gets a color from a fixed palette; Matt can change it in the detail pane.

**Search**

- SQLite FTS5 with prefix matching and BM25 ranking, so `invoice err` finds "invoices" and "error".
- Filter tokens: `tag:bug`, `app:chrome`, `kind:video`, `before:2026-10-01`, `after:2026-09-01`.
- Results refresh as Matt types, after a short pause.

**OCR**

- Windows.Media.Ocr runs in the background after each image save. Its text goes into `OcrText` and the search index.
- The OCR engine has a maximum image size, so tall scrolling captures are processed in tiles and the text joined.
- Grab Text: select a region, OCR it, and put the text on the clipboard without saving a capture.
- English only, using the Windows profile language.

**Videos in the library**

- Searchable by title, description, tags and source app.
- Thumbnails come from an FFmpeg frame grab about 1 second in.
- Speech-to-text transcripts are out of v1.

## Video recording

Recordings are MP4 files (H.264 video, AAC audio) made with ScreenRecorderLib. They include the microphone, optional system audio, and the webcam baked in as an overlay.

**Before recording**

- Pick a region, window or monitor with the same overlay used for stills.
- Choose the mic, toggle system audio, and choose the webcam. A live mic level meter is a Should.
- Preview the webcam and set its corner and size. The default is a rectangle in the bottom-right corner, about 20% of the recording height. A circle shape is after v1.
- A 3-second countdown starts the recording.

**While recording**

- A floating control bar offers pause/resume, stop, restart, discard, elapsed time, webcam on/off and mic mute.
- The control bar and other app windows are kept out of the video with `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` (`CaptureExclusion`).
- The start hotkey also stops the recording.
- Mouse clicks are highlighted, using ScreenRecorderLib's built-in option (v1).

**Output (Decided)**

- 30 fps by default, 60 as an option. Typical recordings are under 30 minutes.
- Hardware H.264 through NVENC, quality-based bitrate, at native resolution.
- NVENC H.264 is limited to 4096 px wide. Recording areas wider than that are scaled down to 4096 wide (a full 5120×1440 monitor becomes 4096×1152). H.264 is kept everywhere for compatibility.
- Written straight into the library folder.

**After recording**

- The video is saved to the library with a thumbnail and opens in a simple player with title, description and tags.
- Trimming the start and end (FFmpeg stream copy) and GIF export (FFmpeg with a palette pass) are in v1.
- A separate webcam file, composited later so the webcam can be moved or hidden after recording, is after v1.

**Spike before Phase 4:** confirm ScreenRecorderLib can toggle the webcam overlay mid-recording, mix mic and system audio at set levels, and pause cleanly. It is a native C++/CLI library (built for .NET Framework 4.8, loading fine on .NET 10) that needs the Visual C++ x64 runtime, so the whole solution builds x64 only (Decided in Phase 0). The fallback is FFmpeg capture or Media Foundation directly. A failed spike is a stack change and goes back to Matt.

## Scrolling capture

Scrolling capture ships in two stages. Manual comes first because it works in any app. Automatic scrolling follows for apps that expose a scroll control.

**Scope (Decided):** automatic scrolling targets Chrome and Edge; every other app, including VS Code, uses manual mode. Vertical only. Captures are capped at 30,000 px tall.

**Stage 1: manual**

1. Matt selects the region to capture and presses start.
2. He scrolls at his own pace.
3. The app grabs the region about 10 times a second, keeps only frames that moved, and stitches each onto the result.
4. A live preview strip shows the image growing. Stop ends it and saves to the library.

**Stage 2: automatic**

1. Matt hovers the scrollable area. The app finds it through UI Automation's scroll pattern.
2. The app scrolls one step, using the scroll pattern or a simulated mouse wheel as fallback.
3. It waits until two grabs in a row match (rendering has settled), then grabs and stitches.
4. It stops when a scroll adds nothing new or the height limit is reached.

**Stitching**

- Frames are compared in grayscale.
- The vertical offset comes from matching a strip at the bottom of the previous frame inside the new frame, using OpenCvSharp template matching.
- Matches below a confidence threshold are dropped, with a warning in the preview.
- Rows that stay identical at the same screen position across frames are treated as sticky headers or footers. They are left out of matching and kept only once.

**Known trouble spots:** lazy-loaded images, animations or video on the page, smooth-scroll inertia, and sticky elements that change while scrolling. Manual mode lets Matt slow down and retry.

**Testing:** recorded frame sequences with known offsets live in `Snare.Capture.Tests` as fixtures, so the stitcher is tested without a screen.

## Roadmap

Seven phases, each usable on its own. Phases run back to back without stopping for review (Decided). Checks that need Matt's eyes or hands are built as runnable harnesses and listed under **Awaiting Matt** in the phase's `task.md`. A phase whose code is merged but still has unticked checks is *code-complete*; the next phase starts anyway. Work stops for Matt only when a spike forces a stack change or a new decision comes up.

**Phase 0: Foundation and spikes** (code-complete Oct 28, 2026; manual checks in [task.md](phases/0-foundation/task.md))

- [x] Solution scaffolded as in Architecture; builds clean with warnings as errors
- [x] Generic Host with DI, Serilog and a settings file
- [ ] Tray icon with Capture, Library and Exit (built; needs a manual check on Windows)
- [ ] Spike: `PrtSc` hotkey registers, or the app names what holds it
- [ ] Spike: frozen overlay lines up on both monitors, including the one at negative coordinates
- [ ] Spike: ScreenRecorderLib records with mic, system audio and webcam overlay
- [ ] Spike: SkiaSharp canvas in WPF draws and drags an arrow smoothly on a 5120×2880 capture

**Phase 1: Capture and library** (code-complete Oct 29, 2026; manual checks in [task.md](phases/1-capture/task.md))

- [ ] Region, window and full-screen capture from the hotkey
- [ ] Delayed capture and repeat last region
- [ ] Each capture saves its original, a PNG export and a database row
- [ ] Each capture is copied to the clipboard and shows the quick bar
- [ ] Library grid shows thumbnails, newest first
- [ ] `tools/publish.ps1` publishes a self-contained build

**Phase 2: Editor** (code-complete Oct 29, 2026; manual checks in [task.md](phases/2-editor/task.md))

- [ ] Every Must tool, plus Line, Step numbers and Eyedropper, works on new and reopened captures
- [ ] Undo and redo work for every tool
- [ ] Reopening a capture, moving an annotation and re-exporting works
- [ ] Golden-image tests pass for every tool
- [ ] A test proves redacted areas of the export contain no original pixels
- [ ] Discard original works and ends re-editing

**Phase 3: Organize and search**

- [ ] Tags and descriptions editable in the detail pane and quick bar
- [ ] OCR runs in the background and its text is searchable
- [ ] Search with filter tokens updates as Matt types
- [ ] Grab Text copies OCR text from a region

**Phase 4: Video**

- [ ] Record a region, window or monitor with mic, system audio and webcam
- [ ] The control bar never appears in the video
- [ ] Pause, resume, and stop by hotkey work
- [ ] Recordings land in the library with a thumbnail and play back
- [ ] Trim, GIF export and click highlighting work

**Phase 5: Scrolling capture**

- [ ] Manual mode stitches a long web page with a sticky header correctly
- [ ] Stitcher fixture tests pass
- [ ] Automatic mode works in Chrome and Edge, after a UI Automation spike

**Phase 6: Polish**

- [ ] Settings window for hotkeys, library folder and defaults
- [ ] Start with Windows
- [ ] One-click backup of the library database
- [ ] Callout tool

*Ready to retire Snagit once every Awaiting Matt check is ticked.*

## Risks and spikes

The biggest risks are multi-monitor coordinate bugs and scroll stitching. Both are front-loaded: monitor layout in the Phase 0 spike, stitching through manual mode and fixture tests.

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Negative virtual-desktop coordinates or mixed DPI offset the selection | Wrong pixels captured | Per-Monitor V2, physical pixels end to end, Phase 0 spike on Matt's stacked layout |
| Snagit owns `PrtSc` | Hotkey silently fails | Detect the failed registration and name the likely owner; Matt turns off Snagit's hotkey |
| Preview and export render differently | Exports don't match the editor | One SkiaSharp renderer plus golden-image tests |
| ScreenRecorderLib can't toggle webcam or pause cleanly | Video features blocked | Phase 0 spike; fall back to FFmpeg or Media Foundation |
| 5120 px wide recordings exceed NVENC H.264 | Recording fails or falls back to slow software | Scale to 4096 wide |
| Stitching errors on sticky or lazy content | Broken scrolling captures | Manual mode first, live preview, fixture tests |
| Very tall images exhaust memory | Editor crashes | Tiled rendering, 30,000 px cap |
| HDR displays wash out GDI grabs | Dull captures | HDR is off on Matt's monitors; log a warning if it's turned on |
| Unredacted originals stay on disk | Sensitive data kept | Discard original action, suggested after redaction |

## Conventions and definition of done

A task is done when it builds clean, its tests pass, its manual check is ticked in `task.md`, and this plan reflects any decision it changed. The day-to-day rules for agents are in [AGENTS.md](../AGENTS.md).

**Code**

- Nullable reference types on; warnings as errors; central package versions.
- MVVM with CommunityToolkit.Mvvm source generators. Code-behind holds only view wiring.
- Async all the way for I/O. Capture and rendering stay off the UI thread where possible.
- Interfaces live in Core; Win32 calls live only in Interop.

**Tests**

- xUnit v3 across Core, Rendering, Capture and Library.
- Rendering: golden-image tests render known annotation JSON and compare against checked-in PNGs with a small pixel tolerance.
- Capture: stitcher fixtures from recorded frame sequences.
- Library: SQLite and FTS5 tests against a temporary database file.
- Multi-monitor and DPI behavior is checked by hand against a written checklist in each phase's `task.md`.

**Reliability**

- Serilog writes a rolling log in `%LOCALAPPDATA%\Snare\logs`.
- A global exception handler logs and keeps the tray app alive.
- The original pixels are written first, so a crash never loses a capture.

**Workflow**

- One phase per branch (`phase/<n>-<name>`) and worktree.
- Each phase gets its own `spec.md` and `task.md` under `docs/phases/`, cut from this plan.
- A code-review subagent reviews the phase diff before it merges. The same review served as the council review of the stack calls.
- When the phase is code-complete and review is clean, it merges into `main` with `--no-ff`. `main` and the phase branch are pushed to `origin` so CI runs; no pull requests.

## Decisions from the grill

Every question below was answered by Matt on Oct 28, 2026. The answers are recorded in place above.

**Before Phase 0**

- [x] What is the app called? **Snare** (repo `ContactEstablished/Snare`, public)
- [x] Is Windows 11 only acceptable? **Yes.**
- [x] How many monitors, at what resolutions and scaling? Any HDR? **Two, both at 100% scaling (see Matt's setup). HDR is assumed off.**
- [x] Is it OK to turn off Windows' `PrtSc` to Snipping Tool setting? Which hotkeys should we use? **The Windows setting is already off; Snagit holds `PrtSc` and Matt turns its hotkey off. Hotkeys as proposed.**
- [x] Does the single SkiaSharp renderer survive council review, or should the editor use WPF drawing? **Single Skia renderer; spike on 5120×2880.**
- [x] Run from a build folder, or install with auto-update? **Self-contained publish into a fixed folder; no auto-update.**
- [x] Do the remaining Proposed items stand? **Yes; all are Decided.**

**Before Phase 1**

- [x] Where does the library live, and is that folder synced to OneDrive? **`%USERPROFILE%\Pictures\Snare`; local, not synced.**
- [x] What file naming pattern should exports use? **`yyyy\MM\yyyy-MM-dd_HHmmss` with `_2` suffixes.**
- [x] After a capture: open the editor, show a quick bar, or just copy to the clipboard? **Copy and show the quick bar; a setting opens the editor instead.**
- [x] Is every capture auto-saved, or only ones Matt keeps? **Every capture.**
- [x] Include the mouse cursor by default? **No.**
- [x] Window captures: rounded corners with transparency, or a plain rectangle? **Plain rectangle.**

**Before Phase 2**

- [x] Which Should and Could tools move into Phase 2? **Line, Step numbers, Eyedropper and recent colors. Callout in Phase 6; the rest after v1.**
- [x] Re-editing: overwrite the export, or keep the old one as a version? **Overwrite.**
- [x] Keep unredacted originals, encrypt them, or discard them after redaction? **Keep them, with a Discard original action.**
- [x] Are saved style presets per tool needed? **No.**
- [x] Is combining several captures into one image needed? **No.**

**Before Phase 3**

- [x] Should tags have colors? Any hierarchy? **Auto-assigned, editable colors; flat.**
- [x] Is OCR needed in any language besides English? **No.**
- [x] Will video transcripts for search ever matter? **Not in v1.**

**Before Phase 4**

- [x] Typical recording length, resolution and frame rate? **Under 30 minutes, native resolution capped at 4096 wide, 30 fps (60 optional), NVENC H.264.**
- [x] Is a baked-in webcam OK for v1? Which shape and position? **Yes; rectangle, bottom-right, about 20% of the height.**
- [x] Is system audio needed, or mic only? **System audio, mixed with the mic.**
- [x] Are trim, GIF export and click highlighting in v1? **Yes, all three.**

**Before Phase 5**

- [x] Which apps matter most for scrolling capture? **Chrome and Edge automatic; everything else manual.**
- [x] Is horizontal scrolling needed? **No.**
- [x] What is the longest capture Matt expects to take? **Capped at 30,000 px.**

**Process**

- [x] Which phases can run without stopping for review? **All of them; manual checks batch up under Awaiting Matt.**
- [x] Which models handle which roles? **One agent plans and implements; a code-review subagent reviews each phase.**
