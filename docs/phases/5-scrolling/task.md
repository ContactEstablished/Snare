# Phase 5 tasks

See [spec.md](spec.md).

## Tasks

- [x] Capture: `ScrollStitcher` with OpenCvSharp4; synthetic fixture tests
- [x] Recorded fixtures from headless Edge (`tools/scroll-fixture/record.mjs`) and their test
- [x] Capture: `ScrollCaptureSession` (manual and automatic) with `IScrollDriver`; plus tests
- [x] Interop: `ScrollTarget` (UI Automation scroll pattern, mouse-wheel fallback)
- [x] Library: `SaveImageAsync` with a kind; tall thumbnails; plus tests
- [x] App: `Ctrl+Shift+PrtSc`, tray entry, scroll panel with live preview, area border, save path shared with stills; tall images fit width in the editor; plus tests
- [x] Code review, merge, push

## Checked by the agent (Oct 29, 2026)

- The recorded headless-Edge sequence (27 frames, sticky header, fixed footer, a pause and a scroll back up) stitches byte-for-byte at the browser's own scroll positions.
- Snare starts with the scrolling service resolved and `Ctrl+Shift+PrtSc` registered.
- Not run: anything that scrolls or grabs Matt's real screen. That's below.

## Known gaps

- Grabs leave out the mouse pointer.
- Lazy-loaded images, video and animations can make a frame fail to line up. The frame is dropped with a warning, and scrolling back a little recovers.
- A sticky element that changes size while scrolling (a header that shrinks) can leave a seam.
- The wheel fallback assumes about 100 px per notch (Chrome and Edge at 100% scaling). Other apps may scroll further per notch, so automatic mode is only aimed at the browsers.
- Rows are committed from the frame where they first appear at the bottom of the view. A row still repainting there (a late-loading image) is kept as first seen.
- Horizontal scrolling and stitching across monitors are out of scope.

## Awaiting Matt

- [ ] **UI Automation spike (Chrome, then Edge):**
  - Open a long page.
  - Run `tests\Snare.Interop.Tests\bin\x64\Debug\net10.0-windows10.0.22621.0\Snare.Interop.Tests.exe -explicit only -method "*ScrollTarget*" -diagnostics`, and within 5 seconds rest the mouse over the page.
  - The page scrolls three steps. Note the printed method in each browser:
    - "scroll pattern" means UI Automation drives it;
    - "mouse wheel" means the fallback did. That still works, but tell the agent which one.
- [ ] **Manual mode, sticky header:**
  - Open a long article with a sticky header in Chrome (a learn.microsoft.com docs page is a good one).
  - Press `Ctrl+Shift+PrtSc` and drag over the page content, header included.
  - Press **Start**, scroll down with the wheel at a normal pace for a few screens, then press **Stop**.
  - The result shows:
    - the header once at the top;
    - no repeated or missing lines;
    - the footer, if any, once at the bottom.

  The quick bar (or editor) appears as for a still.
- [ ] **Panel and frame:**
  - The panel sits beside the area on the same monitor, and on the upper monitor when the area is there.
  - The blue frame sits just outside the area.
  - Neither appears in the result.
  - The preview grows while scrolling, and the height counts up.
- [ ] **Too fast, then recovery:** flick the wheel hard. A warning appears ("Couldn't line up that frame…"). Scroll back up a little, then down again slowly. It carries on with no gap in the result.
- [ ] **Automatic mode:**
  - In Chrome, `Ctrl+Shift+PrtSc`, click the page (window capture), then **Auto-scroll**. It scrolls to the end and saves by itself; the browser's toolbar appears once at the top.
  - Repeat in Edge.
  - Press **Stop** partway on another run: it saves what it has.
- [ ] **VS Code, manual:** a long file in VS Code stitches correctly in manual mode.
- [ ] **Cancel:**
  - **Cancel** saves nothing.
  - `Ctrl+Shift+PrtSc` before pressing Start also cancels.
  - `Ctrl+Shift+PrtSc` while capturing stops and saves.
- [ ] **Height cap:** on a very long page, automatic mode stops at 30,000 px with "Reached the height limit." and saves.
- [ ] **Library and editor:**
  - The tile thumbnail shows the top of the page.
  - `kind:scrolling` finds it.
  - Searching a word from far down the page finds it, once OCR has run.
  - The editor opens it fitted to the window's width at the top; the wheel scrolls down it.
- [ ] **Pointer flicker:** while a manual capture runs, the mouse pointer doesn't flicker. If it does, tell the agent: scrolling grabs can skip CAPTUREBLT, since the panel and frame are already excluded from capture.
- [ ] **Focus stays in the page:** after clicking Start, the arrow keys and Page Down still scroll the page.
- [ ] **Sidebars:** a window capture of an app with a fixed sidebar (Outlook, Teams or VS Code) stitches in manual mode.
- [ ] **`A` is refused:** `Ctrl+Shift+PrtSc`, then `A`, shows a "covers one monitor" notification.
