# Scope: Celestial Welding & Lights/Actions (kiwis-marbles, zippo, red-alert)

Permanent reference cataloging how three unscience mods integrate with the KSA game,
for detecting when a game update breaks them.

**Versions compared**
- NEW = `2026.9.7.5402` — decomp root `~/repos/meow-sci/ksa-game-assemblies/current/decomp`
- OLD = `2026.8.22.5348` — decomp root `~/repos/meow-sci/ksa-game-assemblies_prev/current/decomp`
- Decomp paths below are relative to `<root>/KSA/` unless noted. Line numbers are NEW (5402); "OLD" = 5348.
  Verified by grep/diff of both trees.

**Shared integration (all three mods)** — each mod's `Patcher.cs` calls
`HotkeyGuard.Patch/Unpatch` (`ksa-abstractions.lib/HotkeyGuard.cs`). HotkeyGuard Harmony-patches
`GameSettings.OnKeyAll(GlfwKeyEvent) : bool` (NEW `GameSettings.cs:3301`, prefix with `ref bool __result`;
`GameSettings.cs` is byte-identical 5348↔5402).
All three call `_harmony.PatchAll(...)` but define **no** `[HarmonyPatch]` methods of their own, so PatchAll
is a no-op aside from HotkeyGuard. Lifecycle is StarMap attributes (`[StarMapMod]`,
`[StarMapImmediateLoad]`, `[StarMapAllModsLoaded]`, `[StarMapBeforeGui]`, `[StarMapAfterGui]`,
`[StarMapUnload]`) on `Mod.cs`, plus `MeowSci.KsaAbstractions.ISubmod` implemented by each `*Submod`.
None of the three persists any state.

---

## kiwis-marbles

**Purpose** — "Celestial welding": teleports a `Celestial` (planet/moon = *source*) every frame to maintain a
user-set CCI offset relative to any `IOrbiter` (*target*; celestial or vehicle). Re-parents the source via
`SetOrbit` when the target sits under a different parent. Multiple welds are processed in dependency order
(Kahn topological sort) so weld chains (Moon→Earth→Mars) resolve correctly.

**Unscience integration** — Standalone StarMap mod hosting `KiwisMarblesSubmod : ISubmod` (also bundled in
the unscience toolbox). Weld application is driven by `KiwisMarblesPatches`, a `Priority.First` Harmony prefix
on `Universe.ExecuteNextVehicleSolvers` → `KiwisMarblesSubmod.Instance.UpdateBeforeVehicleSolvers()`, which
calls `CelestialWeldEngine.UpdateWeld` per weld and applies deferred unweld restores. `ISubmod.Update(dt)`
(`[StarMapBeforeGui]`) is a deliberate no-op — see *Timing* below. Discovers bodies through
`CelestialProvider` (abstractions). Stateless math in `CelestialWeldEngine`.

**Timing (why the solver prefix)** — Since 2026.8.x `Celestial`s are propagated by `CelestialUpdateTask`
jobs on `JobSystems.OrbitSolvers` worker threads: `Universe.ExecuteNextOrbitSolvers` queues one per body
(snapshots `Celestial.Orbit`, computes `GetStateVectorsAt(simStep.NextTime)`); next frame
`Program.PrepareFrame` does `OrbitSolvers.Wait()` → `Universe.ApplyOrbitSolvers()` (`Orbit.UpdatePosition`) →
`Universe.ApplyVehicleSolvers()` (ends in `CelestialSystem.UpdatePerFrameData()`) → `ExecuteNextVehicleSolvers`
→ `ExecuteNextOrbitSolvers`. Mutating `Orbit` from a render-loop hook races the worker and is overwritten by
the staged result (the pre-fix symptom: welds had no visible effect). The prefix runs in the only safe window:
main thread, solvers drained, results applied, next step not yet queued, all target positions current.

**UI/hotkeys** — **F9** toggles the window (`Mod.cs:51`). ImGui (Brutal.ImGuiApi): filterable Source/Target
combos, `DragFloat3` offset + unit combo (m/km/Mm/Gm), per-weld live offset editor with a surface/lat-lon mode,
red "Unweld" button. Renders inside the Unscience toolbox via `ISubmod.RenderContent`.

