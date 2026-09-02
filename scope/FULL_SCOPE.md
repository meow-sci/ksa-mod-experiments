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

- **Cataloged against:** KSA build **`2026.8.22.5348`** (2026-08-23) — decomp at
  `…/ksa-game-assemblies/current/decomp`, assets at `…/ksa-game-assemblies/current/Content`.
  On macOS `Directory.Build.props` tier 2 resolves `KSAFolder` to
  `../ksa-game-assemblies/current/dll/`, so **`dotnet build` compiled against exactly this build** —
  there is no separate install to reconcile and no `KSAFolder` trap.
- **Diffed from:** KSA build **`2026.8.19.5261`** — the previously verified baseline, and also what is
  on disk as `ksa-game-assemblies_prev`. **Baseline == OLD**, so this is a clean single hop of
  **87 revisions (5262–5348)**, unlike the 5117→5261 pass.
- ✅ **The changelog is complete for this span.** `5261` == NEW's `fromRevision`, so NEW's
  `version.json` alone (175 changelog lines) covers every revision from the baseline. Nothing went
  unreviewed.
- **How each touchpoint was verified:** (1) `dotnet build ksa-mod-experiments.slnx` against the `5348`
  reference DLLs for *typed* breaks — **55/55 projects, 0 warnings, 0 errors** at the time of the pass
  (the suite went to 57 with dont-stifle-me and back to **55** when flexo was removed; still 0/0); (2) re-grep of the
  **entire** string-reflection watchlist plus a signature diff of **every** Harmony patch target
  across both trees, for the silent breaks the compiler can't see — including a **field-vs-property
  audit** prompted by rev 5329 moving `Module.Parent` to a property; (3) byte-layout diff of
  `PerInstanceData`/`MaterialData`, a full `diff -rq` of `Content/Core/Shaders`, and an id check of
  every referenced asset, including humble-arteest's and mesh-deform's anchor strings; (4) the
  `version.json` changelog for behavioral changes that move no symbol.
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
   current one: [`../plans/KSA_5348_UPGRADE.md`](../plans/KSA_5348_UPGRADE.md)).

---

## The integration model (how unscience attaches to KSA)

- **StarMap is the loader seam, not the game.** `unscience/Mod.cs` is the single `[StarMapMod]` entry.
  StarMap.API Harmony-patches the game's render loop (`Program.OnDrawUiFrame` / `OnDrawUiViewports` /
  `OnFrame`) and dispatches to attributed methods (`[StarMapBeforeGui]`, `[StarMapAfterGui]`, …). The
  suite rides those hooks rather than touching the frame loop itself. **One exception:** the two GUI
  hooks' targets are skipped by the game while the HUD is hidden (F2 → `Program.DrawUI == false`), so
  `ksa-abstractions.lib/HiddenUiFrameHook` prefixes the always-called `Program.OnDrawUiConsole` and
  replays the shell's non-UI per-frame work only in that state (see
  [`00-architecture-and-abstractions.md`](00-architecture-and-abstractions.md)).
- **One consolidated Harmony instance.** `unscience/Patcher.cs` owns a single
  `Harmony("MeowSci.Unscience")`; each feature lib exposes `Apply(Harmony)`/`Remove(Harmony)` and the
  supermod applies them all onto that instance. `HotkeyGuard` is applied first.
