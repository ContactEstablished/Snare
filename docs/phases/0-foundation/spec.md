# Phase 0: Foundation and spikes

Cut from [the build plan](../../build-plan.md#roadmap). The scaffold, Generic Host, Serilog and settings are already done. This phase finishes the tray and proves the four risky parts of the stack on Matt's machine before Phase 1 builds on them.

## Goal

Each spike answers one question with a runnable harness Matt can try from the tray. Code that Phase 1 will keep (hotkeys, monitor enumeration, screen grabs, the virtual-desktop composer, the recording-size rule) is written to production standard with tests. Throwaway harness code lives in `src/Snare.App/Spikes/` and is deleted or absorbed in Phase 1.

## Matt's setup

Two monitors at 100% scaling: a 5120×1440 primary at (0, 0) and a 3440×1440 monitor above it at (856, −1440). The virtual desktop is 5120×2880 starting at (0, −1440), and two strips of its bounding box (left and right of the upper monitor) belong to no monitor. Snagit is running and owns `PrtSc`.

## Spikes

### 1. Hotkeys

- `HotkeyService` (App) owns a hidden message-only window (`HwndSource`) and registers `PrtSc` (image), `Shift+PrtSc` (video) and `Ctrl+Shift+PrtSc` (scrolling) through `GlobalHotkeys`, all with `MOD_NOREPEAT`.
- When a registration fails, `HotkeyConflictDiagnosis` (Core) produces a sentence naming the likely owner:
  - Snagit, if a `Snagit*` process is running;
  - else Snipping Tool, if Windows' "Use the Print screen key to open screen capture" setting is on (read by `SnippingToolSetting` in Interop from `HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled`);
  - else "another app".
- The sentence goes to the log and the tray tooltip.
- In Phase 0, `PrtSc` opens the freeze overlay spike. The other two keys only log that they fired.

### 2. Freeze-frame and overlay

- `DisplayMonitors.GetAll()` (Interop) returns each monitor's device name, physical bounds, work area, DPI and primary flag (`EnumDisplayMonitors`, `GetMonitorInfo`, `GetDpiForMonitor`).
- `ScreenGrabber.Grab(x, y, width, height)` (Interop) copies physical screen pixels to a top-down BGRA buffer with `BitBlt` + `GetDIBits`.
- Core gets `MonitorInfo` and `PixelBuffer` (BGRA, width, height, stride).
- `FreezeFrame` (Capture) grabs every monitor before any UI appears and returns a `FrozenDesktop`: the per-monitor frames plus their bounds.
- `VirtualDesktopComposer` (Capture) combines frames into one bitmap over the bounding box, with uncovered areas filled opaque black. Fully unit-tested, including negative coordinates and gaps.
- The spike harness opens one borderless, topmost overlay window per monitor. Each is placed with `SetWindowPos` in physical pixels (`WindowPlacement`, Interop), so DPI scaling can't move it. The window shows its monitor's frozen frame, a crosshair, and a readout of the physical cursor position. The readout also shows the color of the frozen pixel under the cursor, read straight from the frozen frame, so misalignment shows as a mismatch. Esc closes every overlay.

### 3. SkiaSharp canvas

- A spike window hosts an `SKElement`. It draws the frozen virtual desktop, or a generated 5120×2880 test pattern when opened from the menu, scaled to fit the window.
- Over the image sits an arrow whose end points can be dragged, drawn by `SpikeArrowPainter`. The real arrow tool and its golden tests are Phase 2.
- An FPS and last-frame-time readout shows whether dragging stays smooth.

### 4. Video (ScreenRecorderLib 7.0.1)

- `ScreenRecorderLib` is added to `Snare.Recording`. Because it is a native C++/CLI library, `Snare.Recording`, `Snare.App` and `Snare.Recording.Tests` build for `x64` only.
- `RecordingSpike` (Recording) records the primary monitor for about 20 seconds:
  - default mic plus system audio (loopback) at set volumes;
  - the default webcam as a bottom-right overlay at 20% of the output height;
  - click highlighting;
  - pause at 5 s and resume at 8 s;
  - the webcam overlay toggled off at 11 s and back on at 14 s;
  - stop at 20 s.
- The output goes to `%LOCALAPPDATA%\Snare\spikes\recording-spike-<timestamp>.mp4`, along with the ScreenRecorderLib log.
- `RecordingFrameSize.Fit` (Recording) applies the Decided rule: anything wider than 4096 px scales down to 4096 wide, keeping the aspect ratio and even dimensions. It has unit tests, and the spike uses it, so the 5120×1440 primary records at 4096×1152.

## Out of scope

Real capture flow, saving, the library, the editor, and settings UI all belong to later phases.

## Done when

Builds clean, all tests pass, CI is green, and every Awaiting Matt item in `task.md` is listed with steps. If the video spike fails, that is a stack change and goes back to Matt.
