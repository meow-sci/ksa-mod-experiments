# 00 — Unscience supermod shell + `ksa-abstractions.lib` game-integration scope

Permanent reference for the **unscience supermod shell** (`unscience/`) and the **shared
seam library** (`ksa-abstractions.lib/`). Use it to detect when a KSA game update breaks these
two foundational projects. Individual feature submods (blinky, glass, i-feel-seen, …) are
catalogued in their own `scope/` files; here they appear only in the consolidated Harmony
cross-reference table.

Verification baseline:

- **NEW decomp (current, build 2026.6.9.4750):** `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- **OLD decomp (previous, build 2026.6.8.4680):** `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`
- Decomp paths below are **relative to the decomp root** (e.g. `KSA/Universe.cs`). KSA game types live under `KSA/`; ImGui/console types under `Brutal.ImGuiApi*`.
- Every game target was grepped in BOTH decomps; "Δ vs OLD" records the delta (line moves are not deltas).

---

## Architecture overview

- **One StarMap host.** `unscience/Mod.cs` is the single `[StarMapMod]` entry class. StarMap.API
  (NuGet **`StarMap.API` v0.3.6**, `PrivateAssets="all"`) is the loader seam, NOT the game — StarMap
  itself Harmony-patches the game's render loop and invokes the mod's attributed methods. So the
  shell never references the game's frame loop directly; it rides StarMap's hooks.
- **Submod aggregation.** The host instantiates 22 `ISubmod` implementations (one per feature
  lib), stores them in a list, and drives them uniformly: `Initialize()` once, `Update(dt)` every
  frame (even hidden), `RenderContent()` inside a `CollapsingHeader`, `RenderFloatingWindows()`
  always, `Dispose()` on unload. The same `ISubmod` classes are reused by each feature's own
  standalone mod host.
- **Single consolidated Harmony instance.** `unscience/Patcher.cs` owns exactly one
  `new Harmony("MeowSci.Unscience")`. Each feature lib exposes a static `Apply(Harmony)`/`Remove(Harmony)`
  patch class; the supermod applies them all onto
  its one instance instead of each mod owning its own. `HotkeyGuard` (from the seam lib) is applied
  first, exactly once.
- **`ksa-abstractions.lib` is the game-facing seam.** All cross-cutting game access is funnelled
  through small static helpers here (`VehicleProvider`, `CelestialProvider`, `SimTimeProvider`,
  `PartHelpers`, `XkcdColorHelper`, `HotkeyGuard`, `IvaForceRender`, `KsaPaths`) plus pure-C#
  utilities (`ISubmod`, `EasingHelper`, `Directions`, `GameThread`/`GameStateQueue`/
  `IGameStateScheduler`, `ReflectionHelpers`, `SubmodUI`). Concentrating game touchpoints here means a
  game update's blast radius is mostly this one library.

### StarMap lifecycle attributes used by `Mod.cs`

Attributes come from `StarMap.API` (`StarMap.API/BaseAttributes.cs`, `OnGuiAttributes.cs`); the
"game hook" column is the game method StarMap Harmony-patches to dispatch each attribute
(`StarMap.Core/Patches/ProgramPatcher.cs`, string-named).

| Mod.cs member (line) | Attribute | StarMap → game hook | Game method (NEW / OLD) | Δ vs OLD |
|---|---|---|---|---|
| `class Mod` (33) | `[StarMapMod]` | marks entry class (`StarMapModAttribute`) | n/a | — |
| `ImmediateUnload` prop (36) | required bool property | StarMap reads it during unload | n/a | — |
| `OnImmediateLoad` (51) | `[StarMapImmediateLoad]` | early load (renderer NOT live) | n/a | — |
| `OnFullyLoaded` (54) | `[StarMapAllModsLoaded]` | after all mods loaded → build submods + `Patcher.Patch()` | n/a | — |
| `OnBeforeUi(double dt)` (122) | `[StarMapBeforeGui]` | **PREFIX** of `Program.OnDrawUiFrame(double)` | `KSA/Program.cs:2639` / `:2582` | none (same sig) |
| `OnAfterUi(double dt)` (135) | `[StarMapAfterGui]` | **POSTFIX** of `Program.OnDrawUiViewports(double)` | `KSA/Program.cs:2666` / `:2609` | none (same sig) |
| `Unload` (182) | `[StarMapUnload]` | mod unload → `Patcher.Unload()` | n/a | — |

`[StarMapAfterOnFrame]` (POSTFIX of `Program.OnFrame(double,double)`, `KSA/Program.cs:1986` / OLD
`:1955`) exists in StarMap but is **not** used by the supermod shell. The shell's F11 toggle uses
`ImGui.IsKeyPressed(ImGuiKey.F11)` inside `OnAfterUi` (Brutal.ImGuiApi, not a game member).

> Risk seam: StarMap dispatch depends on the **string** method names `"OnDrawUiFrame"`,
> `"OnDrawUiViewports"`, `"OnFrame"` in `ProgramPatcher.cs`. If the game renames these, **StarMap.API**
> (not unscience) must be updated. All three are present and unchanged 4680→4750.

---

## Consolidated Harmony patches (cross-reference)

`unscience/Patcher.cs` applies/removes the following on its single `Harmony("MeowSci.Unscience")`
instance. Targets are listed at cross-reference granularity (type+member); per-class decomp deltas
live in each feature's own `scope/` file. **Two entries are owned by this area** (in **bold**) and
are fully verified below: the inlined `EternalFlamePatches` and `MenuBarPatch`.

| Patch class | Owning project | Apply (Patcher.cs) | Remove (Patcher.cs) | Primary game target(s) | Kind | Risk note |
|---|---|---|---|---|---|---|
| `HotkeyGuard` | **ksa-abstractions.lib** | 45 | 94 | `GameSettings.OnKeyAll(GlfwKeyEvent)` | prefix | verified ↓ (no delta) |
| `ThugLifeRenderPatches` | thug-life.lib | 46 | 107 | `SuperMeshRenderSystem.RenderMainPass` | postfix | render pass — see thug-life scope |
| **`MenuBarPatch`** | **unscience/ (self)** | 47 | 95 | `Program.DrawProgramMenusHook()` | postfix | verified ↓ (no delta) |
| `BlinkyPatches` | blinky.lib | 52 | 96 | `PartModelModule`/`PartModelDynamicModule`/`PartModelGlassModule`.`UpdateRenderData` | prefix ×3 | render — see blinky scope |
| `ShinyPatches` | its-so-shiny.lib | 53 | 97 | same three `UpdateRenderData` | prefix ×3 | render — see its-so-shiny scope |
| `CameraControllerOverridePatches` | camera-controller-override.lib | 54 | 98 | `OrbitController.OnFrame` / `FlyController.OnFrame` (**string** "OnFrame") | prefix | string-named — see camera scope |
| **`EternalFlamePatches`** | **unscience/ (INLINE)** | 59 | 99 | `Universe.ExecuteNextVehicleSolvers` | prefix `Priority.First` | verified ↓ (no delta) |
| `KiwisMarblesPatches` | kiwis-marbles.lib | 60 | 100 | `Universe.ExecuteNextVehicleSolvers` | prefix `Priority.First` | sim-step timing — see celestial-and-lights scope |
| `GlassPatches` | glass.lib | 65 | 101 | `Camera.ChangeFieldOfView` / `Camera.UpdateProjection` (**string**) + field `Camera._fovRadians` (**string**) | prefix | string-named — see glass scope |
| `IFeelSeenPatches` | i-feel-seen.lib | 66 | 102 | `Vehicle.GetWorldMatrix` / `Vehicle.UpdateRenderData` (**string**) | prefix | string-named — see i-feel-seen scope |
| `VehiclePaintPatches` | humble-arteest.lib | 67 | 106 | `PartModel.AddInstance` | prefix | render — see humble-arteest scope |
| `EngineEmissivePatches` | humble-arteest.lib | 68 | 103 | `PartModelDynamic.AddInstance` | prefix | render — see humble-arteest scope |
| `IvaForceRender` | **ksa-abstractions.lib** | 70 | 108 | IVA render gate (see ui-customization scope) | prefix | wired 2026-08-23 |
| `EditorScalePatches` | dont-stifle-me.lib | 71 | 105 | `VehicleEditor.ScaleBoundsFor` / `UpdateSelectedScale` / `QuantizeScale` | postfix/prefix | see part-editor-and-robotics scope |
| `KittenAnimationPatches` | kitten-animations.lib | 72 | 109 | `AnimatedRenderable.UpdateAnimation(double)` (**string** via `AccessTools.Method`) | prefix `(AnimatedRenderable __instance, ref double dt)` | ⚠️ **hot path** — runs for every animated renderable every frame; must stay a reference compare + early return. See character-and-materials scope |

Non-Harmony cleanup also driven by `Patcher.Unload()`: `VehiclePaint.Cleanup()` (line 111) and
`EngineEmissive.Cleanup()` (line 112), both humble-arteest.lib.

Notes:
- **garrys-torch is intentionally NOT a Harmony patch.** Its weld physics runs from
  `Mod.cs:173` (`OnAfterUi`) via `GarrysTorchSubmod.UpdateWelds(dt)`, which internally calls
  `JobSystems.VehicleSolvers.Wait()` before touching vehicle state (avoids the worker-iteration race).
- `IFeelSeenPatches.Apply` takes a second argument (`IFeelSeenTracker`, wired at `Mod.cs:106`).
- `CameraControllerOverridePatches.SequencePlayer` and `MenuBarPatch.ToggleWindow` are wired before
  Apply (Patcher.cs:49, 56).
- `KittenAnimationPatches.Driver` is wired **after** Apply, from `KittenAnimationsSubmod.Initialize()`
  (`Mod.cs` initialises submods after `Patcher.Patch()`). The prefix null-checks it, so the ordering
  is safe; before the submod initialises the patch is simply inert.

### `MenuBarPatch` (unscience/MenuBarPatch.cs) — owned by this area

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `MenuBarPatch.cs:8` (`[HarmonyPatch]`), applied `:15`, removed `:21` | `Program.DrawProgramMenusHook()` — `public void DrawProgramMenusHook()` (empty hook) | `KSA/Program.cs:3391` | Yes | None — identical empty instance method (OLD `:3334`) | Game ships this as a deliberate no-op modding hook. Postfix appends an "Unscience" `ImGui.MenuItem`. Low risk. |

### `EternalFlamePatches` (inlined in unscience/Patcher.cs) — owned by this area

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony prefix (`Priority.First`) | `Patcher.cs:96` (lookup), `:104` (patch), `:109-112` (remove) | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep)` — `public static void` | `KSA/Universe.cs:1660` | Yes | None — identical sig (OLD `:1109`) | Looked up by name only (`nameof`, no param-type array), so a param change would NOT break the lookup unless the method became overloaded. Prefix dispatches to `EternalFlameSubmod.Instance?.UpdateBeforeVehicleSolvers()`, wrapped in try/catch. Same target kiwis-marbles and kitchen-sink also patch. |

