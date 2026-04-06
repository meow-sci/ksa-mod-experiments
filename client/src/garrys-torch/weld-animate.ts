#!/usr/bin/env bun
/**
 * weld-animate.ts — Animate a vehicle weld to a target state via Unladen Swallow RPC.
 *
 * Smooth-interpolates a weld from its current state to a target state over a
 * specified duration. Provide either inline weld data or a preset name.
 *
 * If an animation is already playing for this weld, the new animation is queued
 * and starts when the current one finishes.
 *
 * Usage:
 *   bun run weld-animate.ts -s <sourceId> -d <duration> [weld-data-options] [easing-options]
 *   bun run weld-animate.ts -s <sourceId> -d <duration> -p <presetName> [easing-options]
 *
 * Examples:
 *   bun run weld-animate.ts -s vehicle-001 -d 2.5 -x 0 -y 5.0 -z 0
 *   bun run weld-animate.ts -s vehicle-001 -d 2.5 -x 0 -y 5.0 -z 0 --rot-y 45 --easing easeInOut
 *   bun run weld-animate.ts -s vehicle-001 -d 3.0 -p docking-clamp --easing easeIn --easing-power-start 2
 */

import { parseArgs } from "util";
import type { AnimateWeldRequest, ApiResponse, AnimateWeldData, TorchEasingType, TorchEasingConfig } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Usage
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run weld-animate.ts -s <sourceId> -d <duration> [options]");
  console.error("");
  console.error("Animates a weld to a target state over the given duration.");
  console.error("");
  console.error("Required:");
  console.error("  -s, --source      ID of the source (child) vehicle whose weld to animate");
  console.error("  -d, --duration    Animation duration in seconds");
  console.error("");
  console.error("Target state (required unless --preset is specified):");
  console.error("  -x, --pos-x       Target position X in metres");
  console.error("  -y, --pos-y       Target position Y in metres");
  console.error("  -z, --pos-z       Target position Z in metres");
  console.error("      --rot-x       Target rotation X in degrees (default: 0)");
  console.error("      --rot-y       Target rotation Y in degrees (default: 0)");
  console.error("      --rot-z       Target rotation Z in degrees (default: 0)");
  console.error("      --scale       Target uniform scale factor, 0.05–20.0");
  console.error("      --no-lock-rotation  Disable rotation locking (locked by default)");
  console.error("");
  console.error("Or use a preset:");
  console.error("  -p, --preset      Name of a saved preset to animate towards");
  console.error("");
  console.error("Easing (all optional):");
  console.error("  --easing          Easing curve: linear | easeIn | easeOut | easeInOut (default: linear)");
  console.error("  --easing-power-start   Exponent for ease-in curve (default: 3.0)");
  console.error("  --easing-power-end     Exponent for ease-out curve (default: 3.0)");
  console.error("");
  console.error("Examples:");
  console.error("  bun run weld-animate.ts -s vehicle-001 -d 2.5 -x 0 -y 5.0 -z 0");
  console.error("  bun run weld-animate.ts -s vehicle-001 -d 2.5 -x 0 -y 5.0 -z 0 --rot-y 45 --easing easeInOut");
  console.error("  bun run weld-animate.ts -s vehicle-001 -d 3.0 -p docking-clamp --easing easeIn");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    source:               { type: "string",  short: "s" },
    duration:             { type: "string",  short: "d" },
    preset:               { type: "string",  short: "p" },
    "pos-x":              { type: "string",  short: "x" },
    "pos-y":              { type: "string",  short: "y" },
    "pos-z":              { type: "string",  short: "z" },
    "rot-x":              { type: "string" },
    "rot-y":              { type: "string" },
    "rot-z":              { type: "string" },
    scale:                { type: "string" },
    "no-lock-rotation":   { type: "boolean" },
    easing:               { type: "string" },
    "easing-power-start": { type: "string" },
    "easing-power-end":   { type: "string" },
    help:                 { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.source)   { console.error("Missing required: --source"); usage(); }
if (!args.duration) { console.error("Missing required: --duration"); usage(); }

const durationSeconds = parseFloat(args.duration!);
if (isNaN(durationSeconds) || durationSeconds <= 0) {
  console.error(`Invalid duration: ${args.duration}`);
  usage();
}

if (args.preset && args["pos-x"] !== undefined) {
  console.error("Provide either --preset or weld data (--pos-x/y/z), not both.");
  usage();
}

// Easing config
let easingConfig: TorchEasingConfig | undefined;
const hasEasing = args.easing !== undefined || args["easing-power-start"] !== undefined || args["easing-power-end"] !== undefined;
if (hasEasing) {
  const validEasings: TorchEasingType[] = ["linear", "easeIn", "easeOut", "easeInOut"];
  if (args.easing !== undefined && !validEasings.includes(args.easing as TorchEasingType)) {
    console.error(`Invalid easing: '${args.easing}'. Must be one of: ${validEasings.join(", ")}`);
    usage();
  }
  easingConfig = {};
  if (args.easing !== undefined) easingConfig.easing = args.easing as TorchEasingType;
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

let body: AnimateWeldRequest;

if (args.preset) {
  body = {
    sourceVehicleId: args.source!,
    durationSeconds,
    presetName: args.preset,
    easing: easingConfig,
  };
} else {
  if (args["pos-x"] === undefined) { console.error("Missing required: --pos-x (or use --preset)"); usage(); }
  if (args["pos-y"] === undefined) { console.error("Missing required: --pos-y (or use --preset)"); usage(); }
  if (args["pos-z"] === undefined) { console.error("Missing required: --pos-z (or use --preset)"); usage(); }

  const px = parseFloat(args["pos-x"]!);
  const py = parseFloat(args["pos-y"]!);
  const pz = parseFloat(args["pos-z"]!);
  if (isNaN(px) || isNaN(py) || isNaN(pz)) { console.error("Position values must be numbers."); usage(); }

  const rx = args["rot-x"] !== undefined ? parseFloat(args["rot-x"]) : 0;
  const ry = args["rot-y"] !== undefined ? parseFloat(args["rot-y"]) : 0;
  const rz = args["rot-z"] !== undefined ? parseFloat(args["rot-z"]) : 0;
  if (isNaN(rx) || isNaN(ry) || isNaN(rz)) { console.error("Rotation values must be numbers."); usage(); }

  let scale: number | undefined;
  if (args.scale !== undefined) {
    scale = parseFloat(args.scale);
    if (isNaN(scale) || scale < 0.05 || scale > 20) {
      console.error("Scale must be a number between 0.05 and 20.0.");
      usage();
    }
  }

  body = {
    sourceVehicleId: args.source!,
    durationSeconds,
    data: {
      position: { x: px, y: py, z: pz },
      rotation: { x: rx, y: ry, z: rz },
      scale,
      lockRotation: args["no-lock-rotation"] ? false : true,
    },
    easing: easingConfig,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/welds/animate`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

const json = (await res.json()) as ApiResponse<AnimateWeldData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
