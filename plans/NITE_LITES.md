# NITE LITES — Seeing Vessel Lights From Orbital Heights

Date: 2026-07-27
Game build researched: KSA 2026.7.9.5018 (`ksa-game-assemblies/current`)

## Goal

Have a vessel sitting on a planet surface at night, pull the camera up to orbital heights, and see its lights — the way you'd see a bright light or a city from space in real life. The i-feel-seen mod already bypasses vehicle render-distance culling, but cranking light brightness does nothing at orbital distance. Figure out why, and what a mod can do about it.

## TL;DR

Nothing you can do with intensity alone will ever work — there are **four independent blockers**, and intensity fixes none of them:

1. **Hard range cutoff in the light shader.** Point/spot light attenuation includes a quartic window that reaches exactly zero at `Light.Range`. Stock vessel lights have `Range` of **3–5 meters**. Beyond that radius, contribution is `0` regardless of intensity.
2. **Terrain never samples vehicle lights.** The clustered light system's irradiance is only consumed by *mesh* shaders (vehicles, kittens, props). `Planet.frag` has zero references to it — so a "pool of light on the ground" around the vessel is impossible through the stock pipeline, no matter how large Range/Intensity get.
3. **Sub-pixel rasterization.** From ~200 km a 10 m vessel projects to ~0.05 px. Triangles that small almost never cover a pixel sample point, so the GPU produces no fragments at all. i-feel-seen makes the game *submit* the vessel (its only cull is a `< 1 pixel` check), but the rasterizer still drops it.
4. **The game's own answer for sub-pixel vehicles is sun-driven.** Sub-pixel vehicles are already drawn as star-like point sprites (`StaticCelestialDistanceRendering` + the DistantGlint system), but sprite color is multiplied by a sun-lit factor — **at night the sprite is black**. Vehicle lights never feed it.

The good news: the fix is unusually clean. The distant-sprite system's instance buffer is a **public static field**, and the game's sprite shader is a full star PSF glow renderer (HDR color, size-driven brightness, correct depth handling). A mod can append one glowing instance per lit vehicle from a single Harmony postfix — **no custom Vulkan work at all**.

Physics sanity check: the user's intuition ("maybe it needs a city's worth of lights") is *too* pessimistic for a point source. A 1 kW isotropic light at 400 km delivers ~5×10⁻¹⁰ W/m² — roughly a magnitude ~4 star, visible to the naked eye as a faint point. Reality renders sub-pixel lights as star-like points via the eye's PSF; the game just has no path from vehicle lights to its point-sprite renderer. The mod restores exactly that path.

## Detailed findings (decomp references)

### 1. Vehicle render culling — already solved by i-feel-seen

`KSA/Vehicle.cs`:

- `GetWorldMatrix(Camera)` (~line 3087): returns `null` when `GetObjectDiameterPixelsAsDouble(2*MeanRadius, dist) < 1.0`. That is the **only** distance cull.
- `UpdateRenderData(Viewport, int)` (~line 3100): same `< 1.0 px` check gates `Parts.UpdateRenderData(...)`.

i-feel-seen prefixes both and forces the full path, so for tracked vehicles `PartTree.UpdateRenderData` → `LightModule.UpdateRenderData` **does run at any distance** — the lights *are* submitted to the light system from orbit. Light submission is not the problem.

### 2. Light range is a hard wall

`KSA/LightModule.cs` — `UpdateRenderData` builds the light from `TemplateData`:
- checks `Parent.FullPart.LightSwitch.LightIsActive` **and** the power-consumer state `Active` (unpowered lights early-out),
- then `Light.CreatePointLight(pos, Template.Range, Template.ColorRgb, Template.Intensity, CastsShadows|SoftShadows)` (or spot equivalent),
- submits via `Program.LightSystem.CreateLightInstance(light, viewport)`.

`Content/Core/Shaders/Lighting/LightPrePass.comp` (~line 281):

```glsl
float invRange  = 1.0 / max(light.range, RANGE_EPSILON);
float x2        = distSq * invRange * invRange;
float falloff   = saturate(1.0 - x2 * x2);          // quartic window: exactly 0 at d = range
float rangeAtt  = (invDist * invDist) * mix(1.0, falloff, step(RANGE_EPSILON, light.range));
```

Stock parts (`Content/Core/CoreElectricalAGameData.xml` etc.): `<Range Value="5"/> <Intensity Value="10"/>`, some `Range 3`. So a stock light is pitch dark 5 m out. Intensity multiplies inside the window; it cannot extend it.

`ClusteredLightSystem.cs` (`KSA.Rendering.Lighting`): no CPU distance culling — `CreateLightInstance` only requires MainViewport, `Range` not ~0, and < 1024 lights. `BuildSortedLightList` sorts by influence `Intensity * Range² / dist²` (sort only, not a cull). So a big-Range light submitted from orbit *would* survive to the GPU — it just has nothing to light (see next).

