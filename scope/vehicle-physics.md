# Vehicle Manipulation / Physics Mods — Game Integration Scope

Permanent reference for detecting when KSA game updates break the vehicle-manipulation /
physics mods (`eternal-flame`, `garrys-torch`, `i-feel-seen`). Every game-facing member
these mods touch is enumerated and verified against decompiled sources.

**Verified game versions**

- NEW decomp `2026.6.9.4750` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- OLD decomp `2026.6.8.4680` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`

Paths in the **Decomp path (NEW)** column are relative to the NEW decomp root
(namespace-foldered, e.g. `KSA/Vehicle.cs`). **Mod code** paths are relative to the repo
root `C:\Users\Alex\repos\meow-sci\unscience`.

**How these mods are hosted (all three)**

- Logic + game access live in the `*.lib` project; each `.lib` exposes an `ISubmod`
  (`MeowSci.KsaAbstractions.ISubmod`) consumed two ways:
  1. **Standalone** StarMap mod (`<mod>/Mod.cs`) — own ImGui window, F11 toggle, own
     `Patcher`.
  2. **Embedded** in the **unscience** supermod (`unscience/Mod.cs` `OnFullyLoaded`,
     submods created at `unscience/Mod.cs:60-85`) as collapsible sections, with a single
     shared `Harmony("MeowSci.Unscience")` instance (`unscience/Patcher.cs`).
- Vehicle enumeration is funneled through `ksa-abstractions.lib/VehicleProvider.cs`
  (`GetAllVehicles`/`GetControlledVehicle`), so that helper's game touchpoints are part of
  each mod's effective surface and are listed per mod.
- Every top-level mod also applies the shared `HotkeyGuard` (`ksa-abstractions.lib/HotkeyGuard.cs`,
  patches `GameSettings.OnKeyAll`) — catalogued in `scope/telemetry.md` and not repeated in
  full here; listed as one row per mod.

**Summary of 4680 -> 4750 risk**

- **eternal-flame** — NO breaking deltas. Every member it touches is signature-identical
  OLD->NEW. The rev 4681 electrical refactor does **not** reach it: it refills batteries by
  calling `Battery.Refill(ref BatteryState)` (which the game refactored internally but kept
  signature-stable), never naming `Joules`/`JoulesReference`/`EnergyReference`/`Charge`.
- **garrys-torch** — **1 confirmed compile break** (CS8604, rev 4729 Brutal nullability) at
  `garrys-torch.lib/GarrysTorchSubmod.cs:457`. All ~25 typed/reflected game touchpoints are
  signature-identical OLD->NEW. One behavioral watch item from rev 4699 (`Vehicle.IsControllable`).
- **i-feel-seen** — NO breaking deltas. Both string-resolved Harmony targets
  (`Vehicle.GetWorldMatrix`, `Vehicle.UpdateRenderData`) and every prefix-body member are
  signature-identical OLD->NEW.

---

## eternal-flame (`eternal-flame` / `eternal-flame.lib`)

**Purpose** — Infinite fuel + electricity. Keeps selected vehicles topped up: periodically
calls `Vehicle.RefillConsumables()` (fuel/resource tanks) and refills every `Battery`
module to `MaximumCapacity`. Battery refills are driven from a Harmony **prefix** on
`Universe.ExecuteNextVehicleSolvers` so the new charge is copied into the next electrical
simulation step; fuel refills run on the normal UI update tick.

**Unscience integration** — `EternalFlameSubmod : ISubmod`
(`eternal-flame.lib/EternalFlameSubmod.cs:10`), holding a `FuelManager`
(`eternal-flame.lib/EternalFlameLib.cs:25`). `Update(dt)` -> `FuelManager.Update` (fuel);
`UpdateBeforeVehicleSolvers()` -> `FuelManager.UpdateElectricityBeforeVehicleSolvers`
(batteries). Standalone host `eternal-flame/Mod.cs:27` (`new EternalFlameSubmod()`), with the
solver prefix wired in `eternal-flame/Patcher.cs:43-69` (`EternalFlameSolverPatch`). Embedded
host: `unscience/Mod.cs:69` (`new EternalFlameSubmod()`); the supermod re-declares the
identical solver prefix as `EternalFlamePatches` in `unscience/Patcher.cs:92-126`
(applied at `unscience/Patcher.cs:42`). `EternalFlameSubmod.Instance` (static) is the bridge
the prefix calls into.

**UI/hotkeys** — Standalone window "Eternal Flame - Infinite Fuel", 500x450, toggled by
**F11** (`eternal-flame/Mod.cs:58,91`). Content (`EternalFlameSubmod.RenderContent`,
`eternal-flame.lib/EternalFlameSubmod.cs:34`): filterable vehicle combo + Add, monitored-vehicle
table with per-row Fuel/Elec checkboxes and remove, refill-interval `DragInt` (0–5000 ms).
All ImGui via `Brutal.ImGuiApi`.

**Persistence** — None. Monitored list, interval, and toggles are in-memory
(`FuelManager._monitored`, `RefillIntervalMs`) and reset on reload. No disk I/O, no save hooks.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Harmony (prefix) | `eternal-flame/Patcher.cs:47,55` (standalone) and `unscience/Patcher.cs:96,104` (supermod) | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep)` — `public static void`; resolved `AccessTools.Method(typeof(Universe), nameof(...))` (no param array), prefix is param-less `void` (priority First) | `KSA/Universe.cs:1660` | Yes | Same (OLD `Universe.cs:1109`) | Single overload, so no-arg resolution is unambiguous. Prefix returns void -> original always runs. Highest-value chokepoint for this mod. |
| 2 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:80` | `Vehicle.RefillConsumables()` — `public void` | `KSA/Vehicle.cs:2300` | Yes | Same (OLD `Vehicle.cs:2210`) | Internally calls `Parts.RefillConsumables()` + `RecomputeMassProperties` + `FlightComputer.ReadUpdatedVehicleConfiguration` (all internal; not touched directly). |
| 3 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:128` | `Vehicle.Parts` — `public PartTree Parts` (field) | `KSA/Vehicle.cs:264` | Yes | Same (OLD `Vehicle.cs:233`) | Entry to battery state list. |
| 4 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:128` | `PartTree.Batteries` — `public ModuleStateful<Battery,BatteryState,EmptyStruct,EmptyStruct>.StateList Batteries` (field) | `KSA/PartTree.cs:37` | Yes | Same (OLD `PartTree.cs:37`) | Generic `StateList`. |
| 5 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:129` | `StateList.NumModules` — `public int NumModules` | `KSA/ModuleStateful.cs:251` | Yes | Same (OLD `ModuleStateful.cs:250`) | Early-out when 0. |
| 6 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:132` | `StateList.Modules` — `public Span<TModule> Modules` | `KSA/ModuleStateful.cs:243` | Yes | Same (OLD `ModuleStateful.cs:242`) | Iterates `Battery[]`. |
| 7 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:136` | `StateList.GetModuleAndAllMutableStatesForInitialization(TModule)` — returns `ModuleAndAllMutableStatesRef` | `KSA/ModuleStateful.cs:508` | Yes | Same (OLD `ModuleStateful.cs:493`) | Returns ref struct with `.Module` + `.State`. |
| 8 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:137` | `ModuleAndAllMutableStatesRef.Module` / `.State` (Battery / BatteryState) | `KSA/ModuleStateful.cs:516-` | Yes | Same | Game uses the same `.Module.Refill(ref ...State)` shape at `KSA/ResourceManager.cs:423`. |
| 9 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:137` | `Battery.Refill(ref BatteryState state)` — `public void` (sets `state.Charge = MaximumCapacity`) | `KSA/Battery.cs:59` | Yes | Same (OLD `Battery.cs:59`) | **Insulates the mod from rev 4681.** Body unchanged OLD->NEW. |
| 10 | Direct typed API (indirect) | via #9 | `Battery.MaximumCapacity` — `public required Joules MaximumCapacity` | `KSA/Battery.cs:21` | Yes | Same (OLD `Battery.cs:21`) | Read only inside `Refill`; mod never names `Joules`. |
| 11 | Direct typed API | `eternal-flame.lib/EternalFlameLib.cs:74,111` (lookup) | `Vehicle.Id` — `public virtual string Id` (inherited `Astronomical.Id`) | `KSA/Astronomical.cs:85` | Yes | Same (OLD `Astronomical.cs:85`) | Monitored-vehicle key matching. |
| 12 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs:14` (called `EternalFlameLib.cs:65,102`; `EternalFlameSubmod.cs:54,109`) | `Universe.CurrentSystem` (`KSA/Universe.cs:92`) -> `CelestialSystem.All` (`KSA/CelestialSystem.cs:57`) -> `LookupCollection<Astronomical>.UnsafeAsList()` (`KSA/LookupCollection.cs:210`) | `KSA/Universe.cs:92` | Yes | Same | Shared enumerator; a break here cascades to all three mods' UI. |
| 13 | Harmony + Reflection | `eternal-flame/Patcher.cs:20` -> `HotkeyGuard.cs:21` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool`; `nameof`-resolved, prefix `ref bool __result` | `KSA/GameSettings.cs:2379` | Yes | Same | Shared guard (full row in `scope/telemetry.md`). |
| 14 | Lifecycle | `eternal-flame/Mod.cs:19-87` | StarMap attrs: `StarMapMod`, `StarMapImmediateLoad`, `StarMapAllModsLoaded`, `StarMapBeforeGui`, `StarMapAfterGui`, `StarMapUnload` (StarMap.API) | (StarMap.API package) | Yes | Same | Fuel in `OnBeforeUi`; battery via the solver prefix. |

