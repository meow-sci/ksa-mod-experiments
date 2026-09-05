# Pebbles Workshop: visual mesh and collider authoring

**Proposal awaiting review; no implementation.** This extends [the Pebbles plan](PEBBLES_PLAN.md) in response to the request for a friendly in-game mesh/collision editor. Source baseline remains KSA 2026.9.7.5402. `D/` means the sibling `ksa-game-assemblies/current/decomp/` tree.

## Recommended experience

Add an **Edit mesh & colliders…** button to Pebbles. It opens a resizable **Pebbles Workshop** window containing one object on a neutral ground grid. The user can orbit, zoom and pan, choose its appearance, and add colored collision shapes around it. The ordinary game view and active vehicle remain under their existing controls outside this window.

Make this workshop part of the initial Pebbles scope. Basic box/sphere/capsule/cylinder editing should not be deferred to an unspecified advanced physics phase. Convex-hull generation and a physics simulation sandbox can follow later.

The common workflow should take very few actions:

1. **Pick an object.** Open a mesh already chosen in Pebbles, or use the searchable mesh picker. Automatically frame it and show its size in metres.
2. **Fit a starting shape.** Choose **Fit box**, **Fit sphere**, **Fit capsule** or **Fit cylinder**. Fit the whole prepared mesh group by default; allow a selected mesh component. No manually entering six coordinates to get started.
3. **Adjust visually.** Click a shape, then drag Move, Rotate or Resize handles. The mesh and translucent collider outlines update immediately. Numeric fields remain available for precision.
4. **Add detail only if useful.** Add or duplicate another primitive; rename it, mirror its position/orientation, or delete it. A duck could use one capsule for its body and a sphere for its head; a mug could use a cylinder body with a few small boxes around the handle. Multiple primitives can overlap in the resulting compound.
5. **Use this object.** **Done** copies the mesh/material/collider design into the calling draft. Pebbles' existing **Apply to [body]** publishes it to the selected clutter slots. **Save settings as preset** stores it for reuse.

No world collision shapes are rebuilt while dragging. “Immediate” means immediate preview feedback; applying the finished design uses the coordinated render/physics transaction already planned for Pebbles.

## Window layout

| Region | Contents |
|---|---|
| Top | Object name; mesh picker; Mesh / Materials / Colliders tabs; Undo and Redo. |
| Main preview | Textured object, ground grid, axis indicator, selectable collider outlines, selected-shape handles and a small controls hint. |
| Shapes list | Named shapes with type icon, selection, editor visibility, collision enabled state, duplicate and delete. Visibility and collision enable are separate. |
| Selected shape inspector | Type-specific dimensions, position, rotation, local/object coordinate mode, Fit and Reset. |
| Preview toolbar | Frame all / Frame selected; perspective or orthographic; front/side/top; mesh opacity; collider x-ray; ground contact; preview LOD and instance scale. |
| Footer | Design size, enabled shape count, pending changes/error text; Cancel / Done. The caller displays destination body/slot scope before its Apply. |

Use the largest region for the preview. On narrow windows, the inspector becomes a tab below it; numeric controls and Apply must remain reachable. Reuse shared padded form/table helpers, full-width pickers and consistent spacing. Do not require unexplained shortcuts: every essential operation has a labeled control and tooltip.

Suggested interaction defaults: left-click selects or drags a handle; right-drag on background orbits; middle-drag pans, with an on-screen Pan mode for trackpads; wheel/pinch equivalent zooms where supported. Front/side/top buttons provide easy planar editing. Mouse capture begins only inside the preview and lasts through that drag; releasing outside must end capture safely. Camera gestures never start through another widget.

Move/Rotate/Resize toolbar buttons always work. Optional keyboard shortcuts operate only when the preview has focus, are suppressed during text entry, and must be consumed before corresponding game actions. Escape cancels the current drag first. Opening the workshop does not change time warp, pause state, the main camera, the selected vehicle or the vehicle-editor mode. The user can use the game's normal pause separately.

