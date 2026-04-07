/**
 * types.ts — TypeScript types for the Zippo Light Control RPC API.
 *
 * Derived from the OpenAPI spec at unladen-swallow.lib/openapi/zippo.yml.
 * Base URL: http://127.0.0.1:7887
 */

// ─────────────────────────────────────────────────────────────────────────────
// Primitives
// ─────────────────────────────────────────────────────────────────────────────

export interface ZippoColor {
  /** Red channel (0–1). */
  r: number;
  /** Green channel (0–1). */
  g: number;
  /** Blue channel (0–1). */
  b: number;
}

// Matches C# enum ZippoEasingType (System.Text.Json serializes enums as integers)
export const ZippoEasingType = {
  Linear: 0,
  EaseIn: 1,
  EaseOut: 2,
  EaseInOut: 3,
} as const;

export type ZippoEasingType = typeof ZippoEasingType[keyof typeof ZippoEasingType];

export interface ZippoEasingConfig {
  easing?: ZippoEasingType;
  /** Exponent controlling the shape of the ease-in curve. Default: 3.0 */
  easingPowerStart?: number;
  /** Exponent controlling the shape of the ease-out curve. Default: 3.0 */
  easingPowerEnd?: number;
}

/**
 * Color specification for animation endpoints.
 * Provide EITHER `rgb` OR `colorName` — not both.
 */
export interface ZippoAnimColor {
  rgb?: ZippoColor;
  /** KSAColor.Xkcd color name (case-insensitive), e.g. "HotPink", "NeonBlue". */
  colorName?: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// Light part info
// ─────────────────────────────────────────────────────────────────────────────

export interface ZippoLightPartInfo {
  partId: string;
  displayName: string;
  /** Current brightness (0–1). */
  intensity: number;
  color: ZippoColor;
  /** Whether the light is currently on. */
  isEnabled: boolean;
  /** Whether an animation is currently playing on this part. */
  isAnimating: boolean;
  /** Number of animations queued (not counting the active one). */
  queuedAnimations: number;
}

export interface ZippoLightsListData {
  vehicleId: string;
  lights: ZippoLightPartInfo[];
}

// ─────────────────────────────────────────────────────────────────────────────
// Request bodies
// ─────────────────────────────────────────────────────────────────────────────

/** Only fields present and non-null are applied; omitted fields are unchanged. */
export interface ZippoSetStateRequest {
  vehicleId: string;
  partId: string;
  /** Set color by RGB. Mutually exclusive with colorName. */
  color?: ZippoColor;
  /** Set color by KSAColor.Xkcd name (e.g. "NeonBlue"). Mutually exclusive with color. */
  colorName?: string;
  /** Set intensity (0–1). Omit to leave unchanged. */
  intensity?: number;
  /** Toggle light on/off. Omit to leave unchanged. */
  enabled?: boolean;
}

export interface ZippoAnimateRequest {
  vehicleId: string;
  partId: string;
  /** Duration of the animation in seconds. */
  durationSeconds: number;
  /** Starting color. Defaults to current part color if omitted. */
  startColor?: ZippoAnimColor;
  /** Ending color. Defaults to current part color if omitted. */
  endColor?: ZippoAnimColor;
  /** Starting intensity (0–1). Defaults to current intensity if omitted. */
  startIntensity?: number;
  /** Ending intensity (0–1). Defaults to current intensity if omitted. */
  endIntensity?: number;
  /** Easing configuration. Defaults to EaseInOut with power 3.0/3.0. */
  easing?: ZippoEasingConfig;
}

export interface ZippoClearAnimationRequest {
  vehicleId: string;
  partId: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// Response data payloads
// ─────────────────────────────────────────────────────────────────────────────

export interface ZippoSetStateData {
  partId: string;
  color: ZippoColor;
  intensity: number;
  isEnabled: boolean;
}

export interface ZippoAnimateData {
  partId: string;
  /** e.g. "queued" */
  status: string;
  /** Position in the queue (0 = now active, 1+ = waiting). */
  queuePosition: number;
}

export interface ZippoClearAnimationData {
  partId: string;
  /** e.g. "cleared" */
  status: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// API envelope
// ─────────────────────────────────────────────────────────────────────────────

export type ApiResponse<T> =
  | { status: "ok"; data: T }
  | { status: "error"; message: string };