### 3. Terrain does not consume the clustered light system

The LightPrePass compute writes screen-space diffuse/specular irradiance textures. Only these shaders sample them (`grep SampleLightPrePass|lightPrePassDiffuse` over Content):

- `Mesh/MeshIndirect*.frag`, `Mesh/ModelPbr.frag`, `Mesh/ModelTranslucent.frag`, `Mesh/MeshGlassIndirect*.frag`, `Mesh/Fur.frag`, `GenericMesh.frag`, `Debug/MetalRoughSpheres.frag`

`Planet/Planet.frag` (terrain), ground clutter, water: **no references**. Vehicle lights only illuminate vehicle-ish meshes. Boosting `Range` to kilometers lights up *other vessels* and the vessel itself, never the ground. There is no emergent "city glow" available.

### 4. The distant-sprite + glint system (the attach point)

`KSA/StaticCelestialDistanceRendering.cs`:

- `UpdateRenderData(Viewport, int)` — called per viewport per frame from `Program` (~line 4042). Iterates `Universe.CurrentSystem.All`; for any `IOrbiter` (vehicles included) with apparent diameter ≤ 6 px it emits an `InstanceData { float3 PositionEgo; float3 Color; float ScalePixel; }` into `InstancesDevice[frameIndex]` (a **public static** `DeviceVector[]`, `DeviceVector.Add(ReadOnlySpan<byte>)` is public).
- Vehicles get a guaranteed minimum sprite size (`num4 = max(num4, 1.0)` when `dist < MeanRadius * 1e8`) scaled by `GetApparentSizeScale` (min 3 for vehicles) and, if `GameSettings.ShowDistantGlints()`, by `ComputeGlintMultiplier(...)` — a **sun-specular** model (`GlintShininess`, `MaxGlint`, distance falloff, atmosphere transmittance).
- **Crucially**: `Color = x * preset` where `x` is a sun-lit proxy — on the night side `x ≈ 0`, so the sprite is drawn *black*. This is exactly what the user observed: the vehicle sprite machinery works from orbit, but nothing feeds it at night.
- `UpdateDescriptorSets` re-binds automatically when the buffer grows (`LastFrameUpdated` vs `FullLastFrameMoved`), `Run` draws `InstancesDevice[frame].ElementCount` instances — appended instances are picked up with no further plumbing.

`Content/Core/Shaders/StaticCelestialDistance.vert/.frag`:

- Vert: screen-space octagon fan sized `ScalePixel / screenWidth`, depth computed from the true ego position.
- Frag: Celestia-derived star PSF glow. Brightness derives from `ScalePixel` (capped at 4), multiplied by `Color` — **HDR values allowed** (offscreen pass feeds bloom). Sub-4px sprites use a flat "simple brightness" ramp to avoid flicker.
- Depth: bright sprites (≥1 brightness) write true depth (planet horizon occludes them via z-test); faint sprites write far depth (z-fail against any foreground — they never bleed through terrain).

This is a complete, physically-sane "point source seen from far away" renderer with occlusion, bloom, and flicker handling already solved.

`KSA/DistantGlintSettings.cs` — public static tunables (`MaxGlint`, `GlintShininess`, `EndFalloffDistance` in km (default 1000 km!), `AtmosphereAttenuation`, `GlintFadeInPixelSize`) — useful reference for our own knobs, and the falloff default explains why even sunlit glints die past 1000 km.

`KSA/AtmosphericBody.cs:30` — `public float3 GetAtmosphereTransmittance(float3 planetPosition, float cosZenith)` — public; usable for optional atmospheric extinction of our light sprites (same call the glint path uses).

## The plan — new mod: `nite-lites`

### Approach A (primary): append light sprites to the game's distant-sprite renderer

One Harmony **postfix** on `StaticCelestialDistanceRendering.UpdateRenderData(Viewport viewport, int frameIndex)`:

For each vehicle in `Universe.CurrentSystem.Vehicles.GetList()` (or the tracked subset):

1. **Gate on lights actually on** — mirror `LightModule.UpdateRenderData`'s checks per light module:
   `part.FullPart.LightSwitch != null && LightSwitch.LightIsActive` and power state `Active` via `Parent.Tree.PowerConsumers.GetAllStatesByIdx(LightSwitch.StatesIdx).State.Active`. Skip vehicles with no live light.
