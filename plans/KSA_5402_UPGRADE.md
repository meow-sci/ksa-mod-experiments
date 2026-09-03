# KSA `2026.9.7.5402` upgrade — impact review & remediation record

**Reviewed:** 2026-09-02 · **Span:** `2026.8.22.5348` (recorded baseline) → `2026.9.7.5402`
**Trees:** NEW `ksa-game-assemblies` @ `2026.9.7.5402`; OLD `ksa-game-assemblies_prev` @ `2026.8.22.5348`
**Host:** macOS. `Directory.Build.props` tier 2 resolves `KSAFolder` to
`../ksa-game-assemblies/current/dll/` — **the build compiled against NEW (5402)**, not a separate
install. No `KSAFolder` trap this pass.

> ⚠ **The changelog is NOT complete for this span.** `<BASELINE>` (`5348`) == `<OLD>.build`, so this
> is a single hop — but NEW's `version.json` covers only `fromRevision: 5400 → toRevision: 5402` and
> logs **one** commit (rev 5401, *"Fixed crash for incorrect data stride for thumbnail rendering"*).
> Revisions **5349–5400 are in no `version.json` on disk.** Everything below therefore comes from the
> **source diff** (`diff -rq` of both decomp trees: 197 `KSA/*.cs` files changed, 66 added, 2 removed;
> 20 Content files changed), not from a changelog.

---

## 1. Result

`dotnet build ksa-mod-experiments.slnx --no-incremental` → **63/63 projects, 0 warnings, 0 errors**
against the 5402 reference DLLs (the suite is 63 projects since pyro, graffiti, rocky-mcrock-face and
bloomin-onion were added after the 5348 pass).

**Three compile breaks, in six files across five libs — all fixed in this pass** (§2). All three are
typed API changes from one unlogged refactor (the viewport system) plus one from the exhaust-plume
rework. Nothing else in the suite moved: **every string-reflection entry, every Harmony patch target,
both GPU byte layouts and every shader anchor verified unchanged** (§5).

The residual risk is **behavioral** and concentrated in the new game systems that landed unlogged
(§4): parachutes with a cloth solver, **part structural failure / debris**, plume bend/fold
deformation, and the viewport registry.

| # | Mod / lib | Change class | New in 5402? | Verdict |
|---|---|---|---|---|
| 1 | ksa-abstractions, dont-stifle-me, i-feel-seen, parts-now, graffiti | **removed type** `KSA.Viewport` → `IViewport`/`IGameViewport`; `Viewport.Index` → `ShaderSlot` | ✅ yes (unlogged) | **fixed** — 6 one-line retypes |
| 2 | graffiti | **removed member** `Cursor.InputRay` → `Cursor.GetEgoRay(IViewport)` | ✅ yes (unlogged) | **fixed** |
| 3 | pyro | **re-signatured** `VolumetricExhaustRenderer.AddInstance(…, float throttle)` → `(…, float throttle, float3 airVelocity, float airDensity) : float` | ✅ yes (unlogged) | **fixed** — mirrors `Vehicle.AddVolumetricExhaustInstances` |
| 4 | pyro (and the game itself) | **game-side regression** — the exhaust refraction pass can never enable in 5402 | ✅ yes (unlogged) | needs live confirmation; no mod change |
| 5 | garrys-torch | new-gating — **part structural failure / debris** can now fire on welded (overlapping) vehicles | ✅ yes (unlogged) | needs live re-verification; hardening recommended |
| 6 | graffiti | semantic drift — `Celestial.GetTerrainHeightFromDirCcf(accurate:true)` now derives from `MeanRadius`, not `RenderData.SurfaceRadius` | ✅ yes (unlogged) | needs live terrain-decal check |
| 7 | IvaForceRender (ksa-abstractions) | new-gating — `PartModel.AddInstance` early-returns for viewports without `RenderPartModels`; the postfix still runs | ✅ yes (unlogged) | dead today (every viewport has the flag); defensive mirror recommended |
| 8 | thug-life | render env — `RenderMainPass` now runs per secondary viewport with owned targets; two-sided skinned draws added | ✅ yes | still needs the live pass it has needed since 5261 |

