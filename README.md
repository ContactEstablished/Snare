# Snare

A personal Snagit-style screen capture app for Windows. Region, window and scrolling capture; an annotation editor with redaction and blur; video recording with mic and webcam; and a searchable library of plain PNG, JPG and MP4 files.

**Status:** Phase 0, foundation and spikes. The solution builds and runs as a tray app; capture arrives in Phase 1. See the [build plan](docs/build-plan.md).

## Requirements

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026, Rider, or VS Code with C# Dev Kit

## Build and run

```powershell
dotnet build Snare.slnx
dotnet test Snare.slnx
dotnet run --project src/Snare.App
```

Snare starts in the system tray. Logs are written to `%LOCALAPPDATA%\Snare\logs`.

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
