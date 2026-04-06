#!/usr/bin/env bun
/**
 * weld-delete.ts — Remove a vehicle weld via Unladen Swallow RPC.
 *
 * Usage: bun run weld-delete.ts -s <sourceId>
 *
 * Example:
 *   bun run weld-delete.ts -s vehicle-001
 */

import { parseArgs } from "util";
import type { ApiResponse, MessageData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Usage
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run weld-delete.ts -s <sourceId>");
  console.error("");
  console.error("Removes the weld from the specified source (child) vehicle.");
  console.error("");
  console.error("Required:");
  console.error("  -s, --source    ID of the source (child) vehicle whose weld to remove");
  console.error("");
  console.error("Example:");
  console.error("  bun run weld-delete.ts -s vehicle-001");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    source: { type: "string", short: "s" },
    help:   { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.source) { console.error("Missing required: --source"); usage(); }

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/welds`, {
  method: "DELETE",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ sourceVehicleId: args.source }),
});

const json = (await res.json()) as ApiResponse<MessageData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
