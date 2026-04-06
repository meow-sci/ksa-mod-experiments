/**
 * types.ts — TypeScript types for the Garry's Torch RPC API.
 *
 * Derived from the OpenAPI spec at unladen-swallow.lib/openapi/garrystorch.yml.
 * Base URL: http://localhost:7887
 */

// ─────────────────────────────────────────────────────────────────────────────
// Primitives
// ─────────────────────────────────────────────────────────────────────────────

export interface Vec3 {
  x: number;
  y: number;
  z: number;
}

export type TorchEasingType = "linear" | "easeIn" | "easeOut" | "easeInOut";

export interface TorchEasingConfig {
  easing?: TorchEasingType;
  /** Exponent controlling the shape of the ease-in curve. Default: 3.0 */
  easingPowerStart?: number;
  /** Exponent controlling the shape of the ease-out curve. Default: 3.0 */
  easingPowerEnd?: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// Weld data
// ─────────────────────────────────────────────────────────────────────────────

export interface WeldData {
  position: Vec3;
  rotation: Vec3;
  /** Uniform scale factor. Valid range 0.05 – 20.0. Default: 1.0 */
  scale?: number;
  /** Whether the weld locks relative rotation between vehicles. Default: true */
  lockRotation?: boolean;
}

export interface WeldInfo {
  sourceVehicleId: string;
  targetVehicleId: string;
  position: Vec3;
  rotation: Vec3;
  scale: number;
  lockRotation: boolean;
}

// ─────────────────────────────────────────────────────────────────────────────
// Preset
// ─────────────────────────────────────────────────────────────────────────────

export interface TorchPresetInfo {
  name: string;
  position: Vec3;
  rotation: Vec3;
  scale: number;
  lockRotation: boolean;
}

// ─────────────────────────────────────────────────────────────────────────────
// Request bodies
// ─────────────────────────────────────────────────────────────────────────────

/** Provide either `data` or `presetName`, not both. */
export interface CreateWeldRequest {
  sourceVehicleId: string;
  targetVehicleId: string;
  data?: WeldData;
  presetName?: string;
}

export interface DeleteWeldRequest {
  sourceVehicleId: string;
}

/** Only fields present and non-null are applied; omitted fields are unchanged. */
export interface ModifyWeldRequest {
  sourceVehicleId: string;
  position?: Vec3;
  rotation?: Vec3;
  scale?: number;
  lockRotation?: boolean;
}

/** Provide either `data` or `presetName`, not both. */
export interface AnimateWeldRequest {
  sourceVehicleId: string;
  durationSeconds: number;
  data?: WeldData;
  presetName?: string;
  easing?: TorchEasingConfig;
}

export interface SavePresetRequest {
  name: string;
  data: WeldData;
}

export interface DeletePresetRequest {
  name: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// Response payloads
// ─────────────────────────────────────────────────────────────────────────────

export interface WeldListData {
  welds: WeldInfo[];
}

export interface WeldInfoData {
  weld: WeldInfo;
}

export interface AnimateWeldData {
  sourceVehicleId: string;
  status: string;
}

export interface MessageData {
  message: string;
}

export interface PresetListData {
  presets: TorchPresetInfo[];
}

export interface PresetInfoData {
  preset: TorchPresetInfo;
}

// ─────────────────────────────────────────────────────────────────────────────
// Envelope
// ─────────────────────────────────────────────────────────────────────────────

export type ApiResponse<T> =
  | { status: "ok"; data: T }
  | { status: "error"; message: string };