---

## 2. Compile breaks and their fixes

### 2.1 `KSA.Viewport` replaced by an interface family

The `Viewport` class (`OLD KSA/Viewport.cs`) is gone. NEW has `IViewport` (`KSA/IViewport.cs`),
`IGameViewport : IViewport` (`KSA/IGameViewport.cs`), `ViewportBase`, `GameViewport`,
`PartThumbnailViewport`, `ViewportRegistry` (`MAX_VIEWPORTS = 8`, a `ShaderIndexPool`),
`ViewportType {Main, Secondary, PartThumbnail, CharacterPortrait}`, `ViewportOptionFlags`
(`HasUi/HasInput/HasAudio/RenderGizmos/RenderOrbitLines/RenderWorldUi/UseShadows/UseRaytracing/
RenderStars/RenderAtmosphere/RenderVehicles/RenderPartModels/AllowSelection`) and `ViewportStateFlags`.
`Program.MainViewport` is now `IGameViewport` (`KSA/Program.cs:485`), `RenderedViewport`/
`FrameViewport`/`ThumbnailViewport` are `IViewport` (`:491,:493,:497`), `Program.Viewports`/
`ViewportCount` are gone (`ViewportRegistry.Views`), and `Viewport.Index` became `IViewport.ShaderSlot`.
Every game method that took a `Viewport` now takes `IViewport` — signatures otherwise identical, and
all of them remain **single overloads**, so every by-name `AccessTools.Method` still resolves.

| File | Line | Was | Now |
|---|---|---|---|
| `ksa-abstractions.lib/IvaForceRender.cs` | 98 | `AddInstancePostfix(…, Viewport __1)` | `IViewport __1` |
| `dont-stifle-me.lib/EditorScalePatches.cs` | 124 | `UpdateSelectedScalePrefix(…, Viewport inViewport)` | `IViewport inViewport` |
| `dont-stifle-me.lib/PerAxisScaleDrag.cs` | 28 | `Step(…, Viewport viewport, …)` | `IViewport viewport` |
| `i-feel-seen.lib/IFeelSeenPatches.cs` | 64 | `UpdateRenderDataPrefix(…, Viewport viewport, …)` | `IViewport viewport` |
| `parts-now.lib/Runtime/PartThumbnailGenerator.cs` | 61, 176, 338, 512 | `Viewport` field/locals/param | `IViewport` |
| `graffiti.lib/DecalRenderer.cs` | 402 | `GlobalShaderBindings.DynamicOffset(Program.MainViewport.Index)` | `.ShaderSlot` |

`Program.MainViewport.Mode` (IvaForceRender) still compiles — `Mode` is a property on `IViewport`
(`:29`); it was a field on the old class. All uses are compile-bound, no reflection, so no silent risk.

### 2.2 `Cursor.InputRay` removed (graffiti)

`KSA/Cursor.cs` was rewritten: `InputRay`, `UpdateInputRay(Camera?)`, `ScreenPosition`,
`ScreenDelta`, `LastScreenPosition` are gone. NEW exposes `DesktopPosition : float2` (`:11`),
`GetPosition(IViewport)` (`:22`, viewport-local) and `GetEgoRay(IViewport)` (`:27`) =
`inViewport.GetCamera().ScreenToEgoRay(GetPosition(inViewport))`. The cursor is set in
`Program.PrepareImGui` (`:2091`, `SetDesktopPosition(ImGui.GetIO().MousePos)`), **before** the UI
phase, so the ray is now built on demand from the **same-frame** camera and cursor (it used to be
refreshed after the UI phase, one frame stale).

Fix: `graffiti.lib/DecalPicker.cs:56` → `Cursor.GetEgoRay(Program.MainViewport)` (the pick already
gated on `Program.GetMainCamera()`, which is `MainViewport.GetCamera()`, so the camera is the same).
Doc comment and `graffiti/README.md` updated.

