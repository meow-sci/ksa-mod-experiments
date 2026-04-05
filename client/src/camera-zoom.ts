#!/usr/bin/env bun
/**
 * camera-zoom.ts — Execute a camera zoom animation via Unladen Swallow RPC.
 * 
 * Zooms the camera out (or in) over a specified duration.
 * Hard-codes easing to "easeOut" for smooth motion.
 * Optionally returns camera to start position after animation completes.
 * 
 * Usage: bun run camera-zoom.ts -m (out|in) -s <speed> -d <duration> [-r <returnDuration>]
 *
 * Examples:
 *   bun run camera-zoom.ts -m out -s 5.0 -d 3.0
 *   bun run camera-zoom.ts -m out -s 5.0 -d 3.0 -r 2.0
 */

import { parseArgs } from "util";
import { CameraEasingType, type CameraAnimateRequest } from "./types/camera";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Argument parsing
// ─────────────────────────────────────────────────────────────────────────────

function usage(): never {
  console.error("Usage: bun run camera-zoom.ts -m (out|in) -s <speed> -d <duration> [-r <returnDuration>]");
  console.error("  -m, --mode              zoom direction: 'out' or 'in' (required)");
  console.error("  -s, --speed             zoom speed in meters/second (required)");
  console.error("  -d, --duration          zoom duration in seconds (required)");
  console.error("  -r, --return-duration   optional: return-to-start duration in seconds");
  console.error("");
  console.error("Examples:");
  console.error("  bun run camera-zoom.ts -m out -s 5.0 -d 3.0");
  console.error("  bun run camera-zoom.ts -m in -s 2.5 -d 4.0 -r 2.0");
  process.exit(1);
}

const { values: args } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    mode:            { type: "string", short: "m" },
    speed:           { type: "string", short: "s" },
    duration:        { type: "string", short: "d" },
    "return-duration": { type: "string", short: "r" },
  },
  strict: true,
  allowPositionals: false,
});

const mode = args.mode as string | undefined;
const rawSpeed = args.speed as string | undefined;
const rawDuration = args.duration as string | undefined;
const rawReturnDuration = args["return-duration"] as string | undefined;

// Validate mode
if (!mode || (mode !== "out" && mode !== "in")) {
  console.error("Invalid mode: must be 'out' or 'in'");
  usage();
}

// Validate speed
if (!rawSpeed) {
  console.error("Speed is required");
  usage();
}
const speed = parseFloat(rawSpeed);
if (isNaN(speed) || speed <= 0) {
  console.error(`Invalid speed: ${rawSpeed}`);
  usage();
}

// Validate duration
if (!rawDuration) {
  console.error("Duration is required");
  usage();
}
const duration = parseFloat(rawDuration);
if (isNaN(duration) || duration <= 0) {
  console.error(`Invalid duration: ${rawDuration}`);
  usage();
}

// Optional return duration
let returnDuration: number | undefined;
if (rawReturnDuration !== undefined) {
  returnDuration = parseFloat(rawReturnDuration);
  if (isNaN(returnDuration) || returnDuration <= 0) {
    console.error(`Invalid return-duration: ${rawReturnDuration}`);
    usage();
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Build request
// ─────────────────────────────────────────────────────────────────────────────

const request: CameraAnimateRequest = {
  sequence: [
    {
      [mode === "out" ? "zoomOut" : "zoomIn"]: {
        speedMetersPerSecond: speed,
        durationSeconds: duration,
        easing: CameraEasingType.EaseOut,
      },
    },
  ],
};

if (returnDuration !== undefined) {
  request.returnToStart = {
    durationSeconds: returnDuration,
    easing: CameraEasingType.EaseOut,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Execute
// ─────────────────────────────────────────────────────────────────────────────

async function main() {
  console.error(
    `Starting camera zoom-${mode} animation: ` +
    `speed=${speed}m/s, duration=${duration}s` +
    (returnDuration !== undefined ? `, return=${returnDuration}s` : "")
  );

  let res: Response;
  try {
    res = await fetch(`${BASE_URL}/camera/animate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
  } catch (err) {
    console.error("Network error:", err);
    process.exit(1);
  }

  let response: unknown;
  try {
    response = await res.json();
  } catch {
    console.error("Failed to parse response JSON (HTTP", res.status, ")");
    process.exit(1);
  }

  if (!res.ok) {
    const msg = typeof response === "object" && response !== null && "message" in response
      ? (response as Record<string, unknown>).message
      : `HTTP ${res.status}`;
    console.error("Error:", msg);
    process.exit(1);
  }

  console.log(JSON.stringify(response, null, 2));
}

main();
