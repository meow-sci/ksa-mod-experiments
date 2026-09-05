# 00 — Unscience supermod shell + `ksa-abstractions.lib` game-integration scope

Permanent reference for the **unscience supermod shell** (`unscience/`) and the **shared
seam library** (`ksa-abstractions.lib/`). Use it to detect when a KSA game update breaks these
two foundational projects. Individual feature submods (glass, i-feel-seen, …) are
catalogued in their own `scope/` files; here they appear only in the consolidated Harmony
cross-reference table.

Verification baseline:

- **NEW decomp (current, build 2026.9.7.5402):** `~/repos/meow-sci/ksa-game-assemblies/current/decomp`
- **OLD decomp (previous, build 2026.8.22.5348):** `~/repos/meow-sci/ksa-game-assemblies_prev/current/decomp`
- Decomp line numbers in the tables below are **@5402** unless a row says otherwise (older passes' lines are kept only inside the dated area summaries).
- Decomp paths below are **relative to the decomp root** (e.g. `KSA/Universe.cs`). KSA game types live under `KSA/`; ImGui/console types under `Brutal.ImGuiApi*`.
- Every game target was grepped in BOTH decomps; "Δ vs OLD" records the delta (line moves are not deltas).

---

## Architecture overview

- **One StarMap host.** `unscience/Mod.cs` is the single `[StarMapMod]` entry class. StarMap.API
  (NuGet **`StarMap.API` v0.3.6**, `PrivateAssets="all"`) is the loader seam, NOT the game — StarMap
  itself Harmony-patches the game's render loop and invokes the mod's attributed methods. So the
  shell never references the game's frame loop directly; it rides StarMap's hooks.
- **Workspace feature aggregation.** The single host initializes 25 `IWorkspaceFeature` implementations from separate feature libraries. `Update` and floating windows continue regardless of visibility. The main window renders one selected authoring form; a separate Live State window collects typed items and invokes their inspectors. Standalone feature entry projects and RPC are retired. The host drains `GameThread` before feature updates.
- **Pure authoring restore.** `unscience-contracts.lib` owns versioned documents, atomic named-save storage and prepared/rollback restoration. `ksa-abstractions.lib/Workspace` binds explicit values/choices/sections. Apply methods and live record registries stay in each feature. Restoring a workspace calls no feature lifecycle or game mutation method.
- **Shared domain infrastructure.** `ksa-lights.lib` owns LightController; `ksa-rings.lib` owns ring assets and outgoing-reference replacement coordination. Feature libraries do not reference other feature libraries.
- **Single consolidated Harmony instance.** `unscience/Patcher.cs` owns exactly one
  `new Harmony("MeowSci.Unscience")`. Each feature lib exposes a static `Apply(Harmony)`/`Remove(Harmony)`
  patch class; the supermod applies them all onto
  its one instance instead of each mod owning its own. `HotkeyGuard` (from the seam lib) is applied
  first, exactly once.
- **`ksa-abstractions.lib` is the game-facing seam.** All cross-cutting game access is funnelled
  through small static helpers here (`VehicleProvider`, `CelestialProvider`, `SimTimeProvider`,
  `PartHelpers`, `XkcdColorHelper`, `HotkeyGuard`, `HiddenUiFrameHook`, `IvaForceRender`, `KsaPaths`) plus pure-C#
  utilities (`ISubmod`, `EasingHelper`, `Directions`, `GameThread`/`GameStateQueue`/
  `IGameStateScheduler`, `ReflectionHelpers`, `SubmodUI`). Concentrating game touchpoints here means a
  game update's blast radius is mostly this one library.

### StarMap lifecycle attributes used by `Mod.cs`

Attributes come from `StarMap.API` (`StarMap.API/BaseAttributes.cs`, `OnGuiAttributes.cs`); the
"game hook" column is the game method StarMap Harmony-patches to dispatch each attribute
(`StarMap.Core/Patches/ProgramPatcher.cs`, string-named).

| Mod.cs member | Attribute | StarMap → game hook | Game method (NEW / OLD) | Δ vs OLD |
|---|---|---|---|---|
| `class Mod` (38) | `[StarMapMod]` | marks entry class (`StarMapModAttribute`) | n/a | — |
| `ImmediateUnload` prop (40) | required bool property | StarMap reads it during unload | n/a | — |
| `OnImmediateLoad` (56) | `[StarMapImmediateLoad]` | early load (renderer NOT live) | n/a | — |
| `OnFullyLoaded` (59) | `[StarMapAllModsLoaded]` | after all mods loaded → build submods + `Patcher.Patch()` | n/a | — |
| `OnBeforeUi(double dt)` (137) → `UpdateSubmods` (143) | `[StarMapBeforeGui]` | **PREFIX** of `Program.OnDrawUiFrame(double)` | `KSA/Program.cs` @5402 (`:2892` @5348) | none (same sig; body only gained `PartContactLoadDebug.Draw()`) |
| `OnAfterUi(double dt)` (171) → `UpdateWelds` (162) | `[StarMapAfterGui]` | **POSTFIX** of `Program.OnDrawUiViewports(double)` | `KSA/Program.cs` @5402 (`:2921` @5348) | same sig; body now iterates `ViewportRegistry.GameViews` and draws only `HasUi` secondary viewports (5402) |
| `UpdateSubmods` / `UpdateWelds` (registered during OnFullyLoaded) | `HiddenUiFrameHook.BeforeGui` / `.AfterGui` (**not** StarMap) | **PREFIX** of `Program.OnDrawUiConsole(double)`, active only while `Program.DrawUI == false` | `KSA/Program.cs` @5402 (`:2880` @5348) | same sig; body uses `HoveredViewport.IsMain()` instead of index compare (5402) |
| `Unload` (212) | `[StarMapUnload]` | mod unload → `Patcher.Unload()` | n/a | — |

**Hidden-HUD (F2) fallback.** `Program.OnFrame` (`KSA/Program.cs` @5402) calls `OnDrawUiFrame` /
`OnDrawUiViewports` / `OnDrawUiThreadSafe` only inside `if (DrawUI)`, and F2 (`InputAction.ToggleUi`,
`KSA/Input.cs`, handled `KSA/Program.cs`) flips `Program.DrawUI` (`:527`). So while the HUD is
hidden **neither StarMap GUI hook fires** and every `Update(dt)`-driven feature freezes (welds let go,
refills stop, queued game-thread work does not drain). `ksa-abstractions.lib/HiddenUiFrameHook.cs` prefixes
`Program.OnDrawUiConsole(double)` — called unconditionally at `:2201`, in the same frame phase
(after `PrepareFrame`, inside ImGui `NewFrame`…`Render`, before `OnPreRender`) — and replays the
shell's registered `UpdateSubmods` then `UpdateWelds` only when `DrawUI` is false. ImGui rendering
(`RenderWindow`, `RenderFloatingWindows`, F11) is intentionally **not** replayed so mod windows honour
the hidden HUD. `DrawUI` only flips during `Glfw.PollEvents()` in `PrepareFrame` (or from the menu bar,
drawn later), so a frame never runs both StarMap's hooks and the fallback.

`[StarMapAfterOnFrame]` (POSTFIX of `Program.OnFrame(double,double)`, `KSA/Program.cs` / OLD
`:2066`) exists in StarMap but is **not** used by the supermod shell. The shell's F11 toggle uses
`ImGui.IsKeyPressed(ImGuiKey.F11)` inside `OnAfterUi` (Brutal.ImGuiApi, not a game member).

> Risk seam: StarMap dispatch depends on the **string** method names `"OnDrawUiFrame"`,
> `"OnDrawUiViewports"`, `"OnFrame"` in `ProgramPatcher.cs`. If the game renames these, **StarMap.API**
> (not unscience) must be updated. All three are present and unchanged 4680→5402.

---

## Consolidated Harmony patches (cross-reference)

`unscience/Patcher.cs` applies/removes the following on its single `Harmony("MeowSci.Unscience")`
instance. Targets are listed at cross-reference granularity (type+member); per-class decomp deltas
live in each feature's own `scope/` file. **Two entries are owned by this area** (in **bold**) and
are fully verified below: the inlined `EternalFlamePatches` and `MenuBarPatch`.

| Patch class | Owning project | Apply (Patcher.cs) | Remove (Patcher.cs) | Primary game target(s) | Kind | Risk note |
|---|---|---|---|---|---|---|
| `HotkeyGuard` | **ksa-abstractions.lib** | Patch | Unload | `GameSettings.OnKeyAll(GlfwKeyEvent)` | prefix | verified ↓ (no delta; `GameSettings.cs` byte-identical @5402) |
| `HiddenUiFrameHook` | **ksa-abstractions.lib** | Patch | Unload | `Program.OnDrawUiConsole(double)` (**string** "OnDrawUiConsole") | prefix (no-op while `Program.DrawUI`) | string-named — verified ↓ @5402 |
| `ThugLifeRenderPatches` | thug-life.lib | Patch | Unload | `SuperMeshRenderSystem.RenderMainPass` | postfix | render pass — see thug-life scope |
| **`MenuBarPatch`** | **unscience/ (self)** | Patch | Unload | `Program.DrawProgramMenusHook()` | postfix | verified ↓ (no delta) |
| `ShinyPatches` | its-so-shiny.lib | Patch | Unload | `PartModelModule` / `PartModelDynamicModule` / `PartModelGlassModule`.`UpdateRenderData` | prefix ×3 | render — see its-so-shiny scope |
| `CameraControllerOverridePatches` | camera-controller-override.lib | Patch | Unload | `OrbitController.OnFrame` / `FlyController.OnFrame` (**string** "OnFrame") | prefix | string-named — see camera scope |
| **`EternalFlamePatches`** | **unscience/ (INLINE)** | Patch | Unload | `Universe.ExecuteNextVehicleSolvers` | prefix `Priority.First` | verified ↓ (no delta) |
| `KiwisMarblesPatches` | kiwis-marbles.lib | Patch | Unload | `Universe.ExecuteNextVehicleSolvers` | prefix `Priority.First` | sim-step timing — see celestial-and-lights scope |
| `GlassPatches` | glass.lib | Patch | Unload | `Camera.ChangeFieldOfView` / `Camera.UpdateProjection` (**string**) + field `Camera._fovRadians` (**string**) | prefix | string-named — see glass scope |
| `IFeelSeenPatches` | i-feel-seen.lib | Patch | Unload | `Vehicle.GetWorldMatrix` / `Vehicle.UpdateRenderData` (**string**) | prefix | string-named — see i-feel-seen scope |
| `VehiclePaintPatches` | humble-arteest.lib | Patch | Unload | `PartModel.AddInstance` | prefix | render — see humble-arteest scope (`IViewport` param + new `RenderPartModels` gate @5402) |
| `EngineEmissivePatches` | humble-arteest.lib | Patch | Unload | `PartModelDynamic.AddInstance` | prefix | render — see humble-arteest scope |
| `IvaForceRender` | **ksa-abstractions.lib** | Patch | Unload | `PartModel..ctor` + `PartModel.AddInstance` (see IvaForceRender ↓) | postfix ×2 | wired 2026-08-23; `IViewport` retype @5402 |
| `EditorScalePatches` | dont-stifle-me.lib | Patch | Unload | `VehicleEditor.ScaleBoundsFor` / `UpdateSelectedScale` / `QuantizeScale` | postfix/prefix | see part-editor-and-robotics scope |
| `KittenAnimationPatches` | kitten-animations.lib | Patch | Unload | `AnimatedRenderable.UpdateAnimation(double)` (**string** via `AccessTools.Method`) | prefix `(AnimatedRenderable __instance, ref double dt)` | ⚠️ **hot path** — runs for every animated renderable every frame; must stay a reference compare + early return. See character-and-materials scope |
| `PyroPatches` | pyro.lib | Patch | Unload | `Vehicle.AddVolumetricExhaustInstances` (`nameof`) | postfix | see exhaust-plumes scope |
| `GraffitiPatches` | graffiti.lib | Patch | Unload | `RenderTarget.ResolveAttachments` (`nameof`) | postfix | see decals scope |
| `HotPursuitPatches` | hot-pursuit.lib | Patch | Unload | `FixedController.OnFrame(IViewport,double)` (`nameof`) | selective prefix | skips stock math only for owned part-mounted cameras; see camera scope |
| `EditorValueLimitPatches` | dont-stifle-me.lib | Patch | Unload | `VehicleEditor.DrawParachuteSection` / `Parachute.SetDiameter` | prefixes | editor parachute bounds; see part-editor-and-robotics scope |
| `FreeFallinPatches` / `CanopyProjectionShaders` | free-fallin.lib | Patch | Unload | `ChuteRenderable.Draw`, `Utils.SetShaderFromMod`, `ShaderModuleUtils.FromFile` | prefixes | observed canopy material handles and projection shader patches; see parachutes scope |

Non-Harmony cleanup also driven by `Patcher.Unload()`: `VehiclePaint.Cleanup()` and
`EngineEmissive.Cleanup()`, both humble-arteest.lib.

Notes:
- **garrys-torch is intentionally NOT a Harmony patch.** Its weld physics runs from
  `Mod.cs` (`OnAfterUi`) → `UpdateWelds` (`:162-168`) via `GarrysTorchSubmod.UpdateWelds(dt)`, which internally calls
  `JobSystems.VehicleSolver.Wait()` before touching vehicle state (avoids the worker-iteration race).
- `IFeelSeenPatches.Apply` takes a second argument (`IFeelSeenTracker`, wired at `Mod.cs`).
- `CameraControllerOverridePatches.SequencePlayer` and `MenuBarPatch.ToggleWindow` are wired before
  Apply (Patcher.cs, 54).
- `KittenAnimationPatches.Driver` is bound during feature Initialize, before host patch installation. The camera live player and menu callback are also wired before Patch.

### `MenuBarPatch` (unscience/MenuBarPatch.cs) — owned by this area

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `MenuBarPatch.cs` (`[HarmonyPatch]`), applied `:15`, removed `:21-24` | `Program.DrawProgramMenusHook()` — `public void DrawProgramMenusHook()` (empty hook) | `KSA/Program.cs` (called from `DrawMenuBar` at `:3863`) | Yes | None — identical empty instance method (OLD `:3736`) | Game ships this as a deliberate no-op modding hook. Postfix appends an "Unscience" `ImGui.MenuItem`. Low risk. |

### `EternalFlamePatches` (inlined in unscience/Patcher.cs) — owned by this area

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony prefix (`Priority.First`) | `Patcher.cs` (lookup), `:156` (patch), `:159-165` (remove) | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep)` — `public static void` | `KSA/Universe.cs` (`SimStep` = `KSA/SimStep.cs`, readonly struct) | Yes | None — identical sig and body (OLD `:1767`); still the only overload | Looked up by name only (`nameof`, no param-type array), so a param change would NOT break the lookup unless the method became overloaded. Prefix dispatches to `EternalFlameSubmod.Instance?.UpdateBeforeVehicleSolvers()`, wrapped in try/catch. Kiwi patches the same target. |

---

## `ksa-abstractions.lib` — per-helper integration points

Decomp paths relative to NEW decomp root. All confirmed present in NEW; OLD line noted only where useful.

### VehicleProvider.cs

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Direct API (prop) | `VehicleProvider.cs` | `Program.ControlledVehicle` — `public static Vehicle? ControlledVehicle { get; set; }` (setter calls `_controlledVehicle?.ClearHeldPlayerInput()`) | `KSA/Program.cs` | Yes | None (OLD `:480`; already a property, not a field, at 5348) | Returned as-is from `GetControlledVehicle()`; compile-bound so field→property was harmless. |
| 2 | Direct API (prop) | `:15` | `Universe.CurrentSystem` — `public static CelestialSystem? CurrentSystem { get; private set; }` | `KSA/Universe.cs` | Yes | None (OLD `:94`) | Null-safe (`?.`). |
| 3 | Direct API (prop) | `:15` | `CelestialSystem.All` — `public LookupCollection<Astronomical> All => _all;` | `KSA/CelestialSystem.cs` | Yes | None (OLD `:57`) | |
| 4 | Direct API (method) | `:15` | `LookupCollection<Astronomical>.UnsafeAsList()` — `public List<T> UnsafeAsList()` | `KSA/LookupCollection.cs` | Yes | None (file byte-identical) | Then LINQ `OfType<Vehicle>()`. |
| 5 | Direct API (type) | `:11,21,29` | `Vehicle` — `public class Vehicle : Astronomical, …, IObjectId, …` | `KSA/Vehicle.cs` | Yes | None | |
| 5b | Direct API (prop) | `:24` | `Vehicle.IsDebris` — `public bool IsDebris { get; private set; }` | `KSA/Vehicle.cs` | Yes | **NEW @5402** (absent in OLD) | Set by `Vehicle.MarkAsDebris()` from `PartFailure` (`KSA/PartFailure.cs`). `GetAllVehicles(bool includeDebris = false)` filters on it so shed fragments stay out of every mod's picker; `FindVehicle` and the two callers that must see everything pass `true`. |
| 6 | Direct API (prop) | `:22` | `Vehicle.Id` (inherited `Astronomical.Id` via `IObjectId`) — `public virtual string Id { get; protected set; }` | `KSA/Astronomical.cs` | Yes | None (OLD `:104`) | `Id` is not declared on `Vehicle`; resolved through base `Astronomical`/`IObjectId`. |

`unscience/Patcher.cs` applies/removes IvaForceRender. The policy inspector manages the active override, including existing and newly created PartModel instances. Kitchen Sink Flexo panels and solver patch are removed.

### KsaPaths.cs

| # | Kind | Mod code | Game target | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|
| 1 | OS path | `KsaPaths.cs` | **none** — `MyDocuments\My Games\Kitten Space Agency` | n/a | n/a | No game API. Breaks only if the game changes its user-data folder name. |

Update-risk findings (4680→4750): **No breaking deltas detected.**

### SubmodUI.cs

| # | Kind | Mod code | Game target | Decomp path | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | ImGui API | `SubmodUI.cs` | Brutal.ImGuiApi only — `PushStyleVar(WindowPadding)`, `BeginChild(AutoResizeY\|AlwaysUseWindowPadding, NoScrollbar)`, `PopStyleVar`, `Dummy`, `EndChild` | `Brutal.ImGuiApi/*` | Yes | None observed | No KSA game internals. See Brutal-package note. |

Update-risk findings (4680→4750): **No breaking deltas detected** (compiles against 4750 Brutal).

---

## Workspace persistence and exact selections

`WorkspaceWindow` owns navigation, visibility, placements and the three menu flows. `WorkspaceDialogs` implements name/collision handling and reusable target-preserving feature presets. `LiveStateWindow` enumerates `ILiveStateItem` providers; rendering it never captures authoring values from the game automatically. `UnscienceState` retains only preferences and legacy migration. Storage/recovery details are in [WORKSPACE.md](../docs/WORKSPACE.md).

`PartIdentity` integrates `Vehicle.Id`, `Vehicle.Parts.Parts`, `Part.Id`, `Part.Template.Id`, `Part.SubParts`, `Part.InstanceId` and `ImGui.GetFrameCount()`. It caches a vehicle-topology hash plus root/subpart path per GUI frame. KSA constructs `Part.InstanceId` using `Universe.GetNextRunningId`; `PartInstance.GlobalInstanceId` is `[XmlIgnore]` (see current decomp `KSA/Part.cs` and `KSA/PartInstance.cs`). Therefore neither is serialized as a durable exact target. Changed topology remains unresolved; editor parts are session-only. No new reflection or Harmony seam is used for identity. Recheck tree ordering and ID persistence on game updates.

`DraftChoice` persists option identities, resolving vehicle choices through `VehicleProvider`; `$controlled` explicitly resolves its current controlled vehicle. A missing exact choice sets the backing index to -1 and disables dependent actions. `LiveIdentity` supplies process-only object identities for runtime records and does not serialize references. `FormGrid` uses responsive one/two-column tables with padded cells; `FormField` places wrapped labels above full-width inputs. Native ImGui calls in the new shell include BeginChild, tables, BeginPopupModal, BeginCombo, Selectable/IsMouseDoubleClicked and window position/size/scroll access; use the game-shipped Brutal wrapper.

All 25 features implement `CaptureDraft`, `PrepareRestore`, `Draft`, and `GetLiveItems` in addition to `ISubmod`. GPU resources, jobs, callbacks, game objects and live dictionaries must never enter the draft JSON. Hidden-HUD callbacks and HotkeyGuard remain required. The per-feature scope pages describe each adapter and its applied state.

## Historical evidence

See [dated integration and upgrade reference](history/00-architecture-and-abstractions.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.
