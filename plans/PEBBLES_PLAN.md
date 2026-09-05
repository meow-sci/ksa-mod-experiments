# Pebbles: ground-clutter investigation and proposed implementation

> Historical approved proposal, retained as design evidence. Implementation is now authorized and present in `pebbles.lib`. See [implementation status](PEBBLES_IMPLEMENTATION.md) and the [current feature README](../pebbles.lib/README.md). Statements below about awaiting approval describe the original planning turn.

**Status: proposal awaiting review; no implementation authorized or added.**
Investigation date: 2026-09-05. Reference: **KSA 2026.9.7.5402**, the current baseline in [FULL_SCOPE](../scope/FULL_SCOPE.md). This is a source-level reverse engineering report and implementation plan, not a claim of in-game verification. The repository still ships 25 features.

## Recommended outcome

Pebbles should be a bundled feature for replacing surface clutter on an **exact celestial**. Its main action should be: choose a mesh, optionally choose textures, fill every selected clutter slot, then Apply. Advanced controls should expose ecotypes, object variants, all five LODs, mesh groups, material assignments and the placement settings actually consumed by the game.

The requested [Pebbles Workshop](PEBBLES_WORKSHOP_PLAN.md) adds an orbitable mesh preview and direct manipulation of box/sphere/capsule/cylinder colliders. Include this friendly primitive editor in the initial scope: preview changes immediately, while Done updates the draft and world Apply remains explicit. Its detailed plan covers auto-fit, gizmos, undo/redo, shape dimensions and an isolated rendering approach.

Per-celestial control is supported by the game's architecture. However, native object templates and materials are shared, and the renderer has a global material table. Pebbles must isolate its replacement graphs and GPU materials; editing the shared rock template would affect both Luna and Mars.

Implement the complete body/ecotype/variant tool first, including placement biome masks. **Different replacement appearances within different biomes of the same ecotype require an additional routing design.** Merely cloning an ecotype and splitting its biome mask produces overlaps and holes at biome blends. That extension is planned separately below, rather than being disguised as a working native feature.

The first implementation milestone should prove resource isolation, safe replacement, restoration and collision synchronization. The UI follows that proof. No existing game debug editor is a complete safe Apply implementation.

## Reading guide and evidence convention

- Sections 1–5 explain the game and its constraints.
- Section 6 is the proposed control inventory and scope.
- Sections 7–10 specify ownership, application, integration and implementation order.
- Sections 11–12 define acceptance and review decisions.
- [PEBBLES_SOURCE_MAP.md](PEBBLES_SOURCE_MAP.md) contains the detailed pipeline, source locations, binary contracts and investigation limits.
- [PEBBLES_WORKSHOP_PLAN.md](PEBBLES_WORKSHOP_PLAN.md) specifies the mini mesh/collider editor requested after the initial investigation.

Source references in both documents use `D/` for `../ksa-game-assemblies/current/decomp/` and `C/` for `../ksa-game-assemblies/current/Content/`. Line numbers refer to this decompilation, not original developer source lines embedded in logging strings. Repository-relative references have no prefix. Findings are from local game source, shaders and parsed XML; no external API descriptions were substituted.

## 1. The actual hierarchy

```text
Celestial (exact identity)
  BodyTemplate.GroundClutterReference
    Ecotypes[]                         e.g. Rocks, LargerRocks, Grass, Tree
      Placement                        biome mask, distribution, scale, rotation…
      CollisionType                    None / PrimitiveList / ConvexHullList
      ClutterObjects[]                 alternatives selected during generation
        ClutterObjectTemplate          often a shared registered asset
          Lods[5]
            Meshes[]                   ALL meshes in this group render together
              HostPrimitives[]         ALL primitives participate
            MaterialReferences[]       mapped from atlas material IDs
            MinScreenSizePixels
            CastShadows
          Colliders[] / Substances / derived mass
      MaterialReferences[]             derived, deduplicated material inventory
```

An ecotype can span several biomes; a biome can contain several ecotypes. There is no native one-to-one biome → mesh relationship. A tree's trunk and leaves can be separate meshes/materials within a single LOD; these are components, not randomized alternatives. The randomized alternatives are the ecotype's object templates.

Exactly five LODs are required, even when all five refer to the same mesh. The render constructor enforces `5 * objectCount <= 256`, so the current limit is 51 variants per ecotype. It constructs an ecotype-owned GPU atlas from CPU mesh primitives. Rocky's ring-specific primitive-zero GPU conversion is inappropriate here.

Evidence: `D/KSA/AstronomicalTemplate.cs:53`, `ClutterEcotypeReference.cs:15`, `ClutterObjectTemplate.cs:73`, `GroundClutterLodReference.cs:13–29,83–121`, `ClutterEcotypeRenderData.cs:128–204`.

