# Decals (graffiti) — Game Integration Scope

Permanent reference for detecting when KSA game updates break **graffiti** (click-to-place
projected PNG decals on vehicles and terrain). Every game-facing member the mod touches is
enumerated with its decompiled-source path.

**Verified game version:** written against KSA **`2026.8.22.5348`** (decomp root
`…/ksa-game-assemblies/current/decomp`, namespace-foldered). The implementation is a port of the
gatOS sticker system, whose anchors were independently verified against the same build.

**How the mod is hosted:** all logic in `graffiti.lib` (`GraffitiSubmod : ISubmod`,
`GraffitiPatches.Apply/Remove`), consumed by the standalone host (`graffiti/Mod.cs`,
`graffiti/Patcher.cs`) and by unscience (`unscience/Mod.cs` adds `GraffitiSubmod`;
`unscience/Patcher.cs` → `TryApply("graffiti", …)`). Findings apply to both hosts identically.

**No string reflection anywhere** — every game API used is public, so a rename/reshape is a
**compile** break (loud), never a silent runtime miss.

---

## Integration model

1. **One Harmony postfix** on `KSA.Rendering.RenderTarget.ResolveAttachments(CommandBuffer)`
   (`KSA.Rendering/RenderTarget.cs:315`). `Program.RenderGame` calls it unconditionally per
   viewport; the method body is MSAA-gated but a postfix fires either way — a reliable seam at
   every MSAA setting, the same post-resolve window the game's own `GridPass` draws in (resolved
   single-sample depth + colour both current and unbound). The postfix (`GraffitiPatches.cs`)
   gates on: `GraffitiSubmod.RenderActive` (static volatile, false when nothing live),
   `!Program.EditorFlag` (the editor resolves the SAME target through the main viewport index),
   `__instance == Program.OffscreenTarget` and `Program.RenderedViewport == Program.MainViewport`
   (crew-portrait/secondary viewports have their own targets and cameras).
2. **Frame ordering** — decal matrices are composed in `GraffitiSubmod.Update`, dispatched by
   StarMap's `[StarMapBeforeGui]` = prefix on `Program.OnDrawUiFrame` (`KSA/Program.cs:2095`),
   which runs AFTER `OnFrameViewports` (cameras updated) and BEFORE `RenderGame` — same-frame
   camera, no swim. `Cursor.UpdateInputRay` runs after the UI phase (`:2146`), so the click ray
   is one frame stale — imperceptible for a click UX (gatOS documents the same).