### 2.3 `VolumetricExhaustRenderer.AddInstance` gained air state (pyro)

NEW `KSA/VolumetricExhaustRenderer.cs:710`
`public float AddInstance(float3 emitterPosition, float3 axis, VolumetricExhaustInstance, float throttle, float3 airVelocity, float airDensity)`
(OLD `:860`, 4 args, `void`). The two new inputs feed the new `ExhaustPlumeDeformation.SplitWind /
ComputePlumeFold / ComputePlumeBend` (`:809-811`) — atmospheric plume bending. The game's caller
computes them once per vehicle in `Vehicle.AddVolumetricExhaustInstances` (`KSA/Vehicle.cs:5518-5525`):

```csharp
float3 airVelocity = float3.Pack(GetSurfaceVelocityCci().Transform(Parent.GetCci2Cce()));
float airDensity = 0f;
AtmosphereReference atmosphereReference = Parent.GetAtmosphereReference();
if (atmosphereReference != null)
{
    double altitudeInMeters = (GetPositionEcl() - Parent.GetPositionEcl()).Length() - Parent.MeanRadius;
    airDensity = (float)atmosphereReference.Physical.GetAtmosphericDensityAtAltitude(altitudeInMeters);
}
```

Fix: `pyro.lib/PlumeEmitter.cs` gained `ComputeAirState(Vehicle, out float3, out float)` (`:87-98`)
that mirrors this exactly (`Vehicle.GetSurfaceVelocityCci()` at `Vehicle.cs:2922` is itself new in
5402) and passes the result at `:76-78`. The `float` return (visual expansion radius) is ignored, as
the game's own nozzle path ignores it.

---

## 3. Changelog delta

Only rev **5401** is logged: *"Fixed crash for incorrect data stride for thumbnail rendering."* It is
not in the Thumbnail files at all — it is `KSA/GlobalShaderBindings.cs:94,217` (and
`AtmosphereRenderer`) sizing the per-viewport camera UBO for a fixed **8** shader slots
(`ViewportRegistry.MAX_VIEWPORTS`) instead of `Program.ViewportCount`, so a viewport's `ShaderSlot`
slice is always inside the buffer. parts-now's thumbnail generator passes the viewport object through
`ThumbnailDynamic.UpdateGlobalCameraData` / `ThumbnailRenderer.RecordPartRender` and never indexes
the buffer itself, so it inherits the fix.

Reconstructed from the source diff, the unlogged 5349–5400 window contains (mods at risk in bold):