## 2. What the shipped content actually uses

Parsed active XML, excluding XML comments:

| Body | Ecotype | Variants | Biomes | Separation / range (metres) | Collision |
|---|---|---|---|---|---|
| Earth | Grass | Grass | Grass, GrassMountains | 1.45 / 80 | None |
| Earth | Tree | TreeType1–3 | Grass, GrassMountains | 18.3 / 5,500 | PrimitiveList |
| Earth | SmallTree | TreeType4–6 | Grass, GrassMountains | 30 / 2,500 | PrimitiveList |
| Earth | Shrub | TreeType10–12 | Grass, GrassMountains | 5 / 750 | PrimitiveList |
| Luna | Rocks | RockType1–7 | Surface, Craters, Maria | 25 / 2,250 | PrimitiveList |
| Luna | LargerRocks | RockType1–7 | Surface, Craters | 130 / 12,000 | PrimitiveList |
| Luna | SmallerRocks | RockType1–7 | Surface, Craters, Maria | 6.5 / 350 | PrimitiveList |
| Mars | Rocks | RockType1–7 | SandAndCliffs, SandOnly, VallesMarineris | 25 / 2,250 | PrimitiveList |
| Mars | LargerRocks | RockType1–7 | same three | 130 / 12,000 | PrimitiveList |
| Mars | SmallerRocks | RockType1–7 | same three | 6.5 / 350 | PrimitiveList |

These are the three bodies with `GroundClutter` definitions in the inspected Core astronomical file. This is not a hardcoded target list: installed content can add more. A body with no native clutter should be shown as unsupported for replacement initially, with the reason visible. Creating a new clutter system on such a body is an explicit later capability.

Earth defines seven biome aliases, Luna three, Mars four. Mars's Poles are excluded from its current rock groups. The available asset files contain seven rock templates, fifteen tree templates and one grass template; not all tree templates are assigned to an active ecotype.

Luna and Mars use the same `GenericRock` atlas and `LunaRocksMaterial`. Trees use trunk/leaf materials and distant card materials. Grass uses diffuse, normal, packed PBR, opacity and thickness maps. The `Material` block still present directly under Earth's Grass ecotype in `Astronomicals.xml` is not the authoritative material route in this version: the ecotype's material list is `[XmlIgnore]` and is populated from object LOD references. The active Grass material comes from `GroundClutter/GrassAssets.xml`.

Evidence: `C/Core/Astronomicals.xml:554–565,1087–1313,1925–1930,2585–2668,2779–2787,3054–3137`; `C/Core/GroundClutter/{GenericRockAssets,EarthTreesAssets,GrassAssets}.xml`; `D/KSA/ClutterEcotypeReference.cs:21,188–235`.

## 3. How placement, rendering and collision work

The game streams candidates near the camera using a cube-sphere grid. Each cell has 16 × 16 deterministic candidate positions. It jitters them, samples a distribution texture, blends biome weights, evaluates terrain/procedural modifiers, applies slope and altitude acceptance, selects a variant and one of sixteen scale levels, then finalizes orientation and position.

It is not instantiating a tree GameObject for every visible tree. Instances live in GPU buffers. Mesh/texture replacement should keep this instanced rendering system.

Render generation and collision generation use related but separate grids and buffers. Rendering follows the nearby celestial/camera; collision generation follows vessels and physics bubbles. GPU collision results are read back, then Bepu static colliders are installed in the bubbles. Physics also uses the render atlas and computed object radii. Changing a mesh can therefore affect collision dependencies even if the user thinks of it as a visual edit.

Destroyed clutter is tracked by celestial, ecotype index, cell and subcell. That index structure is why the default bulk operation must **retain variant count/order and ecotype count/order**. Placement changes that change cell topology need an explicit exclusion-state policy; restoring the original appearance must not promise to undo collisions or destruction that happened while the override was active.

The game has a live placement editor, but its inexpensive update branch forces only render regeneration. Its separation/range branch recreates only render data. Pebbles must pair render, placement, physical caches, bounds and bubble-static refresh, rather than copying that editor's change handling.

## 4. Materials: real capabilities and traps

The GPU clutter material is six uints: diffuse, normal, AO/roughness/metallic, opacity, thickness and a flags word. Native conversion populates the five texture handles; shader features primarily use compiled pipeline flags. Pipeline grouping, primitive-to-material indexes, depth rendering and shadows must all agree.

Native material capabilities include terrain blending, opacity cutouts, double-sided rendering, shadow casting/receiving, upward-biased foliage normals, extra specular, distance dithering and transmission enabled by a thickness map. Texture replacement and pipeline-flag changes have different costs.

