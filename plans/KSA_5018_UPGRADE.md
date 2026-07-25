# KSA Upgrade — `2026.6.9.4750` → `2026.7.9.5018`

Impact review + remediation record for the game update landed on **2026-07-25**.
Supersedes [`FIX_CURRENT_GAPS_PLAN.md`](FIX_CURRENT_GAPS_PLAN.md) as the current baseline record;
that document remains the history for the `4680 → 4750` hop.

Authoritative per-touchpoint detail lives in [`../scope/`](../scope/FULL_SCOPE.md).

---

## Inputs

| Input | Value |
|---|---|
| **NEW** | `2026.7.9.5018` (2026-07-25) — `…/ksa-game-assemblies/current` |
| **OLD / baseline** | `2026.6.9.4750` — git tag `2026.6.9.4750` in the same repo |
| **Live install** | `C:\Program Files\Kitten Space Agency\` — `KSA.dll` **md5-identical** to `<NEW>/current/dll/KSA.dll`, so the default `dotnet build` compiled against 5018 |
| **Intermediate builds** | `4826`, `4892`, `4939`, `4980` — shipped but never separately verified, so the real span is 4750 → 5018 |

⚠ **The changelog does not cover the whole span.** No `version.json` on disk covers revs
**4751–4824** or **4827–4859** (~110 revisions). The source diff (433 changed decomp files, ~40k
inserted lines) covers that gap; the changelog scan alone does not.

## Framing — what this update actually changed

Three game-side rewrites dominate the span:

1. **Combustion → reaction model.** `CombustionObject`, `CombustionProcess`,
   `CombustionProcessTemplate`, `CombustionTable` deleted; replaced by
   `Reaction`/`FixedReaction`/`MixtureReaction`, `ReactantMix`, `Reactant`, thermal/fixed/mixture
   reaction tables, `GrainGeometry*`, `BurnRateLaw`, `SolidMotor*`.
2. **Fuel/resource feed wiring.** `FuelLink*`, `FuelPort`, `ConsumerFeedWiring`, `ConsumerRole`,
   `IResourceConsumer`, `ISubstanceStore`, `DrainContext`, `CoreDrainState`, `ResolvedConsumerFeeds`.
3. **Staging → resource groups.** `StageList.cs` and `Staging.cs` deleted; `ResourceGroups`,
   `ResourceGroupList`, `ResourceGroupsPanel`, `SequencePerformance*`, `StagingDrawContext` added.

Plus a substantial **gauge/HUD** rework (`ImGauge*`, `SerializedCanvas`, `LayoutSave*`, native
HudLayouts), a **navball marker** rework, and new **wetness/frost** vessel shading.

Despite that scale, the blast radius on unscience was **three compile errors and no new runtime
break** — the suite's coupling sits mostly on surfaces the rewrites left alone.

---

## Findings

### 🔴 P1 build-blocking — all three FIXED

| # | Mod | Site | Change | Fix |
|---|---|---|---|---|
| B1 | space-tape | `space-tape.lib/PartImporter.cs:84` | `PartTemplate.Tank` **removed**; tanks are `Tank.TemplateData` entries in `PartTemplate.Components` (a part may have several) | Iterate `Components`, import every `Tank.TemplateData`. `AsmbTankTemplate` and both subclasses unchanged, so `ImportTank` needed no edit. **Now supports multi-tank parts.** |
| B2 | doh | `doh.lib/Spawning/KittenSpawner.cs:281` | `SubstanceLibrary.TryGetCombustionProcess` **removed** with the whole `Combustion*` family. `Tank.ConfigureFor` now takes a `ReactantMix`. Mixture ratio left the asset id: `MMH_NTO_1.6` → `MMH_NTO` + `DefaultMixtureRatio` 1.65 | Added a `TryGetReactantMix(string)` helper: `SubstanceLibrary.TryGetReaction` → `MixtureReaction.AtMixtureRatio(DefaultMixtureRatio).ReactantMix`, falling back to `IReactantMix.ReactantMix` for fixed reactions |
| B3 | blinky | `blinky.lib/BlinkySubmod.cs:615` | `RocketCore.ResourceManager` **moved down** to the `Combustor` subclass; the new `SolidMotor` core has none. Base `RocketCore` now exposes `FeedConnectors`/`TryPrepareDrain`/`TryAccumulateDrain` | `core is Combustor combustor ? combustor.ResourceManager : null`. Diagnostics-only path, no functional impact |

### 🟠 P2 silent byte-layout change — guarded, no live break

- **`PerInstanceData` padding is now game-used.** `PartModel.PerInstanceData.packing2` and
  `PartModelDynamic.PerInstanceData.packing1` are both now **`public float Wetness`**, feeding a new
  `ENABLE_WETNESS` shader variant (`MeshIndirect.vert` `outWetness`@loc8), compiled when
  `GameSettings.Current.Graphics.VesselWater` is on. A sibling `ENABLE_FROST` variant was added too.
  - **humble-arteest Vehicle Paint** (`PaintB`) and **mesh-deform** (`DeformRadius`) map onto that
    slot, but both prefixes return early unless their `ShadersActive` probe is true — impossible on
    ≥4693 — so **nothing writes there today**. The hazard is strictly worse than the previously
    recorded `EmissiveColor` clobber; the guards are what keep it inert.
  - **humble-arteest Engine Emissive is unaffected** — it writes only `Temperature`/`TfiThickness`,
    whose offsets did not move.
  - Applied: renamed the trailing mirror-struct fields to `Wetness` and annotated them "game-used —
    do not write", so the boundary is visible to the next reader.

### 🟡 P3 behavioral — compile-clean, needs a live pass

- **con-man / marque vs the gauge-HUD rework.** rev 4940 added a **Hud dropdown to the file bar**,
  **moved the gauge enable/disable toggles there from the View dropdown**, and shipped native
  **HudLayouts** (save/load named gauge arrangements, default-markable, serialized to a `HUDLayouts`
  folder) — a first-party re-implementation of con-man's whole feature. rev 4959 added an
  `AlwaysEnabled` canvas flag con-man does not know about. revs 4919/5003 moved the sequence UI, burn
  UI and all pop-ups into the gauge-canvas system. All 7 of con-man's reflected fields still resolve.
- **`KeyframeAnimationModule.TimeGoal` now fans out to mirrored parts** (`ApplyToMirroredParts`) →
  red-alert may move symmetry partners it did not intend to.
- **Animation pipeline reworked**: `IAnimProcessor` gained `UpdateLocalPose(…, Span<TransformTRS>, …)`,
  `CatExpressionAnim.MixPose` → `MixPoseLocal`, `AnimatedRenderable` now builds a pose buffer.
  kitten-animations does not implement the interface and its `_expressionPose` cache-bust target is
  intact — but this is the **prime suspect for the `ISSUES.md` "always the same expression" report**.
- **space-tape tank XML round-trip**: `GameDataXmlSerializer` still emits tanks in the pre-5018 shape.
  Needs a save/load check that the emitted element lands where the new `Components` deserializer
  expects it — same class of runtime break as the docking-port R1 item.

### ✅ Verified clean (no action)

- **Every Harmony patch-target signature** is unchanged: `SuperMeshRenderSystem.RenderMainPass`,
  the three `*Module.UpdateRenderData`, `PartModel.AddInstance`, `PartModelDynamic.AddInstance`,
  `PartModelRenderer.UpdateRenderData(Viewport,int)`, `Universe.ExecuteNextVehicleSolvers`,
  `Program.DrawProgramMenusHook`, `OrbitController`/`FlyController.OnFrame`,
  `Camera.ChangeFieldOfView`/`UpdateProjection`, `Vehicle.GetWorldMatrix`/`UpdateRenderData`,
  `GameSettings.OnKeyAll`, `GaugeCanvas.OnDrawMenuBar`.
- **The full string-reflection watchlist** resolves. Notable near-misses worth recording:
  `GaugeCanvas._windowTitle` went `private` → `protected` (still `NonPublic`, still found), and all 7
  of con-man's fields are still declared **on `GaugeCanvas` itself** — a lift into `GaugeBase` would
  have silently broken `GetField`, which does not walk base types for non-public members.
- **`MaterialData`** byte-identical (`AlbedoColor` @16, stride 80) and **`GpuMaterialSystem`**
  unchanged → doh/humble-arteest Kitten Color safe. `CharacterAvatar.cs` unchanged; `CharacterCore`
  still a **struct** with `public float Scale` → garrys-torch's boxed write-back still works.
- **Shaders**: `UnlitMesh.vert`/`.frag`, `Common/MaterialSet.glsl` and `ModelPbr.frag` are
  byte-identical → thug-life and Kitten Color unaffected. The `#ifdef ENABLE_TEMPERATURE` LUT
  survives → Engine Emissive still works.
