# Exhaust Plumes (pyro) — Game Integration Scope

## Workspace integration (current)

Active bundled features: **pyro**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

PlumeInstance records and bulk plume controls now render in Live State. Draft target/template choices and PlumePreset values are independent. `ExhaustTemplateRecipe` explicitly copies the existing VolumetricExhaustTemplate mutable settings into detached data; Apply records an original recipe and a typed template override. Shared-template edit/restore still invokes TemplateRefresher, preserving the existing renderer notification path. Template editing no longer writes to game data merely by rendering a draft.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break **pyro** (independent volumetric engine
plumes). Every game-facing member the mod touches is enumerated with its decompiled-source path.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard remains in `unscience/Patcher.cs`; feature Harmony groups are registered by their owning libraries through `ConfigureRuntime`. See [architecture](00-architecture-and-abstractions.md).

---

## Integration model

1. **One Harmony postfix** on `Vehicle.AddVolumetricExhaustInstances(Camera, IViewport,
   VolumetricExhaustRenderer, double)` (`KSA/Vehicle.cs`). The game calls this from
   `Program.OnPreRender` once per visible vehicle, after `VolumetricExhaustRenderer.UpdateFrameData()`
   reset the instance list. pyro's postfix (`PyroPatches.cs`) hands the same `camera`/`renderer`/
   `frameDeltaTime` to `PyroSubmod.SubmitPlumes`, which submits every plume welded to `__instance`.
2. **Per plume, pyro owns a real `VolumetricExhaustInstance`** built from
   `new VolumetricExhaustReference { Id }.Load()` (`PlumeTemplates.cs`) and drives it exactly like
   `RocketNozzleState.AddExhaustInstance` (`KSA/RocketNozzleState.cs`): `UpdateState(simTime,
   isActive, dt, plumeData)` then `renderer.AddInstance(posEgo, axis, instance, throttle, airVelocity,
   airDensity)` (`PlumeEmitter.cs`). The air state (`ComputeAirState`, `PlumeEmitter.cs`)
   mirrors the game's own derivation in `Vehicle.AddVolumetricExhaustInstances` (`:5518-5525`) — 5402
   uses it to fold/bend plumes in atmosphere.
3. **`PlumeData` is synthesised**, not read from an engine: `PlumePhysics.TryCompute` mirrors
   `RocketNozzle.UpdatePlumeData` (`KSA/RocketNozzle.cs`, now a thin wrapper over the public static
   `ComputePlumeData` `:266`) and `RecomputeGasVisibilityDensity` (`:182`, formula extracted to public
   static `ComputeMinGasVisibilityDensity` `:197`) from user nozzle settings +
   `PhysicalAtmosphereReference.GetAtmosphericPressure(camera)`.
4. **Positioning** chain: part-local offset → `Part.MatrixAsmb2VehicleAsmb` / `Part.Asmb2VehicleAsmb`
   (`KSA/Part.cs`) → `Vehicle.PosAsmbToBody` (`:1270`) → `Vehicle.Body2Cce` (`:475`) →
   `Camera.GetPositionEgo(vehicle)` (`KSA/Camera.cs`). Base axis is part-local **-X**, matching every
   stock `<ExhaustDirection X="-1">` in `Core/CorePropulsionAGameData.xml`.
