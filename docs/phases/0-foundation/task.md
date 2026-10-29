# Phase 0 tasks

See [spec.md](spec.md).

## Tasks

- [x] Solution scaffolded; builds clean with warnings as errors
- [x] Generic Host with DI, Serilog and a settings file
- [ ] Hotkey service with conflict diagnosis, plus tests
- [ ] Monitor enumeration, screen grabs, freeze-frame and virtual-desktop composer, plus tests
- [ ] Freeze overlay spike harness
- [ ] SkiaSharp canvas spike harness
- [ ] ScreenRecorderLib added (x64); recording frame-size rule plus tests; recording spike harness
- [ ] Tray menu: Capture, Library, Spikes submenu, Exit
- [ ] Code review, merge, push

## Awaiting Matt

Run with `dotnet run --project src/Snare.App` (or `dotnet build` then `src\Snare.App\bin\x64\Debug\net10.0-windows10.0.22621.0\Snare.exe`). Logs are in `%LOCALAPPDATA%\Snare\logs`.

- [ ] **Tray icon:** a blue "S" appears in the tray. Right-click shows Capture, Library, Spikes and Exit. Exit closes the app and the icon disappears.
- [ ] **Hotkey with Snagit running:** the tray tooltip says `PrtSc` is taken and names Snagit; the log says the same.
- [ ] **Hotkey with Snagit's PrtSc turned off (or Snagit quit), then Snare restarted:** the tooltip shows no warning, and `PrtSc` opens the freeze overlay.
- [ ] **Freeze overlay, both monitors:** open Spikes → Freeze overlay, or press `PrtSc`.
  - Each monitor shows a frozen copy of itself with no visible shift. Move a window first, so a frozen screen is easy to tell apart.
  - The readout's coordinates run from (0, 0) at the primary's top-left. The upper monitor shows negative Y, starting at (856, −1440).
  - The color shown in the readout matches the pixel under the crosshair.
  - Esc closes both overlays.
- [ ] **Skia canvas:** open Spikes → Skia canvas (5120×2880 test pattern), then maximize the window. Drag both arrow ends around quickly. The movement feels smooth and the FPS readout stays near your refresh rate, or at least above 60. Repeat after Spikes → Freeze overlay → **Ctrl+S**, which opens the canvas on the real frozen desktop.
- [ ] **Recording:** open Spikes → Record 20 s test. Talk, play some audio, click around and stay in front of the webcam. When the tray balloon says it's done, open the MP4 from `%LOCALAPPDATA%\Snare\spikes` and check:
  - the video is 4096×1152;
  - both your voice and the system audio are audible;
  - the webcam is in the bottom-right corner;
  - the pause from 5 to 8 s left no gap or glitch;
  - the webcam disappears from about 11 to 14 s (shifted 3 s earlier in the file by the pause);
  - clicks are highlighted.