**Game assets referenced** — None.

**Update-risk findings (4680 -> 4750)**

- **No breaking deltas.** All 11 typed members + the patched `ExecuteNextVehicleSolvers` are
  signature-identical OLD->NEW (line shifts only).
- **rev 4681 electrical refactor — confirmed NOT impacting this mod.** The refactor renamed the
  *serialization* type `JoulesReference` -> `EnergyReference` and changed `Battery.SaveData`
  (`KSA/Battery.cs:12-13`, `110`, `120` differ OLD vs NEW) and `Battery.DrawStateInfo`
  (`JoulesReference.ToNearest` -> `EnergyReference.ToNearestElectrical`, `Battery.cs:94-95`).
  The mod touches **none** of those — it calls `Battery.Refill(ref BatteryState)`, whose body
  (`state.Charge = MaximumCapacity`) is byte-identical OLD->NEW, and `MaximumCapacity` is still
  `Joules`. If a future build changes `Battery.Refill`'s signature or removes it, this mod's
  electricity path breaks; watch that method specifically.
- `RefillConsumables` internals changed shape across builds but its public no-arg signature is
  stable; the mod only calls the public method.

---

## garrys-torch (`garrys-torch` / `garrys-torch.lib`)

**Purpose** — Vehicle-to-vehicle welding. Every frame it teleports a *source* vehicle to a
pose relative to a *target* vehicle (optionally anchored to a specific target `Part`), with
position/rotation offset, uniform part scaling (special-cased for `KittenEva` avatars), and
optional rotation lock. Also supports eased animation of weld params.

