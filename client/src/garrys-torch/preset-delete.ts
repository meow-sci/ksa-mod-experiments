#!/usr/bin/env bun
/**
 * preset-delete.ts — Delete a named weld preset via Unladen Swallow RPC.
 *
 * Usage: bun run preset-delete.ts -n <name>
 *
 * Example:
 *   bun run preset-delete.ts -n docking-clamp
 */

import { parseArgs } from "util";
import type { ApiResponse, MessageData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Usage
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run preset-delete.ts -n <name>");
  console.error("");
  console.error("Deletes the named weld preset. Returns an error if no preset with that name exists.");
  console.error("");
  console.error("Required:");
  console.error("  -n, --name    Name of the preset to delete");
  console.error("");
  console.error("Example:");
  console.error("  bun run preset-delete.ts -n docking-clamp");
  process.exit(1);
}

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    name: { type: "string",  short: "n" },
    help: { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.name) { console.error("Missing required: --name"); usage(); }

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/presets`, {
  method: "DELETE",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ name: args.name }),
});

const json = (await res.json()) as ApiResponse<MessageData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