---

## `ksa-abstractions.lib` — per-helper integration points

Decomp paths relative to NEW decomp root. All confirmed present in NEW; OLD line noted only where useful.

### VehicleProvider.cs

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Direct API (field) | `VehicleProvider.cs:11` | `Program.ControlledVehicle` — `public static Vehicle? ControlledVehicle = null;` | `KSA/Program.cs:254` | Yes | None (OLD `:253`) | Returned as-is from `GetControlledVehicle()`. |
| 2 | Direct API (prop) | `:15` | `Universe.CurrentSystem` — `public static CelestialSystem? CurrentSystem { get; private set; }` | `KSA/Universe.cs:92` | Yes | None (OLD `:91`) | Null-safe (`?.`). |
| 3 | Direct API (prop) | `:15` | `CelestialSystem.All` — `public LookupCollection<Astronomical> All` | `KSA/CelestialSystem.cs:57` | Yes | None (OLD `:56`) | |
| 4 | Direct API (method) | `:15` | `LookupCollection<Astronomical>.UnsafeAsList()` — `public List<T> UnsafeAsList()` | `KSA/LookupCollection.cs:210` | Yes | None (OLD `:197`) | Then LINQ `OfType<Vehicle>()`. |
| 5 | Direct API (type) | `:11,14,18` | `Vehicle` — `public class Vehicle : Astronomical, …, IObjectId, …` | `KSA/Vehicle.cs:28` | Yes | None | |
| 6 | Direct API (prop) | `:22` | `Vehicle.Id` (inherited `Astronomical.Id` via `IObjectId`) — `public virtual string Id { get; protected set; }` | `KSA/Astronomical.cs:85` | Yes | None | `Id` is not declared on `Vehicle`; resolved through base `Astronomical`/`IObjectId`. |

