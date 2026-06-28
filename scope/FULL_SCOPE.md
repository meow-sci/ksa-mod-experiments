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

- **Cataloged against:** KSA build **`2026.6.9.4750`** (current install) — decomp at
  `…/ksa-game-assemblies/current/decomp`, assets at `…/ksa-game-assemblies/current/Content`.
- **Diffed from:** KSA build **`2026.6.8.4680`** (previous) — `…/ksa-game-assemblies_2026.6.8.4680/current/…`.
- **How each touchpoint was verified:** (1) `dotnet build` against the live `4750` game DLLs to catch
  *typed* breaks; (2) grep of every touchpoint in **both** decomp trees to catch *string/reflection*
  breaks the compiler can't see; (3) cross-reference against the `version.json` changelog (revs
  4681–4748) for *behavioral* changes that don't move a symbol.
- The repo's own `decomp/ksa` copy is **older than 4680** and is not authoritative — always diff
  against the two `ksa-game-assemblies*` trees above.

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
4. **Re-check shaders & per-instance layout.** Runtime-recompiled GLSL and byte-offset struct hacks
   (humble-arteest, mesh-deform) break when the game's shader sources change even though the C#
   compiles. See [`game-integration-surface.md`](game-integration-surface.md) → *Shaders & assets*.
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
| [`character-and-materials.md`](character-and-materials.md) | doh, humble-arteest, kitten-animations | `GpuMaterialSystem.BigBuffer`, `KittenEva`/`EVADoor`, `PerInstanceData` byte hijack, `CatExpressionAnim`; **humble-arteest paint break** |
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

## Current status against `4750` (summary)

Full detail and the remediation plan live in
[`../plans/FIX_CURRENT_GAPS_PLAN.md`](../plans/FIX_CURRENT_GAPS_PLAN.md). Headline:

**Build-blocking (must fix to compile against the live game):**
- **space-tape.lib** — 4 compile-error groups (thumbnail API `CreateImGuiThumbnail`→`GetOrCreateImGuiTexture`;
  energy/power `float`→`double`; `DockingPortTemplate.Force`→`PushoffImpulse`; `ComputeBoundingSphereRadius`
  now needs `out float3`) plus a docking-port XML writer runtime break. *Mostly accumulated API drift
  that predates 4680; only the energy/power change is strictly from this update.*
- **garrys-torch.lib** — one nullable break (`ImString.AppendFormatted` non-nullable after the rev 4729
  Brutal package bump). *Genuinely from this update.*

**Runtime/asset breaks (compile-clean, broken in game) — now guarded (Phase 2 done):**
- **humble-arteest** Vehicle Paint — runtime GLSL shader-swap is inert. Root cause is deeper than the
  missing anchors: rev 4693 moved part-color compilation to
  `ShaderReference.CompileVariantWithCustomOptions()`, which recompiles MeshIndirect from disk per
  `ENABLE_*` pipeline variant and **ignores `ShaderReference.Shader`**, so the mod's module-swap can
  never take effect (even with correct anchors). The feature now **self-detects and disables** with a
  clear "unavailable on this build" notice, and no longer clobbers `PerInstanceData.EmissiveColor`.
  Engine Emissive and Kitten Color are unaffected. Reviving paint is a redesign (Harmony-patch the
  shared part-shader compilation, blast radius = every part) needing in-game GPU iteration — see the plan.
- **mesh-deform** (standalone) — same root cause; now **self-detects and disables** on `≥4693`.

**Pre-existing latent bugs surfaced by the audit (not caused by 4750):**
- **zippo** color control inert (reflects `"Color"`; field is `ColorRgb`).
- **camera-controller-override** `___Transform` Harmony field injector targets a non-existent field
  (camera is `Camera`); likely makes the animation override inert — *needs runtime confirmation*; also
  manifests as an inert `POST /camera/animate` in unladen-swallow.
- humble-arteest Vehicle Paint clobber of the now-used `PerInstanceData.EmissiveColor` byte — **fixed**
  (Phase 2): the `AddInstance` prefix now no-ops unless paint is genuinely active (impossible on `≥4693`).
- supermod `Patcher.cs` never wires `IvaForceRender.Patch` (kitchen-sink IVA force-render is partial
  inside the supermod).

**Behavioral watch items (changed semantics, not symbol breaks):** `Vehicle.IsControllable` gating
(rev 4699); editor tag/category schema — "Interstage" removed, "Stages"→"Resource Groups", tags moved
to XML (rev 4731/4732/4741); face-snapping/connector rules (rev 4687–4740); part-size XML (rev 4721).

**Everything else verified clean** — all other mods' typed + reflection touchpoints exist in 4750 with
identical signatures (only line numbers shifted).
