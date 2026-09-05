# Vehicle Manipulation / Physics Mods — Game Integration Scope

## Workspace integration (current)

Active bundled features: **eternal-flame, i-feel-seen, garrys-torch, kiwis-marbles**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

Fuel/electricity recipes and intervals are detached from monitored Vehicle entries; visibility tracking, WeldEntry and celestial weld entries are live records. Garry’s Torch keeps the solver wait and after-GUI weld phase; Kiwi keeps its solver Harmony seam. Restoring target identities never calls Add/Remove/Unweld or changes a live transform.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break the vehicle-manipulation /
physics mods (`eternal-flame`, `garrys-torch`, `i-feel-seen`). Every game-facing member
these mods touch is enumerated and verified against decompiled sources.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard and feature Harmony patches are wired through `unscience/Patcher.cs`. See [architecture](00-architecture-and-abstractions.md).

## eternal-flame (`eternal-flame` / `eternal-flame.lib`)

**Purpose** — Infinite fuel + electricity. Keeps selected vehicles topped up: periodically
calls `Vehicle.RefillConsumables()` (fuel/resource tanks) and refills every `Battery`
module to `MaximumCapacity`. Battery refills are driven from a Harmony **prefix** on
`Universe.ExecuteNextVehicleSolvers` so the new charge is copied into the next electrical
simulation step; fuel refills run on the normal UI update tick.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**F11** (`unscience/Mod.cs`). Content (`EternalFlameSubmod.RenderContent`,
`eternal-flame.lib/EternalFlameSubmod.cs`): filterable vehicle combo + Add, monitored-vehicle
table with per-row Fuel/Elec checkboxes and remove, refill-interval `DragInt` (0–5000 ms).
All ImGui via `Brutal.ImGuiApi`.

**Persistence** — Exact/controlled vehicle, fuel/electricity flags and refill interval. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Harmony (prefix) | `EternalFlamePatches` in `unscience/Patcher.cs` | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep)` — `public static void`; resolved `AccessTools.Method(typeof(Universe), nameof(...))` (no param array), prefix is param-less `void` (priority First) | `KSA/Universe.cs` | Yes | Same (OLD `Universe.cs`); 5402 body diff = removal of a clutter debug-draw block only | Single overload, so no-arg resolution is unambiguous. Prefix returns void -> original always runs. Highest-value chokepoint for this mod. Since 5402 `Universe.ExecuteNextClothSolvers` is kicked **before** this method (`KSA/Program.cs`); irrelevant to battery refill. |
| 2 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `Vehicle.RefillConsumables()` — `public void` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`; body identical) | Internally calls `Parts.RefillConsumables()` + `RecomputeMassProperties` + `FlightComputer.ReadUpdatedVehicleConfiguration` (all internal; not touched directly). |
| 3 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `Vehicle.Parts` — `public PartTree Parts` (field) | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Entry to battery state list. |
| 4 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `PartTree.Batteries` — `public ModuleStateful<Battery,BatteryState,EmptyStruct,EmptyStruct>.StateList Batteries` (field) | `KSA/PartTree.cs` | Yes | Same (OLD `PartTree.cs`) | Generic `StateList`. |
| 5 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `StateList.NumModules` — `public int NumModules` | `KSA/ModuleStateful.cs` | Yes | Same (file byte-identical) | Early-out when 0. |
| 6 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `StateList.Modules` — `public Span<TModule> Modules` | `KSA/ModuleStateful.cs` | Yes | Same (file byte-identical) | Iterates `Battery[]`. |
| 7 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `StateList.GetModuleAndAllMutableStatesForInitialization(TModule)` — returns `ModuleAndAllMutableStatesRef` | `KSA/ModuleStateful.cs` | Yes | Same (file byte-identical) | Returns ref struct with `.Module` + `.State`. |
| 8 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `ModuleAndAllMutableStatesRef.Module` / `.State` (Battery / BatteryState) | `KSA/ModuleStateful.cs` (nested ref struct) | Yes | Same | Game uses the same `.Module.Refill(ref ...State)` shape in `KSA/ResourceManager.cs`. |
| 9 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` | `Battery.Refill(ref BatteryState state)` — `public void` (sets `state.Charge = MaximumCapacity`) | `KSA/Battery.cs` | Yes | Same (file byte-identical 5348→5402) | **Insulates the mod from rev 4681.** Body unchanged OLD->NEW. |
| 10 | Direct typed API (indirect) | via #9 | `Battery.MaximumCapacity` — `public required Joules MaximumCapacity` | `KSA/Battery.cs` | Yes | Same (file byte-identical) | Read only inside `Refill`; mod never names `Joules`. |
| 11 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs` (lookup) | `Vehicle.Id` — `public virtual string Id` (inherited `Astronomical.Id`) | `KSA/Astronomical.cs` | Yes | Same (OLD `Astronomical.cs`) | Monitored-vehicle key matching. |
| 12 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs` (called `EternalFlameLib.cs`; `EternalFlameSubmod.cs`) | `Universe.CurrentSystem` (`KSA/Universe.cs`) -> `CelestialSystem.All` (`KSA/CelestialSystem.cs`) -> `LookupCollection<Astronomical>.UnsafeAsList()` (`KSA/LookupCollection.cs`) | `KSA/Universe.cs` | Yes | Same (`CelestialSystem.All` OLD `:57`) | Shared enumerator; a break here cascades to all three mods' UI. Since 5402 the list also contains debris fragments (`Vehicle.IsDebris`, `KSA/Vehicle.cs`). |
| 13 | Harmony + Reflection | `unscience/Patcher.cs` -> `HotkeyGuard.cs` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool`; `nameof`-resolved, prefix `ref bool __result` | `KSA/GameSettings.cs` | Yes | Same (file byte-identical) | Shared guard (full row in `scope/telemetry.md`). |
| 14 | Lifecycle | `unscience/Mod.cs` | StarMap attrs: `StarMapMod`, `StarMapImmediateLoad`, `StarMapAllModsLoaded`, `StarMapBeforeGui`, `StarMapAfterGui`, `StarMapUnload` (StarMap.API) | (StarMap.API package) | Yes | Same | Fuel in `OnBeforeUi`; battery via the solver prefix. |