**Unscience integration** — `GarrysTorchSubmod : ISubmod`
(`garrys-torch.lib/GarrysTorchSubmod.cs:12`); stateless math in `WeldEngine`
(`garrys-torch.lib/WeldEngine.cs:11`); per-weld state `WeldEntry`
(`garrys-torch.lib/WeldEntry.cs:7`). Weld physics runs from **`OnAfterUi`** (not a Harmony
patch): `GarrysTorchSubmod.UpdateWelds(dt)` (`GarrysTorchSubmod.cs:85`) first calls
`KSA.JobSystems.VehicleSolvers.Wait()` to drain in-flight vehicle workers, then
`WeldEngine.UpdateWeld` per weld. Standalone host `garrys-torch/Mod.cs:27,59`; embedded host
`unscience/Mod.cs:71` (submod) + `unscience/Mod.cs:173` (`GarrysTorchSubmod.Instance?.UpdateWelds(dt)`).
`garrys-torch/Patcher.cs` applies **only** `HotkeyGuard` — no game-targeting Harmony patch
(earlier prefix/postfix approaches on `ExecuteNextVehicleSolvers`/`ApplyVehicleSolvers` were
abandoned; see `garrys-torch/README.md:32-43`). Public API (`CreateWeld`/`ModifyWeld`/
`RemoveWeld`/`AnimateWeld`/preset methods) is consumed by `unladen-swallow.lib` HTTP RPC.

**UI/hotkeys** — Standalone window "Garry's Torch", 450x500, toggled by **F11**
(`garrys-torch/Mod.cs:51,85`). Content (`GarrysTorchSubmod.RenderContent:105`): Create-Weld
header (filterable source / target / target-part / preset combos), position/rotation
`DragFloat3`, scale `DragFloat`, lock-rotation checkbox, active-weld child panels with
per-weld edit + Save-as-preset / Unweld, and delete/save modals.

