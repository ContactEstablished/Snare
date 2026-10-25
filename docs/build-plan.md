# Snare build plan

Personal Snagit replacement for Windows. Status as of Oct 25, 2026.

## How to use this plan

This is the starting spec. The Grill Me role should challenge it until every **Open** item is answered. Chorus then splits it into a `spec.md` and `task.md` per phase under `docs/phases/`.

- **Audience:** Chorus agents. The Grill Me role goes first, then planners, implementers and reviewers.
- **Status markers:** **Decided** means Matt confirmed it. **Proposed** is a recommended default to challenge. **Open** needs Matt's answer before the phase that depends on it.
- **Scope of use:** one user, one Windows PC, no distribution. Prefer simple, robust choices over configurability.
- **Architectural calls:** run Proposed items in the Stack and Architecture sections through a council review before Phase 1 code.

## Goals and scope

The goal is to replace Snagit for Matt's daily use on his own PC. Captures are saved as ordinary PNG, JPG and MP4 files, and found again by tag, description or the text on screen.

| Feature | Status | Phase | Notes |
| --- | --- | --- | --- |
| Region, window and full-screen capture | Decided | 1 | Freeze the screen, then select |
| Library of every capture | Decided | 1 | Auto-saved and browsable |
| Plain PNG/JPG files, no proprietary format | Decided | 1 | Editable layers live in the library database |
| Delayed capture (for menus and hovers) | Proposed | 1 | Countdown before the freeze |
| Draw and highlight tools | Decided | 2 | Arrows, shapes, pen, highlighter, text |
| Redaction and blur | Decided | 2 | Solid fill, blur, pixelate |
| Tags and descriptions, searchable | Decided | 3 | Full-text search |
| OCR text in search, plus Grab Text | Proposed | 3 | Windows built-in OCR |
| Video with microphone and webcam | Decided | 4 | MP4, webcam as an overlay |
| System audio in video | Proposed | 4 | Mixed with the mic |
| GIF export and video trim | Open | 4 | Via FFmpeg |
| Scrolling capture | Decided | 5 | Manual first, then automatic |

**Non-goals:**

- Mac or Linux support
- Cloud upload, share links or team features
- A proprietary project file like `.snagx`
- A full video editor (timeline, captions, transitions)
- Templates and Snagit's Simplify tool
- AI features in v1

## Tech stack

The app is C# on .NET 10 with WPF, using Windows' own capture, OCR and media APIs. WinUI 3 was passed over because it handles transparent overlay windows poorly. Electron, Tauri and Avalonia were passed over because cross-platform support isn't needed and native interop would be constant.

| Area | Choice | Why | Status |
| --- | --- | --- | --- |
| Language and runtime | C# on .NET 10 (LTS) | Matt's strongest stack, long support window | Proposed |
| Target OS | Windows 11 only, Per-Monitor V2 DPI awareness | Can hide the yellow capture border; no legacy code paths | Proposed |
| UI shell | WPF + CommunityToolkit.Mvvm | Borderless, transparent, topmost overlays across monitors | Proposed |
| Hosting | Microsoft.Extensions.Hosting | DI, config and logging Matt already knows | Proposed |
| Editor canvas | SkiaSharp, hosted in WPF via SkiaSharp.Views.WPF | One renderer for on-screen preview and export | Proposed |
| Still capture | GDI `BitBlt` per monitor, plus Windows.Graphics.Capture for single windows | Instant freeze-frame; windows captured even when partly covered | Proposed |
| Win32 interop | CsWin32 source generator | Typed, generated P/Invoke | Proposed |
| Video recording | [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib/wiki/Quickstart-guide-v5.x.x) | MP4 via Media Foundation; mic plus system audio; webcam overlay built in | Proposed |
| Video tooling | FFmpeg (bundled exe) | Thumbnails, GIF export, trim | Proposed |
| Scroll stitching | OpenCvSharp4 | Template matching to find frame overlap | Proposed |
| OCR | Windows.Media.Ocr | Built in, offline, free | Proposed |
| Data | SQLite via Microsoft.Data.Sqlite + EF Core; FTS5 through raw SQL | One file, fast full-text search | Proposed |
| Tray icon | H.NotifyIcon.Wpf | WPF-native tray and context menu | Proposed |
| Logging | Serilog, rolling file | Diagnose capture bugs after the fact | Proposed |
| Tests | xUnit v3 | Unit, golden-image and fixture tests | Proposed |
| Packaging | Self-contained publish; Velopack only if updates get tedious | Single user, no distribution | Open |

Target framework: `net10.0-windows10.0.22621.0` for Windows-facing projects; Core and Rendering target plain `net10.0`. Package versions live in `Directory.Packages.props`.

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
  docs/           this plan; spec.md and task.md per phase
  Directory.Build.props      TFMs, nullable, warnings as errors
  Directory.Packages.props   central package versions