**Game assets referenced** — None.

## garrys-torch (`garrys-torch` / `garrys-torch.lib`)

**Purpose** — Vehicle-to-vehicle welding. Every frame it teleports a *source* vehicle to a
pose relative to a *target* vehicle (optionally anchored to a specific target `Part`), with
position/rotation offset, uniform part scaling (special-cased for `KittenEva` avatars), and
optional rotation lock. Also supports eased animation of weld params.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Source and target vehicles, verified anchor part, translation, rotation, scale, rotation lock and legacy preset. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed API | `garrys-torch.lib/GarrysTorchSubmod.cs` | `KSA.JobSystems.VehicleSolver` (`public static JobScheduler VehicleSolver`, single-runner orchestrator; renamed from `VehicleSolvers` @5261) -> `JobScheduler.Wait()` | `KSA/JobSystems.cs` | Yes | Same (OLD `JobSystems.cs`). 5402 added a sibling `ClothSolvers` scheduler (`:18`) — see 5348→5402 summary | Game itself calls `JobSystems.VehicleSolver.Wait()` (`KSA/Program.cs`, `Universe.cs`). Core race-avoidance for the whole mod. |
| 2 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.Parent` — `public IParentBody Parent => Orbit.Parent` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Reference-compared for parent-body match; `.GetCci2Cce()` called on it (#10). |
| 3 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.GetPositionCci()` — `public double3` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Target world position. |
| 4 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.GetVelocityCci()` — `public double3` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Source velocity = target velocity. |
| 5 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.GetBody2Cci()` — `public doubleQuat` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Orientation transforms. |
| 6 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.CenterOfMassAsmb` — `public double3 CenterOfMassAsmb` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Part-anchor offset base. |
| 7 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.BodyRates` — `public double3 BodyRates` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Passed to `Teleport`; NaN-guarded by mod. |
| 8 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.Orbit` — `public Orbit Orbit => Patch.Orbit` (reads `.OrbitLineColor`) | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Source orbit's line color reused for new orbit. |
| 9 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.Teleport(Orbit? orbit, doubleQuat? body2Cce, double3? bodyRates)` — `public void` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`; body identical bar a log line-number constant) | The core mutation. Nullable params; mod passes non-null. No new gating in 5402 — but the vehicle it moves is now subject to the new `PartFailure` contact-pressure system (see 5348→5402 summary). |
| 10 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Vehicle.UpdatePerFrameData()` — `public override void` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`; body identical) | Refresh caches post-teleport. |
| 11 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `IParentBody.GetCci2Cce()` — `doubleQuat` (interface) | `KSA/IParentBody.cs` | Yes | Same (file byte-identical) | Called on `Vehicle.Parent`. |
| 12 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Part.PositionVehicleAsmb` — `public double3` (computed property) | `KSA/Part.cs` | Yes | Same (OLD `Part.cs`) | Part-anchor position. |
| 13 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Part.Asmb2VehicleAsmb` — `public doubleQuat` (computed property) | `KSA/Part.cs` | Yes | Same (OLD `Part.cs`) | Part-anchor orientation. (5402 also added `Asmb2VehicleAsmb` to the nested `Part.Connection.IConnector` interface, `Part.cs` — unrelated to this binding.) |
| 14 | Direct typed API (write) | `garrys-torch.lib/WeldEngine.cs` | `Part.Scale` — `public double3 Scale { get; set; }` (setter calls `ResetCachedPosMatrixValues`) | `KSA/Part.cs` | Yes | Same (OLD `Part.cs`) | Recursive uniform scale write. |
| 15 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Part.SubParts` — `public ReadOnlySpan<Part> SubParts`; `PartTree.Parts` — `public ReadOnlySpan<Part> Parts` | `KSA/Part.cs`; `KSA/PartTree.cs` | Yes | Same (OLD `Part.cs`; `PartTree.cs`) | Part-tree walk for scaling + target-part list. |
| 16 | Direct typed API | `garrys-torch.lib/GarrysTorchSubmod.cs` | `Part.Template` (`public PartTemplate Template`) -> `PartTemplate.Id` (`public string Id`, inherited `SerializedId.Id`); `Part.Id` (`public string Id { get; init; }`) | `KSA/Part.cs`,`698`; `KSA/SerializedId.cs` | Yes | Same (OLD `Part.cs`,`690`) | Target-part combo labels. |
| 17 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Universe.GetJobSimStep(double dtPlayer)` -> `SimStep.NextTime` (`UniverseTime`, renamed from `SimTime` @5261) | `KSA/Universe.cs`; `KSA/SimStep.cs` | Yes | Same (OLD `Universe.cs`) | Tick-end time for the new orbit's state time (avoids SnapToLeader mismatch). |
| 18 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Program.GetPlayerDeltaTime()` — `public static double` | `KSA/Program.cs` | Yes | Same (OLD `Program.cs`) | Fed into `GetJobSimStep`. |
| 19 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Orbit.CreateFromStateCci(IParentBody parent, UniverseTime stateTime, double3 positionCci, double3 velocityCci, byte4 orbitLineColor)` — `public static Orbit` | `KSA/Orbit.cs` | Yes | Same (OLD `Orbit.cs`) | 5-arg factory; arg order/types unchanged since the 5261 `SimTime`→`UniverseTime` rename. |
| 20 | Direct typed API | `garrys-torch.lib/WeldEngine.cs` | `Orbit.OrbitLineColor` — `public byte4 OrbitLineColor` (field) | `KSA/Orbit.cs` | Yes | Same (OLD `Orbit.cs`) | — |
| 21 | Reflection (type-name) | `garrys-torch.lib/WeldEngine.cs` | `vehicle.GetType().Name == "KittenEva"` — `public class KittenEva : Vehicle` | `KSA/KittenEva.cs` | Yes | Same (OLD `KittenEva.cs`) | String type-name compare; breaks silently if the class is renamed. |
| 22 | Reflection (private field, string) | `garrys-torch.lib/WeldEngine.cs` (via `ReflectionHelpers.GetFieldValue`) | `KittenEva._renderable` — `private KittenRenderable _renderable` | `KSA/KittenEva.cs` | Yes | Same (OLD `KittenEva.cs`) | **String field name, not compile-checked.** |
| 23 | Reflection (private field, string) | `garrys-torch.lib/WeldEngine.cs` | `KittenRenderable._characterAvatar` — `private CharacterAvatar _characterAvatar` | `KSA/KittenRenderable.cs` | Yes | Same (OLD `KittenRenderable.cs`) | **String field name.** |
| 24 | Reflection (public field, string) | `garrys-torch.lib/WeldEngine.cs` | `CharacterAvatar.Core` — `public CharacterCore Core` (**struct** field) | `KSA/CharacterAvatar.cs` | Yes | Same (OLD `CharacterAvatar.cs`; shifted by the new `CharacterCore.HeadMeshIndices` list) | Mod uses `GetField("Core")` only (no property fallback) and writes the struct back via `SetValue` — correct **only while `Core` is a field of a value type**. If it becomes a property/ref type, scaling silently no-ops. |
| 25 | Reflection (public field, string) | `garrys-torch.lib/WeldEngine.cs` | `CharacterCore.Scale` — `public float Scale = 0.01f` (field) | `KSA/CharacterAvatar.cs` | Yes | Same (OLD `CharacterAvatar.cs`) | Mod sets `factor * 0.01f`; field + property paths both handled. |
| 26 | Direct typed API (UI color) | `garrys-torch.lib/GarrysTorchSubmod.cs` | `KSAColor.Xkcd.Scarlet`, `KSAColor.Xkcd.PaleGrey` — `static Color.Preset` | `KSA/KSAColor.cs`,`837` | Yes | Same (file byte-identical) | Unweld-button styling only; failure is visual, not functional. |
| 27 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs` (called `GarrysTorchSubmod.cs`) | `Universe.CurrentSystem` / `CelestialSystem.All` / `LookupCollection.UnsafeAsList` / `Vehicle.Id` | `KSA/Universe.cs` etc. | Yes | Same | Shared enumerator (see eternal-flame #12). |
| 28 | Harmony + Reflection | `unscience/Patcher.cs` -> `HotkeyGuard.cs` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool`, `nameof`-resolved | `KSA/GameSettings.cs` | Yes | Same (file byte-identical) | Shared guard. Installed once by the shared Unscience host. |
| 29 | Lifecycle | `unscience/Mod.cs` | StarMap attrs (full set); weld physics in `OnAfterUi` after `JobSystems.VehicleSolver.Wait()` | (StarMap.API package) | Yes | **Renamed @5261** (was `VehicleSolvers`) | See *Update-risk findings (5117 → 5261)* |

**Game assets referenced** — None (TOML preset file is mod-authored under `.unscience/`, not a game asset).

## i-feel-seen (`i-feel-seen` / `i-feel-seen.lib`)

**Purpose** — Render-distance / LOD-cull override. For user-selected ("tracked") vehicles,
two Harmony **prefixes** replace the vehicle's render-matrix and render-data computation so
the vehicle is drawn regardless of camera distance.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Exact or controlled vehicle selection. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Harmony (prefix) + Reflection (string) | `i-feel-seen.lib/IFeelSeenPatches.cs` (prefix body `:52`) | `Vehicle.GetWorldMatrix(Camera camera)` — `public float4x4?`; resolved `AccessTools.Method(typeof(Vehicle), "GetWorldMatrix")` (string), prefix `(Vehicle __instance, Camera camera, ref float4x4? __result)` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`; body identical) | **String-resolved**; method is `public`, non-virtual, single overload. Only game caller in both trees is `KittenEva.UpdateRenderData` (`KSA/KittenEva.cs`). |
| 2 | Harmony (prefix) + Reflection (string) | `i-feel-seen.lib/IFeelSeenPatches.cs` (prefix body `:64`) | `Vehicle.UpdateRenderData(IViewport viewport, int inFrameIndex)` — `public virtual void`; resolved `AccessTools.Method(typeof(Vehicle), "UpdateRenderData")`, prefix `(Vehicle __instance, IViewport viewport, int inFrameIndex)` | `KSA/Vehicle.cs` | Yes | **Retyped @5402** — `Viewport` → `IViewport` (OLD `Vehicle.cs`); mod prefix updated. Still the single `UpdateRenderData` overload. | **String-resolved.** `virtual`; `KittenEva` overrides it (`KSA/KittenEva.cs`, also `IViewport`) — see findings. Cull gate (`objectDiameterPixels < 1.0`) and the non-kitten call site (`KSA/Program.cs`) unchanged; `viewport == Program.MainViewport` became `viewport.IsMain()`. |
| 3 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs` | `Camera.GetPositionEgo(IPosition astronomical)` — `public double3` | `KSA/Camera.cs` | Yes | Same (OLD `Camera.cs`; body identical) | Passes `__instance` (Vehicle is `IPosition`). |
| 4 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs` | `Vehicle.Body2Cce` — `public doubleQuat Body2Cce` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Rotation for the override matrix. |
| 5 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs` | `IViewport.GetCamera()` — `Camera` (interface member; implemented by `GameViewport` via `ViewportBase`) | `KSA/IViewport.cs` | Yes | **Retyped @5402** — was `Viewport.GetCamera()` at `KSA/Viewport.cs`; `Viewport.cs` no longer exists | Mod receives the `IViewport` from the prefix and calls through the interface. |
| 6 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs` | `Vehicle.GetMatrixAsmb2Ego(Camera camera)` — `public double4x4` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | — |
| 7 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs` | `Vehicle.IsEditedVehicle` — `public bool` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Passed to `PartTree.UpdateRenderData`. |
| 8 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs` | `PartTree.UpdateRenderData(ref readonly double4x4 matrixAsmb2Ego, bool isEditedVehicle, IViewport viewport, int frameIndex)` — `public void` (via `Vehicle.Parts`, `KSA/Vehicle.cs`) | `KSA/PartTree.cs` | Yes | **Retyped @5402** — `Viewport` → `IViewport` (OLD `PartTree.cs`); body also gained a `Parachute.UpdateLineRenderData` loop (`:938-945`) | Mod passes `in matrixAsmb2Ego` -> `ref readonly`. Re-implements the original's body to bypass the cull check; because it calls the real `PartTree.UpdateRenderData`, tracked vehicles get chute lines too. Chute canopies are drawn by the new, uncalled-by-mod `Vehicle.UpdateParachuteRenderData(IViewport)` (`Vehicle.cs`, invoked without a distance cull from `Program.cs`). |
| 9 | Direct typed API | `i-feel-seen.lib/IFeelSeenSubmod.cs` + `VehicleTracker` | `VehicleProvider.GetAllVehicles()` chain + `Vehicle.Id`; tracked entries compared by reference | `KSA/Universe.cs` etc. | Yes | Same | Shared enumerator (see eternal-flame #12). |
| 10 | Harmony + Reflection | `unscience/Patcher.cs` -> `HotkeyGuard.cs` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool`, `nameof`-resolved | `KSA/GameSettings.cs` | Yes | Same (file byte-identical) | Shared guard. |
| 11 | Lifecycle | `unscience/Mod.cs` | StarMap attrs (full set) | (StarMap.API package) | Yes | Same | Patches applied in `OnFullyLoaded` after tracker init. |

**Game assets referenced** — None.

## Cross-cutting notes (all three mods)

- **Shared chokepoints to watch first** (a change breaks multiple mods at once):
  - `VehicleProvider` chain — `Universe.CurrentSystem` (`KSA/Universe.cs`),
    `CelestialSystem.All` (`KSA/CelestialSystem.cs`),
    `LookupCollection<Astronomical>.UnsafeAsList()` (`KSA/LookupCollection.cs`),
    `Vehicle.Id` (`KSA/Astronomical.cs`), `Program.ControlledVehicle` (`KSA/Program.cs`).
    Drives every mod's vehicle list. All signature-identical OLD->NEW.
  - `Universe.ExecuteNextVehicleSolvers(double, SimStep)` (`KSA/Universe.cs`) — patched by
    eternal-flame and central to garrys-torch's timing rationale.
  - `GameSettings.OnKeyAll` (`KSA/GameSettings.cs`) — shared `HotkeyGuard`, `nameof`-resolved.
- **Consolidated Harmony:** `unscience/Patcher.cs` owns the single host instance. Eternal Flame and Kiwi use their solver prefixes; I Feel Seen uses render prefixes. Garry’s Torch runs from the host’s after-GUI solver-safe phase. Retired standalone copies must not be installed alongside the host.
- **Mutation vs read:** eternal-flame and garrys-torch **write** game state
  (`Battery.Refill`/`Part.Scale`/`Vehicle.Teleport`); i-feel-seen **replaces** render computation
  via skip-original prefixes. None of these were affected by the 4680->4750 signature surface;
  the only build-induced breakage is the garrys-torch CS8604 (Brutal nullability, rev 4729).

---

## Historical evidence

See [dated integration and upgrade reference](history/vehicle-physics.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.
