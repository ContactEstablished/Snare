# Phase 4: Video

Cut from [the build plan](../../build-plan.md): Video recording, Library (videos), and Roadmap → Phase 4. It builds on the Phase 0 ScreenRecorderLib spike.

## Goal

`Shift+PrtSc` records a region, window or monitor to MP4 (H.264 + AAC). The recording can include the mic, system audio, a baked-in webcam overlay, and click highlights. A control bar that never appears in the video can pause, resume, restart, discard, mute, hide the webcam and stop. The recording lands in the library with a thumbnail, opens in a simple player, and can be trimmed or exported as a GIF through FFmpeg.

**Risk carried from Phase 0:** two things are still unverified, because the spike's 22-second recording hasn't been run by Matt:
- hiding the webcam mid-recording, done by moving the overlay off the canvas, since ScreenRecorderLib has no on/off switch;
- muting the mic, done by setting the source volume to 0.

This phase builds on both. If Matt's check shows either doesn't work, that's a stack change and goes back to him.

## Flow

1. `Shift+PrtSc`, or tray → Record video, freezes the screen and opens the capture overlay, so the target is chosen the same way as for stills:
   - region drag, window click, `F` for a monitor;
   - `A` (all monitors) is refused for video, with a message, because a recording comes from one display;
   - a region is clipped to the monitor under its center.
2. **`RecordSetupWindow`**, on the target's monitor:
   - mic: a device list or None;
   - system audio: on or off;
   - webcam: a device list or None;
   - webcam corner (four buttons) and size (10–40% of the output height, default 20%);
   - frame rate (30 or 60) and click highlighting (on by default);
   - Record and Cancel buttons.

   Choices are remembered in `%LOCALAPPDATA%\Snare\recording.json`. A live webcam preview is deferred (see Out of scope); a schematic shows the corner and size instead.
3. A 3-second countdown, using the existing `CountdownWindow`, which is excluded from capture.
4. **`ScreenRecorderLibRecorder`** (Recording) starts with options from `RecordingOptionsFactory`. That factory is pure, so it can be tested:
   - the display source is the target monitor, with `SourceRect` relative to it, or a window source by handle;
   - `OutputFrameSize` comes from `RecordingFrameSize.Fit`, at most 4096 wide with even dimensions;
   - H.264 at quality 70, hardware encoding, fixed frame rate, MP4 fast start;
   - audio sources: the chosen mic and/or the default loopback;
   - a webcam `VideoCaptureOverlay` anchored to the chosen corner with a 24 px offset;
   - mouse click detection when enabled.
5. **`RecordingControlBar`** is a small topmost window: elapsed time, Pause/Resume, Mic mute, Webcam on/off, Restart, Discard and Stop.
   - It is excluded from capture with `CaptureExclusion`.
   - It sits just above the recorded area on the same monitor, or just inside the top edge if there's no room.
   - `Shift+PrtSc` again means Stop.
6. **On stop:**
   1. The MP4 is written straight to `<LibraryRoot>\yyyy\MM\yyyy-MM-dd_HHmmss[_n].mp4`.
   2. `VideoTools` reads its duration and size with FFmpeg and grabs a frame about 1 s in (or at 0 s for shorter videos) as the thumbnail.
   3. `ICaptureStore.SaveVideoAsync` adds the row: `Kind = Video`, `DurationMs`, source app and window from the freeze, and the thumbnail.

   The player then opens.
7. **Restart** discards the file and starts again with the same settings, after a new countdown. **Discard** stops recording and deletes the file; nothing is saved.

## FFmpeg (Recording)

- **`FfmpegLocator`** looks in this order: `SnareOptions.FfmpegPath`, then `tools\ffmpeg\ffmpeg.exe` next to the app, then the `PATH`. File access and `PATH` are injected, so it can be tested.
- **`FfmpegArguments`** (pure, tested) builds the argument lists for:
  - probe: `-hide_banner -i <in>`, parsing `Duration:` and the video stream size from stderr;
  - thumbnail: `-ss <t> -i <in> -frames:v 1 -vf scale=640:-2 <out.png>`;
  - trim: `-ss <start> -to <end> -i <in> -c copy -avoid_negative_ts make_zero <out>`, a stream copy, so cuts land on keyframes;
  - GIF: `-ss <start> -to <end> -i <in> -vf "fps=12,scale=<w>:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse" <out.gif>`, with the width capped at 960.
- **`FfmpegRunner`** runs the exe asynchronously with an argument list (no shell), captures stderr, supports cancellation, and throws `FfmpegException` with the last stderr lines on a non-zero exit.
- If FFmpeg isn't found:
  - recording still works and the video is saved;
  - it gets no thumbnail, and its duration is taken from the recorder's elapsed time;
  - Trim and GIF export are disabled, with a note explaining how to install FFmpeg.

## Library

- `LibraryPaths.UniqueVideoPathFor(capturedAt)`.
- `ICaptureStore.SaveVideoAsync(path, width, height, durationMs, source, capturedAt, thumbnailPng?)`.
- `ICaptureStore.ReplaceVideoAsync(id, durationMs, thumbnailPng?)`, used after a trim: the file is replaced in place and the row and thumbnail are updated.
- The OCR and reconcile code already skip videos. Reconcile moves a video whose file is missing to the Trash.
- **Library grid:** video tiles show a "▶ m:ss" badge.
  - Double-click, Enter or Edit opens the player instead of the editor.
  - `kind:video` already filters.
  - The detail pane works the same for videos (title, description, tags).

## Player (App)

**`VideoPlayerWindow`** plays with WPF `MediaElement`:
- play/pause (Space), a seek slider, and current time and duration;
- **Set start**, **Set end** and **Trim**. Trim first writes a stream copy to a temp file, then replaces the video in place and updates the row and thumbnail. It asks first, because it overwrites the video.
- **Export GIF…**, which uses the selected range, or the whole video;
- Show in folder, and Copy file (a file drop on the clipboard).

## Out of scope

- A separately composited webcam, a circular webcam, and webcam preview in the setup window. A live preview needs a camera pipeline that ScreenRecorderLib doesn't expose. It is listed as a known gap.
- Recording across several monitors at once.
- A mic level meter (a Should) is deferred to Phase 6 polish.
- Transcripts.

## Done when

Builds clean, all tests pass (including options factory, FFmpeg arguments and locator, video storage, and explicit FFmpeg integration tests on a generated test video), CI is green, and the Awaiting Matt checks are listed in `task.md`.