```

## Data and storage

Pixels live in plain files. Metadata, tags, OCR text and the editable annotation layers live in one SQLite database. The exported PNG or JPG is always flattened, so it is safe to share.

```
<LibraryRoot>\                      default %USERPROFILE%\Pictures\Snare (Open)
  2026\10\2026-10-25_182301.png     exported, flattened image
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

- Save order is original, then database row, then export, so a crash never loses pixels.
- Re-editing renders the original plus annotations again and overwrites the export. Whether to keep the old export as a version is Open.
- Export strips metadata chunks. JPG quality defaults to 90 (Proposed).
- Delete is soft, with a Trash view. Emptying Trash removes the files.
- Originals of redacted captures still hold the sensitive pixels. Encrypting originals with DPAPI, or discarding them on request, is Open.
- Files added, moved or deleted outside the app are reconciled on startup (Proposed).

## Image capture

Every still capture freezes all monitors first, then Matt selects on the frozen image. That is how open menus, tooltips and hover states survive the capture.

1. The hotkey fires (or the tray menu, or a delay countdown ends).
2. Every monitor is grabbed at physical resolution with `BitBlt` before any UI appears. The window and process under the cursor are recorded.
3. One borderless, topmost overlay per monitor shows the frozen image, dimmed outside the selection.
4. Matt drags a region, clicks a highlighted window, or presses a key for full screen.
5. The result is cropped from the frozen bitmap, never re-grabbed.
6. The capture is saved to the library and copied to the clipboard. The editor or a quick action bar opens next (default is Open).

| Mode | How it's selected | Notes |
| --- | --- | --- |
| Region | Drag | Handles to adjust before accepting; arrow keys nudge 1 px, Shift+arrow 10 px (`SelectionNudge`) |
| Window | Hover highlights the window, click to take it | Bounds from `DWMWA_EXTENDED_FRAME_BOUNDS` (`WindowBounds`) so invisible borders are excluded |
| Full screen | Key in the overlay | Monitor under the cursor; another key for all monitors |
| Repeat last region | Its own hotkey | Proposed |
| Delayed | 3, 5 or 10 second countdown | Proposed; catches menus that close on keypress |

**Overlay behavior**

- A crosshair with a magnifier loupe shows pixel coordinates, selection size and the color under the cursor.
- Esc cancels; Enter or double-click accepts.
- Regions may span monitors. The per-monitor grabs are combined into one virtual-desktop bitmap.

**Hotkeys (Proposed)**

- `PrtSc` takes an image, `Shift+PrtSc` starts video, `Ctrl+Shift+PrtSc` starts a scrolling capture.
- Windows 11 gives `PrtSc` to Snipping Tool by default. If registering the hotkey fails, the app names the Windows setting to turn off.

**Monitors and DPI**

- The app manifest declares Per-Monitor V2 DPI awareness.
- All capture math stays in physical pixels; DIPs are used only to draw overlay chrome.
- HDR monitors can make GDI grabs look washed out. Whether Matt uses HDR is Open.

**Other details**

- Including the mouse cursor is a setting, off by default (Proposed).
- Source process name and window title are saved for search. Reading a browser's current URL through UI Automation is a later nice-to-have.

## Editor

One SkiaSharp renderer draws the annotation model both on screen and on export, so the PNG matches exactly what Matt saw. WPF supplies only the toolbars and property panels. This single-renderer choice is Proposed and the main architectural call to challenge.

| Tool | Behavior | Priority |
| --- | --- | --- |
| Select | Move, resize, multi-select, z-order, delete | Must |
| Arrow | Straight, with head size and style; curved later | Must |
| Rectangle and ellipse | Outline or fill, corner radius | Must |
| Highlighter | Translucent multiply blend so text stays readable | Must |
| Pen | Freehand with smoothing | Must |
| Text | Font, size, color, optional background | Must |
| Redact | Solid fill box | Must |
| Blur | Gaussian, adjustable radius | Must |
| Pixelate | Mosaic, adjustable block size | Must |
| Crop and resize | Whole-image operations | Must |
| Line | Plain line with optional dash | Should |
| Callout | Text bubble with a movable tail | Should |
| Step numbers | Auto-incrementing badges that renumber on delete | Should |
| Eyedropper and recent colors | Pick from the image, remember the last few | Should |
| Magnify and spotlight | Zoom bubble; dim everything else | Could |
| Border, shadow, torn edge | Image-frame effects | Could |

**Editing model**

- Every change is a command object, giving unlimited undo and redo within a session.
- Edits autosave to `AnnotationJson` after a short pause (Proposed).
- Each tool gets a single-letter shortcut and remembers its last style (Proposed).
- `Ctrl+wheel` zooms and `Space+drag` pans. Very tall scrolling captures render in tiles.

**Redaction rules**