Some attractive-looking controls **do not work merely by setting their C# fields**:

- `AlphaCutoff` exists in the reference but is not uploaded; depth and shadow shaders use a hardcoded 0.5 threshold.
- Inherited emissive, thin-film and alpha-map references are not part of the clutter GPU material. Opacity is its separate supported cutout channel.
- Normal-map power is not uploaded by `ToGpuMaterial`.
- There are no native per-material tint, UV-transform, roughness-multiplier or metallic-multiplier fields in this GPU record.
- Terrain blend distances and transmission tuning are shader constants.

These belong in an explicitly optional shader-extension phase. The initial UI must not expose inert sliders.

Texture color also deserves care: clutter diffuse alpha encodes its terrain-color behavior; it is not ordinary transparency. Even with the terrain-mask feature off, the shader can multiply diffuse color by instance terrain color. Provide a deliberate **source colors** mode using an owned prepared texture with the correct alpha/color encoding, preserving cutouts in the separate opacity map. Verify texture format/gamma in-game. Do not alter borrowed texture pixels. Turning off `UseTerrainMask` alone is insufficient to promise exact source colors.

## 5. Runtime constraints that govern the design

| Constraint | Consequence for Pebbles |
|---|---|
| Global templates and hash-deduplicated GPU materials | Clone mutable references; provide owned material tables for owned render data. |
| LOD resolution and material lists are cached | Rebuild fresh LOD mapping and ecotype material inventory; editing `MeshIds` alone does nothing. |
| Five LODs, 51 variant limit | Validate before allocating; fill five slots explicitly. |
| Sixteen quantized scales | Fixed scale is straightforward; min/max produces sixteen discrete interpolated values, not continuous random scales. |
| Uniform scale required for collidable ecotypes | Disable anisotropic scale with collisions; validate all axes, not just X. |
| Smooth-normal orientation forbidden for collidable ecotypes | Preserve native validation. |
| No serialized random seed or candidate-jitter control | Fixed orientation/scale/appearance are native; new seed/jitter sliders require synchronized shader work. |
| Both vehicle and cloth jobs read clutter shapes | Commit physical changes after both jobs have completed and before new cloth snapshots start. |
| GPU work can still reference old atlases/descriptors | Retain outgoing objects through safe completion; a successful setter is not safe disposal. |
| Native renderer recreation on settings changes | Reconcile active records with new renderer generations; avoid stale handles and double disposal. |
| Global graphics/collision settings gate the feature | Show effective availability. A body override does not implicitly enable global settings. |
| Biome masks use one uint | Require resolved IDs in 0–31; do not let C# shift wrapping silently select a different biome. |
| Constant-grid candidate count and repeated atlas inputs | Show estimated candidate/triangle/GPU cost before Apply; identical appearance does not eliminate render cost. |

## 6. Proposed complete control inventory

Refresh labels below describe the planned implementation, not a claim that a public native setter handles the operation: **M** = owned material upload; **R** = render resources/pipelines; **G** = regenerate placements and invalidate corresponding collision results; **P** = paired render/physical reconstruction; **S** = shader extension.

### Scope and easy actions

| Control | Scope / semantics | Refresh |
|---|---|---|
| Exact celestial | Persist body identity, never list position; missing body blocks Apply. | Selection only |
| All ecotypes / selected ecotypes | Bulk authoring command expands into explicit destination mappings. | On Apply |
| Individual variant | Replaces that variant wherever its ecotype generates it. This is not a single placed instance. | P |
| All LODs / one LOD | Five explicit slots; retain sensible distant meshes unless user fills them all. | P |
| Replace entire mesh group / one mesh component | Whole group is default, so replacing a tree does not accidentally retain its old canopy. | P |
| One material for everything / per material slot | Correctly rebuild primitive material indirection even for multi-material imports. | M/R |
| Make appearances identical | Fill selected variant/LOD slots without deleting variants. | P |
| Make size/rotation identical too | Separate option sets equal min/max values; bulk mesh filling otherwise retains each ecotype's placement settings. | P/G |
| Enable/disable body or ecotype | Pebbles-owned suppression of rendering and collision generation/installation; do not toggle global game settings or remove indexed slots. | P |
| Copy stock / copy live / reset draft | Authoring operations only. | None |
| Apply to body / restore affected group / restore body | Explicit runtime operations; report completion only after successful commit. | As needed |

Bulk selection and assignment expansion should have a visible affected-slot count. Use explicit mappings instead of persistent overlapping wildcard rules. Applying a new body recipe replaces that body's complete Pebbles override; unmentioned old overrides return to that body's captured baseline. Copy live to draft supports incremental editing without accidental loss.

