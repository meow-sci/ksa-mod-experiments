#!/usr/bin/env bun
/**
 * animate.ts — Queue a color/intensity animation for a light part via Unladen Swallow RPC.
 *
 * Interpolates from start values to end values over `durationSeconds`.
 * Start/end values default to the part's current state if omitted.
 * Animations are queued per-part (max 25) and play back-to-back.
 * Manual light controls are locked while any animation is active.
 *
 * Usage:
 *   bun run animate.ts -v <vehicleId> -p <partId> -d <duration> [options]
 *
 * Examples:
 *   bun run animate.ts -v Rocket-001 -p light-nose-001 -d 3.0 --end-r 1.0 --end-g 0.0 --end-b 0.0
 *   bun run animate.ts -v Rocket-001 -p light-nose-001 -d 2.0 --end-color-name NeonBlue --easing EaseOut
 *   bun run animate.ts -v Rocket-001 -p light-nose-001 -d 0.5 --start-intensity 0.0 --end-intensity 1.0
 */

import { parseArgs } from "util";
import { ZippoEasingType } from "./types";
import type {
  ApiResponse,
  ZippoAnimateRequest,
  ZippoAnimateData,
  ZippoEasingConfig,
  ZippoAnimColor,
} from "./types";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Usage
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run animate.ts -v <vehicleId> -p <partId> -d <duration> [options]");
  console.error("");
  console.error("Queues a color/intensity animation for a light part.");
  console.error("");
  console.error("Required:");
  console.error("  -v, --vehicle              Vehicle ID");
  console.error("  -p, --part                 Part ID");
  console.error("  -d, --duration             Duration in seconds");
  console.error("");
  console.error("Start color (defaults to current color if omitted):");
  console.error("  --start-r/g/b              Start color RGB (0–1 each)");
  console.error("  --start-color-name         Start color by XKCD name");
  console.error("");
  console.error("End color (defaults to current color if omitted):");
  console.error("  --end-r/g/b                End color RGB (0–1 each)");
  console.error("  --end-color-name           End color by XKCD name");
  console.error("");
  console.error("Intensity:");
  console.error("  --start-intensity          Start intensity (0–1)");
  console.error("  --end-intensity            End intensity (0–1)");
  console.error("");
  console.error("Easing (all optional):");
  console.error("  --easing                   Linear | EaseIn | EaseOut | EaseInOut (default: EaseInOut)");
  console.error("  --easing-power-start       Ease-in exponent (default: 3.0)");
  console.error("  --easing-power-end         Ease-out exponent (default: 3.0)");
  console.error("");
  console.error("Examples:");
  console.error("  bun run animate.ts -v Rocket-001 -p light-nose-001 -d 3.0 --end-r 1.0 --end-g 0.0 --end-b 0.0");
  console.error("  bun run animate.ts -v Rocket-001 -p light-nose-001 -d 2.0 --end-color-name NeonBlue --easing EaseOut");
  console.error("  bun run animate.ts -v Rocket-001 -p light-nose-001 -d 0.5 --start-intensity 0.0 --end-intensity 1.0");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    vehicle:              { type: "string",  short: "v" },
    part:                 { type: "string",  short: "p" },
    duration:             { type: "string",  short: "d" },
    "start-r":            { type: "string" },
    "start-g":            { type: "string" },
    "start-b":            { type: "string" },
    "start-color-name":   { type: "string" },
    "end-r":              { type: "string" },
    "end-g":              { type: "string" },
    "end-b":              { type: "string" },
    "end-color-name":     { type: "string" },
    "start-intensity":    { type: "string" },
    "end-intensity":      { type: "string" },
    easing:               { type: "string" },
    "easing-power-start": { type: "string" },
    "easing-power-end":   { type: "string" },
    help:                 { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.vehicle)  { console.error("Missing required: --vehicle");  usage(); }
if (!args.part)     { console.error("Missing required: --part");     usage(); }
if (!args.duration) { console.error("Missing required: --duration"); usage(); }

const durationSeconds = parseFloat(args.duration!);
if (isNaN(durationSeconds) || durationSeconds <= 0) {
  console.error(`Invalid duration: ${args.duration}`);
  usage();
}

const hasStartRgb  = args["start-r"] !== undefined || args["start-g"] !== undefined || args["start-b"] !== undefined;
const hasStartName = args["start-color-name"] !== undefined;
const hasEndRgb    = args["end-r"]   !== undefined || args["end-g"]   !== undefined || args["end-b"]   !== undefined;
const hasEndName   = args["end-color-name"] !== undefined;

