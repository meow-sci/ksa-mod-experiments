# Pebbles

Pebbles is an independent bundled feature library in the Unscience workspace. It authors per-celestial ground clutter replacements and collision recipes. It has no standalone StarMap entry and references no other feature library.

## Usage and behavior

1. Pick a **Mesh** or use **Import GLB…**. Import selects the complete scene automatically; choose an individual imported mesh if needed. The mesh recipe can be prepared before selecting a planet.
2. Set the uniform **Scale**. **Preview and set up colliders** opens the textured preview with that scale intact. Add fitted box, sphere, capsule or cylinder shapes and adjust their positions, rotations and dimensions. Changing mesh scale resizes existing collider dimensions and offsets together. **Use colliders** enables/disables the authored shapes.
3. Choose a **Planet**, then check one or more clutter target types, or **All clutter types**.
4. Press **Apply to planet**. Every variant and all five LODs of those types receive the mesh, materials, scale and colliders. Other types keep their current settings. Restore an applied type or the entire planet in **Live State**.

The form does not expose placement, LOD, resource-budget or material-channel tuning. Native placement locations and LOD distances are retained. Selected types use a fixed instance multiplier of one, so the authored mesh/collider size matches the preview. Custom colliders automatically enable native primitive-list collision; smooth-normal orientation becomes surface-normal orientation when collision requires it. Mesh selection clears old colliders; scale is retained. Registry meshes use a neutral material; imported GLBs automatically use their own supported materials, including base-color textures as diffuse with source colors preserved, through both import and the regular mesh picker.

The collider editor retains orbit/pan/zoom, framing, grounding, fitted primitives, numeric and handle editing, duplicate/mirror/delete, snapping and undo/redo. It no longer exposes separate LOD meshes or texture channels. **Done** keeps the detached recipe; **Cancel** discards the edit. Finish the editor before changing the main mesh or applying. Neither editing nor Done changes a planet.

Planet and clutter-type identities are exact. Refresh discovers targets without applying anything; missing types stay unresolved and changed target signatures block Apply until refreshed. Settings presets preserve the destination planet and target selection while replacing the mesh/collider recipe. Workspace loads change authoring data only, leaving applied clutter and GPU resources untouched. Older detailed-form saves require choosing a replacement mesh and refreshing target types in the simplified form; a saved collider editor can still be explicitly refreshed and kept with Done.

## Loading your own GLB

Use **Import GLB…** to browse for a self-contained GLB, or expand **GLB file path** to paste an absolute path and press **Load file**. Import selects the complete scene and automatically assigns embedded base-color/diffuse, normal, PBR and opacity maps where supported. Choosing a different imported scene/mesh also refreshes its materials automatically; there is no separate texture-assignment step.

Import supports self-contained **GLB 2.0**, static triangle primitives, indexed or non-indexed geometry, float positions/normals and UV0. Missing normals are generated. UV0 is required for textured geometry and supports float or normalized unsigned byte/short streams. Scene hierarchy transforms and mirrored instances are baked; animation is not played, and individual meshes use raw local geometry. Imported meshes share the scale and collider controls.

Core metallic/roughness materials support embedded PNG/JPEG images, base-color factors, normal scale, AO/roughness/metallic factors, opaque or alpha-mask coverage and double-sided rendering. Material slots from different imported files remain distinct. Common Blender material extensions (specular, IOR, clearcoat, sheen, anisotropy, iridescence, transmission, volume, dispersion, unlit and emissive strength) fall back to core base-color/PBR materials, including when marked required. Import reports omitted effects; glass becomes solid, unlit becomes normally lit and emissive glow is omitted. Existing base-color textures and factors remain intact. Export without Draco/meshopt compression, skinning, morph targets, unknown required extensions, unsupported material/texture extensions, alpha blending, external/data image URIs or secondary UV texture references. Texture wrapping must repeat. Vertex colors, authored tangents and per-texture filter preferences are not reproduced; this is a conversion to the native clutter material model. See [GLB material conversion](../scope/ground-clutter-glb-materials.md).

Limits: 128 MiB file, 512 meshes, 4096 scene nodes, 2 million vertices/12 million indices/2048 primitives per selection, 4096 pixels per image dimension and 256 MiB retained CPU pixels per source. The import cache permits 16 file-content versions and 8 million retained vertices. Existing Apply budgets also count the copies repeated across variants and LODs: assigning a detailed model everywhere can exceed that budget.

Saved selections include the absolute path and SHA-256 of the file, plus mesh/material identity. Saves store recipes, not copies of the GLB. Save/load does no file import or GPU work. Explicit preview/Apply can reopen an exact saved source; missing or changed files block that action until you explicitly import and select a replacement. Already-cached versions remain immutable snapshots even if the file changes. Moving a workspace to another machine requires making its files available at the saved paths or selecting new imports.

Imported resources appear in **Live State → Imported GLB assets**. **Release all Pebbles state** first retires the live overrides and Workshop preview, then releases imported CPU/GPU resources before GUI rendering. Hiding the feature or loading a workspace does not purge that cache. A newly requested import/assignment cancels a pending cache purge; failed native body retirement retains imports for safety.

## Existing Blender materials

Try exporting existing materials unchanged first. Pebbles tolerates the common appearance extensions listed above; a new material or texture is usually unnecessary. It names unsupported extensions and suggests an export or baking fix. UV transforms remain unsupported because dropping them moves the artwork.

For procedural textures or unsupported mapping, save a separate export copy of the Blender file. Bake the material color into a new PNG using Cycles and a non-overlapping first UV map, with the new Image Texture node active in each material. For a diffuse color bake, enable Color and disable Direct/Indirect. After baking, use the saved image as Base Color on a simple Principled material in the export copy. Metallic/emissive or complex mixed shaders may need their intended color routed through emission and baked with Emit. Original source textures need not be edited. See the [Blender baking manual](https://docs.blender.org/manual/en/4.3/render/cycles/baking.html).

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
