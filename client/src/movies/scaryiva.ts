#!/usr/bin/env bun
/**
 * scaryiva.ts — A mini movie sequence using Unladen Swallow RPC.
 *
 * Places kittens via Garry's Torch welding, then runs a creepy flickering
 * light animation sequence (buzz buildup, pop, darkness, flicker).
 *
 * Usage:
 *   bun run client/src/movies/scaryiva.ts
 */

import type {
  CreateWeldRequest,
  WeldInfoData,
  ApiResponse as TorchApiResponse,
} from "../garrys-torch/types.ts";
import type {
  ZippoSetStateRequest,
  ZippoSetStateData,
  ZippoAnimateRequest,
  ZippoAnimateData,
  ApiResponse as ZippoApiResponse,
} from "../zippo/types.ts";
import { ZippoEasingType } from "../zippo/types.ts";

const BASE_URL = "http://127.0.0.1:7887";

// ─────────────────────────────────────────────────────────────────────────────
// Scene data — edit these to match your save
// ─────────────────────────────────────────────────────────────────────────────

/** The vehicle that owns the light part we're animating. */
const LIGHT = {
  vehicleId: "Rocket",
  partId: "CoreIVASpaceA_Prefab_MediumCapsuleA",
} as const;

/** Target vehicle that kittens are welded onto. */
const STAGE_TARGET_VEHICLE = "Rocket";

/** Kittens placed with rotation locked (standing still, posed). */
const KITTENS_LOCKED = [
  {
    sourceId: "Hunter",
    position: { x: 4, y: 4, z: 4 },
    rotation: { x: 0, y: 0, z: 0 },
    scale: 1.0,
  },
  {
    sourceId: "Polaris",
    position: { x: 5, y: 5, z: 5 },
    rotation: { x: 0, y: 0, z: 0 },
    scale: 1.0,
  },
] as const;

/** Kitten placed with rotation unlocked (free to wobble). */
const KITTEN_UNLOCKED = {
  sourceId: "Banjo",
  position: { x: 8, y: 8, z: 8 },
  rotation: { x: 0, y: 0, z: 0 },
  scale: 0.25,
} as const;

// ─────────────────────────────────────────────────────────────────────────────
// RPC helpers
// ─────────────────────────────────────────────────────────────────────────────

async function rpc<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body ? JSON.stringify(body) : undefined,
  });
  const json = await res.json() as any;
  if (json?.status !== "ok") {
    throw new Error(`RPC ${method} ${url} failed: ${json.message ?? JSON.stringify(json)}`);
  }
  return json.data as T;
}

async function createWeld(
  sourceId: string,
  targetId: string,
  position: { x: number; y: number; z: number },
  rotation: { x: number; y: number; z: number },
  scale: number,
  lockRotation: boolean,
): Promise<void> {
  const body: CreateWeldRequest = {
    sourceVehicleId: sourceId,
    targetVehicleId: targetId,
    data: { position, rotation, scale, lockRotation },
  };
  try {
    await rpc<WeldInfoData>("POST", `${BASE_URL}/torch/welds`, body);
    console.log(`  welded ${sourceId} → ${targetId} (lock=${lockRotation})`);
  } catch (err) {
    console.warn(`[${sourceId}] weld failed: ${err instanceof Error ? err.message : String(err)}`);
  }
}

async function setLight(
  intensity?: number,
  enabled?: boolean,
): Promise<void> {
  const body: ZippoSetStateRequest = {
    vehicleId: LIGHT.vehicleId,
    partId: LIGHT.partId,
    ...(intensity !== undefined ? { intensity } : {}),
    ...(enabled !== undefined ? { enabled } : {}),
  };
  await rpc<ZippoSetStateData>("POST", `${BASE_URL}/zippo/lights/state`, body);
}

async function animateLight(
  startIntensity: number,
  endIntensity: number,
  durationSeconds: number,
  easing: ZippoEasingType = ZippoEasingType.EaseInOut,
): Promise<void> {
  const body: ZippoAnimateRequest = {
    vehicleId: LIGHT.vehicleId,
    partId: LIGHT.partId,
    durationSeconds,
    startIntensity,
    endIntensity,
    easing: { easing },
  };
  await rpc<ZippoAnimateData>("POST", `${BASE_URL}/zippo/animate`, body);
}