if (hasStartRgb && hasStartName) {
  console.error("Provide either --start-r/g/b or --start-color-name, not both.");
  usage();
}
if (hasEndRgb && hasEndName) {
  console.error("Provide either --end-r/g/b or --end-color-name, not both.");
  usage();
}

// ─────────────────────────────────────────────────────────────────────────────
// Build color specs
// ─────────────────────────────────────────────────────────────────────────────

let startColor: ZippoAnimColor | undefined;
if (hasStartRgb) {
  const r = parseFloat(args["start-r"] ?? "0");
  const g = parseFloat(args["start-g"] ?? "0");
  const b = parseFloat(args["start-b"] ?? "0");
  if (isNaN(r) || isNaN(g) || isNaN(b)) { console.error("Start color RGB values must be numbers (0–1)."); usage(); }
  startColor = { rgb: { r, g, b } };
} else if (hasStartName) {
  startColor = { colorName: args["start-color-name"]! };
}

let endColor: ZippoAnimColor | undefined;
if (hasEndRgb) {
  const r = parseFloat(args["end-r"] ?? "0");
  const g = parseFloat(args["end-g"] ?? "0");
  const b = parseFloat(args["end-b"] ?? "0");
  if (isNaN(r) || isNaN(g) || isNaN(b)) { console.error("End color RGB values must be numbers (0–1)."); usage(); }
  endColor = { rgb: { r, g, b } };
} else if (hasEndName) {
  endColor = { colorName: args["end-color-name"]! };
}

// ─────────────────────────────────────────────────────────────────────────────
// Build intensity values
// ─────────────────────────────────────────────────────────────────────────────

let startIntensity: number | undefined;
if (args["start-intensity"] !== undefined) {
  startIntensity = parseFloat(args["start-intensity"]);
  if (isNaN(startIntensity) || startIntensity < 0 || startIntensity > 1) {
    console.error("--start-intensity must be a number between 0 and 1.");
    usage();
  }
}

let endIntensity: number | undefined;
if (args["end-intensity"] !== undefined) {
  endIntensity = parseFloat(args["end-intensity"]);
  if (isNaN(endIntensity) || endIntensity < 0 || endIntensity > 1) {
    console.error("--end-intensity must be a number between 0 and 1.");
    usage();
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Build easing config
// ─────────────────────────────────────────────────────────────────────────────

let easingConfig: ZippoEasingConfig | undefined;
const hasEasing = args.easing !== undefined || args["easing-power-start"] !== undefined || args["easing-power-end"] !== undefined;
if (hasEasing) {
  const validEasingNames = Object.keys(ZippoEasingType) as (keyof typeof ZippoEasingType)[];
  if (args.easing !== undefined && !validEasingNames.includes(args.easing as keyof typeof ZippoEasingType)) {
    console.error(`Invalid easing: '${args.easing}'. Must be one of: ${validEasingNames.join(", ")}`);
    usage();
  }
  easingConfig = {};
  if (args.easing !== undefined) easingConfig.easing = ZippoEasingType[args.easing as keyof typeof ZippoEasingType];
  if (args["easing-power-start"] !== undefined) {
    const v = parseFloat(args["easing-power-start"]);
    if (isNaN(v)) { console.error("--easing-power-start must be a number."); usage(); }
    easingConfig.easingPowerStart = v;
  }
  if (args["easing-power-end"] !== undefined) {
    const v = parseFloat(args["easing-power-end"]);
    if (isNaN(v)) { console.error("--easing-power-end must be a number."); usage(); }
    easingConfig.easingPowerEnd = v;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const body: ZippoAnimateRequest = {
  vehicleId: args.vehicle!,
  partId:    args.part!,
  durationSeconds,
  ...(startColor     !== undefined && { startColor }),
  ...(endColor       !== undefined && { endColor }),
  ...(startIntensity !== undefined && { startIntensity }),
  ...(endIntensity   !== undefined && { endIntensity }),
  ...(easingConfig   !== undefined && { easing: easingConfig }),
};

const res = await fetch(`${BASE_URL}/zippo/animate`, {
  method:  "POST",
  headers: { "Content-Type": "application/json" },
  body:    JSON.stringify(body),
});
const json = (await res.json()) as ApiResponse<ZippoAnimateData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
