# Camera / View Mods — Game Integration Scope

Permanent reference for detecting when KSA game updates break the camera/view mods
(`camera-controller-override`, `glass`). Every game-facing member these mods touch is
enumerated and verified against decompiled sources.

**Verified game versions**

- NEW decomp `2026.6.9.4750` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- OLD decomp `2026.6.8.4680` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`

Paths in the **Decomp path (NEW)** column are relative to the NEW decomp root
(namespace-foldered, e.g. `KSA/Camera.cs`). **Mod code** paths are relative to the
repo root `C:\Users\Alex\repos\meow-sci\unscience`.

**How these mods are hosted (both)**

- Camera state + game reads/writes live in the `*.lib` projects (`camera-controller-override.lib`,
  `glass.lib`). Each `.lib` exposes an `ISubmod` (`MeowSci.KsaAbstractions.ISubmod`) and a
  static patch helper (`CameraControllerOverridePatches`, `GlassPatches`) consumed two ways:
  1. **Standalone** StarMap mod (`camera-controller-override/Mod.cs`, `glass/Mod.cs`) — own ImGui
     window; its own `Patcher.cs` applies the lib's patch helper.
  2. **Embedded** in the **unscience** supermod: `unscience/Mod.cs:62,66` adds
     `CameraControllerOverrideSubmod`, `unscience/Mod.cs:73` adds `GlassSubmod`; `unscience/Patcher.cs:41`
     calls `CameraControllerOverridePatches.Apply` and `unscience/Patcher.cs:47` calls `GlassPatches.Apply`.
- **Because both host paths route through the same patch helpers, every finding below applies
  identically to the standalone mod and to the unscience-embedded copy.**

**Summary of 4680 -> 4750 risk**

- **glass — NO breaking deltas.** All six game members (incl. the critical private field
  `Camera._fovRadians`) are signature-identical between OLD and NEW; only source line numbers shifted.
- **camera-controller-override — NO 4680->4750 deltas in the resolvable targets.** A long-standing
  defect — the Harmony field injector `___Transform` matched **no field** on the `KSA` controllers
  (the camera is the public field `Camera`, type `KSA.Camera : Transform3D`) — is now **FIXED (Phase 4)**:
  the prefix reads `__instance.Camera` directly. Because that injector threw at patch time (Harmony 2.4.2),
  it had also been aborting the rest of the supermod's patch chain; see the findings below.

---

## camera-controller-override

**Purpose** — Cinematic camera animation system (zoom / orbit / loopy-orbit / spiral / shake /
pan / rotate, keyframe sequences, animation groups, return-to-start). When a sequence is playing,
it intercepts the active camera controller's per-frame update and drives the camera transform
itself instead of letting the game position the camera.

**Unscience integration**

- `CameraControllerOverrideSubmod` (`camera-controller-override.lib/CameraControllerOverrideSubmod.cs`)
  owns all UI config + a `KeyframeSequencePlayer`, exposed via `SequencePlayer`.
- Patch wiring: the host sets `CameraControllerOverridePatches.SequencePlayer` then calls
  `Apply(Harmony)` (standalone: `camera-controller-override/Patcher.cs:18-20`; unscience:
  `unscience/Patcher.cs:40-41`, player wired at `unscience/Mod.cs:108`).
- RPC: `CameraControllerOverrideSubmod.Instance` (static, set in `Initialize()`) is the entry point
  `unladen-swallow.lib` uses to drive sequences over HTTP (catalogued in the RPC scope, not here).

**UI/hotkeys** — Standalone window toggled with **F11** (`camera-controller-override/Mod.cs:51`),
rendered in `OnAfterUi`. Embedded copy renders as a collapsible section inside the unscience window.
No game hotkeys are rebound; `HotkeyGuard` is applied to block game keys while typing in ImGui.

**Persistence** — None. Keyframe sequences are built at runtime via the UI and are not saved/loaded
(README lists save/load as a future idea). The unscience supermod persists only generic submod
visibility/header state, not animation data.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Reflection (AccessTools.Method) + Harmony prefix | `camera-controller-override.lib/CameraControllerOverridePatches.cs:25,29` | `KSA.OrbitController.OnFrame(Viewport inViewport, double inDeltaTime)` — `public override void` | `KSA/OrbitController.cs:375` | Yes | None (OLD `KSA/OrbitController.cs:373`) | Method target resolves. Bound via `using KSA;` → `KSA.OrbitController` (NOT `RenderCore.Input.Controllers.OrbitController`). |
| 2 | Reflection (AccessTools.Method) + Harmony prefix | `camera-controller-override.lib/CameraControllerOverridePatches.cs:26,31` | `KSA.FlyController.OnFrame(Viewport inViewport, double inDeltaTime)` — `public override void` | `KSA/FlyController.cs:417` | Yes | None (OLD `KSA/FlyController.cs:417`) | Method target resolves. Bound to `KSA.FlyController`. |
| 3 | Harmony arg injection (`__instance`) | `camera-controller-override.lib/CameraControllerOverridePatches.cs:42` | `KSA.Controller` (base of both controllers) | `KSA/Controller.cs:8` | Yes | None (OLD `KSA/Controller.cs:8`) | Base type unchanged; `__instance` typing OK. |
| 4 | Harmony arg injection (by param NAME) | `camera-controller-override.lib/CameraControllerOverridePatches.cs:42` | `OnFrame` parameter `double inDeltaTime` | `KSA/OrbitController.cs:375`, `KSA/FlyController.cs:417` | Yes | None | Param name `inDeltaTime` matches in both controllers/versions. |
| 5 | Direct field read (`__instance.Camera`) — **FIXED (Phase 4)** | `camera-controller-override.lib/CameraControllerOverridePatches.cs:42,55` | `KSA.Controller.Camera : Camera` (public field; `KSA.Camera : Transform3D`) | `KSA/Controller.cs:12` | Yes | None | Was a Harmony field injector `Transform3D ___Transform` binding to a **non-existent** field (the camera is `Camera`, not `Transform`; a `Transform` field exists only on the unrelated `RenderCore.Input.Controllers.CameraController`). Harmony 2.4.2 validates injected field names at patch time, so `harmony.Patch` **threw** → the prefix never attached AND the throw aborted the rest of the supermod chain. Now the prefix reads `__instance.Camera` (a `Transform3D`) directly and passes it to `Update`. |
| 6 | Direct typed API (read chain) | `camera-controller-override.lib/Animation/AnimationHelpers.cs:33` | `KSA.Controller.Camera` (field) → `KSA.Camera.Following` (`IFollowable?` prop) → `IFollowable.GetPositionEcl() : double3` | `KSA/Controller.cs:12`, `KSA/Camera.cs:140`, `KSA/IPosition.cs:7` | Yes | None (OLD `KSA/Camera.cs:139`; `Following`/`GetPositionEcl` unchanged) | Target-tracking. `GetPositionEcl` is on `IPosition` (base of `IFollowable`). Live now that #5 is fixed. |
| 7 | Direct typed API (static) | `AnimationHelpers.cs:46`, `Animation/Animations/SpiralZoomOutAnimation.cs:127`, `Animation/Animations/SpiralZoomInAnimation.cs:136` | `KSA.Camera.LookAtRotation(double3 forwardEcl, double3 upEcl) : doubleQuat` — `public static` | `KSA/Camera.cs:180` | Yes | None (OLD `KSA/Camera.cs:179`) | Static helper, compile-time bound. Signature identical. |
| 8 | Direct typed API (read/write) | `camera-controller-override.lib/Animation/KeyframeSequencePlayer.cs:450,473` + all `Animation/Animations/*` | `KSA.Transform3D.PositionEcl { get; set; } : double3` — `public virtual` | `KSA/Transform3D.cs:15` | Yes | None | Mutates the controller's `Camera` (a `Transform3D`) by reference to move the camera. `Camera` overrides `PositionEcl` (`KSA/Camera.cs:94`). Live now that #5 is fixed. |
| 9 | Direct typed API (read/write) | `KeyframeSequencePlayer.cs:451,476,477` + `Animation/Animations/*` | `KSA.Transform3D.LocalRotation : doubleQuat` — `public` field | `KSA/Transform3D.cs:13` | Yes | None | Mutates the controller's `Camera` by reference to rotate the camera. Live now that #5 is fixed. |
| 10 | Harmony patch (shared, required) | `camera-controller-override/Patcher.cs:20` (+ `unscience/Patcher.cs` via HotkeyGuard) | `MeowSci.KsaAbstractions.HotkeyGuard.Patch(Harmony)` | n/a (abstraction lib; targets game input — see HotkeyGuard scope) | Yes | n/a | Blocks game hotkeys while typing in ImGui. Per-mod requirement. |
| 11 | Lifecycle (StarMap attributes) | `camera-controller-override/Mod.cs:19,22,38,45,60` | `StarMap.API` `[StarMapMod]`, `[StarMapImmediateLoad]`, `[StarMapAllModsLoaded]`, `[StarMapBeforeGui]`, `[StarMapAfterGui]`, `[StarMapUnload]` | StarMap library | Yes | n/a | Standalone load lifecycle. |

**Game assets referenced** — None. The mod only manipulates camera transforms via math (`Brutal.Numerics`); it loads no textures, models, shaders, or `Content/` files.

**Update-risk findings (5117 → 5261)**

- ✅ **No breaking deltas.** `RenderCore.Input.Controllers/Controller.cs` is **byte-identical** between
  OLD (5168) and NEW (5261), including the public `Camera` field the prefix reads.
  `OrbitController.OnFrame` and `FlyController.OnFrame` both still resolve by string.
- ✅ **glass** — `Camera._fovRadians` (the single most important glass check), `ChangeFieldOfView` and
  `UpdateProjection` all still resolve on `KSA/Camera.cs`.
- ✅ The `___Transform` field injector is **retired**, so the historic `Apply`-time throw (which used
  to abort the rest of the supermod's patch chain) cannot recur. The master index's §4/§6 entries
  still described it as broken; corrected this pass to match what `camera-controller-override.lib`
  actually does.
- ⚠️ **Camera behavioral watch items (compile-clean, need a live pass):** docking-camera orientation
  no longer baked at toggle time (rev 5191), docking camera un-flipped (5222), 0.1 m stand-off from
  the docking ring + reticle from actual viewport size (5255), portrait-camera FOV (5195) and a
  temporary side view for kittens on ladders (5245). These change what the camera *does* without
  moving a symbol glass or camera-controller-override binds to.

**Update-risk findings (4680 -> 4750)**

- **No 4680->4750 deltas in any resolvable target.** `OrbitController.OnFrame`, `FlyController.OnFrame`,
  base `Controller` (incl. the `Camera` field), `Camera.Following`, `Camera.LookAtRotation`,
  `IPosition.GetPositionEcl`, and `Transform3D.PositionEcl`/`LocalRotation` are all signature-identical
  between OLD and NEW; only line numbers shifted.
- **FIXED (Phase 4) — `___Transform` field injector (#5).** No `Transform` field exists on
  `KSA.Controller`/`OrbitController`/`FlyController` in **either** 4680 or 4750 (the camera is the public
  field `Camera`, `KSA.Camera : Transform3D`). The prefix now reads `__instance.Camera` directly, so the
  animation drives the live camera. **Secondary impact this also fixed:** Harmony 2.4.2 validates injected
  `___` field names at patch time and **throws** when none binds — so `CameraControllerOverridePatches.Apply`
  threw inside `unscience/Patcher.cs`, and because that call sat mid-chain, every feature applied *after* it
  (eternal-flame, glass, i-feel-seen, vehicle-paint, engine-emissive, flexo) silently failed to patch in the
  supermod. The supermod patch chain is now also hardened so any single feature's apply failure is isolated
  (logged + skipped) rather than aborting the rest. (`Transform` is the correct member name only on the
  unrelated `RenderCore.Input.Controllers.CameraController` family.)
- **Two controller families share class names — binding hazard.** `OrbitController`/`FlyController` exist
  in BOTH `KSA` (base `KSA.Controller`, `OnFrame(Viewport,double)`) and `RenderCore.Input.Controllers`
  (base `CameraController`, `OnFrame(double)`, and *does* have a `Transform` member). The mod's
  `using KSA;` selects the `KSA` family. If a future update moves the live camera controllers to (or
  resolves `using` toward) the `RenderCore` family, both the patch target signature and the `Transform`
  semantics change — re-verify which family the game actually instantiates for the flight camera.
- **Brutal package dependency (low).** Animations rely on `Brutal.Numerics` (`double3`, `doubleQuat`,
  and the `KSA.Transform3D` math). Changelog 4729 updated Brutal packages; the referenced types/members
  are still present and used by the game itself, so risk is low — but a `Transform3D`/numerics API change
  would surface here.

---

## glass

**Purpose** — Camera field-of-view control: 8 photographic lens presets plus a manual FOV slider
(clamped 1°–179°). When the override is active it forces the camera FOV every frame and blocks the
game's own FOV input.

**Unscience integration**

- FOV state + logic live in `glass.lib/FovController.cs` (static), so other projects can drive FOV by
  referencing `glass.lib` without the `glass` mod. `GlassSubmod` (`glass.lib/GlassSubmod.cs`) is the
  `ISubmod` UI; its `Update(dt)` calls `FovController.ApplyFov()` each frame.
- Patch wiring: `GlassPatches.Apply(Harmony)` (standalone: `glass/Patcher.cs:15`; unscience:
  `unscience/Patcher.cs:47`). `GlassSubmod` added at `unscience/Mod.cs:73`.
- RPC: `unladen-swallow.lib` controls FOV through `glass.lib`'s `FovController` (catalogued in RPC scope).

**UI/hotkeys** — Standalone window toggled with **F9** (`glass/Mod.cs:51`). Embedded copy is a
collapsible section in the unscience window. `HotkeyGuard` applied. The game's own +/- FOV keys (which
call `Camera.ChangeFieldOfView`, `KSA/Camera.cs:769,774`) are suppressed by the prefix while override is active.

**Persistence** — None. `FovController` state (`IsOverrideActive`, `OverrideFovDegrees`) is runtime-only;
`GlassSubmod.Dispose()` calls `FovController.DisableOverride()` to hand control back to the game on unload.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | **Reflection (AccessTools.Field — PRIVATE)** | `glass.lib/GlassPatches.cs:20` | `KSA.Camera._fovRadians` — `private float` | `KSA/Camera.cs:47` | **Yes** | **None** (OLD `KSA/Camera.cs:46`, same `private float _fovRadians`) | **Single most important check — PASSED.** String-based private field; a rename would silently break all FOV control with no compile error. Name + type unchanged 4680→4750. |
| 2 | Reflection (private field write, `SetValue`) | `glass.lib/GlassPatches.cs:62` | `KSA.Camera._fovRadians` (write target FOV in radians each frame) | `KSA/Camera.cs:47` | Yes | None | Same field as #1; written in `UpdateProjectionPrefix` so the very next projection build uses the override. |
| 3 | Reflection (AccessTools.Method) + Harmony prefix (skips original) | `glass.lib/GlassPatches.cs:25,28` | `KSA.Camera.ChangeFieldOfView(float change)` — `public void` | `KSA/Camera.cs:418` | Yes | None (OLD `KSA/Camera.cs:417`) | Prefix returns `false` (skip) when override active, so the game cannot change FOV. Public method. |
| 4 | Reflection (AccessTools.Method) + Harmony prefix (`void`, runs-before) | `glass.lib/GlassPatches.cs:26,29` | `KSA.Camera.UpdateProjection()` — `public void` | `KSA/Camera.cs:434` | Yes | None (OLD `KSA/Camera.cs:433`) | `void` prefix injects `_fovRadians` then lets original rebuild the projection matrix (`CreatePerspectiveFieldOfViewReverseZ`). |
| 5 | Direct typed API (static + instance) | `glass.lib/FovController.cs:42` | `KSA.Program.GetCamera() : Camera` (static) + `KSA.Camera.GetFieldOfView() : float` (returns RADIANS) | `KSA/Program.cs:504`, `KSA/Camera.cs:702` | Yes | None (OLD `KSA/Program.cs:503`, `KSA/Camera.cs:689`) | Reads live FOV for the UI display. Compile-time bound. |
| 6 | Direct typed API (instance) | `glass.lib/FovController.cs:55` | `KSA.Camera.SetFieldOfView(float fovDegrees)` — `public void` (param is DEGREES; converts to radians internally) | `KSA/Camera.cs:402` | Yes | None (OLD `KSA/Camera.cs:401`) | Called from `ApplyFov()` on the game thread. Note the asymmetry: setter takes **degrees**, getter (#5) returns **radians**. |
| 7 | Harmony patch (shared, required) | `glass/Patcher.cs:15` (+ unscience via HotkeyGuard) | `MeowSci.KsaAbstractions.HotkeyGuard.Patch(Harmony)` | n/a (abstraction lib) | Yes | n/a | Per-mod requirement. |
| 8 | Lifecycle (StarMap attributes) | `glass/Mod.cs:19,22,38,45,60` | `StarMap.API` load/gui/unload attributes | StarMap library | Yes | n/a | Standalone load lifecycle. |

**Game assets referenced** — None. Pure projection-math / field manipulation; no `Content/` assets.

**Update-risk findings (4680 -> 4750)**

- **No breaking deltas detected.** All six game members are signature-identical between 4680 and 4750;
  only line numbers shifted as `Camera.cs` grew.
- **Critical verification PASSED:** the private field `KSA.Camera._fovRadians` (`private float`) exists and
  is unchanged (OLD `KSA/Camera.cs:46` → NEW `KSA/Camera.cs:47`). A future rename of this field is the
  single highest-risk break (string-based, no compile error, FOV silently stops responding) — re-check
  this field name on every game update.
- **Standing string-resolution risk:** `ChangeFieldOfView` and `UpdateProjection` are resolved by name via
  `AccessTools.Method`; renames/overload changes would break at patch time (caught only at runtime, not
  compile time). `Program.GetCamera`, `Camera.GetFieldOfView`, and `Camera.SetFieldOfView` are typed
  (compile-time) and would fail the build if changed — lower risk.
- `KSA.Camera` remains in the `KSA` namespace (file `KSA/Camera.cs`) in both versions; note an unrelated
  `Brutal.GltfApi.Camera` also exists — the typed `using KSA;` keeps the binding unambiguous.

---

## Area summary — Update-risk findings (5261 → 5348)

- ✅ **`KSA/Camera.cs` is byte-identical between 5261 and 5348.** glass is completely clean this span:
  `Camera._fovRadians` (private field, by name), `Camera.ChangeFieldOfView(float)` and
  `Camera.UpdateProjection()` all resolve exactly as before.
- ✅ **camera-controller-override clean.** `AccessTools.Method(typeof(OrbitController), "OnFrame")` and
  the `FlyController` equivalent both still resolve; the `___Transform` field-injector bug stays
  **retired** — the prefix reads the public `__instance.Camera`
  (`CameraControllerOverridePatches.cs:42-54`).
- ✅ **Coordinate frames unchanged.** Rev 5280's `CelestialFrameMath` extraction preserved every public
  `Celestial` frame accessor, so camera-controller-override's ego-space math is unaffected.
- ℹ️ **New camera surface this span, none of it breaking:** per-kitten crew-portrait cameras with a FOV
  slider and Head/Neck/Chest bone targeting (revs 5270/5273), a resizable kitten-cam gauge (5297),
  portrait cameras skipped when not visible (5295), stars no longer rendered in kitten cams (5292), and
  `ViewportLightModes` so each viewport picks its own light path (5301). If glass's FOV override ever
  needs to apply per-viewport rather than globally, these are the seams to look at.
