# Planetary rings — rocky-mcrock-face

Scope for **rocky-mcrock-face** (`rocky-mcrock-face.lib`, bundled in the unscience supermod), which
swaps the meshes and textures of KSA's planetary ring system (Saturn's rock field + 2D band) at
runtime. Written against KSA build **2026.8.22.5348**.

## How it hooks the game

**No Harmony patches.** The mod is a pure data-level swap: KSA's entire ring definition is public
XML-backed data (`AstronomicalTemplate.RingsReference` → `PlanetaryRingsReference` →
`RingObjectsReference` → `RingLodReference.MeshFileReference.Mesh`), and
`PlanetaryRingsRenderData` re-reads that tree from scratch every time the renderer is (re)built —
baking per-LOD index counts, the max bounding-sphere radius, and the material's bindless handles
into its UBO in the constructor. So the mod mutates the public reference tree (snapshotting
originals for restore) and then calls the public `Program.Instance.RebuildRenderer()` — the exact
path the game's own graphics settings use — to rebuild the ring render data with proper GPU sync.

The one genuinely non-public thing it does is **mesh conversion**: the ring pipeline draws
`MeshReference.DeviceMesh` (`SimpleVkMesh`, one vertex stream per attribute), which the game builds
only for `Simple` meshes. Part/subpart meshes are atlas-loaded `Interleaved` (DeviceMesh == null),
and flipping their flags in place would break IVA raytracing
(`KSA.Rendering.Raytracing/RaytracingRenderer.cs:1027` throws on a non-interleaved subpart). The
mod clones such meshes into a private `Simple` `MeshReference` that shares the retained CPU-side
`HostPrimitives` array (written via the auto-property backing field) and binds a `SimpleVkMesh` for
primitive 0.

## Update-risk narrative

1. **The ctor-baking contract is the design keystone.** The whole approach relies on
   `PlanetaryRingsRenderData`'s constructor reading `Lods[i].MeshFileReference.Get().Mesh`,
   `MaterialReference.{DiffuseReference,NormalReference,PBRMap}.Get().BindlessHandle`,
   `reference.Texture.Get().ImageView`, and Size/Density/RenderDistance/Thickness at build time
   (`KSA.Rendering.Rings.Rendering/PlanetaryRingsRenderData.cs:180-326` @5348). If a future build
   caches ring data elsewhere or stops rebuilding it in `RebuildFrameResources` →
   `PopulatePlanets`, Apply would silently stop having an effect (no crash).
2. **Only primitive 0 is drawn** (`RenderMeshes` uses `MeshLods[i].DeviceMesh`, i.e.
   `DevicePrimitives[0]` — `PlanetaryRingsRenderer.cs:597-603`). If the ring renderer ever starts
   drawing all primitives, converted clones (built with `PrimitiveCount = 1`) would need to bind
   every primitive.
3. **Vertex layout compatibility is an invariant, not a check.** The ring pipeline's
   `MakeMeshesVertexInput` is instance-uint + Position/Normal/Uv0 streams
   (`PlanetaryRingsRenderer.cs:880`), and `MeshReference.Load` imports exactly
   `Normals | UVs` with missing attributes default-filled (`MeshReference.cs:76-115`;
   `RenderCore.Gltf/GltfUtils.cs` fills absent streams). Any game change to either side breaks
   converted meshes with garbage rendering, not a compile error.
4. **RebuildRenderer alone does NOT rebuild ring data — the rings renderer must be disposed
   first.** `PlanetTransparenciesRenderer.RebuildFrameResources` (`:325-343`) only calls
   `_ringsRenderer.RebuildFrameResources(...)` when `_ringRendererCreated` is true — that
   destroys/rebuilds pipelines and frame images but never re-runs `PopulatePlanets` (ctor-only,
   `PlanetaryRingsRenderer.cs:170`), so `PlanetaryRingsRenderData` (meshes, UBO, instances)
   survives untouched. Only the `else if (_anyRings) CreateRingsRenderer(...)` branch (`:334-337`)
   constructs a fresh renderer and re-reads the reference tree. The mod therefore: waits for the
   device (`Renderer.Device.WaitIdle()` — in-flight frames may reference ring GPU resources),
   calls the public `PlanetaryRingsRenderer.Dispose()` on the reflected instance, clears the
   private `_ringRendererCreated` flag, THEN calls `Program.RebuildRenderer(bool = false)`
   (`KSA/Program.cs:4742`, which also WaitIdles at `:4749`) so the game's own create branch
   rebuilds everything — including instance buffers resized for density/render-distance changes
   (`PopulatePlanets` runs before `CreateMeshRenderingResources` in the ctor). Called from the
   ImGui phase — the same phase the game's settings Apply uses. If this branching ever changes
   (e.g. `RebuildFrameResources` starts re-populating data itself), the dispose becomes redundant
   but harmless.
