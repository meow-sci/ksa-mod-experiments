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

- **Cataloged against:** KSA build **`2026.7.9.5018`** (2026-07-25) — decomp at
  `…/ksa-game-assemblies/current/decomp`, assets at `…/ksa-game-assemblies/current/Content`.
  Confirmed identical to the live install (`C:\Program Files\Kitten Space Agency\KSA.dll`), so
  `dotnet build` compiled against this build.
- **Diffed from:** KSA build **`2026.6.9.4750`** — the previously verified baseline, recovered from
  git tag `2026.6.9.4750` in the `ksa-game-assemblies` repo. The intermediate builds `4826`, `4892`,
  `4939` and `4980` were never separately verified, so the honest span is **4750 → 5018**.
- **How each touchpoint was verified:** (1) `dotnet build ksa-mod-experiments.slnx` against the live
  `5018` DLLs for *typed* breaks; (2) re-grep of the **entire** string-reflection watchlist plus a
  signature diff of **every** Harmony patch target in both trees, for the silent breaks the compiler
  can't see; (3) byte-layout diff of `PerInstanceData`/`MaterialData` and a content diff of every
  referenced GLSL/asset; (4) the `version.json` changelogs for behavioral changes that move no symbol.
- ⚠ **The changelog is incomplete for this span.** No `version.json` on disk covers revs **4751–4824**
  or **4827–4859** (~110 revisions). Steps 1–3 above compare *source*, so they cover the gap; the
  changelog scan alone does not.
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
   overload param arrays) fail **silently at runtime**, not at compile.
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
- **`ISubmod` aggregation.** 22 feature libs implement `ISubmod` (`Name`/`Initialize`/`Update`/
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
| [`part-editor-and-robotics.md`](part-editor-and-robotics.md) | space-tape, flexo | `ThumbnailReference`/`ThumbnailPart`, `PartImporter` templates, `PartModelRenderer.UpdateRenderData`, `Part.Asmb2ParentAsmb`; **space-tape compile breaks** |
| [`ui-customization.md`](ui-customization.md) | skittles, con-man, kitchen-sink | `ImGui` style surface, `GaugeCanvas` private-field reflection, `ReinitializeDerivedValues` + IvaForceRender |
| [`rpc.md`](rpc.md) | unladen-swallow | GenHTTP server + game-thread marshaling; delegates to other libs (cross-ref table inside) |
| [`standalone-mods.md`](standalone-mods.md) | marque, byo-music, steely-eyed-missile-kitten, mesh-deform, stampy | **Not bundled in the supermod**; secondary reference. **mesh-deform shader break** |

Bundled in the unscience supermod (22): average-twr, blinky, camera-controller-override, con-man,
doh, eternal-flame, flexo, garrys-torch, geeforce, glass, humble-arteest, i-feel-seen, its-so-shiny,
kitchen-sink, kitten-animations, kiwis-marbles, red-alert, skittles, space-tape, thug-life,
unladen-swallow, zippo. (marque, byo-music, steely-eyed-missile-kitten, mesh-deform, stampy live in
the repo but are **not** loaded by the supermod.)

---

## Current status against `5018` (summary)

Full detail lives in [`game-integration-surface.md`](game-integration-surface.md) §6; the remediation
record is in [`../plans/FIX_CURRENT_GAPS_PLAN.md`](../plans/FIX_CURRENT_GAPS_PLAN.md). Headline: the
4750→5018 span is large (433 decomp files, ~40k inserted lines), driven by three game-side rewrites —
**combustion → reaction model**, **fuel/resource feed wiring**, and **staging → resource groups** —
plus a substantial **gauge/HUD** rework. Despite that, the blast radius on unscience was three compile
errors and no new runtime break.

**Build-blocking (all three FIXED — `dotnet build ksa-mod-experiments.slnx` is green):**
- **space-tape.lib** — `PartTemplate.Tank` removed; tanks are now `Tank.TemplateData` entries in
  `PartTemplate.Components`. Importer iterates `Components` (and so now supports multi-tank parts).
- **doh.lib** — `SubstanceLibrary.TryGetCombustionProcess` and the whole `Combustion*` type family
  removed. Now resolves `TryGetReaction("MMH_NTO")` and evaluates the `MixtureReaction` at its
  `DefaultMixtureRatio` (1.65) to get the `ReactantMix` that `Tank.ConfigureFor` now takes. The old
  ratio-in-the-id convention (`MMH_NTO_1.6`) is gone.
- **blinky.lib** — `RocketCore.ResourceManager` moved down to the `Combustor` subclass; the diagnostic
  now tests `core is Combustor` (`SolidMotor` cores legitimately have none).

**Silent byte-layout change (mesh-deform still exposed; humble-arteest no longer is):**
- `PartModel.PerInstanceData.packing2` and `PartModelDynamic.PerInstanceData.packing1` are now
  **`public float Wetness`**, feeding a new `ENABLE_WETNESS` shader variant. **mesh-deform** writes
  into that slot but stays inert behind its content probe (still correct on 5018).
  humble-arteest **Vehicle Paint** was rebuilt (2026-07-25) and no longer writes any per-instance
  *field* — it uses the free `StateBitFlag` bits 11..31 instead, so `EmissiveColor`, `Temperature`,
  `TfiThickness` and `Wetness` are all left intact. **Engine Emissive** was never affected — it
  writes only `Temperature`/`TfiThickness`, whose offsets did not move.
- 🔶 **New standing invariant to audit each update:** `PerInstanceData.StateBitFlag` bits **11..31**
  must remain unused by KSA (it uses 0..10 today). See
  [`character-and-materials.md`](character-and-materials.md) → humble-arteest row A10.

**Behavioral watch items (compile-clean, need a live pass):**
- **con-man / marque vs the gauge-HUD rework** (revs 4919/4940/4959/5003): the game moved the gauge
  toggles from the View dropdown into a new **Hud** dropdown and shipped a native **HudLayouts**
  save/load feature — a first-party re-implementation of con-man's feature and a relocation of the
  menu marque injects into. All 7 of con-man's reflected fields still resolve.
- **`KeyframeAnimationModule.TimeGoal` now fans out to mirrored parts** → red-alert.
- **Animation pipeline reworked** (`IAnimProcessor.UpdateLocalPose`, `MixPose`→`MixPoseLocal`);
  kitten-animations still works, and this is the prime suspect for the `ISSUES.md` "always the same
  expression" report.
- **Rev 4914 control-module lockout** is UI-layer only — `EngineController`/`ThrusterController`
  `SetIsActive` are byte-identical, so blinky/its-so-shiny are unaffected.
- Carried forward from 4750: editor tag/category schema, face-snapping/connector rules, part-size XML.

**Previously-recorded breaks now CLOSED (fixed in-repo; re-verified correct against 5018):**
- **camera-controller-override** `___Transform` field injector — fixed; the prefix reads
  `__instance.Camera` directly, and 5018 still exposes `public Camera Camera` with no `Transform`
  field. (This also unblocked the supermod patch chain, which the injector's throw used to abort.)
- **zippo** color control — fixed; reflects `"ColorRgb"`, matching 5018.
- supermod `Patcher.cs` now wires `IvaForceRender.Patch`/`Unpatch`.

- **humble-arteest Vehicle Paint** — **rebuilt for 5018 (2026-07-25)** and working again. The
  4693-era shader-swap was replaced by a `ShaderModuleUtils.FromFile` Harmony prefix that compiles a
  patched `MeshIndirect(.Raytraced).frag` in memory, with the per-part color carried in the free
  `StateBitFlag` bits. Targeting is per **part instance**, per part type, or global; works in flight
  and in the editor. See [`character-and-materials.md`](character-and-materials.md) rows A1–A11.

**Still genuinely broken, pre-existing, NOT caused by this update:**
- **mesh-deform** (standalone, not bundled) — dead by design change since rev 4693; self-detects and
  disables, and its probe is still correct on 5018. It could now be revived by reusing
  humble-arteest's `FromFile` interception, but it also needs per-instance *floats*, which the
  free-bit trick cannot carry.

**Everything else verified clean against 5018** — every Harmony patch-target signature, the full
string-reflection watchlist, `MaterialData`/`CharacterAvatar` layouts, and the `UnlitMesh`/
`MaterialSet.glsl`/`ModelPbr.frag` shaders (byte-identical → thug-life and doh Kitten Color safe).