## Friendly transform rules

Separate three concepts in both data and UI:

- **Object preparation:** the mesh's orientation, origin and base size, expressed in a canonical object frame, with Y up. Ground-align and Center buttons operate here. The mesh group, all LODs and colliders must use that same frame.
- **Collider editing:** each shape's local centre, rotation and dimensions in the prepared object frame. Editing a collider never deforms the rendered mesh.
- **Clutter placement:** instance size/orientation chosen later by the ecotype. Preview includes a size selector for base/min/max or one of the sixteen native scale levels. This does not change the recipe just to inspect it.

An object's collider design is shared across its five visual LODs. Switching preview LOD does not select a different collider set; the inspector should say so. This prevents collisions changing as the camera moves away.

Resizing should change physically meaningful dimensions rather than expose a misleading unrestricted XYZ scale:

| Shape | User-facing size | Allowed handles and native mapping |
|---|---|---|
| Box | Width, height, depth | Independent local axes; native `LengthX/Y/Z` are full lengths. |
| Sphere | Diameter | Uniform radius handle; native Radius = diameter/2. No ellipsoid from nonuniform scaling. Rotation is unnecessary and hidden. |
| Cylinder | Diameter, height | Linked radial resize and separate axial resize; native Y-axis length is full height. Rotation or an X/Y/Z align control chooses its direction. |
| Capsule | Diameter, total height | Linked radial resize and separate length; native `LengthY = totalHeight - 2 * Radius` is the centre-segment length. Keep total height ≥ diameter; a zero-segment sphere-like result must be tested or represented as a sphere. |
| Convex hull, later | Source points/component and bounded preparation transform | Generate a closed convex volume; preserve its computed centre offset. It will bridge concave gaps, which the UI must explain. |

All shapes support translation and useful rotation. Display degrees and metres, not engine quaternion fields or half-extents. Persist a stable rotation representation and convert through the game's actual XYZ-radian convention on Apply; avoid Euler jumps during a drag. Box overlays use half lengths, whereas native box creation uses full lengths. Capsule total-height vs segment-length and hull centre offsets must match between preview and applied colliders.

For multi-selection, Move/Rotate uses an explicit shared pivot and uniform group resize scales positions and dimensions together. Nonuniform group transforms can shear rotated primitives or turn spheres into ellipsoids; omit those transforms initially. Per-shape dimension controls remain available. Mirroring creates a positive-dimension reflected copy with the correct pose, never a negative-size Bepu shape.

Provide separate **Move mesh only** and **Move object and colliders together** modes for object-origin adjustments; default to preserving mesh/collider alignment. If nonuniform mesh preparation cannot transform existing analytic colliders exactly, block that combined operation and offer mesh-only followed by Fit again. Do not silently distort the visual overlay into a shape the physics engine will not receive.

## Assistance that makes it usable

- **Auto-fit:** conservative bounds fitting in the chosen object/shape frame, with a small adjustable margin. Clearly describe it as a starting approximation. No automatic expensive convex decomposition in the first version.
- **Fit a component:** fit a trunk, body or other mesh-group member without covering the whole object. More granular triangle-region selection can follow later.
- **Center / align axis / rest on ground:** common operations as buttons. Ground snapping uses the chosen collider's actual rotated extent.
- **Snap:** toggle grid spacing and angle increments; fine-adjust modifier while dragging; always show exact values. Defaults should scale sensibly with object size, not assume every asset is a metre tall.
- **Undo/redo:** one drag is one undo step; add, delete, fit, duplicate and transform commands are reversible. Cancel restores the workshop's entry snapshot; Done commits to the parent draft, not the planet.
- **Readable overlap:** selected collider bright with handles, others dim; toggle x-ray, mesh transparency and “show selected only.” Picking chooses the nearest hit with explicit cycling/list selection for nested shapes. Occluded handles must not steal clicks unexpectedly.
- **Honest scope:** distinguish editor hide from collider disable. Label the design as shared by the selected object variants across all LODs. Show how many destination slots will receive it before Apply.
- **Reuse:** save mesh, prepared origin/scale, material choices and collider list together as a reusable object recipe within Pebbles settings. Applying it to another body's chosen slots must preserve those destination targets.
- **Cost feedback:** show enabled primitive count and projected object×16-scale shape/compound cost before a world Apply. Default to a few simple shapes; establish practical limits with measured native acceptance.