Update-risk findings (4680→4750):
- **No breaking deltas.** All targets present, signatures identical.
- Behavioral (rev 4699): the game added `Vehicle.IsControllable` (`KSA/Vehicle.cs:526`,
  `public virtual bool IsControllable => _overrideIsControllable || Parts.Controls.NumModules > 0;`)
  — **absent in OLD** (0 occurrences in `Vehicle.cs`), backed by new `PartTree.Controls`
  (`KSA/PartTree.cs:49`, also absent in OLD). `VehicleProvider` does **not** consume it:
  `GetControlledVehicle()` still mirrors `Program.ControlledVehicle`, and `GetAllVehicles()` returns
  **all** `Vehicle`s regardless of controllability. Watch only if a consumer starts assuming a
  vehicle is controllable — control is now gated on a Control Module (capsule+kittens have one).

### CelestialProvider.cs

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Direct API | `CelestialProvider.cs:11-12` | `Universe.CurrentSystem.All.UnsafeAsList()` (as above) | `KSA/Universe.cs:92`, `KSA/CelestialSystem.cs:57`, `KSA/LookupCollection.cs:210` | Yes | None | then `OfType<Celestial>()`. |
| 2 | Direct API (type) | `:12` | `Celestial` — `public abstract class Celestial : Astronomical, IOrbiter, …` | `KSA/Celestial.cs:19` | Yes | None | |
| 3 | Direct API (type) | `:16` | `IOrbiter` — `public interface IOrbiter : IFollowable, IObjectId, …` | `KSA/IOrbiter.cs:10` | Yes | None | `GetAllOrbiters()` = celestials + vehicles. |

Update-risk findings (4680→4750): **No breaking deltas detected.**

### SimTimeProvider.cs

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Direct API (method) | `SimTimeProvider.cs:9` | `Universe.GetElapsedTime()` — `public static UniverseTime GetElapsedTime()` | `KSA/Universe.cs:2124` | Yes | **RENAMED @5261** (was `GetElapsedSimTime()`) | rev 5211 |
| 2 | Direct API (type) | `:9` | `UniverseTime` — `public readonly struct UniverseTime : IEquatable<UniverseTime>`, backed by `Int128` nanoseconds | `KSA/UniverseTime.cs:6` | Yes | **RENAMED + RETYPED @5261** (was `SimTime`, double seconds, `KSA/SimTime.cs`) | rev 5211 |
| 3 | Direct API (method) | consumers | `UniverseTime.Seconds()` — `public double Seconds()` | `KSA/UniverseTime.cs:95` | Yes | None | **The compatibility hinge** — still returns `double`, so no caller arithmetic changed |