The main form should put the celestial selector first, then the bulk replacement mesh/material controls and an affected-slot summary beside Apply. Advanced disclosures show a searchable ecotype/variant list and the selected item's five LOD groups, material slots and placement form. A biome filter on that list must be labeled as a discovery filter; it does not make a mesh edit biome-local. Use the existing `SubmodUI`, `FormField` and responsive `FormGrid` patterns with full-width pickers and padded cells. Applied items and their management belong in the host's Live State window.

### Placement and variation

| Control | Actual setting / practical behavior | Refresh |
|---|---|---|
| Allowed biomes | Alias selection → validated `BiomeMask`; blank native string means all. Provide explicit All/None in the recipe so an empty selection cannot accidentally mean all. | G |
| Object separation | Metres; changes cube grid resolution/topology and density, not an exact pairwise spacing guarantee. | P |
| Generation range | Metres; visual coverage and allocation cost, not the physics bubble radius. | P |
| Distribution texture + tiling | Red channel is candidate acceptance; white allows all otherwise-valid candidates. Supported 2D bound texture with lifetime held. | G |
| Density multiplier | Convenience that adjusts a prepared distribution map or documented spacing conversion; not a nonexistent native scalar. Label the chosen meaning. | G/P |
| Min/max scale XYZ | Sixteen interpolated scale levels; equal values eliminate scale variation. Uniform-only when collidable. | P |
| Orientation | Up, surface normal, surface normal plus gradient; smooth surface normal for noncollidable only. | G/P |
| Min/max rotation | Degrees in UI; native references use radians and upload degrees. Equal values fix yaw in Up/SurfaceNormal modes. The native downhill/gradient branch ignores yaw limits; disable these inputs there. | G |
| Slope strength, contrast, bias | Existing mask equation; full finite stock-compatible values, not stock debug-widget clamp ranges. | G |
| Altitude density spline | Metres vs density, editable keys and in/out tangents; sampled to 1×1024 R8 LUT over body's terrain range. | G |
| Use object-type texture | Select variant by texture instead of ordinary random choice. | G |
| Object-type texture + tiling + jitter | Separate from distribution texture and position jitter. Preserve variant order as the texture encodes that order. | G |
| Add/remove/reorder variants | Structural editor after exclusion/identity support; bounded by 51. Not needed for uniform appearance. | P, later |
| Change appearance only inside selected biome | Requires weighted routing extension described below; initial biome control changes placement membership. | S, later |
| Seed / positional jitter | Shader extension; no current reference setting. | S, later |

Stock grass uses slope contrast 25 even though the game's debug UI permits a smaller range. Pebbles must accept valid stock values rather than clamp them when loading a recipe.

### Geometry, materials and rendering

| Control | Scope / behavior | Refresh |
|---|---|---|
| Existing clutter object preset | Copy its five LOD groups and material assignments into selected destination variants. Collider choice remains explicit. | P |
| Loaded game mesh / named glTF mesh | Use retained CPU primitives; private CPU import where needed. Static geometry only; selecting a kitten mesh does not animate it. | P |
| Mesh-local scale, rotation, offset / ground-align | Apply to private copied CPU vertices/normals; recalculate bounds. Optional geometry preparation, independent of placement rotation. | P |
| LOD minimum screen sizes | Per variant, pixels; validate descending nonnegative thresholds, show final cull/fade behavior. | R; P initially for coherent bounds |
| Per-LOD and per-material cast shadows | Both participate; effective result also depends on global shadow settings. | R |
| Diffuse / normal / AO-roughness-metallic textures | Per owned material assignment; include explicit keep/source/replacement behavior and valid fallback maps. | M |
| Opacity / thickness textures | Optional; adding/removing changes cutout/transmission pipeline flags. | M/R |
| Preserve source colors / terrain color | Prepare diffuse alpha/color convention; separately control terrain blending. | M/R |
| Terrain mask, double-sided, receive shadows | Native pipeline flags; ensure color, depth and shadow pipelines agree. | R |
| Bias normals up, extra specular, distance dither | Native flags; useful to switch off grass behavior on replacement props. | R |
| Alpha cutoff, tint, normal strength, UV transform, PBR multipliers, emissive | Optional feature-owned shader/material extension with parity across relevant passes. | S |
| Counts, atlas bytes, active cells, render/physics readiness, effective global gates | Read-only diagnostics in live inspector. No editable raw Vulkan handles. | None |

The initial asset browser should cover the same useful loaded game assets as Rocky, support multi-primitive geometry correctly, and display why an asset is unavailable. Validate position/normal/UV streams and indices, and normalize private index streams to the destination atlas width: the current atlas's mixed ushort/uint staging path has a source-observed allocation hazard. Arbitrary file import can be a later extension; it is not necessary to promise a new content loader for the first release.

