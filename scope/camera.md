# Camera / View Mods — Game Integration Scope

## Workspace integration (current)

Active bundled features: **camera-controller-override, glass, hot-pursuit**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

Camera animation authoring uses tagged AnimationRecipe data and a separate KeyframeSequencePlayer from the one supplied to Harmony. Glass applies its detached FOV only on Apply. Hot Pursuit stores next-camera settings; active viewport leases, mount transforms and players are inspected in Live State. Loading cancels uncommitted placement gestures without releasing an existing viewport or altering playback. Existing IViewport/ViewportRegistry gates remain.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break the camera/view mods
(`camera-controller-override`, `glass`, `hot-pursuit`). Every game-facing member these mods touch is
enumerated and verified against decompiled sources.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard remains in `unscience/Patcher.cs`; feature Harmony groups are registered by their owning libraries through `ConfigureRuntime`. See [architecture](00-architecture-and-abstractions.md).

## camera-controller-override

**Purpose** — Cinematic camera animation system (zoom / orbit / loopy-orbit / spiral / shake /
pan / rotate, keyframe sequences, animation groups, return-to-start). When a sequence is playing,
it intercepts the active camera controller's per-frame update and drives the camera transform
itself instead of letting the game position the camera.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — All animation parameters; ordered recipes, parallel groups, return-to-start settings and selected keyframe. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Reflection (AccessTools.Method) + Harmony prefix | `camera-controller-override.lib/CameraControllerOverridePatches.cs` | `KSA.OrbitController.OnFrame(IViewport inViewport, double inDeltaTime)` — `public override void` | `KSA/OrbitController.cs` | Yes | **Retyped @5402** — first param `Viewport` → `IViewport` (OLD `KSA/OrbitController.cs`); still the only `OnFrame` overload, so string resolution stays unambiguous | Method target resolves. Bound via `using KSA;` → `KSA.OrbitController` (NOT `RenderCore.Input.Controllers.OrbitController`). The prefix never names the viewport param, so the retype is invisible to it. |
| 2 | Reflection (AccessTools.Method) + Harmony prefix | `camera-controller-override.lib/CameraControllerOverridePatches.cs` | `KSA.FlyController.OnFrame(IViewport inViewport, double inDeltaTime)` — `public override void` | `KSA/FlyController.cs` | Yes | **Retyped @5402** — `Viewport` → `IViewport` (OLD `KSA/FlyController.cs`); only `OnFrame` overload (`OnFrameEditor` at `:742` is a different name) | Method target resolves. Bound to `KSA.FlyController`. |
| 3 | Harmony arg injection (`__instance`) | `camera-controller-override.lib/CameraControllerOverridePatches.cs` | `KSA.Controller` (base of both controllers) | `KSA/Controller.cs` | Yes | None (`Controller.OnFrame` virtual retyped to `IViewport` at `:62`; class otherwise identical) | Base type unchanged; `__instance` typing OK. |
| 4 | Harmony arg injection (by param NAME) | `camera-controller-override.lib/CameraControllerOverridePatches.cs` | `OnFrame` parameter `double inDeltaTime` | `KSA/OrbitController.cs`, `KSA/FlyController.cs` | Yes | None | Param name `inDeltaTime` matches in both controllers/versions. The prefix deliberately does not bind `inViewport`, which is why the 5402 `Viewport` → `IViewport` retype needed no mod change. |
| 5 | Direct field read (`__instance.Camera`) — **FIXED (Phase 4)** | `camera-controller-override.lib/CameraControllerOverridePatches.cs` | `KSA.Controller.Camera : Camera` (public field; `KSA.Camera : Transform3D`) | `KSA/Controller.cs` | Yes | None | Was a Harmony field injector `Transform3D ___Transform` binding to a **non-existent** field (the camera is `Camera`, not `Transform`; `KSA/Controller.cs` contains no `Transform` member in 5348 or 5402 — a `Transform` field exists only on the unrelated `RenderCore.Input.Controllers.CameraController`). Harmony 2.4.2 validates injected field names at patch time, so `harmony.Patch` **threw** → the prefix never attached AND the throw aborted the rest of the supermod chain. Now the prefix reads `__instance.Camera` (a `Transform3D`) directly and passes it to `Update`. |
| 6 | Direct typed API (read chain) | `camera-controller-override.lib/Animation/AnimationHelpers.cs` | `KSA.Controller.Camera` (field) → `KSA.Camera.Following` (`IFollowable?` prop) → `IFollowable.GetPositionEcl() : double3` | `KSA/Controller.cs`, `KSA/Camera.cs`, `KSA/IPosition.cs` | Yes | None (OLD `KSA/Camera.cs`; `Following`/`GetPositionEcl` unchanged; `IPosition.cs` byte-identical) | Target-tracking. `GetPositionEcl` is on `IPosition` (base of `IFollowable`). Live since the Phase 4 fix of #5. |
| 7 | Direct typed API (static) | `AnimationHelpers.cs`, `Animation/Animations/SpiralZoomOutAnimation.cs`, `Animation/Animations/SpiralZoomInAnimation.cs` | `KSA.Camera.LookAtRotation(double3 forwardEcl, double3 upEcl) : doubleQuat` — `public static` | `KSA/Camera.cs` | Yes | None (OLD `KSA/Camera.cs`) | Static helper, compile-time bound. Signature identical. |
| 8 | Direct typed API (read/write) | `camera-controller-override.lib/Animation/KeyframeSequencePlayer.cs` + all `Animation/Animations/*` | `KSA.Transform3D.PositionEcl { get; set; } : double3` — `public virtual` | `KSA/Transform3D.cs` | Yes | None (file byte-identical) | Mutates the controller's `Camera` (a `Transform3D`) by reference to move the camera. `Camera` overrides `PositionEcl` (`KSA/Camera.cs`). Live since the Phase 4 fix of #5. |
| 9 | Direct typed API (read/write) | `KeyframeSequencePlayer.cs` + `Animation/Animations/*` | `KSA.Transform3D.LocalRotation : doubleQuat` — `public` field | `KSA/Transform3D.cs` | Yes | None (file byte-identical) | Mutates the controller's `Camera` by reference to rotate the camera. Live since the Phase 4 fix of #5. |
| 10 | Harmony patch (shared, required) | `unscience/Patcher.cs` (+ `unscience/Patcher.cs` via HotkeyGuard) | `MeowSci.KsaAbstractions.HotkeyGuard.Patch(Harmony)` | n/a (abstraction lib; targets game input — see HotkeyGuard scope) | Yes | n/a | Blocks game hotkeys while typing in ImGui. Per-mod requirement. |
| 11 | Lifecycle (StarMap attributes) | `unscience/Mod.cs` | `StarMap.API` `[StarMapMod]`, `[StarMapImmediateLoad]`, `[StarMapAllModsLoaded]`, `[StarMapBeforeGui]`, `[StarMapAfterGui]`, `[StarMapUnload]` | StarMap library | Yes | n/a | Standalone load lifecycle. |

