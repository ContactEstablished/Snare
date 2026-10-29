# Phase 5: Scrolling capture

Cut from [the build plan](../../build-plan.md): Scrolling capture, Image capture (hotkeys), Library and search (OCR tiling), and Roadmap → Phase 5.

## Goal

`Ctrl+Shift+PrtSc` captures something taller than the screen. Matt picks a region or window, then either scrolls it himself (manual mode, any app) or lets Snare scroll it (automatic mode, aimed at Chrome and Edge). Snare grabs the region about 10 times a second, stitches the frames that moved into one tall image, and saves it to the library as a **Scrolling** capture, up to 30,000 px tall. Vertical only.

## Flow

1. `Ctrl+Shift+PrtSc`, or tray → Scrolling capture, freezes the screen and opens the capture overlay. A region drag, a window click or `F` for a monitor picks the area. `A` (all monitors) is refused with a message, as for video. The area is clipped to the monitor under its center.
2. The screen unfreezes and a **scroll panel** appears beside the area, on the same monitor and outside the area. It is excluded from capture. It holds:
   - **Start**: manual mode. Matt scrolls at his own pace.
   - **Auto-scroll**: automatic mode.
   - **Stop**: ends the capture and saves it.
   - **Cancel**: ends it without saving. Closing the panel any other way cancels too.
   - The panel never takes focus, so the page being scrolled keeps it.
   - A live preview of the bottom of the image as it grows, the height so far ("4,210 px of 30,000"), and the latest warning ("Couldn't line up that frame; scroll a little slower").
3. While it runs, a thin border is drawn around the area (outside it, so it never appears in the grabs).
4. When it stops (Stop, the height cap, or automatic mode reaching the end), the image goes through the same path as a still capture: saved to the library, copied to the clipboard, then the quick bar or the editor (the `AfterCapture` setting).

## Stitching (`Snare.Capture/Scrolling`)

`ScrollStitcher` takes BGRA frames of a fixed size and builds the result.

- Frames are compared in grayscale.
- **Scrolling columns:** only the widest run of columns that changed between the two frames takes part in matching. A fixed sidebar, a table of contents or the window frame would otherwise pull every match toward "no movement". A change narrower than an eighth of the area (a spinner, a cursor) on its own counts as no movement.
- **Sticky rows:** rows unchanged (at least 95% of their scrolling columns) at the same position in the previous and new frame, counted from the top and from the bottom, are the sticky header and footer. Each is capped at 40% of the frame. They are left out of matching. The header is kept once, from the first frame; the footer is kept once, from the last frame. A wrong guess (blank rows that only look sticky) costs nothing: those rows are still committed from a later frame.
- **Offset:** a textured strip (by edge strength, so blank space and smooth gradients are skipped, and never one that sat still) near the bottom of the previous frame's content band is found in the new frame's band with OpenCvSharp's normalized cross-correlation (`TM_CCOEFF_NORMED`). The offset is how far it moved up.
- **Checks:**
  - a match below 0.9 confidence is dropped;
  - so is a match whose whole overlap differs on average by more than a small tolerance (a repeating pattern or a half-painted frame);
  - an offset of 0 is "no movement";
  - an upward scroll is ignored until Matt scrolls back down;
  - a blank band (nothing to match) is dropped.

  Each drop sets a warning. The previous frame stays as the reference, so scrolling back to it recovers.
- **Commit:** only rows that can no longer change are committed. Each accepted pair commits the previous frame's rows from the last commit point down to the top of its footer. `Compose()` adds the rest of the last frame, footer included.
- **Cap:** at 30,000 px the stitcher reports the limit and accepts no more frames; `Compose()` truncates to the cap.

## Session (`Snare.Capture/Scrolling`)

`ScrollCaptureSession` runs the grab loop off the UI thread.

- **Manual:** grab every 100 ms and feed the stitcher.
- **Automatic:** through `IScrollDriver`:
  1. scroll one step;
  2. grab until two grabs in a row are identical (rendering has settled), up to 1.5 s;
  3. feed the stitcher.

  A step is half the view. A step that can't be lined up is scrolled back and retried at half the size, down to an eighth. After a step, it waits for the page to change before waiting for it to settle, so a slow repaint isn't mistaken for the end.

  It stops after two steps in a row that don't move the page (the end of the page), four misses in a row, or at the cap. When it ends by itself, a notification says why. The preview is shrunk to the panel's width and updated about four times a second.
- Progress (height, last outcome, warning, the tail of the image) is reported through `IProgress<ScrollProgress>`.

## Automatic scrolling (`Snare.Interop`)

`ScrollTarget.FromPoint(point)` uses UI Automation (the COM `IUIAutomation` through CsWin32):
- it takes the element under the area's center and walks up to the first ancestor whose scroll pattern is vertically scrollable;
- a step is `SetScrollPercent` by the requested share of the visible fraction; it then polls for up to 300 ms for the new position, since browsers report it a little later.

If there is no scroll pattern, or the pattern doesn't move the page, it falls back to mouse-wheel input (`SendInput`), with the cursor over the area's center: enough notches (100 DIPs each, so the monitor's scaling counts) for the requested share of the area. The fallback takes over for good the first time a pattern step doesn't move the page, within that same step.

## Library

- `ICaptureStore.SaveImageAsync` takes a `CaptureKind` (Image or Scrolling); `kind:scrolling` already searches it.
- Thumbnails of tall images use the top of the image (4:3) rather than a sliver of the whole.
- OCR already reads tall images in tiles (`OcrTiling`).

## Editor

Tall captures open fitted to the window's width rather than its height, so the text is readable. Skia draws only the visible part, which is the tiling the build plan asks for.

## Tests

- **Stitcher fixtures, synthetic:** a generated page with a sticky header and footer, cut into frames at known offsets (including no movement, scrolling back, a blank gap, noise, and the cap). The result must match the page exactly.
- **Stitcher fixtures, recorded:** frame sequences recorded from a local test page in headless Edge (`tests/Snare.Capture.Tests/Fixtures/Scrolling/`, recorded by `tools/scroll-fixture/record.mjs`). The page has a sticky header, so the frames carry real text rendering, sticky positioning and recorded scroll positions. The test checks the stitched height and that every frame's content appears at its recorded scroll position.
- Session tests with a fake frame source and scroll driver: manual skips duplicates; automatic waits for settling and stops at the end.
- UI tests: panel XAML loads, the view model's buttons, and tall-image editor fit.

## Out of scope

- Horizontal scrolling (decided: not needed).
- Stitching across monitors.
- Fixing lazy-loaded images or animations. Manual mode lets Matt slow down and retry.