Optional later **Test shape** can run a probe/drop in a separate owned Bepu simulation. It would require its own buffer pool, shapes and disposal; it must not use the game world's shared shape registry. It is not necessary for responsive shape editing. The first version can use analytic ray intersections and common geometry calculations for selection and fitting.

## Source feasibility and reuse boundaries

The game already supports the required collider descriptions:

- `D/KSA/ColliderTemplate.cs:11–21,54–68`: local `LocationAsmb`, XYZ-radian `Collider2Asmb`, shape offset and scaled shape construction.
- `D/KSA/{Box,Sphere,Capsule,Cylinder}ColliderTemplate.cs`: creates Bepu analytic primitives directly from length/radius fields. These templates can be constructed privately from a detached recipe at Apply.
- `D/BepuPhysics.Collidables/Capsule.cs:11–39`: Radius plus HalfLength; constructor length is the centre segment, not end-to-end height.
- `D/KSA/ClutterEcotypePhysicalData.cs:239–326`: creates each variant at sixteen uniform scales, assembles multiple authored primitives as a compound, and rotates/scales local offsets correctly. The workshop edits base dimensions; native instance scale is applied once afterward.
- `D/KSA/ConvexHullColliderTemplate.cs:17–51`: computes a centred hull, retains `ShapeOffsetCollider`, then creates scaled copies. This also allocates memory and accesses shared shapes; reading a hull offset is not a harmless preview operation if it triggers lazy construction.
- `D/KSA/MeshColliderTemplate.cs:51–54`: plain mesh collider registration throws unsupported; choose analytic primitives or a deliberately generated convex hull, not an unrestricted triangle-mesh option.

There are useful rendering precedents, but no verified drop-in mini-editor:

- `D/KSA.Rendering.Thumbnails/ThumbnailDynamic.cs:167–236,272` has separate commands/fence and an ImGui texture, but uses `ThumbnailCreator.Viewport` and shared global camera bindings; `ThumbnailRenderer.cs:117–191` is coupled to part geometry/material buffers. Copy the ownership/synchronization pattern, not its global state usage. `ThumbnailReference.cs:31–60` shows ImGui texture registration/removal.
- `D/KSA/VehicleEditor.cs:895–899,1253–1255` closes a game-save UI and changes main-camera mode/follow during creation/initialization. Do not create a dummy vehicle or open the stock editor simply to obtain a preview.
- `D/KSA/GenericGizmo.cs:218–225,245–277` registers with global static gizmo collections, and `GenericGizmoRenderData.cs:75–99` uses shared pass formats. Picking/math can inform the implementation; do not register workshop handles into the world's gizmo list.
- `D/KSA/GizmosRenderer.cs:237–328` demonstrates shape geometry, but its inputs include half-extents/half-lengths and its rendering is world-oriented. A private preview overlay must use the workshop's camera, clipping and dimensions.

Preferred implementation: a feature-owned offscreen color/depth target, private camera matrices and small preview mesh/line pipelines, displayed as an ImGui image. Collider shapes and handles can use a private depth-aware overlay, with deliberately separate x-ray drawing. Reuse immutable CPU mesh inputs through the planned mesh cache and private copied geometry where preparation requires it. No simulation viewport lease, global camera slot, vehicle creation or shared thumbnail interruption should be required.

The preview is a neutral geometry/material view. It should correctly show opacity, material assignments and prepared texture colors, but it must not claim exact planetary lighting, terrain blending or procedural placement. Those remain subject to the actual in-world Apply and native acceptance. Do not reuse the planet clutter shaders wholesale without supplying their full planet/global descriptor dependencies.

## Data, scheduling and workspace behavior