### Collision and exclusions

| Control | Proposed semantics | Refresh |
|---|---|---|
| Collision policy | Keep original colliders / no colliders for replaced variants / replacement's supplied or Workshop-authored colliders. Default arbitrary visual swap to no colliders until a collider recipe is explicitly selected or authored. | P |
| Edit mesh & colliders | Private orbitable preview, primitive auto-fit and direct Move/Rotate/Resize controls; see the Workshop plan. Preview edits are immediate; Done changes the draft; Apply builds world shapes safely. | Preview only, then P on Apply |
| Approximate geometry collider | Optional feature-generated bounded primitive/convex-hull proxy after performance/geometry acceptance. There is no native Mesh collision mode that automatically converts render geometry. | P, advanced |
| Collision scope | Native placement/collision mode is ecotype-wide. Empty collider lists on private variant clones can suppress only those variants' statics while retaining siblings and the ecotype's existing placement path; handle native warnings deliberately. Whole-group None also changes the native terrain snapping path. | P |
| Original collision mismatch | Explicit status when visual geometry and retained colliders differ. Do not suggest that copying a mesh updates authored collider shapes. | None |
| Mass/substances | Display native mass and scale-cubed behavior initially. Local mass override is a later physical recipe option; never edit shared SubstanceLibrary densities. | P, advanced |
| Break energy | Native `BreakEnergyPerKg` is global. A per-body breakability control requires scoped collision handling; do not expose it as if local. | Patch extension |
| Destroyed-instance exclusions | Preserve while identity topology matches; never clear on save/load. Regeneration alone is not “restore destroyed clutter.” | G/P |
| Launch-site exclusions | Keep native reserved areas initially; future local settings need explicit GPU data changes. Do not edit global landmark definitions. | G, advanced |

Collision removal must refresh installed bubble statics, not just visibility. Per-variant empty collider lists keep the ecotype on its collidable placement path, including uniform-scale and orientation constraints. An explicit whole-ecotype None policy can remove collision generation as well; show its broader placement effect. The current non-None enum labels both flow through authored collider lists in `PopulateColliderPrimitives`; do not advertise an unproven behavior difference between them.

Compute physical collection reach from actual collider extents, local offsets and permitted scales, separately from visual/shadow bounds. A tiny replacement mesh retaining a large original collider must not shrink the physics grid enough to miss that collider. Reject invalid or unbounded geometry; validate a deliberate positive finite mass policy for collidable variants.

## 7. Architecture and resource ownership

Add **one `pebbles.lib.csproj`**, hosted by `unscience`. There is no standalone mod entry or second HotkeyGuard. No feature references Rocky or another feature library.

Proposed components (names are provisional):

| Component | Responsibility |
|---|---|
| `PebblesSubmod` and Ui/Workspace/Live partials | Feature contract, form, typed per-body live item and inspector. |
| `ClutterDiscovery` | Snapshot exact body/ecotype/variant/LOD/material structure, aliases, capabilities and source asset IDs. |
| `PebblesRecipe` / validation | Detached data, bulk expansion, target mapping, finite/budget checks and structural signatures. |
| `ClutterOverrideController` | One applied recipe per body; baseline graph, staged replacement, commit/restore and lifecycle reconciliation. |
| `ClutterGraphBuilder` | Private references, all five LODs, material indirection, unique material identities; no global asset registration. |
| `ClutterMeshSourceCache` | Borrowed immutable CPU meshes vs owned imports/transformed copies with explicit leases. |
| `ClutterMaterialResources` | Owned local GPU table and any prepared textures; no mutation of shared materials. |
| `ClutterRenderBridge` / `ClutterPhysicsBridge` | Narrow version-checked game adapter, safe scheduling and paired rebuilds. |
| `PebblesPatches` | Only necessary targeted patches through host-managed `FeatureRuntime` groups, with explicit demand and targeted release. |
| `PebblesWorkshop` / preview resources | Detached object/collider editor, local undo history, private preview camera/target, picking and transform handles; no game-world shapes during editing. |

Extract generic read-only mesh/texture discovery from `ksa-rings.lib/RingAssetCatalog` into `ksa-abstractions.lib/Assets` if shared unchanged. A ring adapter may remain for compatibility. Keep ring conversion and ring replacement coordination in `ksa-rings.lib`. Clutter-specific game access stays in Pebbles. Generic picker extraction is optional and should remain small.

