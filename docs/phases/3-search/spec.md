# Phase 3: Organize and search

Cut from [the build plan](../../build-plan.md): Library and search, Data and storage, and Roadmap → Phase 3. This phase also covers the two items Phase 1 deferred: startup reconciliation and the Trash view.

## Goal

Every capture can carry a title, a description and flat tags. Windows OCR reads the text in every image in the background. One search box finds captures by any of these, plus the source app, with prefix matching, BM25 ranking and filter tokens. Grab Text OCRs a region straight to the clipboard. The library gets a detail pane, multi-select bulk actions, and a Trash view. On startup, the library repairs itself after files change outside the app.

## Search (Core + Library)

- **`SearchQueryParser`** (Core, pure) splits input into free terms and filters:
  - `tag:<name>` (repeatable; all must match)
  - `app:<process>` (prefix, case-insensitive)
  - `kind:image|video|scrolling`
  - `before:yyyy-MM-dd` and `after:yyyy-MM-dd` (local dates; `before` excludes its day, `after` includes its day)
  - Quoted phrases (`"exact words"`)
  - An unknown `key:value` is treated as a plain term.
- **Prefix matching:** free terms become an FTS5 expression of AND-ed quoted prefix terms, so `invoice err` becomes `"invoice"* "err"*` and finds "invoices" and "error". Quotes inside terms are escaped.
- **Schema script 2** adds the `CaptureSearch` FTS5 table:
  - columns: `CaptureId UNINDEXED`, `Title`, `Description`, `Tags`, `OcrText`, `SourceApp`, `SourceWindowTitle`;
  - tokenizer: `unicode61 remove_diacritics 2`.

  The store rewrites a capture's index row whenever it saves anything about the capture, and the script backfills existing captures.
- **`SearchAsync(SearchQuery)`:**
  - With terms: ranked by `bm25` with weights (title 10, tags 8, description 5, app 3, window title 2, OCR 1), then newest first.
  - Without terms: newest first.
  - Filters are applied in SQL, and the Trash is excluded.

## Tags and details (Library)

- **EF mappings** for `Tag(Id, Name NOCASE UNIQUE, Color)` and `CaptureTag`.
- **Store methods:**
  - `UpdateDetailsAsync(id, title, description)`.
  - `SetTagsAsync(id, names)` replaces a capture's tags. Names are trimmed and de-duplicated without regard to case. Unknown tags are created with the next color from `TagPalette` (Core, 10 fixed colors), and tags no capture uses any more are removed.
  - `AddTagsAsync(ids, names)` for bulk tagging.
  - `ListTagsAsync()` returns names, colors and usage counts.
  - `SetTagColorAsync(name, color)`.
  - `GetTagsAsync(id)`.
- **Trash:**
  - `ListTrashAsync()`.
  - `RestoreAsync(id)`.
  - `EmptyTrashAsync()` deletes the export, original and thumbnail files, then the rows and index rows.
- **`SetOcrTextAsync(id, text)`.**

## OCR (Library)

- **`WindowsOcrEngine : IOcrEngine`:**
  - Uses `Windows.Media.Ocr` with the user-profile language (English).
  - Images taller or wider than `OcrEngine.MaxImageDimension` are processed in overlapping tiles, and the lines are joined with duplicate lines at tile seams removed.
- **`OcrQueue`:** a `Channel<Guid>` consumed by `LibraryMaintenanceService`, a hosted `BackgroundService` that also runs reconciliation.
  - New image captures and re-exports are queued.
  - On startup, every image capture whose `OcrText` is null is queued.
  - Each item OCRs the export (what Matt sees, including annotations), then calls `SetOcrTextAsync`. Failures are logged and skipped, so a single bad image can't stop the queue.
  - Captures with no text get an empty string, so they aren't retried.

## Reconciliation (Library)

`SqliteCaptureStore.ReconcileAsync` runs once at startup, from `LibraryMaintenanceService`, before OCR backfill. All catalog writes share one in-process write gate, and saving and reindexing happen in one transaction. It handles three cases:

- **Export missing, original present:** re-export from the original and annotations.
- **Export and original both missing:** the row moves to the Trash, logged as "missing on disk".
- **A PNG or JPG under the library root that no row references:** it is imported as a new capture. The file is copied into `originals\`, so it is editable, and the row points at the file where it is. `CreatedUtc` comes from the file's last-write time, and the source is "imported".

It stops scanning after 5,000 imports in a single start, as a safety limit.

## Grab Text (App)

- **`Ctrl+Alt+PrtSc`**, or tray → Grab text, opens the same freeze-and-select overlay.
- The selection is OCR'd and the text goes on the clipboard. A tray notification shows the first line and the line count.
- Nothing is saved to the library.
- If no text is found, the notification says so and the clipboard is left untouched.

## Library window (App)

- **Search box** at the top:
  - Results refresh 250 ms after typing stops.
  - Esc clears it.
  - A hint lists the filter tokens.
  - The match count replaces the capture count.
- **Detail pane** on the right, for a single selection:
  - Editable: title (saved on Enter or when focus leaves) and description (multi-line, saved when focus leaves).
  - Tags: chips with a remove ×, a color dot that cycles through the palette when clicked, and an add box with autocomplete from existing tags (Enter or comma adds).
  - Read-only: source app and window title, dimensions, date, file path, and the OCR text in a collapsed expander.
- **Multi-select** (Ctrl and Shift): the pane shows the count, plus "Add tags to all" and "Move all to Trash".
- **Trash toggle:** shows trashed captures with Restore, and Empty Trash with a confirmation.
- **Quick bar:** gains a tag box. Enter adds the tags, without closing the bar or stopping its timer while focused.

## Out of scope

Video and scrolling captures in the library (Phases 4 and 5). Tag hierarchy (Decided: flat). Languages other than English. Transcripts.

## Done when

Builds clean, all tests pass (including parser, FTS ranking and prefix, tags, trash, reconciliation, and OCR tiling math), CI is green, and the Awaiting Matt checks are listed in `task.md`.
