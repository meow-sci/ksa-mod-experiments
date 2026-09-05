# Planetary rings — rocky-mcrock-face

## Workspace integration (current)

Active bundled features: **rocky-mcrock-face, bloomin-onion**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

RingAssetCatalog, RingMeshFactory and RockyUi moved into `ksa-rings.lib` (`MeowSci.KsaRings`). Bloom has no reference to rocky-mcrock-face.lib. `RingOwnership.BeforeReplace(Celestial)` coordinates the two: Bloom announces replacement before changing PlanetaryRings, Rocky restores/releases its overlay on the outgoing reference, then Bloom rebuilds. Rocky has independent draft and applied-per-body RingSelection data; Bloom has detached RingDefinition plus AppliedRing entries. Loading a draft does not rebuild the renderer, dispose mesh clones or restore a ring. The existing reflection/shader/asset watchlist below still applies.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Scope for **rocky-mcrock-face** (`rocky-mcrock-face.lib`, bundled in the unscience supermod), which
swaps the meshes and textures of KSA's planetary ring system (Saturn's rock field + 2D band) at
runtime. Written against KSA build **2026.8.22.5348**; re-verified against **2026.9.7.5402**.

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
(`KSA.Rendering.Raytracing/RaytracingRenderer.cs` throws on a non-interleaved subpart). The
mod clones such meshes into a private `Simple` `MeshReference` that shares the retained CPU-side
`HostPrimitives` array (written via the auto-property backing field) and binds a `SimpleVkMesh` for
primitive 0.

## Touchpoints

Decomp paths relative to `~/repos/meow-sci/ksa-game-assemblies/current/decomp` (NEW = 5402).

