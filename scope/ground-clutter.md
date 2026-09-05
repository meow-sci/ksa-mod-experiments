# Ground clutter: Pebbles

Current owner: `pebbles.lib`. Runtime code is in `Runtime/` and asset/geometry ownership in `Assets/`; main authoring and Workshop UI belong to the same feature. Reference baseline: KSA **2026.9.7.5402**, sibling `ksa-game-assemblies/current`. Compilation and offline shader validation do not establish native GPU or gameplay behavior.

## Runtime behavior and ownership

Pebbles queues per-celestial recipe application and restoration. The prefix of `Universe.ExecuteNextClothSolvers` runs after the prior frame's solver completion/application and before the next cloth and vehicle work is scheduled. It waits vehicle/cloth jobs and the graphics device, verifies the vehicle task's sync window, constructs a private reference graph/material table/placement/render/physical bundle, drains old collision exclusions, clears matching physics-bubble statics, and replaces the three arrays for the exact celestial hash. Visible geometry and collider proxies have independent bounds; the physics constructor receives the maximum of visual and proxy reach both per object and per ecotype. `_planetClutterMaxBoundingRadius` remains the visual radius used for shadow-frustum extension.

The original body template, placement/render/physical arrays and shadow bound remain retained for exact body restore. Both `Celestial.BodyTemplate` and `Astronomical.bodyTemplate` are rebound to a shallow private template whose clutter graph is wholly private. Shared mesh primitives, materials, texture references, and collider templates are never edited. Keep-original colliders are rebuilt privately; stock convex-hull behavior retains the native first-primitive rule. Custom hulls combine the selected mesh's primitives. Hull points, shapes and offsets belong to Pebbles; native physical data owns the registered scaled Bepu shapes.

Meshes are copied into private CPU `MeshAsset`s, including transformed positions/normals, UVs, and a uniformly uint source index stream to avoid the mixed-index atlas staging defect. Mesh and collider Euler rotations match native `QuaternionEx.CreateFromXyzRadians` (XYZ; row-vector matrices Rx * Ry * Rz). Positive object transforms are supported; reflected scales are rejected. Imported named glTF meshes are CPU-only, cached by `ClutterAssets`, and not registered globally. Bound game textures are borrowed. The native renderer builds/uploads the private atlas.

Each private LOD receives explicit material indirection and private material references. Pebbles routes only its own native material-call sites to its private `GroundClutterGpuMaterial` buffer and hash/index map. Construction context is thread-local; frame-resource rebuild context is keyed by the exact owned render object. Global material buffers and stock shader references are not overwritten. An explicit transfer-to-fragment-read buffer barrier follows material upload.

`SourceColors` adapts the current `ClutterSolidFrag` source while building the owned color pipeline. Bit 31 of the private material flags removes terrain-color modulation; bit 30 records an sRGB texture format so already-linear hardware sampling is not decoded a second time. The source marker must occur exactly once. Native include callbacks and original, NUL-terminated source path are preserved. Other stock shader variants and depth/shadow paths remain native. Native lighting, PBR response and shadows still apply.

Recipe identity includes ecotype name, ordered object IDs, LOD mesh IDs/primitive counts and material IDs. Variant count/order and five LOD slots remain stable. Runtime requires nonempty geometry for every LOD. Maximum 51 object slots, candidate and repeated-vertex budgets, uniform XYZ placement scale for collidable ecotypes, valid positive installed-collider mass, resolved biome aliases/assets, and native parameter conversion are checked before commitment. Biome controls edit an ecotype's native 32-bit eligibility mask; duplicating/splitting ecotypes for biome-specific replacement is not implemented because native candidate selection is not a disjoint partition.

Exclusions are remembered per live body, ecotype name, and exact separation value, with immutable object slot identity. Every transition first drains old pending hits, then copies exclusion words and merges them using bitwise AND. Matching grids receive queued render and physical mask uploads. Switching spacing retains masks for both grids, so returning to the original spacing does not resurrect previously removed original instances. Returning to an unrelated spacing does not reinterpret another grid's subcell keys. Body identity and radius are fixed for each record's lifetime. Native launchpad/decal/terrain suppression stays active; exclusions are transient game state, not persisted workspace recipes.

Release queues restores into the same safe frame phase. Hiding the feature does not release it. `GroundClutterRenderer.Dispose` restores the original arrays before native disposal and suspends live recipes; a replacement renderer requeues them. Explicit feature unload waits CPU/GPU completion and restores immediately. Ownership comparisons reject overwriting arrays or templates replaced externally. Load/restore of workspace authoring data does not call these runtime operations.

## Harmony targets

All hooks belong to the feature's `FeatureRuntime` demand-scoped Harmony owner:

