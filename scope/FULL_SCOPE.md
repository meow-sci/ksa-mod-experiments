# Unscience — Full Game-Integration Scope

This folder is the **authoritative reference for how the unscience mod suite plugs into the
Kitten Space Agency (KSA) game**. Its purpose is singular: when KSA ships an update, this is the
first place to look to decide *what might break and where*. Every unscience feature is mapped to the
exact game types, methods, fields, Harmony patch targets, shaders and assets it depends on, with the
decompiled-source path for each so the dependency can be re-checked against any future build.

> Keep this in sync. Any change to an unscience feature's game integration **MUST** update the
> relevant file here. See [`../AGENTS.md`](../AGENTS.md) → "scope/ maintenance" for the binding rule.

---

## Version baseline

- **Cataloged against:** KSA build **`2026.8.19.5261`** (2026-08-11) — decomp at
  `…/ksa-game-assemblies/current/decomp`, assets at `…/ksa-game-assemblies/current/Content`.
  Confirmed identical to the live install (`C:\Program Files\Kitten Space Agency\KSA.dll`), so
  `dotnet build` compiled against this build.
- **Diffed from:** KSA build **`2026.8.3.5117`** — the previously verified baseline. The intermediate
  build `2026.8.5.5168` is on disk as `ksa-game-assemblies_prev` and was used for OLD/NEW diffing,
  but **was never itself validated**, so the honest span is **5117 → 5261** (~140 revisions).
  Two compile breaks found this pass in fact originate in the unvalidated **5118–5168** window.
- ✅ **The changelog is complete for this span.** `5117` == `_prev`'s `fromRevision`, so the two
  `version.json` files cover revs **5118–5168** (`ksa-game-assemblies_prev`) and **5169–5258**
  (`ksa-game-assemblies`) contiguously from the 5117 baseline — no revision went unreviewed.
- **How each touchpoint was verified:** (1) `dotnet build ksa-mod-experiments.slnx` against the live
  `5261` DLLs for *typed* breaks — **all 55 projects, 0 warnings, 0 errors** after fixes; (2) re-grep
  of the **entire** string-reflection watchlist plus a signature diff of **every** Harmony patch
  target across both trees, for the silent breaks the compiler can't see; (3) byte-layout diff of
  `PerInstanceData`/`MaterialData` and a content diff of every referenced GLSL/asset, including
  humble-arteest's anchor strings; (4) both `version.json` changelogs for behavioral changes that
  move no symbol.
- ⚠ **A green build is a small fraction of the risk here.** The render and behavioral findings below
  **cannot be cleared statically** — see *Current status* for what still needs a live in-game pass.
- The repo's own `decomp/ksa` copy is **older still** (June 12) and is not authoritative — always diff
  against the `ksa-game-assemblies` git tags.
- **Follow-up 2026-08-22 — the build is now cross-platform.** `dotnet build` with no environment
  overrides was failing off-Windows (41 × CS0246 from an unresolvable `KSAFolder`, then 3 more from a
  `CommunityToolkit.HighPerformance` reference the `ksa-game-assemblies` DLL copy set omits). Both are
  build config, not game drift; fixed, and the suite now builds **57/57 projects, 0 warnings,
  0 errors** on macOS against `5261`. Detail in
  [`../plans/KSA_5261_UPGRADE.md`](../plans/KSA_5261_UPGRADE.md) §7.

When a new game version arrives, bump this baseline and re-run the workflow below.

---

## How to use this on a game update

1. **Rebuild first.** `dotnet build` against the updated install surfaces all *typed* breaks
   immediately (renamed/removed public members, changed signatures used via `nameof`/direct calls).
   Many integration points are typed and will fail loudly here.
2. **Diff the string/reflection touchpoints.** Compile-clean ≠ safe. Open
   [`game-integration-surface.md`](game-integration-surface.md) → *String-based reflection watchlist*
   and re-grep each entry in the new decomp. These (private fields, string method names, Harmony
   overload param arrays) fail **silently at runtime**, not at compile. The same section lists the
   🔶 **standing invariants** — facts about the game that no grep can check, chiefly
   `StateBitFlag` bits 11..31 (humble-arteest) and **`[StarMapAllModsLoaded]` firing before
   `ModLibrary.Bind()`** (parts-now).
3. **Scan the changelog for behavioral hits.** Read the new `version.json` commit list and match it
   against the per-area "Update-risk findings" sections — some changes (control gating, editor tag
   schema, particle/shader reworks) break behavior without moving a symbol.