Workspace rules apply completely: every authoring setting, selection, filter, disclosure and durable scroll state is explicitly bound. `PrepareRestore` validates detached data and returns authoring-only setters. It must not discover/load game assets, allocate resources, rebuild, alter collisions, cancel committed work or apply anything. Body/ecotype/variant destinations are identities; assets are recipe data. Cross-body presets preserve destination targets and leave unmatched named mappings unresolved. Common bulk recipes may be explicitly expanded against a new target on Apply; do not silently map a missing “Tree” to “Rocks.”

One typed live item per celestial contains applied ecotypes and nested slot inventory. Inspector editing uses a detached pending recipe and explicit Update. Include Copy to form, Restore selected group, Restore body, current generation/status and resource usage. Hidden features continue servicing their live records. Workspaces do not serialize native object graphs, GPU handles, collision cells or live records.

## 8. Concrete Apply/restore strategy

### Owned material isolation

Preferred design: each owned ecotype renderer gets an owned material buffer and local material-index mapping. Native `BuildRenderResources` captures its material buffer in descriptor binding 3 for color, depth and shadow pipelines, and captures material indexes while sorting primitives.

Build the owned table first. During the synchronous build of an owned ecotype, establish a strictly nested context keyed to the exact renderer and owned ecotype. Scoped prefixes on `GroundClutterRenderer.MaterialBuffer` getter and `GetMaterialIndex(GroundClutterMaterialReference)` return that local table. Outside the context, native behavior is unchanged. Always unwind context in `finally`; support nesting and reject mismatched resources. New material identities must also prevent unintended deduplication between independently editable slots.

This is a source-supported design, **not yet a proven hook implementation**. The first spike must check getter inlining, Harmony coverage, descriptor lifetime, exception cleanup and graphics-settings reconstruction. If getter patching is unreliable, use a narrowly validated caller patch or owned pipeline construction/binding adapter, retaining the same ownership design. Record the final choice before broad UI work.

Do not call native `BuildMaterialBuffer` repeatedly: it sizes a new buffer using only newly inserted dictionary entries but uploads every dictionary entry, and replaces its buffer field. A naive rebuild can underallocate, retain stale indexes, leak the previous buffer and leave descriptors bound to it. A full global-table rebuild is a possible fallback, but requires rebuilding/rebinding every consumer and coordinating all bodies; it is not the preferred per-body design.

### Transaction sequence

1. **Capture intent.** Apply copies the detached draft into a pending request. Resolve exact targets/assets and compare the content structural signature. Block missing/ambiguous mappings. Show the scope and effective collision policy.
2. **Prepare graph and budget.** Build private object/LOD/material references, valid material indirection and owned texture/mesh leases. Preserve slot order. Validate shaders, textures, five LODs, positive scales, bounds, finite curve data and allocation budgets.
3. **Prepare graphics resources off the active graph.** At an approved graphics phase, build placement resources, owned material table and render atlas/pipelines. Keep old resources and live bookkeeping intact. Capture/dispose partial-construction allocations if a constructor throws. Do not construct physical data here while solvers may be running: its constructor allocates shared Bepu shapes even before insertion into an active array.
4. **Reach the solver-safe preparation/commit window.** Candidate is a prefix on `Universe.ExecuteNextClothSolvers(double, SimStep)`, because normal `Program.PrepareFrame` has waited and applied orbit/vehicle/cloth results before this call, and starts cloth before vehicle jobs. Verify all call sites and idle assertions in the implementation spike. Construct matching physical data under the verified shared-shape lock and CPU-idle conditions before detaching active consumers; failure retains the old live graph. `ConstraintSim.UnlockShapes` is nonblocking and throws during vehicle stepping; it does not itself protect cloth readers. This applies even to None mode and partial-construction cleanup. Coordinate any physical GPU allocation with the graphics requirements too. A vehicle-solver prefix alone is too late for cloth snapshots.
5. **Quiesce old consumers.** Establish GPU completion as well as CPU solver completion; process pending destruction/exclusion events against their original generation. Remove affected body's old bubble statics before freeing their shapes. Do not globally clear other bodies. Generation stamps prevent delayed readbacks from installing outgoing data.
6. **Commit complete state.** Swap coherent body/ecotype reference view, `PlanetPlacementData`, `PlanetEcotypeRenderData`, `PlanetPhysicalData` and cached maximum bounds. Regenerate required grids; resume native synchronization. Preserve exclusions when identity topology is compatible. Do not leave a render atlas paired with physical data referring to a disposed predecessor.
7. **Finish and retire.** Mark the new live record applied only after commit. Release old GPU resources after completion and old physics shapes after all readers/statics are detached. During failed preparation retain the previous live override. A post-commit failure requires explicit rollback or faulted state, never a false “restored” success.

The exact body reference routing is part of the spike: use a private cloned `GroundClutterReference` for the selected body while retaining the captured original; do not mutate registered object/material assets or broad body terrain data. Inventory any template sharing at runtime. If two live celestials share their body template object, isolate the body's reference access rather than replacing a field on their shared template.