2. **Gate on distance/apparent size** — compute `pixelDiameter = camera.GetObjectDiameterPixelsFrac(2*MeanRadius, dist)`. Only emit the sprite while `pixelDiameter` is below a handoff threshold (~4–6 px, mirroring `SPRITE_END`/`EMISSION_END`), fading `ScalePixel`/`Color` out with the same smoothstep shape as `SpriteSphereBlend` so the sprite dissolves as real geometry (via i-feel-seen) takes over.
3. **Cull behind camera** — `dot(positionEgo, camera.GetForward()) < 0` skip (same as the game).
4. **Aggregate the vehicle's lights** into ONE instance (sub-pixel — per-light positions are meaningless): total flux `F = Σ Intensity_i`, color = intensity-weighted average of `Template.ColorRgb`, position = `camera.GetPositionEgo(vehicle)` (float precision at 400 km ego distance is ~0.03 m — fine).
5. **Brightness model** (the interesting knob):
   - Physical mode: apparent irradiance `E ∝ F / d²`. Map `log10(E)` through a user-tunable gain/zero-point onto `ScalePixel ∈ [minPx, maxPx]` (defaults ~[1.5, 10]) and an HDR `Color` multiplier ∈ [0, ~8] so strong sources bloom. With stock intensity 10 a light fades out at a few km (honest); with intensity cranked to 1e6+ (see Approach B UI) it reads from orbit — "bright enough to see from space" becomes literally true in-model.
   - Arcade mode: constant `ScalePixel`/brightness clamp — always visible when enabled, for finding your base from orbit.
   - Optional atmospheric extinction: multiply color by mean of `AtmosphericBody.GetAtmosphereTransmittance(...)` when `camera.NearbyCelestial` is atmospheric (identical to the glint path's private helper — reimplement, it's ~6 lines).
6. **Emit**: build the game's own public `StaticCelestialDistanceRendering.InstanceData` struct and `InstancesDevice[frameIndex].Add(MemoryMarshal.AsBytes(span))`. Descriptor refresh, projection, PSF glow, depth/occlusion, bloom — all handled by the game.

Occlusion note: no analytic horizon check needed for the MVP — the sprite writes true depth when bright, so terrain/planet geometry z-culls it when the vessel is around the limb. Add a defensive camera→light ray-vs-parent-sphere test (radius × ~0.999) later only if artifacts show up at extreme distances where the planet itself degrades to a sprite.

Unload: plain `harmony.Unpatch`; we own no GPU resources.

### Approach B (companion): Range/Intensity override UI

Reuse the established `TemplateData` reflection pattern (see `.claude/skills/ksa/lights.md`, zippo / its-so-shiny): per-instance clone of `LightModule.TemplateData`, then write `Range.Value`, `Intensity.Value`, `ColorRgb`. Purpose here:

- **Mid-range visibility** (hundreds of m – a few km, vessel ≥ 1 px): a boosted-Range light makes the vessel's own hull and *nearby vehicles* glow brightly (mesh shaders do sample the light pre-pass), giving a continuous look as you zoom: lit hull → bright dot → PSF sprite.
- Feeds the physical brightness model of Approach A (sprite flux reads the same boosted `Intensity`).