4. **Re-check shaders & per-instance layout.** Runtime-recompiled GLSL and per-instance data hacks
   (humble-arteest, mesh-deform) break when the game's shader sources change even though the C#
   compiles. Includes verifying `PerInstanceData.StateBitFlag` bits 11..31 are still unused by the
   game. See [`game-integration-surface.md`](game-integration-surface.md) → *Shaders & assets*.
5. **Record deltas + update these docs**, then capture the fix work in a `plans/` document (see the
   current one: [`../plans/KSA_5261_UPGRADE.md`](../plans/KSA_5261_UPGRADE.md)).

---

## The integration model (how unscience attaches to KSA)

- **StarMap is the loader seam, not the game.** `unscience/Mod.cs` is the single `[StarMapMod]` entry.
  StarMap.API Harmony-patches the game's render loop (`Program.OnDrawUiFrame` / `OnDrawUiViewports` /
  `OnFrame`) and dispatches to attributed methods (`[StarMapBeforeGui]`, `[StarMapAfterGui]`, …). The
  suite rides those hooks rather than touching the frame loop itself.
- **One consolidated Harmony instance.** `unscience/Patcher.cs` owns a single
  `Harmony("MeowSci.Unscience")`; each feature lib exposes `Apply(Harmony)`/`Remove(Harmony)` and the
  supermod applies them all onto that instance. `HotkeyGuard` is applied first.
- **`ISubmod` aggregation.** 23 feature libs implement `ISubmod` (`Name`/`Initialize`/`Update`/
  `RenderContent`/`RenderFloatingWindows`/`Dispose`); the same classes power each feature's standalone
  mod too.
- **`ksa-abstractions.lib` is the game-facing seam.** Cross-cutting game access is funneled through a
  handful of static helpers there, so a game update's blast radius is concentrated in one library.
- **Integration-point taxonomy** used throughout these docs: *Harmony patch* (prefix/postfix), *Reflection*
  (`AccessTools`/`System.Reflection`, especially string-named private members), *Direct API* (typed,
  compile-checked), *Render-pass/GPU* (render-system patches, shaders, Vulkan, per-instance byte
  offsets), *Asset* (templates/shaders/characters/sounds by id/path), *Lifecycle* (StarMap/ISubmod).

---

## Contents

| Area file | Covers | Highlights / highest-risk seams |
|---|---|---|
| [`game-integration-surface.md`](game-integration-surface.md) | **Master cross-reference index** — every game type/member touched, merged across mods | Start here for "does the game still have X?"; includes the string-reflection watchlist + shader/asset table |
| [`00-architecture-and-abstractions.md`](00-architecture-and-abstractions.md) | unscience supermod shell (`Mod.cs`/`Patcher.cs`/`MenuBarPatch`/`UnscienceState`) + `ksa-abstractions.lib` | StarMap lifecycle map, consolidated-Harmony cross-ref, `HotkeyGuard`, `IvaForceRender`, providers |
| [`vehicle-physics.md`](vehicle-physics.md) | eternal-flame, garrys-torch, i-feel-seen | `Universe.ExecuteNextVehicleSolvers`, `Battery.Refill`, `Vehicle.Teleport`, KittenEva reflection; **garrys-torch solver-drain rewrite (`JobSystems.VehicleSolver`)** |
| [`celestial-and-lights.md`](celestial-and-lights.md) | kiwis-marbles, zippo, red-alert | `Celestial.SetOrbit`, `IOrbiter`, `LightModule`/`LightSwitch`, `KeyframeAnimationModule.TimeGoal`; **zippo color latent bug** |
| [`camera.md`](camera.md) | camera-controller-override, glass | `OrbitController/FlyController.OnFrame`, `Camera._fovRadians`; the `___Transform` injector bug is **fixed** (prefix now reads `__instance.Camera`) |
| [`telemetry.md`](telemetry.md) | average-twr, geeforce | `NavBallData.ThrustWeightRatio`, `VehicleConfigInfo.TotalEngineVacuumThrust`, `Vehicle.AccelerationBody`, `Situation` |
| [`pixel-grids-and-render.md`](pixel-grids-and-render.md) | blinky, its-so-shiny, thug-life | three `*Module.UpdateRenderData` patches, `PartTree.CreateFromNewPartTree`, `SuperMeshRenderSystem.RenderMainPass`, UnlitMesh shaders |
| [`character-and-materials.md`](character-and-materials.md) | doh, humble-arteest, kitten-animations | `GpuMaterialSystem.BigBuffer`, `KittenEva`/`EVADoor`, `PerInstanceData` `StateBitFlag` free-bit paint + `ShaderModuleUtils.FromFile` shader patch, `CatExpressionAnim` |
| [`part-editor-and-robotics.md`](part-editor-and-robotics.md) | space-tape, flexo, parts-now | `ThumbnailReference`/`ThumbnailPart`, `PartImporter` templates, `PartModelRenderer.UpdateRenderData`, `Part.Asmb2ParentAsmb`; **space-tape compile breaks**; parts-now's `ModLibrary` reflection + `DeviceMeshInterleaved.Shared` headroom invariant |
| [`ui-customization.md`](ui-customization.md) | skittles, con-man, kitchen-sink | `ImGui` style surface, `GaugeCanvas` private-field reflection, `ReinitializeDerivedValues` + IvaForceRender |
| [`rpc.md`](rpc.md) | unladen-swallow | GenHTTP server + game-thread marshaling; delegates to other libs (cross-ref table inside) |
| [`standalone-mods.md`](standalone-mods.md) | marque, byo-music, steely-eyed-missile-kitten, mesh-deform, stampy | **Not bundled in the supermod**; secondary reference. **mesh-deform shader break** |

