# KSA `2026.8.22.5348` upgrade — impact review & remediation record

**Reviewed:** 2026-08-23 · **Span:** `2026.8.19.5261` (recorded baseline) → `2026.8.22.5348` (87 revs)
**Trees:** NEW `ksa-game-assemblies` @ `2026.8.22.5348`; OLD `ksa-game-assemblies_prev` @ `2026.8.19.5261`
**Host:** macOS. `Directory.Build.props` tier 2 resolves `KSAFolder` to
`../ksa-game-assemblies/current/dll/` — **the build compiled against NEW (5348)**, not a separate
install. No `KSAFolder` trap this pass.

> ✅ **Changelog coverage is complete and this is a clean single hop.** `<BASELINE>` (`5261`) ==
> `<OLD>.build` == NEW's `fromRevision`, so NEW's `version.json` alone covers the whole span
> (revs **5262–5348**, 175 changelog lines). No revision went unreviewed.

---

## 1. Result

`dotnet build ksa-mod-experiments.slnx` → **55/55 projects, 0 warnings, 0 errors.**

**One compile break, in one project — `space-tape.lib`.** It was resolved by **removing space-tape
entirely** (see §2), at the requester's direction; the mod is defunct.

Everything else in the suite is compile-clean and every string-reflection, Harmony-signature,
byte-layout and shader-anchor check passes. The residual risk this pass is **behavioral**, and it is
concentrated in four places (§4).

| # | Mod | Change class | New in 5348? | Verdict |
|---|---|---|---|---|
| 1 | `space-tape.lib` | **removed member** (`PartTemplate.Decoupler`) | ✅ yes (rev 5329) | **mod removed** |
| 2 | con-man | semantic drift (`GameSettings.GetGaugeScale()`) | ✅ yes (rev 5293) | needs live re-verification |
| 3 | kitten-animations | new-gating (per-frame pose guard) | ✅ yes (rev 5278) | needs live re-verification |
| 4 | thug-life | render-env change (Vulkan 1.4, UI coverage culling) | ✅ yes (revs 5315, 5283) | needs live re-verification |
| 5 | blinky / its-so-shiny | semantic drift — **favourable** (power graph now on-demand) | ✅ yes (rev 5326) | no change needed |

---

## 2. space-tape removed

Rev **5329** ("Sequencing has moved from parts down to modules") deleted `KSA.DecouplerTemplate` and
the `PartTemplate.Decoupler` field. Decouplers are now a **module**: `KSA.Decoupler.TemplateData`
(`[XmlType(TypeName = "Decoupler")]`) living in `PartTemplate.Components`, alongside the new
`ISequenced` / `IPartParent` / `IRescale` / `IInertMass` interfaces.

That broke `space-tape.lib/PartImporter.cs:124,128,129` with three × `CS1061`. Since space-tape is
defunct, the whole mod was removed rather than ported:

- `git rm -r space-tape space-tape.lib`
- `ksa-mod-experiments.slnx` — both project entries dropped (**57 → 55 projects**)
- `unscience/unscience.csproj` — `ProjectReference` to `space-tape.lib` dropped
- `unscience/Mod.cs` — `using MeowSci.SpaceTapeLib;`, `_submods.Add(new SpaceTapeSubmod())` and the
  `SpaceTapeSubmod.HideHostWindow` wiring dropped. **The supermod now bundles 22 submods, not 23.**
- `scope/part-editor-and-robotics.md`, `scope/game-integration-surface.md`, `scope/FULL_SCOPE.md`,
  `scope/00-architecture-and-abstractions.md`, `REPOSITORY_INDEX.md` — space-tape rows/sections removed