Update-risk findings (5117 → 5261):

- **CONFIRMED COMPILE BREAK (rev 5211):** *"Replaced SimTime with UniverseTime, backed by 128-bit
  nanoseconds. This is a prelude to creating 64-bit nanosecond integer BubbleTime within physics
  steps…"* → **CS0246** at `SimTimeProvider.cs:9`. Because this is the suite's single game-facing
  time seam, the failure blocked **all 55 projects** — the rest of the solution's errors were hidden
  behind it until this one was fixed.
- **Fix is type-only.** `.Seconds()` survives on the new struct and every consumer either calls it
  (`geeforce.lib/GeeForceSubmod.cs:34`, `steely-eyed-missile-kitten.lib/Monitoring/MonitoringLoop.cs:45`,
  `steely-eyed-missile-kitten/Mod.cs:158`) or passes the value straight into
  `Orbit.CreateFromStateCci` (`kiwis-marbles.lib/CelestialWeldEngine.cs:33`). **No precision or
  arithmetic handling needed changing**, despite the double→`Int128` backing swap.
- **The wrapper keeps the name `SimTimeProvider`.** Renaming the class to match the game would churn
  four call sites across three mods for no functional gain; noted as an optional follow-up, not done.
  This is exactly the blast-radius concentration this library exists for — one game rename cost
  **one line** here plus two incidental direct callers (`doh.lib`, `garrys-torch.lib`) that bypass it.

Update-risk findings (4680→4750): **No breaking deltas detected.**

### ReflectionHelpers.cs

| # | Kind | Mod code (file:line) | Game target | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Reflection (generic) | `ReflectionHelpers.cs:14,22` | none hardcoded — `Type.GetField(name, Public\|NonPublic\|Instance)` get/set | n/a | n/a | n/a | This helper has **no** compile-checked or string-literal game member of its own. Runtime risk lives entirely in **callers** that pass private field-name strings; those are catalogued per consuming submod. |

Update-risk findings (4680→4750): **No breaking deltas detected** (no game member referenced here).

### PartHelpers.cs

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Direct API (field) | `PartHelpers.cs:13` | `Vehicle.Parts` — `public PartTree Parts;` | `KSA/Vehicle.cs:264` | Yes | None (OLD `:233`) | |
| 2 | Direct API (prop) | `:13` | `PartTree.Parts` — `public ReadOnlySpan<Part> Parts => …` | `KSA/PartTree.cs:67` | Yes | None (OLD `:65`) | top-level parts. |
| 3 | Direct API (prop) | `:32` | `Part.SubParts` — `public ReadOnlySpan<Part> SubParts => …` | `KSA/Part.cs:655` | Yes | None (OLD `:654`) | recursion key. |
| 4 | Direct API (type) | `:11,20,29` | `Part` | `KSA/Part.cs` | Yes | None | |

Update-risk findings (4680→4750):
- **No breaking deltas detected.** The helper traverses via `SubParts` (span recursion).
- For completeness: `Part.TreeParent` (`KSA/Part.cs:385`, OLD `:384`) and `Part.TreeChildren`
  (`KSA/Part.cs:387`, OLD `:386`) — the alternate tree API named in the task — both exist and are
  unchanged, but `PartHelpers` does **not** use them.

### IGameStateScheduler.cs / GameStateQueue.cs / GameThread.cs

| # | Kind | Mod code | Game target | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|
| 1 | Pure C# | all three files | **none** — `System.Threading`, `ConcurrentQueue`, `TaskCompletionSource(RunContinuationsAsynchronously)` only | n/a | n/a | Game-thread affinity abstraction. Off-thread callers `Schedule(...)`; game thread `DrainOnGameThread()` (called from a submod `Update`, i.e. inside `[StarMapBeforeGui]`). No game API surface. |

Update-risk findings (4680→4750): **No breaking deltas detected** (no game dependency).

### ISubmod.cs

| # | Kind | Mod code | Game target | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|
| 1 | Pure C# interface | `ISubmod.cs` | **none** | n/a | n/a | Contract consumed by the shell + every feature lib. No game dependency. |

Update-risk findings (4680→4750): **No breaking deltas detected.**

### EasingHelper.cs / EasingType

| # | Kind | Mod code | Game target | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|
| 1 | Pure C# | `EasingHelper.cs` | **none** — `System.Math` only | n/a | n/a | No game dependency. |

Update-risk findings (4680→4750): **No breaking deltas detected.**

### Directions.cs