Bundled in the unscience supermod (23): average-twr, blinky, camera-controller-override, con-man,
doh, eternal-flame, flexo, garrys-torch, geeforce, glass, humble-arteest, i-feel-seen, its-so-shiny,
kitchen-sink, kitten-animations, kiwis-marbles, parts-now, red-alert, skittles, space-tape,
thug-life, unladen-swallow, zippo. (marque, byo-music, steely-eyed-missile-kitten, mesh-deform,
stampy live in the repo but are **not** loaded by the supermod.)

---

## Current status against `5261` (summary)

Full detail lives in [`game-integration-surface.md`](game-integration-surface.md) §6; the remediation
record is in [`../plans/KSA_5261_UPGRADE.md`](../plans/KSA_5261_UPGRADE.md). The 5117→5261 span is
large (420 decomp files, ~16.7k inserted lines) and dominated by a **vehicle-threading rewrite**
(`DynamicWorkerPool`/`ParallelBatch`/`PhysicsBubble` islands), the **`SimTime` → `UniverseTime`**
128-bit-nanosecond migration, an **EVA/kitten overhaul** (control modes, ladders, jump/tumble anims),
a **gauge/HUD context system**, and continued **render-pass modernization**. Blast radius on
unscience: **five compile breaks across four projects, one confirmed behavioral break, and a broad
set of watch items.**

> ⚠ **Two of the five compile breaks did not originate in this build.** They landed in the
> **5118–5168** window, which was never validated (no upgrade pass ran for `2026.8.5.5168`). Treat
> `_prev` as a diff aid, not as a verified baseline.

**Build-blocking (all FIXED — `dotnet build ksa-mod-experiments.slnx` is green, 0 warnings/0 errors):**
- **ksa-abstractions.lib / doh.lib / garrys-torch.lib** — rev 5211 replaced `SimTime` with
  **`UniverseTime`** (`Int128` nanoseconds) and renamed `Universe.GetElapsedSimTime()` →
  `GetElapsedTime()`. `.Seconds()` survives on the new type and every consumer either calls it or
  passes the value into `Orbit.CreateFromStateCci`, so **no arithmetic or precision handling changed**.
- **garrys-torch.lib** — revs 5208–5216 split `JobSystems.VehicleSolvers` into the single-runner
  orchestrator **`VehicleSolver`** plus the **`VehicleWorkerPool`** (`DynamicWorkerPool`). The mod's
  correctness-critical drain is now `VehicleSolver.Wait()`: the pool has no `Wait()` and is only
  driven through scoped `ParallelBatch()` fork/join, so all pool work joins before the queued
  `_vehicleUpdateTask` completes — **the game drains the same way in `Universe.DeserializeSave`**.
- **parts-now.lib** *(pre-existing, 5118–5168)* — `ImageBarrierInfo.Presets.SampledReadFragment` →
  **`SampledReadF`** (an abbreviation sweep: `…Vertex`→`…V`, `…Fragment`→`…F`, `…Compute`→`…C`).
  Same layout/access/stage; a pure rename.