Caveats to document in the mod README:
- Does **not** light terrain (blocker #2) — no ground pool, ever.
- `Light.NearPlane = Range * 0.01`: at Range 5000 the shadow near plane is 50 m, so self-shadowing degrades (the vessel sits inside the near plane). Acceptable; optionally patch `LightModule.UpdateRenderData` later to strip `CastsShadows` from boosted lights.
- `TemplateData` is shared per part template — clone per instance (the lights.md sharing gotcha).

### Rejected / stretch options (investigated)

- **Patch terrain lighting** (make `Planet.frag` sample the light pre-pass): requires replacing the planet shader *and* rebinding the lighting descriptor set into the planet pipeline layout from C#. Deeply invasive, fights every game update. Rejected.
- **Fake ground-glow quad** (quad.md render-to-texture pattern): an additive radial-gradient disc, km-scale, anchored at the vessel's surface position — simulates the missing terrain illumination ("city glow" disc visible from orbit). Visually the best complement to the sprite; real custom-pipeline work. Stretch milestone, not MVP.
- **ImGui overlay glow** (project via `camera.EgoToScreen`, `AddCircleFilled`, like `LightUtils.AddPointLight`): trivial and safe but LDR, no bloom, draws over everything unless we hand-roll occlusion. Keep as a debug view only.

## Mod structure

Standard two-project split (per `.claude/skills/ksa/lifecycle.md`):

- `nite-lites/` — `Mod.cs` (StarMap lifecycle, ImGui window on `[StarMapAfterGui]`), `Patcher.cs` (`HotkeyGuard.Patch/Unpatch` per repo rules), `mod.toml`, README.md
- `nite-lites.lib/` — `NiteLitesPatches.cs` (the `UpdateRenderData` postfix + sprite emission), `LightSpriteModel.cs` (flux aggregation + brightness mapping), `LightBoost.cs` (Approach B template writes), `NiteLitesSubmod.cs` (ISubmod)

UI (single window, F-key toggle):
- vehicle list (auto-detect vehicles carrying `LightModule`s), per-vehicle enable
- global: mode (physical/arcade), gain slider, min/max sprite px, HDR boost, atmosphere extinction toggle
- per-vehicle: Range/Intensity/Color overrides (Approach B), "reset to template"
- hint row when a tracked vehicle's lights are off/unpowered (the #1 support question — `LightIsActive` vs power state)

Optional integration: if i-feel-seen is also tracking the vehicle, the mesh appears as soon as the sprite fades — mention in README; no hard dependency either way.

## Implementation milestones

1. **M1 — proof of sprite injection**: hardcoded postfix appending one white `ScalePixel=8` instance at the controlled vehicle's position; verify visible from orbit at night, verify horizon occlusion, verify clean unload. (This de-risks the decomp-drift question immediately.)
2. **M2 — light detection + aggregation**: live LightSwitch/power gating, flux/color aggregation, handoff fade vs `pixelDiameter`.
3. **M3 — brightness model + UI**: physical/arcade modes, gain, atmosphere extinction; persistence to `My Games/Kitten Space Agency/mods/nite-lites/`.
4. **M4 — Approach B boost UI** (template clone + Range/Intensity/Color writes).
5. **M5 (stretch) — ground-glow disc** via the quad pattern.

Each milestone: `dotnet build` green before done (repo rule).

## Test protocol

1. Place a vehicle with stock lights on Earth's night side; lights ON and powered (battery charged).
2. Camera at ~100 m: confirm normal light rendering unchanged (patch inert below threshold... sprite gated off by pixelDiameter).
3. Zoom to 50 km / 200 km / 1000 km: sprite visible, brightness follows gain; toggle lights off in-game → sprite disappears (power gating works).
4. Let the vessel pass behind the planet limb → sprite occluded.
5. Day side: sprite coexists sanely with the stock sun glint (both render; consider suppressing ours when the glint dominates).
6. Toggle `GameSettings.ShowDistantGlints()` off → our sprite unaffected (independent path).
7. Unload mod mid-session → no crash, sprites gone, stock behavior restored.
8. Multi-viewport (if the user opens extra viewports): verify no misplaced sprites — we only emit for the viewport passed to the postfix call, mirroring the game's per-viewport calls into the shared per-frame buffer (note: the game itself shares one instance buffer across viewports; mirror its behavior exactly).

## Risks & open questions

- **Decomp drift** (the standing caveat): verify at runtime before building on them — `StaticCelestialDistanceRendering.InstancesDevice` (public static field), `InstanceData` layout (explicit offsets 0/16/28, 32-byte stride — use the game's own struct type, never a local copy), `DeviceVector.Add` visibility, `UpdateRenderData(Viewport,int)` signature for the patch target. M1 exists to smoke-test all of these.
- **DeviceVector growth**: initial capacity is 2 elements; the game appends more each frame, so growth is clearly supported — but M1 should confirm appends late in the phase don't race `UpdateDescriptorSets` (they shouldn't: the postfix runs before the game's `UpdateDescriptorSets` call in the same phase, per `Program.cs` ordering ~4042 → ~4062).
- **Draw-order/sorting**: the game sorts its own instances by size before upload; ours append unsorted afterward. Blending is order-dependent only between overlapping sprites — negligible; revisit only if two lights overlap a planet sprite badly.
- **Exposure/tonemap**: night-scene auto-exposure behavior is unverified; if sprites look dimmer than expected, raise the HDR color multiplier (bloom threshold shaders exist: `kawase_downsample_with_thresholds.comp`).
- **Sun-glint interplay**: on the day side both our sprite and the glint render for the same vehicle; may want `max()` semantics (skip ours when the glint multiplier exceeds ours) rather than additive stacking.
- 1024-light cap and cluster behavior are irrelevant to Approach A (we bypass the light system entirely for the sprite) — only Approach B touches it, and a handful of boosted lights is nowhere near any limit.

## Repo compliance checklist (for the implementation change)

- [ ] `REPOSITORY_INDEX.md` — add `nite-lites`
- [ ] `nite-lites/README.md` — full feature/architecture doc
- [ ] `scope/` — new entry in the relevant area file + `scope/game-integration-surface.md` + `scope/FULL_SCOPE.md` ToC/status: Harmony patch on `StaticCelestialDistanceRendering.UpdateRenderData`, reflection/public-field deps (`InstancesDevice`, `InstanceData`, `DeviceVector.Add`, `LightModule.TemplateData.{Range,Intensity,ColorRgb}`, `PowerConsumer.LightIsActive`, `AtmosphericBody.GetAtmosphereTransmittance`), shader-coupling note (`StaticCelestialDistance.vert/frag` instance layout byte offsets 0/16/28)
- [ ] `HotkeyGuard` wired in `Patcher.cs`
- [ ] `dotnet build` green
