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

- **Cataloged against:** KSA build **`2026.8.3.5117`** (2026-08-01) — decomp at
  `…/ksa-game-assemblies/current/decomp`, assets at `…/ksa-game-assemblies/current/Content`.
  Confirmed identical to the live install (`C:\Program Files\Kitten Space Agency\KSA.dll`), so
  `dotnet build` compiled against this build.
- **Diffed from:** KSA build **`2026.7.9.5018`** — the previously verified baseline, recovered from
  git tag `2026.7.9.5018` in the `ksa-game-assemblies` repo. The intermediate build `2026.7.10.5056`
  was not separately verified, so the honest span is **5018 → 5117**.
- ✅ **The changelog is complete for this span** — unlike 4750→5018. The two `version.json` files on
  disk cover revs **5019–5056** (`ksa-game-assemblies_prev`) and **5057–5117** (`ksa-game-assemblies`)
  contiguously from the 5018 baseline, so no revision went unreviewed.
- **How each touchpoint was verified:** (1) `dotnet build ksa-mod-experiments.slnx` against the live
  `5117` DLLs for *typed* breaks — **all 55 projects, 0 warnings, 0 errors**; (2) re-grep of the
  **entire** string-reflection watchlist plus a signature diff of **every** Harmony patch target
  across both git-tagged trees, for the silent breaks the compiler can't see; (3) byte-layout diff of
  `PerInstanceData`/`MaterialData` and a content diff of every referenced GLSL/asset, including
  humble-arteest's anchor strings; (4) both `version.json` changelogs for behavioral changes that
  move no symbol.
- ⚠ **A green build is a small fraction of the risk here.** The render and behavioral findings below
  **cannot be cleared statically** — see *Current status* for what still needs a live in-game pass.
- The repo's own `decomp/ksa` copy is **older still** (June 12) and is not authoritative — always diff
  against the `ksa-game-assemblies` git tags.

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
   current one: [`../plans/FIX_CURRENT_GAPS_PLAN.md`](../plans/FIX_CURRENT_GAPS_PLAN.md)).

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
| [`vehicle-physics.md`](vehicle-physics.md) | eternal-flame, garrys-torch, i-feel-seen | `Universe.ExecuteNextVehicleSolvers`, `Battery.Refill`, `Vehicle.Teleport`, KittenEva reflection; **garrys-torch compile break** |
| [`celestial-and-lights.md`](celestial-and-lights.md) | kiwis-marbles, zippo, red-alert | `Celestial.SetOrbit`, `IOrbiter`, `LightModule`/`LightSwitch`, `KeyframeAnimationModule.TimeGoal`; **zippo color latent bug** |
| [`camera.md`](camera.md) | camera-controller-override, glass | `OrbitController/FlyController.OnFrame`, `Camera._fovRadians`; **camera `___Transform` injector latent bug** |
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

## Current status against `5117` (summary)

Full detail lives in [`game-integration-surface.md`](game-integration-surface.md) §6; the remediation
record is in [`../plans/FIX_CURRENT_GAPS_PLAN.md`](../plans/FIX_CURRENT_GAPS_PLAN.md). The 5018→5117
span is moderate (223 decomp files, ~11.7k inserted lines; 103 Content files) and dominated by a
**crew/roster system** (KittenRoster, IVASeat, EVADoor↔seat linking), a **burn/orbit UX rework**,
**launch pads**, **vehicle structural destruction**, and **cloud shadows on vessels**. Blast radius on
unscience: **two compile breaks, one silent break, and five behavioral watch items.**

**Build-blocking (both FIXED — `dotnet build ksa-mod-experiments.slnx` is green, 0 warnings/0 errors):**
- **space-tape.lib** — `Double3Ex.{Up,Down,Left,Right,Forward,Backward}` removed (rev 5067: *"they
  were misleading and often misused"*). 15 CS0117. The six constants now live in
  **`ksa-abstractions.lib/Directions.cs`** with identical values, so behavior is unchanged. The game's
  own migration went to new `Camera.{ForwardView,RightView,UpView}` and renamed
  `Camera.GetForward/GetRight/GetUp` → `Get*Ecl` — **no mod calls the renamed accessors.**
- **average-twr.lib** — `FlightComputer.VehicleConfigInfo.TotalEngineVacuumThrust` removed with the
  whole vacuum-referenced family (rev 5114). Now reads
  `Vehicle.ComputeActiveThrust(FlightComputer.AmbientPressure)`, the same call the game's navball TWR
  uses. **Behavior change:** TWR/accel are now ambient-corrected and propellant-aware.

**Silent break (compile-clean, was actively corrupting — FIXED):**
- **space-tape.lib** — rev 5112 changed `Part`'s uncached-matrix sentinel from `double4x4.Identity`
  to an all-NaN `UncachedMatrix` (and added three more cached fields). space-tape wrote `Identity`
  into `_matrixAsmb`/`_matrixAsmb2Parent` **to invalidate** them; on 5117 that instead asserts a
  cached identity transform, collapsing the part transform with no build error. All three sites now
  call the public **`Part.ResetCachedPosMatrixValues()`** and the reflection is retired — a watchlist
  entry removed outright. flexo's similar `HingeController` path was never exposed (it touches
  property setters, which invalidate correctly).