- **Viewport registry rework** (§2.1) — **every mod that names a viewport type**; also `Program.OnKey`
  restructured around `InputViewport` (HotkeyGuard's `GameSettings.OnKeyAll` is still the first term),
  `OnDrawUiViewports`/`OnDrawUiConsole` iterate `ViewportRegistry.GameViews` (StarMap hooks intact).
- **Parachutes** — `Parachute`/`ActiveChute`/`Chute*` (30 new files), a Bepu cloth solver on a new
  `JobSystems.ClothSolvers` scheduler, `ExecuteNextClothSolvers` now runs **before**
  `ExecuteNextVehicleSolvers` in `PrepareFrame` (`Program.cs:2144-2146`), `PhysicsStates.ComputeDerivatives`
  gained chute forces, `PartTree.UpdateRenderData` draws chute lines, `Vehicle.UpdateParachuteRenderData`,
  two-sided skinned PBR renderer + `ModelPbr.frag`/`ModelNormal.frag` `gl_FrontFacing` normal flip,
  `Content/Core/ParachuteAssets.xml`, `CoreUtilityA_Prefab_ParachuteBayA` → `B` + radial chutes —
  **kiwis-marbles, garrys-torch** (frame ordering), **geeforce** (readings), **thug-life** (pass content).
- **Part structural failure / debris** — `PartFailure`, `PartFailureEvent`, `PartContactLoad`,
  `PartStructuralLimits`, `Part.CrashTolerancePascals`/`InertMassKg`/`StructuralPart`,
  `PartTemplate.CrashTolerance` (XML attr, set on `EngineA2..A6`), `Vehicle.IsDebris`/`MarkAsDebris`/
  `SpawnSubPartDebris`, `Universe.DestroyVehicle(Vehicle, CrewDisposition)` + `HandOffCameras`. No
  global off-switch exists (`GameSettings.cs` is byte-identical) — **garrys-torch**, and every mod's
  vehicle combo now lists debris.
- **Exhaust plume deformation** — `ExhaustPlumeDeformation`, `ExhaustPlumeGasDynamics`, the
  `AddInstance` signature (§2.3), `ExhaustInstance` grew 224 → 272 B (five bend/fold fields appended
  after everything pyro writes), `VolumetricExhaust.vert` + new `PlumeBend.glsl`, `PlumeTrail*`
  (trail LOD/segment store rewrite), `ExhaustAssets.xml` colour gradients retuned, and the refraction
  regression (§4.1) — **pyro**.
- **Light switch consolidation** — `Part.IsLightSwitchedOff()` (`Part.cs:1357`) replaces the inline
  `LightSwitch.LightIsActive` + power-state chain in `LightModule.IsActive` and
  `PartModelModule.UpdateRenderData`; `PowerConsumer.IsSwitchedOn()`; `ResetModuleProperties` nulls
  `LightSwitch` — **zippo, red-alert, its-so-shiny** (they write `LightIsActive`, still the first term).
- Editor: `Part.DisplayName` now prefers the template display name; `HandleConnectorConnections`
  gained a `CanConnect()` gate + coincident-connector lookup; `SubPartGroup` XML element;
  `Part.ComputeBoundingBoxVehicleAsmb` now accumulates every `MeshViewModule` — **dont-stifle-me,
  parts-now, kitchen-sink** (cosmetic/none).
- Terrain: `Celestial.GetTerrainHeightFromDirCcf` accurate path uses `MeanRadius` — **graffiti** (§4.3).
- Render plumbing: per-viewport descriptor arrays sized 8 (`GridPass`, `SingleToMultisamplePass`,
  `InstancedRenderTechnique`, `StaticCelestial`), `GizmosRenderer.MAX_GIZMO_INSTANCES` ×5, new
  `StaticObjectPrePassIndirectFrag`, `RayIntersections.glsl` quadratic term, `MeshIndirect.*` and
  `UnlitMesh.*` **byte-identical** — **thug-life, graffiti, humble-arteest, mesh-deform** (no anchor moved).
- Character: `CharacterCore.HeadMeshIndices`, `KittenRenderable.HideHead` / `AnimatedRenderable.
  MaskedMeshIndices` (seated kitten hides its own head), `SkinningPoseIsViewportInvariant` early-out
  (chutes only) — **doh, kitten-animations, garrys-torch** (all additive; `Core`/`Scale` still fields).

---

## 4. Findings requiring attention

### 4.1 pyro — the refraction pass is dead in 5402 (game-side) ⚠️

OLD `VolumetricExhaustRenderer.cs:960` set `_hasRefractionInstances = true` inside `AddInstance` when
`shaderData.refractionIntensity > 0.0001f`. NEW has only the per-frame reset (`:654`) and the three
readers (`:907, :1084, :1129`); nothing sets it. `refractionIntensity` is still scaled (`:803`) but
the refraction/blur/screen-copy passes can never run. Verified independently in both trees.

- **Impact:** pyro's per-plume *Refraction* slider (`PlumeEmitter.cs:103-106` writes
  `ExhaustInstance.refractionIntensity`) is a no-op — as is the game's own refraction on stock engines.
- **Verdict:** needs live confirmation (look for heat-haze on a stock engine). Not a pyro bug; keep
  the write. If confirmed, annotate the slider as inactive on this build (not done — approach to confirm).

### 4.2 garrys-torch — part failure can now fire on welded vehicles ⚠️