Named unit-axis vectors (`Up`/`Down`/`Left`/`Right`/`Forward`/`Backward`) in KSA's right-handed,
Y-up, -Z-forward convention. Added at **5117** to replace `KSA.Double3Ex.{Up,Down,Left,Right,Forward,
Backward}`, which rev 5067 removed (*"they were misleading and often misused"*). Values are identical
to the removed properties, so adopting it was behavior-neutral.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Direct API (struct) | `Directions.cs:20-35` | `Brutal.Numerics.double3.UnitX/UnitY/UnitZ` — `public static double3 UnitX => new double3(1,0,0)` etc. | `Brutal.Numerics/double3.cs:47-51` | ✅ | Same | Only dependency. Not a KSA type — moves with the Brutal package, not the game build. |

**Used by:** *(none as of 5348)* — its 19 call sites all lived in space-tape, which was removed.
Kept because it is a cheap, correct abstraction and the next editor-side mod will want it.

Update-risk findings (5018→5117):
- Deliberately **not** an alias for `Camera.ForwardView`/`RightView`/`UpView`. The game kept those for
  genuine camera-view-frame use and explicitly narrowed their meaning in rev 5067 (*"Clarified
  reference frame for camera vectors"*); routing frame-agnostic gizmo/thumbnail axes through a camera
  type would re-create the ambiguity the game just removed.
- Lowest-risk entry in this library: no KSA member is referenced at all.

### XkcdColorHelper.cs

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Reflection (type) | `XkcdColorHelper.cs:22` | `KSAColor.Xkcd` — `public static class Xkcd` (nested in `struct KSAColor`) | `KSA/KSAColor.cs:23` | Yes | None (OLD `:23`) | Enumerates `GetProperties(Public\|Static)`. |
| 2 | Direct API (cast) | `:29` | `Color.Preset` (Brutal.Numerics) — property type of each Xkcd color; implicit `Color.Preset → float4` | `KSA/KSAColor.cs:25+` (props), `Brutal.Numerics/Color.cs` (Preset) | Yes | None | Each prop is `public static Color.Preset Name => float3.Rgb(...)`. |

Update-risk findings (4680→4750):
- **No breaking deltas detected.** Reflection-driven enumeration is resilient to individual color
  additions/renames; it breaks only if the `KSAColor.Xkcd` type is removed/renamed, or if the
  `Color.Preset → float4` conversion is dropped. Neither occurred.

### HotkeyGuard.cs

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony prefix | `HotkeyGuard.cs:21` (lookup), `:23` (patch), `:28-29` (unpatch) | `GameSettings.OnKeyAll(GlfwKeyEvent keyEvent)` — `public static bool` | `KSA/GameSettings.cs:2379` | Yes | None (OLD `:2347`) | Prefix `Prefix(ref bool __result)`: when guard active, sets `__result = true` and returns false (skip original), swallowing the key. Looked up by `nameof`. |
| 2 | Direct API (field) | `:38` | `Program.ConsoleWindow` — `public static ConsoleWindow ConsoleWindow;` | `KSA/Program.cs:246` | Yes | None (OLD `:245`) | |
| 3 | Direct API (prop) | `:38` | `ConsoleWindow.IsOpen` — `public bool IsOpen => _show;` | `Brutal.ImGuiApi.Abstractions/ConsoleWindow.cs:292` | Yes | None (OLD `:292`) | Guard is bypassed while the dev console is open. |
| 4 | ImGui API | `:38` | `ImGui.GetIO().WantTextInput` (Brutal.ImGuiApi) | `Brutal.ImGuiApi/*` | Yes | None observed | Detects ImGui text-input focus globally (every InputText/combo filter). See Brutal-package note below. |

Update-risk findings (4680→4750):
- **No breaking deltas detected.** `GameSettings.OnKeyAll` and `Program.ConsoleWindow.IsOpen`
  unchanged. `ImGui.GetIO().WantTextInput` compiles against the 4750 Brutal packages.

### IvaForceRender.cs

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony postfix (ctor) | `IvaForceRender.cs:42` (lookup), `:44` (patch) | `PartModel..ctor(PartModelModule.Template)` — `protected PartModel(PartModelModule.Template template)` | `KSA/PartModel.cs:351` | Yes | None (OLD `:351`) | `AccessTools.Constructor` finds the **protected** ctor; explicit param-type array. |
| 2 | Harmony postfix (method) | `:46` (lookup), `:48` (patch) | `PartModel.AddInstance(PerInstanceData, Viewport, int)` — `public void` | `KSA/PartModel.cs:375` | Yes | None (OLD `:375`) | Postfix captures `__instance`, `__0`(PerInstanceData), `__1`(Viewport); ignores the `int frameIndex`. |
| 3 | Direct API (nested struct) | `:98` | `PartModel.PerInstanceData` — `public struct PerInstanceData` | `KSA/PartModel.cs:299` | Yes | None (OLD `:299`) | postfix param type. |
| 4 | Direct API (field) | `:87,89,101` | `PartModelModule.Template.Internal` — `public bool Internal = false;` | `KSA/PartModelModule.cs:36` | Yes | None (OLD `:36`) | mutated to force interior render. |
| 5 | Direct API (field) | `:103` | `PartModelModule.Template.RayTracing` — `public RaytracingMode RayTracing` | `KSA/PartModelModule.cs:30` | Yes | None (OLD `:30`) | |
| 6 | Direct API (enum) | `:103` | `PartModelModule.RaytracingMode.ShadowProxy` | `KSA/PartModelModule.cs:14` | Yes | None (OLD `:14`) | |
| 7 | Direct API (field) | `:100` | `Program.Editor` — `public static VehicleEditor? Editor;` | `KSA/Program.cs:194` | Yes | None (OLD `:193`) | editor-only branch. |
| 8 | Direct API (prop) | `:102` | `Program.MainViewport` — `public static Viewport MainViewport => …` | `KSA/Program.cs:403` | Yes | None (OLD `:402`) | |
| 9 | Direct API (field/enum) | `:102` | `Viewport.Mode` (`public CameraMode Mode;`) vs `CameraMode.IVA` | `KSA/Viewport.cs:14`, `KSA/CameraMode.cs:14` | Yes | None (OLD `:14`/`:14`) | |
| 10 | Direct API (nested static) | `:105` | `PartModel.ViewportData.Get(PartModel, Viewport)` → `.InstanceList.Add(...)` | `KSA/PartModel.cs:281` (Get), `:277` (InstanceList) | Yes | None (OLD `:281`/`:277`) | re-adds internal instance to the per-viewport draw list in the editor. |
| 11 | Direct API (static field) | `:111` | `PartModel.Instances` — `public static List<PartModel> Instances` | `KSA/PartModel.cs:325` | Yes | None (OLD `:325`) | enumerated by the `Enabled` setter to mutate existing templates. |

Update-risk findings (4680→4750):
- **No breaking deltas detected.** Every IvaForceRender target is byte-for-byte unchanged
  4680→4750, including line numbers — despite the changelog's mesh churn (4693 merged
  DynamicMeshIndirect into MeshIndirect; 4745 cleaned MeshIndirect layout indices / combined
  ModelGlass+ModelEye shaders). Those changes touched mesh layout and shaders, not the `PartModel`
  instance-list / `Template.Internal` API this helper uses.
