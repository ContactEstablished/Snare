# Phase 0 tasks

See [spec.md](spec.md).

## Tasks

- [x] Solution scaffolded; builds clean with warnings as errors
- [x] Generic Host with DI, Serilog and a settings file
- [x] Hotkey service with conflict diagnosis, plus tests
- [x] Monitor enumeration, screen grabs, freeze-frame and virtual-desktop composer, plus tests
- [x] Freeze overlay spike harness
- [x] SkiaSharp canvas spike harness
- [x] ScreenRecorderLib added (whole solution now x64); recording frame-size rule plus tests; recording spike harness
- [x] Tray menu: Capture, Library, Spikes submenu, Exit
- [ ] Code review, merge, push

## Checked by the agent on Matt's PC (Oct 28, 2026)

- The app starts as x64 with every service resolved. The log shows `Shift+PrtSc` and `Ctrl+Shift+PrtSc` registered, and `PrtSc is taken by Snagit` with the advice text.
- `GdiScreenFreezer` grabbed `\\.\DISPLAY1` at 5120×1440, 96 dpi (run with `tests\Snare.Capture.Tests\bin\x64\Debug\net10.0-windows10.0.22621.0\Snare.Capture.Tests.exe -explicit only -diagnostics`). The upper monitor was switched off at the time; Windows reported only one display. The two-monitor check below is still needed.
- ScreenRecorderLib loads, and the screen-only recorder smoke test produced a 4096×1152 H.264 MP4 from the 5120×1440 monitor (`Snare.Recording.Tests.exe -explicit only -diagnostics`). No mic, audio or webcam were used.

## Awaiting Matt

Run with `dotnet run --project src/Snare.App`, or `dotnet build` and then `src\Snare.App\bin\x64\Debug\net10.0-windows10.0.22621.0\Snare.exe`. Logs are in `%LOCALAPPDATA%\Snare\logs`.

- [ ] **Tray icon:** a blue "S" appears in the tray. Right-click shows Capture, Library, Spikes and Exit. Exit closes the app and the icon disappears.
- [ ] **Hotkey with Snagit running:** the tray tooltip says `PrtSc is taken by Snagit`, and a notification explains what to change.
- [ ] **Hotkey with Snagit's PrtSc turned off (or Snagit quit), then Snare restarted:** the tooltip shows no warning, and `PrtSc` opens the freeze overlay.
- [ ] **Freeze overlay, both monitors on:** open Spikes → Freeze overlay, or press `PrtSc`.
  - Each monitor shows a frozen copy of itself with no visible shift. Move a window first, so a frozen screen is easy to tell apart.
  - The readout's coordinates run from (0, 0) at the primary's top-left. The upper monitor shows negative Y, starting at (856, −1440).
  - The color shown in the readout matches the pixel under the crosshair.
  - An open menu or tooltip (for example, hover a toolbar button, wait for its tooltip, then press `PrtSc`) appears in the frozen image.
  - Esc closes both overlays.
- [ ] **Skia canvas:** open Spikes → Skia canvas (5120×2880 pattern), then maximize the window. Drag both arrow ends around quickly. The movement feels smooth, and the title bar shows a paint time well under 16 ms. Repeat after Spikes → Freeze overlay → **Ctrl+S**, which opens the canvas on the real frozen desktop. Note the paint time either way; if it's over 16 ms, Phase 2 moves to `SKGLElement` (GPU).
- [ ] **Recording:** open Spikes → Record 20 s test. Talk the whole time, play some audio, click around and stay in front of the webcam. The script runs on wall-clock time: pause at 5 s, resume at 8 s, webcam hidden 11–14 s, mic muted 15–18 s, stop at 22 s. When the tray notification says it's done, open the MP4 from `%LOCALAPPDATA%\Snare\spikes` and check:
  - the video is 4096×1152 and about 19 s long (the 3 s pause is cut out);
  - both your voice and the system audio are audible;
  - the webcam is in the bottom-right corner, about a fifth of the height;
  - the jump at 5 s, where the pause was, is clean;
  - the webcam disappears from about 8 to 11 s of video;
  - your voice drops out from about 12 to 15 s of video, while system audio continues;
  - left clicks show a yellow dot and right clicks a cyan one.

  If the webcam doesn't hide or the mic doesn't mute, note it here. Phase 4 would then rebuild the recorder to apply those changes, or fall back to FFmpeg. That counts as a stack change, so Matt decides.
