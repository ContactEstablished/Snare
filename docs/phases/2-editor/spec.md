# Phase 2: Editor

Cut from [the build plan](../../build-plan.md): Editor, Data and storage rules, and Roadmap → Phase 2. It builds on the Phase 1 library and its untouched originals.

## Goal

Any capture opens in an editor that draws vector annotations over its original. The editor has every Must tool plus Line, Step numbers, and Eyedropper with recent colors. It has unlimited undo, autosaves the annotation JSON, and re-exports a flattened PNG/JPG that matches the screen exactly. Redaction is provably destructive in the export. A capture's original can be discarded so its redactions become permanent.

## Annotation model (Core)

Every tool is a record deriving from `AnnotationObject`, registered with `[JsonDerivedType]`. Coordinates are original-image pixels. New optional parameters go at the end of each record, so schema version 1 JSON stays readable.

| Type | Record | Notes |
| --- | --- | --- |
| `arrow` | `ArrowAnnotation(Id, From, To, Stroke, StrokeWidth, HeadStyle = Filled, HeadSize = 4)` | `HeadSize` is a multiple of the stroke width |
| `line` | `LineAnnotation(Id, From, To, Stroke, StrokeWidth, Dashed = false)` | |
| `rectangle` | `RectangleAnnotation(Id, Bounds, Stroke, StrokeWidth, Fill = null, CornerRadius = 0)` | |
| `ellipse` | `EllipseAnnotation(Id, Bounds, Stroke, StrokeWidth, Fill = null)` | |
| `pen` | `PenAnnotation(Id, Points, Stroke, StrokeWidth)` | Smoothed with quadratic curves through midpoints |
| `highlighter` | `HighlighterAnnotation(Id, Points, Color, Width)` | Multiply blend at 40% opacity, so text underneath stays readable |
| `text` | `TextAnnotation(Id, Position, Text, Color, FontSize, FontFamily = "Segoe UI", Background = null, Bold = false)` | Multi-line; `Position` is the top-left |
| `redact` | `RedactAnnotation(Id, Bounds, Mode, Fill = "#000000", Strength = 0)` | `Solid` is secure. `Blur` (Gaussian sigma) and `Pixelate` (block size) are labeled *obscure*, not *secure*. A `Strength` of 0 means the default (12). |
| `step` | `StepAnnotation(Id, Center, Fill, Radius = 18)` | The number is derived, not stored: the badge's position among step objects in z-order. Deleting one renumbers the rest. |

`CanvasInfo(Width, Height, Crop = null, Resize = null)`:
- `Crop` is a rectangle in original pixels.
- `Resize` is the final output size after the crop.
- Both are applied only at export. The editor shows the crop as a frame.

Records with point lists override equality so that round-trip tests compare by value.

## Rendering

`AnnotationRenderer` has two jobs:

- **`Draw(SKCanvas, SKImage original, AnnotationDocument)`** paints the original and every object in z-order. Coordinates are original pixels, and the caller sets the transform. The editor uses this for preview, and export uses it too, so preview and export can't drift apart.
- **`Render(SKBitmap original, AnnotationDocument)`** produces the flattened export bitmap. It applies `Crop` and then `Resize`.

How each kind of object is drawn:

- **Blur and pixelate** act on the original pixels inside their bounds, not on annotations beneath them. That keeps them independent of draw order, and it is the honest description of what they hide.
- **Solid redaction** fills its bounds with an opaque color, with antialiasing off. An exported redacted area therefore contains only the fill color, and a test proves it.
- **Step badges** are filled circles with a centered white number.
- **Text** is drawn with `SKFont`, with optional padded background.

**Golden-image tests** cover each tool. Every tool's JSON is rendered over a generated 320×200 base image and compared with a checked-in PNG in `tests/Snare.Rendering.Tests/Golden/`. Up to 0.5% of pixels may differ by more than 3 per channel. A mismatch writes `*.received.png`, which is gitignored.

## Editing logic (`Snare.Rendering/Editing`)

`EditorSession` holds everything the editor does, with no WPF:

- The canvas and object list, a selection, the current tool, per-tool styles, recent colors (the last 8), and the undo and redo stacks.
- **Commands** (`IEditCommand`): add, remove, replace (move, resize, restyle, edit text), reorder, and change canvas (crop, resize). Each command has `Apply` and `Revert`, so undo and redo are unlimited within a session.
- **Pointer input in image coordinates:** `PointerDown`, `PointerMove` and `PointerUp`, with Shift and Ctrl modifiers.
  - Shape tools create objects by dragging. Shift constrains to a square or circle, or to a 45° step for lines.
  - Pen and highlighter collect points.
  - Text and Step create an object with a click. Text then asks the view to open an inline editor.
  - Eyedropper samples the original's pixel under the pointer, sets the current tool's color, and adds it to the recent colors.
  - Crop draws a crop frame, and Enter applies it.
- **Select tool:**
  - Click hits the topmost object; Shift+click toggles it in the selection; dragging empty space draws a marquee.
  - Dragging the selection moves it. Handles resize a single box-like object; lines and arrows have endpoint handles.
  - Delete removes the selection, and Ctrl+A selects everything.
  - Z-order commands: bring to front, bring forward, send backward, send to back.
- **Hit testing** (`AnnotationGeometry`) uses Skia paths widened by the stroke plus a tolerance. Text bounds come from `SKFont` measurement.
- **Single-letter tool shortcuts:**

  | Key | Tool | Key | Tool |
  | --- | --- | --- | --- |
  | V | Select | T | Text |
  | A | Arrow | D | Redact |
  | L | Line | B | Blur |
  | R | Rectangle | X | Pixelate |
  | E | Ellipse | N | Step |
  | H | Highlighter | I | Eyedropper |
  | P | Pen | C | Crop |

- **Styles:** each tool remembers its last style. Styles are persisted to `%LOCALAPPDATA%\Snare\editor-styles.json`. There are no named presets.

## Library

`ICaptureStore` gains:

| Method | What it does |
| --- | --- |
| `LoadOriginalAsync(id)` | Returns the original pixels, or the export's pixels if the original was discarded |
| `SaveAnnotationsAsync(id, document)` | Writes `AnnotationJson` through `AnnotationJson`, and updates `ModifiedUtc` |
| `ReexportAsync(id, document)` | Renders, overwrites the export in place (same path and format), rewrites the thumbnail, and updates width, height and JSON |
| `DiscardOriginalAsync(id)` | Re-exports, deletes the original file, and clears `OriginalPath` and `AnnotationJson`. The capture can no longer be re-edited, and the export becomes its only copy. |

`ExportEncoder` (Library) turns an original and a document into encoded PNG or JPG bytes. Both the store and Save As use it.

## Editor window (App)

- **Layout:**
  - **Top toolbar:** tool toggles with their shortcut letters, color swatches plus recent colors, stroke width, fill on/off, font size, blur and pixelate strength, and arrow head style.
  - **Right of the toolbar:** Undo, Redo, Copy, Save, Save As, Drag-out handle and Discard original.
- **Canvas:**
  - An `SKElement` draws through `AnnotationRenderer.Draw` with the view transform, plus selection chrome: handles, marquee and crop frame.
  - Ctrl+wheel zooms around the pointer, the wheel scrolls, Space+drag pans, Ctrl+0 fits the image and Ctrl+1 shows it at 100%.
  - It opens at fit-to-window, never above 100%.
- **Text:** a borderless WPF `TextBox` sits over the text object while it is being edited. Ctrl+Enter, Esc or a click elsewhere commits the edit; empty text deletes the object.
- **Saving:**
  - The annotation JSON autosaves 1.5 s after the last change.
  - Save (Ctrl+S) and closing the window re-export the image if anything changed since the last export.
  - Save As writes a PNG or JPG copy anywhere.
  - Copy (Ctrl+C with nothing selected) puts the flattened image on the clipboard.
  - The drag-out handle starts a file drag of the export.
- **Redaction label:** when the capture has Blur or Pixelate objects, a banner says they *obscure* rather than *secure*.
- **Discard original:** when the capture has a solid Redact, the editor offers Discard original. It asks for confirmation first, because the action can't be undone.
- **Opening the editor:**
  - from the quick bar's new **Edit** button;
  - by double-clicking a library tile (the context menu gains Edit and Open in default app);
  - automatically after every capture, if `SnareOptions.AfterCapture` is `Editor` (the default is `QuickBar`).

## Out of scope

Callout (Phase 6). Magnify, spotlight and frame effects (after v1). Tiled rendering for very tall images (Phase 5, with scrolling capture). Curved arrows.

## Done when

Builds clean, all tests pass (including golden images and the redaction proof), CI is green, and the Awaiting Matt checks are listed in `task.md`.
