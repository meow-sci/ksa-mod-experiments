#!/usr/bin/env bun
/**
 * preset-list.ts — List all saved weld presets via Unladen Swallow RPC.
 *
 * Usage: bun run preset-list.ts
 */

import type { ApiResponse, PresetListData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

function usage(): never {
  console.error("Usage: bun run preset-list.ts");
  console.error("");
  console.error("Lists all saved weld presets.");
  console.error("");
  console.error("Example:");
  console.error("  bun run preset-list.ts");
  process.exit(1);
}

if (Bun.argv.slice(2).some(a => a === "--help" || a === "-h")) {
  usage();
}

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/presets`);
const json = (await res.json()) as ApiResponse<PresetListData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
