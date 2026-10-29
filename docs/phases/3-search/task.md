# Phase 3 tasks

See [spec.md](spec.md).

## Tasks

- [x] Core: `SearchQueryParser`, `SearchQuery`, `TagPalette`, `IOcrEngine`, `ICaptureCatalog`, plus tests
- [x] Library: schema script 2 (FTS5, backfilled), tag mappings, details, tags, search with bm25 and filters, trash, OCR text, index maintenance, plus tests
- [x] Library: `WindowsOcrEngine` with tiling, `OcrQueue` and `LibraryMaintenanceService`, plus tests
- [x] Library: reconciliation (re-export, trash, import), plus tests
- [x] App: Grab Text (`Ctrl+Alt+PrtSc` and the tray)
- [x] App: library search box, detail pane, multi-select bulk actions, Trash view; quick bar tag box; plus view-model tests
- [ ] Code review, merge, push

## Checked by the agent on Matt's PC (Oct 29, 2026)

- Real Windows OCR (`Snare.Library.Tests.exe -explicit only -diagnostics`) read a rendered image exactly: "Invoice 1234 payment failed | Second line of text".
- The app starts and registers `Ctrl+Alt+PrtSc` for Grab Text, and the startup maintenance service runs without errors.

## Awaiting Matt

- [ ] **OCR in search:** take a capture of a web page or document, wait a few seconds, open the Library, and search for a word in the image. The capture appears. Select it and expand "Text in image" to see what was read.
- [ ] **Prefix and ranking:** search `invoice err`: captures containing "invoices" and "error" both match. A capture with the word in its title or tags ranks above one that only has it in the image text.
- [ ] **Filters:** try `tag:bug`, `app:chrome`, `kind:image`, `before:2026-10-29` and `after:2026-10-01`, alone and combined with words. Esc in the search box clears it, and Ctrl+F focuses it.
- [ ] **Detail pane:**
  - Edit the title (Enter saves) and the description (clicking away saves). The tile's caption shows the title.
  - Add tags by typing a name and pressing Enter, or with commas. Existing tags autocomplete.
  - Click a tag's dot to change its color, and × to remove it.
- [ ] **Multi-select:** Ctrl+click and Shift+click several tiles. "Add tags to all" tags them all, and "Move all to Trash" and Del trash them all.
- [ ] **Trash:** toggle Trash. Right-click → Restore from Trash brings a capture back. "Empty Trash…" asks for confirmation, then deletes the files from `Pictures\Snare` and `%LOCALAPPDATA%\Snare\originals` and `thumbs`.
- [ ] **Quick bar tag box:** after a capture, click the tag box, type `bug, ui` and press Enter. The bar says "Tagged bug, ui" and stays open while you type.
- [ ] **Grab Text:** press `Ctrl+Alt+PrtSc`, select a paragraph and paste into Notepad: you get the text. Nothing new appears in the Library. Selecting an empty area says "No text found".
- [ ] **Reconciliation:**
  - Quit Snare. Delete one export PNG from `Pictures\Snare\…`, and copy some other PNG into `Pictures\Snare\2026\10\`.
  - Start Snare and open the Library.
  - The deleted export is back, and the copied PNG appears with source "imported". It can be edited and is searchable by its file name.
