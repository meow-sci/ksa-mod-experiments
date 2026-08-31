# Exhaust Plumes (pyro) — Game Integration Scope

Permanent reference for detecting when KSA game updates break **pyro** (standalone volumetric engine
plumes). Every game-facing member the mod touches is enumerated with its decompiled-source path.

**Verified game version:** written against KSA **`2026.8.22.5348`** (decomp root
`…/ksa-game-assemblies/current/decomp`, namespace-foldered). The in-repo `decomp/ksa` copy is **older**
and materially different for this area (pre-5348 `PlumeData`/`ExhaustInstance` layouts) — always check
this file's members against the provided tree, not the repo copy.

**How the mod is hosted:** all logic in `pyro.lib` (`PyroSubmod : ISubmod`, `PyroPatches.Apply/Remove`),
consumed by the standalone host (`pyro/Mod.cs`, `pyro/Patcher.cs`) and by unscience
(`unscience/Mod.cs` adds `PyroSubmod`; `unscience/Patcher.cs` → `TryApply("pyro", …)`). Findings apply to
both hosts identically.

---

## Integration model

1. **One Harmony postfix** on `Vehicle.AddVolumetricExhaustInstances(Camera, Viewport,
   VolumetricExhaustRenderer, double)` (`KSA/Vehicle.cs:5303`). The game calls this from
   `Program.OnPreRender` once per visible vehicle, after `VolumetricExhaustRenderer.UpdateFrameData()`
   reset the instance list. pyro's postfix (`PyroPatches.cs:35`) hands the same `camera`/`renderer`/
   `frameDeltaTime` to `PyroSubmod.SubmitPlumes`, which submits every plume welded to `__instance`.
2. **Per plume, pyro owns a real `VolumetricExhaustInstance`** built from
   `new VolumetricExhaustReference { Id }.Load()` (`PlumeTemplates.cs:55-59`) and drives it exactly like
   `RocketNozzleState.AddExhaustInstance` (`KSA/RocketNozzleState.cs:81`): `UpdateState(simTime,
   isActive, dt, plumeData)` then `renderer.AddInstance(posEgo, axis, instance, throttle)`
   (`PlumeEmitter.cs:56,76`).
3. **`PlumeData` is synthesised**, not read from an engine: `PlumePhysics.TryCompute` mirrors
   `RocketNozzle.UpdatePlumeData` (`KSA/RocketNozzle.cs`) and `RecomputeGasVisibilityDensity`
   (`:182-199`) from user nozzle settings + `PhysicalAtmosphereReference.GetAtmosphericPressure(camera)`.
4. **Positioning** chain: part-local offset → `Part.MatrixAsmb2VehicleAsmb` / `Part.Asmb2VehicleAsmb`
   (`KSA/Part.cs:728,712`) → `Vehicle.PosAsmbToBody` (`:1218`) → `Vehicle.Body2Cce` (`:374`) →
   `Camera.GetPositionEgo(vehicle)` (`KSA/Camera.cs:231`). Base axis is part-local **-X**, matching every
   stock `<ExhaustDirection X="-1">` in `Core/CorePropulsionAGameData.xml`.