- Redact is the only tool described as secure. Blur and pixelate are labeled as obscuring, not protecting, because blurred text can sometimes be recovered.
- Export always flattens. No layer data ever goes into the exported file.

**Output**

- Save overwrites the library export. Save As writes a PNG or JPG copy elsewhere.
- Copy puts the flattened image on the clipboard; the image can also be dragged straight into other apps.
- PNG is the default format (Proposed).

## Library and search

The library window shows every capture as a thumbnail grid. One search box matches titles, descriptions, tags, source app and the text OCR found in the image.

**Library window**

- Thumbnails, newest first, with filters for kind, date range, tag and source app.
- A detail pane shows title, description, tag chips with autocomplete, source app and window, dimensions, date, and the OCR text (collapsed).
- Actions: open in editor, copy, reveal in Explorer, drag out to other apps, delete to Trash. Multi-select allows bulk tagging and deleting.
- The post-capture quick bar has a tag box, so tags go on while the context is fresh (Proposed).

**Search**

- SQLite FTS5 with prefix matching and BM25 ranking, so `invoice err` finds "invoices" and "error".
- Filter tokens: `tag:bug`, `app:chrome`, `kind:video`, `before:2026-10-01` (Proposed).
- Results refresh as Matt types, after a short pause.

**OCR**

- Windows.Media.Ocr runs in the background after each image save. Its text goes into `OcrText` and the search index.
- The OCR engine has a maximum image size, so tall scrolling captures are processed in tiles and the text joined.
- Grab Text: select a region, OCR it, and put the text on the clipboard without saving a capture (Proposed).
- OCR language is the Windows profile language. Whether other languages are needed is Open.

**Videos in the library**

- Searchable by title, description, tags and source app.
- Thumbnails come from an FFmpeg frame grab about 1 second in.
- Local speech-to-text transcripts for search are a later idea (Open).

## Video recording

Recordings are MP4 files (H.264 video, AAC audio) made with ScreenRecorderLib. They include the microphone, optional system audio, and the webcam baked in as an overlay.

**Before recording**

- Pick a region, window or monitor with the same overlay used for stills.
- Choose the mic, toggle system audio, and choose the webcam. A live mic level meter is a Should.
- Preview the webcam and set its corner and size. Rectangle or circle shape is Open; a circle needs masking.
- A 3-second countdown starts the recording.

**While recording**

- A floating control bar offers pause/resume, stop, restart, discard, elapsed time, webcam on/off and mic mute.
- The control bar and other app windows are kept out of the video with `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` (`CaptureExclusion`).
- The start hotkey also stops the recording.
- Highlighting mouse clicks is a Should.

**Output (Proposed)**

- 30 fps by default, 60 as an option.
- Hardware encoding when the GPU supports it, quality-based bitrate.
- Written straight into the library folder.

**After recording**

- The video is saved to the library with a thumbnail and opens in a simple player with title, description and tags.
- Trimming the start and end (FFmpeg stream copy) and GIF export are Open for v1.
- A separate webcam file, composited later so the webcam can be moved or hidden after recording, is a Could.

**Spike before Phase 4:** confirm ScreenRecorderLib can toggle the webcam overlay mid-recording, mix mic and system audio at set levels, and pause cleanly. It is a native library, so the app will likely need an x64 platform target. The fallback is FFmpeg capture or Media Foundation directly.

## Scrolling capture

Scrolling capture ships in two stages. Manual comes first because it works in any app. Automatic scrolling follows for apps that expose a scroll control.

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

Seven phases, each usable on its own. Matt reviews after Phase 0, because the spikes confirm the stack, then after Phase 3 and Phase 6 (Proposed). Chorus can run the phases between review gates without stopping. Each phase is done when every box below is ticked.

**Phase 0: Foundation and spikes**

- [x] Solution scaffolded as in Architecture; builds clean with warnings as errors
- [x] Generic Host with DI, Serilog and a settings file
- [ ] Tray icon with Capture, Library and Exit (built; needs a manual check on Windows)
- [ ] Spike: `PrtSc` hotkey registers, or the app names the Windows setting to change
- [ ] Spike: frozen overlay lines up on every monitor at its own scaling
- [ ] Spike: ScreenRecorderLib records with mic, system audio and webcam overlay
- [ ] Spike: SkiaSharp canvas in WPF draws and drags an arrow smoothly on a 4K capture

*Review gate: spikes confirm the stack.*

**Phase 1: Capture and library**

- [ ] Region, window and full-screen capture from the hotkey
- [ ] Delayed capture
- [ ] Each capture saves its original, a PNG export and a database row
- [ ] Each capture is copied to the clipboard
- [ ] Library grid shows thumbnails, newest first

**Phase 2: Editor**

