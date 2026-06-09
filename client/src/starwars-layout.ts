/**
 * starwars-layout.ts
 * Text wrapping, bitmap rendering, and frame extraction for Star Wars scrolling.
 */

import type { FontMap } from "./starwars-fonts";

/** 2D pixel bitmap: bitmap[row][col], row 0 = top, col 0 = left */
export type Bitmap = boolean[][];

/**
 * Word-wrap rawText to fit within pixelWidth pixels, splitting on whitespace.
 * Explicit \n chars in the input produce an empty string in the output array,
 * which renderBlock() treats as a fontHeight-tall blank vertical gap.
 */
export function wrapText(
  rawText: string,
  pixelWidth: number,
  fontWidth: number
): string[] {
  const charWidth = fontWidth + 1; // glyph width + 1 spacing column
  // Max chars per line: N chars occupy N*(fontWidth+1)-1 px; must fit in pixelWidth
  const maxChars = Math.floor((pixelWidth + 1) / charWidth);
  const result: string[] = [];

  const segments = rawText.split("\n");
  for (let i = 0; i < segments.length; i++) {
    if (i > 0) {
      // Explicit newline in the input → blank gap line
      result.push("");
    }
    const words = (segments[i] ?? "").split(/\s+/).filter(w => w.length > 0);
    if (words.length === 0) continue;

    let currentLine = "";
    for (const word of words) {
      if (currentLine === "") {
        currentLine = word;
      } else if (currentLine.length + 1 + word.length <= maxChars) {
        currentLine += " " + word;
      } else {
        result.push(currentLine);
        currentLine = word;
      }
    }
    if (currentLine !== "") result.push(currentLine);
  }

  return result;
}

/**
 * Render an array of text lines into a Bitmap.
 * - Empty string lines render as fontHeight blank rows (vertical gap).
 * - Non-empty lines are centered horizontally within bitmapWidth.
 * - lineGap blank rows are inserted between consecutive non-empty lines.
 */
export function renderBlock(
  lines: string[],
  fontMap: FontMap,
  fontWidth: number,
  fontHeight: number,
  bitmapWidth: number,
  lineGap: number = 2
): Bitmap {
  const bitmap: Bitmap = [];
  const charWidth = fontWidth + 1;
  let prevWasContent = false;

  for (const line of lines) {
    if (line === "") {
      // Explicit blank gap: one font-height worth of empty rows
      for (let r = 0; r < fontHeight; r++) {
        bitmap.push(new Array<boolean>(bitmapWidth).fill(false));
      }
      prevWasContent = false;
      continue;
    }

    // Insert inter-line gap between consecutive content lines
    if (prevWasContent) {
      for (let r = 0; r < lineGap; r++) {
        bitmap.push(new Array<boolean>(bitmapWidth).fill(false));
      }
    }

    // Center the line horizontally
    const linePixelWidth = line.length * charWidth - 1;
    const xOffset = Math.max(0, Math.floor((bitmapWidth - linePixelWidth) / 2));

    // Allocate fontHeight rows for this line
    const rows: boolean[][] = Array.from({ length: fontHeight }, () =>
      new Array<boolean>(bitmapWidth).fill(false)
    );

    let x = xOffset;
    for (const ch of line) {
      const glyph = fontMap[ch] ?? fontMap[" "]!;
      for (let r = 0; r < fontHeight; r++) {
        const rowStr = glyph[r] ?? "0".repeat(fontWidth);
        for (let c = 0; c < fontWidth; c++) {
          const px = x + c;
          if (px < bitmapWidth && rowStr[c] === "1") {
            rows[r]![px] = true;
          }
        }
      }
      x += charWidth;
    }

    for (const row of rows) bitmap.push(row);
    prevWasContent = true;
  }

  return bitmap;
}

/**
 * Combine title and body bitmaps with a blank gap between them.
 * The resulting bitmap is the full scroll strip from top to bottom.
 */
export function buildScrollBitmap(
  titleBitmap: Bitmap,
  gapHeight: number,
  bodyBitmap: Bitmap,
  bitmapWidth: number
): Bitmap {
  const blankRow = (): boolean[] => new Array<boolean>(bitmapWidth).fill(false);
  const gap: Bitmap = Array.from({ length: gapHeight }, blankRow);
  return [...titleBitmap, ...gap, ...bodyBitmap];
}

/**
 * Extract the lit pixels visible at the current scroll position.
 *
 * scrollY is the number of rows that have scrolled upward past the bottom
 * of the grid. At scrollY=0 the grid is empty; content enters from the
 * bottom as scrollY increases.
 *
 * Returns {x, y}[] in API coordinates where y=0 is the bottom of the grid.
 */
export function extractFramePixels(
  bitmap: Bitmap,
  gridW: number,
  gridH: number,
  scrollY: number
): Array<{ x: number; y: number }> {
  const pixels: Array<{ x: number; y: number }> = [];

  for (let r = 0; r < gridH; r++) {
    // Display row r (0=top) maps to this row in the bitmap
    const bitmapRow = scrollY - gridH + r;
    if (bitmapRow < 0 || bitmapRow >= bitmap.length) continue;

    const rowData = bitmap[bitmapRow];
    if (!rowData) continue;

    // Flip: top of grid → highest y in API
    const apiY = gridH - 1 - r;

    for (let c = 0; c < gridW; c++) {
      if (c < rowData.length && rowData[c]) {
        pixels.push({ x: c, y: apiY });
      }
    }
  }

  return pixels;
}
