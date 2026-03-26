#!/usr/bin/env bun
/**
 * off.ts — Turn off blinky LEDs for a vehicle.
 * Usage: bun run off.ts <vehicleId>
 */

const BASE_URL = "http://localhost:7887";

const vehicleId = Bun.argv[2];

if (!vehicleId) {
  console.error("Usage: bun run off.ts <vehicleId>");
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
      body: JSON.stringify({ vehicleId }),
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
