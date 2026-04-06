#!/usr/bin/env bun
/**
 * weld-create.ts — Create a new vehicle weld via Unladen Swallow RPC.
 *
 * Welds a source (child) vehicle to a target (parent) vehicle. Provide either
 * inline weld data (position, rotation, scale) or a preset name — not both.
 *
 * Usage:
 *   bun run weld-create.ts -s <sourceId> -t <targetId> [weld-data-options]
 *   bun run weld-create.ts -s <sourceId> -t <targetId> -p <presetName>
 *
 * Examples:
 *   bun run weld-create.ts -s vehicle-001 -t vehicle-002 -x 0 -y 2.5 -z 0
 *   bun run weld-create.ts -s vehicle-001 -t vehicle-002 -x 0 -y 2.5 -z 0 --scale 1.2 --no-lock-rotation
 *   bun run weld-create.ts -s vehicle-001 -t vehicle-002 -p docking-clamp
 */

import { parseArgs } from "util";
import type { ApiResponse, CreateWeldRequest, WeldInfoData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Usage
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run weld-create.ts -s <sourceId> -t <targetId> [options]");
  console.error("");
  console.error("Required:");
  console.error("  -s, --source    ID of the source (child) vehicle to weld");
  console.error("  -t, --target    ID of the target (parent) vehicle to weld to");
  console.error("");
  console.error("Weld data (required unless --preset is specified):");
  console.error("  -x, --pos-x     Weld position X in metres");
  console.error("  -y, --pos-y     Weld position Y in metres");
  console.error("  -z, --pos-z     Weld position Z in metres");
  console.error("      --rot-x     Weld rotation X in degrees (default: 0)");
  console.error("      --rot-y     Weld rotation Y in degrees (default: 0)");
  console.error("      --rot-z     Weld rotation Z in degrees (default: 0)");
  console.error("      --scale     Uniform scale factor, 0.05–20.0 (default: 1.0)");
  console.error("      --no-lock-rotation  Disable rotation locking (locked by default)");
  console.error("");
  console.error("Or use a preset:");
  console.error("  -p, --preset    Name of a saved preset to use");
  console.error("");
  console.error("Examples:");
  console.error("  bun run weld-create.ts -s vehicle-001 -t vehicle-002 -x 0 -y 2.5 -z 0");
  console.error("  bun run weld-create.ts -s vehicle-001 -t vehicle-002 -x 0 -y 2.5 -z 0 --scale 1.2");
  console.error("  bun run weld-create.ts -s vehicle-001 -t vehicle-002 -p docking-clamp");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    source:           { type: "string",  short: "s" },
    target:           { type: "string",  short: "t" },
    preset:           { type: "string",  short: "p" },
    "pos-x":          { type: "string",  short: "x" },
    "pos-y":          { type: "string",  short: "y" },
    "pos-z":          { type: "string",  short: "z" },
    "rot-x":          { type: "string" },
    "rot-y":          { type: "string" },
    "rot-z":          { type: "string" },
    scale:            { type: "string" },
    "no-lock-rotation": { type: "boolean" },
    help:             { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();

if (!args.source) { console.error("Missing required: --source"); usage(); }
if (!args.target) { console.error("Missing required: --target"); usage(); }

// Preset vs inline data
if (args.preset && (args["pos-x"] !== undefined || args["pos-y"] !== undefined || args["pos-z"] !== undefined)) {
  console.error("Provide either --preset or weld data (--pos-x/y/z), not both.");
  usage();
}

let body: CreateWeldRequest;

if (args.preset) {
  body = {
    sourceVehicleId: args.source,
    targetVehicleId: args.target,
    presetName: args.preset,
  };
} else {
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

  body = {
    sourceVehicleId: args.source,
    targetVehicleId: args.target,
    data: {
      position: { x: px, y: py, z: pz },
      rotation: { x: rx, y: ry, z: rz },
      scale,
      lockRotation: args["no-lock-rotation"] ? false : true,
    },
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/welds`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

const json = (await res.json()) as ApiResponse<WeldInfoData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
