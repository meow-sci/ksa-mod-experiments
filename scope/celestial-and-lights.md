# Scope: Celestial Welding & Lights/Actions (kiwis-marbles, zippo)

## Creative tools — current integration

Zippo additionally owns Disco via `DiscoRecipe`, `DiscoTiming`, `DiscoLight` and `ZippoSubmod.Disco`. Each live record clones its recipe; save/load touches only authoring fields. All-craft Apply resolves exact live lights once; new craft lights are not auto-enrolled. Ordinary Zippo color/intensity retains its legacy shared-template scope.

| Touchpoint (KSA 2026.9.7.5402) | Owner and behavior |
|---|---|
| `Part.Modules.Get<LightModule>()`; `LightModule.Template`, `TemplateData.{Id,Type,Transform,Range,Intensity,ColorRgb,InnerAngle,OuterAngle,RayTracing,DisableInIva}` | `DiscoLight` manually copies all template fields. Own ColorRgb/angle references only for enabled channels; restore original template only if module still points to the owned copy. Point lights skip cone angles. UpdateRenderData consumes these exact fields. |
| `ColorRgbReference(float3)`, `R/G/B`, `OnDataLoad(null)`; `FloatReference(float)`, `Value` | Instance-local color refresh and degree-to-radian half-angle interpolation. No shared color-template mutation or new GPU resource. |
| `Part.FullPart.Modules.Get<KeyframeAnimationModule>()`; `Shared.{Duration,PartLookup}`; `TimeGoal` | Select drivers whose animation targets the light subpart ID; set normalized goal × Duration, as the native Actuate slider does. Game solver controls actual movement. A shared driver has one owner; later Apply releases old ownership and captures the original goal. Restore only if the last written goal remains current. |
| `Part.LightSwitch`, `FullPart.LightSwitch`, `LightIsActive` | Explicit live assembly switch control; Disco start leaves switches unchanged. |
| `VehicleProvider.GetAllVehicles(includeDebris:true)`, `PartHelpers.GetAllParts` | Check exact runtime references each update; disappeared lights restore/release their owned state. No retargeting. |

No Harmony target or shader layout is added. Pausing holds recipe time, not solver motion already targeting a goal. Native actuation, isolation, external-template replacement and unload behavior require live verification.

## Workspace integration (current)

Active bundled features: **kiwis-marbles, zippo**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

LightController now belongs to `ksa-lights.lib` (namespace `MeowSci.KsaLights`) and is shared with its-so-shiny. Zippo’s exact live Part references have host-assigned identities (`LiveIdentity`), separate from persistent draft part identities. Color/intensity still edit shared light TemplateData; LightSwitch is per-part. Queue updates cancel when the exact managed part disappears. No new Harmony patch or shader dependency.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference cataloging how two Unscience features integrate with the KSA game,
for detecting when a game update breaks them.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard and feature Harmony patches are wired through `unscience/Patcher.cs`. See [architecture](00-architecture-and-abstractions.md).

---

## kiwis-marbles

