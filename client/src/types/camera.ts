/**
 * Camera animation API types
 * Generated from unladen-swallow.lib/openapi/camera.yml
 */

// ─────────────────────────────────────────────────────────────────────────────
// Easing Types
// Matches C# enum CameraEasingType (System.Text.Json serializes enums as integers)
// ─────────────────────────────────────────────────────────────────────────────

export const CameraEasingType = {
  Linear: 0,
  EaseIn: 1,
  EaseOut: 2,
  EaseInOut: 3,
} as const;

export type CameraEasingType = typeof CameraEasingType[keyof typeof CameraEasingType];

// ─────────────────────────────────────────────────────────────────────────────
// Animation Common Base
// ─────────────────────────────────────────────────────────────────────────────

export interface AnimationBase {
  durationSeconds: number;
  easing?: CameraEasingType;
  easingPowerStart?: number;
  easingPowerEnd?: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// Individual Animation Types
// ─────────────────────────────────────────────────────────────────────────────

export interface CameraZoomOut extends AnimationBase {
  speedMetersPerSecond: number;
}

export interface CameraZoomIn extends AnimationBase {
  speedMetersPerSecond: number;
}

export interface CameraZoomInToOffset extends AnimationBase {
  speedMetersPerSecond: number;
  offsetX: number;
  offsetY: number;
  offsetZ: number;
}

export interface CameraOrbit extends AnimationBase {
  degrees: number;
}

export interface CameraLoopyOrbit extends AnimationBase {
  degrees: number;
  loopIntervalDegrees: number;
  amplitudeMeters: number;
}

export interface CameraSpiralZoomIn extends AnimationBase {
  speedMetersPerSecond: number;
  spiralDegrees: number;
}

export interface CameraSpiralZoomOut extends AnimationBase {
  speedMetersPerSecond: number;
  spiralDegrees: number;
}

export interface CameraShake extends AnimationBase {
  shakeCount: number;
  amplitudeDegrees: number;
  shakeSpeed: number;
}

export interface CameraPan extends AnimationBase {
  offsetX: number;
  offsetY: number;
  offsetZ: number;
}

export interface CameraRotate extends AnimationBase {
  yawDegrees: number;
  pitchDegrees: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// Sequence Step
// ─────────────────────────────────────────────────────────────────────────────

export interface CameraSequenceStep {
  zoomOut?: CameraZoomOut;
  zoomIn?: CameraZoomIn;
  zoomInToOffset?: CameraZoomInToOffset;
  orbit?: CameraOrbit;
  loopyOrbit?: CameraLoopyOrbit;
  spiralZoomIn?: CameraSpiralZoomIn;
  spiralZoomOut?: CameraSpiralZoomOut;
  shake?: CameraShake;
  pan?: CameraPan;
  rotate?: CameraRotate;
  group?: CameraSequenceStep[];
}

// ─────────────────────────────────────────────────────────────────────────────
// Return to Start
// ─────────────────────────────────────────────────────────────────────────────

export interface CameraReturnToStart {
  durationSeconds?: number;
  easing?: CameraEasingType;
  easingPowerStart?: number;
  easingPowerEnd?: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// Request / Response
// ─────────────────────────────────────────────────────────────────────────────

export interface CameraAnimateRequest {
  sequence: CameraSequenceStep[];
  returnToStart?: CameraReturnToStart;
}

export interface CameraAnimateResult {
  keyframeCount: number;
  totalDurationSeconds: number;
  returnToStartEnabled: boolean;
}

export interface CameraAnimateResponse {
  status: "ok";
  data: CameraAnimateResult;
}

export interface CameraPlaybackStatus {
  state: "Stopped" | "Playing" | "Paused";
  isReturningToStart: boolean;
  currentKeyframeIndex: number;
  totalKeyframes: number;
  totalElapsedTime: number;
  totalDurationSeconds: number;
}

export interface CameraStatusResponse {
  status: "ok";
  data: CameraPlaybackStatus;
}

export interface CameraStopResult {
  previousState: "Stopped" | "Playing" | "Paused";
}

export interface CameraStopResponse {
  status: "ok";
  data: CameraStopResult;
}

// ─────────────────────────────────────────────────────────────────────────────
// Error Response
// ─────────────────────────────────────────────────────────────────────────────

export interface ErrorResponse {
  status: "error";
  message?: string;
}

export type ApiResponse<T> =
  | ({ status: "ok" } & T)
  | ErrorResponse;