`PartFailure.Detect` (`KSA/PartFailure.cs:47`) runs from `PhysicsBubble.cs:1459` for every vehicle
that is not on rails / a kitten / already failing, and compares accumulated contact pressure against
`Part.CrashTolerancePascals`. Welded sources are teleported into overlap with the target every frame
(`WeldEngine.cs:129`); if Bepu produces contacts between the two compounds, parts can now shed debris
or destroy the vehicle. `Vehicle.Teleport` itself is byte-identical.

- `WeldEngine.UpdateWeld:19` dereferences `entry.Source.Parent` before any disposed check — a
  part-failure destroy of either end surfaces as an exception in `OnAfterUi` (caught by the host).
- **Verdict:** live test welding two capsule-class vehicles and watch for *"exceeded its crash
  tolerance"* log lines. Recommended hardening (not applied): an `IsDisposed`/null guard at the top of
  `UpdateWeld`, and optionally filtering `Vehicle.IsDebris` out of `VehicleProvider.GetAllVehicles()`
  so debris fragments stop appearing in every mod's vehicle combo.

### 4.3 graffiti — terrain-decal placement input changed ⚠️

`Celestial.cs:825-857` (the modifier evaluation inside the `accurate` branch of
`GetTerrainHeightFromDirCcf`, which `DecalPicker.cs:245` / `DecalAnchors.cs:81` call) now derives
`gradientWeight` and `CelestialRadiusKm` from `(float)MeanRadius` instead of
`RenderData.SurfaceRadius`. If the rendered terrain uses the same input the decal still lands on the
surface; if not it floats or sinks. Signature unchanged.

- **Verdict:** live check — place a terrain decal on flat and hilly ground, confirm with the debug box.

### 4.4 IvaForceRender — `PartModel.AddInstance` early-returns before the postfix's work ⚠️ (dormant)

NEW `PartModel.cs:410-413` inserts `if (!viewport.HasAny(ViewportOptionFlags.RenderPartModels)) return;`
before `ViewportData.Get`. A Harmony postfix still runs after that return, so for a viewport lacking
the flag the mod would push into an `InstanceList` the game never consumes. Today every viewport the
game creates carries `RenderPartModels` (`ViewportPresets.cs:5-11`, `Program.cs:948-956`), so the
branch is dead.

- **Recommended (not applied):** mirror the gate — `if (!__1.HasAny(ViewportOptionFlags.RenderPartModels)) return;`
  — and use `__1.Mode` instead of `Program.MainViewport.Mode` to track the game's per-viewport IVA check.

### 4.5 thug-life — render environment moved again (unchanged verdict)

`SuperMeshRenderSystem.RenderMainPass(CommandBuffer)` is at `:347` (was `:338`); the only body change
wraps the new two-sided skinned draws in a profiler tag. It is now called once per **visible secondary
viewport** too (`Program.cs:4395`), each with an owned target built from the same `ColorFormat` and
sample count as the main one, so a pipeline built against `Program.OffscreenTarget` stays compatible.
`UnlitMesh.*` byte-identical; `DefaultAssets.xml` ids unmoved (`:53/:54`). Still needs the live pass.

---

## 5. Verified clean against 5402