**Game assets referenced** — None. The mod only manipulates camera transforms via math (`Brutal.Numerics`); it loads no textures, models, shaders, or `Content/` files.

## hot-pursuit

**Purpose** — Click a rendered vehicle part and mount a live camera to the exact hit sub-part.
Each entry leases one of KSA's four preallocated secondary viewports and exposes part-local XYZ
translation, mount-relative pitch/yaw/roll, FOV, resolution, visibility and lease controls.

**Feasibility boundary** — KSA creates main + thumbnail + four secondary + two portrait viewports
at boot, exhausting `ViewportRegistry.MAX_VIEWPORTS == 8`, then seals allocation. Hot Pursuit does
not reflect into allocation or create GPU resources: it uses the supported public secondary-lease
API. Therefore at most four Hot Pursuit cameras can render, less any slots held by stock Add Camera,
docking cameras, or another mod.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**Placement and coordinate model**

- Placement is a one-shot world click using `Cursor.GetEgoRay(Program.MainViewport)`. It sweeps all
  live vehicles (including debris), calls the same mesh-precise `Part.RayCastEgo` used by KSA hover
  picking, and stores the returned closest sub-part, position, and normal. The latter two are in the
  hit sub-part's local assembly frame.
- The initial mount point is 0.15 m along the local hit normal. A tangent derived from local +Y (or
  +X fallback) completes the outward-facing basis.
- A `FixedController.OnFrame` prefix checks whether the passed viewport currently belongs to Hot
  Pursuit and skips stock fixed-camera math only for those views. It composes the mount through
  `Part.MatrixAsmb2Ego`, converts the point from
  the main camera's ego frame to ECL, transforms tangents normally and the surface normal by the
  inverse-transpose (then re-orthogonalizes the basis for non-uniform scale), then
  writes `Camera.PositionEcl` and `WorldRotation`. Immediately after writing `PositionEcl`, it
  applies the same public terrain clamp that `Camera.OnFrame` is about to apply, then
  `HotPursuitCelestialState.Synchronize` calls `Program.FindNearbyCelestial` for that secondary
  camera, mirrors KSA's 80,000 km surface-distance rejection, and fills the camera's public
  distance, terrain-height, and current-altitude fields. This prevents the nearby body from also
  entering `StaticCelestialDistanceRendering` as a distant sphere. The prefix runs before
  `GameViewport.OnFrame` calls `Camera.OnFrame`, so view/frustum data is baked from the current
  part pose without UI-hook lag.