> ℹ️ **The on-disk XML did not change.** `<Decoupler ConnectorId="…" Force="…" />` still serializes
> identically (the nested `TemplateData` keeps `[XmlType(TypeName = "Decoupler")]`). Only the *typed
> reader* — walking `PartTemplate.Decoupler` — broke. Noted in case a future part tool needs it: the
> replacement read is `template.Components.OfType<Decoupler.TemplateData>()`, and `ConnectorId`/`Force`
> are now **fields**, not properties.
>
> ⚠️ **Stale deploy folder.** `~/repos/meow-sci/mods/mods/space-tape/` still holds the last built DLL
> and will keep loading into the game until deleted. Not removed here — it lives outside the repo.

---

## 3. Changelog delta (revs 5262–5348), filtered to unscience coupling

| Rev(s) | Change | Mods at risk | Outcome |
|---|---|---|---|
| **5329** | Sequencing moved parts → modules; `ISequenced`/`IPartParent`/`IRescale`/`IInertMass`; `DecouplerTemplate` deleted; duplicate-module-id warnings; editor scaling triaxial → uniform (0.5×–2×) | space-tape, blinky, its-so-shiny, flexo | **compile break** (space-tape, removed). Others clean — see §5 |
| **5326** | Vehicle power reworked onto `ElectricalCircuits`; `PowerManager.PopulateGraph` is now **on-demand only** | blinky, its-so-shiny, eternal-flame, red-alert | ✅ clean, and a **performance win** — see §4.4 |
| **5301** | Clustered lighting rewrite; `ViewportLightModes`; `PartModelShadowCull`; portrait lights generalised | zippo, red-alert, its-so-shiny, doh, humble-arteest, thug-life | ✅ clean — see §5 |
| **5293** | **Hud Scale setting** — a global scalar applied after player scale and HUD layouts | **con-man** | ⚠️ behavioral — see §4.1 |
| **5277** | Font Size setting **removed**; Interface Scale setting added; `ConsoleStyle` windows now scale with it | skittles, con-man | ⚠️ watch — see §4.1 |
| **5278** | Crew/EVA animation moved from once-per-viewport to **once-per-frame** (pose guard) | **kitten-animations**, doh | ⚠️ behavioral — see §4.2 |
| **5315** | **Vulkan 1.3 → 1.4** | **thug-life** | ⚠️ live check — see §4.3 |
| **5283** | UI coverage culling — expensive shaders skipped behind opaque UI | **thug-life**, blinky, its-so-shiny | ⚠️ live check — see §4.3 |
| **5280** | CCF/CCI/CCE quaternion composition refactored into `CelestialFrameMath` | garrys-torch, kiwis-marbles, thug-life, camera-controller-override, flexo, doh | ✅ clean — pure extraction, see §5 |
| **5331, 5339, 5274** | Physics-bubble merge/split rewrite; bubble ownership moved into `VehicleUpdateTask` | eternal-flame, garrys-torch, flexo, kitchen-sink | ✅ clean — see §5 |
| **5332** | Save/Load menu entry hidden while the vehicle editor is open | marque, jplrepo, unscience `MenuBarPatch` | ✅ clean — see §5 |
| **5265** | `ImGuiHelper` functions now require an explicit draw list; overlay draws consolidated | marque, IOrbiter overlays | ✅ clean (game-internal) |
| **5340** | Part characteristics now computed by instantiating a real `Part`; **every part instantiated at load** (`PartArchetypes.WarnOnMalformedParts`) | parts-now, average-twr | ⚠️ watch — see §4.5 |
| **5269** | MMU fold-away anim — `CharacterAvatar.Attachments.Mmu.MmuMesh` retyped `StaticMeshRenderable` → `AnimatedRenderable` | **doh** | ✅ clean — doh's walker is type-agnostic, see §5 |
| **5317, 5318, 5333** | TVC gain fix; sequence-0 zeroing delta-v/TWR fixed; engine-deactivate-mid-burn fixed | average-twr, geeforce, blinky | ✅ clean (game bug fixes; average-twr's readings may shift) |
| **5312, 5308** | Raytracing for IVA kittens; multi-viewport raytrace fix | doh, kitchen-sink (IVA force render), humble-arteest | ⚠️ live check |
| 5263–5264, 5274, 5287–5289, 5303–5307, 5342–5346 | Ground clutter (collisions, exclusion masks, destruction, distribution) | — | n/a — no unscience coupling |
| 5319–5325 | Terrain precision rework, `PrecisionFuncs.glsl`, `AnchoredNoise.glsl` | — | n/a — no unscience coupling |
| 5328, 5330, 5334–5337 | Static objects + launchpad models; `StaticObject.vert/.frag`; decals | — | n/a. `LaunchPad` GltfFile removed from `DefaultAssets.xml` — **not referenced by any mod** |
| 5281, 5291 | GPU profiler | — | n/a |

---

## 4. Findings requiring attention

### 4.1 con-man — HUD Scale is a second scalar con-man doesn't know about ⚠️

**New in 5348** (rev 5293). Change class: **semantic drift**. Verdict: **needs live re-verification**;
a code change is likely but should not be made blind.

All seven private `GaugeCanvas` fields still resolve — declarations are **byte-identical and at the
same line numbers** in both trees (`KSA/GaugeCanvas.cs:92,115,130,132,134,136,143`), and `_windowTitle`
is still `protected` on `GaugeCanvas` itself. The reflection is fine. The **arithmetic around it is not**:

| `KSA/GaugeCanvas.cs` | 5261 | 5348 |
|---|---|---|
| `:534` → `:536` | `ScreenReference.PixelsToUv(pixelsSize)` | `PixelsToUv(pixelsSize / GameSettings.GetGaugeScale())` |
| `:815` → `:817` | `SetNextWindowSizeConstraints(2f, 2048f, …)` | `… 2048f * MathF.Max(1f, GameSettings.GetGaugeScale()), …` |
| `:856` → `:859-866` | — | new `ConsoleStyle.BeginGaugeHostScope(GetGaugeScale() * clamp(ContentScale, 0.6, 3))` around the draw |

con-man captures and restores `_windowPosition` / `_windowSize` / `_customScale` / `_customOffset`
(`con-man.lib/GaugeStateAccessor.cs:28-34`, arithmetic documented at `LayoutManager.cs:151-152`).
With a global HUD scale multiplying on top, **layouts saved at one Hud Scale will restore at the wrong
size and position at another**. Layouts saved and restored at the same Hud Scale should be unaffected.

Also new and worth checking together:
- `RecalculateAll()` → `RecalculateAll(bool forceReattach = false)` — optional param, source-compatible;
  con-man does not call it.
- `Detached = false` is now reset in the reattach path (`:954`).
- The crew-portraits canvas gained a third gate: `GameSettings.ShowCrewPortraitCameras()` (rev 5276),
  on top of the rev-5201 `IsContextVisible()` gate already recorded as **open** from the 5261 pass.
- Rev 5277 removed `GameSettings.Interface.FontSize` outright and redefined
  `GetInterfaceScale()` from `FontSize / 20f` to a dedicated 50–200 % `Interface.Scale`.
  con-man reads neither directly, but its layouts are in the affected coordinate space.

**Recommended (not applied):** have `LayoutManager` record `GameSettings.GetGaugeScale()` alongside
each saved layout and normalise on restore. Confirm the approach before implementing.

### 4.2 kitten-animations — the expression cache-bust may now be a no-op ⚠️

**New in 5348** (rev 5278: *"Fixed seated crew and EVA crew animation updating once per visible
viewport instead of once per frame"*). Change class: **new-gating**. Verdict: **needs live
re-verification**.

`KSA/AnimatedRenderable.cs` gained `private ulong _lastPoseUpdateFrameNumber = ulong.MaxValue;` and
the pose path is now guarded by `if (Program.FrameNumber != _lastPoseUpdateFrameNumber)`. Previously
the guard was `if (!FreezeAnimation)`.

kitten-animations busts `CatExpressionAnim._expressionPose` to force a re-pose. `CatExpressionAnim` is
**byte-identical** between trees and `_expressionPose` still resolves — but a forced second pose
evaluation **within the same frame is now dropped**. This is a live candidate explanation for the
long-standing [`ISSUES.md`](../ISSUES.md) entry *"kitten animations don't properly play each one,
always the same"*, which no previous pass could explain.

Related and additive (not breaks): revs 5268 (seated idle + fidget), 5269 (MMU fold-away), 5284
(low-gravity walk/run), 5314 (swimming + `KittenLocomotion` swim state).

### 4.3 thug-life — new render environment, statically unverifiable ⚠️

**New in 5348.** Change class: **render-environment**. Verdict: **needs live in-game re-verification.**

Everything thug-life binds to is intact:
- `SuperMeshRenderSystem.RenderMainPass(CommandBuffer)` — signature unchanged.
- `Program.OffscreenTarget` — unchanged (`KSA/Program.cs:438`), so the 5261 dynamic-rendering rebuild
  still applies.
- `UnlitMesh.vert` / `UnlitMesh.frag` — **byte-identical**; `UnlitMeshVert`/`UnlitMeshFrag` ids still
  in `Content/Core/DefaultAssets.xml`.

Two environment changes cannot be cleared from source:
1. **Rev 5315 — Vulkan 1.3 → 1.4.** thug-life builds its own pipeline, descriptor set, VB/IB and
   texture upload against the game's device. 1.4 is backward compatible, but the mod's own
   `Brutal.VulkanApi` usage should be exercised once in game.
2. **Rev 5283 — UI coverage culling** (`Content/Core/Shaders/UiCoverage/*`, seven new shader ids in
   `DefaultAssets.xml`, and `GaugeCanvas.RegisterOpaqueCoverage`). Expensive shaders are skipped
   behind opaque UI. thug-life's quad is drawn as a **postfix on the main pass** and does not register
   coverage — it should be unaffected, but a mis-culled or z-fighting quad would only show in game.

### 4.4 blinky / its-so-shiny — the O(N³) power DFS is gone (favourable) ✅

**New in 5348** (rev 5326). Change class: **semantic drift, favourable**. Verdict: **no code change
needed**; both mods keep working, and an optimisation they carry is now dead weight.

`PowerManager.PopulateGraph(_part.FullPart, 0uL, null)` moved out of the constructor
(`KSA/PowerManager.cs:14` in 5261) into `OnDrawUi`, behind `if (base.ShowFlow && !_displayGraphBuilt)`
(`:130-138` in 5348). The graph is now built **only when "Draw Graph" is ticked in the part window**.
Power itself runs off the new `PartTree.ElectricalCircuits` (built on demand, invalidated by
`MarkDirty()` on derived-data/resource-manager rebuilds). The changelog reports a 4500-consumer craft
going from 3.3 s to ~0.3 ms per rebuild.

- `blinky.lib/LcdGridBuilder.cs:319` splits grids specifically *"to reduce ResourceManager.PopulateGraph
  cost from O(N³) to O(N³/K²)"*, and `:62`/`:114` reason about the same DFS.
- `its-so-shiny.lib/ShinyGridBuilder.cs:42` places *"distinct battery anchors [for] the per-PowerConsumer
  DFS in PowerManager.PopulateGraph"*.

Both are now solving a problem the game no longer has. **Not simplified in this pass** — it is a
behavioral change to grid construction and belongs in its own task. `ResourceManagerBase.PopulateGraph`
and the `NearestToFurtherestNode` / `…SameStage` fields blinky's diagnostics read are all still present.

Counterweight: `Part.Modules` is now `new ModuleList(keepModuleIdIndex: true)` for **every** part, so
blinky's thousands of pixel parts each carry an id index. Worth a perf glance in game.

### 4.5 parts-now — every part is now instantiated at load ⚠️

**New in 5348** (rev 5340). Change class: **new-gating**. Verdict: **watch; needs live verification.**

`Program.cs:1212-1215` now runs `PartArchetypes.WarnOnMalformedParts()` inside a
`Loading.Task("Part Validation")`, which constructs a real `Part` from **every** non-subpart template
in `ModLibrary.AllParts` and calls `Tree.ReinitializeDerivedValues()`, logging any exception.
Rev 5329 additionally added `PartTemplate.WarnOnDuplicateModuleIds()`.

The registration-ordering invariant still holds — `ModLibrary.Bind(_renderer)` is at `Program.cs:942`,
well before the validation pass at `:1214`, and parts-now registers from `[StarMapAllModsLoaded]`
before that. So parts registered by parts-now **will** be instantiated and validated at load. Expect
new load-time warnings for any generated part that was previously only latently malformed. Not an
error path for the mod itself.

> `parts-now.lib/Runtime/MeshBudget.cs:23,177` cite `ModLibrary.Bind()` at `Program.cs:985`; the call
> is now at `:942`. Comment-only staleness — the ordering is unchanged.

---

## 5. Verified clean against 5348

Nothing below needs action; recorded so the next pass doesn't re-derive it.

**Build.** 55/55 projects, 0 warnings, 0 errors, compiled against the 5348 reference DLLs
(`ksa-game-assemblies/current/dll/`, 38 assemblies). No `TreatWarningsAsErrors` nullability fallout —
no Brutal/ImGui surface used by the suite changed shape.

**The entire string-reflection watchlist resolves**, re-grepped in full against both trees:
`Camera._fovRadians` · `Camera.ChangeFieldOfView` / `UpdateProjection` · `Vehicle.GetWorldMatrix` /
`UpdateRenderData` · `KittenEva` (type name) → `_renderable` → `_characterAvatar` →
`CharacterAvatar.Core` → `CharacterCore.Scale` · `CatExpressionAnim._expressionPose` ·
`"KSA.LightModule+TemplateData"` + `Components` + `Intensity` + `ColorRgb` + `ColorRgbReference.{R,G,B,
OnDataLoad}` · all seven `GaugeCanvas` fields · the doh/humble-arteest render bridge
(`Program.{Instance,MaterialSystem,SuperMeshRenderSystem}`, `GpuObjectSystem.{BigBuffer,DeviceCtx}`,
`AssetManager.AssetMap`, `BindlessHandle`, `Handle`) · `ShaderReference.{Shader,DoLoad,ModPath}` +
`ShaderModuleUtils.FromFile` · all seven `ModLibrary.All*` registries ·
`SerializedCollection<T>._collection` · `VehicleEditor._editorTagLookup` ·
`PartTree.RecomputeStaticMass` · `ResourceManagerBase.NearestToFurtherestNode(SameStage)` ·
`GameSettings.OnKeyAll` · `Universe.ExecuteNextVehicleSolvers` · `Part.ResetCachedPosMatrixValues` ·
`Situation` enum names.

> **Field-vs-property audit.** Rev 5329 moved `Parent` from a `Module<T>` **field** to a
> `ModuleBase.Parent` **auto-property** (and `ModuleBase` now implements `IPartParent`). This would
> silently break any `GetField("Parent")` — `ksa-abstractions.lib/ReflectionHelpers` has no property
> fallback. **No mod reflects on `Parent`.** `CharacterAvatar.Core` and `CharacterCore.Scale` both
> remain plain **fields**, so garrys-torch's `SetValue` path is intact.

**Every Harmony patch-target signature is unchanged** (line shifts only), verified across both trees:
`Universe.ExecuteNextVehicleSolvers(double, SimStep)` (still one overload) ·
`SuperMeshRenderSystem.RenderMainPass(CommandBuffer)` · `PartModel/PartModelDynamic/PartModelGlass
.AddInstance(PerInstanceData, Viewport, int)` · the three `*Module.UpdateRenderData(in double4x4, bool,
Viewport, int)` · `PartModelRenderer.UpdateRenderData(Viewport, int)` (the explicit param array flexo
uses still resolves uniquely — a new 3-arg `UpdateRenderData(Viewport, int, ref readonly double4x4)`
overload appeared elsewhere but not on `PartModelRenderer`) · `Program.DrawProgramMenusHook` ·
`Program.DrawMenuBar(Viewport, int)` · `GaugeCanvas.OnDrawMenuBar` · `GameSettings.OnKeyAll` ·
`OrbitController`/`FlyController.OnFrame` · `Camera.ChangeFieldOfView` · `Vehicle.Teleport` /
`RefillConsumables` · `Battery.Refill(ref BatteryState)` · `EngineController.SetIsActive(Vehicle?, bool)` ·
`PartTree.CreateFromNewPartTree(Part)` · `Celestial.SetOrbit(Orbit)` ·
`ShaderModuleUtils.FromFile(Device, string, out VkShaderStageFlags, CompileOptions?)`.

**jplrepo's IL transpiler still matches.** It injects before the first `ImGui.SetCursorPosY` in
`Program.DrawMenuBar`. Rev 5332 rewrote that method (Save/Load hidden while the editor is open), but
the version-string block is untouched: `DrawProgramMenusHook()` → `SetCursorPosY` → `SetCursorPosX` →
`EndMenuBar`, still exactly one `SetCursorPosY`, at `Program.cs:509→512`.

**GPU byte layouts identical** — `PartModel.PerInstanceData`, `PartModelDynamic.PerInstanceData` and
`MaterialData` all diff clean. humble-arteest's padding-byte hijack (bytes 68–79) and doh's
`handle*80+16` `BigBuffer` writes are safe.

**Shaders.** `UnlitMesh.vert`/`.frag` and `MeshIndirect.vert` are **byte-identical**.
`MeshIndirect.frag` changed by exactly one line (`SamplePortraitLight` → `SampleMeshForwardLights`,
rev 5301) — humble-arteest's `vec3 sampledColor` anchor is still at `:114` and the
`ENABLE_TEMPERATURE` LUT still lives in that file, so **Engine Emissive is unaffected**.
`ModelPbr.frag` gained a `RAYTRACED_REFLECTIONS` variant and the same portrait-light rename; the
`albedo` path doh and Kitten Color depend on is unchanged, and `Common/MaterialSet.glsl` is untouched.

**Assets.** Every id referenced by a surviving mod is still present and unchanged vs OLD:
`UnlitMeshVert`/`UnlitMeshFrag`, `MeshIndirectVert`/`MeshIndirectFrag`, `CorePropulsionA_Prefab_EngineA2…A6`,
`LightPart` (+ its `<PowerConsumer LightSwitch="true">`), `KittenBackPackPart`, `FurNoise`,
`MixtureReaction Id="MMH_NTO"`. `DefaultAssets.xml` **removed** the `LaunchPad` GltfFile (rev 5328) and
**added** `StaticObject*`, seven `UiMask*` and `LightEvalStats` shaders — none referenced by unscience.

**Coordinate frames unchanged.** Rev 5280 extracted `CelestialFrameMath.ComputeCcf2Cci` /
`ComposeCcf2Cce`, but `Celestial.GetCcf2Cci` / `GetCci2Ccf` / `GetCci2Cce` / `GetCce2Cci` keep the same
signatures and semantics — a pure extraction. garrys-torch, kiwis-marbles, flexo, doh, thug-life and
camera-controller-override are unaffected. `KinematicMeasurements` is **byte-identical** (geeforce
clean). `Camera.cs` is **byte-identical** (glass clean).

**The shared provider chokepoint is intact** — `Universe.CurrentSystem` (`CelestialSystem?`, private
setter) → `CelestialSystem.All` (`LookupCollection<Astronomical>`) → `LookupCollection<T>.UnsafeAsList()`,
all unchanged. Every feature mod's UI reaches vehicles and celestials through this.

**Physics-bubble rewrite does not move the eternal-flame seam.** `Universe.ExecuteNextVehicleSolvers`
has a substantially rewritten body (bubble ownership moved into `VehicleUpdateTask`; merge checks
multithreaded — revs 5331/5339), but the signature is unchanged and the prefix still runs before
`JobSystems.VehicleSolver.ExecuteJobs()`. `JobSystems.VehicleSolver` (the name garrys-torch drains on,
`GarrysTorchSubmod.cs:103`) still exists with `VehicleWorkerPool` alongside it. `Vehicle.Teleport`,
`Vehicle.RefillConsumables`, `Battery.cs` (byte-identical) and `Vehicle.IsControllable` are all unchanged.

**Lights clean.** `LightModule.cs` diffs only by decompiler cosmetics (`Parent` → `base.Parent`, from
the `IPartParent` split) plus one lighting-registration change — lights now register for **all**
viewports rather than only `Program.MainViewport` (rev 5301 `ViewportLightModes`). `SolarPanel.cs`,
`KeyframeAnimationModule.cs` and `PowerConsumer.cs` are cosmetics-only. `LightModule.TemplateData`'s
field set (`Intensity`, `ColorRgb`, `Range`, `Type`, …) is unchanged.

**doh's MMU walk survives a retype.** Rev 5269 changed `CharacterAvatar.Attachments.Mmu.MmuMesh` from
`StaticMeshRenderable` to `AnimatedRenderable`. `doh.lib/Spawning/KittenSpawner.cs:542-556` walks by
**field name** and then finds `MaterialIndices` anywhere in the runtime type hierarchy, and
`AnimatedRenderable` declares `protected readonly int[] MaterialIndices` just as
`StaticMeshRenderable` does. No change needed.

**`Part`'s API churned but missed the suite.** Rev 5329 **removed** `Part.Sequence`, `Part.SetSequence(int)`,
`Part.ActivateInStage(Vehicle?)`, `Part.DeactivateInStage(Vehicle?)` and `Part.ScaleTotal`, and added
`ActivateSubtreeInStage(Vehicle?, int)`, `SetAllSubtreeModulesSequence`, `SetSubtreeModulesSequence`,
`ShiftSubtreeModulesSequence`, `CountEnabledSubtreeSequencedModules`, `HasSubtreeSequencedModule`,
`GetSubtreeSequencedModules`, `RefreshScale`, `RefreshScaleAndReposition`, `RefreshTankContents`,
`FindSurfaceMountPointPartAsmb`. **No unscience mod referenced any of the removed members** (confirmed
by the green build and by grep). `PartTree` gained public `RefreshStaticMass()` — flexo and kitchen-sink
still `Traverse.Method("RecomputeStaticMass")` on the private one, which is still there; switching to
the public wrapper is an available simplification, not a fix.

**Menu bar clean.** `Program.DrawProgramMenusHook()` and `GaugeCanvas.OnDrawMenuBar()` are unchanged,
so unscience's `MenuBarPatch`, marque's prefix and jplrepo's prefix all still attach. Rev 5332 only
wraps the Save/Load `MenuItem` in `if (!IsEditorOpen)`.

**Suite load path intact** — `Program.OnDrawUiFrame` / `OnFrame` / `DrawProgramMenusHook` all present,
so StarMap keeps its seams.

---

## 6. Known-broken reconciliation

| Item | Status at 5348 |
|---|---|
| **camera-controller-override `___Transform` injector** | ✅ **Closed.** Fixed at 5261; the prefix reads `__instance.Camera` (`CameraControllerOverridePatches.cs:42-54`). `OrbitController`/`FlyController.OnFrame` still resolve. |
| **zippo `GetField("Color")`** | ✅ **Closed — the scope docs were stale.** The code reads `"ColorRgb"` (`zippo.lib/LightController.cs:59,80`), which is the real field name (`KSA/LightModule.cs` → `TemplateData.ColorRgb`). There is no `GetField("Color")` anywhere in the repo. Fixed by commit `07787ea`; §4/§6 of the master index still described it as broken and have been corrected. |
| **humble-arteest Vehicle Paint** | ❌ Still dead by design (rev 4693 `CompileVariantWithCustomOptions`). Self-disables. Anchors still resolve. Unchanged by 5348. |
| **mesh-deform** | ❌ Still broken, **confirmed at 5348 and unchanged from 5261**: `MeshIndirect.vert` is byte-identical, and its struct anchor `"    uint EmissiveColor;\n};"` still does not match — the file has `uint EmissiveColor;` followed by `#endif` (`:16-17`). Self-disables. Its world-position anchor (`:63`) does still match. |
| **space-tape API drift cluster** | ✅ **Closed by removal** (§2). |
| **garrys-torch CS8604 (rev-4729 Brutal nullability)** | ✅ Closed — build is warning-free. |
| **unscience supermod never wires `IvaForceRender.Patch`** | ❌ Still open — unchanged by this build. kitchen-sink's IVA force-render remains partial inside the supermod. |
| **`Vehicle.IsControllable` gating (4699)** | ✅ Unchanged at 5348 (`Vehicle.cs:582`). |
| **Editor tag/category schema; face-snapping; part-size XML** | These were space-tape watch items. **Moot** — mod removed. Rev 5329 did change editor scaling (triaxial → uniform, clamped 0.5×–2×), which now affects only flexo/parts-now. |
| **blinky default `EnginePartId = "CorePropulsionA_Prefab_EngineA1"`** | ❌ Still open. `BlinkySubmod.cs:35` was moved to `EngineA3`, but `LcdGridConfig.cs:47` — the persisted default — is **still `EngineA1`**, an id that does not exist in Content at 5261 or 5348. `ModLibrary.Get` throws. Still the best candidate for *"blinky broken"* in `ISSUES.md`. |

---

## 7. What still needs a live in-game pass

A green `dotnet build` is a small fraction of the risk here, and **there is no test suite in this
repo** — `dotnet build` plus a live session is the whole verification story.

1. **con-man** — save a layout, change **Hud Scale**, reload the layout (§4.1).
2. **kitten-animations** — cycle expressions and confirm they still change (§4.2). This is the first
   pass with a concrete mechanism for the standing `ISSUES.md` complaint.
3. **thug-life** — F12, confirm the quad draws correctly under Vulkan 1.4 and is not culled by the new
   UI coverage pass (§4.3).
4. **blinky** — build a grid and watch load/rebuild timing now that the power DFS is on-demand (§4.4);
   separately, fix the `LcdGridConfig` default engine id (§6).
5. **parts-now** — watch the new `Loading.Task("Part Validation")` output at startup for warnings on
   generated parts (§4.5).
6. **doh** — confirm the MMU attachment still recolours after the `AnimatedRenderable` retype, and that
   IVA kitten raytracing (rev 5312) doesn't disturb material cloning.
7. **its-so-shiny / red-alert / zippo** — lights now register for all viewports (rev 5301); confirm no
   double-lighting in crew-portrait viewports.
8. **eternal-flame / garrys-torch / flexo** — re-check the `ISSUES.md` error spam under the rewritten
   physics-bubble model (revs 5331/5339).

Fastest route: build, launch KSA, open the unscience window (**F11** — 22 submods load through it),
then exercise the specific mods above. unladen-swallow's HTTP endpoints (`0.0.0.0:7887`) can drive
blinky / its-so-shiny / glass / camera / torch without UI clicking.
