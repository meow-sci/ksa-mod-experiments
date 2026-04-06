#!/usr/bin/env bun
/**
 * preset-save.ts — Save or update a named weld preset via Unladen Swallow RPC.
 *
 * Creates a new preset or overwrites an existing one with the same name.
 *
 * Usage: bun run preset-save.ts -n <name> -x <posX> -y <posY> -z <posZ> [options]
 *
 * Examples:
 *   bun run preset-save.ts -n docking-clamp -x 0 -y 2.5 -z 0
 *   bun run preset-save.ts -n docking-clamp -x 0 -y 2.5 -z 0 --rot-y 180 --scale 0.8
 *   bun run preset-save.ts -n docking-clamp -x 0 -y 2.5 -z 0 --no-lock-rotation
 */

import { parseArgs } from "util";
import type { ApiResponse, PresetInfoData, SavePresetRequest } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Usage
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run preset-save.ts -n <name> -x <posX> -y <posY> -z <posZ> [options]");
  console.error("");
  console.error("Saves a named weld preset. Overwrites any existing preset with the same name.");
  console.error("");
  console.error("Required:");
  console.error("  -n, --name      Name for the preset");
  console.error("  -x, --pos-x     Position X in metres");
  console.error("  -y, --pos-y     Position Y in metres");
  console.error("  -z, --pos-z     Position Z in metres");
  console.error("");
  console.error("Optional:");
  console.error("      --rot-x     Rotation X in degrees (default: 0)");
  console.error("      --rot-y     Rotation Y in degrees (default: 0)");
  console.error("      --rot-z     Rotation Z in degrees (default: 0)");
  console.error("      --scale     Uniform scale factor, 0.05–20.0 (default: 1.0)");
  console.error("      --no-lock-rotation  Disable rotation locking (locked by default)");
  console.error("");
  console.error("Examples:");
  console.error("  bun run preset-save.ts -n docking-clamp -x 0 -y 2.5 -z 0");
  console.error("  bun run preset-save.ts -n docking-clamp -x 0 -y 2.5 -z 0 --rot-y 180 --scale 0.8");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    name:               { type: "string",  short: "n" },
    "pos-x":            { type: "string",  short: "x" },
    "pos-y":            { type: "string",  short: "y" },
    "pos-z":            { type: "string",  short: "z" },
    "rot-x":            { type: "string" },
    "rot-y":            { type: "string" },
    "rot-z":            { type: "string" },
    scale:              { type: "string" },
    "no-lock-rotation": { type: "boolean" },
    help:               { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.name)          { console.error("Missing required: --name"); usage(); }
if (args["pos-x"] === undefined) { console.error("Missing required: --pos-x"); usage(); }
if (args["pos-y"] === undefined) { console.error("Missing required: --pos-y"); usage(); }
if (args["pos-z"] === undefined) { console.error("Missing required: --pos-z"); usage(); }

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

const body: SavePresetRequest = {
  name: args.name!,
  data: {
    position: { x: px, y: py, z: pz },
    rotation: { x: rx, y: ry, z: rz },
    scale,
    lockRotation: args["no-lock-rotation"] ? false : true,
  },
};

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/presets`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

const json = (await res.json()) as ApiResponse<PresetInfoData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
