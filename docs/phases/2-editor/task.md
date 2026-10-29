# Phase 2 tasks

See [spec.md](spec.md).

## Tasks

- [x] Core annotation model: new records and options, `CanvasInfo.Resize`, JSON round-trip tests
- [x] Rendering: `Draw`/`Render` for every tool with crop and resize; golden-image tests (12 goldens, reviewed by eye); redaction proof
- [x] Editing: `EditorSession` (tools, selection, handles, commands, undo/redo, steps, eyedropper, crop, styles), `AnnotationGeometry`, plus tests
- [x] Library: `LoadOriginalAsync`, `SaveAnnotationsAsync`, `ReexportAsync`, `DiscardOriginalAsync`, `ExportEncoder`, plus tests (including "discard leaves no secret pixels")
- [x] App: editor window (canvas, toolbar, zoom and pan, text editing, autosave, save, Save As, copy, drag-out, discard original), plus off-screen smoke tests
- [x] App: open from the quick bar (Edit), the library (double-click, Enter, context menu Edit) and `AfterCapture: "Editor"`; persisted tool styles
- [x] Code review, merge, push

## Not in this phase

- **Resize** is supported in the model, renderer, session and store, but the editor has no size dialog yet. Phase 6's settings and polish work adds the UI.

## Awaiting Matt

Capture something first (Phase 1 checks), then open it with the quick bar's **Edit**, or by double-clicking it in the library.

- [ ] **Every Must tool:** draw one of each and check it looks right:
  - Arrow (A): try Open head as well.
  - Rectangle (R), with Fill on and off.
  - Ellipse (E).
  - Highlighter (H) over text: the text stays readable. Shift keeps it straight.
  - Pen (P).
  - Text (T): click, type two lines, then click away. Try Bold and Background.
  - Redact (D), Blur (B) and Pixelate (X): adjust Strength on the last two. The orange "obscure, not secure" banner appears for Blur and Pixelate.
- [ ] **Should tools:**
  - Line (L), with Dashed.
  - Step (N): click three badges, delete the first, and the rest renumber 1, 2.
  - Eyedropper (I) from the Arrow tool: the arrow color changes to the picked pixel, and the color shows under Recent.
- [ ] **Select tool (V):**
  - Click an object, then drag it to move.
  - Resize a rectangle by a corner and an edge, and drag an arrow's endpoints.
  - Shift+click two objects; drag a marquee.
  - Delete; Ctrl+A.
  - Ctrl+] and Ctrl+[ change the stacking order (with Shift: to the front or back).
  - Arrow keys nudge 1 px; Shift+arrows nudge 10 px.
  - With an object selected, change its color or size in the panel: the object changes.
- [ ] **Undo and redo:** Ctrl+Z and Ctrl+Y (or Ctrl+Shift+Z) step back and forth through every action above, including moves, restyles, crop and text edits.
- [ ] **Crop (C):** drag a frame, press Enter: outside is shaded. Save. The library image is cropped. Remove crop brings the rest back on the next save.
- [ ] **Zoom and pan:**
  - Ctrl+wheel zooms around the pointer.
  - The wheel scrolls; Shift+wheel scrolls sideways.
  - Space+drag (or middle-drag) pans.
  - Ctrl+0 fits the image and Ctrl+1 shows it at 100%.
  - Drawing stays accurate at 25% and 400%.
- [ ] **Saving:**
  - Status shows "Edited", then "Edits kept · Save to update the image" about 1.5 s later.
  - Ctrl+S updates the PNG in `Pictures\Snare\…` and the library thumbnail.
  - Closing the window with unsaved edits saves them.
  - Reopening the capture shows every annotation, still editable.
- [ ] **What you see is what you get:** open the exported PNG next to the editor at 100%. They match: same positions, colors, blur, text.
- [ ] **Copy, Save As, Drag:**
  - Ctrl+C or Copy, then paste into Paint: you get the flattened image.
  - Save As… writes a PNG or a JPG.
  - Dragging "⇲ Drag" into a Slack message or an Explorer folder drops the image file.
- [ ] **Discard original:**
  - After a solid Redact, the blue bar offers Discard original. Confirm it.
  - The editor closes, `%LOCALAPPDATA%\Snare\originals\<id>.png` is gone, and the export has the black box.
  - Opening the capture again says it can't be edited.
- [ ] **Open editor after capture:** set `"AfterCapture": "Editor"` in `appsettings.json`, restart, and capture. The editor opens instead of the quick bar.
- [ ] **Remembered styles:** change the arrow color and size, close the editor, restart Snare. A new arrow uses the same style.
- [ ] **Smoothness on a big capture:** capture both monitors (`A`, 5120×2880), open it, and drag an arrow around at fit zoom. If it feels sluggish, note it here; the fix is switching the canvas to `SKGLElement` (GPU).

## Known gaps (from review, deferred)

- Tray → Exit closes editors without saving their last 1.5 s of edits (`Application.Shutdown` ignores `Closing` cancellation). Phase 6 adds an exit flush.
- Dragging a style slider with a selection records one undo step per tick.
- Text uses Skia without font fallback, so emoji and CJK characters render as boxes in the export.
- `EditorStyleStore` reads and writes its small JSON file on the UI thread.
