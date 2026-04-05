#!/usr/bin/env bun
/**
 * weld-list.ts — List all active vehicle welds via Unladen Swallow RPC.
 *
 * Usage: bun run weld-list.ts
 */

import type { ApiResponse, WeldListData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

function usage(): never {
  console.error("Usage: bun run weld-list.ts");
  console.error("");
  console.error("Lists all currently active vehicle welds.");
  console.error("");
  console.error("Example:");
  console.error("  bun run weld-list.ts");
  process.exit(1);
}

// No arguments needed, but catch --help
if (Bun.argv.slice(2).some(a => a === "--help" || a === "-h")) {
  usage();
}

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const res = await fetch(`${BASE_URL}/torch/welds`);
const json = (await res.json()) as ApiResponse<WeldListData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
