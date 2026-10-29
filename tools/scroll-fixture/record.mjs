// Records a scrolling-capture fixture: headless Edge (or Chrome) loads page.html, scrolls it
// through a script of positions, and saves a screenshot per step plus frames.json with the
// real scrollY of each. Everything runs offscreen; nothing touches the desktop.
//
//   node tools/scroll-fixture/record.mjs [outDir] [browserExe]
//
// Default outDir: tests/Snare.Capture.Tests/Fixtures/Scrolling/docs-page
import { spawn } from "node:child_process";
import { mkdirSync, mkdtempSync, writeFileSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const outDir = resolve(process.argv[2] ?? join(here, "../../tests/Snare.Capture.Tests/Fixtures/Scrolling/docs-page"));
const browser = process.argv[3] ?? [
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  "C:/Program Files/Google/Chrome/Application/chrome.exe",
].find(existsSync);
const width = 640;
const height = 480;
const port = 9300 + Math.floor(Math.random() * 500);

// Down in uneven steps, a pause (same position twice), a scroll back up, then on to the end in 280 px steps.
const script = [0, 90, 250, 250, 410, 330, 410, 600, 760, 980, 1150, 1320, 1500, 1700, 1880, 2050, 2240, 2420, 2600];

const profile = mkdtempSync(join(tmpdir(), "snare-fixture-"));
const child = spawn(browser, [
  "--headless=new", `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
  "--hide-scrollbars", "--force-device-scale-factor=1", "--disable-gpu", "--no-first-run", "about:blank",
], { stdio: "ignore" });

const sleep = ms => new Promise(r => setTimeout(r, ms));
let targets;
for (let i = 0; i < 50 && !targets; i++) {
  try { targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); } catch { await sleep(200); }
}
const page = targets.find(t => t.type === "page");
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise(r => ws.addEventListener("open", r, { once: true }));
let nextId = 1;
const pending = new Map();
ws.addEventListener("message", e => {
  const msg = JSON.parse(e.data);
  if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); }
});
const send = (method, params = {}) => new Promise((ok, fail) => {
  const id = nextId++;
  pending.set(id, msg => msg.error ? fail(new Error(`${method}: ${msg.error.message}`)) : ok(msg.result));
  ws.send(JSON.stringify({ id, method, params }));
});
const evaluate = async expression => (await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true })).result.value;

try {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false });
  await send("Page.navigate", { url: pathToFileURL(join(here, "page.html")).href });
  for (let i = 0; i < 50 && (await evaluate("document.readyState")) !== "complete"; i++) await sleep(100);
  await evaluate("document.fonts.ready.then(() => true)");

  mkdirSync(outDir, { recursive: true });
  const frames = [];
  for (let i = 0; ; i++) {
    const target = i < script.length ? script[i] : frames.at(-1).scrollY + 280;
    const y = await evaluate(`new Promise(r => { window.scrollTo(0, ${target}); requestAnimationFrame(() => requestAnimationFrame(() => r(window.scrollY))); })`);
    if (i >= script.length && y === frames.at(-1).scrollY) break; // the end: nothing moved
    const shot = await send("Page.captureScreenshot", { format: "png" });
    const file = `frame-${String(i).padStart(2, "0")}.png`;
    writeFileSync(join(outDir, file), Buffer.from(shot.data, "base64"));
    frames.push({ file, scrollY: y });
  }

  const header = await evaluate("document.querySelector('header').getBoundingClientRect().height");
  const footer = await evaluate("document.querySelector('footer').getBoundingClientRect().height");
  writeFileSync(join(outDir, "frames.json"), JSON.stringify({
    source: "tools/scroll-fixture/page.html in headless " + (browser.includes("msedge") ? "Edge" : "Chrome"),
    width, height, header, footer, frames,
  }, null, 2) + "\n");
  console.log(`Wrote ${frames.length} frames to ${outDir}; last scrollY ${frames.at(-1).scrollY}`);
} finally {
  ws.close();
  child.kill();
}