**Persistence** — None. `CelestialWeldEntry.OriginalOrbit` is captured in memory at weld time to restore the
body on unweld; welds are lost on reload (README §Notes).

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed | `CelestialWeldEngine.ApplyOrbit` | `Celestial.SetOrbit(Orbit newOrbit)` | `Celestial.cs:153` | Yes | Same | Bare `Orbit = newOrbit`. Does **not** touch `Children` (never did — earlier "auto-reparents" note was wrong); engine re-parents explicitly (#2b). |
| 2 | Direct typed | `CelestialWeldEngine.ApplyOrbit` | `IParentBody.UpdatePerFrameDataTree() : void` (default interface method) | `IParentBody.cs:110` | Yes | Same | Refreshes cached CCI/CCE/ECL data for the body + its subtree after the swap (replaces the old bare `UpdatePerFrameData()` call). |
| 2b | Direct typed | `CelestialWeldEngine.Reparent` | `IParentBody.Children : List<IOrbiter>`; `Orbit.Parent : IParentBody`; `Celestial.Parent => Orbit.Parent` | `IParentBody.cs:27`; `Orbit.cs:1186`; `Celestial.cs:73` | Yes | Same | Cross-parent weld/restore moves the body between old/new parent lists (drives `UpdatePerFrameDataTree` order + orbit-tree UI). |
| 2c | Harmony prefix (`Priority.First`) | `KiwisMarblesPatches.cs:24-32` (`AccessTools.Method` by name) | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep) : static void` | `Universe.cs:1834` | Yes | Body identical (OLD `:1767`) | Sim-step driver for all weld work. Shared keystone with eternal-flame/kitchen-sink; single overload so by-name lookup is safe. Sequence dependency: must stay *after* `ApplyOrbitSolvers`/`ApplyVehicleSolvers` and *before* `ExecuteNextOrbitSolvers` in `Program.PrepareFrame` (`Program.cs:2103-2146`). 5402 inserted the parachute cloth solvers into the same sequence (`ClothSolvers.Wait()`/`ApplyClothSolvers()` before, `ExecuteNextClothSolvers` immediately before this call at `:2144`) — the weld window is unchanged. |
| 3 | Direct typed | `CelestialWeldEngine.cs:42-48` | `Orbit.CreateFromStateCci(IParentBody, UniverseTime, double3, double3, byte4) : Orbit` (static) | `Orbit.cs:1563` | Yes | **Identical sig** (OLD `:1563`) | 5-arg state-vector → orbit. Arg types must stay (IParentBody/UniverseTime/double3/double3/byte4). `UniverseTime` replaced `SimTime` at rev 5211. |
| 4 | Direct typed | `CelestialWeldEngine.cs:47` | `Celestial.OrbitColor : byte4 { get; protected set; }` (via IOrbiter) | `Celestial.cs:77`; `IOrbiter.cs:24` | Yes | Same (OLD `:77`) | Passed as orbit line color to #3. |
| 5 | Direct typed | `CelestialWeldEngine.cs:32,37` | `IOrbiter.Parent : IParentBody { get; }` (= `Orbit.Parent`) | `IOrbiter.cs:18` | Yes | Same | Null-checked before weld. |
| 6 | Direct typed | `CelestialWeldEngine.cs:32`; `KiwisMarblesSubmod.cs:483` | `IOrbiter.Orbit : Orbit { get; }` / `Celestial.Orbit { get; set; }` | `IOrbiter.cs:16`; `Celestial.cs:71` | Yes | Same (OLD `:71`) | Source `.Orbit` saved for restore. |
| 7 | Direct typed | `CelestialWeldEngine.cs:35` | `IOrbiter.GetPositionCci() : double3` | `IOrbiter.cs:48` | Yes | Same | Target CCI position each frame. |
| 8 | Direct typed | `CelestialWeldEngine.cs:36` | `IOrbiter.GetVelocityCci() : double3` | `IOrbiter.cs:62` | Yes | Same | Target CCI velocity each frame. |
| 9 | Direct typed | `KiwisMarblesSubmod.cs:196-197,369,382` | `Celestial.MeanRadius : double` (override) | `Celestial.cs:91` | Yes | Same (OLD `:91`) | Surface-placement helper only. |
| 10 | Direct typed | `KiwisMarblesSubmod.cs:113,114` (via `CelestialProvider`) | `Universe.CurrentSystem : CelestialSystem? { get; }` → `.All : LookupCollection<Astronomical>` → `.UnsafeAsList()` | `Universe.cs:94`; `CelestialSystem.cs:64`; `LookupCollection.cs:210` | Yes | Same (`All` OLD `:57`) | Source list `OfType<Celestial>()`, target list `OfType<IOrbiter>()`. |
| 11 | Direct typed | `CelestialWeldEngine.cs:44` (via `SimTimeProvider`) | `Universe.GetElapsedTime() : UniverseTime` (static) | `Universe.cs:2114` | Yes | Same (OLD `:2060`) | State time for #3. (Was `GetElapsedSimTime() : SimTime` before rev 5211.) |
| 12 | Cast/type | `CelestialWeldEngine.cs:119`; `KiwisMarblesSubmod.cs:194,257` | `(IOrbiter)Celestial` cast; `IParentBody` as parent type | `IOrbiter.cs`, `IParentBody.cs` | Yes | Same | Celestial implements IOrbiter (topo-sort edge test). |
| 13 | Lifecycle/Harmony | `Patcher.cs:22,39` | `HotkeyGuard` → `GameSettings.OnKeyAll(GlfwKeyEvent) : bool` | `GameSettings.cs:3301` | Yes | Same | Shared guard; PatchAll defines no own patches. |

**Game assets referenced** — None. Bodies are discovered live from `Universe.CurrentSystem`; no model/texture/path lookups.

**Update-risk findings (4680→4750)** — No breaking deltas detected. `Celestial`, `IOrbiter`, `IParentBody`,
`Orbit.CreateFromStateCci`, `Universe`/`CelestialSystem`/`LookupCollection` members are byte-for-byte identical
across versions (only line numbers shifted). All access is typed (no string reflection), so the compile against
4750 DLLs (already green) fully covers this mod's surface.

---

## zippo

**Purpose** — Select a vehicle and one of its light parts, then control intensity/color in real time, toggle
on/off, and queue single-step color+intensity animations with easing. Also exposes an RPC API
(`ZippoSubmod` public methods) for unladen-swallow.

**Unscience integration** — `ZippoSubmod : ISubmod` (static `Instance` for RPC). `Update(dt)` drives
`LightAnimationManager` which re-applies interpolated color/intensity each frame. Light access is centralized in
the stateless `LightController`. Vehicles via `VehicleProvider`, part-tree walk via `PartHelpers.GetPartsWhere`,
XKCD palette via `XkcdColorHelper` (all abstractions).

**UI/hotkeys** — **F11** toggles the window (`Mod.cs:47`). Vehicle/light-part filterable combos, intensity
`DragFloat`, "Default/preset/(Custom)" color combo, `ColorEdit4` picker, animation builder (start/end XKCD color
combos + intensity/duration/easing/power) with a progress bar, and a Debug "Dump Parts" button.

**Persistence** — None. `_originalColors` dict and per-part animation queues are in-memory only.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | **Reflection (string type)** | `LightController.cs:39` | type name `"KSA.LightModule+TemplateData"` (nested) | `LightModule.cs:12` (`[XmlType("Light")] class TemplateData`) | Yes | Same | **High runtime risk**: hard-coded full name. Rename/move of nested type silently yields zero light parts. |
| 2 | **Reflection (string field)** | `LightController.cs:33` | `PartTemplate.Components : List<ModuleBase.TemplateDataBase>` (field) | `PartTemplate.cs:113` | Yes | Same (OLD `:107`; shifted by new `CrashTolerance`/`SubPartGroups` fields) | Field name `"Components"` must persist. |
| 3 | **Reflection (string field)** | `LightController.cs:50,71` | `LightModule.TemplateData.Intensity : FloatReference` (field) → `FloatReference.Value : float` | `LightModule.cs:30`; `FloatReference.cs:9` | Yes | Same | Intensity read/write **works**. Field names `"Intensity"`/`"Value"` must persist. |
| 4 | Reflection (string field) — **FIXED (Phase 4)** | `LightController.cs:59,80` | reads/writes field `"ColorRgb"` on `TemplateData` | `LightModule.cs:33` (`ColorRgbReference ColorRgb`) | Yes | Same | Was `"Color"` (the `[XmlElement("Color")]` XML name, not the C# field) ⇒ `GetField`→null ⇒ color was a silent no-op in both 4680 and 4750. Now `"ColorRgb"`; the C# field name must persist. |
| 5 | Reflection (string field/method) + **typed enum** | `LightController.cs:61-63,82-89` | `ColorRgbReference.R/G/B : float` + `OnDataLoad(Mod) : void`; write side clears `IndexedColor` to `KSA.IndexedColor.Invalid` | `ColorRgbReference.cs:10,13,16,19,35` | Yes | Same | Now reachable (post-#4). `OnDataLoad` re-derives R/G/B from `IndexedColor` unless it is `Invalid`, so `WriteColor` sets `IndexedColor = KSA.IndexedColor.Invalid` (typed — **compile-checked**, breaks loudly) before `OnDataLoad(null)`. |
| 6 | Direct typed | `ZippoSubmod.cs:152,441,465` | `Part.LightSwitch : PowerConsumer?` (field) | `Part.cs:686` | Yes | Same (OLD `:678`) | On/off path. Consumer side changed in 5402: `LightModule.IsActive`/`PartModelModule.UpdateRenderData` now read the new `Part.IsLightSwitchedOff()` (`Part.cs:1357-1369` = `!LightIsActive \|\| !IsSwitchedOn()`, plus a `lightSwitch.Parent.Tree != Tree ⇒ not off` precondition). `LightIsActive` is still the first term, so the write still works. |
| 7 | Direct typed | `ZippoSubmod.cs:152,441,465` | `Part.FullPart : Part { get; }` | `Part.cs:1123` | Yes | Same (OLD `:1056`) | `.FullPart.LightSwitch` fallback. |
| 8 | Direct typed | `ZippoSubmod.cs:161,442,467` | `PowerConsumer.LightIsActive : bool` (field) | `PowerConsumer.cs:30` | Yes | Same | On/off toggle. Electrical refactor (4681) didn't touch this field; 5402 added `PowerConsumer.IsSwitchedOn()` (`:50-54`, bounds-checked `StatesIdx`) next to it. |
| 9 | Direct typed | `LightController.cs:95,98,102,106` | `Part.Template : PartTemplate` (field) | `Part.cs:576` | Yes | Same (OLD `:568`) | Feeds reflection in #1–#5. |
| 10 | Direct typed | `ZippoSubmod.cs:405`; `LightController.cs:102` (via `PartHelpers`) | `Vehicle.Parts : PartTree` → `PartTree.Parts : ReadOnlySpan<Part>` | `Vehicle.cs:604`; `PartTree.cs:95` | Yes | Same (OLD `:598`; `:95`) | Part enumeration root. |
| 11 | Direct typed | `LightController.cs:133-134` (recursion); `PartHelpers.cs` | `Part.SubParts : ReadOnlySpan<Part>` | `Part.cs:1079` | Yes | Same (OLD `:1052`) | Recursive light search. |
| 12 | Direct typed | `ZippoSubmod.cs:444-445` (combo labels) | `Part.Id : string { get; init; }`, `Part.DisplayName : string { get; init; }` | `Part.cs:698,700` | Yes | Same (OLD `:690,692`) | Display/keys. 5402 initialises `DisplayName` from `Template.DisplayName` when it differs from `Template.Id` (`Part.cs:1391`; was `= Id`) — labels may change, keys (`Id`) don't. |
| 13 | Reflection (palette) | `ZippoSubmod.cs:253,284` (via `XkcdColorHelper.GetAll`) | `KSAColor.Xkcd` static props → `Color.Preset` | `KSAColor.cs:23` | Yes | Same | Reflects all `Xkcd` static color props; cast `(Color.Preset)`. Rename of `Xkcd`/prop-type change would empty the combo. |
| 14 | Direct typed | `LightController.cs:20-27` | hard-coded preset float3 (Marine/HotPink/RadioactiveGreen/BabyPurple) | n/a (constants from `KSAColor.cs`) | n/a | n/a | Hard-coded RGB; cosmetic only, no runtime dependency. |
| 15 | Lifecycle/Harmony | `Patcher.cs:19,31` | `HotkeyGuard` → `GameSettings.OnKeyAll` | `GameSettings.cs:3301` | Yes | Same | Shared. |

**Game assets referenced** — None.

**Update-risk findings (4680→4750)**
- **No breaking deltas from the update.** `LightModule.TemplateData`, `FloatReference`, `ColorRgbReference`,
  `Part`, `PowerConsumer`, `Vehicle`/`PartTree`, `KSAColor.Xkcd` are identical across 4680 and 4750.
- **Color get/set — FIXED (Phase 4).** Zippo previously reflected the field `"Color"`, but the C# field is
  `ColorRgb` (`[XmlElement("Color")]` is only the XML element name) ⇒ `GetField`→null ⇒ color was a silent
  no-op in both 4680 and 4750 (intensity and on/off always worked). `LightController` now reads/writes
  `"ColorRgb"`, and `WriteColor` additionally clears `IndexedColor` (`KSA.IndexedColor.Invalid`) so
  `ColorRgbReference.OnDataLoad` keeps the written RGB instead of re-deriving it from a named/indexed color.
- **Watch (string reflection surface):** items #1–#5 and #13 are the only update-fragile points. A future
  rename of `LightModule.TemplateData`, its `Components`/`Intensity`/`ColorRgb` fields, `FloatReference.Value`,
  `ColorRgbReference.{R,G,B,OnDataLoad}`, or `KSAColor.Xkcd` would fail silently at runtime (no compile error).
  Zippo now also has **one typed** game dependency — `KSA.IndexedColor.Invalid` in `WriteColor` — which would
  fail at **compile** (not silently) if that enum is renamed/moved.
- **Electrical refactor (4681):** `LightModule.UpdateRenderData` now also gates on the part's PowerConsumer
  state, but the on/off switch remains `PowerConsumer.LightIsActive` (unchanged) — no zippo impact.

---

## red-alert

**Purpose** — Build reusable **action plans** that bundle one-click actions across light parts and solar panels
(light on/off/toggle/color, light "actuate", solar deploy/retract/toggle, solar "actuate"). One **Engage** button
runs every action in order.

**Unscience integration** — `RedAlertSubmod : ISubmod` (static `Instance`). `ActionScanner` discovers each
top-level part's capabilities by inspecting its module subtree; `ActionExecutor` resolves a `PlannedAction` to a
live `Part` (by `InstanceId`) and performs it; `LightActions` does typed color + on/off (with per-instance
TemplateData cloning). Vehicles via `VehicleProvider`.

**UI/hotkeys** — **F11** toggles the window (`Mod.cs:52`). Create-plan form, collapsible plan list, add-action
form with filtered Vehicle/Part/Action combos (the part list shows each part's capabilities; the action list is
filtered to what the part supports), per-action `ColorEdit4`/actuate `DragFloat`, red "Engage" button.

**Persistence** — None. Plans and form state are in-memory only.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed | `ActionScanner.cs:17`; `ActionExecutor.cs:73` | `Vehicle.Parts : PartTree` → `PartTree.Parts : ReadOnlySpan<Part>` | `Vehicle.cs:604`; `PartTree.cs:95` | Yes | Same (OLD `:598`; `:95`) | Top-level part scan + resolve. |
| 2 | Direct typed | `ActionScanner.cs:68` | `Part.Modules : ModuleList` → `ModuleList.Get<LightModule>() : Span<LightModule>` | `Part.cs:680`; `ModuleList.cs:178` | Yes | Same (OLD `:672`; `:177`) | Light detection. `LightModule : Module<LightModule>, IDisposable` satisfies `Get<T>` constraint. |
| 3 | Direct typed | `ActionScanner.cs:48`; `ActionExecutor.cs:82` | `Part.SubtreeModules : ModuleList` → `.Get<KeyframeAnimationModule>() : Span<...>` | `Part.cs:688`; `ModuleList.cs:178` | Yes | Same (OLD `:680`) | Anim detection + actuation handle. |
| 4 | Direct typed | `ActionScanner.cs:51` | `Part.SubtreeModules.Get<SolarPanel>() : Span<SolarPanel>` | `SolarPanel.cs:9`; `ModuleList.cs:178` | Yes | Same | Solar detection (presence only). `SolarPanel.cs` diff in 5402 = `OnDrawUi(IViewport …)` retype only. |
| 5 | Direct typed | `ActionScanner.cs:50` | `KeyframeAnimationModule.ShowDeployRetract : bool` (field) | `KeyframeAnimationModule.cs:82` | Yes | Same (file byte-identical) | Splits deploy/retract vs continuous actuate. |
| 6 | Direct typed | `ActionExecutor.cs:92,100-101` | `KeyframeAnimationModule.TimeGoal : float` (field) | `KeyframeAnimationModule.cs:76` | Yes | Same (OLD `:76`) | Solar/light actuation driver (set `t*Duration`). |
| 7 | Direct typed | `ActionExecutor.cs:92,100-101` | `KeyframeAnimationModule.Shared : KeyframeAnimationData` (`required` field) → `.Duration` | `KeyframeAnimationModule.cs:74` | Yes | Same | `Duration` confirmed via game use (`KeyframeAnimationModule.cs:241,256`). |
| 8 | Direct typed | `ActionScanner.cs:45`; `LightActions.cs:41,45` | `Part.LightSwitch : PowerConsumer?` (field) | `Part.cs:686` | Yes | Same (OLD `:678`) | On/off capability + execution. See zippo #6 for the 5402 `Part.IsLightSwitchedOff()` consumer refactor. |
| 9 | Direct typed | `LightActions.cs:42,45` | `PowerConsumer.LightIsActive : bool` (field) | `PowerConsumer.cs:30` | Yes | Same | Light on/off/toggle. |
| 10 | Direct typed | `LightActions.cs:51` | `Part.Modules.Get<LightModule>()` (per-light color walk) | `Part.cs:680`; `ModuleList.cs:178` | Yes | Same | Color write target enumeration. |
| 11 | Direct typed (settable field) | `LightActions.cs:66,73` | `LightModule.Template : TemplateData` (public field, **assigned**) | `LightModule.cs:62` | Yes | Same | Per-instance unshare swaps in a cloned TemplateData. Must stay a writable field. |
| 12 | Direct typed | `LightActions.cs:56,70-71` | `LightModule.TemplateData.ColorRgb : ColorRgbReference` (field) | `LightModule.cs:33` | Yes | Same | Same field zippo now reflects by name (#4). |
| 13 | **Reflection (string field/method)** | `LightActions.cs:83-86` | `ColorRgbReference.R/G/B : float` + `OnDataLoad(Mod) : void` | `ColorRgbReference.cs:10,13,16,35` | Yes | Same | Color RGB write + recompute `Value`. `OnDataLoad` 1-arg (`new object?[]{null}` ✓). Medium risk (string names). |
| 14 | Reflection (clone) | `LightActions.cs:92-104` | `RuntimeHelpers.GetUninitializedObject` + copy all instance fields of `TemplateData`/`ColorRgbReference` | `LightModule.cs:12`; `ColorRgbReference.cs` | Yes | Same | Generic field-copy clone; resilient to field set changes (copies whatever exists). |
| 15 | Direct typed | `ActionScanner.cs:25-28`; `ActionExecutor.cs:74` | `Part.InstanceId : uint`, `Part.Id`, `Part.DisplayName`, `Part.Template.Id` | `Part.cs:574,698,700`; `PartTemplate.cs` (Id) | Yes | Same (OLD `:566,690,692`) | Instance addressing + labels (see zippo #12 for the 5402 `DisplayName` initialisation change). |
| 16 | Direct typed | `ActionExecutor.cs:81-82` | `Part.FullPart : Part { get; }` | `Part.cs:1123` | Yes | Same (OLD `:1056`) | Anim-module owner resolution. |
| 17 | Direct typed | `RedAlertSubmod.cs:128,129` | `KSAColor.Xkcd.Scarlet`, `KSAColor.Xkcd.PaleGrey : Color.Preset` | `KSAColor.cs:1561,837` | Yes | Same | Engage-button styling. |
| 18 | Direct typed | `ActionScanner.cs:14`, `ActionExecutor.cs:70` (via `VehicleProvider`) | `Universe.CurrentSystem.All.UnsafeAsList().OfType<Vehicle>()` | `Universe.cs:94`; `CelestialSystem.cs:64`; `LookupCollection.cs:210` | Yes | Same | Vehicle enumeration. |
| 19 | Lifecycle/Harmony | `Patcher.cs:19,31` | `HotkeyGuard` → `GameSettings.OnKeyAll` | `GameSettings.cs:3301` | Yes | Same | Shared. |

**Game assets referenced** — None.

**Update-risk findings (4750→5018)**
- ⚠ **`KeyframeAnimationModule.TimeGoal` now fans out to mirrored parts.** 5018 added
  `ApplyToMirroredParts(TimeGoal, DeploymentState.Deployed|Retracted)` and a
  `Module.TimeGoal != State.TimeCurrent` guard on the update path. `TimeGoal` is still a plain
  settable member (no compile break), but a single red-alert write can now also move the part's
  symmetry partners. **Behavioral — needs a live pass** to confirm red-alert's per-part actuation
  still targets only what it intends.
- ✅ `LightModule` (`Template`/`TemplateData`/`ColorRgb`/`Intensity`), `ColorRgbReference`,
  `PowerConsumer.LightIsActive` and `SolarPanel` are otherwise unchanged; `Celestial.SetOrbit(Orbit)`
  and `CelestialSystem.All` are signature-identical (kiwis-marbles unaffected).
- ~~🔴 zippo's `GetField("Color")` is still wrong~~ — **CLOSED**: this was true at 5018, but
  `zippo.lib/LightController.cs:59,80` now reads `"ColorRgb"` (fixed by commit `07787ea`; see the
  5261→5348 area summary). Kept for history only.

#### Carried over from the 4680→4750 review
- No breaking deltas detected. `KeyframeAnimationModule` (`TimeGoal`/`Shared`/`ShowDeployRetract`),
  `SolarPanel`, `Part` (`Modules`/`SubtreeModules`/`LightSwitch`/`FullPart`/`InstanceId`/`Template`),
  `ModuleList.Get<T>`, `LightModule` (`Template`/`TemplateData`/`ColorRgb`), `ColorRgbReference`,
  `PowerConsumer.LightIsActive`, and `KSAColor.Xkcd` are identical across versions.
- **Electrical refactor (4681) — no impact.** `SolarPanel` gained electrical internals (`Watts Produced`,
  `PowerManager`, `Watts`/`Joules` flow), but red-alert only checks `Get<SolarPanel>().Length > 0` and actuates
  the associated `KeyframeAnimationModule.TimeGoal`; it never reads the power fields.
- **`Vehicle.IsControllable` (4699) — behavioral note, not a break.** Control is now gated by a Control Module.
  red-alert does not read `IsControllable`; its API calls (LightIsActive, TimeGoal) still execute, but on an
  uncontrollable vehicle the player may see no in-world effect. No code change required.
- **Watch (string reflection surface):** only #13 (`ColorRgbReference.{R,G,B,OnDataLoad}`) and the #14 clone
  rely on reflected names; a rename there would fail silently. Everything else is typed and compile-checked.

---

## Cross-mod summary

- **No update-driven breaks (4680→4750)** for any of the three mods. Every typed member used is identical across
  versions and the solution compiles against the 4750 DLLs.
- **Former latent bug — now FIXED (Phase 4):** zippo color get/set previously targeted a non-existent field
  `"Color"` (actual: `ColorRgb`) → was a silent no-op in both 4680 and 4750. `LightController` now uses
  `ColorRgb` and clears `IndexedColor` on write.
- **Highest ongoing runtime risk = string-based reflection** (invisible to the compiler):
  - zippo: `"KSA.LightModule+TemplateData"`, `Components`, `Intensity`/`Value`, `ColorRgb`/`R`/`G`/`B`/`OnDataLoad`, `KSAColor.Xkcd` props (plus one typed dep, `KSA.IndexedColor`, which breaks loudly at compile).
  - red-alert: `ColorRgbReference.{R,G,B,OnDataLoad}` + generic field-copy clone.
  - kiwis-marbles: **none** (fully typed) — lowest risk of the three.

---

## Area summary — Update-risk findings (5261 → 5348)

- ✅ **zippo's colour bug is CLOSED — and the earlier scope text was stale.** The `GetField("Color")`
  entry recorded as BROKEN since 4680 no longer exists: `zippo.lib/LightController.cs:59,80` reads
  `"ColorRgb"`, which is the real field (`LightModule.TemplateData.ColorRgb : ColorRgbReference`).
  There is no `GetField("Color")` anywhere in the repo. Fixed by commit `07787ea`; §4 and §6 of the
  master index have been corrected.
- ✅ **zippo / red-alert reflection all resolves.** `PartTemplate.Components` (still a public field),
  the hard-coded nested-type name `"KSA.LightModule+TemplateData"`, `TemplateData.Intensity`
  (`FloatReference`), `TemplateData.ColorRgb`, and `ColorRgbReference.{R,G,B}` (`public float`, `:10,13,16`)
  plus `OnDataLoad` — every one present and unchanged.
- ⚠️ **Lights now register for every viewport** (rev 5301, `ViewportLightModes` / clustered lighting).
  `KSA/LightModule.cs:125,141` went from `else if (viewport == Program.MainViewport)` to a bare `else`,
  so lights zippo and red-alert drive are now evaluated in crew-portrait and other secondary viewports
  too. The rest of `LightModule.cs` diffs only by decompiler cosmetics (`Parent` → `base.Parent`, from the
  rev-5329 `IPartParent` split). **Needs a live look** for double-lighting or a cost spike, not a code
  change.
- ✅ **red-alert's other targets unchanged.** `PowerConsumer.LightSwitch` (`:28`), `Part.LightSwitch`
  (`Part.cs:678`), `PowerConsumerTemplate.LightSwitch`, `SolarPanel` and
  `KeyframeAnimationModule.TimeGoal` are all present; `SolarPanel.cs` and `KeyframeAnimationModule.cs`
  diff only by `Parent` → `base.Parent` cosmetics.
- ✅ **kiwis-marbles FIXED (2026-08-23) — was silently broken at runtime, not by a symbol change.** Every
  typed member still compiled, but the mod applied welds from `[StarMapBeforeGui]`, which the 2026.8.x
  job-based celestial propagation (`CelestialUpdateTask` on `JobSystems.OrbitSolvers`, applied by
  `Universe.ApplyOrbitSolvers`) overwrites every frame. Weld work now runs from a `Priority.First` prefix on
  `Universe.ExecuteNextVehicleSolvers` (rows #2c), the engine re-parents `Children` explicitly (#2b) and
  refreshes via `UpdatePerFrameDataTree` (#2). Watchlist for future builds: the `PrepareFrame` ordering
  (Wait → Apply → ExecuteNext) and `SetOrbit` remaining a bare setter.
  `Celestial.SetOrbit(Orbit)` and `Celestial.UpdatePerFrameData` are unchanged,
  and the CCI reads are safe: rev 5280's `CelestialFrameMath` extraction preserved every
  `GetCcf2Cci`/`GetCci2Ccf`/`GetCci2Cce`/`GetCce2Cci` signature and its semantics.
  `Universe.CurrentSystem` → `CelestialSystem.All` → `LookupCollection<T>.UnsafeAsList()` is unchanged.
- ℹ️ `IOrbiter.ShowOrbit` (marque's write target) is unchanged; the only diff in `IOrbiter.cs` is the
  game switching its own overlay draws to `ImGuiHelper.GetOverlayDrawList(inViewport)` (rev 5265).

---

## Area summary — Update-risk findings (5348 → 5402)

Revisions 5349–5400 are **unlogged** (the only changelog entry in this span is rev 5401, "Fixed crash
for incorrect data stride for thumbnail rendering"), so the decomp diff is the only evidence. The
solution compiles clean against 5402. None of the three mods references the retired `Viewport` type
(`rg '\bViewport\b'` over their sources: zero hits), so the `Viewport` → `IViewport`/`IGameViewport`
replacement is not a compile break here.

- ✅ **No code change required for kiwis-marbles, zippo or red-alert.** Every typed member and every
  string-reflection target resolves to the same kind and type; `IParentBody.cs`, `GameSettings.cs`,
  `KeyframeAnimationModule.cs`, `ColorRgbReference.cs`, `FloatReference.cs`, `KSAColor.cs`,
  `LookupCollection.cs` and `Situation.cs` are byte-identical 5348↔5402.
- ⚠️ **Light on/off consumer path refactored — `Part.IsLightSwitchedOff()` (new).** `LightModule.IsActive`
  (`LightModule.cs:72`) and `PartModelModule.UpdateRenderData` (`PartModelModule.cs:106`) replaced their
  inlined `LightSwitch.LightIsActive` + `PowerConsumers.GetAllStatesByIdx(...).State.Active` checks with
  `FullPart.IsLightSwitchedOff()` (`Part.cs:1357-1369`), which returns
  `!LightIsActive || !IsSwitchedOn()` and adds a new precondition: a switch whose
  `PowerConsumer.Parent.Tree != part.Tree` is treated as **on** regardless of `LightIsActive`.
  `PowerConsumer.IsSwitchedOn()` (`PowerConsumer.cs:50-54`) bounds-checks `StatesIdx` where the old
  path indexed unconditionally. zippo (`ZippoSubmod.cs:161,467`) and red-alert (`LightActions.cs:42`)
  still write `PowerConsumer.LightIsActive`, which remains the first term — behaviour on a normal
  vehicle is unchanged. **Optional live check**: toggle a light with zippo and confirm the mesh
  emissive (`0x40` bit) and the point/spot light both follow.
- ℹ️ `LightModule.UpdateRenderData` now takes `IViewport` and gates its raytrace-light registration on
  `viewport.HasAll(ViewportOptionFlags.UseRaytracing)` instead of `viewport == Program.MainViewport`
  (`LightModule.cs:101,113,129`). Only the main viewport carries that flag (`Program.cs:948`), so this
  is a renaming of the same condition. Neither mod calls it.
- ℹ️ **kiwis-marbles frame ordering still holds.** `Program.PrepareFrame` (`Program.cs:2103-2146`) gained
  the parachute cloth solvers: `ClothSolvers.Wait()` → `ApplyOrbitSolvers` → `ApplyVehicleSolvers` →
  `ApplyClothSolvers` … `ExecuteNextClothSolvers` → **`ExecuteNextVehicleSolvers`** (our prefix, `:2145`)
  → `ExecuteNextOrbitSolvers`. `Universe.ExecuteNextVehicleSolvers` (`:1834`) is byte-identical to 5348
  and the weld window (after the Apply* calls, before `ExecuteNextOrbitSolvers`) is intact.
  `ExecuteNextClothSolvers` (`Universe.cs:1822`, `ChuteClothSystem.SnapshotAndKick`) runs just before
  the prefix, so a deployed parachute's cloth sees the *pre-weld* body state for that frame. Only
  relevant to a chute flying near a welded body — **live check only if that scenario matters**.
  `Celestial.SetOrbit` (`:153`) is still a bare `Orbit = newOrbit;`; `Orbit.CreateFromStateCci`
  (`Orbit.cs:1563`, `UniverseTime`) and `Universe.GetElapsedTime()` (`:2114`) are unchanged.
- ℹ️ **`Part.DisplayName` initialisation changed (cosmetic).** `Part.cs:1391` now sets
  `DisplayName = Template.DisplayName != Template.Id ? Template.DisplayName : Id` (was `= Id`). zippo's
  and red-alert's combo labels use `DisplayName ?? Id`, so some entries may read differently; all keys
  still use `Id`/`InstanceId`. No code change.
- ℹ️ `PartTemplate` gained `CrashTolerance` (`:17-18`) and `SubPartGroups` (`:107-108`), shifting the
  reflected `Components` field to `:113`; still a public `List<ModuleBase.TemplateDataBase>` field.
- ℹ️ Content/asset diffs (`RayIntersections.glsl`, `ModelPbr.frag`, `ModelNormal.frag`, new
  `StaticObjectNormalIndirect.frag`, `ParachuteAssets.xml`, `DefaultAssets.xml`) touch nothing these
  mods reference (they reference no assets).
- 🔁 **Carried forward, pre-existing:** red-alert's `KeyframeAnimationModule.TimeGoal` mirrored-part
  fan-out (5018) still **needs a live pass**; the file is byte-identical this span. zippo's colour bug
  remains **closed** (`LightController.cs:59,80` → `"ColorRgb"`).
- **Verified clean this span** (NEW line numbers refreshed in the tables above): kiwis-marbles rows
  1–13; zippo rows 1–15; red-alert rows 1–19.
- **Needs a live pass**: the `IsLightSwitchedOff()` light toggle (optional), red-alert `TimeGoal` mirror
  fan-out (carried), kiwis-marbles weld near a deployed parachute (only if relevant).