**Integration points**

| # | Kind | Mod code | Game target (Type.Member + signature) | Decomp path (NEW) | Risk/notes |
|---|------|----------|----------------------------------------|-------------------|------------|
| 1 | Harmony selective prefix (explicit signature; typed arg) | `hot-pursuit.lib/HotPursuitPatches.cs` | `FixedController.OnFrame(IViewport,double)` | `KSA/FixedController.cs` | Keystone timing seam. Returns false only for a registry-confirmed owned viewport; missing/stale leases run stock. Caller still invokes `Camera.OnFrame` immediately afterward. |
| 2 | Direct viewport lease API | `HotPursuitSubmod.cs` | `ViewportRegistry.{AvailableSecondaryCount,TryClaimSecondaryViewport(IViewportOwner,out IGameViewport),TryGetOwned,ReleaseSecondaryViewport(IViewportOwner)}` | `KSA/ViewportRegistry.cs` | Four shared leases; reset-on-claim/release means all settings are reapplied. Allocation is sealed and capped at 8. |
| 3 | Direct viewport configuration | `HotPursuitSubmod.cs`, `.Ui.cs` | `IGameViewport.{SetName,SetCameraMode,SetResizeAllowed,RequestResize,SetVisible,BaseCamera}`; `CameraMode.Fixed` | `KSA/IGameViewport.cs`; `KSA/IViewport.cs`; `KSA/CameraMode.cs` | Stock render targets and ImGui window. Closing `DrawImGui` releases the lease. |
| 4 | Direct camera API | `HotPursuitSubmod.cs`, `HotPursuitPose.cs`, `HotPursuitCelestialState.cs` | `Camera.{SetFollow(...changeControl:false),SetFieldOfView(float),PositionEcl,WorldRotation,LookAtRotation,ClampCamera,NearbyCelestial,DistanceToNearbyCelestialKm,DistanceToNearbyCelestialSurfaceMeanKm,NearbyCelestialTerrainHeight,CurrentAltitudeKm}` | `KSA/Camera.cs` | `changeControl:false` is load-bearing: true would change the controlled vehicle. Hot Pursuit applies the public 0.5 m AGL terrain clamp before synchronizing celestial metrics; `Camera.OnFrame` repeats the now-idempotent clamp immediately afterward. |
| 5 | Direct picking API | `HotPursuitPicker.cs` | `Cursor.GetEgoRay(IViewport)`; `Part.RayCastEgo(...)`; `Vehicle.{BoundingSphereRadiusBody,GetMatrixAsmb2Ego(Camera)}` | `KSA/Cursor.cs`; `KSA/Part.cs`; `KSA/Vehicle.cs` | Same-frame main cursor ray. Hit position/normal belong to the returned closest sub-part, not necessarily the top-level part. Terrain is deliberately unsupported. |
| 6 | Direct part transform/addressing | `HotPursuitCamera.cs`, `HotPursuitPose.cs`, `HotPursuitSubmod.cs` | `Part.{InstanceId,SubParts,MatrixAsmb2Ego}`; `Vehicle.Id`; `Universe.CurrentSystem` via `VehicleProvider` | `KSA/Part.cs`; `KSA/Astronomical.cs` | Stable id re-resolution makes missing/failed/staged targets dormant rather than dereferencing destroyed objects. Full matrix includes scale and articulated sub-part parents. |
| 7 | Direct nearby-celestial lookup | `hot-pursuit.lib/HotPursuitCelestialState.cs` | `Program.FindNearbyCelestial(Camera)` | `KSA/Program.cs` | KSA's `OnFrameCelestials` updates only `Program.GetCamera()` (the main/frame camera); Hot Pursuit explicitly performs the equivalent lookup for its owned secondary camera after `PositionEcl`. |
| 8 | Lifecycle/shared input guard | `unscience/Mod.cs`, `Patcher.cs`; unscience host | StarMap attributes; `HotkeyGuard` | StarMap / abstraction | No game assets or custom shaders. |

**Compatibility and live-test risks**

- Glass now scopes its reflection-based FOV injection and direct FOV application to the main
  viewport cameras. This preserves Hot Pursuit's per-camera FOV; re-check both together after camera
  changes.