| # | Game member | Kind | Decomp path | Mod code ref | 5402 |
|---|---|---|---|---|---|
| 1 | `AstronomicalTemplate.RingsReference : PlanetaryRingsReference?` (public field, via `Celestial.BodyTemplate`) | direct API | `KSA/AstronomicalTemplate.cs`; `KSA/Celestial.cs` | `RingSwapController.RefreshBodies` | OK |
| 2 | `PlanetaryRingsReference.{Texture, ControlTexture : TextureReference, RingObjects : RingObjectsReference}` (public fields) | direct API | `KSA/PlanetaryRingsReference.cs` | `RingSwapController.{Apply,Restore,TakeSnapshot}` | OK |
| 3 | `RingObjectsReference.{Lods : List<RingLodReference>, MaterialReference : PbrMaterialReference, Size/Thickness/RenderDistance : DistanceReference, Density : DoubleReference, NumLods}` | direct API | `KSA/RingObjectsReference.cs` | `RingSwapController` | OK |
| 4 | `RingLodReference.{MinScreenSizePixels : float, MeshFileReference : MeshFileReference?}` | direct API | `KSA/RingLodReference.cs` | `RingSwapController`, submod UI (LOD labels) | OK |
| 5 | `MeshFileReference.{Get() : MeshFileReference, Mesh : MeshReference?}` — **`Mesh` is the swap slot** | direct API | `KSA/MeshFileReference.cs` | `RingSwapController.{Apply,Restore}` | OK |
| 6 | `PbrMaterialReference.{DiffuseReference : TextureReference?, NormalReference : TexturePowerReference?, PBRMap : TextureReference?}` (public fields) | direct API | `KSA/PbrMaterialReference.cs` | `RingSwapController` | OK |
| 7 | `MeshReference` — public `Id/Simple/Interleaved/PrimitiveCount/BoundingSphereRadius` fields, `HostPrimitives`/`DevicePrimitives` get-only props, `DeviceMesh => DevicePrimitives[0]`, `Bind(Renderer, StagingPool)`, `Dispose()` | direct API | `KSA/MeshReference.cs` | `RingMeshFactory`, `RingAssetCatalog` | OK — **multi-primitive shape is new @5348** (`DevicePrimitives[]` replaced the old single `DeviceMesh` field) |
| 8 | `MeshReference.<HostPrimitives>k__BackingField : MeshAsset[]` (auto-prop backing field) | **reflection (string)** | `KSA/MeshReference.cs` | `RingMeshFactory` static field lookup — null-checked; Apply fails with a UI error, never crashes | OK |
| 9 | `ModLibrary.AllMeshes : SerializedCollection<MeshReference>` / `ModLibrary.AllFiles : SerializedCollection<FileReference>` / `ModLibrary.AllGltfs : SerializedCollection<Gltf2Reference>` (internal static fields) | **reflection (string)** | `KSA/ModLibrary.cs` | `RingAssetCatalog.Collection<T>` — same pattern/names as parts-now `GameRegistry` (AllMeshes/AllFiles already on the watchlist) | OK |
| 9b | `Gltf2Reference.{Id, Source : FileReference?}` + `FileReference.ModPath` · `GltfUtility.LoadModel(string) : Gltf` + `Gltf.Meshes[].Name` (JSON-only parse for the catalog) · `GltfLoader(string)` ctor + `MeshReference.Load(GltfLoader, int mesh, createDeviceMesh: false)` (conversion — the exact import `MeshFileReference.DoLoad` runs for the stock ring rocks; skinned meshes import in bind pose) | direct API | `KSA/Gltf2Reference.cs`; `KSA/FileReference.cs`; `Brutal.GltfApi/GltfUtility.cs`, `GltfLoader.cs`; `KSA/MeshReference.cs` | `RingAssetCatalog.RefreshGltfMeshes`, `RingMeshFactory.GetRingUsableFromGltf` — makes character/MMU/helmet meshes (the glTF-file pipeline, never in `AllMeshes`) selectable | OK |
| 9c | `DeviceMeshInterleaved.IndexCount` (public field) · `SimpleVkMesh.IndexCount` | direct API | `KSA/DeviceMeshInterleaved.cs`; `RenderCore.Mesh/SimpleVkMesh.cs` | `RingAssetCatalog.GetMeshIndexCount`, `RingMeshFactory.GetConvertedIndexCount` — UI triangle-cost readout only | OK |
| 10 | `SerializedCollection<T>.GetList() : List<T>` (live list — copied before iteration) | direct API | `KSA/SerializedCollection.cs` | `RingAssetCatalog.Refresh` | OK |
| 11 | `TextureReference.{Id, BindlessHandle : int}` + `TexturePowerReference : TextureReference` | direct API | `KSA/TextureReference.cs`; `KSA/TexturePowerReference.cs` | `RingAssetCatalog` (handle==0 ⇒ excluded), `RingSwapController` | OK |
| 12 | `Program.{Instance : static Program, GetRenderer() : static Renderer, RebuildRenderer(bool = false)}` | direct API | `KSA/Program.cs` | `RingSwapController.RebuildRenderer`, `RingMeshFactory` | OK — body retyped for `IViewport`/`ShaderSlot` (5402), call order unchanged |
| 13 | `Program._planetTransparenciesRenderer` → `PlanetTransparenciesRenderer.{_ringsRenderer, _ringRendererCreated}` (private fields) + public `PlanetaryRingsRenderer.Dispose()` (typed) + `Renderer.Device.WaitIdle()` | **reflection (string)** + direct API | `KSA/Program.cs`; `KSA/PlanetTransparenciesRenderer.cs`; `PlanetaryRingsRenderer.cs`; `Brutal.VulkanApi.Abstractions/DeviceExtensions.cs` | `RingSwapController.{IsRingsRendererCreated, DisposeRingsRendererForRecreation}` — the dispose-for-recreation step that makes the rebuild actually re-read ring data (narrative #4). A field rename degrades to a frame-resources-only rebuild: Apply hitches but changes nothing — **the original symptom**, so a silent break here is user-visible immediately | OK |
| 14 | `Renderer.Allocator.CreateStagingPool(renderer.GraphicsAndCompute, 1)` + `StagingPool` dispose = submit+wait; `SimpleVkMesh` built by `MeshReference.Bind` | direct API (render) | `Brutal.VulkanApi.Abstractions/StagingPoolExtensions.cs`, `StagingPool.cs`; `RenderCore.Mesh/SimpleVkMesh.cs` | `RingMeshFactory.GetRingUsable` | OK |
| 15 | `GameSettings.{ShowRings(), ShowRingMeshes()} : static bool` | direct API | `KSA/GameSettings.cs` | submod UI status hints | OK |
| 16 | `Universe.CurrentSystem.All.OfType<Celestial>()` | direct API | `KSA/Universe.cs`; `KSA/CelestialSystem.cs` | `RingSwapController.RefreshBodies` | OK |
| 17 | Consumer contract (not called, relied upon): `PlanetaryRingsRenderData` ctor bakes `LodProperties[i].Y = MeshLods[i].DeviceMesh.IndexCount`, `MeshCullingRadius = max BoundingSphereRadius`, `MeshDiffuseId/NormalId/PbrId = …BindlessHandle`; `PlanetaryRingsRenderer.{PopulatePlanets, RenderMeshes(Celestial, CommandBuffer, IViewport, int)}` | behavioral invariant | `KSA.Rendering.Rings.Rendering/PlanetaryRingsRenderData.cs` (byte-identical 5348↔5402); `PlanetaryRingsRenderer.cs` | design keystone — see narrative #1-#3 | OK (`RenderMeshes` param retyped `Viewport`→`IViewport`, `Index`→`ShaderSlot`; draw logic unchanged) |

Related but **not** integration points: `RingSelection` (mod-local, session-only state — overrides
are deliberately not persisted; a game restart is back to stock).

---

# Runtime ring definition — bloomin-onion

Scope for **bloomin-onion** (`bloomin-onion.lib`, bundled in the unscience supermod), which builds
brand-new `PlanetaryRingsReference` trees at runtime and puts them on any celestial. Written against
KSA build **2026.8.22.5348**, re-verified against **2026.9.7.5402** (same NEW/OLD roots as above).
Shares rocky-mcrock-face's catalog + mesh conversion (touchpoints #7-#11
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

## Touchpoints (bloomin-onion)

| # | Game member | Kind | Decomp path | Mod code ref | 5402 |
|---|---|---|---|---|---|
| B1 | `PlanetaryRingsReference` (all public fields: `DefinitionFrame, Inclination, LongitudeOfAscendingNode, InnerRadius, OuterRadius, Texture, ControlTexture, DetailScale, Volume, RingObjects`; `IsValid()` deliberately unused — narrative #1) · `PlanetaryRingsVolumeReference.{MinThickness, MaxThickness, MinRenderDistance, MaxRenderDistance, Step, FadeToMeshes}` · `RingRaymarchingStepReference.{Scale, MinSize, MaxSize}` · `RingObjectsReference.{Name, Thickness, Size, RenderDistance, Density, Lods, MaterialReference}` · `RingLodReference.{MinScreenSizePixels, MeshFileReference}` · `MeshFileReference.Mesh` · `PbrMaterialReference.{DiffuseReference, NormalReference, PBRMap}` — **constructed**, not just mutated | direct API | `KSA/PlanetaryRingsReference.cs`; `KSA/PlanetaryRingsVolumeReference.cs`; `KSA/RingRaymarchingStepReference.cs`; `KSA/RingObjectsReference.cs`; `KSA/RingLodReference.cs`; `KSA/MeshFileReference.cs`; `KSA/PbrMaterialReference.cs` | `RingReferenceBuilder.Build`, `RingDefinitionSerializer.FromReference` | OK |
| B2 | `DistanceReference(double, DistanceUnit)` / `(double meters)` · `RadianReference(double radians)` + `.ToDegrees()` · `DoubleReference.FromValue` · `BoolReference(bool)` · `DistanceReference.{InMeters(), InKilometers()}` · `MathEx.{ToDeviationAngle, ToCompassAngle}(double)` · `OrbitDefinitionFrame` | direct API | `KSA/DistanceReference.cs`; `KSA/RadianReference.cs`; `KSA/DoubleReference.cs`; `KSA/BoolReference.cs`; `KSA/MathEx.cs`; `KSA/OrbitDefinitionFrame.cs` | `RingReferenceBuilder`, `RingDefinitionSerializer` | OK |
| B3 | `AstronomicalTemplate.RingsReference` (public field, **written**) via `Celestial.BodyTemplate : CelestialTemplate` · `Celestial.{Id, MeanRadius, Parent}` | direct API | `KSA/AstronomicalTemplate.cs`; `KSA/Celestial.cs` | `RingDefinitionController.{Apply, Remove, RestoreTemplate, HasStockRings}`, `RingReferenceBuilder.Validate` | OK |
| B4 | `PlanetTransparenciesRenderer.PopulatePlanets() : bool` (public) | direct API | `KSA/PlanetTransparenciesRenderer.cs` | `RingRendererRebuilder.Rebuild` — refreshes `HasRings` per body + `_bodiesSortedBackToFront` sizing | OK |
| B5 | `Program._planetTransparenciesRenderer` → `PlanetTransparenciesRenderer.{_ringsRenderer, _ringRendererCreated, _anyRings}` (private fields) + public `PlanetaryRingsRenderer.Dispose()` + `Device.WaitIdle()` + `Program.RebuildRenderer()` | **reflection (string)** + direct API | `KSA/Program.cs`; `KSA/PlanetTransparenciesRenderer.cs` | `RingRendererRebuilder.{Rebuild, DisposeRingsRenderer, IsRingsRendererCreated}` — `_anyRings` is the only field new vs rocky (narrative #6) | OK |
| B6 | `TextureReference` subclassing: public `Category, Width, Height, Manifest, BindlessHandle, Bind(Renderer, StagingPool)` (virtual), `Dispose(Device)`, `SetHash()` · `TextureReference.<TextureAsset>k__BackingField` (private-set auto-prop) | direct API + **reflection (string)** | `KSA/TextureReference.cs` (fields), `:125` (`virtual Bind`), `:77` (`Dispose`); `KSA/SerializedId.cs` | `PaintedTextureReference.Create/Release` (`:19-20,50,60-61`) — null-checked; a miss disables Painted mode in the UI (`IsSupported`) with a clear message | OK |
| B7 | `RenderCore.TextureAsset(ITexture, string)` ctor · `Brutal.TextureApi.Abstractions.GenericTexture.Defaults.RGBA8UNorm(int2)` + `.Data` · `TextureFormatExtensions.Descriptor()` → `FormatDescriptor.{IsBlockCompressed, BlockSizeInBytes}` · `TextureAsset.Texture.Format` | direct API | `RenderCore/TextureAsset.cs`; `Brutal.TextureApi.Abstractions/GenericTexture.cs`; `FormatDescriptor.cs` | `PaintedTextureReference.Create`, `RingReferenceBuilder.IsCpuSampleable` | OK |
| B8 | `StaticCelestial._distantRenderer` → `DistantSphereRenderer._data` (private field of the **public struct `KSA.DistantSphereData`**, public fields `UseRingShadows, RingInnerRadius, RingOuterRadius, RingTextureId, SamplerClampId`) · `Program.TextureSystem.SamplerClampHandle` | **reflection (string)**, cosmetic | `KSA/StaticCelestial.cs`; `KSA/DistantSphereRenderer.cs`; `KSA/DistantSphereData.cs`; `KSA/GpuTextureSystem.cs`; `KSA/Program.cs` | `RingRendererRebuilder.SyncDistantSphereShadow` (`:87-102`) — every lookup null-tolerant, wrapped in try/catch | OK |
| B9 | `GameSettings.{ShowRings(), ShowRingMeshes()}` · `CelestialProvider.GetAllCelestials()` (ksa-abstractions → `Universe.CurrentSystem.All`) | direct API | `KSA/GameSettings.cs`; `KSA/Universe.cs` | submod UI / `BloominOnionSubmod.RefreshBodies` | OK |
| B10 | Consumer contract (relied upon): `PlanetTransparenciesRenderer.RebuildFrameResources` takes `CreateRingsRenderer` only when `!_ringRendererCreated && _anyRings`; `PlanetaryRingsRenderer.PopulatePlanets` iterates `Universe.CurrentSystem.All.OfType<Celestial>()` reading `BodyTemplate.RingsReference` at ctor; `PlanetRenderer` reads `RingsReference` per frame for the ring shadow (`PlanetRenderer.cs`); `AtmosphereRenderer.AssignPlanetSlots` keys on `AtmosphericBody` only (so a ring-only body joining `_planetsWithTransparencies` is harmless) | behavioral invariant | `KSA/PlanetTransparenciesRenderer.cs`; `PlanetaryRingsRenderer.cs`; `KSA/PlanetRenderer.cs`; `KSA/AtmosphereRenderer.cs` | design keystone — narrative #1, #6 | OK |

Related but **not** integration points: `RingDefinition` / `RingPresetStore` (mod-local model +
TOML under `.unscience/bloomin-onion-rings.toml`; body assignments deliberately session-only).

## Historical evidence

See [dated integration and upgrade reference](history/rings.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.
