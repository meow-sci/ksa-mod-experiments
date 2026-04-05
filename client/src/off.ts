#!/usr/bin/env bun
/**
 * off.ts — Turn off blinky LEDs for a vehicle.
 * Usage: bun run off.ts -v <vehicleId> [-g <gridName>]
 */

import { parseArgs } from "util";

const BASE_URL = "http://localhost:7887";

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    vehicle: { type: "string", short: "v" },
    grid:    { type: "string", short: "g" },
  },
  strict: true,
  allowPositionals: false,
});

const vehicleId = args.vehicle;
const gridName  = args.grid;

if (!vehicleId) {
  console.error("Usage: bun run off.ts -v <vehicleId> [-g <gridName>]");
  console.error("  -v, --vehicle  vehicle ID (required)");
  console.error("  -g, --grid     grid name (optional)");
  process.exit(1);
}

type ApiResponse =
  | { status: "ok"; data: Record<string, unknown> }
  | { status: "error"; message?: string; data?: Record<string, unknown> };

async function main() {
  let res: Response;
  try {
    res = await fetch(`${BASE_URL}/blinky/off`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ vehicleId, gridName }),
    });
  } catch (err) {
    console.error("Network error:", err);
    process.exit(1);
  }

  let response: ApiResponse;
  try {
    response = (await res.json()) as ApiResponse;
  } catch {
    console.error("Failed to parse response JSON (HTTP", res.status, ")");
    process.exit(1);
  }

  if (!res.ok || response.status === "error") {
    const msg =
      response.status === "error" && response.message
        ? response.message
        : `HTTP ${res.status}`;
    console.error("Error:", msg);
    process.exit(1);
  }

  console.log(JSON.stringify(response, null, 2));
}

main();