- `Camera.OnFrame` calls terrain clamping, so a hull mount below 0.5 m AGL will be raised.
- Secondary viewports are substantial additional scene renders and expensive, but KSA 5402's
  `Program.RenderViewport` does not run `ParticleSystem`, `VolumetricExhaustRenderer`, the main
  planet/ocean/cloud pipeline, part-glass, or overall-bloom passes. Engine plumes and generic
  particles are therefore absent by design; those game-owned passes bind main-camera
  targets/resources and are not safe to inject from Hot Pursuit. Other effects/shadows are not
  guaranteed identical to the main view; verify the nearby-body artifact, vehicles, night lighting,
  scaled/robotic parts, target destruction/debris handoff, closing/reopening, and simultaneous
  docking cameras live.
- No reflection, assets, shaders, or custom Vulkan resources are used by Hot Pursuit itself.

---

## glass

**Purpose** — Camera field-of-view control: 8 photographic lens presets plus a manual FOV slider
(clamped 1°–179°). When the override is active it forces the camera FOV every frame and blocks the
game's own FOV input.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — FOV and lens preset selection. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | **Reflection (AccessTools.Field — PRIVATE)** | `glass.lib/GlassPatches.cs` | `KSA.Camera._fovRadians` — `private float _fovRadians = 0.87266463f` | `KSA/Camera.cs` | **Yes** | **None** (OLD `KSA/Camera.cs`, identical declaration) | **Single most important check — PASSED (5402).** String-based private field; a rename would silently break all FOV control with no compile error. Name + type unchanged through every build since 4680. |
| 2 | Reflection (private field write, `SetValue`) | `glass.lib/GlassPatches.cs` | `KSA.Camera._fovRadians` (write target FOV in radians) | `KSA/Camera.cs` | Yes | behavior narrowed for Hot Pursuit | `UpdateProjectionPrefix` now first checks `ViewportRegistry.IsMainCamera(__instance)`, so secondary/portrait/thumbnail cameras retain their independent projection. |
| 3 | Reflection (AccessTools.Method) + Harmony prefix (skips original) | `glass.lib/GlassPatches.cs` | `KSA.Camera.ChangeFieldOfView(float change)` — `public void` | `KSA/Camera.cs` | Yes | behavior narrowed for Hot Pursuit | Prefix blocks stock FOV input only for a main viewport camera while override is active. |
| 4 | Reflection (AccessTools.Method) + Harmony prefix (`void`, runs-before) | `glass.lib/GlassPatches.cs` | `KSA.Camera.UpdateProjection()` — `public void` | `KSA/Camera.cs` | Yes | None (OLD declaration identical) | Main-camera-only prefix injects `_fovRadians`, then original rebuilds the projection matrix. |
| 5 | Direct typed API (static + instance) | `glass.lib/FovController.cs` | `KSA.Program.GetMainCamera() : Camera` + `KSA.Camera.GetFieldOfView() : float` (RADIANS) | `KSA/Program.cs`, `KSA/Camera.cs` | Yes | switched from frame camera | Reads/applies the player's main lens explicitly so a frame-scoped secondary camera cannot be stomped. |
| 6 | Direct typed API (instance) | `glass.lib/FovController.cs` | `KSA.Camera.SetFieldOfView(float fovDegrees)` — `public void` (param is DEGREES; converts to radians internally) | `KSA/Camera.cs` | Yes | None (OLD `KSA/Camera.cs`) | Called from `ApplyFov()` on the game thread. Note the asymmetry: setter takes **degrees**, getter (#5) returns **radians**. |
| 7 | Harmony patch (shared, required) | `unscience/Patcher.cs` (+ unscience via HotkeyGuard) | `MeowSci.KsaAbstractions.HotkeyGuard.Patch(Harmony)` | n/a (abstraction lib) | Yes | n/a | Per-mod requirement. |
| 8 | Lifecycle (StarMap attributes) | `unscience/Mod.cs` | `StarMap.API` load/gui/unload attributes | StarMap library | Yes | n/a | Standalone load lifecycle. |

**Game assets referenced** — None. Pure projection-math / field manipulation; no `Content/` assets.

## Historical evidence

See [dated integration and upgrade reference](history/camera.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.

## Current runtime release behavior

FOV activation captures the actual camera’s prior lens, including stock zoom. Disable/unload restores it; changing main cameras restores the outgoing camera before capturing the new one. FOV and preset-index inputs are validated before draft restoration. Camera hooks exist only during playback. Stop releases camera control; teardown removes only this feature’s hooks. Release cancels placement and releases every secondary viewport lease. Camera hooks are present only while live cameras exist.

Feature hook targets retain their existing signatures; patch ownership now follows explicit demand through the shared runtime coordinator. Native acceptance remains outstanding.
