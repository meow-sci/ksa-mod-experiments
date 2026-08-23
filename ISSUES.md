- blinky broken
- eternal flame broken (seems like refill not working while engines are lit.. maybe race condition on data mutations since DMZ changes?)
- flexo throws errors but works
- garry's torch - works but throws errors
- humble arteest vehicle paint broken
- kitten animations don't properly play each one, always the same
- 

- new zippo feature .. refill electricity

---

## Triage notes — KSA `2026.8.22.5348` upgrade (2026-08-23)

Full review: [`plans/KSA_5348_UPGRADE.md`](plans/KSA_5348_UPGRADE.md). One compile break this pass
(space-tape, resolved by **removing the mod** — it was defunct); the build is green, 55/55 projects,
0 warnings, 0 errors. Everything below still needs a live pass.

- **kitten animations always the same expression** — 🔍 **FIRST CONCRETE MECHANISM FOUND.**
  Rev 5278 (*"Fixed seated crew and EVA crew animation updating once per visible viewport instead of
  once per frame"*) added `private ulong _lastPoseUpdateFrameNumber` to `KSA/AnimatedRenderable.cs` and
  changed the pose gate from `if (!FreezeAnimation)` to
  `if (Program.FrameNumber != _lastPoseUpdateFrameNumber)`. kitten-animations busts
  `CatExpressionAnim._expressionPose` to force a re-pose — **a second pose evaluation in the same frame
  is now silently dropped.** `CatExpressionAnim` is byte-identical and `_expressionPose` still resolves,
  so the reflection is not the problem; the *timing* is. **Test this first.** If confirmed, the fix is
  to trigger the expression change on a frame boundary rather than inline.
- **con-man (new this pass)** — ⚠️ rev 5293 added a global **Hud Scale** applied *after* per-canvas
  scale. `GaugeCanvas` now divides by `GameSettings.GetGaugeScale()` and wraps draws in
  `ConsoleStyle.BeginGaugeHostScope`. con-man's saved `_windowPosition`/`_windowSize`/`_customScale` are
  in a space it doesn't model, so **layouts saved at one Hud Scale will restore wrong at another.**
  Stacks on the still-open rev-5201 context-visibility gate. Plan doc §4.1.
- **blinky broken** — ❌ **still open, and the previously identified root cause was only half-fixed.**
  `blinky.lib/BlinkySubmod.cs:35` was moved to `CorePropulsionA_Prefab_EngineA3`, but
  **`blinky.lib/LcdGridConfig.cs:47` — the persisted default — is still `EngineA1`**, an id that does not
  exist in Content at 5117, 5168, 5261 or 5348. `ModLibrary.Get` throws. Fix `LcdGridConfig` too.
  Good news: rev 5326 moved `PowerManager.PopulateGraph` behind the part window's "Draw Graph" toggle,
  so blinky's grid rebuilds should be **dramatically faster** — re-measure.
- **eternal flame refill not working while engines are lit** — no new evidence. `Battery.cs` is
  **byte-identical**, and `Vehicle.RefillConsumables()` / `Battery.Refill(ref BatteryState)` are
  unchanged. The rev-5326 power rework touched circuit construction and draw, not refill. The 5261 leads
  (rev 5227 ×10 battery capacity; revs 5252/5253 control lockout) still stand.
- **garry's torch / flexo throw errors** — no signature drift in either mod's patch targets. New
  suspects this span: the **physics-bubble merge/split rewrite** (revs 5331/5339 — bubble ownership moved
  into `VehicleUpdateTask`, merge checks multithreaded off the render thread) and **ground-clutter
  collisions** (revs 5263/5303/5307, default off, destroy on >25 J/kg impact). garrys-torch teleports a
  vehicle every frame, so both are plausible interactions. **Re-test: the spam may have changed shape.**
- **humble arteest vehicle paint broken** — unchanged: still dead by design since rev 4693, still
  self-disables. `MeshIndirect.frag` changed by one line (portrait-light rename) and the
  `vec3 sampledColor` anchor still resolves. Engine Emissive and Kitten Color unaffected.
- **new zippo feature — refill electricity** — unchanged; still a feature request, not a break.
  (Separately: zippo's long-recorded `GetField("Color")` bug is **closed** — the code reads `"ColorRgb"`,
  which is correct. The scope docs describing it as broken were stale and have been fixed.)
- **space-tape** — 🗑️ **removed from the repo this pass.** Rev 5329 deleted `PartTemplate.Decoupler`;
  the mod was defunct, so it and its `.lib` were deleted rather than ported. Note the stale deploy folder
  `~/repos/meow-sci/mods/mods/space-tape/` will keep loading the old DLL until deleted by hand.

---

## Triage notes — KSA `2026.8.19.5261` upgrade (2026-08-11)

Full review: [`plans/KSA_5261_UPGRADE.md`](plans/KSA_5261_UPGRADE.md). Five compile breaks were fixed
this pass (build is green, 55/55 projects); everything below still needs a live pass.

- **blinky broken** — 🔍 **PROBABLE ROOT CAUSE FOUND.** blinky's default `EnginePartId` is
  `"CorePropulsionA_Prefab_EngineA1"` (`blinky.lib/LcdGridConfig.cs:47`, `BlinkySubmod.cs:51`), and
  **that part id no longer exists in the game.** It was removed from
  `Content/Core/CorePropulsionAAssets.xml` between builds 5018 and 5117, so it is absent at 5117,
  5168 and 5261. Only `EngineA2`–`EngineA6` remain (`A2` = "LR91 Sea", `A3`/`A6` = "LR91 Vac",
  `A4` = "VTR-10", `A5` = "LR91 Vac + Verniers"). `ModLibrary.Get` throws on a missing id.
  The 5117 triage missed this because it only checked blinky's *patch targets* (all byte-identical)
  and never its *asset ids*. **Suggested fix: default to `CorePropulsionA_Prefab_EngineA2`.**
  Not changed yet — it is a behavioral default, so confirm the preferred engine first.
- **garry's torch throws errors** — the vehicle threading model was **rewritten** this span (revs
  5208–5216: `DynamicWorkerPool`, `ParallelBatch`, per-vehicle parallel jobs, object-pooled
  `PhysicsBubble`/`ConstraintSim`; plus rev 5237's stale-resource-handle crash fix). The mod's
  solver drain was ported from `JobSystems.VehicleSolvers.Wait()` to `JobSystems.VehicleSolver.Wait()`
  and is provably still complete, but **re-test: the error spam may have changed shape or gone.**
- **kitten animations always the same expression** — `CatExpressionAnim._expressionPose` still
  resolves and the type is byte-identical, so the reflection is not the cause. New suspects this
  span: revs 5203/5233/5235/5244/5249 added ladder, jump, tumble and landing anims and changed
  blend/freeze behaviour ("anim frozen before the blend completed").
- **eternal flame refill not working while engines are lit** — `Vehicle.RefillConsumables()` and
  `Battery.Refill` are still signature-identical. Two new leads: rev 5227 made **all batteries ×10
  maximum capacity** (so a fixed-rate refill now looks far slower), and revs 5252/5253 tightened
  control lockout (`ControlsLockout`, engine shutdown blocked without a control module).
- **humble arteest vehicle paint broken** — unchanged: still dead by design since rev 4693. Both GLSL
  anchors still resolve. Engine Emissive and Kitten Color are **unaffected** by this build; the one
  `MeshIndirect.frag` change (rev 5196 portrait lights) does not touch the anchor.
- **flexo throws errors but works** — no signature drift in its patch targets this span. New
  editor-side suspects: bendable fuel-line hoses (5171), roll-while-snapped (5258), and the map grid
  moving out of screen space (5256/5257).
- **con-man (new)** — ⚠️ gauges enabled in con-man may now silently refuse to draw: rev 5201 added a
  per-canvas visibility **context** system, and `_enabled` is no longer the only gate. See the plan
  doc §4.1.

---

## Triage notes — KSA `2026.8.3.5117` upgrade (2026-08-01)

Nothing above was **confirmed** fixed or explained by this build; all entries still need a live pass.
Recorded here so the next triage doesn't re-derive it. Full review:
[`plans/KSA_5117_UPGRADE.md`](plans/KSA_5117_UPGRADE.md).

- **eternal flame refill** — the game changed refill behavior in rev 5021: *"Prevented tanks from
  being unexpectedly refilled when loading vehicles, saving vehicles, or exiting the editor… swapping
  engines, etc. will still cause refills"*, plus an explicit **"Refill Consumables"** button in the
  editor. `Vehicle.RefillConsumables()` and `Battery.Refill` are signature-identical, so this is not
  a break — but it is a plausible interaction with the reported symptom and worth testing first.
  Note also rev 5114 (*flight computer now aware when engines run out of propellant*) touches the
  same "while engines are lit" window.
- **humble arteest vehicle paint** — this entry predates the 2026-07-25 rebuild that made paint work
  again on 5018. Both GLSL anchors still resolve on 5117 (verified statically), so **re-test before
  assuming it is still broken**; if it is, the cause is new.
- **kitten animations always the same expression** — prime suspect remains the 5018-era animation
  pipeline rework, not this build: `CatExpressionAnim` and `AnimatedRenderable` are byte-identical
  5018→5117 and `_expressionPose` still resolves.
- **garry's torch / flexo error spam** — no signature drift in either mod's patch targets this span.
  New in 5117: rev 5115 vehicle destruction on structural g-limit / dynamic pressure, which torch's
  per-frame `Vehicle.Teleport` could now trip. Worth checking whether the errors changed shape.
- **blinky** — `EngineController.SetIsActive`, `PartTree.CreateFromNewPartTree` and the three
  `*Module.UpdateRenderData` patch targets are all byte-identical this span.