**Persistence** — Named **presets** only (not active welds). `PresetManager`
(`garrys-torch.lib/PresetManager.cs`) reads/writes TOML at
`<MyDocuments>/My Games/Kitten Space Agency/.unscience/garrys-torch-presets.toml`
(`PresetManager.cs:23-24`, dir from `ksa-abstractions.lib/KsaPaths.cs:9` via
`Environment.SpecialFolder.MyDocuments`). Active welds are in-memory (`_welds`) and lost on
reload. TOML via `Tomlyn`.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed API | `garrys-torch.lib/GarrysTorchSubmod.cs:93` | `KSA.JobSystems.VehicleSolvers` (`public static JobScheduler VehicleSolvers`) -> `JobScheduler.Wait()` | `KSA/JobSystems.cs:11` | Yes | Same (OLD `JobSystems.cs:11`) | Game itself calls `JobSystems.VehicleSolvers.Wait()` (`KSA/Program.cs:1942`, `Universe.cs:2030`). Core race-avoidance for the whole mod. |
| 2 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:19,75` | `Vehicle.Parent` — `public IParentBody Parent => Orbit.Parent` | `KSA/Vehicle.cs:332` | Yes | Same (OLD `Vehicle.cs:299`) | Reference-compared for parent-body match; `.GetCci2Cce()` called on it (#10). |
| 3 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:28` | `Vehicle.GetPositionCci()` — `public double3` | `KSA/Vehicle.cs:1949` | Yes | Same (OLD `Vehicle.cs:1859`) | Target world position. |
| 4 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:29` | `Vehicle.GetVelocityCci()` — `public double3` | `KSA/Vehicle.cs:1897` | Yes | Same (OLD `Vehicle.cs:1807`) | Source velocity = target velocity. |
| 5 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:30,90` | `Vehicle.GetBody2Cci()` — `public doubleQuat` | `KSA/Vehicle.cs:2242` | Yes | Same (OLD `Vehicle.cs:2152`) | Orientation transforms. |
| 6 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:58` | `Vehicle.CenterOfMassAsmb` — `public double3 CenterOfMassAsmb` | `KSA/Vehicle.cs:510` | Yes | Same (OLD `Vehicle.cs:477`) | Part-anchor offset base. |
| 7 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:85,92` | `Vehicle.BodyRates` — `public double3 BodyRates` | `KSA/Vehicle.cs:458` | Yes | Same (OLD `Vehicle.cs:425`) | Passed to `Teleport`; NaN-guarded by mod. |
| 8 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:126` | `Vehicle.Orbit` — `public Orbit Orbit => Patch.Orbit` (reads `.OrbitLineColor`) | `KSA/Vehicle.cs:330` | Yes | Same (OLD `Vehicle.cs:297`) | Source orbit's line color reused for new orbit. |
| 9 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:129` | `Vehicle.Teleport(Orbit? orbit, doubleQuat? body2Cce, double3? bodyRates)` — `public void` | `KSA/Vehicle.cs:1594` | Yes | Same (OLD `Vehicle.cs:1558`) | The core mutation. Nullable params; mod passes non-null. rev 4708 (orbit-time printout) was an internal fix; **signature unchanged**. |
| 10 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:130` | `Vehicle.UpdatePerFrameData()` — `public override void` | `KSA/Vehicle.cs:1972` | Yes | Same (OLD `Vehicle.cs:1882`) | Refresh caches post-teleport. |
| 11 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:75` | `IParentBody.GetCci2Cce()` — `doubleQuat` (interface) | `KSA/IParentBody.cs:47` | Yes | Same (OLD `IParentBody.cs:47`) | Called on `Vehicle.Parent`. |
| 12 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:58` | `Part.PositionVehicleAsmb` — `public double3` (computed property) | `KSA/Part.cs:415` | Yes | Same (OLD `Part.cs:414`) | Part-anchor position. |
| 13 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:61` | `Part.Asmb2VehicleAsmb` — `public doubleQuat` (computed property) | `KSA/Part.cs:431` | Yes | Same (OLD `Part.cs:430`) | Part-anchor orientation. |
| 14 | Direct typed API (write) | `garrys-torch.lib/WeldEngine.cs:200` | `Part.Scale` — `public double3 Scale { get; set; }` (setter calls `ResetCachedPosMatrixValues`) | `KSA/Part.cs:499` | Yes | Same (OLD `Part.cs:498`) | Recursive uniform scale write. |
| 15 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:157,201` | `Part.SubParts` — `public ReadOnlySpan<Part> SubParts`; `PartTree.Parts` — `public ReadOnlySpan<Part> Parts` | `KSA/Part.cs:655`; `KSA/PartTree.cs:67` | Yes | Same (OLD `Part.cs:654`; `PartTree.cs:65`) | Part-tree walk for scaling + target-part list. |
| 16 | Direct typed API | `garrys-torch.lib/GarrysTorchSubmod.cs:188` | `Part.Template` (`public PartTemplate Template`) -> `PartTemplate.Id` (`public string Id`, inherited `SerializedId.Id`); `Part.Id` (`public string Id { get; init; }`) | `KSA/Part.cs:323`,`411`; `KSA/SerializedId.cs:13` | Yes | Same (OLD `Part.cs:322`,`410`) | Target-part combo labels. |
| 17 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:119` | `Universe.GetJobSimStep(double dtPlayer)` -> `SimStep.NextTime` (`SimTime`) | `KSA/Universe.cs:2188` | Yes | Same (OLD `Universe.cs:1637`) | Tick-end time for the new orbit's state time (avoids SnapToLeader mismatch). |
| 18 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:119` | `Program.GetPlayerDeltaTime()` — `public static double` | `KSA/Program.cs:4467` | Yes | Same (OLD `Program.cs:4407`) | Fed into `GetJobSimStep`. |
| 19 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:121` | `Orbit.CreateFromStateCci(IParentBody parent, SimTime stateTime, double3 positionCci, double3 velocityCci, byte4 orbitLineColor)` — `public static Orbit` | `KSA/Orbit.cs:1396` | Yes | Same (OLD `Orbit.cs:1379`) | 5-arg factory; arg order/types unchanged. |
| 20 | Direct typed API | `garrys-torch.lib/WeldEngine.cs:126` | `Orbit.OrbitLineColor` — `public byte4 OrbitLineColor` (field) | `KSA/Orbit.cs:1062` | Yes | Same (OLD `Orbit.cs:1045`) | — |
| 21 | Reflection (type-name) | `garrys-torch.lib/WeldEngine.cs:161` | `vehicle.GetType().Name == "KittenEva"` — `public class KittenEva : Vehicle` | `KSA/KittenEva.cs:7` | Yes | Same (OLD `KittenEva.cs:7`) | String type-name compare; breaks silently if the class is renamed. |
| 22 | Reflection (private field, string) | `garrys-torch.lib/WeldEngine.cs:165` (via `ReflectionHelpers.GetFieldValue`) | `KittenEva._renderable` — `private KittenRenderable _renderable` | `KSA/KittenEva.cs:9` | Yes | Same (OLD `KittenEva.cs:9`) | **String field name, not compile-checked.** |
| 23 | Reflection (private field, string) | `garrys-torch.lib/WeldEngine.cs:168` | `KittenRenderable._characterAvatar` — `private CharacterAvatar _characterAvatar` | `KSA/KittenRenderable.cs:10` | Yes | Same (OLD `KittenRenderable.cs:10`) | **String field name.** |
| 24 | Reflection (public field, string) | `garrys-torch.lib/WeldEngine.cs:172-173,182` | `CharacterAvatar.Core` — `public CharacterCore Core` (**struct** field) | `KSA/CharacterAvatar.cs:204` | Yes | Same (OLD `CharacterAvatar.cs:204`) | Mod uses `GetField("Core")` only (no property fallback) and writes the struct back via `SetValue` — correct **only while `Core` is a field of a value type**. If it becomes a property/ref type, scaling silently no-ops. |
| 25 | Reflection (public field, string) | `garrys-torch.lib/WeldEngine.cs:176-188` | `CharacterCore.Scale` — `public float Scale = 0.01f` (field) | `KSA/CharacterAvatar.cs:33` | Yes | Same (OLD `CharacterAvatar.cs:33`) | Mod sets `factor * 0.01f`; field + property paths both handled. |
| 26 | Direct typed API (UI color) | `garrys-torch.lib/GarrysTorchSubmod.cs:333-334` | `KSAColor.Xkcd.Scarlet`, `KSAColor.Xkcd.PaleGrey` — `static Color.Preset` | `KSA/KSAColor.cs:1561`,`837` | Yes | Same (cosmetic; no changelog color changes) | Unweld-button styling only; failure is visual, not functional. |
| 27 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs:14` (called `GarrysTorchSubmod.cs:158,527`) | `Universe.CurrentSystem` / `CelestialSystem.All` / `LookupCollection.UnsafeAsList` / `Vehicle.Id` | `KSA/Universe.cs:92` etc. | Yes | Same | Shared enumerator (see eternal-flame #12). |
| 28 | Harmony + Reflection | `garrys-torch/Patcher.cs:16` -> `HotkeyGuard.cs:21` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool`, `nameof`-resolved | `KSA/GameSettings.cs:2379` | Yes | Same | Shared guard. The **only** Harmony patch this mod registers. |
| 29 | Lifecycle | `garrys-torch/Mod.cs:19-80` | StarMap attrs (full set); weld physics in `OnAfterUi` after `JobSystems.VehicleSolver.Wait()` | (StarMap.API package) | Yes | **Renamed @5261** (was `VehicleSolvers`) | See *Update-risk findings (5117 → 5261)* |