**String-reflection watchlist (master index §4) — every entry resolves**, same kind (field vs
property) and type: `Camera._fovRadians` (`:53`); con-man's seven `GaugeCanvas` fields
(**byte-identical at the same lines** `:92,115,130,132,134,136,143`); the avatar chain `KittenEva`
(`:13`) → `_renderable` (`:15`) → `KittenRenderable._characterAvatar` (`:12`) → `CharacterAvatar.Core`
(**struct field**, `:211`) → `CharacterCore.Scale` (**float field**, `:34`); `"KSA.LightModule+TemplateData"`
(`:11-12`) with `Intensity`/`ColorRgb` fields; the doh render-system bridge (`MaterialData.cs`,
`GpuMaterialSystem.cs`, `GpuObjectSystem.cs`, `AssetManager.cs`, `CharacterRenderSystem.cs` all
byte-identical); `ShaderReference.*` / `ShaderModuleUtils.FromFile` (files identical); `ModLibrary.
AllParts/AllCharacters/AllMeshes/AllFiles/AllGltfs` (internal static fields, `:86/:100/:80/:68/:76`);
`PartTree.RecomputeStaticMass` (still private, `:778`); pyro's `VolumetricExhaustInstance._shaderData`
(`:48`); rocky's `<HostPrimitives>k__BackingField`; bloomin-onion's `_anyRings` (`:68`) /
`_planetTransparenciesRenderer` (`:176`); `CatExpressionAnim._expressionPose` (file identical); the five
by-name `VehicleEditor` targets of dont-stifle-me (each a single overload); `Situation` enum names
(file identical).

**Harmony patch targets — every signature unchanged except the `Viewport → IViewport` retypes**, all
still single overloads: `GameSettings.OnKeyAll` (`:3301`, file identical), `Universe.
ExecuteNextVehicleSolvers(double, SimStep)` (`:1834`, body identical), the three
`*Module.UpdateRenderData` (`PartModelModule.cs:87`, `PartModelDynamicModule.cs:55`,
`PartModelGlassModule.cs:72`), `PartModel.AddInstance` (`:408`) / `PartModelDynamic.AddInstance`
(`:412`), `Vehicle.UpdateRenderData` (`:3675`) / `GetWorldMatrix` (`:3662`), `Vehicle.
AddVolumetricExhaustInstances` (`:5512`, param names unchanged), `OrbitController.OnFrame` (`:487`) /
`FlyController.OnFrame` (`:653`), `Camera.ChangeFieldOfView`/`UpdateProjection` (`:450/:466`),
`GaugeCanvas.OnDrawMenuBar` (`:1396`, body identical), `PartModel` ctor (`:384`),
`KittenEva` 8-arg ctor (`:78`), `AnimatedRenderable.UpdateAnimation` (`:134`),
`SuperMeshRenderSystem.RenderMainPass` (`:347`), `RenderTarget.ResolveAttachments` (`:315`, file
identical), `Program.DrawProgramMenusHook` (`:3876`), StarMap's `OnDrawUiFrame`/`OnDrawUiViewports`/
`OnFrame` (`:3021/:3051/:2164`).

**GPU byte layouts identical** — `PartModel.PerInstanceData` (`:332-343`: `ModelMatrix`,
`StateBitFlag`@64, `EmissiveColor`@68, `packing1`@72, `Wetness`@76), `PartModelDynamic.PerInstanceData`
(`:342-353`), `MaterialData` (`AlbedoColor`@16, stride 80; file identical). `StateBitFlag` bits 11..31
still unused by the game (writers re-audited). `ExhaustInstance` grew, but only **after** pyro's fields.

**Shaders** — `MeshIndirect.vert/.frag`, `MeshIndirectRaytraced.frag`, `UnlitMesh.vert/.frag`,
`Common/MaterialSet.glsl`, `Common/Shared.glsl`, `Grid.*`, ring shaders all **byte-identical**;
humble-arteest's `sampledColor` anchor and `ENABLE_TEMPERATURE` LUT intact; mesh-deform's struct anchor
still absent (pre-existing). `ModelPbr.frag` change is the two-sided normal flip, albedo path untouched.

**Assets** — every referenced id present: `UnlitMeshVert/Frag` (`DefaultAssets.xml:53/54`), `LightPart`,
`CorePropulsionA_Prefab_EngineA2..A6` (+ new `CrashTolerance` attr), `KittenBackPackPart`, pyro's seven
exhaust template ids (same lines; gradients retuned), rings/character ids. Editor tag definitions
identical. `SabotageMusic` still absent (pre-existing, null-guarded).