5. **Template Editor** writes the shared `VolumetricExhaustTemplate` sub-objects (same fields and same
   `ColorRgbReference(float3)` + `OnDataLoad(new Mod())` idiom as the game's `VolumetricExhaustRenderer.
   OnDrawUi`, `:2306-2311`), then `TemplateRefresher` calls `OnSettingsChanged()` on every affected
   instance and `RecomputeGasVisibilityDensity` on every real nozzle — the debug editor's own `changed`
   path (`:2493-2515`) minus the transient-LUT rebake (pyro does not edit transients).

**Persistence** — Named **presets** only (not active plumes). `PlumePresetManager`
(`pyro.lib/PlumePresetManager.cs`) reads/writes TOML at
`<MyDocuments>/My Games/Kitten Space Agency/.unscience/pyro-presets.toml`
(dir from `ksa-abstractions.lib/KsaPaths.cs:9`). Mod-authored file, not a game asset —
no game integration point beyond the `KsaPaths` directory convention.

## Touchpoints

| # | Kind | Mod code | Game member | Decomp path (5348) | Status | Notes |
|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `PyroPatches.cs:16,35` | `Vehicle.AddVolumetricExhaustInstances(Camera, Viewport, VolumetricExhaustRenderer, double)` | `KSA/Vehicle.cs:5303` | ✅ | resolved with `nameof` — a rename is a compile break. Param **names** (`camera`, `renderer`, `frameDeltaTime`) are bound by Harmony: a param rename silently unbinds → `Apply` throws → `TryApply` logs and skips pyro |
| 2 | Direct API | `PlumeEmitter.cs:76` | `VolumetricExhaustRenderer.AddInstance(float3, float3, VolumetricExhaustInstance, float)` | `KSA/VolumetricExhaustRenderer.cs:860` | ✅ | reads `instance.ShaderData` (copy) + `LastPlumeData`; consumes `ApparentExhaustVelocity`, `ThroatRadius`, `ThroatDensity` (all new @5348) |
| 3 | Direct API | `PyroSubmod.cs:75` | `VolumetricExhaustRenderer.Disabled` | `:312` | ✅ | |
| 4 | Direct API | `PlumeTemplates.cs:55-59` | `VolumetricExhaustReference { Id }`, `.Load()`, `.Template`; `new VolumetricExhaustInstance(ref)` | `KSA/VolumetricExhaustReference.cs`; `KSA/VolumetricExhaustInstance.cs:75` | ✅ | |
| 5 | Direct API | `PlumeEmitter.cs:56` | `VolumetricExhaustInstance.UpdateState(double, bool, double, PlumeData) : bool` | `KSA/VolumetricExhaustInstance.cs:91` | ✅ | 4-slot pulse tracker (was 2 pre-5348) |
| 6 | Direct API | `TemplateRefresher.cs:20,42` | `VolumetricExhaustInstance.OnSettingsChanged()` | `KSA/VolumetricExhaustInstance.cs` | ✅ | |
| 7 | **Reflection (private, string)** | `PlumeEmitter.cs:25,84-87` | `VolumetricExhaustInstance._shaderData : ExhaustInstance` via `FieldRefAccess` | `KSA/VolumetricExhaustInstance.cs:48` | ✅ | writes `absorptionDensity`, `refractionIntensity`. Soft-fails: `PerPlumeLookAvailable=false`, UI notice |
| 8 | Struct layout | `PlumeEmitter.cs:86-87` | `ExhaustInstance.absorptionDensity`, `.refractionIntensity` | `KSA/ExhaustInstance.cs` | ✅ | ⚠ 5348 moved colours/brightness/noise/sample counts to `ExhaustTemplateData` (per-template buffer, `templateIndex`) — per-plume colour intentionally not offered |
| 9 | Direct API (object init) | `PlumePhysics.cs:70-92` | `PlumeData` (all `required`) | `KSA/PlumeData.cs` | ✅ | any added/renamed `required` member = compile break |
| 10 | Direct API | `PlumePhysics.cs:30-89` | `GasProperties{Gamma,SpecificGasConstant}.ComputeSpeedOfSound/…PressureAngle/…PressureMach/ComputePrandtlMeyer`; `GasConditions{Pressure,Temperature}.ComputeDensity` | `KSA/GasProperties.cs`; `KSA/GasConditions.cs` | ✅ | pressures **Pa** |
| 11 | Direct API | `PlumePhysics.cs:33,61` | `RocketDesign.SolveMachNumberFromAreaRatio(GasProperties,double)`, `ComputeAreaRatioFromMachNumber(double,double)` | `KSA/RocketDesign.cs:168,187` | ✅ | |
| 12 | Direct API | `PlumePhysics.cs:113` | `PhysicalAtmosphereReference.GetAtmosphericPressure(Camera) : double` (**atm**) | `KSA/PhysicalAtmosphereReference.cs:50` | ✅ | ×101325 → Pa |
| 13 | Direct API | `PlumePhysics.cs:102-105` | `template.Emission.Brightness.Value`, `Absorption.ScatteringBrightness.Value`, `Absorption.Density.Value` | `KSA/Emission.cs`, `KSA/Absorption.cs` | ✅ | visibility threshold formula copied from `RocketNozzle.RecomputeGasVisibilityDensity` (`:190-194`) |
| 14 | Direct API | `PlumeEmitter.cs:69-74` | `Part.MatrixAsmb2VehicleAsmb`, `Part.Asmb2VehicleAsmb`, `Vehicle.PosAsmbToBody(double3)`, `Vehicle.Body2Cce`, `Camera.GetPositionEgo(IPosition)`, `doubleQuat.NormalizedOrZero()` (ext, `KSA/QuaternionEx.cs:280`) | `KSA/Part.cs:728,712`; `KSA/Vehicle.cs:1218,374`; `KSA/Camera.cs:231` | ✅ | |
| 15 | Direct API | `PyroSubmod.cs:77-78` | `Universe.GetElapsedSeconds()`, `Universe.GetSimulationSpeed()` | `KSA/Universe.cs:2054,1334` | ✅ | |
| 16 | Direct API | `PyroSubmod.CreateUi.cs:135,148`; `PyroSubmod.cs:189-190`; `PyroUi.cs:12` | `Vehicle.Parts.Parts`, `Part.SubParts`, `Part.PartParent`, `Part.Template.Id`, `Part.Id` | `KSA/Part.cs:1052,1054` | ✅ | anchor pick + dead-anchor pruning |
| 17 | **Reflection (internal, string)** | `PlumeTemplates.cs:46` | `VolumetricExhaustTemplate.References : SerializedCollection<T>` → `GetList()` | `KSA/VolumetricExhaustTemplate.cs:38`; `KSA/SerializedCollection.cs:42` | ✅ | soft-fails to the 7 stock ids via public `Get(id)` (`:50`) |
| 18 | Direct API (read+write) | `PyroSubmod.TemplateUi.cs` | `VolumetricExhaustTemplate.Absorption/Emission/Noise/LengthWeights/Quality` sub-objects; `DoubleReference.Value`, `BoolReference.Value`, `Quality.VolumetricVesselShadows`, `ColorGradient.Color0..3`, `Flow.MachDiamonds.{LeadIn,LeadOut,MiddleRadius}` | `KSA/VolumetricExhaustTemplate.cs:12-27` + sub-type files | ✅ | GPU `ExhaustTemplateData` rebuilt from these each `Render()` (`VolumetricExhaustRenderer.cs:1236-1243`) |
| 19 | Direct API | `PyroSubmod.TemplateUi.cs:123-127` | `ColorRgbReference.Value.AsFloat3`, `new ColorRgbReference(float3)`, `.OnDataLoad(new Mod())` | `KSA/ColorRgbReference.cs:22,28,35`; `KSA/Mod.cs` | ✅ | identical to the game editor |
| 20 | Direct API | `TemplateRefresher.cs:36-43` | `PartTree.RocketNozzles.ModulesAndAllStates` enumerator → `.FxState.VolumetricExhaust`, `.Module.RecomputeGasVisibilityDensity(in …)` | `KSA/Vehicle.cs:5310` (game usage); `KSA/RocketNozzle.cs:182` | ✅ | in try/catch; failure only means real engines lag on threshold updates |
| 21 | Asset ids | `PlumeTemplates.cs:13`; `PlumeEntry.cs` default `EngineALarge` | `EngineALarge, EngineAMed, EngineACompact, EngineAVernier, EngineATurbine, RCS, MmuRcsVac` | `Core/ExhaustAssets.xml` | ✅ | fallback list only |
| 22 | Build refs | `pyro.lib.csproj` | `Brutal.Vulkan`, `Brutal.Vulkan.Abstractions`, `BepuUtilities` | — | ✅ | needed so `VolumetricExhaustRenderer` / `Symmetric3x3` (`Part` matrix API) resolve |

## Update-risk findings

- **Loud breaks (compile):** `PlumeData` required-member churn (#9), `AddInstance` signature (#2),
  `AddVolumetricExhaustInstances` rename (#1 via `nameof`), any template sub-object field rename (#18).
- **Silent breaks (runtime):** postfix **parameter renames** (#1 — Harmony binds by name, throws at
  `Apply`, pyro is skipped with a console line); the two string lookups (#7, #17) — both degrade
  gracefully and say so in the UI.
- **Semantic drift with no symbol change:** `AddInstance` may start reading new `PlumeData` fields that
  pyro leaves at defaults (as 5348 did with `ThroatRadius`) — symptom is a wrong-shaped plume, not an
  error. Re-diff `RocketNozzle.UpdatePlumeData` against `PlumePhysics.TryCompute` on every bump.
  Likewise the `_shaderData` fields pyro overrides (#8) could migrate to the template buffer, silently
  turning the Look sliders into no-ops.
- **Unit assumption:** pressures are Pa game-side (`PressureReference` stores Pa; `Combustor
  MaxPressure Bar="49"`), ambient from `GetAtmosphericPressure` is atm (`× 9.869e-6`). If either flips,
  plumes become absurdly long/short.
- **Not done / known limits:** no per-plume colour (see #8); Template Editor does not edit
  startup/shutdown transients (would need `TransientAnimationLut.BakeAnimationLutData`, which is private
  renderer state); plumes only update while their vehicle is in `Program.VehiclesInFrame` (same as
  stock engines).
