#!/usr/bin/env bun
/**
 * weld-modify.ts — Modify an existing vehicle weld via Unladen Swallow RPC.
 *
 * Partially updates a weld. Only the fields you supply are changed; omitted
 * fields retain their current values.
 *
 * Usage: bun run weld-modify.ts -s <sourceId> [field-options]
 *
 * Examples:
 *   bun run weld-modify.ts -s vehicle-001 -y 3.0
 *   bun run weld-modify.ts -s vehicle-001 -x 0 -y 3.0 -z 0.5 --scale 1.5
 *   bun run weld-modify.ts -s vehicle-001 --lock-rotation
 *   bun run weld-modify.ts -s vehicle-001 --no-lock-rotation
 */

import { parseArgs } from "util";
import type { ApiResponse, ModifyWeldRequest, Vec3, WeldInfoData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Usage
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run weld-modify.ts -s <sourceId> [options]");
  console.error("");
  console.error("Partially updates an existing weld. Omitted fields are left unchanged.");
  console.error("");
  console.error("Required:");
  console.error("  -s, --source    ID of the source (child) vehicle whose weld to modify");
  console.error("");
  console.error("Optional (at least one required):");
  console.error("  -x, --pos-x     New position X in metres");
  console.error("  -y, --pos-y     New position Y in metres");
  console.error("  -z, --pos-z     New position Z in metres");
  console.error("      --rot-x     New rotation X in degrees");
  console.error("      --rot-y     New rotation Y in degrees");
  console.error("      --rot-z     New rotation Z in degrees");
  console.error("      --scale     New uniform scale factor, 0.05–20.0");
  console.error("      --lock-rotation     Enable rotation locking");
  console.error("      --no-lock-rotation  Disable rotation locking");
  console.error("");
  console.error("Notes:");
  console.error("  Position: all three of --pos-x/y/z must be supplied together if any is set.");
  console.error("  Rotation: all three of --rot-x/y/z must be supplied together if any is set.");
  console.error("  --lock-rotation and --no-lock-rotation are mutually exclusive.");
  console.error("");
  console.error("Examples:");
  console.error("  bun run weld-modify.ts -s vehicle-001 -y 3.0");
  console.error("  bun run weld-modify.ts -s vehicle-001 -x 0 -y 3.0 -z 0.5 --scale 1.5");
  console.error("  bun run weld-modify.ts -s vehicle-001 --no-lock-rotation");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    source:             { type: "string",  short: "s" },
    "pos-x":            { type: "string",  short: "x" },
    "pos-y":            { type: "string",  short: "y" },
    "pos-z":            { type: "string",  short: "z" },
    "rot-x":            { type: "string" },
    "rot-y":            { type: "string" },
    "rot-z":            { type: "string" },
    scale:              { type: "string" },
    "lock-rotation":    { type: "boolean" },
    "no-lock-rotation": { type: "boolean" },
    help:               { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.source) { console.error("Missing required: --source"); usage(); }

if (args["lock-rotation"] && args["no-lock-rotation"]) {
  console.error("--lock-rotation and --no-lock-rotation are mutually exclusive.");
  usage();
}

// Position: all three or none
const hasAnyPos = args["pos-x"] !== undefined || args["pos-y"] !== undefined || args["pos-z"] !== undefined;
const hasAllPos = args["pos-x"] !== undefined && args["pos-y"] !== undefined && args["pos-z"] !== undefined;
if (hasAnyPos && !hasAllPos) {
  console.error("--pos-x, --pos-y, and --pos-z must all be provided together.");
  usage();
}

// Rotation: all three or none
const hasAnyRot = args["rot-x"] !== undefined || args["rot-y"] !== undefined || args["rot-z"] !== undefined;
const hasAllRot = args["rot-x"] !== undefined && args["rot-y"] !== undefined && args["rot-z"] !== undefined;
if (hasAnyRot && !hasAllRot) {
  console.error("--rot-x, --rot-y, and --rot-z must all be provided together.");
  usage();
}

const body: ModifyWeldRequest = { sourceVehicleId: args.source! };

if (hasAllPos) {
  const px = parseFloat(args["pos-x"]!);
  const py = parseFloat(args["pos-y"]!);
  const pz = parseFloat(args["pos-z"]!);
  if (isNaN(px) || isNaN(py) || isNaN(pz)) { console.error("Position values must be numbers."); usage(); }
  body.position = { x: px, y: py, z: pz } satisfies Vec3;
}

if (hasAllRot) {
  const rx = parseFloat(args["rot-x"]!);
  const ry = parseFloat(args["rot-y"]!);
  const rz = parseFloat(args["rot-z"]!);
  if (isNaN(rx) || isNaN(ry) || isNaN(rz)) { console.error("Rotation values must be numbers."); usage(); }
  body.rotation = { x: rx, y: ry, z: rz } satisfies Vec3;
}

if (args.scale !== undefined) {
  const scale = parseFloat(args.scale);
  if (isNaN(scale) || scale < 0.05 || scale > 20) {
    console.error("Scale must be a number between 0.05 and 20.0.");
    usage();
  }
  body.scale = scale;
}

if (args["lock-rotation"]) body.lockRotation = true;
if (args["no-lock-rotation"]) body.lockRotation = false;

const hasAnyChange =
  body.position !== undefined ||
  body.rotation !== undefined ||
  body.scale !== undefined ||
  body.lockRotation !== undefined;

if (!hasAnyChange) {
  console.error("Provide at least one field to modify.");
  usage();
}

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/welds/modify`, {
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