**Frames / numerics** — `IParentBody.cs`, `CelestialFrameMath.cs`, `Transform3D.cs`,
`KinematicMeasurements.cs`, `NavBallData.cs`, `Situation.cs`, `UniverseTime.cs` byte-identical;
`Camera.GetPositionEgo`, `Vehicle.Body2Cce/GetBody2Cci/GetAsmb2Cci`, `Celestial.GetCci2Cce` unchanged.

**Brutal / toolchain** — zero `Brutal*` entries in the decomp diff (DLL bytes rebuilt, sizes
identical); ImGui/ImPlot bindings unchanged; no nullability drift.

---

## 6. Known-broken reconciliation

| Item | Status at 5402 |
|---|---|
| camera-controller-override `___Transform` | ✅ closed since 5261; master §3 row still says BROKEN — corrected this pass |
| zippo `GetField("Color")` | ✅ closed (`07787ea`); stale remarks in `celestial-and-lights.md` corrected this pass |
| humble-arteest Vehicle Paint / mesh-deform | ❌ still dead by design (rev 4693); both self-disable. Unchanged |
| space-tape | removed @5348 |
| garrys-torch CS8604 | ✅ closed |
| unscience supermod never wires `IvaForceRender.Patch` | ✅ stale — it is wired at `unscience/Patcher.cs:74`; area-file text corrected |
| con-man vs global Hud Scale (5348) | ⚠ open, unchanged (all seven fields byte-identical) |
| kitten-animations (reworked 5348) | ⚠ open, live pass still wanted; `UpdateAnimation` gained a chute-only early-out that does not affect the kitten model |
| thug-life live pass | ⚠ open (§4.5) |
| parts-now load-time validation (5348) | ⚠ open, ordering invariant re-verified (`LoadAll :942` → `Bind :978` → validation `:1256`) |
| pyro / graffiti / rocky-mcrock-face / bloomin-onion / dont-stifle-me live passes | ⚠ open — none has had one yet; pyro and graffiti now carry 5402 fixes |

`ISSUES.md` user-reported items (eternal flame refill during burns, garrys-torch error spam) are not
explained by this span: `RefillConsumables` and `Teleport` are byte-identical. The new debris system
is a **new** candidate for garrys-torch noise (§4.2).

---

## 7. What still needs a live in-game pass

There is no test suite; `dotnet build` plus a live session is the whole verification story.

1. **F11 smoke** — open the unscience window; all submods load.
2. **pyro** — spawn a plume in atmosphere and check it bends with airspeed; look for heat-haze on a
   stock engine (§4.1).
3. **garrys-torch** — weld two vehicles; watch for crash-tolerance / debris log lines (§4.2).
4. **graffiti** — click-place a decal on a vehicle and on terrain (flat + hilly); the cursor ray is
   now same-frame (§2.2, §4.3).
5. **parts-now** — runtime-load a part and confirm the thumbnail renders (rev 5401 stride fix).
6. **dont-stifle-me** — per-axis-scale a part, then attach it (new `CanConnect()` gate).
7. **kiwis-marbles** — weld a body while a vehicle with a deployed parachute is nearby (cloth snapshot
   precedes the weld prefix by one frame).
8. **thug-life / humble-arteest / blinky / its-so-shiny** — the standing render checks.
9. **glass** — with an FOV override active, confirm part thumbnails are not distorted (the
   `UpdateProjection` prefix now also reaches `PartThumbnailViewport` cameras).

---

## 8. Documentation touched this pass

`scope/FULL_SCOPE.md` (baseline + status), `scope/game-integration-surface.md` (header, `KSA.Viewport`
→ `KSA.IViewport`, `KSA.Cursor`, `KSA.Program`, `KSA.GenericGizmo`, `KSA.VolumetricExhaustRenderer`,
§6), all eleven area files (rows retyped, new *5348 → 5402* sections, stale line citations refreshed),
`ISSUES.md` triage note, `graffiti/README.md`, `dont-stifle-me` README signatures. Line-number drift
from earlier passes that the area agents catalogued is applied where the row was touched; the
per-area reports carry the full NEW line tables.