function wait(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// ─────────────────────────────────────────────────────────────────────────────
// Light sequence helpers
// ─────────────────────────────────────────────────────────────────────────────

/** Buzz buildup from 0.05→2.0 over 1.4s (ease-out), then instant off. */
async function lightPop(): Promise<void> {
  console.log("  light: buzz buildup 0.05→8.0 (1.4s ease-out)");
  await animateLight(0.05, 8.0, 1.4, ZippoEasingType.EaseOut);
  // Wait for the animation to finish, then turn off
  await wait(1400);
  console.log("  light: off (instant)");
  await setLight(undefined, false);
}

/** Set emissivity to 0 (light stays off). */
async function fixEmissivityZero(): Promise<void> {
  console.log("  light: fix emissivity → 0");
  await setLight(0);
}

/** Slow recovery: on, 0→0.2 over 2s ease-out. */
async function slowRecovery(): Promise<void> {
  console.log("  light: on, 0→0.2 (2s ease-out)");
  await setLight(0, true);
  await animateLight(0, 0.05, 1.5, ZippoEasingType.EaseOut);
  await wait(1500);
}

/** Fast flash: 0→0.4 over 0.1s ease-out, then off. */
async function fastFlash(): Promise<void> {
  console.log("  light: flash 0→0.4 (0.1s ease-out)");
  await setLight(0, true);
  await animateLight(0, 4.0, 0.05, ZippoEasingType.EaseOut);
  await wait(50);
  console.log("  light: off (instant)");
  await setLight(undefined, false);
}

// ─────────────────────────────────────────────────────────────────────────────
// Movie sequence
// ─────────────────────────────────────────────────────────────────────────────

async function main(): Promise<void> {
  console.log("=== scaryiva — movie start ===\n");

  // ── Act 1: Place kittens ──────────────────────────────────────────────────

  console.log("[placing kittens — locked rotation]");
  for (const k of KITTENS_LOCKED) {
    await createWeld(
      k.sourceId,
      STAGE_TARGET_VEHICLE,
      { x: k.position.x, y: k.position.y, z: k.position.z },
      { x: k.rotation.x, y: k.rotation.y, z: k.rotation.z },
      k.scale,
      true,
    );
  }

  console.log("[placing kitten — unlocked rotation]");
  await createWeld(
    KITTEN_UNLOCKED.sourceId,
    STAGE_TARGET_VEHICLE,
    { x: KITTEN_UNLOCKED.position.x, y: KITTEN_UNLOCKED.position.y, z: KITTEN_UNLOCKED.position.z },
    { x: KITTEN_UNLOCKED.rotation.x, y: KITTEN_UNLOCKED.rotation.y, z: KITTEN_UNLOCKED.rotation.z },
    KITTEN_UNLOCKED.scale,
    false,
  );

  // ── Act 2: Light sequence ─────────────────────────────────────────────────

  console.log("\n[light sequence]");

  // 1. Buzz buildup 0.1→2.0 (1.4s ease-out), then pop off
  await lightPop();

  // 2. Fix emissivity to 0 (still off)
  await fixEmissivityZero();

  // 3. Wait 1.5s
  console.log("  wait 1.5s");
  await wait(1500);

  // 4. Turn on, slowly come back up 0→0.2 over 2s ease-out
  await slowRecovery();

  // 5. Wait 2s
  console.log("  wait 2s");
  await wait(2000);

  // 6. Repeat: buzz buildup + pop off
  await lightPop();

  // 7. Fix emissivity to 0
  await fixEmissivityZero();

  // 8. Wait 1.5s
  console.log("  wait 1.5s");
  await wait(1500);

  // 9. Fast flash #1: 0→0.4 (0.1s ease-out), off
  await fastFlash();

  // 10. Wait 0.2s
  console.log("  wait 0.2s");
  await wait(200);

  // 11. Fix emissivity to 0
  await fixEmissivityZero();

  // 12. Fast flash #2
  await fastFlash();

  // 13. Wait 0.2s
  console.log("  wait 0.2s");
  await wait(200);

  // 14. Fix emissivity to 0
  await fixEmissivityZero();

  // 15. Fast flash #3
  await fastFlash();

  // 16. Wait 0.2s
  console.log("  wait 0.2s");
  await wait(200);

  // 17. Fix emissivity to 0
  await fixEmissivityZero();

  // 18. Wait 0.4s
  console.log("  wait 0.4s");
  await wait(400);

  // 19. Fix emissivity to 0 (safety reset)
  await fixEmissivityZero();

  // 20. Turn on and animate 0→0.4 over 0.2s
  console.log("  light: on, 0→0.4 (0.2s)");
  await setLight(0, true);
  await animateLight(0, 0.05, 0.2, ZippoEasingType.EaseOut);

  console.log("\n=== scaryiva — movie end ===");
}

main().catch((err) => {
  console.error("Movie failed:", err);
  process.exit(1);
});
