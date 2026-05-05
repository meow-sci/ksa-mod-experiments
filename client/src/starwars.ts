#!/usr/bin/env bun
/**
 * starwars.ts — Star Wars style scrolling intro text on an its-so-shiny light grid.
 *
 * Usage:
 *   bun run starwars.ts -v <vehicleId> -g <gridName> \
 *     -W <gridWidth> -H <gridHeight> \
 *     --title-font <px> --body-font <px> \
 *     --title <text> --text <body> \
 *     [-s <speed>]
 *
 * The title scrolls first, followed by a blank gap equal to the title font
 * height, then the body text. Word-wrapping respects the pixel width of the
 * grid. Explicit \n chars in --title or --text produce a vertical blank gap.
 *
 * Speed is in pixels per second (default 5.0). The grid is cleared when the
 * animation completes or the process is interrupted (Ctrl-C).
 */

import { parseArgs } from "util";
import { snapHeight, getFontConfig } from "./starwars-fonts";
import {
  wrapText,
  renderBlock,
  buildScrollBitmap,
  extractFramePixels,
} from "./starwars-layout";
import { setPixels, clearGrid } from "./starwars-api";

// ─── Argument parsing ────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run starwars.ts [options]");
  console.error("  -v, --vehicle      Vehicle ID (required)");
  console.error("  -g, --grid         Grid name (required)");
  console.error("  -W, --grid-width   Grid width in pixels (required)");
  console.error("  -H, --grid-height  Grid height in pixels (required)");
  console.error("  --title-font       Title font height in pixels (required)");
  console.error("  --body-font        Body font height in pixels (required)");
  console.error("  --title            Title text (required)");
  console.error("  --text             Body text (required)");
  console.error("  -s, --speed        Scroll speed px/sec (default: 5.0)");
  process.exit(1);
}

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    vehicle:      { type: "string", short: "v" },
    grid:         { type: "string", short: "g" },
    "grid-width":  { type: "string", short: "W" },
    "grid-height": { type: "string", short: "H" },
    "title-font":  { type: "string" },
    "body-font":   { type: "string" },
    title:        { type: "string" },
    text:         { type: "string" },
    speed:        { type: "string", short: "s" },
  },
  strict: true,
  allowPositionals: false,
});

const vehicleId = args.vehicle;
const gridName  = args.grid;
const rawW      = args["grid-width"];
const rawH      = args["grid-height"];
const rawTFont  = args["title-font"];
const rawBFont  = args["body-font"];
const titleText = args.title;
const bodyText  = args.text;
const rawSpeed  = args.speed;

if (
  !vehicleId || !gridName || !rawW || !rawH ||
  !rawTFont || !rawBFont ||
  titleText === undefined || bodyText === undefined
) {
  usage();
}

const gridW = parseInt(rawW!, 10);
const gridH = parseInt(rawH!, 10);
if (isNaN(gridW) || gridW < 1) { console.error("Invalid --grid-width"); usage(); }
if (isNaN(gridH) || gridH < 1) { console.error("Invalid --grid-height"); usage(); }

const titleFontSize = snapHeight(parseInt(rawTFont!, 10));
const bodyFontSize  = snapHeight(parseInt(rawBFont!, 10));
const titleFont = getFontConfig(titleFontSize);
const bodyFont  = getFontConfig(bodyFontSize);

const speed = rawSpeed !== undefined ? parseFloat(rawSpeed) : 5.0;
if (isNaN(speed) || speed <= 0) { console.error("Invalid --speed"); usage(); }

// ─── Build scroll bitmap ─────────────────────────────────────────────────────

const titleLines  = wrapText(titleText!, gridW, titleFont.fontWidth);
const titleBitmap = renderBlock(
  titleLines, titleFont.map, titleFont.fontWidth, titleFont.fontHeight, gridW
);

const bodyLines  = wrapText(bodyText!, gridW, bodyFont.fontWidth);
const bodyBitmap = renderBlock(
  bodyLines, bodyFont.map, bodyFont.fontWidth, bodyFont.fontHeight, gridW
);

// Gap between title and body = one title-font-height of blank rows
const bitmap  = buildScrollBitmap(titleBitmap, titleFont.fontHeight, bodyBitmap, gridW);
const bitmapH = bitmap.length;

// Total rows to scroll: content enters from below and exits from above
const totalScrollRows = gridH + bitmapH;
const durationSec     = totalScrollRows / speed;

console.error(
  `Star Wars scroll: grid ${gridW}×${gridH}, ` +
  `title font ${titleFontSize}px (${titleLines.length} lines), ` +
  `body font ${bodyFontSize}px (${bodyLines.length} lines), ` +
  `bitmap ${bitmapH}px tall, speed ${speed}px/s`
);
console.error(
  `Scroll distance: ${totalScrollRows}px — estimated duration ${durationSec.toFixed(1)}s`
);

// ─── Animation loop ──────────────────────────────────────────────────────────

async function main() {
  let interrupted = false;

  // Handle Ctrl-C: do cleanup then force-exit.
  // Setting the flag alone isn't enough — a pending fetch() won't yield to it.
  process.on("SIGINT", () => {
    if (interrupted) return; // second Ctrl-C: give up immediately
    interrupted = true;
    console.error("\nInterrupted — clearing grid...");
    clearGrid(vehicleId!, gridName!)
      .catch(() => {})
      .finally(() => process.exit(0));
  });

  const startMs  = performance.now();
  let lastScrollY = -1;

  while (!interrupted) {
    const elapsedSec = (performance.now() - startMs) / 1000;
    const scrollY    = Math.floor(elapsedSec * speed);

    if (scrollY >= totalScrollRows) break;

    if (scrollY !== lastScrollY) {
      lastScrollY = scrollY;
      const pixels = extractFramePixels(bitmap, gridW, gridH, scrollY);
      try {
        await setPixels(vehicleId!, gridName!, pixels);
      } catch (err) {
        console.error("Error updating pixels:", err);
        break;
      }
    }

    // Poll every 10ms; only sends a new frame when the integer pixel offset changes
    await Bun.sleep(10);
  }

  console.error("Done — clearing grid...");
  try {
    await clearGrid(vehicleId!, gridName!);
  } catch (err) {
    console.error("Error clearing grid:", err);
  }
}

main().catch(err => {
  console.error("Fatal:", err);
  process.exit(1);
});
