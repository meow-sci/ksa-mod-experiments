# Parachutes (free-fallin) — Game Integration Scope

Permanent reference for detecting when KSA game updates break **free-fallin**, the global parachute
texture/tint/PBR customizer. Cataloged against KSA build **2026.9.7.5402** at
`../ksa-game-assemblies/current/decomp` and `../ksa-game-assemblies/current/Content`.

All logic is in `free-fallin.lib` (`FreeFallinSubmod : ISubmod`,
`FreeFallinPatches.Apply/Remove`), consumed by both the standalone `free-fallin` host and
`unscience`. The standalone Patcher also applies the required `HotkeyGuard`.

## Integration model

KSA creates one `ChuteRenderable` per live canopy and hard-wires material slot zero to
`ParachuteCanopy_Material`. Its `Draw` method updates the cloth pose and calls a private
`AnimatedRenderable`. Free-fallin prefixes that draw, reflects `_renderable`, then swaps element
zero of its protected `MaterialIndices` array to a mod-created material handle. The array is read
by `AnimatedRenderable.Draw` for the main, pre-pass, and shadow submissions, so one substitution
keeps every pass consistent and follows the skinned cloth automatically.

The material is built through KSA's public GPU systems:

- Stock mode reuses the stock albedo bindless handle and multiplies it by `MaterialData.AlbedoColor`.
- Replace mode decodes an imported PNG to RGBA8 and registers it with
  `GpuTextureSystem.TryAddTexture`.
- Center-decal mode detects KSA's runtime BC7 stock texture, reopens its source KTX2 with an explicit
  `Rgba32` transcode request, copies RGBA8 mip 0 into a `GenericTexture`, alpha-blends a scaled PNG
  into its center, and uploads the result. The stock alpha is retained so transparent decal pixels
  cannot cut holes in the canopy. A native, non-transcodable BC7 source degrades to a flat white
  (therefore tintable) base instead of disabling the feature.
- The stock normal texture is always retained. PBR controls either multiply the stock texture's
  R/G/B = AO/roughness/metallic channels through `RoughnessMetalScale`, or upload a 1×1 uniform PBR
  texture for direct 0–1 values.

Settings are session-only. Imported files persist under `.unscience/parachutes`. Generated KSA
assets are intentionally retained until renderer shutdown so frames in flight never reference a
freed material or bindless handle. Restore/unload rewrites every weakly tracked renderable to the
stock material handle.

## Touchpoints

| # | Kind | Mod code | Game member / asset | Decomp/content path (5402) | Risk / invariant |
|---|---|---|---|---|---|
| 1 | **Harmony prefix** | `FreeFallinPatches.cs` | `ChuteRenderable.Draw(float3[], float[]?, floatQuat[]?, ref readonly double4x4, float, double)` | `KSA/ChuteRenderable.cs:32` | Single overload today. Rename/signature change is loud at patch setup/build. Prefix must run before the nested `_renderable.Draw()`. |
| 2 | **Private reflection** | `FreeFallinPatches.cs` | `ChuteRenderable._renderable : AnimatedRenderable` | `KSA/ChuteRenderable.cs:13` | String-named private field; rename is a silent-compile/runtime-patch failure. Add to every update's reflection watchlist. |
| 3 | **Protected reflection** | `FreeFallinPatches.cs` | `AnimatedRenderable.MaterialIndices : int[]` | `KSA/AnimatedRenderable.cs:34` | Slot zero must remain the canopy mesh's material. Rename or material-slot reordering breaks customization/restoration. |
| 4 | Direct GPU API | `CanopyMaterialController.cs` | `GpuObjectSystem<MaterialData>.CreateObject`; `GpuMaterialSystem.GetOrLoad` | `KSA/GpuObjectSystem.cs:45`; `KSA/GpuMaterialSystem.cs` | Allocates one immutable material per Apply. `MaterialData` field order is shader ABI. |
| 5 | Direct GPU API | `CanopyMaterialController.cs` | `GpuTextureSystem.TryAddTexture/GetOrLoad`, sampler/default handles | `KSA/GpuTextureSystem.cs:85` | Adds replacement/composited albedo and optional uniform PBR textures to KSA's bindless table. |
| 6 | Direct asset API | `CanopyMaterialController.cs` | `ModLibrary.Get<PbrMaterialReference>("ParachuteCanopy_Material")`; diffuse/normal/PBR references | `KSA/ModLibrary.cs`; `KSA/PbrMaterialReference.cs` | Asset id and three-map shape are hard dependencies. |
| 7 | Asset + CPU transcode | `CanopyMaterialController.cs` | `ParachuteCanopy_Material`, `TextureReference.ModPath`, `ParachuteCanopy_Diffuse.ktx2`, normal and PBR textures | `Content/Core/ParachuteAssets.xml:23-27`; `Brutal.TextureApi.Ktx/Loader.cs` | Runtime diffuse is BC7 at 5402. Center-decal mode reopens the source KTX2 and requests `KtxTranscodeFmt.Rgba32`; native/non-transcodable BC7 falls back to a flat tintable base. Stock/Replace do not depend on this. |
| 8 | Shader ABI | `CanopyMaterialController.cs` | `MaterialData.{AlbedoTexture,Sampler,AlbedoColor,NormalTexture,RoughMetallicAOTexture,RoughnessMetalScale,EmissiveTexture}` | `KSA/MaterialData.cs`; `Content/Core/Shaders/Common/MaterialSet.glsl:28-41` | Shader defines albedo multiplication and PBR channel order R=AO, G=roughness, B=metallic. Recheck together. |
| 9 | Lifecycle | both hosts | StarMap attributes, `ISubmod`, consolidated Harmony, `HotkeyGuard` | `free-fallin/Mod.cs`, `Patcher.cs`; `unscience/Mod.cs`, `Patcher.cs` | Standalone and umbrella hosts must apply/remove exactly once. |

## Game-update checklist

1. Build against the new KSA assemblies.
2. Recheck the two reflected fields by exact name and type.
3. Verify `ChuteRenderable.Draw` still owns the only canopy submission and still uses material slot 0.
4. Diff `MaterialData` against `MaterialSet.glsl`; confirm AO/roughness/metallic channel semantics.
5. Verify `ParachuteAssets.xml` ids, `TextureReference.ModPath`, and that reopening the stock KTX2
   with `KtxTranscodeFmt.Rgba32` produces RGBA8.
6. Live-test stock tint, replacement, center decal, uniform metallic/roughness, secondary viewports,
   shadows, Restore Stock, and unload with a canopy already deployed.