5. **The ControlTexture is deliberately NOT swappable.** `PlanetaryRingsRenderData.UpdateData`
   CPU-samples it every frame assuming ≥4 bytes/texel uncompressed RGBA
   (`SampleRingTexture`, `PlanetaryRingsRenderData.cs:100-118`); a compressed ktx2 there means
   garbage indexing. The band `Texture` is GPU-sampled only, so it is safe to swap.
6. **Restore-before-dispose ordering.** Converted clone meshes carry a finalizer that frees their
   GPU buffers (`MeshReference.~MeshReference`). The controller restores defaults and rebuilds the
   renderer **before** disposing clones, and caches clones for its lifetime so a GC can never free
   a mesh the renderer still references.

## Touchpoints

Decomp paths relative to `~/repos/meow-sci/ksa-game-assemblies/current/decomp`.

| # | Game member | Kind | Decomp path | Mod code ref | 5348 |
|---|---|---|---|---|---|
| 1 | `AstronomicalTemplate.RingsReference : PlanetaryRingsReference?` (public field, via `Celestial.BodyTemplate`) | direct API | `KSA/AstronomicalTemplate.cs:66`; `KSA/Celestial.cs:83` | `RingSwapController.RefreshBodies` | OK |
| 2 | `PlanetaryRingsReference.{Texture, ControlTexture : TextureReference, RingObjects : RingObjectsReference}` (public fields) | direct API | `KSA/PlanetaryRingsReference.cs:23,26,35` | `RingSwapController.{Apply,Restore,TakeSnapshot}` | OK |
| 3 | `RingObjectsReference.{Lods : List<RingLodReference>, MaterialReference : PbrMaterialReference, Size/Thickness/RenderDistance : DistanceReference, Density : DoubleReference, NumLods}` | direct API | `KSA/RingObjectsReference.cs` | `RingSwapController` | OK |
| 4 | `RingLodReference.{MinScreenSizePixels : float, MeshFileReference : MeshFileReference?}` | direct API | `KSA/RingLodReference.cs:8,11` | `RingSwapController`, submod UI (LOD labels) | OK |
| 5 | `MeshFileReference.{Get() : MeshFileReference, Mesh : MeshReference?}` — **`Mesh` is the swap slot** | direct API | `KSA/MeshFileReference.cs:15,28` | `RingSwapController.{Apply,Restore}` | OK |
| 6 | `PbrMaterialReference.{DiffuseReference : TextureReference?, NormalReference : TexturePowerReference?, PBRMap : TextureReference?}` (public fields) | direct API | `KSA/PbrMaterialReference.cs:10-17` | `RingSwapController` | OK |
| 7 | `MeshReference` — public `Id/Simple/Interleaved/PrimitiveCount/BoundingSphereRadius` fields, `HostPrimitives`/`DevicePrimitives` get-only props, `DeviceMesh => DevicePrimitives[0]`, `Bind(Renderer, StagingPool)`, `Dispose()` | direct API | `KSA/MeshReference.cs:17-58,120,145` | `RingMeshFactory`, `RingAssetCatalog` | OK — **multi-primitive shape is new @5348** (`DevicePrimitives[]` replaced the old single `DeviceMesh` field) |
| 8 | `MeshReference.<HostPrimitives>k__BackingField : MeshAsset[]` (auto-prop backing field) | **reflection (string)** | `KSA/MeshReference.cs:40` | `RingMeshFactory` static field lookup — null-checked; Apply fails with a UI error, never crashes | OK |
| 9 | `ModLibrary.AllMeshes : SerializedCollection<MeshReference>` / `ModLibrary.AllFiles : SerializedCollection<FileReference>` / `ModLibrary.AllGltfs : SerializedCollection<Gltf2Reference>` (internal static fields) | **reflection (string)** | `KSA/ModLibrary.cs:80,68,76` | `RingAssetCatalog.Collection<T>` — same pattern/names as parts-now `GameRegistry` (AllMeshes/AllFiles already on the watchlist) | OK |
| 9b | `Gltf2Reference.{Id, Source : FileReference?}` + `FileReference.ModPath` · `GltfUtility.LoadModel(string) : Gltf` + `Gltf.Meshes[].Name` (JSON-only parse for the catalog) · `GltfLoader(string)` ctor + `MeshReference.Load(GltfLoader, int mesh, createDeviceMesh: false)` (conversion — the exact import `MeshFileReference.DoLoad` runs for the stock ring rocks; skinned meshes import in bind pose) | direct API | `KSA/Gltf2Reference.cs:10`; `KSA/FileReference.cs:23`; `Brutal.GltfApi/GltfUtility.cs:38`, `GltfLoader.cs:23`; `KSA/MeshReference.cs:76` | `RingAssetCatalog.RefreshGltfMeshes`, `RingMeshFactory.GetRingUsableFromGltf` — makes character/MMU/helmet meshes (the glTF-file pipeline, never in `AllMeshes`) selectable | OK |
| 9c | `DeviceMeshInterleaved.IndexCount` (public field) · `SimpleVkMesh.IndexCount` | direct API | `KSA/DeviceMeshInterleaved.cs:117`; `RenderCore.Mesh/SimpleVkMesh.cs:26` | `RingAssetCatalog.GetMeshIndexCount`, `RingMeshFactory.GetConvertedIndexCount` — UI triangle-cost readout only | OK |
| 10 | `SerializedCollection<T>.GetList() : List<T>` (live list — copied before iteration) | direct API | `KSA/SerializedCollection.cs:41` | `RingAssetCatalog.Refresh` | OK |
| 11 | `TextureReference.{Id, BindlessHandle : int}` + `TexturePowerReference : TextureReference` | direct API | `KSA/TextureReference.cs:70`; `KSA/TexturePowerReference.cs` | `RingAssetCatalog` (handle==0 ⇒ excluded), `RingSwapController` | OK |
| 12 | `Program.{Instance : static Program, GetRenderer() : static Renderer, RebuildRenderer(bool = false)}` | direct API | `KSA/Program.cs:434,535,4742` | `RingSwapController.RebuildRenderer`, `RingMeshFactory` | OK |
| 13 | `Program._planetTransparenciesRenderer` → `PlanetTransparenciesRenderer.{_ringsRenderer, _ringRendererCreated}` (private fields) + public `PlanetaryRingsRenderer.Dispose()` (typed) + `Renderer.Device.WaitIdle()` | **reflection (string)** + direct API | `KSA/Program.cs:157`; `KSA/PlanetTransparenciesRenderer.cs:40,46,354-361`; `PlanetaryRingsRenderer.cs:473`; `Brutal.VulkanApi.Abstractions/DeviceExtensions.cs` | `RingSwapController.{IsRingsRendererCreated, DisposeRingsRendererForRecreation}` — the dispose-for-recreation step that makes the rebuild actually re-read ring data (narrative #4). A field rename degrades to a frame-resources-only rebuild: Apply hitches but changes nothing — **the original symptom**, so a silent break here is user-visible immediately | OK |
| 14 | `Renderer.Allocator.CreateStagingPool(renderer.GraphicsAndCompute, 1)` + `StagingPool` dispose = submit+wait; `SimpleVkMesh` built by `MeshReference.Bind` | direct API (render) | `Brutal.VulkanApi.Abstractions/StagingPoolExtensions.cs:5`, `StagingPool.cs:167`; `RenderCore.Mesh/SimpleVkMesh.cs:69` | `RingMeshFactory.GetRingUsable` | OK |
| 15 | `GameSettings.{ShowRings(), ShowRingMeshes()} : static bool` | direct API | `KSA/GameSettings.cs:3122,3133` | submod UI status hints | OK |
| 16 | `Universe.CurrentSystem.All.OfType<Celestial>()` | direct API | `KSA/Universe.cs:94`; `KSA/CelestialSystem.cs` | `RingSwapController.RefreshBodies` | OK |
| 17 | Consumer contract (not called, relied upon): `PlanetaryRingsRenderData` ctor bakes `LodProperties[i].Y = MeshLods[i].DeviceMesh.IndexCount`, `MeshCullingRadius = max BoundingSphereRadius`, `MeshDiffuseId/NormalId/PbrId = …BindlessHandle`; `PlanetaryRingsRenderer.{PopulatePlanets, RenderMeshes}` | behavioral invariant | `KSA.Rendering.Rings.Rendering/PlanetaryRingsRenderData.cs:180-326`; `PlanetaryRingsRenderer.cs:324,571-603` | design keystone — see narrative #1-#3 | OK |

Related but **not** integration points: `RingSelection` (mod-local, session-only state — overrides
are deliberately not persisted; a game restart is back to stock).

---

# Runtime ring definition — bloomin-onion

Scope for **bloomin-onion** (`bloomin-onion.lib`, bundled in the unscience supermod), which builds
brand-new `PlanetaryRingsReference` trees at runtime and puts them on any celestial. Written against
KSA build **2026.8.22.5348**. Shares rocky-mcrock-face's catalog + mesh conversion (touchpoints #7-#11
above apply unchanged; bloomin-onion's own rows are numbered B1+ below).

## How it hooks the game

**No Harmony patches.** Same data-level approach as rocky, one level up: instead of mutating an
existing tree it *constructs* one — every reference class the XML loader would produce
(`PlanetaryRingsReference`, `PlanetaryRingsVolumeReference`, `RingRaymarchingStepReference`,
`RingObjectsReference`, `RingLodReference`, `MeshFileReference`, `PbrMaterialReference`, the
`Distance/Radian/Double/BoolReference` value wrappers) — and assigns it to
`celestial.BodyTemplate.RingsReference` (public field; original snapshotted per template).

Because a body can *gain or lose* rings, the rebuild has one extra step over rocky's: the
transparencies renderer's body list (`_planetsWithTransparencies` + `_bodiesSortedBackToFront`,
rebuilt by its **public** `PopulatePlanets()`) and the private `_anyRings` flag that gates
`CreateRingsRenderer` in `RebuildFrameResources` are refreshed before `Program.RebuildRenderer()`.