- **`ISubmod` aggregation.** 24 feature libs implement `ISubmod` (`Name`/`Initialize`/`Update`/
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
| [`celestial-and-lights.md`](celestial-and-lights.md) | kiwis-marbles, zippo, red-alert | `Celestial.SetOrbit`, `IParentBody.Children`/`UpdatePerFrameDataTree`, `Universe.ExecuteNextVehicleSolvers` prefix (kiwis-marbles sim-step timing, fixed 2026-08-23), `IOrbiter`, `LightModule`/`LightSwitch`, `KeyframeAnimationModule.TimeGoal`; **zippo color latent bug** |
| [`camera.md`](camera.md) | camera-controller-override, glass | `OrbitController/FlyController.OnFrame`, `Camera._fovRadians`; the `___Transform` injector bug is **fixed** (prefix now reads `__instance.Camera`) |
| [`telemetry.md`](telemetry.md) | average-twr, geeforce | `NavBallData.ThrustWeightRatio`, `VehicleConfigInfo.TotalEngineVacuumThrust`, `Vehicle.AccelerationBody`, `Situation` |
| [`pixel-grids-and-render.md`](pixel-grids-and-render.md) | blinky, its-so-shiny, thug-life | three `*Module.UpdateRenderData` patches, `PartTree.CreateFromNewPartTree`, `RocketCore.FeedConnectors` (blinky ignition), `SuperMeshRenderSystem.RenderMainPass`, UnlitMesh shaders |
| [`character-and-materials.md`](character-and-materials.md) | doh, humble-arteest, kitten-animations | `GpuMaterialSystem.BigBuffer`, `KittenEva`/`EVADoor`, `PerInstanceData` `StateBitFlag` free-bit paint + `ShaderModuleUtils.FromFile` shader patch; **kitten-animations reworked @5348** — Harmony prefix on `AnimatedRenderable.UpdateAnimation`, the ground animation set read from 17 private `KittenRenderable` fields, and a mod-owned `CatExpressionAnim` |
| [`part-editor-and-robotics.md`](part-editor-and-robotics.md) | parts-now, dont-stifle-me | parts-now's `ModLibrary` reflection + `DeviceMeshInterleaved.Shared` headroom invariant; **dont-stifle-me** postfix/prefix on `VehicleEditor.ScaleBoundsFor` / `UpdateSelectedScale` / `QuantizeScale` (all new @5348). **space-tape removed @5348** — rev 5329 deleted `PartTemplate.Decoupler`; the mod was defunct and was deleted rather than ported. **flexo removed @5348** — compiled clean, but the robotics approach never worked and will not be reattempted; `PartModelRenderer.UpdateRenderData` and `OrbitLinePass` are now unowned |
| [`exhaust-plumes.md`](exhaust-plumes.md) | pyro | `Vehicle.AddVolumetricExhaustInstances` postfix, `VolumetricExhaustRenderer.AddInstance`, `VolumetricExhaustInstance` (+ private `_shaderData`), internal `VolumetricExhaustTemplate.References`, `PlumeData`/`ExhaustInstance` layout drift (new @5348) |
| [`decals.md`](decals.md) | graffiti | `RenderTarget.ResolveAttachments` postfix (GridPass-window projected-decal pass), `GlobalShaderBindings` + `BindlessTextureLibrary` descriptor sets, runtime GLSL vs `Common/*.glsl` headers, `Part.RayCastEgo` + `Cursor.InputRay` picking, CPU terrain march; **no string reflection** (new @5348) |
| [`rings.md`](rings.md) | rocky-mcrock-face, bloomin-onion | planetary-ring mesh/texture swap (rocky) and **runtime ring definition on any celestial** (bloomin-onion) via the public `PlanetaryRingsReference` data tree + `Program.RebuildRenderer()`; **no Harmony patches**; `ModLibrary.AllMeshes`/`AllFiles` reflection, `MeshReference.<HostPrimitives>k__BackingField`, ctor-baking invariant in `PlanetaryRingsRenderData`; bloomin-onion adds `PlanetTransparenciesRenderer._anyRings` (load-bearing), `TextureReference.<TextureAsset>k__BackingField` (painted textures) and a cosmetic `DistantSphereRenderer._data` sync (new @5348) |
| [`ui-customization.md`](ui-customization.md) | skittles, con-man, kitchen-sink | `ImGui` style surface, `GaugeCanvas` private-field reflection, `ReinitializeDerivedValues` + IvaForceRender |
| [`rpc.md`](rpc.md) | unladen-swallow | GenHTTP server + game-thread marshaling; delegates to other libs (cross-ref table inside) |
| [`standalone-mods.md`](standalone-mods.md) | marque, byo-music, steely-eyed-missile-kitten, mesh-deform, stampy | **Not bundled in the supermod**; secondary reference. **mesh-deform shader break** |

Bundled in the unscience supermod (26): average-twr, blinky, bloomin-onion, camera-controller-override, con-man,
doh, dont-stifle-me, eternal-flame, garrys-torch, geeforce, glass, graffiti, humble-arteest,
i-feel-seen, its-so-shiny, kitchen-sink, kitten-animations, kiwis-marbles, parts-now, pyro,
red-alert, rocky-mcrock-face, skittles, thug-life, unladen-swallow, zippo. (marque, byo-music, steely-eyed-missile-kitten, mesh-deform, stampy and
jplrepo live in the repo but are **not** loaded by the supermod.)

---

## Current status against `5348` (summary)

Full detail lives in [`game-integration-surface.md`](game-integration-surface.md) §6; the remediation
record is in [`../plans/KSA_5348_UPGRADE.md`](../plans/KSA_5348_UPGRADE.md). The 5261→5348 span is
87 revisions dominated by **ground clutter** (collisions, exclusion masks, destruction), a **terrain
precision rework**, a **clustered-lighting rewrite** (rev 5301), a **static-object pipeline**
(launchpads, decals), a **physics-bubble merge/split rewrite** (revs 5331/5339), a **vehicle-power
rework onto electrical circuits** (rev 5326), and — the one that reached us — a **part/module
sequencing refactor** (rev 5329). Blast radius on unscience: **one compile break, three behavioral
watch items, one favourable regression.**

**Build-blocking — resolved by removing the mod:**
- **space-tape** — rev 5329 moved decouplers from `PartTemplate.Decoupler` (a `DecouplerTemplate`
  field, now deleted) into `PartTemplate.Components` as `Decoupler.TemplateData`. Three × CS1061 in
  `space-tape.lib/PartImporter.cs`. **space-tape is defunct; the mod and its `.lib` were deleted**
  and unwired from the solution and the supermod.
  The on-disk XML shape `<Decoupler ConnectorId=… Force=… />` is unchanged — only the typed reader broke.

**Behavioral — compile-clean, needs a live pass before any code change:**
- **con-man** — rev 5293 added a global **Hud Scale** applied *after* per-canvas scale
  (`GaugeCanvas` now divides by `GameSettings.GetGaugeScale()` and wraps draws in
  `ConsoleStyle.BeginGaugeHostScope`). All seven reflected fields still resolve byte-identically, but
  **layouts saved at one Hud Scale will restore wrong at another.** Stacks on the still-**open**
  rev-5201 `IsContextVisible()` gate.
- **kitten-animations** — **reworked 2026-08-23; root cause found and fixed, live pass still wanted.**
  The rev-5278 per-frame pose guard (`AnimatedRenderable._lastPoseUpdateFrameNumber`) turned out to be
  benign. The real defect behind *"always the same expression"* was that the mod wrote
  `ExpressionWeight` to `KittenRenderable._catExpressionAnim` — the reactive face, whose weight
  `UpdateRenderData` damps from vehicle acceleration every frame right before the pose is sampled — so
  only the permanent personality mood face ever showed. The mod now appends **its own**
  `CatExpressionAnim` and merely *caps* the reactive one. Same pass: the full ground locomotion set
  (walk/run/jump/land/tumble/ladder/moonwalk/swim/seated) is now exposed, held against the game by a
  Harmony prefix on `AnimatedRenderable.UpdateAnimation`. See
  [`character-and-materials.md`](character-and-materials.md) and [`../ISSUES.md`](../ISSUES.md).
- **thug-life** — everything it binds to is intact, but rev 5315 moved the game to **Vulkan 1.4** and
  rev 5283 added **UI coverage culling**. Neither can be cleared from source.
  **FIXED 2026-08-23 (load-order, not a 5348 break):** the submod reported *"init failed: Object
  reference not set to an instance of an object"* because it built its Vulkan pipeline in
  `Initialize()`, i.e. from `[StarMapAllModsLoaded]` — which StarMap fires from a postfix on
  `ModLibrary.LoadAll()` (`KSA/Program.cs:897`), **before** the game creates
  `Program.OffscreenTarget` in `BuildRenderTargets()` (`:934`). GPU init is now lazy, on the first
  anchored entry. The same pass moved the per-frame MVP off `Program.GetMainCamera()` onto
  `Program.GetRenderCamera()`, since `RenderMainPass` runs once per visible viewport (the two
  crew-portrait viewports included). See [`pixel-grids-and-render.md`](pixel-grids-and-render.md)
  → thug-life.

**Added after the 5348 pass (written against 5348 directly):**
- **pyro** (2026-08-29) — standalone volumetric plumes. Built against the **current** decomp, not the
  stale in-repo copy: 5348's `PlumeData` (`ApparentExhaustVelocity`, `ThroatRadius`, `ThroatDensity`,
  `InletTemperature`) and the split of colour/noise out of `ExhaustInstance` into the per-template
  `ExhaustTemplateData` buffer are both already accounted for. Needs a live pass. See
  [`exhaust-plumes.md`](exhaust-plumes.md).
