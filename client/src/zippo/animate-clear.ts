#!/usr/bin/env bun
/**
 * animate-clear.ts — Clear the animation queue for a light part via Unladen Swallow RPC.
 *
 * Cancels the active animation and all queued animations for the specified part.
 *
 * Usage: bun run animate-clear.ts -v <vehicleId> -p <partId>
 */

import { parseArgs } from "util";
import type { ApiResponse, ZippoClearAnimationRequest, ZippoClearAnimationData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

function usage(): never {
  console.error("Usage: bun run animate-clear.ts -v <vehicleId> -p <partId>");
  console.error("");
  console.error("Cancels the active and all queued animations for a light part.");
  console.error("");
  console.error("Required:");
  console.error("  -v, --vehicle     Vehicle ID");
  console.error("  -p, --part        Part ID");
  console.error("");
  console.error("Example:");
  console.error("  bun run animate-clear.ts -v Rocket-001 -p light-nose-001");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    vehicle: { type: "string",  short: "v" },
    part:    { type: "string",  short: "p" },
    help:    { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.vehicle) { console.error("Missing required: --vehicle"); usage(); }
if (!args.part)    { console.error("Missing required: --part");    usage(); }

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const body: ZippoClearAnimationRequest = {
  vehicleId: args.vehicle!,
  partId:    args.part!,
};

const res = await fetch(`${BASE_URL}/zippo/animate`, {
  method:  "DELETE",
  headers: { "Content-Type": "application/json" },
  body:    JSON.stringify(body),
});
const json = (await res.json()) as ApiResponse<ZippoClearAnimationData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
