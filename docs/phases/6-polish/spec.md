# Phase 6: Polish

Cut from [the build plan](../../build-plan.md): Roadmap → Phase 6, Image capture (hotkeys), Editor (Callout), Data and storage, Decisions → Before Phase 6, and the gaps earlier phases deferred here.

## Goal

Snare becomes something Matt can live in: a Settings window (hotkeys, library folder, capture defaults, Start with Windows, backups), one-click backup of the library, the Callout editor tool, and the loose ends earlier phases left for polish.

## Settings

- **Storage:** `%LOCALAPPDATA%\Snare\settings.json` holds Matt's changes in the same `"Snare"` section shape as `appsettings.json`. It is layered over `appsettings.json` with reload on change, so `IOptionsMonitor<SnareOptions>` sees a save at once. The file holds the whole section, written atomically (temp file, then replace).
- **New options:**
  - `Hotkeys`: a gesture per action, as text ("Ctrl+Shift+PrtSc"). Defaults are the decided keys.
  - `StartWithWindows`: default true.
  - `BackupFolder`: empty means `Documents\Snare Backups`.
- **Settings window** (tray → Settings…), one page of grouped sections:
  - **Hotkeys:** one box per action. Click a box and press the combination; it shows what was pressed. Esc in the box keeps the old one; Backspace clears it, which turns that hotkey off.
    - Duplicates are refused with a message.
    - At least one key must be pressed with the modifiers. `PrtSc`, `A–Z`, `0–9` and `F1–F24` are allowed.
    - Plain letters and digits need Ctrl or Alt, so typing never fires a capture.
    - After saving, hotkeys are registered again at once. A failure shows the existing conflict notice for that hotkey.
  - **Library:** the folder, with Browse…. A note says existing captures stay where they are and new captures go to the new folder (decided). It takes effect at once.
  - **Captures:**
    - after capture: quick bar or editor;
    - include the mouse pointer;
    - export format (PNG or JPG) and JPG quality (50–100);
    - FFmpeg path, with Browse….
  - **Startup:** Start with Windows.
  - **Backup:** the folder, with Browse…, the time of the last backup, and **Back up now**.
  - **Save** and **Cancel** buttons. Save is disabled while something is invalid, with the reason shown.

## Start with Windows

- Snare writes the per-user Run key (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `Snare`) with the quoted path of the running exe, or removes it.
- This happens at startup, to match the setting, and again when the setting is saved.
- Debug builds never write it, so builds run from a worktree never land in Matt's sign-in. They log that they skipped it.

## Backup

- `LibraryBackup.CreateAsync(folder)` writes `Snare-backup-yyyy-MM-dd_HHmmss.zip`, holding:
  - `library.db`, a consistent copy taken with SQLite's online backup, so it's safe while Snare is running;
  - `originals\*.png`.
- It writes to a temp name and renames when complete, so a failed backup never looks like a good one.
- Afterwards it keeps the 10 newest `Snare-backup-*.zip` files in that folder and deletes older ones; other files are never touched.
- Entry points are tray → **Back up library** and Settings → **Back up now**. A notification gives the file and its size, or the error.

## Callout tool

- `CalloutAnnotation`: a rounded text bubble (bounds, text, font size, text color, fill, stroke, stroke width), with a tail ending at a free point (`Tail`). The tail leaves the bubble from the side nearest the point.
- The text wraps inside the bubble the same way as the Text tool.
- Shortcut `K`, with its own remembered style.
- To draw one, drag the bubble, then type. The tail starts below and left of the bubble.
- In Select, the bubble has resize handles plus a tail handle, which is dragged to point. Moving the bubble moves the tail with it.
- It renders through `AnnotationRenderer` (golden images), and hit-tests on the bubble and the tail.

## Loose ends from earlier phases

- **Exit flush (Phase 2):** tray → Exit saves every open editor's pending autosave before shutting down.
- **Exit while recording (Phase 4):** tray → Exit during a recording asks:
  - **Stop and save**, which finishes the file and adds it to the library, then exits;
  - **Discard**;
  - **Cancel**, which keeps recording.
- **Resize dialog (Phase 2):** Editor → Resize… sets a new width and height, with "keep proportions" on by default, or a percentage. It uses the existing resize command, so it can be undone.

## Tests

- Core: gesture parsing and formatting, validation (duplicates, bare letters).
- Library: paths follow a changed library root; the backup holds a restorable database and every original; pruning keeps 10 and leaves other files alone; a failed backup leaves no zip.
- Interop: Run-key registration against a test key; key-name to virtual-key mapping.
- Rendering: Callout golden images (tail in each direction) and its geometry (hit test, handles, move).
- App: settings view-model validation and save; the settings store round-trip; the hotkey box; XAML loads.

## Out of scope

- A live mic level meter (Phase 4 deferred it here). ScreenRecorderLib has no level API before recording starts, and adding a second audio capture just for a meter isn't worth it in v1. Recorded as a known gap.
- Restoring a backup from inside Snare. Restore is by hand: quit Snare and unzip over `%LOCALAPPDATA%\Snare`. The steps are in task.md.