**Purpose** — "Celestial welding": teleports a `Celestial` (planet/moon = *source*) every frame to maintain a
user-set CCI offset relative to any `IOrbiter` (*target*; celestial or vehicle). Re-parents the source via
`SetOrbit` when the target sits under a different parent. Multiple welds are processed in dependency order
(Kahn topological sort) so weld chains (Moon→Earth→Mars) resolve correctly.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**Timing (why the solver prefix)** — Since 2026.8.x `Celestial`s are propagated by `CelestialUpdateTask`
jobs on `JobSystems.OrbitSolvers` worker threads: `Universe.ExecuteNextOrbitSolvers` queues one per body
(snapshots `Celestial.Orbit`, computes `GetStateVectorsAt(simStep.NextTime)`); next frame
`Program.PrepareFrame` does `OrbitSolvers.Wait()` → `Universe.ApplyOrbitSolvers()` (`Orbit.UpdatePosition`) →
`Universe.ApplyVehicleSolvers()` (ends in `CelestialSystem.UpdatePerFrameData()`) → `ExecuteNextVehicleSolvers`
→ `ExecuteNextOrbitSolvers`. Mutating `Orbit` from a render-loop hook races the worker and is overwritten by
the staged result (the pre-fix symptom: welds had no visible effect). The prefix runs in the only safe window:
main thread, solvers drained, results applied, next step not yet queued, all target positions current.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Source body, target orbiter and offset/unit selection. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed | `CelestialWeldEngine.ApplyOrbit` | `Celestial.SetOrbit(Orbit newOrbit)` | `Celestial.cs` | Yes | Same | Bare `Orbit = newOrbit`. Does **not** touch `Children` (never did — earlier "auto-reparents" note was wrong); engine re-parents explicitly (#2b). |
| 2 | Direct typed | `CelestialWeldEngine.ApplyOrbit` | `IParentBody.UpdatePerFrameDataTree() : void` (default interface method) | `IParentBody.cs` | Yes | Same | Refreshes cached CCI/CCE/ECL data for the body + its subtree after the swap (replaces the old bare `UpdatePerFrameData()` call). |
| 2b | Direct typed | `CelestialWeldEngine.Reparent` | `IParentBody.Children : List<IOrbiter>`; `Orbit.Parent : IParentBody`; `Celestial.Parent => Orbit.Parent` | `IParentBody.cs`; `Orbit.cs`; `Celestial.cs` | Yes | Same | Cross-parent weld/restore moves the body between old/new parent lists (drives `UpdatePerFrameDataTree` order + orbit-tree UI). |
| 2c | Harmony prefix (`Priority.First`) | `KiwisMarblesPatches.cs` (`AccessTools.Method` by name) | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep) : static void` | `Universe.cs` | Yes | Body identical (OLD `:1767`) | Sim-step driver for all weld work. Shared keystone with eternal-flame; single overload so by-name lookup is safe. Sequence dependency: must stay *after* `ApplyOrbitSolvers`/`ApplyVehicleSolvers` and *before* `ExecuteNextOrbitSolvers` in `Program.PrepareFrame` (`Program.cs`). 5402 inserted the parachute cloth solvers into the same sequence (`ClothSolvers.Wait()`/`ApplyClothSolvers()` before, `ExecuteNextClothSolvers` immediately before this call at `:2144`) — the weld window is unchanged. |
| 3 | Direct typed | `CelestialWeldEngine.cs` | `Orbit.CreateFromStateCci(IParentBody, UniverseTime, double3, double3, byte4) : Orbit` (static) | `Orbit.cs` | Yes | **Identical sig** (OLD `:1563`) | 5-arg state-vector → orbit. Arg types must stay (IParentBody/UniverseTime/double3/double3/byte4). `UniverseTime` replaced `SimTime` at rev 5211. |
| 4 | Direct typed | `CelestialWeldEngine.cs` | `Celestial.OrbitColor : byte4 { get; protected set; }` (via IOrbiter) | `Celestial.cs`; `IOrbiter.cs` | Yes | Same (OLD `:77`) | Passed as orbit line color to #3. |
| 5 | Direct typed | `CelestialWeldEngine.cs` | `IOrbiter.Parent : IParentBody { get; }` (= `Orbit.Parent`) | `IOrbiter.cs` | Yes | Same | Null-checked before weld. |
| 6 | Direct typed | `CelestialWeldEngine.cs`; `KiwisMarblesSubmod.cs` | `IOrbiter.Orbit : Orbit { get; }` / `Celestial.Orbit { get; set; }` | `IOrbiter.cs`; `Celestial.cs` | Yes | Same (OLD `:71`) | Source `.Orbit` saved for restore. |
| 7 | Direct typed | `CelestialWeldEngine.cs` | `IOrbiter.GetPositionCci() : double3` | `IOrbiter.cs` | Yes | Same | Target CCI position each frame. |
| 8 | Direct typed | `CelestialWeldEngine.cs` | `IOrbiter.GetVelocityCci() : double3` | `IOrbiter.cs` | Yes | Same | Target CCI velocity each frame. |
| 9 | Direct typed | `KiwisMarblesSubmod.cs` | `Celestial.MeanRadius : double` (override) | `Celestial.cs` | Yes | Same (OLD `:91`) | Surface-placement helper only. |
| 10 | Direct typed | `KiwisMarblesSubmod.cs` (via `CelestialProvider`) | `Universe.CurrentSystem : CelestialSystem? { get; }` → `.All : LookupCollection<Astronomical>` → `.UnsafeAsList()` | `Universe.cs`; `CelestialSystem.cs`; `LookupCollection.cs` | Yes | Same (`All` OLD `:57`) | Source list `OfType<Celestial>()`, target list `OfType<IOrbiter>()`. |
| 11 | Direct typed | `CelestialWeldEngine.cs` (via `SimTimeProvider`) | `Universe.GetElapsedTime() : UniverseTime` (static) | `Universe.cs` | Yes | Same (OLD `:2060`) | State time for #3. (Was `GetElapsedSimTime() : SimTime` before rev 5211.) |
| 12 | Cast/type | `CelestialWeldEngine.cs`; `KiwisMarblesSubmod.cs` | `(IOrbiter)Celestial` cast; `IParentBody` as parent type | `IOrbiter.cs`, `IParentBody.cs` | Yes | Same | Celestial implements IOrbiter (topo-sort edge test). |
| 13 | Lifecycle/Harmony | `Patcher.cs` | `HotkeyGuard` → `GameSettings.OnKeyAll(GlfwKeyEvent) : bool` | `GameSettings.cs` | Yes | Same | Shared guard; PatchAll defines no own patches. |

**Game assets referenced** — None. Bodies are discovered live from `Universe.CurrentSystem`; no model/texture/path lookups.

## zippo

**Purpose** — Select a vehicle and one of its light parts, then control intensity/color in real time, toggle
on/off, and queue single-step color+intensity animations with easing. Its programmatic methods use the same live registry as the UI; there is no RPC server.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Exact/controlled vehicle and light part, enabled state, intensity/color, animation endpoints, timing and easing. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | **Reflection (string type)** | `ksa-lights.lib/LightController.cs` | type name `"KSA.LightModule+TemplateData"` (nested) | `LightModule.cs` (`[XmlType("Light")] class TemplateData`) | Yes | Same | **High runtime risk**: hard-coded full name. Rename/move of nested type silently yields zero light parts. |
| 2 | **Reflection (string field)** | `ksa-lights.lib/LightController.cs` | `PartTemplate.Components : List<ModuleBase.TemplateDataBase>` (field) | `PartTemplate.cs` | Yes | Same (OLD `:107`; shifted by new `CrashTolerance`/`SubPartGroups` fields) | Field name `"Components"` must persist. |
| 3 | **Reflection (string field)** | `ksa-lights.lib/LightController.cs` | `LightModule.TemplateData.Intensity : FloatReference` (field) → `FloatReference.Value : float` | `LightModule.cs`; `FloatReference.cs` | Yes | Same | Intensity read/write **works**. Field names `"Intensity"`/`"Value"` must persist. |
| 4 | Reflection (string field) — **FIXED (Phase 4)** | `ksa-lights.lib/LightController.cs` | reads/writes field `"ColorRgb"` on `TemplateData` | `LightModule.cs` (`ColorRgbReference ColorRgb`) | Yes | Same | Was `"Color"` (the `[XmlElement("Color")]` XML name, not the C# field) ⇒ `GetField`→null ⇒ color was a silent no-op in both 4680 and 4750. Now `"ColorRgb"`; the C# field name must persist. |
| 5 | Reflection (string field/method) + **typed enum** | `ksa-lights.lib/LightController.cs` | `ColorRgbReference.R/G/B : float` + `OnDataLoad(Mod) : void`; write side clears `IndexedColor` to `KSA.IndexedColor.Invalid` | `ColorRgbReference.cs` | Yes | Same | Now reachable (post-#4). `OnDataLoad` re-derives R/G/B from `IndexedColor` unless it is `Invalid`, so `WriteColor` sets `IndexedColor = KSA.IndexedColor.Invalid` (typed — **compile-checked**, breaks loudly) before `OnDataLoad(null)`. |
| 6 | Direct typed | `ZippoSubmod.cs` | `Part.LightSwitch : PowerConsumer?` (field) | `Part.cs` | Yes | Same (OLD `:678`) | On/off path. Consumer side changed in 5402: `LightModule.IsActive`/`PartModelModule.UpdateRenderData` now read the new `Part.IsLightSwitchedOff()` (`Part.cs` = `!LightIsActive \|\| !IsSwitchedOn()`, plus a `lightSwitch.Parent.Tree != Tree ⇒ not off` precondition). `LightIsActive` is still the first term, so the write still works. |
| 7 | Direct typed | `ZippoSubmod.cs` | `Part.FullPart : Part { get; }` | `Part.cs` | Yes | Same (OLD `:1056`) | `.FullPart.LightSwitch` fallback. |
| 8 | Direct typed | `ZippoSubmod.cs` | `PowerConsumer.LightIsActive : bool` (field) | `PowerConsumer.cs` | Yes | Same | On/off toggle. Electrical refactor (4681) didn't touch this field; 5402 added `PowerConsumer.IsSwitchedOn()` (`:50-54`, bounds-checked `StatesIdx`) next to it. |
| 9 | Direct typed | `ksa-lights.lib/LightController.cs` | `Part.Template : PartTemplate` (field) | `Part.cs` | Yes | Same (OLD `:568`) | Feeds reflection in #1–#5. |
| 10 | Direct typed | `ZippoSubmod.cs`; `ksa-lights.lib/LightController.cs` (via `PartHelpers`) | `Vehicle.Parts : PartTree` → `PartTree.Parts : ReadOnlySpan<Part>` | `Vehicle.cs`; `PartTree.cs` | Yes | Same (OLD `:598`; `:95`) | Part enumeration root. |
| 11 | Direct typed | `ksa-lights.lib/LightController.cs` (recursion); `PartHelpers.cs` | `Part.SubParts : ReadOnlySpan<Part>` | `Part.cs` | Yes | Same (OLD `:1052`) | Recursive light search. |
| 12 | Direct typed | `ZippoSubmod.cs` (combo labels) | `Part.Id : string { get; init; }`, `Part.DisplayName : string { get; init; }` | `Part.cs` | Yes | Same (OLD `:690,692`) | Display/keys. 5402 initialises `DisplayName` from `Template.DisplayName` when it differs from `Template.Id` (`Part.cs`; was `= Id`) — labels may change, keys (`Id`) don't. |
| 13 | Reflection (palette) | `ZippoSubmod.cs` (via `XkcdColorHelper.GetAll`) | `KSAColor.Xkcd` static props → `Color.Preset` | `KSAColor.cs` | Yes | Same | Reflects all `Xkcd` static color props; cast `(Color.Preset)`. Rename of `Xkcd`/prop-type change would empty the combo. |
| 14 | Direct typed | `ksa-lights.lib/LightController.cs` | hard-coded preset float3 (Marine/HotPink/RadioactiveGreen/BabyPurple) | n/a (constants from `KSAColor.cs`) | n/a | n/a | Hard-coded RGB; cosmetic only, no runtime dependency. |
| 15 | Lifecycle/Harmony | `Patcher.cs` | `HotkeyGuard` → `GameSettings.OnKeyAll` | `GameSettings.cs` | Yes | Same | Shared. |

**Game assets referenced** — None.

## Cross-mod summary

- **No update-driven breaks (4680→4750)** for any of the three mods. Every typed member used is identical across
  versions and the solution compiles against the 4750 DLLs.
- **Former latent bug — now FIXED (Phase 4):** zippo color get/set previously targeted a non-existent field
  `"Color"` (actual: `ColorRgb`) → was a silent no-op in both 4680 and 4750. `LightController` now uses
  `ColorRgb` and clears `IndexedColor` on write.
- **Highest ongoing runtime risk = string-based reflection** (invisible to the compiler):
  - zippo: `"KSA.LightModule+TemplateData"`, `Components`, `Intensity`/`Value`, `ColorRgb`/`R`/`G`/`B`/`OnDataLoad`, `KSAColor.Xkcd` props (plus one typed dep, `KSA.IndexedColor`, which breaks loudly at compile).
  - kiwis-marbles: **none** (fully typed) — no string-reflection lookup in the weld engine.

---

## Historical evidence

See [dated integration and upgrade reference](history/celestial-and-lights.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.
