#!/usr/bin/env bun
/**
 * img2tty.ts — Convert a raster image to a B&W LCD pixel grid display.
 *
 * Sharp decodes and letterbox-resizes the image to grayscale at grid
 * dimensions. OpenCV WASM then applies Canny edge detection (or simple /
 * adaptive threshold) and the result is blitted to an its-so-shiny light grid.
 *
 * Usage:
 *   bun run img2tty.ts -i <image> -v <vehicleId> -g <gridName> \
 *     -W <gridWidth> -H <gridHeight> [options]
 *
 * After blitting the image exits immediately — the image stays on the grid.
 */

import { parseArgs } from "util";
import cv, { type Mat } from "@techstark/opencv-js";
import sharp from "sharp";
import { setPixels } from "./starwars-api";

// ─── OpenCV WASM init ────────────────────────────────────────────────────────

function waitForCV(): Promise<void> {
  return new Promise<void>((resolve) => {
    // cv.Mat is defined once the WASM module has finished initialising
    if (typeof cv.Mat !== "undefined") {
      resolve();
    } else {
      (cv as any).onRuntimeInitialized = resolve;
    }
  });
}

// ─── CLI args ────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run img2tty.ts -i <image> -v <vehicle> -g <grid> -W <width> -H <height> [options]");
  console.error("");
  console.error("Required:");
  console.error("  -i, --image          Input image file (PNG/JPG/GIF)");
  console.error("  -v, --vehicle        Vehicle ID");
  console.error("  -g, --grid           Grid name");
  console.error("  -W, --grid-width     Grid width in pixels");
  console.error("  -H, --grid-height    Grid height in pixels");
  console.error("");
  console.error("Processing mode (default: canny):");
  console.error("  --mode               canny | threshold | adaptive");
  console.error("");
  console.error("Canny options (--mode canny):");
  console.error("  --low                Low threshold  (default: 50)");
  console.error("  --high               High threshold (default: 150)");
  console.error("  --aperture           Sobel aperture: 3|5|7 (default: 3)");
  console.error("  --blur               Gaussian blur kernel size before Canny, 0=off (default: 3)");
  console.error("");
  console.error("Threshold options (--mode threshold):");
  console.error("  --thresh             Threshold value 0–255 (default: 128)");
  console.error("  --otsu               Use Otsu auto-threshold (ignores --thresh)");
  console.error("");
  console.error("Adaptive threshold options (--mode adaptive):");
  console.error("  --block-size         Neighbourhood block size, odd number (default: 11)");
  console.error("  --C                  Constant subtracted from mean (default: 2)");
  console.error("");
  console.error("Output:");
  console.error("  --invert             Swap on/off pixels in the output");
  console.error("  --flip               Flip the image vertically (Y axis) before processing");
  process.exit(1);
}

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    image:        { type: "string",  short: "i" },
    vehicle:      { type: "string",  short: "v" },
    grid:         { type: "string",  short: "g" },
    "grid-width":  { type: "string",  short: "W" },
    "grid-height": { type: "string",  short: "H" },
    mode:         { type: "string" },
    low:          { type: "string" },
    high:         { type: "string" },
    aperture:     { type: "string" },
    blur:         { type: "string" },
    thresh:       { type: "string" },
    otsu:         { type: "boolean" },
    "block-size": { type: "string" },
    C:            { type: "string" },
    invert:       { type: "boolean" },
    flip:         { type: "boolean" },
  },
  strict: true,
  allowPositionals: false,
});

if (!args.image || !args.vehicle || !args.grid || !args["grid-width"] || !args["grid-height"]) {
  usage();
}

const imagePath = args.image!;
const vehicleId = args.vehicle!;
const gridName  = args.grid!;
const gridW     = parseInt(args["grid-width"]!,  10);
const gridH     = parseInt(args["grid-height"]!, 10);

if (isNaN(gridW) || gridW < 1) { console.error("Invalid --grid-width");  usage(); }
if (isNaN(gridH) || gridH < 1) { console.error("Invalid --grid-height"); usage(); }

const mode      = args.mode      ?? "canny";
const lowThresh = parseFloat(args.low    ?? "50");
const hiThresh  = parseFloat(args.high   ?? "150");
const aperture  = parseInt(args.aperture ?? "3",  10);
const blurSize  = parseInt(args.blur     ?? "3",  10);
const threshVal = parseFloat(args.thresh  ?? "128");
const useOtsu   = args.otsu ?? false;
const blockSize = parseInt(args["block-size"] ?? "11", 10);
const adaptiveC = parseFloat(args.C ?? "2");
const invert    = args.invert ?? false;
const flip      = args.flip   ?? false;

