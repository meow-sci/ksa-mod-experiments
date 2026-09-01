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