For a first correct implementation, use paired reconstruction for mesh/scale/structural edits; optimize material-only and placement-only changes after correctness is demonstrated. Applying should be queued and report Preparing/Waiting/Applied/Failed. A temporary absence of regenerated cells is permissible and should be measurable; rendering or colliding against mismatched old data is not.

### Exclusions, restore and lifecycle

- Same grid topology and stable slots: transfer destruction masks and process later exclusions against the active generation.
- Changed spacing/grid or reordered slots: old `(cell, subcell, index)` identities cannot simply be reused. Preserve an original-topology baseline and separate generation state; show that the new distribution regenerates clutter. Do not claim exact cross-topology destruction migration. Geometric remapping is a separate optional feature.
- Restore: reconstruct from the captured baseline/settings while honoring compatible accumulated destruction. It restores Pebbles' configuration, not vehicle motion, impacts or all historical clutter instances.
- Renderer/settings recreation: recognize old-generation disposal, retain the applied recipe as session intent and rebuild it for the same exact body when resources become available. Saving/loading a draft never triggers this. When global clutter is off, live status is suspended by game settings, not removed.
- Universe replacement: never attach old handles to a new body with the same name. End/reconcile records against the new universe generation using an explicit lifecycle policy; default to ending old session effects and keeping the authoring draft.
- Unload: restore owned state while hooks/resources are still available, wait for safe retirement, then remove targeted patches. Hooks also need a pre-disposal notification so native renderer teardown and feature cleanup cannot both dispose the same resources.
- External or game-debug changes to the same runtime graph: detect lost ownership/reference changes and report a conflict. Do not overwrite another owner's newer state on restore.

## 9. Additional biome-local appearance routing

Native biome controls are probabilistic placement filters. At a blended location, an ecotype may have 60% allowed-biome weight and another 40%. Both clones use the same deterministic random value; independent `r < .6` / `r < .4` acceptance yields both meshes for `r < .4` and neither for `r > .6`. Splitting masks is not a complementary assignment.

If finer biome-specific mesh/material changes are wanted, implement a routing stage that first preserves the original ecotype's acceptance decision, then assigns **each accepted instance exactly once** to a destination appearance based on a complementary weighted partition of the contributing biome weights. Use the same routing decision for rendering and physical object selection. Specify tie/zero-weight behavior, preserve stable base instance identity and maintain an explicit destination-slot map.

Prefer routing inside the original logical ecotype over duplicating its placement grids. This needs feature-owned shader variants and a mapping buffer, plus a design for the 51-variant/packed-index limits, collider lookup and exclusions. For original-collider policy, keep base physical identity separate from visual variant identity. For replacement-collider policy, route physics to the corresponding owned collider variant. If the initial implementation cannot fit that mapping without changing layouts, review the extended layout before proceeding.

Acceptance must verify single-biome interiors, weighted transitions, no double instances/holes, stable identity through regeneration, matching render/collision routing and no effects on unselected bodies. This is a separately reviewable milestone; initial ecotype-wide controls must state their true scope.

## 10. Implementation sequence after approval

| Phase | Deliverable | Exit condition |
|---|---|---|
| 1. Integration proof | Small internal, explicitly applied one-body override with isolated materials, paired replacement/restoration; separate private mesh-preview proof for Workshop. | Luna changes while Mars stays stock; repeat apply/restore; solver/GPU lifetime verified; preview does not disturb camera/editor/thumbnails. Decide final hooks. |
| 2. Core Pebbles | Bundled feature, exact targets, discovery, variant/LOD/group overrides, five texture channels, source colors and collision policy; Workshop primitive auto-fit, gizmos and undo/redo. | Multi-mesh tree, multi-primitive silly mesh and all-same action work; preview and custom colliders agree; failures preserve previous live state. |
| 3. Full native controls | Placement masks, textures, spacing/range, scales, orientation, spline, variant texture, native material flags, bounds and budget diagnostics. Workspace/presets/live inspectors completed. | Every enabled control has visible tested effect; paired regeneration coherent; full save/load/hide invariants. |
| 4. Advanced structure and physics | Optional variant restructuring, convex-hull generation/advanced fitting, local mass/break controls, isolated test simulation, no-stock-clutter body creation where terrain prerequisites exist. | Identity/exclusion migration policy implemented; bounded resource usage and physics verified. |
| 5. Shader extensions | Biome-local appearance routing, then optional tint/UV/PBR/emissive/cutoff/seed controls chosen in review. | Explicit GPU layouts and pass parity; no unsupported sliders shipped as native functionality. |