**Game assets referenced** — None (TOML preset file is mod-authored under `.unscience/`, not a game asset).

**Update-risk findings (5117 → 5261)**

- **CONFIRMED COMPILE BREAK (revs 5208–5216, vehicle-threading rewrite):**
  `garrys-torch.lib/GarrysTorchSubmod.cs:93` — `KSA.JobSystems.VehicleSolvers.Wait()` → **CS0117**.
  The rework replaced the single multi-runner scheduler with two objects:

  | OLD (≤5168) | NEW (5261) |
  |---|---|
  | `VehicleSolvers` — `JobScheduler(0.75×count)`, priority Highest | `VehicleSolver` — `JobScheduler(1)` orchestrator |
  | — | `VehicleWorkerPool` — `DynamicWorkerPool(count−1)` parallel physics-bubble islands |

  → Fixed to `JobSystems.VehicleSolver.Wait()`. **Waiting on the orchestrator alone is the complete
  drain**, which matters because this call is correctness-critical (it prevents `Collection was
  modified` inside `VehicleUpdateTask` and `SnapToLeader body/origin time mismatch`):
  `DynamicWorkerPool` exposes **no `Wait()`** and is only ever driven through scoped
  `ParallelBatch()` fork/join blocks inside `VehicleUpdateTask`/`PhysicsBubble`/
  `Universe.ApplyVehicleSolvers`, so all pool work is joined before the queued `_vehicleUpdateTask`
  completes. **The game itself drains identically** — `Universe.DeserializeSave` calls
  `JobSystems.VehicleSolver.Wait()`. Reasoning is recorded at the call site.

- **CONFIRMED COMPILE BREAK (rev 5211, `SimTime` → `UniverseTime`):**
  `garrys-torch.lib/WeldEngine.cs:119` — the local `SimTime tickEndTime =
  Universe.GetJobSimStep(...).NextTime` → **CS0246**. `SimTime` became `UniverseTime` (backed by
  `Int128` nanoseconds instead of double seconds); `SimStep.NextTime` followed the rename.
  → Fixed to `UniverseTime`. No arithmetic changed — the value is still passed straight into
  `Orbit.CreateFromStateCci`, and `.Seconds()` still returns a `double` on the new type.

