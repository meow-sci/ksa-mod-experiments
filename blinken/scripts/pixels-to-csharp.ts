/**
 * Converts a JSON file containing an array of {x, y} objects
 * into a C# (int x, int y)[] initializer for LcdAnimationPixels.Pixels.
 *
 * Usage: bun run pixels-to-csharp.ts <input.json>
 *
 * Input format: [{"x": 0, "y": 0}, {"x": 1, "y": 2}, ...]
 */

const path = process.argv[2];
if (!path) {
  console.error("Usage: bun run pixels-to-csharp.ts <input.json>");
  process.exit(1);
}

const text = await Bun.file(path).text();
const data: { x: number; y: number }[] = JSON.parse(text);

if (!Array.isArray(data)) {
  console.error("Error: expected a JSON array of {x, y} objects");
  process.exit(1);
}

const lines: string[] = [];
for (const { x, y } of data) {
  lines.push(`    (${x}, ${y}),`);
}

console.log(`new (int x, int y)[]`);
console.log(`{`);
console.log(lines.join("\n"));
console.log(`}`);
