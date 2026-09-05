# Ground clutter: imported GLB materials

Current owner: `pebbles.lib/Assets/GlbImportLibrary.cs`, `GlbMaterials.cs`, `GlbTextures.cs`, pure `Models/Glb*` and `Import/GlbFileBrowser.cs`. Baseline: KSA **2026.9.7.5402**. See [ground clutter](ground-clutter.md) for graph, renderer, and lifecycle ownership.

## Geometry, identity and authoring

The in-feature browser uses the existing native ImGui wrapper and managed directory enumeration. All durable folder/path/filter/file/window/scroll/mesh selections are explicit draft bindings. File selection imports the bounded GLB container into an owned CPU cache; assignment resolves CPU geometry/materials and modifies only the draft. GPU texture resolution happens later on explicit preview refresh or Apply. Loading saved drafts invokes neither import nor disposal.

The parser accepts GLB 2.0 with one embedded binary buffer, dense triangle accessors, optional generated normals and float or normalized unsigned UV0. It bounds container/chunk/accessor/index data before constructing private native streams. Complete-scene selection bakes hierarchy transforms and repeated mesh instances, including inverse-transpose normals and mirrored winding; mesh selection uses raw local coordinates. Skinned scene nodes, primitive morph targets, compressed geometry and required extensions are rejected. Animation is not evaluated; vertex colors and authored tangent streams are omitted by the native clutter conversion. Existing `MeshReference` private setters install CPU `MeshAsset` streams (float3 position/normal, float2 UV, uint index) without calling stock arbitrary-file `MeshReference.Load` or registering global assets.

Exact asset IDs include an absolute path, SHA-256 content digest and mesh/material/channel. Cached content versions remain immutable; a fresh resolve requires matching file contents. Missing/changed sources fail explicitly and cannot silently select replacement data. Imported material-slot keys include source identity so independent files' material zero does not collapse into a shared slot; deterministic ordinal ordering survives reload. Limits and usage are maintained in [Pebbles README](../pebbles.lib/README.md#loading-your-own-glb).

## Supported conversion

The private GLB source supplies core metallic/roughness materials. PNG/JPEG images must be embedded buffer views. The importer bounds encoded image dimensions before native decoding, forces RGBA8 output, checks the decoded layout, caps dimensions at 4096 and retained decoded/generated images at 256 MiB per source. Texture references and material recipes are private and never registered in `ModLibrary`.

Base color factors are multiplied in linear space and re-encoded into a UNorm sRGB-valued texture. Pebbles `SourceColors` removes native diffuse-alpha terrain modulation. Normals are renormalized after the glTF normal scale. PBR conversion packs occlusion in R, roughness in G and metallic in B, baking factors and AO strength. Separate AO and metallic/roughness images are resampled by nearest UV at the largest source dimensions. Missing channels use core glTF defaults. The generated textures use the native repeat/filter sampler; glTF filtering preferences are not reproduced independently.

Opaque and alpha-mask materials are supported. Alpha-mask coverage is baked from base-color alpha, alpha factor and the glTF threshold into a separate red-channel opacity texture, with subsequent native mipmap filtering. Clutter diffuse alpha is not opacity. Smooth alpha blending, material/texture extensions (including UV transforms), emissive channels, secondary UV sets, image URIs and clamp/mirror sampling are rejected with an import error. This is a bounded native-clutter conversion, not general glTF material parity.

## Ownership and synchronization

`GetMaterial` builds only detached CPU images/recipes. `ResolveTexture` is invoked only while processing explicit preview refresh or Apply outside GUI rendering; workspace restore must not call it. Images remain cached with their source while live graphs or preview scenes can borrow them. Source identities include the file path and content digest. Texture IDs append material index and channel to that identity.

GPU upload allocates an owned `SimpleVkTexture`, records `UploadData` in the existing private cancellable `PreviewSubmission`, submits and waits for completion, and only then allocates the bindless slot. No texture-loading constructor uses the native staging-pool command list, so a recording failure cannot be automatically submitted by `StagingPool.Dispose`. Private texture references set protected `Texture` and `ImageView` and the private `BindlessHandle` setter through an explicitly scoped reflection lookup. They never call stock `TextureReference.Dispose`, which expects a `TextureAsset` that this adapter does not own.

Owner disposal must retire preview and live consumers first. `GlbMaterials.Dispose` then waits for every renderer that actually allocated a cached texture, before mutating cache state or recycling a bindless slot. CPU-only imports never query or touch the renderer during disposal. Each owned slot is freed once, then its image is disposed. As with the existing preview path, device-loss and constructor-internal native allocation failures cannot promise complete cleanup of unpublished native allocations.

## Integration inventory and evidence

- `Brutal.TextureApi.TextureLoader.LoadFromMemory` / `Unload`, `FormatType.Png` / `Jpg`; `Brutal.TextureApi.Stb.Loader.LoadSettings.ForceRgba8`.
- Decoded `ITexture.Extent` and `Data` must agree with bounded RGBA8 dimensions. CPU data is copied before native image unload.
- `SimpleVkTexture(string, IImageAllocator, width, height, depth, VkFormat, mipLevels, ...)`, `CalculateMaxMipLevels`, `UploadData`, `ImageView`, `Dispose`.
- `TextureReference.Texture` / `ImageView` protected setters; `BindlessHandle` public property/private setter reflection; `SerializedId.SetHash`.
- `Program.GetRenderer`, `Program.Instance.BindlessTextures.AddTexture` / `FreeTexture`; `PreviewSubmission` owns producer completion.
- Source: `Brutal.GltfApi/GltfLoader.cs` image-buffer-view decoding; `RenderCore/SimpleVkTexture.cs:170`, `:378` explicit image allocation/upload; `KSA/TextureReference.cs:64`, `:145` binding ownership; `Brutal.VulkanApi.Abstractions/StagingPool.cs:167` implicit submission hazard.
- Shaders: `GroundClutter/Solid.frag:291` diffuse/terrain-alpha conventions, `:302` AO/roughness/metal channels; `SolidDepth.frag:63` and `SolidShadow.frag:56` red-channel opacity.

`GlbPixelChecks` exercises known sRGB factor values, separate-map AO/MR packing and resampling, normal renormalization, alpha threshold boundaries, missing-map defaults and unchanged source pixels without loading native/game assemblies. Managed validation and compilation do not establish native image decoding, bindless recycling, GPU synchronization, texture orientation, filtering or visual parity. In-game acceptance must include an embedded JPEG material, RGBA PNG alpha mask, constant-color untextured material, multiple material slots, shared source across preview/live bodies, unload after active use, and malformed/oversized/unsupported input errors without replacing live state.