Phases 1–3 are the recommended initial scope. Phase 4 and 5 are planned extensions, not silently included assumptions. A proof failure changes the technical plan before the larger UI implementation; it should not result in a partially working feature advertised as complete.

Implementation documentation changes must include `pebbles.lib/README.md`, repository README/index, workspace guide, host catalog/lifecycle references, solution/build distribution, boundary checks that enumerate features, and the count change from 25 to 26. If generic discovery moves, update rings/abstractions READMEs too. Add `scope/ground-clutter.md`, update `scope/game-integration-surface.md` and FULL_SCOPE ToC/status in the same implementation change. Inventory every chosen Harmony target, private lookup, render layout, shader/asset dependency and lifecycle interface. This proposal does not add Pebbles to the active integration inventory.

## 11. Verification and acceptance

Managed checks should cover recipe validation, five-LOD/material assignment structure, exact/ambiguous identity resolution, preset target preservation, explicit bulk expansion, finite/budget checks, all/none biome encoding, detached snapshots, failed-commit bookkeeping and generation/exclusion routing models. Tests must assert independent expected behavior; do not duplicate the implementation. Native KSA calls do not belong in the data-only contract test executable.

Required implementation checks:

```sh
dotnet build ksa-mod-experiments.slnx --disable-build-servers -m:1 -p:UNSCIENCE_DIST_DIR=/tmp/unscience-dist
dotnet run --project unscience-contracts.tests --no-build
python3 scripts/check-workspace-boundaries.py
python3 scripts/check-docs.py
git diff --check
```

Native acceptance, required before claiming the feature works:

1. Change Luna rocks; Mars, other Luna ecotypes and all unrelated materials remain unchanged. Reverse the test and apply independent overrides to both bodies.
2. Replace all selected slots with one mesh, then just one variant, one LOD and one mesh-group component. Test a tree with trunk/leaves and a multi-primitive imported mesh; preserve material assignments and correct bounds.
3. Exercise all five LOD transitions near/far, camera altitude/FOV, sun shadows, cutout depth with MSAA on/off, source-color textures, terrain blending, normal/PBR and transmission maps. Validate secondary viewports explicitly; do not inherit a claim of multi-camera support from the two camera/shadow view categories.
4. Compare fixed/random scale and orientation, biome filters at transitions, slope/altitude curves, distribution/type textures, range and spacing. Check current cells and newly streamed cells agree, including cube-face boundaries and terrain resnapping.
5. Test collisions off/on, original and replacement policies, landed/moving vessels, multiple bubbles/bodies, deployed parachutes, destruction and exclusions. Include tiny visual geometry with large retained colliders and colliders with large local offsets. Reapply while readbacks/jobs are pending; no stale collider installs, use-after-free or resurrection through ordinary appearance restore.
6. Stress repeated Apply/restore, mid-preparation failures, resource allocation failures, settings/renderer rebuilds, global clutter toggles, body switching, universe replacement and unload. GPU/bindless/shape allocations settle back to baseline.
7. Save/load/presets/hide while a live override exists; effects continue unchanged. Test missing assets/targets and changed content signatures. Narrow/wide forms, keyboard input, hidden HUD, copy-to-form and complete body-recipe replacement behave as documented.
8. Measure worst-case grass density with a heavy replacement, all LODs filled, shadows and collision mode. Establish budget defaults from measurements, not a guessed safe object count.

Investigation verification: the existing solution build passed with zero warnings/errors using `/tmp/unscience-dist`; existing contract tests, workspace-boundary checks, documentation checks and `git diff --check` passed during the investigation. Plan links were checked separately. These are baseline checks only, not verification of unrelated concurrent working-tree edits. No Pebbles runtime, native UI, shader or physics test was run, and no game installation was modified by the isolated build.

## 12. Decisions proposed for review

Recommended initial approval would cover Phases 1–3: per-body/ecotype/variant control, all five LODs, multi-mesh/material replacements, native placement/material settings and explicit coherent collision behavior. Keep the “everything is the same silly object” workflow prominent.

Following the mini-editor request, this includes Workshop mesh preview and user-authored analytic collision primitives with direct translation, rotation and dimension handles. The Workshop proposal supersedes the earlier deferral of all collider authoring to advanced physics work; convex-hull generation and an isolated simulation sandbox remain optional later additions.

The principal scope choices to refine are whether biome-local appearance routing belongs in the first release, whether arbitrary visual replacements should default to no colliders for replaced variants or original colliders, and which shader-only controls are worth the extra integration surface. This document recommends later biome routing, a clearly disclosed no-collider policy for arbitrary replacements, and native controls before new shading features.

No implementation has begun. Approval of this plan can be limited to named phases, or the plan can be revised first.
