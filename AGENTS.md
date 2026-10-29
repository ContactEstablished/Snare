# Agent guide for Snare

Snare is a personal, Windows-only Snagit replacement built with .NET 10 and WPF. The spec is [docs/build-plan.md](docs/build-plan.md). Read the sections for the phase you are working on before writing code.

## Commands

- Build: `dotnet build Snare.slnx`
- Test: `dotnet test Snare.slnx`
- Run: `dotnet run --project src/Snare.App` (Windows only; starts in the tray, no window)

`EnableWindowsTargeting` is on, so the Windows-targeted projects also restore and compile on Linux or WSL. Anything that touches Win32 or WPF only runs on Windows. CI builds and tests on `windows-latest` for every push to `main` and every pull request.

## Architecture rules

- Only `Snare.App` references WPF.
- Only `Snare.Interop` calls Win32. Add the API name to `src/Snare.Interop/NativeMethods.txt`, then wrap it in a small public method in that project. Never call `PInvoke` from anywhere else.
- `Snare.Core` has no project references and no Windows dependency. Domain models and interfaces live there.
- `Snare.Library` may reference `Snare.Rendering`. Otherwise, libraries reference only Core and Interop.
- Each library exposes an `AddSnare<Module>()` extension method; `App.xaml.cs` wires them up.
- All capture geometry is in physical pixels (`PixelRect`, `PixelPoint`). Convert to WPF DIPs only to draw overlay chrome.
- Annotation JSON goes through `AnnotationJson` only. New editor tools are new records deriving from `AnnotationObject`, registered with `[JsonDerivedType]`.

## Code conventions

- Nullable is on and warnings are errors.
- Package versions live in `Directory.Packages.props`. Add a `PackageVersion` there, then a version-less `PackageReference` in the project.
- MVVM with CommunityToolkit.Mvvm source generators. Code-behind holds only view wiring.
- Async all the way for I/O. Keep capture and rendering off the UI thread where possible.
- File-scoped namespaces; one public type per file.

## Tests

- xUnit v3. Every library change comes with tests in the matching `tests/` project.
- Rendering changes need golden-image tests: render known annotation JSON and compare against checked-in PNGs with a small tolerance.
- Stitching changes need fixture tests built from recorded frame sequences.
- Multi-monitor and DPI behavior is checked by hand. List the manual checks in the phase's `task.md`.

## Workflow

- One phase per branch (`phase/<n>-<name>`) and worktree. Each phase gets `docs/phases/<n>-<name>/spec.md` and `task.md`, cut from the build plan.
- Items marked **Open** in the build plan need Matt's answer before the phase that depends on them starts. Ask; don't decide them silently. A new question that comes up mid-phase is added to the plan as **Open** and put to Matt.
- Checks that need Matt's eyes or hands go under an **Awaiting Matt** heading in `task.md`, with a runnable harness or exact steps. They don't block the next phase.
- Before a phase merges, a code-review subagent reviews its diff. Then merge into `main` with `--no-ff` and push `main` and the phase branch to `origin`.
- A task is done when it builds clean, its tests pass, its manual check is ticked in `task.md`, and the build plan reflects any decision it changed.
- Never commit FFmpeg or other large binaries (see `tools/ffmpeg/README.md`).