- ⚠️ **Needs a live pass.** Both fixes are signature-correct and the drain is provably complete, but
  the *parallelism model* underneath changed (per-vehicle parallel batch jobs, object-pooled
  `PhysicsBubble`/`ConstraintSim`, rev 5237's stale-resource-handle crash fix). garrys-torch mutates
  vehicle state from outside the solver, so the error spam recorded in
  [`../ISSUES.md`](../ISSUES.md) must be re-checked in game.

- `Vehicle.Teleport(Orbit?, doubleQuat?, double3?)` is **signature-identical** (line shift only), as
  is `Universe.GetJobSimStep(double)`. `KittenEva` gained ladder/jump/control-mode members but lost
  none, so the `_renderable` → `_characterAvatar` → `CharacterAvatar.Core` → `CharacterCore.Scale`
  reflection chain still resolves.

**Update-risk findings (4680 -> 4750)**

- **CONFIRMED COMPILE BREAK (rev 4729, Brutal package nullability):**
  `garrys-torch.lib/GarrysTorchSubmod.cs:457` — `ImGui.Text($"Are you sure you want to delete\npreset '{_deleteConfirmName}'?");`.
  `_deleteConfirmName` is `string?` (declared `GarrysTorchSubmod.cs:52`) and is interpolated into
  `ImGui.Text`'s `ImString` interpolated-string handler, whose `AppendFormatted(string value, ...)`
  parameter became **non-nullable** in the rev 4729 Brutal update -> **CS8604** "possible null
  reference argument" at col 64. This is the only such site because it is the only `string?`
  interpolated into an ImGui call without a preceding null-check (`_weldError`/`_savePresetError`
  are guarded by `IsNullOrEmpty` before use). Fix is a null-coalesce / local non-null capture;
  no game-symbol change involved.
- **Behavioral watch — rev 4699 `Vehicle.IsControllable`** (`KSA/Vehicle.cs:526`,
  `public virtual bool IsControllable => _overrideIsControllable || Parts.Controls.NumModules > 0`;
  **absent in OLD** — confirmed new). The mod does not read it, but player/Flight-Computer control
  is now gated on it. Welding teleports a *source* vehicle every frame; if that source has **no
  Control Module** (debris, a separated part), it is uncontrollable by the new rule — independent of
  welding. Welding does not strip control modules, and `KittenEva.IsControllable => true`
  (`KSA/KittenEva.cs:15`), so welded capsules/kittens stay controllable. Net new risk is low but
  worth noting for user expectations (e.g. welding a control-less hull won't make it drivable).
- **No symbol deltas** otherwise: all 25 typed/reflected game members (incl. the full KittenEva
  reflection chain `_renderable` -> `_characterAvatar` -> `Core` -> `Scale`) are signature-identical
  OLD->NEW (line shifts only). rev 4708 (orbit time printout) and rev 4722 (≤2-collider memory fix)
  are internal and do not change any signature the mod uses.
- **Standing reflection fragility** (not a 4750 delta, but the highest runtime-risk surface here):
  items #21-#25 are string-keyed. None are compile-checked; a rename of `KittenEva`,
  `_renderable`, `_characterAvatar`, `CharacterAvatar.Core`, or `CharacterCore.Scale` in any future
  build silently disables avatar scaling (caught by the mod's try/catch, logged, no crash).

---

## i-feel-seen (`i-feel-seen` / `i-feel-seen.lib`)

**Purpose** — Render-distance / LOD-cull override. For user-selected ("tracked") vehicles,
two Harmony **prefixes** replace the vehicle's render-matrix and render-data computation so
the vehicle is drawn regardless of camera distance.

**Unscience integration** — `IFeelSeenSubmod : ISubmod`
(`i-feel-seen.lib/IFeelSeenSubmod.cs:8`) owns a `VehicleTracker`
(`i-feel-seen.lib/VehicleTracker.cs:13`) exposed via `IFeelSeenSubmod.Tracker`. The two
prefixes live in `IFeelSeenPatches` (`i-feel-seen.lib/IFeelSeenPatches.cs`). Standalone host
`i-feel-seen/Mod.cs:27-29` calls `Patcher.Patch(_submod.Tracker)`
(`i-feel-seen/Patcher.cs:11`). Embedded host: `unscience/Mod.cs:60` (`var iFeelSeen = new IFeelSeenSubmod()`,
added at `:75`), tracker handed to the supermod patcher at `unscience/Mod.cs:106`
(`Patcher.IFeelSeenTracker = iFeelSeen.Tracker`), patches applied at
`unscience/Patcher.cs:48` (`IFeelSeenPatches.Apply(_harmony, IFeelSeenTracker!)`).

**UI/hotkeys** — Standalone window "I Feel Seen", 400x350, toggled by **F11**
(`i-feel-seen/Mod.cs:47,73`). Content (`IFeelSeenSubmod.RenderContent:27`): filterable vehicle
combo + Add, tracked-vehicle table with per-row "SeeMe" checkbox and del.

**Persistence** — None. Tracked list is in-memory (`VehicleTracker.Tracked`), cleared on
reload (`IFeelSeenSubmod.Dispose` -> `VehicleTracker.Clear`).

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Harmony (prefix) + Reflection (string) | `i-feel-seen.lib/IFeelSeenPatches.cs:27,30` | `Vehicle.GetWorldMatrix(Camera camera)` — `public float4x4?`; resolved `AccessTools.Method(typeof(Vehicle), "GetWorldMatrix")` (string), prefix `(Vehicle __instance, Camera camera, ref float4x4? __result)` | `KSA/Vehicle.cs:2772` | Yes | Same (OLD `Vehicle.cs:2682`) | **String-resolved**; method is `public`, non-virtual. Return type `float4x4?` must match prefix `ref` param. Stable OLD->NEW. |
| 2 | Harmony (prefix) + Reflection (string) | `i-feel-seen.lib/IFeelSeenPatches.cs:28,31` | `Vehicle.UpdateRenderData(Viewport viewport, int inFrameIndex)` — `public virtual void`; resolved `AccessTools.Method(typeof(Vehicle), "UpdateRenderData")`, prefix `(Vehicle __instance, Viewport viewport, int inFrameIndex)` | `KSA/Vehicle.cs:2785` | Yes | Same (OLD `Vehicle.cs:2695`) | **String-resolved.** `virtual`; `KittenEva` overrides it (`KSA/KittenEva.cs:62`) — see findings. |
| 3 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs:57` | `Camera.GetPositionEgo(IPosition astronomical)` — `public double3` | `KSA/Camera.cs:213` | Yes | Same (OLD `Camera.cs:212`) | Passes `__instance` (Vehicle is `IPosition`). |
| 4 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs:59` | `Vehicle.Body2Cce` — `public doubleQuat Body2Cce` | `KSA/Vehicle.cs:423` | Yes | Same (OLD `Vehicle.cs:390`) | Rotation for the override matrix. |
| 5 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs:69` | `Viewport.GetCamera()` — `public Camera` | `KSA/Viewport.cs:366` | Yes | Same (OLD `Viewport.cs:366`) | — |
| 6 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs:69` | `Vehicle.GetMatrixAsmb2Ego(Camera camera)` — `public double4x4` | `KSA/Vehicle.cs:833` | Yes | Same (OLD `Vehicle.cs:798`) | — |
| 7 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs:70` | `Vehicle.IsEditedVehicle` — `public bool` | `KSA/Vehicle.cs:356` | Yes | Same (OLD `Vehicle.cs:323`) | Passed to `PartTree.UpdateRenderData`. |
| 8 | Direct typed API (prefix body) | `i-feel-seen.lib/IFeelSeenPatches.cs:70` | `PartTree.UpdateRenderData(ref readonly double4x4 matrixAsmb2Ego, bool isEditedVehicle, Viewport viewport, int frameIndex)` — `public void` (via `Vehicle.Parts`, `KSA/Vehicle.cs:264`) | `KSA/PartTree.cs:435` | Yes | Same (OLD `PartTree.cs:431`) | Mod passes `in matrixAsmb2Ego` -> `ref readonly`. Re-implements the original's body to bypass the cull check. |
| 9 | Direct typed API | `i-feel-seen.lib/IFeelSeenSubmod.cs:29` + `VehicleTracker` | `VehicleProvider.GetAllVehicles()` chain + `Vehicle.Id`; tracked entries compared by reference | `KSA/Universe.cs:92` etc. | Yes | Same | Shared enumerator (see eternal-flame #12). |
| 10 | Harmony + Reflection | `i-feel-seen/Patcher.cs:15` -> `HotkeyGuard.cs:21` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool`, `nameof`-resolved | `KSA/GameSettings.cs:2379` | Yes | Same | Shared guard. |
| 11 | Lifecycle | `i-feel-seen/Mod.cs:19-69` | StarMap attrs (full set) | (StarMap.API package) | Yes | Same | Patches applied in `OnFullyLoaded` after tracker init. |

**Game assets referenced** — None.

**Update-risk findings (4680 -> 4750)**

- **No breaking deltas.** Both string-resolved patch targets and all six prefix-body members are
  signature-identical OLD->NEW (line shifts only).
- **Highest runtime risk (not a delta, but the thing to recheck every build):** the two patch
  targets are resolved by **string** (`"GetWorldMatrix"`, `"UpdateRenderData"`) via
  `AccessTools.Method`, so a rename/removal or signature change surfaces as a **runtime patch
  failure**, never a compile error. Both verified present + unchanged in 4750. (Note: the README
  mislabels these as "private/instance" — they are actually `public`; `AccessTools` finds them
  either way.)
- **Virtual-dispatch nuance for `UpdateRenderData` (pre-existing, unchanged):** it is `virtual`
  and `KittenEva` overrides it (`KSA/KittenEva.cs:62`, which calls `base.UpdateRenderData`). For a
  tracked normal `Vehicle`, the prefix fires on the direct call; for a tracked `KittenEva` the
  prefix fires only via the `base` call, after the override has already begun. `GetWorldMatrix` is
  non-virtual, so it is intercepted uniformly. This behavior is identical OLD->NEW; flagged only so
  a future change to `KittenEva`/virtual layout is evaluated here.
- **README drift (not a break):** `i-feel-seen/README.md` shows aspirational pseudocode
  (`ComputeWorldMatrix`, `ForceUpdateRenderData`, `vehicle.RenderData.Position`, a 2-arg
  `GetWorldMatrix` prefix). The real prefixes use the API rows above; those README symbols do not
  exist in the game and should not be used for triage.

---

## Cross-cutting notes (all three mods)

- **Shared chokepoints to watch first** (a change breaks multiple mods at once):
  - `VehicleProvider` chain — `Universe.CurrentSystem` (`KSA/Universe.cs:92`),
    `CelestialSystem.All` (`KSA/CelestialSystem.cs:57`),
    `LookupCollection<Astronomical>.UnsafeAsList()` (`KSA/LookupCollection.cs:210`),
    `Vehicle.Id` (`KSA/Astronomical.cs:85`), `Program.ControlledVehicle` (`KSA/Program.cs:254`).
    Drives every mod's vehicle list. All signature-identical OLD->NEW.
  - `Universe.ExecuteNextVehicleSolvers(double, SimStep)` (`KSA/Universe.cs:1660`) — patched by
    eternal-flame and central to garrys-torch's timing rationale.
  - `GameSettings.OnKeyAll` (`KSA/GameSettings.cs:2379`) — shared `HotkeyGuard`, `nameof`-resolved.
- **Embedded vs standalone Harmony:** when the unscience supermod is loaded it owns one
  `Harmony("MeowSci.Unscience")` that re-registers eternal-flame's solver prefix
  (`unscience/Patcher.cs:92-126`) and i-feel-seen's render prefixes (`unscience/Patcher.cs:48`);
  garrys-torch registers no game patch in either mode. Running a standalone mod *and* the supermod
  simultaneously would double-patch `ExecuteNextVehicleSolvers` — not a game-version risk, but a
  packaging note.
- **Mutation vs read:** eternal-flame and garrys-torch **write** game state
  (`Battery.Refill`/`Part.Scale`/`Vehicle.Teleport`); i-feel-seen **replaces** render computation
  via skip-original prefixes. None of these were affected by the 4680->4750 signature surface;
  the only build-induced breakage is the garrys-torch CS8604 (Brutal nullability, rev 4729).

---

## Area summary — Update-risk findings (5261 → 5348)

- ✅ **The physics-bubble rewrite does not move the eternal-flame seam.**
  `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep)` keeps its signature and remains
  a **single overload**, so the prefix shared by eternal-flame, flexo and kitchen-sink still attaches.
  Its **body** was substantially rewritten (revs 5331/5339): physics-bubble ownership moved entirely into
  `VehicleUpdateTask`, merge/split checks were made much less naive and moved onto the vehicle solver
  worker threads, and the method no longer walks `_physicsBubbles` itself — it now calls
  `RemoveEligibleVehicles()` / `PrepareVehicleWorkers()` / `SyncGroundClutter()` and queues
  `_vehicleUpdateTask`. The prefix still runs **before** `JobSystems.VehicleSolver.ExecuteJobs()`, so the
  refill timing is preserved.
- ✅ **garrys-torch's drain is intact.** `JobSystems.VehicleSolver` (single-runner `JobScheduler`,
  priority `Highest`) and `JobSystems.VehicleWorkerPool` (`DynamicWorkerPool`) are both unchanged;
  `GarrysTorchSubmod.cs:103` calls `KSA.JobSystems.VehicleSolver.Wait()`, which still exists.
  (Several nearby comments still say `VehicleSolvers` — comment-only staleness from the 5261 rename.)
- ✅ **eternal-flame's refill path is byte-identical.** `KSA/Battery.cs` diffs clean;
  `Battery.Refill(ref BatteryState)`, `Vehicle.RefillConsumables()` and
  `PartTree.Batteries.GetModuleAndAllMutableStatesForInitialization(...)` are all unchanged.
  The rev-5326 power rework touched circuit *construction* and *draw*, not refill.
- ✅ **garrys-torch / i-feel-seen typed surfaces unchanged.**
  `Vehicle.Teleport(Orbit?, doubleQuat?, double3?)`, `Vehicle.GetWorldMatrix(Camera)`,
  `Vehicle.UpdateRenderData(...)` and `Camera.GetPositionEgo` all keep their signatures.
  `Vehicle.IsControllable` is unchanged (`Vehicle.cs:582`).
- ✅ **Coordinate frames unchanged.** Rev 5280 extracted CCF/CCI/CCE quaternion composition into
  `KSA/CelestialFrameMath.cs` (`ComputeCcf2Cci`, `ComposeCcf2Cce`), but `Celestial.GetCcf2Cci`,
  `GetCci2Ccf`, `GetCci2Cce` and `GetCce2Cci` keep the same signatures and semantics — a pure
  extraction. garrys-torch's `GetCci2Cce` welding math is unaffected.
- ✅ **The KittenEva → `CharacterCore.Scale` chain is intact and still field-shaped**, so garrys-torch's
  `SetValue` still works. Rev 5329 turned `Module.Parent` into a property; garrys-torch does not
  reflect on it.
- ⚠️ **Ground-clutter collisions are new** (revs 5263/5274/5303/5307), default **off** behind
  *Settings → Simulation → Ground Clutter → "[Experimenta] Enable Collisions"*. Clutter is destroyed above
  25 J/kg impact energy, and kitten contact counts. garrys-torch teleports a vehicle **every frame** —
  with the setting on, that could now interact with clutter statics. Worth a live check.
- ℹ️ Re-test the [`../ISSUES.md`](../ISSUES.md) error spam for garrys-torch and flexo under the rewritten
  bubble model; the spam's shape may have changed.