- [ ] Every Must tool works on new and reopened captures
- [ ] Undo and redo work for every tool
- [ ] Reopening a capture, moving an annotation and re-exporting works
- [ ] Golden-image tests pass for every tool
- [ ] A test proves redacted areas of the export contain no original pixels

**Phase 3: Organize and search**

- [ ] Tags and descriptions editable in the detail pane and quick bar
- [ ] OCR runs in the background and its text is searchable
- [ ] Search with filter tokens updates as Matt types
- [ ] Grab Text copies OCR text from a region

*Review gate: daily driver for screenshots.*

**Phase 4: Video**

- [ ] Record a region, window or monitor with mic, system audio and webcam
- [ ] The control bar never appears in the video
- [ ] Pause, resume, and stop by hotkey work
- [ ] Recordings land in the library with a thumbnail and play back

**Phase 5: Scrolling capture**

- [ ] Manual mode stitches a long web page with a sticky header correctly
- [ ] Stitcher fixture tests pass
- [ ] Automatic mode works in Chrome and Edge, after a UI Automation spike

**Phase 6: Polish**

- [ ] Settings window for hotkeys, library folder and defaults
- [ ] Start with Windows
- [ ] One-click backup of the library database

*Review gate: ready to retire Snagit.*

## Risks and spikes

The biggest risks are multi-monitor DPI bugs and scroll stitching. Both are front-loaded: DPI in the Phase 0 spike, stitching through manual mode and fixture tests.

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Mixed-DPI monitors offset the selection | Wrong pixels captured | Per-Monitor V2, physical pixels end to end, Phase 0 spike |
| Snipping Tool owns `PrtSc` | Hotkey silently fails | Detect the failed registration and name the setting; low-level keyboard hook as fallback |
| Preview and export render differently | Exports don't match the editor | One SkiaSharp renderer plus golden-image tests |
| ScreenRecorderLib can't toggle webcam or pause cleanly | Video features blocked | Phase 0 spike; fall back to FFmpeg or Media Foundation |
| Stitching errors on sticky or lazy content | Broken scrolling captures | Manual mode first, live preview, fixture tests |
| Very tall images exhaust memory | Editor crashes | Tiled rendering, height limit |
| HDR displays wash out GDI grabs | Dull captures | Confirm Matt's displays; Windows.Graphics.Capture with tone mapping if needed |
| Unredacted originals stay on disk | Sensitive data kept | DPAPI encryption or discard on request (Open) |
| Library folder syncs to OneDrive | Large videos fill the cloud quota | Confirm the library location before Phase 1 |

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

**Working with Chorus**

- One phase per branch and worktree.
- Each phase gets its own `spec.md` and `task.md` under `docs/phases/`, cut from this plan.
- Open items that block a phase are answered before that phase starts.

## Open questions for Grill Me

These are the starting questions, grouped by the phase they block. Grill Me should push further wherever an answer opens a new question.

**Before Phase 0**

- [x] What is the app called? **Snare** (repo `ContactEstablished/Snare`, public)
- [ ] Is Windows 11 only acceptable?
- [ ] How many monitors, at what resolutions and scaling? Any HDR?
- [ ] Is it OK to turn off Windows' `PrtSc` to Snipping Tool setting? Which hotkeys should we use?
- [ ] Does the single SkiaSharp renderer survive council review, or should the editor use WPF drawing?
- [ ] Run from a build folder, or install with auto-update?

**Before Phase 1**

- [ ] Where does the library live, and is that folder synced to OneDrive?
- [ ] What file naming pattern should exports use?
- [ ] After a capture: open the editor, show a quick bar, or just copy to the clipboard?
- [ ] Is every capture auto-saved, or only ones Matt keeps?
- [ ] Include the mouse cursor by default?
- [ ] Window captures: rounded corners with transparency, or a plain rectangle?

**Before Phase 2**

- [ ] Which Should and Could tools move into Phase 2?
- [ ] Re-editing: overwrite the export, or keep the old one as a version?
- [ ] Keep unredacted originals, encrypt them, or discard them after redaction?
- [ ] Are saved style presets per tool needed?
- [ ] Is combining several captures into one image needed?

**Before Phase 3**

- [ ] Should tags have colors? Any hierarchy?
- [ ] Is OCR needed in any language besides English?
- [ ] Will video transcripts for search ever matter?

**Before Phase 4**

- [ ] Typical recording length, resolution and frame rate?
- [ ] Is a baked-in webcam OK for v1? Which shape and position?
- [ ] Is system audio needed, or mic only?
- [ ] Are trim, GIF export and click highlighting in v1?

**Before Phase 5**

- [ ] Which apps matter most for scrolling capture (Chrome, Edge, VS Code, others)?
- [ ] Is horizontal scrolling needed?
- [ ] What is the longest capture Matt expects to take?

**Process**

- [ ] Which phases can Chorus run without stopping for review?
- [ ] Which models handle which roles for this project?