- **IvaForceRender wiring — FIXED (Phase 4).** `unscience/Patcher.cs` now calls
  `IvaForceRender.Patch(_harmony)` in `Patch()` and `IvaForceRender.Unpatch(_harmony)` in `Unload()`
  (previously wired only in the standalone `kitchen-sink/Patcher.cs:23,39`).
  The supermod's "Force IVA Rendering" toggle therefore now also handles interior parts spawned *after* the
  toggle (ctor postfix) and editor-preview internal meshes (`AddInstance` postfix), not just the
  `Enabled`-setter mutation of already-loaded `PartModel.Instances` templates. (The separate kitchen-sink
  vehicle-solver prefix behind kitchen-sink's "Flexo Part Test" *Update Physics* button remains
  standalone-only — out of scope here. Note that kitchen-sink's Flexo\* test panels are named after the
  removed flexo mod but are independent of it and were kept.)

### KsaPaths.cs

| # | Kind | Mod code | Game target | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|
| 1 | OS path | `KsaPaths.cs:9` | **none** — `MyDocuments\My Games\Kitten Space Agency` | n/a | n/a | No game API. Breaks only if the game changes its user-data folder name. |

Update-risk findings (4680→4750): **No breaking deltas detected.**

### SubmodUI.cs

| # | Kind | Mod code | Game target | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|
| 1 | ImGui API | `SubmodUI.cs:28-31,40-41` | Brutal.ImGuiApi only — `PushStyleVar(WindowPadding)`, `BeginChild(AutoResizeY\|AlwaysUseWindowPadding, NoScrollbar)`, `PopStyleVar`, `Dummy`, `EndChild` | `Brutal.ImGuiApi/*` | Yes | None observed | No KSA game internals. See Brutal-package note. |

Update-risk findings (4680→4750): **No breaking deltas detected** (compiles against 4750 Brutal).

---

## `unscience/UnscienceState.cs` — persistence

Persistence only; no KSA game internals beyond `KsaPaths` + the ImGui ini API.

- **State dir:** `KsaPaths.UserDataDir + "\.unscience"` → `…\My Documents\My Games\Kitten Space Agency\.unscience`.
- **Files:** `window.ini` (ImGui window layout) and `state.toml` (submod header-open + visibility + settings).
- **ImGui ini round-trip:** load via `ImGui.LoadIniSettingsFromMemory(string)` (`:35`); save via
  `ImGui.SaveIniSettingsToMemory().ToString()` (`:48`), then `FilterIniForUnscienceWindows` keeps only
  the `[Window][Unscience Toolbox]` section so unrelated game windows aren't persisted.
- **TOML:** Tomlyn (`Toml.TryToModel<TomlTable>` / `Toml.FromModel`) — `[header_open]`, `[visibility]`,
  `[settings]` (`save_interval` clamped 1–30, `auto_save_enabled`, `show_mod_tooltips`). Pure managed
  library, no game dependency.