if (!["canny", "threshold", "adaptive"].includes(mode)) {
  console.error(`Unknown --mode "${mode}". Must be: canny | threshold | adaptive`);
  usage();
}
if (![3, 5, 7].includes(aperture)) {
  console.error("--aperture must be 3, 5, or 7");
  usage();
}

// ─── Image loading via Sharp ─────────────────────────────────────────────────

/**
 * Decode the input image, letterbox-resize it to gridW×gridH preserving
 * aspect ratio (black bars fill unused space), and return raw grayscale bytes.
 */
async function loadGrayscale(): Promise<{ data: Buffer; srcDesc: string }> {
  const image    = sharp(imagePath);
  const metadata = await image.metadata();
  const srcDesc  = `${metadata.width ?? "?"}×${metadata.height ?? "?"}`;

  const { data } = await image
    .resize(gridW, gridH, { fit: "contain", background: { r: 0, g: 0, b: 0 } })
    .flip(flip)
    .grayscale()
    .raw()
    .toBuffer({ resolveWithObject: true });

  return { data, srcDesc };
}

// ─── OpenCV processing pipeline ──────────────────────────────────────────────

/** Run the selected algorithm on a CV_8UC1 grayscale Mat; returns a binary Mat (255=on, 0=off). */
function applyProcessing(gray: Mat): Mat {
  if (mode === "canny") {
    let src    = gray;
    let blurred: Mat | null = null;

    if (blurSize >= 3) {
      const ksize = blurSize % 2 === 0 ? blurSize + 1 : blurSize;
      blurred = new cv.Mat();
      cv.GaussianBlur(gray, blurred, new cv.Size(ksize, ksize), 0);
      src = blurred;
    }

    const edges = new cv.Mat();
    cv.Canny(src, edges, lowThresh, hiThresh, aperture);
    blurred?.delete();
    return edges;
  }

  if (mode === "threshold") {
    const out       = new cv.Mat();
    const threshType = (useOtsu ? cv.THRESH_OTSU : 0) |
                       (invert  ? cv.THRESH_BINARY_INV : cv.THRESH_BINARY);
    cv.threshold(gray, out, threshVal, 255, threshType);
    return out;
  }

  // adaptive
  const out  = new cv.Mat();
  const bsz  = blockSize % 2 === 0 ? blockSize + 1 : blockSize;
  const ttype = invert ? cv.THRESH_BINARY_INV : cv.THRESH_BINARY;
  cv.adaptiveThreshold(gray, out, 255, cv.ADAPTIVE_THRESH_GAUSSIAN_C, ttype, bsz, adaptiveC);
  return out;
}

/** Extract {x,y} on-pixels from a binary CV_8U single-channel Mat. */
function extractOnPixels(mat: Mat, invertResult: boolean): Array<{ x: number; y: number }> {
  const pixels: Array<{ x: number; y: number }> = [];
  for (let y = 0; y < gridH; y++) {
    for (let x = 0; x < gridW; x++) {
      const val  = mat.ucharAt(y, x);
      const isOn = invertResult ? val === 0 : val > 0;
      if (isOn) pixels.push({ x, y });
    }
  }
  return pixels;
}

// ─── Main ────────────────────────────────────────────────────────────────────

async function main() {
  await waitForCV();

  const { data: grayBuf, srcDesc } = await loadGrayscale();
  console.error(`Loaded: ${imagePath} — original ${srcDesc}, resized to ${gridW}×${gridH} grayscale`);

  // Wrap the Sharp grayscale buffer in an OpenCV Mat (single-channel, no copy)
  const gray      = cv.matFromArray(gridH, gridW, cv.CV_8UC1, grayBuf);
  const processed = applyProcessing(gray);
  gray.delete();

  // For threshold/adaptive the invert flag is already baked into the threshold
  // type; for canny we apply it here when extracting pixels.
  const invertExtract = mode === "canny" ? invert : false;
  const pixels        = extractOnPixels(processed, invertExtract);
  processed.delete();

  console.error(
    `mode=${mode} | ${pixels.length} / ${gridW * gridH} pixels ON`
  );

  await setPixels(vehicleId, gridName, pixels);
  console.error("Done.");
}

main().catch(err => {
  console.error("Fatal:", err);
  process.exit(1);
});
