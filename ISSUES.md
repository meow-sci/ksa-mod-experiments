- blinky broken
- eternal flame broken (seems like refill not working while engines are lit.. maybe race condition on data mutations since DMZ changes?)
- flexo throws errors but works
- garry's torch - works but throws errors
- humble arteest vehicle paint broken
- kitten animations don't properly play each one, always the same
- 

- new zippo feature .. refill electricity

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
