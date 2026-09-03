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

- **Cataloged against:** KSA build **`2026.9.7.5402`** (2026-09-02) — decomp at
  `…/ksa-game-assemblies/current/decomp`, assets at `…/ksa-game-assemblies/current/Content`.
  On macOS `Directory.Build.props` tier 2 resolves `KSAFolder` to
  `../ksa-game-assemblies/current/dll/`, so **`dotnet build` compiled against exactly this build** —
  there is no separate install to reconcile and no `KSAFolder` trap.
- **Diffed from:** KSA build **`2026.8.22.5348`** — the previously verified baseline, and also what is
  on disk as `ksa-game-assemblies_prev`. **Baseline == OLD**, a single hop.
- ⚠ **The changelog is NOT complete for this span.** NEW's `version.json` covers only `5400 → 5402`
  (one commit, rev 5401 — the thumbnail "data stride" fix). **Revisions 5349–5400 are in no
  `version.json` on disk**, so this pass was driven by the source diff (197 `KSA/*.cs` changed, 66
  added, 2 removed; 20 Content files) rather than a changelog.
- **How each touchpoint was verified:** (1) `dotnet build ksa-mod-experiments.slnx --no-incremental`
  against the `5402` reference DLLs — **63/63 projects, 0 warnings, 0 errors** after three compile
  breaks were fixed (`KSA.Viewport` → `IViewport`, `Cursor.InputRay` → `GetEgoRay`,
  `VolumetricExhaustRenderer.AddInstance` air-state args); (2) re-grep of the **entire**
  string-reflection watchlist plus a signature + body diff of **every** Harmony patch target across
  both trees, with a field-vs-property check on each reflected member; (3) byte-layout diff of
  `PerInstanceData`/`MaterialData`/`ExhaustInstance`, `diff -rq` + `cmp` of `Content/Core/Shaders`,
  and an id check of every referenced asset; (4) a read of every changed game file the area tables
  cite, for gating/semantic drift — the substitute for the missing changelog.
- ⚠ **A green build is a small fraction of the risk here.** The behavioral findings below **cannot be
  cleared statically** — see *Current status* for what still needs a live in-game pass.
- The repo's own `decomp/ksa` copy is **older still** (June 12) and is not authoritative — always diff
  against the `ksa-game-assemblies` git tags.
- Build is cross-platform since 2026-08-22 (see [`../plans/KSA_5261_UPGRADE.md`](../plans/KSA_5261_UPGRADE.md) §7).

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

## Current status against `5402` (summary)

Full detail lives in [`game-integration-surface.md`](game-integration-surface.md) §6; the remediation
record is in [`../plans/KSA_5402_UPGRADE.md`](../plans/KSA_5402_UPGRADE.md). The 5348→5402 span
(54 revisions, one logged) is dominated by a **viewport registry rework** (`Viewport` class → the
`IViewport`/`IGameViewport` interfaces, `Index` → `ShaderSlot`), **parachutes** with a cloth solver,
**part structural failure / debris**, an **exhaust plume deformation** rework, and a **light-switch
consolidation**. Blast radius on unscience: **three compile breaks (fixed), four behavioral watch
items, one game-side regression.**

**Build-blocking — fixed this pass:**
- **`KSA.Viewport` removed** → six one-line `IViewport` retypes in ksa-abstractions (`IvaForceRender`),
  dont-stifle-me, i-feel-seen, parts-now and graffiti (`Index` → `ShaderSlot`).
- **`Cursor.InputRay` removed** → graffiti uses `Cursor.GetEgoRay(Program.MainViewport)`; the ray is
  now same-frame rather than one frame stale.
- **`VolumetricExhaustRenderer.AddInstance` gained `airVelocity`/`airDensity`** → pyro computes them
  the way `Vehicle.AddVolumetricExhaustInstances` does.

**Behavioral — compile-clean, needs a live pass before any code change:**
- **pyro (and the game) — refraction is dead in 5402.** Nothing sets `_hasRefractionInstances` any
  more, so pyro's Refraction slider is inert. Game-side; confirm on a stock engine.
- **garrys-torch vs part failure.** Overlapping welded vehicles can now shed debris or be destroyed;
  `WeldEngine.UpdateWeld` has no disposed guard. Recommended hardening is recorded, not applied.
- **graffiti terrain decals** — the accurate terrain-height path now derives from `MeanRadius`.
- **IvaForceRender** — `PartModel.AddInstance` now early-returns for viewports without
  `RenderPartModels`; the postfix still runs. Dormant (every viewport has the flag); mirror recommended.
- **thug-life** — `RenderMainPass` now also runs per secondary viewport; the quad still has never had
  a live pass on any build since 5261.

**Verified clean against 5402:** the **entire string-reflection watchlist** (same kind and type;
con-man's seven fields byte-identical at the same lines), **every Harmony target signature** apart
from the `IViewport` retype (all single overloads; `GameSettings.cs` byte-identical;
`ExecuteNextVehicleSolvers` body identical), **`PerInstanceData`/`MaterialData` byte-identical**,
`MeshIndirect.*`/`UnlitMesh.*` byte-identical, frames and telemetry types unchanged, no `Brutal*` drift.

**Carried forward (unchanged by this build):** con-man vs global Hud Scale (5348); kitten-animations
rework and parts-now load-time validation still want a live pass; humble-arteest Vehicle Paint and
mesh-deform remain dead by design (4693). `___Transform`, zippo `"Color"`, and the "supermod never
wires `IvaForceRender`" notes were stale and are closed. pyro, graffiti, rocky-mcrock-face,
bloomin-onion and dont-stifle-me have still never been exercised in-game.

**What still needs a live pass:** F11 smoke; pyro plume bend in atmosphere + heat-haze check;
garrys-torch weld with crash-tolerance log watch; graffiti vehicle + terrain decal placement;
parts-now runtime part thumbnail (rev 5401); dont-stifle-me scale-then-attach; kiwis-marbles weld near
a deployed chute; glass with thumbnails open; the standing thug-life / humble-arteest / blinky /
its-so-shiny render checks. A green `dotnet build` does not cover these, and there is no test suite.