Painted bands are runtime textures: a `TextureReference` subclass whose private-set `TextureAsset`
is seeded from a `GenericTexture` (`Brutal.TextureApi.Abstractions`) and then bound through the
game's own virtual `Bind` — so the ring renderer's `Texture.Get().ImageView` / `BindlessHandle` /
`ControlTexture.Get().TextureAsset.Texture.Data` reads all work unchanged.

## Update-risk narrative

1. **Reference-tree construction contract.** The builder must produce exactly what
   `PlanetaryRingsRenderData`'s ctor + `UpdateData` dereference: `Texture/ControlTexture.Get()`,
   `Volume.{MinThickness,MaxThickness,MinRenderDistance,MaxRenderDistance,Step.{Scale,MinSize,MaxSize},FadeToMeshes}`,
   `RingObjects.{Lods[i].MeshFileReference.Get().Mesh(.DeviceMesh/.BoundingSphereRadius),
   MaterialReference.{DiffuseReference,NormalReference,PBRMap}.Get().BindlessHandle,
   Size,Thickness,RenderDistance,Density,NumLods}`, `DetailScale`, `Inner/OuterRadius`,
   `Inclination`, `LongitudeOfAscendingNode`, `DefinitionFrame`
   (`PlanetaryRingsRenderData.cs:65-86,180-326` @5348). A new required field on any of these
   classes (left at its C# default) either NREs at rebuild (caught, reported, reference reverted)
   or renders wrong. `rings.IsValid()` is **not** usable as a gate: `DistanceReference.IsValid()`
   (`KSA/DistanceReference.cs:162`) demands `|value| > 100 km`, which the stock rock size / field
   thickness / draw distance / step sizes fail — the game never enforces it on rings.
2. **`Get()` self-resolution.** Freshly constructed `MeshFileReference` / `TextureReference` /
   `PbrMaterialReference` return `this` from `Get()` because their private `_isReference` defaults
   to false (only `OnDataLoad` can flip it). If a future build makes `Get()` consult `ModLibrary`
   unconditionally, every built reference would throw at rebuild.
3. **Angle normalization mirrors `PlanetaryRingsReference.OnDataLoad`** (`MathEx.ToDeviationAngle`
   / `ToCompassAngle`, `KSA/PlanetaryRingsReference.cs:53-60`). Drift here is cosmetic (plane
   orientation), not a crash.
4. **Ecliptic frame dereferences `celestial.Parent.GetCce2Cci()`** in both
   `PlanetaryRingsRenderData` ctor and `PlanetaryRingsRenderer.ComputeRingNormal`; the builder
   refuses the ecliptic frame for a body without a parent.
5. **The control strip is CPU-sampled** (`SampleRingTexture`, 4 bytes/texel assumption) — painted
   control strips are RGBA8 by construction, and picked ones are filtered through
   `FormatDescriptor.{IsBlockCompressed, BlockSizeInBytes == 4}`.
6. **`_anyRings` is the on/off switch for the whole rings renderer.** If a build renames it, the
   `SetFieldValue` is a silent no-op: a system with no stock rings would never get a rings renderer
   (Apply "succeeds" but nothing renders) — immediately user-visible, never a crash. In a system with
   stock rings (Saturn) everything still works because `_anyRings` is already true.
7. **Painted textures' GPU lifetime** is tied to the renderer: freed only after a rebuild that no
   longer references them (`RingTextureFactory.PruneExcept`, controller `Dispose` order:
   restore → rebuild → dispose textures/clones). `GenericTexture` has no `Dispose`; its native
   8 KB buffer per strip is left to the GC finalizer (if any) — negligible.
8. **Distant-sphere ring shadow** (`DistantSphereRenderer._data.UseRingShadows/...`) is baked at
   `StaticCelestial` construction; the mod refreshes those struct fields by reflection, best-effort
   and fully guarded. Failure = the far-away sprite lacks/keeps a shadow band; nothing else.

## Touchpoints (bloomin-onion)

| # | Game member | Kind | Decomp path | Mod code ref | 5348 |
|---|---|---|---|---|---|
| B1 | `PlanetaryRingsReference` (all public fields: `DefinitionFrame, Inclination, LongitudeOfAscendingNode, InnerRadius, OuterRadius, Texture, ControlTexture, DetailScale, Volume, RingObjects`; `IsValid()` deliberately unused — narrative #1) · `PlanetaryRingsVolumeReference.{MinThickness, MaxThickness, MinRenderDistance, MaxRenderDistance, Step, FadeToMeshes}` · `RingRaymarchingStepReference.{Scale, MinSize, MaxSize}` · `RingObjectsReference.{Name, Thickness, Size, RenderDistance, Density, Lods, MaterialReference}` · `RingLodReference.{MinScreenSizePixels, MeshFileReference}` · `MeshFileReference.Mesh` · `PbrMaterialReference.{DiffuseReference, NormalReference, PBRMap}` — **constructed**, not just mutated | direct API | `KSA/PlanetaryRingsReference.cs`; `KSA/PlanetaryRingsVolumeReference.cs`; `KSA/RingRaymarchingStepReference.cs`; `KSA/RingObjectsReference.cs`; `KSA/RingLodReference.cs`; `KSA/MeshFileReference.cs:15`; `KSA/PbrMaterialReference.cs:10-17` | `RingReferenceBuilder.Build`, `RingDefinitionSerializer.FromReference` | OK |
| B2 | `DistanceReference(double, DistanceUnit)` / `(double meters)` · `RadianReference(double radians)` + `.ToDegrees()` · `DoubleReference.FromValue` · `BoolReference(bool)` · `DistanceReference.{InMeters(), InKilometers()}` · `MathEx.{ToDeviationAngle, ToCompassAngle}(double)` · `OrbitDefinitionFrame` | direct API | `KSA/DistanceReference.cs:105-140`; `KSA/RadianReference.cs:23,66`; `KSA/DoubleReference.cs:44`; `KSA/BoolReference.cs:14`; `KSA/MathEx.cs:178,189`; `KSA/OrbitDefinitionFrame.cs` | `RingReferenceBuilder`, `RingDefinitionSerializer` | OK |
| B3 | `AstronomicalTemplate.RingsReference` (public field, **written**) via `Celestial.BodyTemplate : CelestialTemplate` · `Celestial.{Id, MeanRadius, Parent}` | direct API | `KSA/AstronomicalTemplate.cs:66`; `KSA/Celestial.cs:73,83,91` | `RingDefinitionController.{Apply, Remove, RestoreTemplate, HasStockRings}`, `RingReferenceBuilder.Validate` | OK |
| B4 | `PlanetTransparenciesRenderer.PopulatePlanets() : bool` (public) | direct API | `KSA/PlanetTransparenciesRenderer.cs:169` | `RingRendererRebuilder.Rebuild` — refreshes `HasRings` per body + `_bodiesSortedBackToFront` sizing | OK |
| B5 | `Program._planetTransparenciesRenderer` → `PlanetTransparenciesRenderer.{_ringsRenderer, _ringRendererCreated, _anyRings}` (private fields) + public `PlanetaryRingsRenderer.Dispose()` + `Device.WaitIdle()` + `Program.RebuildRenderer()` | **reflection (string)** + direct API | `KSA/Program.cs:157,4742`; `KSA/PlanetTransparenciesRenderer.cs:40,46,68,325-343` | `RingRendererRebuilder.{Rebuild, DisposeRingsRenderer, IsRingsRendererCreated}` — `_anyRings` is the only field new vs rocky (narrative #6) | OK |
| B6 | `TextureReference` subclassing: public `Category, Width, Height, Manifest, BindlessHandle, Bind(Renderer, StagingPool)` (virtual), `Dispose(Device)`, `SetHash()` · `TextureReference.<TextureAsset>k__BackingField` (private-set auto-prop) | direct API + **reflection (string)** | `KSA/TextureReference.cs:36-77,133-166`; `KSA/SerializedId.cs:52` | `PaintedTextureReference.Create/Release` — null-checked; a miss disables Painted mode in the UI (`IsSupported`) with a clear message | OK |
| B7 | `RenderCore.TextureAsset(ITexture, string)` ctor · `Brutal.TextureApi.Abstractions.GenericTexture.Defaults.RGBA8UNorm(int2)` + `.Data` · `TextureFormatExtensions.Descriptor()` → `FormatDescriptor.{IsBlockCompressed, BlockSizeInBytes}` · `TextureAsset.Texture.Format` | direct API | `RenderCore/TextureAsset.cs:21`; `Brutal.TextureApi.Abstractions/GenericTexture.cs:80,122`; `FormatDescriptor.cs` | `PaintedTextureReference.Create`, `RingReferenceBuilder.IsCpuSampleable` | OK |
| B8 | `StaticCelestial._distantRenderer` → `DistantSphereRenderer._data` (private struct `DistantSphereData` public fields `UseRingShadows, RingInnerRadius, RingOuterRadius, RingTextureId, SamplerClampId`) · `Program.TextureSystem.SamplerClampHandle` | **reflection (string)**, cosmetic | `KSA/StaticCelestial.cs:8`; `KSA/DistantSphereRenderer.cs:24,57-66`; `KSA/GpuTextureSystem.cs:24` | `RingRendererRebuilder.SyncDistantSphereShadow` — every lookup null-tolerant, wrapped in try/catch | OK |
| B9 | `GameSettings.{ShowRings(), ShowRingMeshes()}` · `CelestialProvider.GetAllCelestials()` (ksa-abstractions → `Universe.CurrentSystem.All`) | direct API | `KSA/GameSettings.cs:3122,3133`; `KSA/Universe.cs:94` | submod UI / `BloominOnionSubmod.RefreshBodies` | OK |
| B10 | Consumer contract (relied upon): `PlanetTransparenciesRenderer.RebuildFrameResources` takes `CreateRingsRenderer` only when `!_ringRendererCreated && _anyRings`; `PlanetaryRingsRenderer.PopulatePlanets` iterates `Universe.CurrentSystem.All.OfType<Celestial>()` reading `BodyTemplate.RingsReference` at ctor; `PlanetRenderer` reads `RingsReference` per frame for the ring shadow (`PlanetRenderer.cs:1985-1993`); `AtmosphereRenderer.AssignPlanetSlots` keys on `AtmosphericBody` only (so a ring-only body joining `_planetsWithTransparencies` is harmless) | behavioral invariant | `KSA/PlanetTransparenciesRenderer.cs:325-343`; `PlanetaryRingsRenderer.cs:324-346`; `KSA/AtmosphereRenderer.cs:305-317` | design keystone — narrative #1, #6 | OK |

Related but **not** integration points: `RingDefinition` / `RingPresetStore` (mod-local model +
TOML under `.unscience/bloomin-onion-rings.toml`; body assignments deliberately session-only).
