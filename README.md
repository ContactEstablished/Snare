# Snare

A personal Snagit-style screen capture app for Windows. Region, window and scrolling capture; an annotation editor with redaction and blur; video recording with mic and webcam; and a searchable library of plain PNG, JPG and MP4 files.

**Status:** Phases 0–6 are code-complete; the hand checks in each phase's `task.md` are next. See the [build plan](docs/build-plan.md).

## Requirements

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual C++ x64 runtime](https://aka.ms/vs/17/release/vc_redist.x64.exe), needed by ScreenRecorderLib for video
- Visual Studio 2026, Rider, or VS Code with C# Dev Kit

## Build and run

```powershell
dotnet build Snare.slnx
dotnet test Snare.slnx
dotnet run --project src/Snare.App
```

Snare starts in the system tray. Logs are written to `%LOCALAPPDATA%\Snare\logs`.

The solution builds for x64 only, because ScreenRecorderLib is a native library. The exe is in `src\Snare.App\bin\x64\Debug\net10.0-windows10.0.22621.0\`.

## Install

```powershell
./tools/build-msix.ps1     # publishes, packs and signs artifacts\msix\Snare-<version>.msix
./tools/install-msix.ps1   # first time: trusts the signing certificate (one admin prompt); then installs
```

Snare then appears in the Start menu. Run both again to update. The package is signed with a self-signed `CN=Snare Dev` certificate kept in your user certificate store. The library and settings stay in `%LOCALAPPDATA%\Snare` when Snare is updated or uninstalled. FFmpeg isn't bundled: install it with `winget install Gyan.FFmpeg` for video thumbnails, trimming and GIF export.

## Projects

| Project | Responsibility |
| --- | --- |
| `Snare.App` | WPF shell: tray, hotkeys, capture overlay, editor and library windows |
| `Snare.Core` | Domain models and interfaces; no UI, no I/O, no Windows dependency |
| `Snare.Interop` | CsWin32 bindings and thin wrappers; the only project that calls Win32 |
| `Snare.Capture` | Freeze-frame, window capture, scrolling stitcher |
| `Snare.Rendering` | SkiaSharp renderer for annotations, effects and export |
| `Snare.Recording` | Video recording and FFmpeg jobs |
| `Snare.Library` | SQLite, file store, full-text search, OCR indexing, thumbnails |

Tests live under `tests/`, one project per library.

## Working with AI agents

Agent instructions are in [AGENTS.md](AGENTS.md). Claude Code picks them up through [CLAUDE.md](CLAUDE.md).