3. **Picking** (`DecalPicker.cs`) — `Cursor.InputRay` swept vehicles-first with
   `Part.RayCastEgo` (the identical sweep KSA's flight-mode hover picking runs: bounding-sphere
   broad phase, then `Ray.RaycastWatertight` over the view mesh), else a 64-step march + 24
   bisections over `Celestial.GetTerrainHeightFromDirCcf` in CCF (the shape of
   `TerrainImpactFinder.TryFind`). A vehicle hit anchors to the hit **sub-part's** `InstanceId`
   (RayCastEgo returns position/normal in `closestSubPart`'s local frame).
4. **Per-frame composition** (`DecalAnchors.cs`) — decal-space cube → ego as S·R·T·parent in
   double, inverted in double, packed to float push constants. Vehicle anchors:
   `Vehicle.GetMatrixAsmb2Ego(Camera)` + `Part.MatrixAsmb2Ego` (includes scale + sub-part
   chain). Terrain anchors: ENU basis via `Vehicle.ComputeEnu2Cce` + terrain radius, positioned
   as body-ego + body-fixed offset (the terrain-debug-overlay idiom).
5. **Draw** (`DecalRenderer.cs`) — pipeline layout = set 0 `GlobalShaderBindings` (dynamic
   offset per viewport), set 1 own depth sampler, set 2 `Program.Instance.BindlessTextures`
   (UpdateAfterBind|PartiallyBound); 112-byte push block; two GLSL strings compiled at runtime
   with `ShaderModuleUtils.FromString` whose `#include`s resolve next to the shipped `GridFrag`
   asset (`ModLibrary.Get<ShaderReference>("GridFrag").ModPath`). CullFront (camera-inside-box),
   no depth test (occlusion from sampled reverse-Z depth), alpha-over blend, single-sample,
   colour format `Program.Instance.ColorFormat`. Depth is barriered to `DepthSampledReadF` and
   left there, exactly as GridPass leaves it.
6. **Textures** (`DecalTextures.cs`) — `TextureLoader.LoadFromMemory` (PNG, forced RGBA8 via
   `TextureAsset.LoadOptions(R8G8B8A8UNorm, Rgba32)`) → `SimpleVkTexture` (max edge 2048,
   downsample, full mip chain) → `BindlessTextureLibrary.AddTexture/FreeTexture`. Freed images
   ride a MaxFramesInFlight+1 retire queue; teardown drains
   `Program.GetRenderer().GraphicsAndCompute.WaitIdle()` first.

**Persistence** — none for placed decals (session-scoped). The decal **library** is plain PNGs at
`<MyDocuments>/My Games/Kitten Space Agency/.unscience/decals/`
(dir from `ksa-abstractions.lib/KsaPaths.cs:9`). Mod-authored files, not game assets.

## Touchpoints

| # | Kind | Mod code | Game member | Decomp path (5348) | Status | Notes |
|---|---|---|---|---|---|---|
| 1 | **Harmony postfix** | `GraffitiPatches.cs` | `RenderTarget.ResolveAttachments(CommandBuffer inCmdBuffer)` | `KSA.Rendering/RenderTarget.cs:315` | ✅ | resolved with `nameof` — rename = compile break. Param name `inCmdBuffer` bound by Harmony: a rename silently unbinds → `Apply` throws → `TryApply` skips graffiti |
| 2 | Direct API | `GraffitiPatches.cs`; `DecalRenderer.cs` | `Program.{EditorFlag, OffscreenTarget, RenderedViewport, MainViewport, SetViewport(CommandBuffer), PointClampedSampler, Instance.ResourceFrameIndex, Instance.ColorFormat, Instance.BindlessTextures, GetRenderer(), GetMainCamera()}` | `KSA/Program.cs:205,438,472,468,4148,450,199,203,91` | ✅ | all public; viewport identity checks are load-bearing (editor + portrait exclusion) |
| 3 | Direct API | `DecalRenderer.cs` | `RenderTarget.{DepthImage, ColorImage, Extent}`; `BarrierBatch`; `ImageBarrierInfo.Presets.{DepthSampledReadF, ColorAttachmentReadWrite}` | `KSA.Rendering/RenderTarget.cs:36,38,48`; `KSA.Rendering/BarrierBatch.cs`; `KSA.Rendering/ImageBarrierInfo.cs` | ✅ | near-verbatim GridPass.Run port; depth left in sampled-read state as the game's own pass does |
| 4 | Direct API | `DecalRenderer.cs` | `GlobalShaderBindings.{DescriptorSetLayout, DescriptorSet, DynamicOffset(int)}` | `KSA/GlobalShaderBindings.cs` | ✅ | set 0 — the game-wide Camera/Lighting UBO block; set order is baked into the GLSL |
| 5 | Direct API | `DecalRenderer.cs`; `DecalTextures.cs` | `BindlessTextureLibrary.{DescriptorSetLayout, DescriptorSet, AddTexture(VkImageView), FreeTexture(int)}` | `RenderCore.Systems/BindlessTextureLibrary.cs:38,155,198` | ✅ | 1024 shared slots; UpdateAfterBind\|PartiallyBound makes live slot writes legal; FreeTexture rewrites the slot to the empty texture. Sampler slot 0 = linear-clamped full-mip (the shader's `SAMPLE_TEXTURE(texId, 0, uv)`) |
| 6 | Direct API (runtime GLSL) | `DecalRenderer.cs`; `DecalShaders.cs` | `ShaderModuleUtils.FromString(...)`; `ModLibrary.Get<ShaderReference>("GridFrag").ModPath`; GLSL headers `Common/Camera.glsl`, `Common/TextureSet.glsl` | `RenderCore/ShaderModuleUtils.cs:79`; `Content/Core/Shaders/Common/*` | ✅ | debugName must be a NUL-terminated real path next to the game's shaders (relative `#include` root). Shader reads `global.camera.{viewProjection, inverseProjection, inverseView}` and `global.lighting.{sunPosition, sunColor, planetColor}` — GLSL struct drift breaks at shaderc compile (loud console line, feature self-disables) |
| 7 | Direct API (pipeline) | `DecalRenderer.cs` | `Presets.{InputAssembly.TriangleList, Rasterization.Fill.CullFront}`; `RenderingPresets.{ReverseZDepthStencil.NoDepthTest, BlendState.BlendColorAlphaOver}`; `Renderer.{Device, Allocator, Graphics, GraphicsAndCompute, MaxFramesInFlight, DynamicStateInfo, ViewportState}`; `VkUtils.StageAndUploadToBuffer` | `Brutal.VulkanApi.Abstractions/Presets.cs`; `KSA/RenderingPresets.cs`; `Core/Renderer.cs`; `RenderCore/VkUtils.cs` | ✅ | reverse-Z + CullFront semantics are load-bearing (see risk notes) |
| 8 | Direct API (textures) | `DecalTextures.cs` | `TextureLoader.LoadFromMemory`; `TextureAsset(.LoadOptions)`; `new SimpleVkTexture(Allocator, StagingPool, TextureAsset, CreateOptions)`; `Stb/Ktx/GliTexture.Destroy()`; `CreateStagingPool` ext | `Brutal.TextureApi/TextureLoader.cs:130`; `RenderCore/TextureAsset.cs:35`; `RenderCore/SimpleVkTexture.cs:245`; `Brutal.VulkanApi.Abstractions/StagingPoolExtensions.cs` | ✅ | R8G8B8A8UNorm forces 4 channels; `ITexture` has no IDisposable — `Destroy()` must be called or the decode buffer leaks |
| 9 | Direct API (pick) | `DecalPicker.cs` | `Cursor.InputRay`; `Part.RayCastEgo(ref readonly double4x4, Ray, out …×8, out Part?, out Part?)`; `Vehicle.{BoundingSphereRadiusBody, GetMatrixAsmb2Ego(Camera)}`; `Camera.{GetPositionEgo(IPosition), NearbyCelestial}` | `KSA/Cursor.cs:25`; `KSA/Part.cs:2306`; `KSA/Vehicle.cs`; `KSA/Camera.cs:231,71` | ✅ | InputRay is ego-space and one frame stale (what the player last saw). RayCastEgo only hits SUB-parts — a top-level part with no sub-parts returns false (the loop never runs); acceptable: stock parts all have sub-parts |
| 10 | Direct API (terrain) | `DecalPicker.cs`; `DecalAnchors.cs` | `Celestial.{GetCce2Ccf, GetCcf2Cce, GetCci2Cce, MeanRadius, GetTerrainHeightFromDirCcf(dir,bool), GetDirCcfFromLatLon, GetLatitudeFromCcf, GetLongitudeFromCcf}`; `Vehicle.ComputeEnu2Cce(double3, doubleQuat)` | `KSA/Celestial.cs`; `KSA/Vehicle.cs:2997` | ✅ | lat/lon statics return DEGREES; height is metres above MeanRadius (0 for no heightmap); ComputeEnu2Cce returns null on the spin axis (pole fallback basis in `DecalAnchors`) |
| 11 | Direct API (anchor re-resolve) | `GraffitiSubmod.cs` | `Universe.CurrentSystem.Get(string)`; `Vehicle.Parts.Parts`; `Part.{SubParts, InstanceId, MatrixAsmb2Ego(in double4x4), Id}` | `KSA/Universe.cs`; `KSA/Part.cs:1005` | ✅ | per-frame; a despawned anchor makes the decal dormant, never pruned |
| 12 | Build refs | `graffiti.lib.csproj` | `Brutal.Vulkan(.Abstractions/.Vma)`, `Brutal.ShaderC`, `Brutal.Texture(.Abstractions)`, `Brutal.Ktx`, `Brutal.Core.Memory`, `Planet.Core`, `Planet.Render.Core` | — | ✅ | Planet.Render.Core carries `RenderCore`/`RenderCore.Systems`; Planet.Core carries `Core.Renderer` |

## Update-risk findings

- **Loud breaks (compile):** any signature change in the touchpoint table — no reflection is
  used anywhere. `ResolveAttachments` rename (#1 via `nameof`), pipeline/preset reshapes (#7),
  `SimpleVkTexture`/`TextureLoader` churn (#8 — the historically highest-churn surface, shared
  with thug-life).
- **Loud breaks (runtime, one console line, feature self-disables):** GLSL header drift
  (`Common/Camera.glsl` / `Common/TextureSet.glsl` struct or macro changes) fails at shaderc
  compile inside `EnsureGpu`; a draw fault latches `_gpuFailed`.
- **Silent breaks (semantic drift, no symbol change):**
  - the post-resolve seam's *meaning* — if the game moves grid/overlay drawing elsewhere or
    starts resolving into a different image, decals draw into the wrong target (symptom: decals
    gone or double-exposed, no error). Re-check `GridPass.Run` against `RecordPass` on every bump.
  - reverse-Z / NDC conventions in the depth reconstruction (symptom: decals project to wrong
    positions). The debug-box checkbox (magenta checker) is the built-in diagnostic.
  - `Cursor.InputRay` staleness/space, and `Part.RayCastEgo` frame conventions (position/normal
    are in the SUB-part's local frame — if that changes, placements land skewed).
  - bindless sampler slot 0 semantics (linear-clamped); a sampler-table reshuffle turns decals
    point-sampled or wrapped.
- **Harmony param binding:** #1's `inCmdBuffer` param name — a rename throws at `Apply`
  (logged + skipped), same failure mode as pyro's postfix.
- **Projection-depth geometry (not a game coupling, but the #1 user-visible surprise):** the
  visible decal is the surface ∩ projection box. A box too shallow for the surface's curvature
  crops a wide decal to its central region — which looks like the image "zoomed in" (footprint
  grows, image edges vanish; the matrix path itself is exact — verified numerically). Default
  depth therefore scales with the decal (`GraffitiSubmod.AutoDepth`: half the larger side,
  floored at 0.3 m hull / 1 m terrain), and Depth is a placement setting. Too much depth has the
  opposite failure: the parallel projection punches through thin geometry and paints the far
  side (the normal-cutoff fade does not stop it, since the flipped normal can still face the
  decal axis).
- **Not done / known limits:** flight scene only (editor excluded by design); main viewport only;
  placed decals are not persisted; a top-level part with zero sub-parts cannot be clicked (see
  #9); decals do not draw while `VolumetricExhaust`-style per-viewport secondary cameras render.
