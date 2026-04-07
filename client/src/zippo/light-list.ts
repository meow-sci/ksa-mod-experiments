#!/usr/bin/env bun
/**
 * light-list.ts — List all light parts on a vehicle via Unladen Swallow RPC.
 *
 * Usage: bun run light-list.ts -v <vehicleId>
 */

import { parseArgs } from "util";
import type { ApiResponse, ZippoLightsListData } from "./types";

const BASE_URL = "http://127.0.0.1:7887";

function usage(): never {
  console.error("Usage: bun run light-list.ts -v <vehicleId>");
  console.error("");
  console.error("Lists all light parts on the specified vehicle with their current state.");
  console.error("");
  console.error("Required:");
  console.error("  -v, --vehicle     Vehicle ID to inspect");
  console.error("");
  console.error("Example:");
  console.error("  bun run light-list.ts -v Rocket-001");
  process.exit(1);
}

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    vehicle: { type: "string",  short: "v" },
    help:    { type: "boolean", short: "h" },
  },
  strict: true,
  allowPositionals: false,
});

if (args.help) usage();
if (!args.vehicle) { console.error("Missing required: --vehicle"); usage(); }

// ─────────────────────────────────────────────────────────────────────────────
// Request
// ─────────────────────────────────────────────────────────────────────────────

const url = new URL(`${BASE_URL}/zippo/lights/`);
url.searchParams.set("vehicleId", args.vehicle!);

console.log(`Fetching ${url.toString()}...`);

const res = await fetch(url.toString());
const json = (await res.json()) as ApiResponse<ZippoLightsListData>;

if (json.status !== "ok") {
  console.error(`Error: ${(json as { status: string; message: string }).message}`);
  process.exit(1);
}

console.log(JSON.stringify(json, null, 2));
