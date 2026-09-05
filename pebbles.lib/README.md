# Pebbles

Pebbles is an independent bundled feature library in the Unscience workspace. It authors per-celestial ground clutter replacements and collision recipes. It has no standalone StarMap entry and references no other feature library.

## Usage and behavior

Select an exact celestial and **Capture target clutter into draft**. Select an ecotype and object variant to edit all five LODs, component meshes, material slots, native placement and collision. **Make everything the same** fills selected variant/LOD slots across the body, ecotype or variant without changing their identity or count. It includes a full shared-material recipe and fixed size/yaw actions. Individual mesh and texture selections remain available. Mesh choices include retained stock geometry, registered glTF CPU data and runtime `.glb` imports; missing assets stay unresolved.

**Open mesh and collider Workshop** opens an independent textured preview. Orbit, pan, zoom, frame the mesh, add/fit box, sphere, capsule or cylinder primitives, and move/rotate/resize them with handles or numeric fields. Colliders can be selected, duplicated, mirrored and removed. Undo/redo and snapping operate on the detached draft. **Done** copies the result to its original recipe destination; **Cancel** discards the workshop edit. Neither action changes a planet. Loaded Workshop state requires explicit preview refresh.

**Apply to selected celestial** queues a validated replacement at the simulation boundary. Each body has one live override; replacing it keeps the original baseline. Applied items, resource counts, **Restore ecotype** and **Restore original clutter** live in the host's Live State window. Original and applied recipes can be captured separately. Hiding Pebbles does not stop its runtime. Workspace save/load and presets only change authoring data. A preset preserves the destination body and selection; ecotype/object signatures must still match that body when applied.

Placement includes biome aliases/masks, separation, generation range, XYZ scale bounds, orientation/yaw, distribution texture/tiling, slope masks, altitude spline and object-type texture controls. Collidable native placement requires uniform instance scale and cannot use smooth-normal orientation. Mesh replacement defaults to no collision; explicitly choose original colliders or author a custom list when desired. Collision can preserve originals, remove a variant's colliders, or use an authored list. Capsule height is the total height including both caps; rotations are XYZ degrees in the UI.

Ground-clutter diffuse alpha has a terrain-color convention, not ordinary opacity. Source-color handling and the separate opacity channel are explicit. Unsupported native shader settings are not presented as effective controls. Biome masks control placement of an ecotype; this does not introduce separate appearances inside a single shared ecotype. All five LOD slots and variant ordering are retained.

## Loading your own GLB

1. Capture the target celestial's clutter, then expand **Load GLB from disk**.
2. Use **Browse .glb** (folder navigation, filtering, double-click selection) or paste an absolute file path and choose **Load file**.
3. Choose **Complete scene** to assemble the default scene with node transforms and instances, or an individual mesh in its own local coordinates.
4. Choose **Use in selected variant and open Workshop** to preview, resize and author colliders, or **Use in all variants on this body**. Both assign geometry and imported materials to all five LODs in the draft. Collision starts disabled.
5. Finish the Workshop and use the main **Apply** action to replace live clutter.

Import supports self-contained **GLB 2.0**, static triangle primitives, indexed or non-indexed geometry, float positions/normals and UV0. Missing normals are generated. UV0 is required for textured geometry and supports float or normalized unsigned byte/short streams. Scene hierarchy transforms and mirrored instances are baked; animation is not played, and individual meshes use raw local geometry. Imported meshes share the usual scale/texture/collider controls.

Core metallic/roughness materials support embedded PNG/JPEG images, base-color factors, normal scale, AO/roughness/metallic factors, opaque or alpha-mask coverage and double-sided rendering. Material slots from different imported files remain distinct. Export without Draco/meshopt compression, skinning, morph targets, required extensions, material extensions, emissive channels, alpha blending, external/data image URIs or secondary UV texture references. Texture wrapping must repeat. Vertex colors, authored tangents and per-texture filter preferences are not reproduced; this is a conversion to the native clutter material model. See [GLB material conversion](../scope/ground-clutter-glb-materials.md).

Limits: 128 MiB file, 512 meshes, 4096 scene nodes, 2 million vertices/12 million indices/2048 primitives per selection, 4096 pixels per image dimension and 256 MiB retained CPU pixels per source. The import cache permits 16 file-content versions and 8 million retained vertices. Existing Apply budgets also count the copies repeated across variants and LODs: assigning a detailed model everywhere can exceed that budget.

Saved selections include the absolute path and SHA-256 of the file, plus mesh/material identity. Saves store recipes, not copies of the GLB. Save/load does no file import or GPU work. Explicit preview/Apply can reopen an exact saved source; missing or changed files block that action until you explicitly import and select a replacement. Already-cached versions remain immutable snapshots even if the file changes. Moving a workspace to another machine requires making its files available at the saved paths or selecting new imports.

Imported resources appear in **Live State → Imported GLB assets**. **Release all Pebbles state** first retires the live overrides and Workshop preview, then releases imported CPU/GPU resources before GUI rendering. Hiding the feature or loading a workspace does not purge that cache. A newly requested import/assignment cancels a pending cache purge; failed native body retirement retains imports for safety.

## Implementation ownership

- `Models/`: game-independent recipe schema, detached validation, bounded GLB container/geometry/scene decoding, exact file identities, material-slot ordering and pixel conversion.
- `Assets/`: read-only registry discovery, private CPU geometry imports, native embedded-image decoding and lazily uploaded GLB textures.
- `Import/`: feature-owned file browser and explicitly bound navigation/selection state.
- `Runtime/`: source capture, private ecotype/mesh/material graphs, resource preparation, per-body commit/restore, exclusion and physics invalidation, feature-owned Harmony demand.
- `Preview/`: independent Vulkan color/depth target, geometry, material sampling and local camera; no stock thumbnail viewport, camera switch or Bepu simulation.
- `Workshop/`: detached state/history, local camera and gizmo math, collider editing and responsive editor UI.
- `PebblesSubmod*`: workspace bindings, main authoring controls, typed live records and lifecycle routing. Constructors remain detached; runtime resources are never serialized.

See [ground-clutter integration](../scope/ground-clutter.md) for exact native dependencies and [workspace conventions](../docs/WORKSPACE.md). The approved [source investigation](../plans/PEBBLES_SOURCE_MAP.md), [feature plan](../plans/PEBBLES_PLAN.md) and [Workshop plan](../plans/PEBBLES_WORKSHOP_PLAN.md) retain design rationale.

## Verification

Managed checks cover detached copying/serialization, placement and collider constraints, camera/gizmo math, undo history, GLB geometry/container validation, transform baking, exact file identities, material-slot isolation and pure pixel conversion. Compilation verifies typed APIs against KSA 2026.9.7.5402. Native acceptance must cover private material descriptors, shadows, GPU retirement, stationary-cell collision refresh, exclusions, same-body replacement/restoration, scene changes, and Luna/Mars isolation. The preview uses conservative synchronization and may hitch while changing a large mesh or resizing; native rendering and gameplay are not established by managed checks. GLB acceptance additionally needs actual PNG/JPEG decode/upload, texture orientation, masks, multi-material scenes, file changes, preview/live sharing and cache release.

Bound source textures must remain loaded while their recipes are applied or previewed. Native construction failure can leave allocations hidden in game-local variables; reachable resources are retired once and failures are reported, but a renderer/game restart may be required. See the [runtime failure limits](../scope/ground-clutter.md#failure-handling-and-verification-limits).