- **graffiti** (2026-08-30) — click-to-place projected PNG decals, a port of the gatOS sticker
  system (independently verified against 5348) re-hosted as a submod with cursor-click placement.
  All-public API surface (no string reflection); one Harmony postfix on
  `RenderTarget.ResolveAttachments`. Needs a live pass. See [`decals.md`](decals.md).
- **rocky-mcrock-face** (2026-08-31) — swaps the meshes/textures of the planetary ring system
  (Saturn's instanced rock field + 2D band) by mutating the public `PlanetaryRingsReference` data
  tree and forcing the game's own `Program.RebuildRenderer()` path. Written against the **current**
  5348 decomp (the multi-primitive `MeshReference` shape included). **No Harmony patches**; three
  reflection touchpoints, all soft-failing. Needs a live pass. See [`rings.md`](rings.md).
- **bloomin-onion** (2026-09-01) — defines brand-new planetary rings at runtime (painted or
  textured band, volumetric dust, rock field, full geometry) and applies them to any celestial by
  constructing a `PlanetaryRingsReference` tree, assigning it to the body template, refreshing the
  transparencies renderer's body list (public `PopulatePlanets()` + private `_anyRings`) and running
  `Program.RebuildRenderer()`. Painted bands are in-memory `TextureReference` subclasses bound via
  the game's own `Bind`. **No Harmony patches**; three reflection touchpoints (`_anyRings` is the
  only load-bearing one — a rename means no rings in ringless systems, never a crash). Needs a
  live pass. See [`rings.md`](rings.md) (bloomin-onion section).