5. **Template Editor** writes the shared `VolumetricExhaustTemplate` sub-objects (same fields and same
   `ColorRgbReference(float3)` + `OnDataLoad(new Mod())` idiom as the game's `VolumetricExhaustRenderer.
   OnDrawUi`, `:2126-2148`), then `TemplateRefresher` calls `OnSettingsChanged()` on every affected
   instance and `RecomputeGasVisibilityDensity` on every real nozzle — the debug editor's own `changed`
   path (`:2321-2345`) minus the transient-LUT rebake (pyro does not edit transients).

**Persistence** — Exact/controlled vehicle and verified part/subpart, template, transforms, throttle, nozzle/look parameters and shared-template recipe. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

## Touchpoints

| # | Kind | Mod code | Game member | Decomp path (5402) | Status | Notes |
|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `PyroPatches.cs` | `Vehicle.AddVolumetricExhaustInstances(Camera camera, IViewport viewport, VolumetricExhaustRenderer renderer, double frameDeltaTime)` | `KSA/Vehicle.cs` (was `:5303`, `Viewport`) | ✅ | resolved with `nameof` — a rename is a compile break. Param **names** (`camera`, `renderer`, `frameDeltaTime`) are bound by Harmony: a param rename silently unbinds → `Apply` throws → `TryApply` logs and skips pyro. 5402: `Viewport`→`IViewport` only (names unchanged, single overload); body now also derives `airVelocity`/`airDensity` (`:5518-5525`) |
| 2 | Direct API | `PlumeEmitter.cs` (+ `ComputeAirState` `:87-98`) | `VolumetricExhaustRenderer.AddInstance(float3, float3, VolumetricExhaustInstance, float throttle, float3 airVelocity, float airDensity) : float` | `KSA/VolumetricExhaustRenderer.cs` (was `:860`, 4-arg `void`) | ✅ **fixed 5402** | 🔴 5402 **removed the 4-arg overload** (compile break, fixed). Return is `visualExpansionRadius` (discarded). `ComputeAirState` uses `Vehicle.GetSurfaceVelocityCci()` (`KSA/Vehicle.cs`, **new API in 5402**), `IParentBody.GetCci2Cce()`/`.GetAtmosphereReference()`/`.MeanRadius` (`KSA/IParentBody.cs`), `IPosition.GetPositionEcl()` (`KSA/IPosition.cs`), `PhysicalAtmosphereReference.GetAtmosphericDensityAtAltitude(double)` (`:85`). Reads `instance.ShaderData` (copy) + `LastPlumeData`; consumes `ApparentExhaustVelocity`, `ThroatRadius`, `ThroatDensity` (new @5348); 5402 adds wind fold/bend via `ExhaustPlumeDeformation` (`:809-811`) |
| 3 | Direct API | `PyroSubmod.cs` | `VolumetricExhaustRenderer.Disabled` | `:352` (was `:312`) | ✅ | |
| 4 | Direct API | `PlumeTemplates.cs` | `VolumetricExhaustReference { Id }`, `.Load()`, `.Template`; `new VolumetricExhaustInstance(ref)` | `KSA/VolumetricExhaustReference.cs`; `KSA/VolumetricExhaustInstance.cs` | ✅ | file byte-identical 5348↔5402 |
| 5 | Direct API | `PlumeEmitter.cs` | `VolumetricExhaustInstance.UpdateState(double, bool, double, PlumeData) : bool` | `KSA/VolumetricExhaustInstance.cs` | ✅ | 4-slot pulse tracker (was 2 pre-5348) |
| 6 | Direct API | `TemplateRefresher.cs` | `VolumetricExhaustInstance.OnSettingsChanged()` | `KSA/VolumetricExhaustInstance.cs` | ✅ | |
| 7 | **Reflection (private, string)** | `PlumeEmitter.cs` | `VolumetricExhaustInstance._shaderData : ExhaustInstance` via `FieldRefAccess` | `KSA/VolumetricExhaustInstance.cs` | ✅ | writes `absorptionDensity`, `refractionIntensity`. Soft-fails: `PerPlumeLookAvailable=false`, UI notice |
| 8 | Struct layout | `PlumeEmitter.cs` | `ExhaustInstance.absorptionDensity` (`:25`), `.refractionIntensity` (`:69`) | `KSA/ExhaustInstance.cs` | ✅ | ⚠ 5348 moved colours/brightness/noise/sample counts to `ExhaustTemplateData` (per-template buffer, `templateIndex`) — per-plume colour intentionally not offered. 5402: struct grew **224 → 272 B** — `padding0/padding1` replaced by `float bendExponent`, `float boundingLength`, `float4 bendDirectionAndAngle`, `float4 foldParameters`, `float4 foldAxisOffset` (`:81-89`, mirrored in `VolumetricExhaust/Data/InstanceData.glsl:55-70`). All **after** the two fields pyro writes and populated by the renderer (`:787,:809-811`); pyro uses typed field access, so no offset exposure. ⚠ `refractionIntensity` is inert in 5402 — see findings |
| 9 | Direct API (object init) | `PlumePhysics.cs` | `PlumeData` (all `required`) | `KSA/PlumeData.cs` | ✅ | any added/renamed `required` member = compile break |
| 10 | Direct API | `PlumePhysics.cs` | `GasProperties{Gamma,SpecificGasConstant}.ComputeSpeedOfSound/…PressureAngle/…PressureMach/ComputePrandtlMeyer`; `GasConditions{Pressure,Temperature}.ComputeDensity` | `KSA/GasProperties.cs`; `KSA/GasConditions.cs` | ✅ | pressures **Pa** |
| 11 | Direct API | `PlumePhysics.cs` | `RocketDesign.SolveMachNumberFromAreaRatio(GasProperties,double)`, `ComputeAreaRatioFromMachNumber(double,double)` | `KSA/RocketDesign.cs` | ✅ | |
| 12 | Direct API | `PlumePhysics.cs` | `PhysicalAtmosphereReference.GetAtmosphericPressure(Camera) : double` (**atm**) | `KSA/PhysicalAtmosphereReference.cs` | ✅ | ×101325 → Pa |
| 13 | Direct API | `PlumePhysics.cs` | `template.Emission.Brightness.Value`, `Absorption.ScatteringBrightness.Value`, `Absorption.Density.Value` | `KSA/Emission.cs`, `KSA/Absorption.cs` | ✅ | visibility threshold formula copied from `RocketNozzle.RecomputeGasVisibilityDensity`; 5402 extracted it unchanged into public static `RocketNozzle.ComputeMinGasVisibilityDensity(VolumetricExhaustTemplate, double)` (`:197`) — optional hardening: call it directly |
| 14 | Direct API | `PlumeEmitter.cs` | `Part.MatrixAsmb2VehicleAsmb`, `Part.Asmb2VehicleAsmb`, `Vehicle.PosAsmbToBody(double3)`, `Vehicle.Body2Cce`, `Camera.GetPositionEgo(IPosition)`, `doubleQuat.NormalizedOrZero()` (ext, `KSA/QuaternionEx.cs`) | `KSA/Part.cs`; `KSA/Vehicle.cs`; `KSA/Camera.cs` | ✅ | line moves only |
| 15 | Direct API | `PyroSubmod.cs` | `Universe.GetElapsedSeconds()`, `Universe.GetSimulationSpeed()` | `KSA/Universe.cs` (was `:2054,1972`) | ✅ | |
| 16 | Direct API | `PyroSubmod.CreateUi.cs`; `PyroSubmod.cs`; `PyroUi.cs` | `Vehicle.Parts.Parts`, `Part.SubParts`, `Part.PartParent`, `Part.Template.Id`, `Part.Id` | `KSA/Part.cs` | ✅ | anchor pick + dead-anchor pruning |
| 17 | **Reflection (internal, string)** | `PlumeTemplates.cs` | `VolumetricExhaustTemplate.References : SerializedCollection<T>` → `GetList()` | `KSA/VolumetricExhaustTemplate.cs`; `KSA/SerializedCollection.cs` | ✅ | soft-fails to the 7 stock ids via public `Get(id)` (`:50`); both files byte-identical 5348↔5402 |
| 18 | Direct API (read+write) | `PyroSubmod.TemplateUi.cs` | `VolumetricExhaustTemplate.Absorption/Emission/Noise/LengthWeights/Quality` sub-objects; `DoubleReference.Value`, `BoolReference.Value`, `Quality.VolumetricVesselShadows`, `ColorGradient.Color0..3`, `Flow.MachDiamonds.{LeadIn,LeadOut,MiddleRadius}` | `KSA/VolumetricExhaustTemplate.cs` + sub-type files | ✅ | GPU `ExhaustTemplateData` rebuilt from these each `Render()` (`VolumetricExhaustRenderer.cs`, was `:1236-1243`); all sub-type files byte-identical |
| 19 | Direct API | `PyroSubmod.TemplateUi.cs` | `ColorRgbReference.Value.AsFloat3`, `new ColorRgbReference(float3)`, `.OnDataLoad(new Mod())` | `KSA/ColorRgbReference.cs`; `KSA/Mod.cs` | ✅ | identical to the game editor (`VolumetricExhaustRenderer.cs`) |
| 20 | Direct API | `TemplateRefresher.cs` | `PartTree.RocketNozzles.ModulesAndAllStates` enumerator → `.FxState.VolumetricExhaust`, `.Module.RecomputeGasVisibilityDensity(in …)` | `KSA/Vehicle.cs` (game usage); `KSA/RocketNozzle.cs` | ✅ | in try/catch; failure only means real engines lag on threshold updates |
| 21 | Asset ids | `PlumeTemplates.cs`; `PlumeEntry.cs` default `EngineALarge` | `EngineALarge, EngineAMed, EngineACompact, EngineAVernier, EngineATurbine, RCS, MmuRcsVac` | `Core/ExhaustAssets.xml:307,650,993,1331,1670,3,2009` | ✅ | fallback list only. Ids unchanged in 5402; the five `EngineA*` templates had their `Emission/ColorGradient` retuned (see findings) |
| 22 | Build refs | `pyro.lib.csproj` | `Brutal.Vulkan`, `Brutal.Vulkan.Abstractions`, `BepuUtilities` | — | ✅ | needed so `VolumetricExhaustRenderer` / `Symmetric3x3` (`Part` matrix API) resolve |

## Historical evidence

See [dated integration and upgrade reference](history/exhaust-plumes.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.

## Current runtime release behavior

Release/unload restores applied shared exhaust-template snapshots and refreshes their consumers before forgetting records. Independent plume render hooks exist only while plumes are owned.

Feature hook targets retain their existing signatures; patch ownership now follows explicit demand through the shared runtime coordinator. Native acceptance remains outstanding.
