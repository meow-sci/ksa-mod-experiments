#!/usr/bin/env bun
/**
 * light-set.ts — Set color, intensity, and/or enabled state of a light part via Unladen Swallow RPC.
 *
 * Only provided fields are updated; omitted fields are left unchanged.
 * Provide --color-r/g/b OR --color-name (not both) to change the color.
 *
 * Usage:
 *   bun run light-set.ts -v <vehicleId> -p <partId> [options]
 *
 * Examples:
 *   bun run light-set.ts -v Rocket-001 -p light-nose-001 -r 1.0 -g 0.0 -b 0.5
 *   bun run light-set.ts -v Rocket-001 -p light-nose-001 --color-name NeonBlue
 *   bun run light-set.ts -v Rocket-001 -p light-nose-001 -i 0.75 --off
 */

import { parseArgs } from "util";
import type { ApiResponse, ZippoSetStateRequest, ZippoSetStateData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

function usage(): never {
  console.error("Usage: bun run light-set.ts -v <vehicleId> -p <partId> [options]");
  console.error("");
  console.error("Sets the color, intensity, and/or enabled state of a light part.");
  console.error("");
  console.error("Required:");
  console.error("  -v, --vehicle       Vehicle ID containing the light part");
  console.error("  -p, --part          Part ID to update");
  console.error("");
  console.error("Color (provide --color-r/g/b OR --color-name, not both):");
  console.error("  -r, --color-r       Red channel (0–1)");
  console.error("  -g, --color-g       Green channel (0–1)");
  console.error("  -b, --color-b       Blue channel (0–1)");
  console.error("      --color-name    XKCD color name (e.g. NeonBlue, HotPink)");
  console.error("");
  console.error("Other:");
  console.error("  -i, --intensity     Light brightness (0–1)");
  console.error("      --on            Turn light on");
  console.error("      --off           Turn light off");
  console.error("");
  console.error("Examples:");
  console.error("  bun run light-set.ts -v Rocket-001 -p light-nose-001 -r 1.0 -g 0.0 -b 0.5");
  console.error("  bun run light-set.ts -v Rocket-001 -p light-nose-001 --color-name NeonBlue");
  console.error("  bun run light-set.ts -v Rocket-001 -p light-nose-001 -i 0.75 --off");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    vehicle:      { type: "string",  short: "v" },
    part:         { type: "string",  short: "p" },
    "color-r":    { type: "string",  short: "r" },
    "color-g":    { type: "string",  short: "g" },
    "color-b":    { type: "string",  short: "b" },
    "color-name": { type: "string" },
    intensity:    { type: "string",  short: "i" },
    on:           { type: "boolean" },
    off:          { type: "boolean" },
    help:         { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.vehicle) { console.error("Missing required: --vehicle"); usage(); }
if (!args.part)    { console.error("Missing required: --part");    usage(); }

const hasRgb  = args["color-r"] !== undefined || args["color-g"] !== undefined || args["color-b"] !== undefined;
const hasName = args["color-name"] !== undefined;

if (hasRgb && hasName) {
  console.error("Provide either --color-r/g/b or --color-name, not both.");
  usage();
}

if (args.on && args.off) {
  console.error("Provide either --on or --off, not both.");
  usage();
}

// ─────────────────────────────────────────────────────────────────────────────
// Build request body
// ─────────────────────────────────────────────────────────────────────────────

const body: ZippoSetStateRequest = {
  vehicleId: args.vehicle!,
  partId:    args.part!,
};

if (hasRgb) {
  const r = parseFloat(args["color-r"] ?? "0");
  const g = parseFloat(args["color-g"] ?? "0");
  const b = parseFloat(args["color-b"] ?? "0");
  if (isNaN(r) || isNaN(g) || isNaN(b)) {
    console.error("Color channel values must be numbers (0–1).");
    usage();
  }
  body.color = { r, g, b };
}

if (hasName) {
  body.colorName = args["color-name"]!;
}

if (args.intensity !== undefined) {
  const i = parseFloat(args.intensity);
  if (isNaN(i) || i < 0 || i > 1) {
    console.error("--intensity must be a number between 0 and 1.");
    usage();
  }
  body.intensity = i;
}

if (args.on)  body.enabled = true;
if (args.off) body.enabled = false;

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/zippo/lights/state`, {
  method:  "POST",
  headers: { "Content-Type": "application/json" },
  body:    JSON.stringify(body),
});
const json = (await res.json()) as ApiResponse<ZippoSetStateData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
