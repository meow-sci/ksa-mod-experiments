# Decals (graffiti) — Game Integration Scope

## Workspace integration (current)

Active bundled features: **graffiti**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

DecalEntry and global render policy are first-class live records. Main placement size/color/depth and browser state are saved authoring data; render policy uses an explicit Apply. File browser restoration does not import a texture. Loading cancels only the pending placement gesture; existing decal handles, GPU resources and selected live entries continue unchanged. Existing shader/picking integrations are retained.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break **graffiti** (click-to-place
projected PNG decals on vehicles, deployed parachute canopies, and terrain). Every game-facing member the mod touches is
enumerated with its decompiled-source path.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard and feature Harmony patches are wired through `unscience/Patcher.cs`. See [architecture](00-architecture-and-abstractions.md).

**No string reflection anywhere** — every game API used is public, so a rename/reshape is a
**compile** break (loud), never a silent runtime miss.

---

## Integration model

1. **One Harmony postfix** on `KSA.Rendering.RenderTarget.ResolveAttachments(CommandBuffer)`
   (`KSA.Rendering/RenderTarget.cs`). `Program.RenderGame` calls it unconditionally per
   viewport; the method body is MSAA-gated but a postfix fires either way — a reliable seam at
   every MSAA setting, the same post-resolve window the game's own `GridPass` draws in (resolved
   single-sample depth + colour both current and unbound). The postfix (`GraffitiPatches.cs`)
   gates on: `GraffitiSubmod.RenderActive` (static volatile, false when nothing live),
   `!Program.EditorFlag` (the editor resolves the SAME target through the main viewport),
   `__instance == Program.OffscreenTarget` and `Program.RenderedViewport == Program.MainViewport`
   (crew-portrait/secondary viewports own their targets and cameras). Since 5402 viewports are
   `IViewport`/`IGameViewport` objects in `ViewportRegistry`; the main viewport's
   `OffscreenTarget` **is** `Program._offscreenTarget` via `AttachSharedTargets`
   (`KSA/Program.cs`), non-main viewports build their own (`ViewportRenderSurface.BuildOwned`),
   and `_renderedViewport` is set per viewport in `RenderViewport` (`:4315`) — both identity checks
   still select exactly the main flight-scene resolve.
2. **Frame ordering** — decal matrices are composed in `GraffitiSubmod.Update`, dispatched by
   StarMap's `[StarMapBeforeGui]` = prefix on `Program.OnDrawUiFrame` (decl `KSA/Program.cs`,
   called `:2193`), which runs AFTER `OnFrameViewports` (`:2189`, cameras updated) and BEFORE
   `RenderGame` — same-frame camera, no swim. Since 5402 the cursor ray is no longer cached: the
   desktop cursor position is captured in `PrepareImGui` (`:2091 Cursor.SetDesktopPosition`) before
   `OnFrameViewports`, and `Cursor.GetEgoRay(viewport)` builds the ray from that position and the
   viewport's current camera at call time — the click ray is now **same-frame**, not one frame
   stale (5348 and earlier cached `Cursor.InputRay` after the UI phase).
3. **Picking** (`DecalPicker.cs`) — `Cursor.GetEgoRay(Program.MainViewport)` sweeps vehicles and
   their deployed canopies before terrain. Ordinary parts use
   `Part.RayCastEgo` (the identical sweep KSA's flight-mode hover picking runs: bounding-sphere
   broad phase, then `Ray.RaycastWatertight` over the view mesh), else a 64-step march + 24
   bisections over `Celestial.GetTerrainHeightFromDirCcf` in CCF (the shape of
   `TerrainImpactFinder.TryFind`), **every sample `accurate: true`** — see #10. A vehicle hit
   anchors to the hit **sub-part's** `InstanceId` (RayCastEgo returns position/normal in
   `closestSubPart`'s local frame). A **KittenEva** has no raycastable part view mesh, so it gets
   the game's own `KittenEva.UpdateHighlight` treatment instead: `Ray.Raycast` against a
   `BoundingSphere3D` at the root part's ego position (radius × the root's largest scale
   element), anchoring to the root part at the chord midpoint with the normal facing the
   clicker; the placement then floors the box depth at the sphere diameter
   (`PickResult.SuggestedMinDepth`) so the projected box reaches the avatar inside.
   A deployed parachute canopy is not a part view mesh: `DecalPicker.Parachute.cs` transforms its
   public `Parachute.ClothPositionsFront` nodes through the same attachment-local matrix
   as `Parachute.DrawCanopy`, tessellated as the topology's apex fan plus adjacent ring quads, and
   tested with `Ray.RaycastWatertight`. The hit stores three node indices + barycentric weights and
   the clicked side, so the anchor follows the live two-sided cloth surface each frame.
