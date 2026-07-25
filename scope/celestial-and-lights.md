# Scope: Celestial Welding & Lights/Actions (kiwis-marbles, zippo, red-alert)

Permanent reference cataloging how three unscience mods integrate with the KSA game,
for detecting when a game update breaks them.

**Versions compared**
- NEW = `2026.6.9.4750` — decomp root `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- OLD = `2026.6.8.4680` — decomp root `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`
- Decomp paths below are relative to `<root>\KSA\` unless noted. Verified by Grep/Read of both trees.

**Shared integration (all three mods)** — each mod's `Patcher.cs` calls
`HotkeyGuard.Patch/Unpatch` (`ksa-abstractions.lib/HotkeyGuard.cs`). HotkeyGuard Harmony-patches
`GameSettings.OnKeyAll(GlfwKeyEvent) : bool` (NEW `GameSettings.cs:2379`, prefix with `ref bool __result`).
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

**Unscience integration** — Standalone StarMap mod hosting `KiwisMarblesSubmod : ISubmod`. Per-frame
`Update(dt)` (driven from `[StarMapBeforeGui]`) calls `CelestialWeldEngine.UpdateWeld` for each weld.
Discovers bodies through `CelestialProvider` (abstractions). Stateless math in `CelestialWeldEngine`.

**UI/hotkeys** — **F9** toggles the window (`Mod.cs:51`). ImGui (Brutal.ImGuiApi): filterable Source/Target
combos, `DragFloat3` offset + unit combo (m/km/Mm/Gm), per-weld live offset editor with a surface/lat-lon mode,
red "Unweld" button. Renders inside the Unscience toolbox via `ISubmod.RenderContent`.

**Persistence** — None. `CelestialWeldEntry.OriginalOrbit` is captured in memory at weld time to restore the
body on unweld; welds are lost on reload (README §Notes).

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed | `CelestialWeldEngine.cs:39`, `KiwisMarblesSubmod.cs:437` | `Celestial.SetOrbit(Orbit newOrbit)` | `Celestial.cs:139` | Yes | Same (OLD `:132`) | Core re-parent + reposition. Auto-reparents internally. |
| 2 | Direct typed | `CelestialWeldEngine.cs:40`, `KiwisMarblesSubmod.cs:438` | `Celestial.UpdatePerFrameData() : void` (override) | `Celestial.cs:509` | Yes | Same (OLD `:439`) | Refreshes cached CCI/CCE transforms after SetOrbit. |
| 3 | Direct typed | `CelestialWeldEngine.cs:31` | `Orbit.CreateFromStateCci(IParentBody, SimTime, double3, double3, byte4) : Orbit` (static) | `Orbit.cs:1396` | Yes | **Identical sig** (OLD `:1379`) | 5-arg state-vector → orbit. Arg types must stay (IParentBody/SimTime/double3/double3/byte4). |
| 4 | Direct typed | `CelestialWeldEngine.cs:36` | `Celestial.OrbitColor : byte4 { get; protected set; }` (via IOrbiter) | `Celestial.cs:63`; `IOrbiter.cs:24` | Yes | Same (OLD `:58`) | Passed as orbit line color to #3. |
| 5 | Direct typed | `CelestialWeldEngine.cs:21,26` | `IOrbiter.Parent : IParentBody { get; }` (= `Orbit.Parent`) | `IOrbiter.cs:18` | Yes | Same | Null-checked before weld. |
| 6 | Direct typed | `CelestialWeldEngine.cs:21`; `KiwisMarblesSubmod.cs:422` | `IOrbiter.Orbit : Orbit { get; }` / `Celestial.Orbit { get; set; }` | `IOrbiter.cs:16`; `Celestial.cs:57` | Yes | Same (OLD `:52`) | Source `.Orbit` saved for restore. |
| 7 | Direct typed | `CelestialWeldEngine.cs:24` | `IOrbiter.GetPositionCci() : double3` | `IOrbiter.cs:52` | Yes | Same | Target CCI position each frame. |
| 8 | Direct typed | `CelestialWeldEngine.cs:25` | `IOrbiter.GetVelocityCci() : double3` | `IOrbiter.cs:66` | Yes | Same | Target CCI velocity each frame. |
| 9 | Direct typed | `KiwisMarblesSubmod.cs:146,321,334` | `Celestial.MeanRadius : double` (override) | `Celestial.cs:77` | Yes | Same (OLD `:72`) | Surface-placement helper only. |
| 10 | Direct typed | `KiwisMarblesSubmod.cs:66,67` (via `CelestialProvider`) | `Universe.CurrentSystem : CelestialSystem? { get; }` → `.All : LookupCollection<Astronomical>` → `.UnsafeAsList()` | `Universe.cs:92`; `CelestialSystem.cs:57`; `LookupCollection.cs:210` | Yes | Same | Source list `OfType<Celestial>()`, target list `OfType<IOrbiter>()`. |
| 11 | Direct typed | `CelestialWeldEngine.cs:34` (via `SimTimeProvider`) | `Universe.GetElapsedSimTime() : SimTime` (static) | `Universe.cs` (used `:1790`) | Yes | Same (OLD used `:1239`) | State time for #3. |
| 12 | Cast/type | `CelestialWeldEngine.cs:66`; `KiwisMarblesSubmod.cs:146,209` | `(IOrbiter)Celestial` cast; `IParentBody` as parent type | `IOrbiter.cs`, `IParentBody.cs` | Yes | Same | Celestial implements IOrbiter (topo-sort edge test). |
| 13 | Lifecycle/Harmony | `Patcher.cs:19,31` | `HotkeyGuard` → `GameSettings.OnKeyAll(GlfwKeyEvent) : bool` | `GameSettings.cs:2379` | Yes | Same | Shared guard; PatchAll defines no own patches. |

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
| 2 | **Reflection (string field)** | `LightController.cs:33` | `PartTemplate.Components : List<ModuleBase.TemplateDataBase>` (field) | `PartTemplate.cs:91` | Yes | Same | Field name `"Components"` must persist. |
| 3 | **Reflection (string field)** | `LightController.cs:50,71` | `LightModule.TemplateData.Intensity : FloatReference` (field) → `FloatReference.Value : float` | `LightModule.cs:30`; `FloatReference.cs:9` | Yes | Same | Intensity read/write **works**. Field names `"Intensity"`/`"Value"` must persist. |
| 4 | Reflection (string field) — **FIXED (Phase 4)** | `LightController.cs:59,80` | reads/writes field `"ColorRgb"` on `TemplateData` | `LightModule.cs:33` (`ColorRgbReference ColorRgb`) | Yes | Same | Was `"Color"` (the `[XmlElement("Color")]` XML name, not the C# field) ⇒ `GetField`→null ⇒ color was a silent no-op in both 4680 and 4750. Now `"ColorRgb"`; the C# field name must persist. |
| 5 | Reflection (string field/method) + **typed enum** | `LightController.cs:61-63,82-89` | `ColorRgbReference.R/G/B : float` + `OnDataLoad(Mod) : void`; write side clears `IndexedColor` to `KSA.IndexedColor.Invalid` | `ColorRgbReference.cs:10,13,16,19,35` | Yes | Same | Now reachable (post-#4). `OnDataLoad` re-derives R/G/B from `IndexedColor` unless it is `Invalid`, so `WriteColor` sets `IndexedColor = KSA.IndexedColor.Invalid` (typed — **compile-checked**, breaks loudly) before `OnDataLoad(null)`. |
| 6 | Direct typed | `ZippoSubmod.cs:152,441,465` | `Part.LightSwitch : PowerConsumer?` (field) | `Part.cs:407` | Yes | Same (OLD `:406`) | On/off path. |
| 7 | Direct typed | `ZippoSubmod.cs:152,441,465,567` | `Part.FullPart : Part { get; }` | `Part.cs:659` | Yes | Same (OLD `:658`) | `.FullPart.LightSwitch` fallback. |
| 8 | Direct typed | `ZippoSubmod.cs:161,442,467,568` | `PowerConsumer.LightIsActive : bool` (field) | `PowerConsumer.cs:28` | Yes | Same | On/off toggle. Electrical refactor (4681) didn't touch this field. |
| 9 | Direct typed | `LightController.cs:92,95,99` | `Part.Template : PartTemplate` (field) | `Part.cs:323` | Yes | Same (OLD `:322`) | Feeds reflection in #1–#5. |
| 10 | Direct typed | `ZippoSubmod.cs:406,558`; `LightController.cs:98` (via `PartHelpers`) | `Vehicle.Parts : PartTree` → `PartTree.Parts : ReadOnlySpan<Part>` | `Vehicle.cs:264`; `PartTree.cs:67` | Yes | Same | Part enumeration root. |
| 11 | Direct typed | `LightController.cs:130-131` (recursion); `PartHelpers.cs` | `Part.SubParts : ReadOnlySpan<Part>` | `Part.cs:655` | Yes | Same | Recursive light search. |
| 12 | Direct typed | `ZippoSubmod.cs` (combo labels) | `Part.Id : string { get; init; }`, `Part.DisplayName : string { get; init; }` | `Part.cs:411,413` | Yes | Same (OLD `:410,412`) | Display/keys. |
| 13 | Reflection (palette) | `ZippoSubmod.cs:253,284` (via `XkcdColorHelper.GetAll`) | `KSAColor.Xkcd` static props → `Color.Preset` | `KSAColor.cs:23` | Yes | Same | Reflects all `Xkcd` static color props; cast `(Color.Preset)`. Rename of `Xkcd`/prop-type change would empty the combo. |
| 14 | Direct typed | `LightController.cs:20-27` | hard-coded preset float3 (Marine/HotPink/RadioactiveGreen/BabyPurple) | n/a (constants from `KSAColor.cs`) | n/a | n/a | Hard-coded RGB; cosmetic only, no runtime dependency. |
| 15 | Lifecycle/Harmony | `Patcher.cs:19,31` | `HotkeyGuard` → `GameSettings.OnKeyAll` | `GameSettings.cs:2379` | Yes | Same | Shared. |

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
| 1 | Direct typed | `ActionScanner.cs:17`; `ActionExecutor.cs:73` | `Vehicle.Parts : PartTree` → `PartTree.Parts : ReadOnlySpan<Part>` | `Vehicle.cs:264`; `PartTree.cs:67` | Yes | Same | Top-level part scan + resolve. |
| 2 | Direct typed | `ActionScanner.cs:68` | `Part.Modules : ModuleList` → `ModuleList.Get<LightModule>() : Span<LightModule>` | `Part.cs:401`; `ModuleList.cs:112` | Yes | Same (OLD `:400`) | Light detection. `LightModule : Module<LightModule>, IDisposable` satisfies `Get<T>` constraint. |
| 3 | Direct typed | `ActionScanner.cs:50`; `ActionExecutor.cs:82` | `Part.SubtreeModules : ModuleList` → `.Get<KeyframeAnimationModule>() : Span<...>` | `Part.cs:409`; `ModuleList.cs:112` | Yes | Same (OLD `:408`) | Anim detection + actuation handle. |
| 4 | Direct typed | `ActionScanner.cs:51` | `Part.SubtreeModules.Get<SolarPanel>() : Span<SolarPanel>` | `SolarPanel.cs:8`; `ModuleList.cs:112` | Yes | Same | Solar detection (presence only). |
| 5 | Direct typed | `ActionScanner.cs:51` | `KeyframeAnimationModule.ShowDeployRetract : bool` (field) | `KeyframeAnimationModule.cs:82` | Yes | Same | Splits deploy/retract vs continuous actuate. |
| 6 | Direct typed | `ActionExecutor.cs:92,101` | `KeyframeAnimationModule.TimeGoal : float` (field) | `KeyframeAnimationModule.cs:76` | Yes | Same (OLD `:76`) | Solar/light actuation driver (set `t*Duration`). |
| 7 | Direct typed | `ActionExecutor.cs:92,101` | `KeyframeAnimationModule.Shared : KeyframeAnimationData` (field) → `.Duration` | `KeyframeAnimationModule.cs:74` | Yes | Same | `Duration` confirmed via game use (`KeyframeAnimationModule.cs:241,256`). |
| 8 | Direct typed | `ActionScanner.cs:45`; `LightActions.cs:41,45` | `Part.LightSwitch : PowerConsumer?` (field) | `Part.cs:407` | Yes | Same (OLD `:406`) | On/off capability + execution. |
| 9 | Direct typed | `LightActions.cs:42,45` | `PowerConsumer.LightIsActive : bool` (field) | `PowerConsumer.cs:28` | Yes | Same | Light on/off/toggle. |
| 10 | Direct typed | `LightActions.cs:51` | `Part.Modules.Get<LightModule>()` (per-light color walk) | `Part.cs:401`; `ModuleList.cs:112` | Yes | Same | Color write target enumeration. |
| 11 | Direct typed (settable field) | `LightActions.cs:64,72,73` | `LightModule.Template : TemplateData` (public field, **assigned**) | `LightModule.cs:59` | Yes | Same | Per-instance unshare swaps in a cloned TemplateData. Must stay a writable field. |
| 12 | Direct typed | `LightActions.cs:58,71` | `LightModule.TemplateData.ColorRgb : ColorRgbReference` (field) | `LightModule.cs:33` | Yes | Same | **Correct** field (contrast zippo #4). |
| 13 | **Reflection (string field/method)** | `LightActions.cs:83-86` | `ColorRgbReference.R/G/B : float` + `OnDataLoad(Mod) : void` | `ColorRgbReference.cs:10,13,16,35` | Yes | Same | Color RGB write + recompute `Value`. `OnDataLoad` 1-arg (`new object?[]{null}` ✓). Medium risk (string names). |
| 14 | Reflection (clone) | `LightActions.cs:92-104` | `RuntimeHelpers.GetUninitializedObject` + copy all instance fields of `TemplateData`/`ColorRgbReference` | `LightModule.cs:12`; `ColorRgbReference.cs` | Yes | Same | Generic field-copy clone; resilient to field set changes (copies whatever exists). |
| 15 | Direct typed | `ActionScanner.cs:27,30`; `ActionExecutor.cs:74` | `Part.InstanceId : uint`, `Part.Id`, `Part.DisplayName`, `Part.Template.Id` | `Part.cs:321,411,413`; `PartTemplate.cs` (Id) | Yes | Same | Instance addressing + labels. |
| 16 | Direct typed | `ActionExecutor.cs:80,82` | `Part.FullPart : Part { get; }` | `Part.cs:659` | Yes | Same | Anim-module owner resolution. |
| 17 | Direct typed | `RedAlertSubmod.cs:128,129` | `KSAColor.Xkcd.Scarlet`, `KSAColor.Xkcd.PaleGrey : Color.Preset` | `KSAColor.cs:1561,837` | Yes | Same | Engage-button styling. |
| 18 | Direct typed | `ActionScanner.cs:14`, `ActionExecutor.cs:70` (via `VehicleProvider`) | `Universe.CurrentSystem.All.UnsafeAsList().OfType<Vehicle>()` | `Universe.cs:92`; `CelestialSystem.cs:57`; `LookupCollection.cs:210` | Yes | Same | Vehicle enumeration. |
| 19 | Lifecycle/Harmony | `Patcher.cs` | `HotkeyGuard` → `GameSettings.OnKeyAll` | `GameSettings.cs:2379` | Yes | Same | Shared. |

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
- 🔴 **zippo's `GetField("Color")` is still wrong** — re-confirmed against 5018: the field is
  `ColorRgb`. Pre-existing silent no-op, **not** a 5018 regression.

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