- **Autosave cadence:** `Mod.cs:149-156` accumulates `dt` in `OnAfterUi` and saves every
  `SaveIntervalSeconds` while the window is visible.

Update-risk findings (4680→4750): **No breaking deltas detected.** Only game-adjacent surface is
the Brutal.ImGuiApi ini API (see note below); it compiles against 4750.

---

## Area summary — Update-risk findings (5117 → 5261)

- **One breaking delta in `ksa-abstractions.lib`:** `SimTimeProvider` (rev 5211 `SimTime` →
  `UniverseTime`) — see that helper's section above. It blocked the whole solution because every
  project depends on this library; fixing it revealed the remaining four compile breaks.
- **Every other patch target is byte-identical** (line shifts only): `GameSettings.OnKeyAll`
  (HotkeyGuard → **every** top-level mod), `Program.DrawProgramMenusHook()` (MenuBarPatch),
  `Program.DrawMenuBar(Viewport,int)`, `Universe.ExecuteNextVehicleSolvers(double, SimStep)` (still a
  single overload — important, since it is resolved with no param array),
  `PartModel..ctor(PartModelModule.Template)` + `PartModel.AddInstance` (IvaForceRender).
- **StarMap seams intact:** `Program.OnDrawUiFrame`, `OnDrawUiViewports`, `OnFrame` and
  `DrawProgramMenusHook` all still present, so the suite's load path is unaffected. The
  `[StarMapAllModsLoaded]`-before-`ModLibrary.Bind()` invariant was not re-derived this pass.
- **Brutal packages:** solution builds clean with `TreatWarningsAsErrors` and **0 warnings** against
  the 5261 DLLs, so no nullability/signature shift landed in the ImGui surface actually used
  (contrast the rev-4729 bump, which cost `garrys-torch.lib` a CS8604 — now gone).
- ⚠️ **Note for the next pass:** `ksa-game-assemblies_prev` (`2026.8.5.5168`) was **never validated**.
  Two of this pass's five compile breaks originated in that window. Treat `_prev` as a diff aid only.

---

## Area summary — Update-risk findings (5018 → 5117)

- **No breaking deltas** for the supermod shell or any existing `ksa-abstractions.lib` helper. Every
  patch target is byte-identical: `GameSettings.OnKeyAll(GlfwKeyEvent) → bool`
  (`KSA/GameSettings.cs`, HotkeyGuard → **every** top-level mod),
  `Program.DrawProgramMenusHook()` (MenuBarPatch), `Program.DrawMenuBar(Viewport,int)`,
  `Universe.ExecuteNextVehicleSolvers(double, SimStep)` (still a single overload),
  `PartModel..ctor(PartModelModule.Template)` + `PartModel.AddInstance` (IvaForceRender),
  `KSAColor.Xkcd` (file unchanged).
- **StarMap load-order invariant HOLDS:** `ModLibrary.LoadAll()` (`KSA/Program.cs:965`) still precedes
  `ModLibrary.Bind()` (`KSA/Program.cs:994`), so `[StarMapAllModsLoaded]` still fires before
  `DeviceMeshInterleaved.Shared.Build()`. This is parts-now's headline standing invariant (U1).
- **New helper: `Directions.cs`** — see above. Added to absorb the rev-5067 `Double3Ex` removal in one
  place rather than at 19 call sites, per this library's stated purpose of concentrating a game
  update's blast radius.
- **Brutal packages:** solution builds clean with `TreatWarningsAsErrors` and **0 warnings** against
  the 5117 DLLs, so no nullability/signature shift landed in the ImGui surface actually used
  (contrast the rev-4729 bump, which cost `garrys-torch.lib` a CS8604).

---

## Area summary — Update-risk findings (4680 → 4750)

- **No breaking deltas** for the supermod shell or any `ksa-abstractions.lib` helper. Every game
  target (StarMap-hooked `Program` methods, `Universe.*`, `Program.*`, `GameSettings.OnKeyAll`,
  `CelestialSystem`/`LookupCollection`, `Vehicle`/`Part`/`PartTree`, `KSAColor.Xkcd`, full `PartModel`
  IVA surface) is present in 4750 with an identical signature.
- **Additive only (rev 4699):** `Vehicle.IsControllable` and `PartTree.Controls` are new in 4750
  (absent in 4680). Not consumed by the seam library → no break. Behavioral watch-area only:
  game control is now gated on a Control Module.