- **thug-life** reads `Program.OffScreenPass.SampleCount`/`.Pass` and
  `RenderingPresets.ReverseZDepthStencil` dynamically, so render-state churn is absorbed.
  `SuperMeshRenderSystem`'s +32 lines are all in the shadow/CSM path.
- **Rev 4914 control-module lockout is UI-layer only** — `EngineController.SetIsActive` and
  `ThrusterController.SetIsActive` are byte-identical, so blinky/its-so-shiny are not gated by it.
- **`Part.Stage`/`SetStage(int)`** survive the staging rewrite (rev 4873 changed only the internals:
  bulk-guarded rebuilds instead of one full resource-manager rebuild per part).

### 🔵 P4 — previously-recorded breaks confirmed already CLOSED

Re-checked per the skill's known-broken baseline; three had already been fixed in-repo and the fixes
are still correct against 5018. **Do not re-report these.**

- `Controller.___Transform` injector → now reads `__instance.Camera`
  (`CameraControllerOverridePatches.cs:54`); 5018 still has `public Camera Camera` and no `Transform`.
- zippo `GetField("Color")` → now `"ColorRgb"` (`LightController.cs:59,80`), matching 5018.
- supermod `IvaForceRender.Patch` → now wired (`unscience/Patcher.cs:66`, `Unpatch` at :100).

Still genuinely dead (pre-existing, by design change at rev 4693, **not** caused by 5018):
**humble-arteest Vehicle Paint** and **mesh-deform** — both self-detect and disable, and their
content probes remain correct on 5018.

---

## Remaining work

1. **Live in-game pass** — the static review cannot clear these:
   - con-man layouts vs the new Hud dropdown / HudLayouts / `AlwaysEnabled` canvases; marque's menu
     entries after the file-bar reorganisation.
   - kitten-animations expression cycling (does the `_expressionPose` bust still land before
     `UpdateLocalPose` reads it?) — ties to the standing `ISSUES.md` report.
   - thug-life quad visual correctness against the new particle/shadow/trail render work.
   - red-alert deploy/retract with symmetry parts present.
   - space-tape: import a multi-tank part, save, reload.
2. **`ISSUES.md` triage** — this update plausibly explains the kitten-animations entry; the blinky and
   eternal-flame entries are not explained by anything found here and need separate investigation.
