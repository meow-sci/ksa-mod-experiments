/**
 * This program takes in a SVG file which contains <circle> elements and converts them to a pixel grid representation.
 * 
 * It must analyze all the <circle> x,y coordinates and determine the minimum and maximum x and y values to establish the bounds of the grid
 * and use that data to figure out the x,y value for each pixel in the grid.
 * 
 * The input should be a SVG file path and output should be a JSON array of objects { x: number, y: number } representing the pixel grid coordinates.
 */

const svgPath = process.argv[2];

if (!svgPath) {
  console.error("Usage: bun dots-svg-to-pixel-grid.ts <path-to-svg>");
  process.exit(1);
}

const file = Bun.file(svgPath);
const svgContent = await file.text();

// Parse all <circle> elements and extract cx, cy
const circleRegex = /<circle\s[^>]*cx="([^"]+)"[^>]*cy="([^"]+)"[^>]*\/?>/g;
const rawPoints: { cx: number; cy: number }[] = [];

let match: RegExpExecArray | null;
while ((match = circleRegex.exec(svgContent)) !== null) {
  rawPoints.push({
    cx: parseFloat(match[1]!),
    cy: parseFloat(match[2]!),
  });
}

if (rawPoints.length === 0) {
  console.error("No <circle> elements found in the SVG.");
  process.exit(1);
}

// Determine min values for each axis
const minCx = Math.min(...rawPoints.map((p) => p.cx));
const minCy = Math.min(...rawPoints.map((p) => p.cy));

// Determine grid spacing by finding the smallest non-zero gap between sorted unique values on either axis
function findGridSpacing(values: number[]): number {
  const sorted = [...new Set(values)].sort((a, b) => a - b);
  let minGap = Infinity;
  for (let i = 1; i < sorted.length; i++) {
    const gap = sorted[i]! - sorted[i - 1]!;
    if (gap > 1e-6 && gap < minGap) {
      minGap = gap;
    }
  }
  return minGap;
}

const spacingX = findGridSpacing(rawPoints.map((p) => p.cx));
const spacingY = findGridSpacing(rawPoints.map((p) => p.cy));

// Convert each circle position to integer grid coordinates
const pixels = rawPoints.map((p) => ({
  x: Math.round((p.cx - minCx) / spacingX),
  y: Math.round((p.cy - minCy) / spacingY),
}));

console.log(JSON.stringify(pixels, null, 2));