4. **Per-frame composition** (`DecalAnchors.cs`) — decal-space cube → ego as S·R·T·parent in
   double, inverted in double, packed to float push constants. Vehicle anchors:
   `Vehicle.GetMatrixAsmb2Ego(Camera)` + `Part.MatrixAsmb2Ego` (includes scale + sub-part
   chain). Parachute anchors recompute the barycentric point and triangle normal from the current
   front cloth nodes, then use the canopy attachment-local matrix. Terrain anchors: ENU basis via
   `Vehicle.ComputeEnu2Cce` + terrain radius, positioned
   as body-ego + body-fixed offset (the terrain-debug-overlay idiom).
5. **Draw** (`DecalRenderer.cs`) — pipeline layout = set 0 `GlobalShaderBindings` (dynamic
   offset per viewport, `DynamicOffset(Program.MainViewport.ShaderSlot)` since 5402 — was
   `Viewport.Index`), set 1 own depth sampler, set 2 `Program.Instance.BindlessTextures`
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

**Persistence** — Image, size/depth/roll, opacity, brightness, range, renderer policy and import-browser view. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

## Touchpoints

| # | Kind | Mod code | Game member | Decomp path (5402) | Status | Notes |
|---|---|---|---|---|---|---|
| 1 | **Harmony postfix** | `GraffitiPatches.cs` | `RenderTarget.ResolveAttachments(CommandBuffer inCmdBuffer)` | `KSA.Rendering/RenderTarget.cs` | ✅ (file byte-identical 5348→5402) | resolved with `nameof` — rename = compile break. Param name `inCmdBuffer` bound by Harmony: a rename silently unbinds → `Apply` throws → `TryApply` skips graffiti |
| 2 | Direct API | `GraffitiPatches.cs`; `DecalRenderer.cs` | `Program.{EditorFlag, OffscreenTarget, RenderedViewport : IViewport, MainViewport : IGameViewport, SetViewport(CommandBuffer), PointClampedSampler, Instance.ResourceFrameIndex, Instance.ColorFormat, Instance.BindlessTextures, GetRenderer(), GetMainCamera()}` | `KSA/Program.cs` | ✅ retyped @5402 | `RenderedViewport`/`MainViewport` were `Viewport` (class, removed @5402); `ReferenceEquals` on the same `GameViewport` object still holds. Main-viewport `OffscreenTarget` == `Program.OffscreenTarget` (`:1526`). Viewport identity checks are load-bearing (editor + portrait/secondary exclusion) |
| 3 | Direct API | `DecalRenderer.cs` | `RenderTarget.{DepthImage, ColorImage, Extent}`; `BarrierBatch`; `ImageBarrierInfo.Presets.{DepthSampledReadF, ColorAttachmentReadWrite}` | `KSA.Rendering/RenderTarget.cs`; `KSA.Rendering/BarrierBatch.cs`; `KSA.Rendering/ImageBarrierInfo.cs` | ✅ (all three files byte-identical 5348→5402) | near-verbatim GridPass.Run port; depth left in sampled-read state as the game's own pass does. @5402 `GridPass` itself went per-viewport (`SceneDepthDescriptorSets[8]` indexed by `ShaderSlot`, `UpdateDescriptorSet(IViewport)`, `Run` reads `inViewport.OffscreenTarget` — `KSA/GridPass.cs`); graffiti rebuilds its own depth set from `Program.OffscreenTarget.DepthImage` per frame ring, main-only, so nothing changes |
| 4 | Direct API | `DecalRenderer.cs` | `GlobalShaderBindings.{DescriptorSetLayout, DescriptorSet, DynamicOffset(int) : ByteSize}` + `IViewport.ShaderSlot` | `KSA/GlobalShaderBindings.cs`; `KSA/IViewport.cs` | ✅ (mod-side `Viewport.Index` → `ShaderSlot` @5402) | set 0 — the game-wide Camera/Lighting UBO block; set order is baked into the GLSL. UBO stride/order unchanged; the buffer is now sized for a fixed 8 shader slots (`ViewportRegistry.MAX_VIEWPORTS`) instead of `Program.ViewportCount` (6) — slots are pool-allocated, so always look up `MainViewport.ShaderSlot`, never assume 0 |
| 5 | Direct API | `DecalRenderer.cs`; `DecalTextures.cs` | `BindlessTextureLibrary.{DescriptorSetLayout, DescriptorSet, AddTexture(VkImageView), FreeTexture(int)}` | `RenderCore.Systems/BindlessTextureLibrary.cs` | ✅ (file byte-identical 5348→5402) | 1024 shared slots; UpdateAfterBind\|PartiallyBound makes live slot writes legal; FreeTexture rewrites the slot to the empty texture. Sampler slot 0 = linear-clamped full-mip (the shader's `SAMPLE_TEXTURE(texId, 0, uv)`) |
| 6 | Direct API (runtime GLSL) | `DecalRenderer.cs`; `DecalShaders.cs` | `ShaderModuleUtils.FromString(...)`; `ModLibrary.Get<ShaderReference>("GridFrag").ModPath`; GLSL headers `Common/Camera.glsl`, `Common/TextureSet.glsl` | `RenderCore/ShaderModuleUtils.cs`; `Content/Core/Shaders/Common/*` | ✅ | debugName must be a NUL-terminated real path next to the game's shaders (relative `#include` root). Shader reads `global.camera.{viewProjection, inverseProjection, inverseView}` and `global.lighting.{sunPosition, sunColor, planetColor}` — GLSL struct drift breaks at shaderc compile (loud console line, feature self-disables) |
| 7 | Direct API (pipeline) | `DecalRenderer.cs` | `Presets.{InputAssembly.TriangleList, Rasterization.Fill.CullFront}`; `RenderingPresets.{ReverseZDepthStencil.NoDepthTest, BlendState.BlendColorAlphaOver}`; `Renderer.{Device, Allocator, Graphics, GraphicsAndCompute, MaxFramesInFlight, DynamicStateInfo, ViewportState}`; `VkUtils.StageAndUploadToBuffer` | `Brutal.VulkanApi.Abstractions/Presets.cs`; `KSA/RenderingPresets.cs`; `Core/Renderer.cs`; `RenderCore/VkUtils.cs` | ✅ | reverse-Z + CullFront semantics are load-bearing (see risk notes) |
| 8 | Direct API (textures) | `DecalTextures.cs` | `TextureLoader.LoadFromMemory`; `TextureAsset(.LoadOptions)`; `new SimpleVkTexture(Allocator, StagingPool, TextureAsset, CreateOptions)`; `Stb/Ktx/GliTexture.Destroy()`; `CreateStagingPool` ext | `Brutal.TextureApi/TextureLoader.cs`; `RenderCore/TextureAsset.cs`; `RenderCore/SimpleVkTexture.cs`; `Brutal.VulkanApi.Abstractions/StagingPoolExtensions.cs` | ✅ | R8G8B8A8UNorm forces 4 channels; `ITexture` has no IDisposable — `Destroy()` must be called or the decode buffer leaks |
| 9 | Direct API (pick) | `DecalPicker.cs` | `Cursor.GetEgoRay(IViewport)` (**replaced `Cursor.InputRay` @5402**); `Part.RayCastEgo(ref readonly double4x4, Ray, out …×8, out Part?, out Part?)`; `Vehicle.{BoundingSphereRadiusBody, GetMatrixAsmb2Ego(Camera)}`; `Camera.{GetPositionEgo(IPosition), NearbyCelestial}` | `KSA/Cursor.cs`; `KSA/Part.cs`; `KSA/Vehicle.cs`; `KSA/Camera.cs` | ✅ **fixed @5402** (compile break, `Cursor.InputRay`/`UpdateInputRay`/`ScreenPosition` removed) | `GetEgoRay(viewport)` = `viewport.GetCamera().ScreenToEgoRay(Cursor.GetPosition(viewport))` — ego-space, built on demand from the viewport-local cursor position (`DesktopPosition - viewport.Position`) and the **current** camera, so it is same-frame (was one frame stale). Passed `Program.MainViewport` (main-viewport-only feature). RayCastEgo only hits SUB-parts — a top-level part with no sub-parts returns false (the loop never runs); acceptable: stock parts all have sub-parts |
| 9b | Direct API (kitten pick) | `DecalPicker.cs` (`TryPickKitten`) | `KittenEva` (type, `is` check); `new BoundingSphere3D(double3, double)`; `Ray.Raycast(BoundingSphere3D, out double, out bool)`; `Double3Ex.GetAbsoluteLargestElement(double3)`; `Part.{PositionEgo(ref readonly double4x4), ScaleTotal, MatrixAsmb2Ego}`; `PartTree.Root` | `KSA/KittenEva.cs`; `KSA/BoundingSphere3D.cs`; `KSA/Ray.cs`; `KSA/Double3Ex.cs`; `KSA/Part.cs`; `KSA/PartTree.cs` | ✅ (`Ray.cs`, `BoundingSphere3D.cs`, `Double3Ex.cs` byte-identical) | mirrors the game's own `KittenEva.UpdateHighlight(IGameViewport)` sphere pick (`KittenEva.cs`; @5402 it takes `Cursor.GetEgoRay(inViewport)` and reports via `inViewport.PartPicker.TryOffer`, the sphere math is unchanged) — kittens render through `CharacterAvatar`, not part view meshes, so `RayCastEgo` can never hit them |
| 9c | Direct API (parachute pick + anchor) | `DecalPicker.Parachute.cs` (`TryPickParachute`); `DecalAnchors.cs` (`TryComposeParachute`); `GraffitiSubmod.cs` (`FindParachute`) | `PartTree.Modules.Get<Parachute>()`; `ModuleBase.InstanceId`; `Parachute.{ClothPositionsFront, AttachLocationPartAsmb, Parent, CanopyIndex}`; `ChuteClothSystem.Topology`; `ChuteClothTopology.{Rings, Spokes, ApexIndex, CanopyNodeCount, NodeIndex}`; `Ray.RaycastWatertight(double3,double3,double3,out double)`; `Part.MatrixAsmb2VehicleAsmb` | `KSA/Parachute.cs`; `KSA/ChuteClothSystem.cs`; `KSA/ChuteClothTopology.cs`; `KSA/Ray.cs`; `KSA/ModuleBase.cs`; `KSA/Part.cs` | ✅ added @5402 | Canopies bypass `Part.RayCastEgo`. The proxy triangles use the same current front cloth buffer and attachment-local→ego transform as canopy rendering. Re-resolution prefers the runtime module id and falls back to stable parent-part id + authored canopy index after reload; barycentric node weights follow inflation/flutter. The visible skinned GLB is bone-driven by these nodes rather than being the literal 240-triangle proxy, so projection depth absorbs small surface differences; requires a live placement check. |
| 10 | Direct API (terrain) | `DecalPicker.cs`; `DecalAnchors.cs` | `Celestial.{GetCce2Ccf, GetCcf2Cce, GetCci2Cce, MeanRadius, GetTerrainHeightFromDirCcf(dir,bool), GetDirCcfFromLatLon, GetLatitudeFromCcf, GetLongitudeFromCcf}`; `Vehicle.ComputeEnu2Cce(double3, doubleQuat)` | `KSA/Celestial.cs`; `KSA/Vehicle.cs` | ⚠ signature-identical; **accurate-path drift @5402 — needs live check** | lat/lon statics return DEGREES; height is metres above MeanRadius (0 for no heightmap); ComputeEnu2Cce returns null on the spin axis (pole fallback basis in `DecalAnchors`). ⚠ **`accurate: true` is load-bearing** everywhere the surface radius matters: only accurate mode evaluates the procedural terrain modifiers (`Celestial.cs`, gated `if (accurate)` since the 5319–5325 terrain precision rework) — the metres-scale displacement the rendered surface includes. @5402 the modifier evaluation's radius inputs (`gradientWeight`, `CelestialRadiusKm`, `Celestial.cs`) switched from `RenderData.SurfaceRadius` to `(float)MeanRadius`; if the GPU terrain did not move identically, terrain decals float/sink again. The composed radius is cached per entry (`DecalEntry.TerrainRadius`; terrain is static) |
| 11 | Direct API (anchor re-resolve) | `GraffitiSubmod.cs` | `Universe.CurrentSystem.Get(string)`; `Vehicle.Parts.Parts`; `Part.{SubParts, InstanceId, MatrixAsmb2Ego(in double4x4), Id}` | `KSA/Universe.cs`; `KSA/CelestialSystem.cs`; `KSA/Part.cs` | ✅ | per-frame; a despawned anchor makes the decal dormant, never pruned |
| 12 | Build refs | `graffiti.lib.csproj` | `Brutal.Vulkan(.Abstractions/.Vma)`, `Brutal.ShaderC`, `Brutal.Texture(.Abstractions)`, `Brutal.Ktx`, `Brutal.Core.Memory`, `Planet.Core`, `Planet.Render.Core` | — | ✅ | Planet.Render.Core carries `RenderCore`/`RenderCore.Systems`; Planet.Core carries `Core.Renderer` |

## Historical evidence

See [dated integration and upgrade reference](history/decals.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.