- **Secondary watch-area — Brutal packages (rev 4729, "latest Brutal packages, possible ImGui
  nullability/signature shifts"):** the shell's UI (`Mod.cs`), `UnscienceState` ini I/O, `SubmodUI`,
  and `HotkeyGuard.WantTextInput` all ride Brutal.ImGuiApi. The solution **builds clean against the
  4750 DLLs** (recon task #7), so no signature break in the ImGui calls actually used; flag for
  re-check on each Brutal bump.
- **IvaForceRender survived the mesh/shader churn (rev 4693/4745):** its `PartModel` instance-list /
  `Template.Internal` API is unchanged.
- **Coverage gap CLOSED (Phase 4):** the unscience supermod now applies `IvaForceRender.Patch`
  (`unscience/Patcher.cs`), so the ctor/`AddInstance` postfixes run in supermod mode too — not just the
  direct `Enabled`-setter mutation path. (Previously only the standalone kitchen-sink mod applied it.)
- **Patch chain hardened (Phase 4):** `unscience/Patcher.cs` now applies/removes each feature's patches in
  isolation (per-feature try/catch — `TryApply`/`TryRemove`), so a single feature failing to patch logs and is
  skipped instead of aborting every feature after it. This was prompted by the camera `___Transform` defect
  (see `camera.md`), whose patch-time throw had been silently aborting the rest of the chain in the supermod.
- **Highest residual runtime risk lives in the consolidated patch classes owned by other submods**
  (string-named lookups: camera `"OnFrame"`, glass `"ChangeFieldOfView"`/`"UpdateProjection"`/`_fovRadians`,
  i-feel-seen `"GetWorldMatrix"`/`"UpdateRenderData"`). They are cross-referenced above; their decomp
  deltas are catalogued in the respective feature `scope/` files. The two patches owned by this area
  (inline `EternalFlamePatches` → `Universe.ExecuteNextVehicleSolvers`; `MenuBarPatch` →
  `Program.DrawProgramMenusHook`) are verified clean.

---

## Area summary — Update-risk findings (5261 → 5348)

- ✅ **The shared provider chokepoint is unchanged.** `Universe.CurrentSystem`
  (`public static CelestialSystem? CurrentSystem { get; private set; }`) → `CelestialSystem.All`
  (`LookupCollection<Astronomical>`) → `LookupCollection<T>.UnsafeAsList()` all diff clean, so
  `VehicleProvider`, `CelestialProvider` and every feature mod's UI that reaches vehicles/celestials
  through them are safe.
- ✅ **`HotkeyGuard` clean.** `GameSettings.OnKeyAll(GlfwKeyEvent)` is unchanged, and so is the
  `Program.OnKey` call chain it sits in — so the guard still blocks game hotkeys for **every** top-level
  mod (marque's local copy included).
- ✅ **StarMap's seams are present.** `Program.OnDrawUiFrame`, `Program.OnFrame` and
  `Program.DrawProgramMenusHook` all still exist, so the suite's load path is intact. Rev 5332 changed
  `Program.DrawMenuBar` only by gating the Save/Load `MenuItem` on `!IsEditorOpen`; unscience's
  `MenuBarPatch` (a `DrawProgramMenusHook` prefix) is unaffected.
- ⚠️ **`ReflectionHelpers` has no property fallback — and rev 5329 made that matter.**
  `GetFieldValue`/`SetFieldValue` call `Type.GetField` only. Rev 5329 split `IPartParent` out of `Module`
  and moved `Parent` from a `Module<T>` **field** (`public required Part Parent;`) to a
  `ModuleBase.Parent` **auto-property** (`public required Part Parent { get; set; }`).
  **Audited: no mod in the suite reflects on `Parent`,** so nothing broke — but this is the exact shape of
  failure `ReflectionHelpers` cannot survive, and it should be the first thing checked whenever a game
  refactor moves members between base types. Consider adding a property fallback.
- ✅ **`SimTimeProvider` clean.** `Universe.GetElapsedTime() : UniverseTime` and `.Seconds()` are
  unchanged from the 5261 migration.
- ✅ **`IvaForceRender` clean.** `PartModel..ctor(PartModelModule.Template)`, `PartModel.AddInstance`,
  `PartModel.Instances`, `PartModel.ViewportData.Get`, `PartModelModule.Template.Internal` and
  `CameraMode.IVA` all resolve unchanged. Rev 5312 added receive-only raytracing for IVA kittens — worth
  a live look, not a code change.
- ✅ **`PartHelpers` clean.** `Part`, `PartTree`, `Part.Modules`, `Part.SubParts`, `Part.Asmb2ParentAsmb`
  and `Part.PositionParentAsmb` are unchanged. Note rev 5329 **removed** `Part.Sequence`,
  `SetSequence(int)`, `ActivateInStage`, `DeactivateInStage` and `ScaleTotal` — **no unscience code
  referenced any of them**, confirmed by the green build and by grep.
- ✅ **`XkcdColorHelper`, `GameThread`/`GameStateQueue`, `EasingHelper`, `Directions`, `KsaPaths`,
  `SubmodUI`, `UnscienceState`** — no breaking deltas; the whole solution builds with
  `TreatWarningsAsErrors` on and **0 warnings**, so no Brutal/ImGui nullability shift landed in the
  surface the suite uses.
- ❌ **Still open:** `unscience/Patcher.cs` never calls `IvaForceRender.Patch`, so kitchen-sink's IVA
  force-render remains partial inside the supermod. Unchanged by this build.
- ℹ️ **space-tape is gone.** Its `ISubmod` registration, `ProjectReference` and the
  `SpaceTapeSubmod.HideHostWindow` wiring were removed from `unscience/Mod.cs` and
  `unscience/unscience.csproj`. **The supermod now aggregates 22 submods, not 23.**
