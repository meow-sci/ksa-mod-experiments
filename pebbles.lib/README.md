# Pebbles

Pebbles is an independent bundled feature library in the Unscience workspace. It authors per-celestial ground clutter replacements and collision recipes. It has no standalone StarMap entry and references no other feature library.

## Usage and behavior

Select an exact celestial and **Capture target clutter into draft**. Select an ecotype and object variant to edit all five LODs, component meshes, material slots, native placement and collision. **Make everything the same** fills selected variant/LOD slots across the body, ecotype or variant without changing their identity or count. It includes a full shared-material recipe and fixed size/yaw actions. Individual mesh and texture selections remain available. Mesh imports use retained stock geometry or registered glTF CPU data; missing assets stay unresolved.

**Open mesh and collider Workshop** opens an independent textured preview. Orbit, pan, zoom, frame the mesh, add/fit box, sphere, capsule or cylinder primitives, and move/rotate/resize them with handles or numeric fields. Colliders can be selected, duplicated, mirrored and removed. Undo/redo and snapping operate on the detached draft. **Done** copies the result to its original recipe destination; **Cancel** discards the workshop edit. Neither action changes a planet. Loaded Workshop state requires explicit preview refresh.

**Apply to selected celestial** queues a validated replacement at the simulation boundary. Each body has one live override; replacing it keeps the original baseline. Applied items, resource counts, **Restore ecotype** and **Restore original clutter** live in the host's Live State window. Original and applied recipes can be captured separately. Hiding Pebbles does not stop its runtime. Workspace save/load and presets only change authoring data. A preset preserves the destination body and selection; ecotype/object signatures must still match that body when applied.

Placement includes biome aliases/masks, separation, generation range, XYZ scale bounds, orientation/yaw, distribution texture/tiling, slope masks, altitude spline and object-type texture controls. Collidable native placement requires uniform instance scale and cannot use smooth-normal orientation. Mesh replacement defaults to no collision; explicitly choose original colliders or author a custom list when desired. Collision can preserve originals, remove a variant's colliders, or use an authored list. Capsule height is the total height including both caps; rotations are XYZ degrees in the UI.

Ground-clutter diffuse alpha has a terrain-color convention, not ordinary opacity. Source-color handling and the separate opacity channel are explicit. Unsupported native shader settings are not presented as effective controls. Biome masks control placement of an ecotype; this does not introduce separate appearances inside a single shared ecotype. All five LOD slots and variant ordering are retained.

## Implementation ownership

- `Models/`: game-independent recipe schema and detached validation.
- `Assets/`: read-only registry discovery and privately owned CPU imports.
- `Runtime/`: source capture, private ecotype/mesh/material graphs, resource preparation, per-body commit/restore, exclusion and physics invalidation, feature-owned Harmony demand.
- `Preview/`: independent Vulkan color/depth target, geometry, material sampling and local camera; no stock thumbnail viewport, camera switch or Bepu simulation.
- `Workshop/`: detached state/history, local camera and gizmo math, collider editing and responsive editor UI.
- `PebblesSubmod*`: workspace bindings, main authoring controls, typed live records and lifecycle routing. Constructors remain detached; runtime resources are never serialized.

See [ground-clutter integration](../scope/ground-clutter.md) for exact native dependencies and [workspace conventions](../docs/WORKSPACE.md). The approved [source investigation](../plans/PEBBLES_SOURCE_MAP.md), [feature plan](../plans/PEBBLES_PLAN.md) and [Workshop plan](../plans/PEBBLES_WORKSHOP_PLAN.md) retain design rationale.

## Verification

Managed checks cover detached copying/serialization, placement and collider constraints, camera/gizmo math and undo history. Compilation verifies typed APIs against KSA 2026.9.7.5402. Native acceptance must cover private material descriptors, shadows, GPU retirement, stationary-cell collision refresh, exclusions, same-body replacement/restoration, scene changes, and Luna/Mars isolation. The preview uses conservative synchronization and may hitch while changing a large mesh or resizing; native rendering and gameplay are not established by managed checks.

Bound source textures must remain loaded while their recipes are applied or previewed. Native construction failure can leave allocations hidden in game-local variables; reachable resources are retired once and failures are reported, but a renderer/game restart may be required. See the [runtime failure limits](../scope/ground-clutter.md#failure-handling-and-verification-limits).
