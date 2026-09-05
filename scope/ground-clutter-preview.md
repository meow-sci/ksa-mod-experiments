# Ground clutter Workshop preview

Current owner: `pebbles.lib/Preview`. Baseline: KSA **2026.9.7.5402**.
See [ground clutter](ground-clutter.md) for runtime application and collider ownership.

## Isolation and lifecycle

`WorkshopPreview` owns its own mesh buffers, material descriptor sets, sampler, pipelines,
color/depth images, command pools and fences. It does not create a game viewport, allocate a
global camera shader slot, enter `VehicleEditor`, register a `GenericGizmo`, mutate a part or
construct a Bepu shape. Its constructor allocates no GPU resources. `Refresh` resolves/copies geometry and may lazily upload privately owned imported GLB textures;
`Refresh` and `Render` both run from the host's **before-GUI** update. See [GLB ownership](ground-clutter-glb-materials.md).
Workspace restore changes detached Workshop state and requires explicit preview refresh.

The first implementation uses a conservative synchronous policy for changed previews:

1. Before any current-frame ImGui image is emitted, `Device.WaitIdle` retires previously
   submitted producers **and UI consumers** of the existing image/descriptors.
2. Build/upload any changed mesh scene; record the independent offscreen pass.
3. Submit on `Renderer.Graphics`, wait for that submission's fence, then publish the image.
4. Display the persistent image with ImGui. An unchanged scene/camera/size does no GPU work.

Resize, failed-render target replacement, explicit release and unload use the same retirement
rule. Closing the Workshop queues release for a later before-GUI update. Never dispose the
preview after appending its image to an unsubmitted ImGui draw list. A producer fence alone
does not retire UI sampling. Camera drags currently incur a whole-device wait; this is a
documented performance limitation, not an asynchronous frame-graph integration.

`PreviewSubmission` owns a raw private command buffer and fence. Its staging pool contains
upload buffers only. Recording failures destroy the unsubmitted private command pool; they
must not cause `StagingPool.Dispose` to submit an incomplete command buffer. Submission
errors require a device-idle barrier before releasing potentially referenced allocations.
New scenes/targets remain staged until rendering succeeds; failures suppress retries until
an explicit refresh and do not publish a misleading ready image.

## Exact game/native integration

No Harmony targets or reflection lookups are introduced by Preview itself. Asset resolution
is delegated to `ClutterAssets`; its separate discovery/private-import surface belongs to
the main clutter area. Preview consumes:

- `Program.GetRenderer`, `Renderer.Device/Allocator/Graphics`, Vulkan allocation/queue APIs.
- `MeshReference.HostPrimitives`, `PrimitiveCount`, `PrimitiveMaterialIds`; `MeshAsset`
  vertex lists/spans, counts and index buffer. Streams must be float3 position/normal,
  float2 UV0, with valid indexed triangles. All primitives of every selected mesh render. Imported source identities separate file-local material slots; sorting is shared with runtime Apply and is independent of import order.
- `TextureReference.ImageView`, `EmptyWhite`; resolved texture references are retained for
  descriptor lifetime and remain borrowed. Preview does not free or alter game textures.
- `ShaderModuleUtils.FromString` for owned embedded shader modules; modules are destroyed
  after pipeline creation. `Brutal.ShaderC` is a direct assembly dependency.
- `VkUtils.StageAndUploadToBuffer`, `KSA.Rendering.Utils.CreateBarrier`; explicit transfer
  write → vertex-attribute/index-read barriers follow geometry uploads.
- `ImageTransition` / `ImageBarrierInfo.Presets` for color/depth/sampled transitions;
  Vulkan dynamic rendering, viewport/scissor, descriptor binding and indexed draws.
- `ImGuiBackend.Vulkan.AddTexture/RemoveTexture` for the owned color image; descriptor
  removal precedes image-view/image destruction after UI consumers retire.

`PartThumbnailViewport`, `ThumbnailCreator`, `ThumbnailDynamic`, `ThumbnailRenderer`,
`GenericGizmo` and `GizmosRenderer` were reference material for this implementation and are
**not called**. Their shared viewport/global shader/part-atlas/gizmo paths are not dependencies.

## Binary and shader contracts

Embedded shaders: `Preview/Workshop.vert.glsl`, `Preview/Workshop.frag.glsl`.
They have no game shader includes and use no global descriptor sets.

| Contract | Layout / behavior |
|---|---|
| Vertex binding 0 | 32-byte stride: position float3 offset 0, normal float3 offset 12, UV float2 offset 24. Locations 0/1/2. |
| Index buffer | Always uint32. Input ushort/uint values are decoded and range-checked in CPU arrays before upload, avoiding the native atlas mixed-width staging hazard. |
| Push block | 112 bytes: Matrix4x4 view-projection offset 0; camera float4 offset 64; maps float4 offset 80; options float4 offset 96. Vertex + fragment stages. |
| Maps flags | Diffuse, normal, PBR, opacity presence in XYZW. |
| Options flags | Thickness presence X; source-color mode Y; remaining components reserved. |
| Descriptor set 0 | Five combined image samplers at bindings 0..4: diffuse, normal, packed AO/roughness/metalness, opacity, thickness. |
| Color image | R8G8B8A8UNorm, sampled + color attachment, single sample; opaque studio background. |
| Depth image | D32SFloat, single sample; **forward depth**, clear 1, LessOrEqual. This private pass does not reuse the game's reverse-Z state. |
| Render coordinates | System.Numerics row-vector matrices, shared `WorkshopMath.ViewProjection`; projection M22 negated for positive-height Vulkan viewport. GLSL mat4 column interpretation of row-matrix bytes gives equivalent matrix-times-column-vector results. |

Object transforms use `Scale * RotationX * RotationY * RotationZ * Translation`, matching
runtime geometry preparation. Normal vectors use inverse transpose. Local collider gizmos
must use the game's XYZ rotation convention, not `CreateFromYawPitchRoll` with reordered
arguments. Preview projection, UI line projection and picking share one convention; logical
panel size supplies the aspect even when the GPU target resolution is capped.

This is a studio editing preview. It supports five texture channels, separate 0.5 opacity
cutout, normal mapping, packed PBR and approximate transmission under fixed lights. It is
double-sided for editing and does not simulate atmosphere, celestial terrain coloration,
clutter placement, shadows or gameplay lighting. Source-color mode avoids diffuse-alpha
terrain modulation in the preview; final applied source-color preparation is a separate
runtime material operation. The inspector must not imply pixel-identical game lighting.

## Verification and outstanding native acceptance

Managed compilation passes against current reference assemblies. Both GLSL stages compile
to SPIR-V using `glslangValidator -V`. These checks do not execute Vulkan/ImGui or establish
visual correctness. Verify in game: multi-axis mesh/collider alignment, multi-primitive
material mappings, tiny/large meshes, portrait/wide resizing, normal orientation, opacity,
texture color space, every LOD, explicit refresh errors, repeated resource release, and
camera dragging while the vehicle editor/other viewports are active. Confirm no global
camera, thumbnail, gizmo, vehicle, collider or game-save state changes.