**Removed by choice (not a game break):**
- **flexo** (robotics — articulated hinge/rotor Parts) — **deleted 2026-08-23.** `flexo.lib` compiled
  clean against 5348 and every patch target it depended on verified OK, but the approach never worked
  in-game (it leaned on undocumented `Part` transform/bounds cache-invalidation semantics) and will not
  be reattempted this way. Mod + `.lib`, both solution entries, the `unscience.csproj`
  `ProjectReference` and the supermod wiring are all removed. Two game surfaces went **unowned** with
  it: `PartModelRenderer.UpdateRenderData(Viewport, int)` (the keystone render hook flexo inherited
  from space-tape) and `OrbitLinePass.AddLineVertex`/`AddLineEnd` — neither needs re-verification on
  future builds. `GenericGizmo` survives under dont-stifle-me, and `PartTree.RecomputeStaticMass` stays
  on the string-reflection watchlist for kitchen-sink. kitchen-sink's *Flexo Part/Subpart Test* panels
  are named after it but are independent and were kept. See
  [`part-editor-and-robotics.md`](part-editor-and-robotics.md) → flexo.

**Added against 5348 (not a break — a response to one):**
- **dont-stifle-me** (new 2026-08-23) — revs in this span clamped top-level part scale to **0.5x–2x**
  and made scale-gizmo drags **uniform** (`VehicleEditor.ScaleBoundsFor` / `UpdateSelectedScale`,
  both new methods). The mod patches exactly those two (+ a per-frame `UpdateScaleGizmo` postfix) to
  restore the 5261 freedom behind a toggle, plus a `QuantizeScale` prefix that can turn off the new
  0.25 m scale snapping. All five `VehicleEditor` targets are by-name — see
  [`part-editor-and-robotics.md`](part-editor-and-robotics.md) → dont-stifle-me. Needs a live
  editor pass; not yet verified in-game.

