# KSA `2026.8.19.5261` upgrade — impact review & remediation record

**Reviewed:** 2026-08-11 · **Span:** `2026.8.3.5117` (recorded baseline) → `2026.8.19.5261` (~140 revs)
**Trees:** NEW `ksa-game-assemblies` @ `2026.8.19.5261`; OLD `ksa-game-assemblies_prev` @ `2026.8.5.5168`
**Install:** `C:\Program Files\Kitten Space Agency\KSA.dll` = `2026.8.19.5261` — **matches NEW**, so
`dotnet build` compiled against the build under review.

> **Changelog coverage is complete for this span.** `<BASELINE>` 5117 == OLD's `fromRevision`, so the
> two `version.json` files cover revs **5118–5168** and **5169–5258** contiguously. No revision went
> unreviewed — unlike the 4750→5018 era.

---

## 1. Result

`dotnet build ksa-mod-experiments.slnx` → **all 55 projects, 0 warnings, 0 errors** after the fixes below.

**Five compile breaks in four projects**, plus one confirmed behavioral break and several watch items.
Two of the compile breaks did **not** originate in this build — they landed in the unvalidated
**5118–5168** window (no upgrade pass was run for 5168), and are recorded as such.

| # | Mod | Change class | New in 5261? |
|---|---|---|---|
| 1 | `ksa-abstractions.lib` | renamed type + method (`SimTime`→`UniverseTime`) | ✅ yes (rev 5211) |
| 2 | `garrys-torch.lib` (×2) | renamed/split field (`JobSystems.VehicleSolvers`) | ✅ yes (revs 5208–5216) |
| 3 | `doh.lib` | renamed method (`Universe.GetElapsedSimTime`) | ✅ yes (rev 5211) |
| 4 | `parts-now.lib` | renamed member (`SampledReadFragment`→`SampledReadF`) | ❌ **5118–5168** |
| 5 | `thug-life.lib` | **removed member + architecture change** (`Program.OffScreenPass`) | ❌ **5118–5168** |

---

## 2. Fixes applied

### 2.1 `SimTime` → `UniverseTime` (rev 5211) — `ksa-abstractions.lib`, `doh.lib`, `garrys-torch.lib`

Rev 5211: *"Replaced SimTime with UniverseTime, backed by 128-bit nanoseconds."* A clean rename with a
compatible surface:

| OLD (≤5168) | NEW (5261) |
|---|---|
| `struct SimTime` (double seconds) | `struct UniverseTime` (`Int128` nanoseconds) |
| `Universe.GetElapsedSimTime()` | `Universe.GetElapsedTime()` |
| `.Seconds()` → `double` | `.Seconds()` → `double` **(unchanged)** |

Because every consumer either calls `.Seconds()` or passes the value straight into
`Orbit.CreateFromStateCci(parent, <time>, …)`, no arithmetic or precision handling needed changing.

- `ksa-abstractions.lib/SimTimeProvider.cs:9` — return type + underlying call.
  The wrapper keeps the name `SimTimeProvider.GetElapsedTime()`; renaming the class would churn
  geeforce, steely-eyed (×2) and kiwis-marbles for no functional gain. **Optional follow-up**, not done.
- `doh.lib/Spawning/KittenSpawner.cs:167,257` — `GetElapsedSimTime()` → `GetElapsedTime()`.
- `garrys-torch.lib/WeldEngine.cs:119` — local `SimTime` → `UniverseTime` (still
  `Universe.GetJobSimStep(...).NextTime`, whose type followed the rename).

### 2.2 `JobSystems.VehicleSolvers` split (revs 5208–5216) — `garrys-torch.lib`

The vehicle threading model was rebuilt (`DynamicWorkerPool`, `ParallelBatch`, `VehicleUpdateTask`,
`PhysicsBubble` islands, `BepuWorkerDispatcher`). The single multi-runner scheduler became two objects:

| OLD | NEW |
|---|---|
| `VehicleSolvers` — `JobScheduler(0.75×count)`, priority Highest | `VehicleSolver` — `JobScheduler(1)` orchestrator |
| — | `VehicleWorkerPool` — `DynamicWorkerPool(count−1)` parallel islands |

`garrys-torch.lib/GarrysTorchSubmod.cs:93` drains in-flight solver work before mutating vehicle state;
that drain is **correctness-critical** (it prevents `Collection was modified` inside
`VehicleUpdateTask` and `SnapToLeader body/origin time mismatch`).