**Behavioral watch items (compile-clean, need a live pass):**
- **space-tape `<EVADoor>` writer** — `EVADoorTemplate.SeatId` is new (rev 5085) and is what links a
  door to an `IVASeat`; the EVA button now only shows when that seat is occupied. space-tape emits no
  `SeatId`, so authored EVA doors will be **inert**. **Open — not fixed** (needs a UI decision on how
  the user picks a seat id). Its existing `ConnectorId` attribute was never a real
  `EVADoorTemplate` member — pre-existing no-op.
- **doh `KittenSpawner`** — replicates the old `CreateKittenEva()` flow; the game now sources kittens
  from `Universe.KittenRoster` via the aligned `IVASeat`, and vehicle disposal finalizes kitten
  mission stats. Ctor unchanged so doh still runs, but spawns **roster-less** kittens.
- **con-man** — `GaugeCanvas.PlaceBesideActiveBurnGizmo()` (revs 5092/5113) **writes `_customOffset`**,
  the exact private field con-man owns; the game can now overwrite saved layout offsets.
- **garrys-torch / kiwis-marbles** — rev 5115 added vehicle destruction on structural g-limit and
  dynamic-pressure limit. Torch teleports every frame; marbles rewrites orbits.
- **parts-now** — `EditorTag` gained `Booster`/`Coupling`/`Cargo`; `BuiltInEditorTags` still lists six.
  Harmless today (none are registered in `VehicleEditor._editorTagLookup` or `PartGameData.xml`).

**Verified clean against 5117:**
- **All 33 Harmony patch-target signatures byte-identical**, including every shared chokepoint:
  `GameSettings.OnKeyAll` (HotkeyGuard → every mod), `Universe.ExecuteNextVehicleSolvers` (still one
  overload), `Program.DrawProgramMenusHook`, the three `*Module.UpdateRenderData`,
  `PartModel(.Dynamic).AddInstance`, `SuperMeshRenderSystem.RenderMainPass`, `ShaderModuleUtils.FromFile`.
- **The entire string-reflection watchlist resolves** — all 7 `GaugeCanvas` fields, `Camera._fovRadians`,
  the KittenEva→`CharacterCore.Scale` chain, `LightModule+TemplateData.ColorRgb`, all six parts-now
  `ModLibrary.All*` registries, `SerializedCollection<T>._collection`, `VehicleEditor._editorTagLookup`.
  (`ModLibrary.cs`'s diff is **only** log line-number churn.)
- 🔶 **Both standing invariants HOLD:** `PerInstanceData.StateBitFlag` bits 11..31 are still unused by
  the game (it uses ≤ bit 6), and `[StarMapAllModsLoaded]` still fires before `ModLibrary.Bind()`
  (`LoadAll` `Program.cs:965` < `Bind` `:994`).
- **humble-arteest's GLSL anchors survive** — `MeshIndirect(.Raytraced).frag` changed for rev-5100
  cloud shadows, but only inside `getLightColor()`; `vec3 sampledColor` and the `inStateFlags` varying
  are intact and `GetCloudShadow` resolves via the passed-through include callback.
- **thug-life** — `UnlitMesh*` shaders and both shader ids unchanged; the MSAA/alpha-to-coverage work
  (revs 5057/5058) is absorbed because the mod reads `Program.OffScreenPass.SampleCount` dynamically.
- `MaterialData`, `GpuMaterialSystem`, `PartModel(.Dynamic)`, `CharacterAvatar`, `CatExpressionAnim`,
  `LightModule`, `GenericGizmo`, `OrbitLinePass`, `Controller`, `KeyframeAnimationModule`,
  `DeviceMeshInterleaved`, `Situation`, `KinematicMeasurements`, `KSAColor` — **byte-identical**.
- Assets removed in revs 5077/5096 (`IconSymmetry*`, `Icon*Gizmo`, `PlanetMeshVertexDataComp`) are
  referenced by **no** mod.

**Still genuinely broken, pre-existing, NOT caused by this update:**
- **mesh-deform** (standalone, not bundled) — dead by design change since rev 4693; self-detects and
  disables. `MeshIndirect.vert` is byte-identical 5018→5117, so its probe is still correct.

**Not cleared statically — a live in-game pass is still required** for the five behavioral items
above plus render correctness for thug-life's quad, humble-arteest's paint/emissive, blinky /
its-so-shiny grids, and space-tape / flexo gizmos. A green `dotnet build` does not cover these.