**Favourable — no change needed:**
- **blinky / its-so-shiny** — rev 5326 moved `PowerManager.PopulateGraph` out of the constructor and
  behind the part window's "Draw Graph" toggle; power runs off the new `PartTree.ElectricalCircuits`.
  The O(N³) DFS both grid builders are architected around no longer runs during normal play. Their
  splitting optimisations are now dead weight (a separate cleanup, not a fix).

**Verified clean against 5348:**
- **The entire string-reflection watchlist resolves**, plus a **field-vs-property audit**: rev 5329
  turned `Module.Parent` into a property, which would silently break `GetField("Parent")` — no mod
  reflects on it, and `CharacterAvatar.Core` / `CharacterCore.Scale` are still fields.
- **Every Harmony patch-target signature unchanged** (line shifts only), including
  `Universe.ExecuteNextVehicleSolvers(double, SimStep)` despite its body being rewritten, and
  jplrepo's IL transpiler anchor (the sole `ImGui.SetCursorPosY` in `Program.DrawMenuBar`).
- **GPU byte layouts identical** — `PerInstanceData` and `MaterialData`.
- **Shaders** — `UnlitMesh.*` and `MeshIndirect.vert` byte-identical; `MeshIndirect.frag` changed by
  one line (portrait-light rename), leaving humble-arteest's `sampledColor` anchor and the
  `ENABLE_TEMPERATURE` LUT intact.
- **Coordinate frames unchanged** — rev 5280's `CelestialFrameMath` is a pure extraction.
  `Camera.cs` and `KinematicMeasurements.cs` are byte-identical (glass, geeforce clean).
- **`Part` removed `Sequence`/`SetSequence`/`ActivateInStage`/`DeactivateInStage`/`ScaleTotal`** —
  no unscience mod referenced any of them.

**Known-broken reconciliation:**
- **zippo `GetField("Color")`** — ✅ **now closed; the earlier scope text was stale.** The code reads
  `"ColorRgb"`, which is correct. Fixed by commit `07787ea`.
- **camera-controller-override `___Transform`** — ✅ closed at 5261, still closed.
- **humble-arteest Vehicle Paint / mesh-deform** — ❌ still dead by design since rev 4693; both
  self-disable. mesh-deform's `MeshIndirect.vert` struct anchor still does not match (the file is
  byte-identical to 5261, so this is unchanged, not a new regression).
- **blinky broken** — ✅ **closed 2026-08-23.** Root cause was the **propellant feed**, not just the
  part id: the 5018 fuel rewrite made a *declared feed connector* mandatory for the first hop out of a
  consumer part, so blinky's `Part`↔`Part` connection fed nothing and no pixel could ever light.
  blinky now connects `RocketCore.FeedConnectors` → fuel part, and both `EnginePartId` defaults moved
  from the removed `EngineA1` to `EngineA3`. See
  [`pixel-grids-and-render.md`](pixel-grids-and-render.md) → *Root cause of "blinky broken"*.
- **unscience never wires `IvaForceRender.Patch`** — ❌ still open.

**Not cleared statically — a live in-game pass is still required** for con-man's HUD scaling,
kitten-animations' reworked expressions and clip override, thug-life under Vulkan 1.4 + UI culling (its
init-order NRE is fixed, but the quad itself still needs eyes on it), blinky's grid timing and
repaired propellant feed, parts-now against the new load-time part validation (rev 5340), doh's MMU attachment
after its `AnimatedRenderable` retype, and the `ISSUES.md` error spam under the rewritten physics
bubbles. A green `dotnet build` does not cover these, and there is no test suite in this repo.
