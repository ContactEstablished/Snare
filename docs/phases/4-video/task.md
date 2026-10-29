# Phase 4 tasks

See [spec.md](spec.md).

## Tasks

- [x] Core/Recording types; `RecordingOptionsFactory`; `ScreenRecorderLibRecorder` with device lists, pause, mic mute, webcam toggle; plus tests
- [x] FFmpeg: `FfmpegLocator`, `FfmpegArguments`, `FfmpegRunner`, `VideoTools`; plus tests and an explicit real-FFmpeg test
- [x] Library: `UniqueVideoPathFor`, `SaveVideoAsync`, `UpdateVideoAsync`, reconcile for videos; plus tests
- [x] Capture: `RecordingTargets` (selection to one display), plus tests
- [x] App: `RecordingService`, setup window, control bar, `Shift+PrtSc` start and stop, tray entry
- [x] App: video player with trim and GIF export; library video tiles (▶ badge) open the player; plus tests
- [ ] Code review, merge, push

## Checked by the agent on Matt's PC (Oct 29, 2026)

- Real FFmpeg 8.1.1 (`Snare.Recording.Tests.exe -explicit only -method "*RealFfmpeg*"`) probed a generated 640×360 test video, thumbnailed it, trimmed 1–3 s to 2.07 s (stream copy), and exported a GIF.
- The Phase 0 screen-only recorder smoke test produced 4096×1152 H.264 from the 5120-wide monitor.
- The app starts with every recording service resolved.

## Known gaps

- The setup window shows a schematic, not a live webcam preview. ScreenRecorderLib has no preview API; see spec.md.
- Recording covers one monitor. "All monitors" (`A`) is refused with a message.
- Trim is a stream copy, so cuts snap to the nearest earlier keyframe (usually within about 2 s).

## Awaiting Matt

Run the Phase 0 **Recording** spike check first (`docs/phases/0-foundation/task.md`). If the webcam doesn't hide, or the mic doesn't mute, that's a stack decision for Matt before relying on those buttons.

- [ ] **Region recording:**
  - Press `Shift+PrtSc` and drag a region on the primary monitor.
  - In the setup window, pick the fifine mic, turn system audio on, pick the EMEET webcam (bottom-right, 20%) and press Record.
  - After the 3-2-1 countdown, talk, play audio and click around, then press `Shift+PrtSc` again.
  - The player opens. The video shows the region with clicks highlighted, the webcam in the corner, and both audio tracks.
- [ ] **Full monitor:** `Shift+PrtSc`, then `F` on the primary. The file is 4096×1152 (check Properties → Details). Do the same on the upper monitor: 3440×1440.
- [ ] **Window recording:** `Shift+PrtSc`, then click a window, and move the window around while recording. The video follows the window.
- [ ] **Control bar:**
  - It sits just above the recorded area and never appears in the video.
  - Pause, then Resume: no gap in the file, and the clock stops while paused.
  - Mute mic: your voice drops out.
  - Hide webcam: it disappears, and comes back when shown.
  - Restart: a new countdown, and the first take is gone.
  - Discard: nothing is saved.
  - The bar can be dragged.
- [ ] **`A` is refused:** `Shift+PrtSc` then `A` shows a "covers one monitor" notification.
- [ ] **Library:** the recording appears with a thumbnail and a "▶ m:ss" badge. `kind:video` finds it. Double-click opens the player, and its title, description and tags can be edited in the detail pane.
- [ ] **Player:**
  - Space plays and pauses; ←/→ seek by 1 s, and with Shift by 5 s.
  - `[` and `]` (or Set start / Set end) mark a range.
  - Trim to range asks first, then shortens the file in place. The library's badge and thumbnail update.
  - Export GIF… writes a GIF of the range, or of the whole video.
- [ ] **Without FFmpeg:** temporarily set `"FfmpegPath": "C:\\nowhere\\ffmpeg.exe"` and remove winget's FFmpeg from `PATH` for Snare (or uninstall it). Record: the video still saves, without a thumbnail. The player explains how to install FFmpeg, and Trim and GIF are disabled.