**Waiting on the orchestrator alone is the correct and complete drain.** `DynamicWorkerPool` exposes
no `Wait()`; it is only ever driven through scoped `ParallelBatch()` fork/join blocks
(`using (…)`) inside `VehicleUpdateTask` / `PhysicsBubble` / `Universe.ApplyVehicleSolvers`, so all
pool work is joined before the queued `_vehicleUpdateTask` completes. **The game itself drains the
same way** — `Universe.DeserializeSave` calls `JobSystems.VehicleSolver.Wait()`.
Fix: `VehicleSolvers.Wait()` → `VehicleSolver.Wait()`, with the reasoning recorded at the call site.

> ⚠ **Needs a live pass.** The signature is right and the drain is provably complete, but the
> *parallelism model* underneath changed (per-vehicle parallel batch jobs, object-pooled
> `PhysicsBubble`/`ConstraintSim`, rev 5237 stale-handle crash fix). garrys-torch mutates vehicle
> state from outside the solver, so the error spam recorded in `ISSUES.md` must be re-checked in game.

### 2.3 `SampledReadFragment` → `SampledReadF` — `parts-now.lib` *(pre-existing, 5118–5168)*

`ImageBarrierInfo.Presets` was swept for abbreviated names (`…Vertex`→`…V`, `…Fragment`→`…F`,
`…Compute`→`…C`). The preset list is **byte-identical between OLD and NEW**; only the 5117→5168 hop
renamed them. Same layout/access/stage, so this is a pure rename with no semantic change.
`parts-now.lib/Runtime/ThumbnailReadback.cs:56,84`.

### 2.4 `Program.OffScreenPass` removed — `thug-life.lib` *(pre-existing, 5118–5168)* — **architecture change**

The most substantive fix. `Program.OffScreenPass` (`RenderPassState`, exposing `.Pass` and
`.SampleCount`) existed at 5117 and is **gone** in 5168/5261. The offscreen target is now
`Program.OffscreenTarget` (`RenderTarget : IRenderPassInfo`) — and it *is* `PassContext.MainOpaquePass`.

This is not a rename: **the game migrated the main scene pass from classic Vulkan render passes to
dynamic rendering.** `RenderTarget` has no `.Pass` and no `.SampleCount`, and `IRenderPassInfo` now
exposes exactly one method. `RenderTarget.SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)`:

- chains a `VkPipelineRenderingCreateInfo` (colour/depth/stencil formats) onto `pNext`,
- sets `info.RenderPass = VkRenderPass.NullHandle`,
- fills `MultisampleState` with the target's `Samples`,
- supplies `ViewportState` if absent.

`thug-life.lib/ThugLifeQuadRenderer.cs:110` now builds the pipeline **without** `RenderPass`,
`Subpass` or a hand-rolled `MultisampleState`, then calls
`Program.OffscreenTarget.SetupGraphicsPipeline(ref info)` immediately before
`CreateGraphicsPipeline` — mirroring the game's own main-pass pipelines (`GenericMeshRenderer:305`,
`PartModelRenderer:184,269`, `PartModelGlass:269`). The call must stay immediately before creation:
the structures it points `pNext` at are owned and overwritten by the `RenderTarget` on each call.

> ⚠ **Needs a live pass.** This compiles and matches the game's pattern exactly, but thug-life drives
> its own Vulkan pipeline; only an in-game look confirms the quad still rasterizes correctly (F12).

---

## 3. Verified clean (no change needed)

- **String-reflection watchlist (master index §4) — every entry re-verified against NEW.** All resolve,
  including `Camera._fovRadians`, the full 7-field `GaugeCanvas` cluster (still declared on
  `GaugeCanvas` itself), the `KittenEva._renderable → _characterAvatar → CharacterAvatar.Core →
  CharacterCore.Scale` chain, `CatExpressionAnim._expressionPose`, `"KSA.LightModule+TemplateData"`,
  the doh/humble-arteest render bridge, `ShaderReference.*` + `RenderCore.ShaderModuleUtils.FromFile`,
  all seven `ModLibrary.All*` fields, `SerializedCollection._collection`,
  `VehicleEditor._editorTagLookup`, `Part.ResetCachedPosMatrixValues`, `PartTree.RecomputeStaticMass`,
  `ResourceManagerBase.NearestToFurtherestNodeSameStage`, `GameSettings.OnKeyAll`.