- `Universe.ExecuteNextClothSolvers`: prefix applies/restores pending transactions.
- `GroundClutterRenderer.RebuildFrameResources`: postfix rebuilds the retained original render pipelines alongside the active overrides so restoring after graphics settings changes is compatible.
- `GroundClutterRenderer.Dispose`: prefix restores native ownership before native destruction.
- `ClutterEcotypeRenderData.RebuildFrameResources`: prefix/finalizer scopes private material/shader bindings.
- `ClutterEcotypeRenderData.SortMaterialIds`, `CreateColorRenderer`, `CreateDepthPrePassRenderer`, `CreateShadowDepthRenderer`: transpilers replace exactly one `GroundClutterRenderer.MaterialBuffer` getter or `GetMaterialIndex` call per method. Unexpected match counts fail patch activation.
- `ShaderReference.CompileVariantWithCustomOptions`: prefix substitutes only `ClutterSolidFrag` compiled inside the owned material context.
- Public constructors of `GroundClutterPlacementData`, `ClutterEcotypeRenderData`, `ClutterEcotypePhysicalData`, `ClutterCubeCellGrid`, `ClutterViewResources`, `RenderCore.Mesh.SimpleVkMeshAtlas`: prefixes retain partial owned objects only inside construction context for failure cleanup.

## Reflection and binary dependencies

- `Celestial.<BodyTemplate>k__BackingField`, `Astronomical.bodyTemplate`; `object.MemberwiseClone`.
- `GroundClutterRenderer._renderPassInfo`, `_planetClutterMaxBoundingRadius`; public `PlanetPlacementData`, `PlanetEcotypeRenderData`, `PlanetPhysicalData` and `ExcludeInstance`.
- `PlanetRenderer._groundClutterRendererCreated`: distinguishes a live renderer from the nonnull disposed object retained when clutter is disabled.
- `Universe._vehicleUpdateTask`, `VehicleUpdateTask.SyncWindowBubbles`; bubble `Parent`, `ConstraintSim`, `GroundClutterStatics.Clear`, `PopulatePendingExclusions`, `RemoveExcludedClutterInstance`.
- `GroundClutterPlacementData._exclusionCache`; eight uint exclusion words per native cell; `GroundClutterRenderer.ExclusionData.AllIncluded`.
- `GroundClutterLodReference.BuildMaterialIndirection`; private setters of `MeshReference.HostPrimitives` and `PrimitiveMaterialIds`.
- `ModLibrary.AllMeshes`, `AllFiles`, `AllGltfs`; `SerializedCollection<T>.GetList`; glTF model/named mesh indexes and `MeshReference.Load(..., createDeviceMesh: false)`.
- `StagingPool._submitted`, `_commandBufferIndex`: discard the outer transaction's unsubmitted command buffers after preparation failure before pool disposal.
- `ClutterEcotypePhysicalData._compoundShapes`, `_primitiveShapes`: reachable partial-shape retirement; `ConstraintSim.UnlockShapes`, Bepu shape ownership/removal.
- Partial retirement inspects direct public/nonpublic instance fields only on the six captured native ownership classes. It recognizes `BufferEx`, `BufferPartitionInfo`, mapped memories, descriptor pools/layouts, samplers/image views, `SimpleVkTexture`, `SimpleComputePipeline`, `SimpleGraphicsPipeline`, and their collections. Physical `MeshAtlas`/`PlacementData` references are borrowed and skipped. Resource field layout changes require re-audit.
- `GroundClutterGpuMaterial` native layout, texture bindless IDs, flags bits 31/30 reserved by Pebbles, and the shader's `materialData.flags`, `diffuseTextureId`, `globalTextures`, `textureSampler`, `inUv`, diffuse conversion and terrain modulation statement.
- Native 256 candidates/cell, 16 physical scale buckets, five LODs, uint object/material indirection and transformed position/normal/UV layout; `CubeCellGrid.GetCellWidth` call convention follows the renderer's actual MeanRadius argument.

## Failure handling and verification limits

Preparation failures preserve the active graph. Completed resources use normal native disposal; interrupted construction uses once-only best-effort retirement of reachable owned fields so partial native initializers do not stop at their first null field. Cleanup errors retain the failed bundle and are exposed through controller `Faults`; they are not retried blindly because native disposal is not idempotent. Runtime records expose ecotype/material/repeated-vertex counts. The outer pool's partial commands are discarded; nested native pools own their submission/wait lifecycle.

This is not a claim of complete native allocation rollback: native constructors can allocate local buffers/textures/image views/compound children before publishing them to an object field. A native allocation failure in such a window may leave resources that Pebbles cannot reach, requiring renderer restart or game restart. Constructor capture and reachable cleanup do not solve that native ownership gap. Native draw, shadow, collision, bindless recycling, Vulkan resource failure and device-loss paths require in-game acceptance testing.

Acceptance must include stock capture/apply visual parity; multiple bodies sharing stock meshes/materials; native/source-color materials and sRGB formats; tiny visuals with large retained colliders; primitive and nondegenerate compound/hull collision; no-collision variants; all five LODs; spacing A→B→A and A→B→restore exclusions; queued release while hidden; graphics rebuild and renderer recreation; unload while solvers were previously scheduled; and deliberate preparation/retirement failures. Current source-color GLSL has been checked offline against the real game includes with both default and all optional material defines; native appearance remains unverified.

## Source evidence

The investigation and detailed native line map are retained in [the source map](../plans/PEBBLES_SOURCE_MAP.md) and [the design plan](../plans/PEBBLES_PLAN.md). These explain the native system; the current implementation and limitations above govern shipped behavior.