- **thug-life.lib** *(pre-existing, 5118–5168)* — **`Program.OffScreenPass` removed.** Not a rename:
  the game **migrated the main scene pass to dynamic rendering.** The offscreen target is now
  `Program.OffscreenTarget` (`RenderTarget : IRenderPassInfo`), which has no `.Pass`/`.SampleCount`;
  pass compatibility comes from `SetupGraphicsPipeline(ref info)`, which chains a
  `VkPipelineRenderingCreateInfo` onto `pNext`, nulls `RenderPass` and fills `MultisampleState`.
  thug-life now mirrors the game's own main-pass pipelines.

**Behavioral break (compile-clean, code change likely required):**
- **con-man** — rev 5201 added a per-canvas visibility context system. The draw gate is now
  `!_enabled || !IsContextVisible() || (CrewPortraits && !HasOccupants)`, so **setting `_enabled` is
  no longer sufficient to show a gauge**; canvases with `VisibleInContext` flags stay hidden unless
  every flag matches the controlled vehicle. `GameSettings.Current.GaugeContextOverrides` is a second
  source of truth con-man doesn't know about. **Open — needs a decision on coexistence.**
- **blinky** *(pre-existing, predates 5117)* — its default `EnginePartId`
  `"CorePropulsionA_Prefab_EngineA1"` was removed from Content between 5018 and 5117; only
  `EngineA2…A6` exist. `ModLibrary.Get` throws — a concrete candidate explanation for the
  "blinky broken" entry in [`../ISSUES.md`](../ISSUES.md). **Recommend defaulting to `EngineA2`.**

**Verified clean against 5261:**
- **The entire string-reflection watchlist resolves** — all 7 `GaugeCanvas` fields (still on
  `GaugeCanvas` itself), `Camera._fovRadians`, the KittenEva→`CharacterCore.Scale` chain,
  `CatExpressionAnim._expressionPose`, `"KSA.LightModule+TemplateData"`, the doh/humble-arteest render
  bridge, `ShaderReference.*` + `ShaderModuleUtils.FromFile`, all seven `ModLibrary.All*` registries,
  `SerializedCollection<T>._collection`, `VehicleEditor._editorTagLookup`,
  `Part.ResetCachedPosMatrixValues`, `PartTree.RecomputeStaticMass`, `GameSettings.OnKeyAll`.
- **Every Harmony patch-target signature unchanged** (line shifts only), including
  `Universe.ExecuteNextVehicleSolvers(double, SimStep)` — still one overload — plus
  `SuperMeshRenderSystem.RenderMainPass`, `PartModel.AddInstance`, the three `*Module.UpdateRenderData`,
  `Vehicle.Teleport`/`RefillConsumables`, `GaugeCanvas.OnDrawMenuBar`, `Program.DrawMenuBar`.
- **GPU byte layouts identical** — `PerInstanceData` and `MaterialData`, so doh's `handle*80+16`
  writes and humble-arteest's padding-byte hijack are safe.
- **Shaders** — `UnlitMesh.vert/.frag` and `MeshIndirect.vert` **identical**. `MeshIndirect.frag`
  changed by exactly one line (rev 5196 portrait lights); humble-arteest's `vec3 sampledColor` anchor,
  its `inStateFlags` guard and the `ENABLE_TEMPERATURE` LUT all survive — **Engine Emissive unaffected**.
- **Suite load path intact** — `Program.OnDrawUiFrame` / `OnDrawUiViewports` / `OnFrame` /
  `DrawProgramMenusHook` all still present, so StarMap keeps its seams.
- **`KittenEva` changes are purely additive** (ladders, jump/tumble, `KittenControlMode`).
- **Numerics untouched** — rev 5242's row-major quaternion/mat3 fix changed **no `Brutal.Numerics`
  file**, so the frames garrys-torch, kiwis-marbles, thug-life and camera-controller-override rely on
  are unchanged.

**Known-broken reconciliation:**
- **camera-controller-override `___Transform`** — ✅ **now FIXED in the repo**; the prefix reads
  `__instance.Camera`. Earlier scope/ text describing it as broken was stale.
- **zippo `GetField("Color")`** — ❌ still broken (real field is `ColorRgb`); unchanged by this build.
- **humble-arteest Vehicle Paint / mesh-deform** — ❌ still dead by design since rev 4693; both
  self-disable. Anchors still resolve.

**Not cleared statically — a live in-game pass is still required** for thug-life's rebuilt pipeline,
garrys-torch's drain under the new parallel model, con-man's gauge gating, blinky after a default-part
change, doh's fur (rev 5230), and kitten-animations against the new anim states. A green
`dotnet build` does not cover these, and there is no test suite in this repo.