Add a detached object recipe containing stable object/collider IDs, the mesh/LOD/material design, preparation transform, a typed collider list, and mass/collision policy. A collider stores its type, local pose, type-specific dimensions and enabled flag; editor visibility/selection are separate authoring fields. No Bepu handles or renderer objects are serialized.

Preview shape dragging operates on this data and lightweight visualization geometry, never on `ConstraintSim`, `ColliderTemplate.GetShape`, `CreateOwnedShape`, live clutter arrays or active bubble statics. A rendered wire box does not need a registered Bepu box. GPU resources for a new mesh/texture are created only in a safe rendering phase and held through submitted preview frames. Geometry edits should update small buffers/uniforms or copied CPU geometry as appropriate, rather than recreating the whole preview renderer each mouse move.

Preview target lifetime must cover both the offscreen submission producing the image and the later ImGui submission sampling it. Use per-frame targets or proven synchronization; a signaled preview-production fence alone does not authorize overwriting/freeing the image. Version mesh/resize requests so stale completions cannot replace the latest selection. Render on changes or at a bounded refresh rate, with an explicit small resolution budget.

World Apply translates the validated recipe to private collider templates and enters the existing pre-cloth, CPU/GPU-safe commit path. Preview and applied shape conversion must share the same dimension/pose rules. Bake base dimension edits into templates; do not multiply authoring scale a second time. Validate finite positive dimensions, valid rotations, hull volume, local offsets, collider reach and mass. Partial failure keeps the prior applied record intact.

Use the current `IWorkspaceFeature` lifecycle, including `ReleaseLiveState` and explicit `ConfigureRuntime(FeatureRuntime)` patch groups. Preview resource/patch demand must be declared independently of whether a body has a live override. The host's existing HotkeyGuard covers text fields; preview-specific mouse/shortcut consumption must also be scoped and verified.

Workspace capture includes durable editor selection, camera pose, projection mode, display settings and the complete draft collider list. Loading restores authoring data only and cancels an uncommitted drag. It must not register shapes, rebuild applied clutter or allocate/dispose preview GPU resources. If loaded mesh/material choices differ from currently prepared preview resources, mark **Preview needs refresh** and require an explicit **Refresh preview**; do not hide GPU allocation behind deferred load side effects. Keep the last image clearly marked stale until refreshed. Undo history and active pointer capture are transient, not replayed from a save.

Closing/hiding the workshop retains its committed draft and does not affect applied clutter. Stop preview drawing while hidden; release owned resources through explicit lifetime management without using authoring visibility as the applied feature's lifecycle. Opening an applied item through Live State makes an editable copy; Update remains explicit.

## Revised delivery and acceptance

1. **Preview proof:** arbitrary multi-primitive mesh in a private resizable target, orbit/pan/zoom, no main-camera/editor/thumbnail interference; valid rendering and retirement under resize/settings changes.
2. **Primitive workshop:** fit/add/select/transform box, sphere, capsule and cylinder; correct dimensions, overlays, input capture and undo/redo. Numeric editing and drag handles are both required for a friendly first release.
3. **Recipe integration:** Done/Cancel, exact target mapping, preset round-trip and safe world Apply with custom collider compounds. This joins the initial Pebbles Phases 1–3.
4. **Optional extensions:** mesh/component convex hulls, more sophisticated auto-fitting, isolated collision test simulation, triangle-region selection and advanced grouping. These remain reviewable later work.

Acceptance must include asymmetric meshes with off-centre origins, non-unit scales, rotated capsules/cylinders, full-vs-half dimensions, overlapping primitives, disabled versus hidden shapes, all five LODs and sixteen scale samples. Compare the preview against actual applied collider wireframes and contact tests. Check ground alignment, local offsets, compound reach, shape/memory budgets, repeated editing/Apply/restore, failed preparation, mouse release outside the window, text entry without game hotkeys, editor coexistence, hidden HUD, save/load mid-drag and stale-preview behavior. No native tests have been run for this proposal.