- **Every Harmony patch target signature is unchanged** (line shifts only): `RenderMainPass`,
  `PartModel.AddInstance`, `PartModelRenderer.UpdateRenderData(Viewport,int)`,
  `PartTree.CreateFromNewPartTree`, `EngineController.SetIsActive`, `Vehicle.Teleport`,
  `Vehicle.RefillConsumables`, `GaugeCanvas.OnDrawMenuBar`, `Program.DrawMenuBar(Viewport,int)`,
  `Universe.ExecuteNextVehicleSolvers(double, SimStep)`, `ImGui.SetCursorPosY`, `PartModel` ctor.
- **GPU byte layouts identical** — `PartModel.PerInstanceData` and `MaterialData` (doh's
  `handle*80+16` writes and humble-arteest's padding-byte hijack are safe).
- **Shaders** — `UnlitMesh.vert/.frag` (thug-life) and `MeshIndirect.vert` (mesh-deform) **identical**.
  `MeshIndirect.frag` changed by exactly **one line** (rev 5196 portrait lights:
  `lightColor += SamplePortraitLight(...)`). humble-arteest's `vec3 sampledColor …;` anchor and its
  `inStateFlags` guard both still resolve; the `ENABLE_TEMPERATURE` LUT still lives in that file, so
  **Engine Emissive is unaffected**.
- **Suite load path intact** — `Program.OnDrawUiFrame`, `OnDrawUiViewports`, `OnFrame` and
  `DrawProgramMenusHook` all still present, so StarMap still has its seams.
- **`KittenEva` changes are purely additive** (ladders, jump/tumble, `KittenControlMode`); no member
  garrys-torch/doh/kitten-animations reads was removed.
- **Asset ids** — `LightPart`, the four shader ids, and `EngineA2…A6` all still present.

---

## 4. Behavioral findings — no compile error, **code change likely required**

### 4.1 con-man — gauge visibility is no longer con-man's alone to decide ⚠ **highest-value finding**

Rev 5201 added a per-canvas visibility context system. The draw gate is now:

```csharp
if (!_enabled || !IsContextVisible() || (this == Program.CrewPortraitsCanvas && !CrewPortraitPanel.HasOccupants))
```

con-man drives gauges by reflecting on `_enabled` (`con-man.lib/GaugeStateAccessor.cs:29,63,68`). That
field still resolves — but **setting it `true` is no longer sufficient to show a canvas.** A canvas
carrying `VisibleInContext` flags stays hidden unless *every* flag matches the controlled vehicle
(`Burn`, `Engines`, `EVA`, `Vehicle`, `Sequence`, `Target`, `IVA`, `Thrusters`, `Atmosphere`).

There is also now a **second source of truth** con-man doesn't know about:
`GameSettings.Current.GaugeContextOverrides`, persisted to `settings.toml` and applied by
`GaugeCanvas.ApplyContextOverrides()`, plus a stock "Context Assignments" HUD window.

Likely user-visible symptom: enabling a gauge in con-man appears to do nothing.
Candidate fix: read/clear `VisibleInContext` alongside `_enabled`, or surface the context flags in
con-man's UI. **Not implemented — needs a decision on how con-man should coexist with the stock system.**

Related: revs 5179/5193/5201/5229/5232/5246 added new canvases (EVA Control, Crew Portraits,
Resources). con-man enumerates `_canvases` dynamically so it will see them, but saved layouts predate them.

### 4.2 blinky — default engine part id does not exist *(pre-existing, predates the 5117 baseline)*

`blinky.lib/LcdGridConfig.cs:47` and `BlinkySubmod.cs:51` default to
`"CorePropulsionA_Prefab_EngineA1"`. That id was removed from `Content/Core/CorePropulsionAAssets.xml`
between **5018 and 5117** — it is absent at 5117, 5168 and 5261. Only `EngineA2…EngineA6` exist
(`EngineA2` = "LR91 Sea", `A3`/`A6` = "LR91 Vac", `A4` = "VTR-10", `A5` = "LR91 Vac + Verniers").

A missing id makes `ModLibrary.Get` throw, which is a concrete candidate explanation for the
**"blinky broken"** entry in `ISSUES.md`. The previous (5117) triage checked blinky's *patch targets*
— all byte-identical — but never checked the *part id*. **Recommend changing the default to
`EngineA2`**; not done here because it is a behavioral change outside the compile-blocking scope.

### 4.3 Watch items (changelog hits, no symbol moved)

| Revs | Change | Mods at risk |
|---|---|---|
| 5252, 5253 | Engine shutdown blocked without a control module; **all** vehicle input locked out when not controllable (`ControlsLockout`) | unladen-swallow RPC, blinky, its-so-shiny, eternal-flame |
| 5227 | **All batteries ×10 maximum capacity** (Content, not code) | eternal-flame, its-so-shiny, space-tape, red-alert |
| 5203, 5233, 5244, 5249, 5235 | Ladder grab/board, jump/tumble/landing anims, new anim states | kitten-animations (the "always the same expression" issue), doh, garrys-torch |
| 5221 | Cursor hover / orbit-line arbitration rework, `CursorTarget` | marque, kiwis-marbles, space-tape, flexo |
| 5256, 5257 | Map grid moved out of screen space; optional per-body grid plane | space-tape (grid via `OrbitLinePass`), flexo |
| 5191, 5222, 5245, 5255 | Docking camera orientation/standoff; portrait camera FOV/side view | glass, camera-controller-override |
| 5171, 5258, 5202, 5225, 5238, 5239 | Bendable fuel-line hoses; roll (Q/E) while snapped; connector flags/orientation fixes | flexo, space-tape, parts-now |
| 5185 | `MeshColliderTemplate` / `ConvexHullColliderTemplate` added | space-tape XML emitters |
| 5230, 5196, 5204, 5236, 5241, 5243 | Fur render fix, portrait lights, prepass/AO/validation fixes, exhaust perf rework | doh, humble-arteest, thug-life |
| 5176, 5199, 5217, 5248 | RCS double-execution fix, g-force throttle limiting, departure/transfer burn accuracy | average-twr, geeforce, steely-eyed, marque |

Rev 5242 (*"row-major matrix convention for quaternion to/from mat3"*) was checked specifically:
**no `Brutal.Numerics` file changed** between OLD and NEW, so the shared `doubleQuat`/`double4x4`
types garrys-torch, kiwis-marbles, thug-life and camera-controller-override depend on are untouched.

---

## 5. Known-broken baseline — reconciliation

| Item | Status |
|---|---|
| **camera-controller-override `___Transform`** | ✅ **FIXED IN REPO** — `CameraControllerOverridePatches.cs:42-54` now uses `__instance.Camera` instead of the field injector. `scope/` still described it as broken; corrected. |
| **zippo `GetField("Color")`** | ❌ **STILL BROKEN** — the field is `ColorRgb` (with `[XmlElement("Color")]`), so the lookup still returns null. Unchanged by this build. |
| **humble-arteest Vehicle Paint** | ❌ Still dead by design (rev 4693 `CompileVariantWithCustomOptions`). Anchors still resolve; self-disables. Engine Emissive + Kitten Color unaffected. |
| **mesh-deform** | ❌ Same root cause; `MeshIndirect.vert` **identical** this span; self-disables on ≥4693. |
| **space-tape API drift** | ✅ Compiles clean at 5261 (fixed in the 5117 pass). Editor-schema watch items remain. |
| **garrys-torch CS8604** | ✅ No longer present — build is 0 warnings / 0 errors. |
| **unscience `IvaForceRender.Patch` not wired** | Unchanged — not re-examined this pass. |
| **blinky** | 🔍 **Explained** — see §4.2 (missing `EngineA1` part id). |

---

## 6. What a green build does *not* cover

`dotnet build` is the whole automated verification story in this repo — there are no test projects.
These need a **live in-game pass** (F11 for the supermod; thug-life F12, doh F8, kiwis-marbles F9):

1. **thug-life** — dynamic-rendering pipeline rebuild (§2.4). Does the quad still draw?
2. **garrys-torch** — solver drain under the new parallel model (§2.2). Does the error spam persist?
3. **con-man** — gauge context gating (§4.1).
4. **blinky** — after an `EngineA1` → `EngineA2` default change (§4.2).
5. **doh / humble-arteest** — fur (rev 5230) and Engine Emissive after the portrait-light shader edit.
6. **kitten-animations** — against the new ladder/jump/tumble anim states.
