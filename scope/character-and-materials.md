# Character / Material / GPU-Customization Mods — Game Integration Scope

## Workspace integration (current)

Active bundled features: **doh, humble-arteest, kitten-animations**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

Humble Arteest binds detached PaintDraft settings and exact part/model target paths; Live State enumerates part paint, template paint, material-handle color overrides and engine overrides. Engine targets enumerate `Part.Modules.Get<PartModelDynamicModule>()` and module models. Kitten recipes explicitly represent driver, expression and KittenLocomotionTuning values plus clip identity; Apply is the only bridge into bound live driver state. Doh retains its explicit spawn/suit actions and live EVA management. GPU paint and animation Harmony integration remains in the same feature libraries.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break the kitten/character and
GPU-material customization mods (`doh`, `humble-arteest`, `kitten-animations`). Every
game-facing member, Harmony target, reflection string, GPU/Vulkan API, per-instance struct
byte-offset, and shader these mods touch is enumerated and verified against the decompiled
sources **and** the Content shader tree, in both game builds.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard and feature Harmony patches are wired through `unscience/Patcher.cs`. See [architecture](00-architecture-and-abstractions.md).

**Key C# layout facts verified (load-bearing for the patches)**

- `KSA/PartModel.cs` @5018 — `PerInstanceData` (80 B) = `float4x4 ModelMatrix`(0) · `int StateBitFlag`(64) · `uint EmissiveColor`(68) · `int packing1`(72, read as `TfiThickness` by the shader) · `float Wetness`(76).
- `KSA/PartModelDynamic.cs` @5018 — `PerInstanceData` (80 B) = `float4x4 ModelMatrix`(0) · `int StateBitFlag`(64) · `float Temperature`(68) · `float TfiThickness`(72) · `float Wetness`(76).
- **`StateBitFlag` bit map** (identical in the static, dynamic and glass structs): game uses bits **0..10** (0 highlighted · 1 grabbed · 2 fake-translucent · 3 selected · 4 edited-vehicle/no-celestial-shadow · 5 IVA/no-planet-shine · 6 no-emissive · 7 add-emissive-color · 8 selected-connected · 9 selected-disconnected · 10 fuel-flow highlight). **Bits 11..31 are free** and are what humble-arteest's Vehicle Paint uses.
- **std430 stride is exactly 80 B in every variant** — the maximal enabled combination (static: `EMISSIVE`+`THIN_FILM`+`WETNESS`; dynamic: `TEMPERATURE`+`THIN_FILM`+`WETNESS`) fills it precisely, so there is **no spare trailing space** to append a field to. Any future per-instance mod data must reuse free bits, not new fields.
- `KSA/MaterialData.cs` — **byte-identical**, `[StructLayout(Sequential, Pack=1)]`: `int AlbedoTexture`(0) `int NormalTexture`(4) `int RoughMetallicAOTexture`(8) `int Sampler`(12) `float4 AlbedoColor`(**16**) `float4 RoughnessMetalScale`(32) `float4 ExtraData`(48) `int EmissiveTexture`(64).
- `KSA/CharacterAvatar.cs`, `KSA/CatExpressionAnim.cs`, `KSA/CatFurRenderable.cs`, `KSA/StaticMeshRenderable.cs`, `KSA/CharacterReference.cs`, `KSA/CharacterTexturesReference.cs`, `KSA/PbrMaterialReference.cs`, `KSA/GpuTextureSystem.cs`, `KSA/PartModelDynamicModule.cs` — all **byte-identical** OLD↔NEW (4680↔4750).
- **@5402 re-check:** `MaterialData` still byte-identical (the 80 B stride is `EmissiveTexture`(64) + `Padding0..2`(68-79)); both `PerInstanceData` structs byte-for-byte identical (`PartModel.cs`, `PartModelDynamic.cs`); `StateBitFlag` writers still stop at bit 10. `PartModel.AddInstance`/`PartModelDynamic.AddInstance` and the two `*Module.UpdateRenderData` now take `IViewport` (was `Viewport`) and `AddInstance` is gated on `ViewportOptionFlags.RenderPartModels` — see the 5348 → 5402 area summary.

---

## doh

**Purpose** — "Dynamically Originating Hominids": programmatically spawns `KittenEva` entities
near a vehicle (replicating `EVADoor.CreateKittenEva`), with optional per-kitten GPU material
cloning + `AlbedoColor` tint. Live recolor, batch spawn, "I'm Feeling Lucky" rainbow, despawn.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Exact/controlled reference vehicle, character or random choice, count, offset and tint settings. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

### Integration points

| # | Kind | Mod code (file) | Game target (Type.Member + sig / struct-offset) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Reflection (private) | MaterialSystemAccessor.cs | `KSA.Program` type; `Program.Instance` static prop (`public static Program Instance { get; private set; }`) | KSA/Program.cs | ✅ | none (was :434 @5348) | singleton root |
| 2 | Reflection | MaterialSystemAccessor.cs | `Program.MaterialSystem : GpuMaterialSystem` (`public readonly` field) | KSA/Program.cs | ✅ | none (was :99 @5348) | |
| 3 | Reflection (hierarchy) | MaterialSystemAccessor.cs | `AssetManager<T>.AssetMap` (protected `ConcurrentDictionary<AssetName,T>`) | KSA/AssetManager.cs | ✅ | none | walks base types |
| 4 | Reflection | MaterialSystemAccessor.cs | `GpuObjectSystem<T>.BigBuffer : BufferEx` (public get/protected set) | KSA/GpuObjectSystem.cs | ✅ | none | |
| 5 | Reflection (hierarchy) | MaterialSystemAccessor.cs | `GpuObjectSystem<T>.DeviceCtx : IVulkanContext` (protected field) | KSA/GpuObjectSystem.cs | ✅ | none | |
| 6 | Reflection (hierarchy) | MaterialSystemAccessor.cs | `GpuObjectSystem<T>.CreateObject(AssetName, T) : bool` | KSA/GpuObjectSystem.cs | ✅ | none | `(AssetName)name, MaterialData` |
| 7 | Reflection (hierarchy) | MaterialSystemAccessor.cs | `AssetManager<T>.GetOrLoad(AssetName) : T` | KSA/AssetManager.cs | ✅ | none | returns `GpuObjectAssetRef` |
| 8 | Reflection | MaterialSystemAccessor.cs | asset-ref `.Handle` (int) on `GpuObjectAssetRef` | KSA/GpuObjectAssetRef.cs | ✅ | none | map name→buffer index |
| 9 | Reflection | MaterialSystemAccessor.cs | `Program.SuperMeshRenderSystem`; `.TextureSystem : GpuTextureSystem` (`public readonly`); `GpuTextureSystem.GetOrLoad` | KSA/Program.cs; SuperMeshRenderSystem.cs | ✅ | none (5402 `SuperMeshRenderSystem.cs` diff is two-sided skinned techniques + `IViewport` only) | texture bindless lookup |
| 10 | GPU write (Vulkan) | MaterialSystemAccessor.cs; KittenMaterialSet.cs | `BufferEx.VkBuffer`; `IVulkanContext.Device.CreateStagingPool`; `VkUtils.StageAndUploadToBuffer`; `ByteSize.Of<MaterialData>()`; `Marshal.OffsetOf<MaterialData>(AlbedoColor)`=16 | KSA/MaterialData.cs; Brutal.Vulkan | ✅ | none | writes `float4` at `handle*80+16`; span→bytes via BCL `MemoryMarshal.AsBytes` (no `CommunityToolkit.HighPerformance` reference) |
| 11 | Typed | MaterialFactory.cs | `KSA.MaterialData` ctor fields (Albedo/Normal/RoughMetallicAO/Sampler/AlbedoColor/RoughnessMetalScale/Emissive/ExtraData) | KSA/MaterialData.cs | ✅ | none | `Pack=1`, identical |
| 12 | Reflection | MaterialFactory.cs | `ModLibrary.Get<PbrMaterialReference>(string)` | KSA/ModLibrary.cs | ✅ | none (was :1040 @5348) | |
| 13 | Typed | MaterialFactory.cs | `ModLibrary.Get<CharacterReference>`; `CharacterReference.CharacterTextures : CharacterTexturesReference` | KSA/CharacterReference.cs | ✅ | none (file identical) | |
| 14 | Reflection | MaterialFactory.cs | `CharacterTexturesReference.{CharacterBodyMaterial,CharacterHeadMaterial,CharacterEyeMaterial} : PbrMaterialReference` | KSA/CharacterTexturesReference.cs | ✅ | none (file identical) | |
| 15 | Reflection | MaterialFactory.cs | `PbrMaterialReference.{DiffuseReference,NormalReference,PBRMap,EmissiveMap,Id}`; non-generic `.Get()` | KSA/PbrMaterialReference.cs | ✅ | none (file identical) | `.BindlessHandle` off resolved `TextureReference` |
| 16 | Reflection | MaterialFactory.cs | `Program.CharacterRenderSystem`; `CharacterRenderSystem._resources : CharacterRenderResources`; `.FurTexture/.CatFurMaskTexture` (`.BindlessHandle`), `.FurSampler` (`.BindlessIndex`) | KSA/CharacterRenderSystem.cs; CharacterRenderResources.cs | ✅ | fields none; file diff is internal shader wiring only (see below) | fur `ExtraData` handles |
| 17 | Reflection | MaterialFactory.cs | `GpuTextureSystem.{SamplerRepeatHandle,DefaultWhiteTexture,DefaultBlackTexture}`; `SuperMeshRenderSystem.GltfSystem`; `GltfPbrSystem.BlankMaterialTexture.BindlessHandle` | KSA/GpuTextureSystem.cs; GltfPbrSystem.cs | ✅ | none (GpuTextureSystem.cs identical) | default-texture fallbacks |
| 18 | Reflection (internal field) | KittenSpawner.cs | `ModLibrary.AllParts` (internal static `SerializedCollection<PartTemplate>`); `.Find(KeyHash) : PartTemplate` | KSA/ModLibrary.cs; SerializedCollection.cs | ✅ | none | `"KittenBackPackPart"` (`:275`) |
| 19 | Reflection (internal field) | KittenSpawner.cs | `ModLibrary.AllCharacters` (internal static `SerializedCollection<CharacterReference>`); `.GetList() : List<T>` | KSA/ModLibrary.cs; SerializedCollection.cs | ✅ | none (line was already `:100` @5348) | character enumeration |
| 20 | Typed | KittenSpawner.cs | `new KittenEva(CelestialSystem system, string characterId, doubleQuat body2Cce, double3 bodyRates, IParentBody parent, string id, Part root, Orbit orbit)` | KSA/KittenEva.cs | ✅ | none (5402 `KittenEva.cs` diff is `UpdateRenderData(IViewport)`, `UpdateHighlight(IGameViewport)` + new `DrawHud` only) | 8-arg ctor identical |
| 21 | Typed (pattern) | KittenSpawner.cs | mirrors `EVADoor.CreateKittenEva(Vehicle, IVASeat, KittenRosterEntryData)` (private) | KSA/EVADoor.cs | ✅ | none (file byte-identical 5348↔5402) | call shape mirrored, not invoked |
| 22 | Typed | KittenSpawner.cs | `new Part(id, PartTemplate)` (`Part.cs`); `Part.Tree.ReinitializeDerivedValues/RefillConsumables` (`PartTree.cs`); `Part.SubtreeModules.Get<Tank>()`; `Tank.ConfigureFor(ReactantMix, bool recreateResourceManagers = true)` | KSA/Tank.cs | ✅ | none (`Tank.cs` byte-identical) | backpack/propellant |
| 23 | Typed | KittenSpawner.cs | `SubstanceLibrary.TryGetReaction(KeyHash)` → `MixtureReaction.AtMixtureRatio(DefaultMixtureRatio).ReactantMix`; `KeyHash.Make` | KSA/SubstanceLibrary.cs | ✅ | none (`SubstanceLibrary.cs` byte-identical; `TryGetCombustionProcess` removed at 5018) | `"MMH_NTO"` |
| 24 | Typed | KittenSpawner.cs | `Orbit.CreateFromStateCci(IParentBody, UniverseTime, double3, double3, byte4)`; `Orbit.OrbitLineColor : byte4` | KSA/Orbit.cs | ✅ | none (`Orbit.cs` differs elsewhere only) | |
| 25 | Typed | KittenSpawner.cs | `Universe.CurrentSystem : CelestialSystem?`; `Universe.GetElapsedTime() : UniverseTime` | KSA/Universe.cs | ✅ | none (was `:2060` @5348) | `GetElapsedSimTime` was renamed at 5211 — fixed then |
| 26 | Typed | KittenSpawner.cs | `Vehicle.GetAsmb2Cci()`; `.Body2Cce`; `.BodyRates`; `.Parent`; `.Orbit.StateVectors`(`.PositionCci/.VelocityCci`); `double3.Transform(doubleQuat)` | KSA/Vehicle.cs | ✅ | none (line moves) | spawn positioning |
| 27 | Typed | KittenSpawner.cs | `KittenEva.Teleport(Orbit?,doubleQuat?,double3?)`; `IParentBody.Children.Add` (`IParentBody.cs`); `Vehicle.UpdatePerFrameData()` (override) | KSA/Vehicle.cs | ✅ | none (line moves) | |
| 28 | Typed | KittenSpawner.cs | `CelestialSystem.All.TryGet(string,out Astronomical)` (`All :64`); `CelestialSystem.Deregister(Astronomical)` (`:91`, takes the `Vehicle` by upcast); `Vehicle.Dispose()` | KSA/CelestialSystem.cs; Vehicle.cs | ✅ | none (5402 `CelestialSystem.cs` diff is the internal `AstronomicalRef` lookup only) | despawn |
| 29 | Reflection | KittenSpawner.cs | `KittenEva._renderable : KittenRenderable` (`KittenEva.cs`) → `._characterAvatar : CharacterAvatar` (both private) | KSA/KittenEva.cs; KSA/KittenRenderable.cs | ✅ | none | avatar root |
| 30 | Reflection (field path) | KittenSpawner.cs | `CharacterAvatar.Core.CharacterModel.MaterialIndices`; `.Fur.CatFurRenderable.MaterialIndices`; `.Attachments.Helmet.HelmetMesh/.VisorMesh.MaterialIndices`; `.Attachments.Mmu.MmuMesh.MaterialIndices` | KSA/CharacterAvatar.cs/61,213/107,109,128; AnimatedRenderable.cs; CatFurRenderable.cs; StaticMeshRenderable.cs | ✅ | 5402 additive only: `CharacterCore.HeadMeshIndices : List<int>` (`CharacterAvatar.cs`, from `CharacterCoreReference.HeadMeshIndices` `:21-22` / `CharacterAssets.xml:244-251`); `AnimatedRenderable.{PrePassIgnoreMeshIndices,MaskedMeshIndices,HideMaskedMeshes}` (`:62-66`) | `MaterialIndices` is `protected readonly int[]` on each renderable; in-place handle swap. ℹ️ `KittenRenderable.HideHead` (`:98`, set by `IVASeat.cs` when the camera is in that seat) masks the head meshes and skips the fur draw (`:355-360`) — cosmetic, the handle swap is untouched |
| 31 | Typed (context) | — (not referenced) | `KittenEva.IsControllable => true` / `Vehicle.IsControllable` (virtual) | KSA/KittenEva.cs; Vehicle.cs | ✅ | **ADDED (rev 4699)** | informational: spawned kittens now controllable; not a break |
| 32 | Typed | KittenSpawner.cs | `JobSystems.VehicleSolver : JobScheduler` (public static, `Brutal.Concurrency.Jobs`); `JobScheduler.Wait()` (spins until all runners idle) | KSA/JobSystems.cs; Brutal.Concurrency.Jobs/JobScheduler.cs | ✅ | **ADDED (5402 fix)** | Guards `new KittenEva` and `Vehicle.Dispose()` against `ConstraintSim.UnlockShapes()` (`ConstraintSim.cs`) throwing while `VehicleUpdateTask.Run` (`:176`, `BeginVehicleUpdate`/`EndVehicleUpdate`) is stepping on the solver thread. Depends on frame ordering in `Program.PrepareFrame` (`Program.cs`): `VehicleSolver.Wait()` → `ApplyVehicleSolvers` → `ExecuteNextVehicleSolvers` queues the next step; nothing re-queues mid-frame. `doh.lib.csproj` now references `Brutal.Concurrency.dll`. Game's own equivalent is staging via `InputEvents.EvaSpawnBuffer` (`InputEvents.cs`, applied `:1072`) |

### Game assets referenced

- **Characters** by id from `ModLibrary.AllCharacters` (e.g. `"Calico"`, user-selectable / random). No hardcoded character id.
- **Part template** `"KittenBackPackPart"` (`KittenSpawner.cs`).
- **Reaction** `"MMH_NTO"` (`KittenSpawner.cs` → `TryGetReactantMix`), defined in
  `Core/Reactions.xml` as `<MixtureReaction Id="MMH_NTO">` with `DefaultMixtureRatio` 1.65.
  Was the combustion process `"MMH_NTO_1.6"` before 5018 — the mixture ratio is no longer part of
  the id, so the old id resolves to nothing.
- **Fur texture** `"FurNoise"` reached indirectly via `CharacterRenderResources.FurTexture` (game loads it; mod only reads `.BindlessHandle`).
- No shader files referenced directly. Tint takes effect through `ModelPbr.frag` → `MaterialSet.glsl` (`albedo = mat.albedoColor * texture(...)`, **identical** both builds).

## humble-arteest

**Purpose** — Three independent visual-customization features matched to KSA's three rendering
data paths: **(A) Vehicle Paint** (per-part-instance albedo tint — color packed into the free high
bits of `PerInstanceData.StateBitFlag`, applied by a runtime-patched copy of the part fragment
shaders), **(B) Kitten Color** (`AlbedoColor` writes to the GPU material buffer), **(C)
Engine Emissive** (Harmony override of the per-instance `Temperature`/`TfiThickness` engines glow).

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Paint brush/blend/scope, exact part and engine sets, part types/material names, tints and emissive parameters. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Vehicle Paint mechanism (rewritten for 5018)** — The paint color is quantized to 7:7:7 sRGB and
ORed into `StateBitFlag` **bits 11..31**, which the game leaves unused (it writes only bits 0..10).
That field exists at the same offset in *every* `PerInstanceData` variant and is already forwarded
to every part fragment shader as the `inStateFlags`@loc4 varying, so **no vertex shader, struct
layout, stride, descriptor binding, or game-used field is touched** — in particular `EmissiveColor`,
`Temperature`, `TfiThickness` and `Wetness` are all left alone. Only the *fragment* shader is
modified, and only in memory: a prefix on `ShaderModuleUtils.FromFile` compiles a patched source
string (with the caller's own `CompileOptions`, so every `ENABLE_*` variant still builds) instead of
the file on disk. Installation requests the game's own deferred `Program.RendererRebuildNeeded`
rebuild, which is what recompiles the part pipelines.

### Integration points

| # | Kind | Mod code (file) | Game target (Type.Member + sig / struct-offset / shader path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| A1 | Harmony PREFIX | VehiclePaintPatches.cs `ResolveFromFile`/`FromFilePrefix` | `RenderCore.ShaderModuleUtils.FromFile(Device, string filePath, out VkShaderStageFlags shaderStage, CompileOptions? options) : VkShaderModule` (**static**) | RenderCore/ShaderModuleUtils.cs | ✅ | n/a (new seam) | The only interception point that works ≥4693: part pipelines recompile per variant straight from disk. Prefix returns `true` (stock behavior) for every non-target path and on any error |
| A2 | Typed call | VehiclePaintPatches.cs `FromFilePrefix` | `ShaderModuleUtils.FromString(Device, ReadOnlySpan<byte>, VkShaderStageFlags, CompileOptions?, ReadOnlySpan<byte> debugName)`; `ShaderStageFromFileExtension(string)` | RenderCore/ShaderModuleUtils.cs | ✅ | n/a (new) | `debugName` = the original file path (NUL-terminated) so relative `#include`s resolve exactly as stock; `options` passed through unmodified |
| A3 | Harmony PREFIX | VehiclePaintPatches.cs `PartModelModulePrefix` | `PartModelModule.UpdateRenderData(in double4x4, bool, IViewport viewport, int) : void`; reads `Module<T>.Parent : Part` | KSA/PartModelModule.cs; KSA/Module.cs | ✅ | 5402: `Viewport`→`IViewport` (single overload, resolved by name → no impact); light-switch test collapsed to `Parent.FullPart.IsLightSwitchedOff()` (`:106-108`, still bit 6) | Records which `Part` is about to submit. Callers of `PartModel.AddInstance`: this (`:155`) **and** `KSA.Rendering.Thumbnails/ThumbnailPart.cs` (thumbnails, `StateBitFlag = 0`, no `UpdateRenderData` → `_pendingPart` is null → unpainted, harmless) |
| A4 | Harmony PREFIX | VehiclePaintPatches.cs `PartModelDynamicModulePrefix` | `PartModelDynamicModule.UpdateRenderData(in double4x4, bool, IViewport viewport, int) : void` | KSA/PartModelDynamicModule.cs | ✅ | 5402: `Viewport`→`IViewport`; same `IsLightSwitchedOff()` collapse (`:97-99`) | Same hand-off for dynamic parts; callers of `PartModelDynamic.AddInstance`: this (`:127`) and `ThumbnailPart.cs` (same null-slot guard) |
| A5 | Harmony PREFIX | VehiclePaintPatches.cs `AddInstancePrefix` | `PartModel.AddInstance(PerInstanceData instanceData, IViewport viewport, int frameIndex) : void` — ORs paint into `instanceData.StateBitFlag` | KSA/PartModel.cs (struct :332-343) | ✅ | 5402: `Viewport`→`IViewport`; new early-return `if (!viewport.HasAny(ViewportOptionFlags.RenderPartModels)) return;` (`:410`) runs **after** the prefix — the pending slot is still consumed, nothing leaks | Binds by param name `instanceData` (unchanged); **no** `Unsafe.As` mirror struct any more — writes the public field directly |
| A6 | Harmony PREFIX | VehiclePaintPatches.cs `AddInstanceDynamicPrefix` | `PartModelDynamic.AddInstance(PerInstanceData inInstanceData, IViewport viewport, int inFrameIndex) : void` | KSA/PartModelDynamic.cs (struct :342-353) | ✅ | 5402: `Viewport`→`IViewport`; same `RenderPartModels` gate (`:414`) | Param name `inInstanceData` (unchanged) |
| A7 | Typed | VehiclePaintShaders.cs `RequestRendererRebuild` | `Program.RendererRebuildNeeded : bool` (public static) | KSA/Program.cs (consumed at :2097 `PrepareFrame`) | ✅ | none (line moves) | The game's own deferred-rebuild flag — the same path a Frost/Water graphics-setting change takes, so pipelines are destroyed at a frame boundary, not mid-record |
| A8 | Typed | VehiclePaintShaders.cs `TryResolveShaderPath` | `ModLibrary.Get<ShaderReference>("MeshIndirectFrag")` → `FileReference.ModPath : string` | KSA/PartModelRenderer.cs; KSA/FileReference.cs | ✅ | none | Pre-flight check only, so a shader change fails visibly at "Enable" instead of silently |
| A9 | **Shader text edit** (in memory) | VehiclePaintShaders.cs `Inject`/`BuildSnippet` | `MeshIndirect.frag` **and** `MeshIndirectRaytraced.frag` — anchor = first line starting `vec3 sampledColor` and ending `;`; also requires the `inStateFlags` varying | Content/Core/Shaders/Mesh/MeshIndirect.frag:114; MeshIndirectRaytraced.frag:156 | ✅ | n/a (new anchors) | Anchored on the albedo *declaration*, not an exact line, so incidental upstream edits do not break it. Snippet appends after the sample so paint flows through thin film / frost / PBR. Uses `gammaToLinear` (Common/Shared.glsl:203) |
| A10 | **Per-instance bit budget** | VehiclePaint.cs `EncodeBits` (`PaintBitShift`=11, 7:7:7) | `PerInstanceData.StateBitFlag` **bits 11..31** — game writes only bits 0..10 | writers: KSA/PartModelModule.cs, PartModelDynamicModule.cs, PartModelGlassModule.cs; readers: MeshIndirect.frag:312-353, MeshIndirectRaytraced.frag:291-333 | ✅ | none @5402 — writers still `<<0..3`, `<<8..10`, `0x10..0x80`; thumbnails write `StateBitFlag = 0` | 🔶 **The one thing to re-check on every game update:** if KSA starts using bit 11 or above, paint and that feature will corrupt each other. `RayTraceInstance.StateFlags` is `int` (RaytracingRenderer.cs, copied at :1107), so the bits survive the RT path too |
| A11 | Typed | PaintTargets.cs | `Program.Editor : VehicleEditor?`; `VehicleEditor.EditingSpace : VehicleEditingSpace` → `.Parts : PartTree?`; `.UnattachedPartTrees : List<PartTree>`; `PartTree.Parts : ReadOnlySpan<Part>`; `Part.SubParts/Id/DisplayName/Modules` | KSA/Program.cs; VehicleEditor.cs; VehicleEditingSpace.cs; PartTree.cs; Part.cs | ✅ | none (line moves) | Enumerates paint targets in both flight (via `VehicleProvider`) and the editor — mirrors the two sources `Program` itself walks |
| B1 | Reflection | KittenColor.cs | `Program.Instance`→`MaterialSystem`→`AssetMap`/`BigBuffer`/`DeviceCtx` (same chain as doh #1-5) | KSA/Program.cs; GpuObjectSystem.cs; AssetManager.cs | ✅ | none | |
| B2 | GPU write (Vulkan) | KittenColor.cs | `BigBuffer.VkBuffer` + `VkUtils.StageAndUploadToBuffer` at `handle*ByteSize.Of<MaterialData>() + OffsetOf(AlbedoColor=16)` | KSA/MaterialData.cs | ✅ | none (file byte-identical) | tints fur/body/eyes; span→bytes via BCL `MemoryMarshal.AsBytes` (no `CommunityToolkit.HighPerformance` reference) |
| B3 | Shader path (read-only) | (effect) KittenColor.cs concept | `ModelPbr.frag` → `MaterialSet.glsl`: `albedo = mat.albedoColor * texture(...)` (`:31`); alpha `discard` (`ModelPbr.frag:67`) | Content/Core/Shaders/Mesh/ModelPbr.frag:65-75; Common/MaterialSet.glsl:31 | ✅ | MaterialSet.glsl **identical**; ModelPbr.frag @5402 adds only `faceNorm = gl_FrontFacing ? inNormal : -inNormal` (`:70-73`, two-sided parachute canopy) — albedo path untouched | tint path intact |
| C1 | Harmony PREFIX | EngineEmissivePatches.cs | `PartModelDynamic.AddInstance(PerInstanceData inInstanceData, IViewport viewport, int inFrameIndex) : void` (prefix `ref … inInstanceData`) | KSA/PartModelDynamic.cs | ✅ | 5402: `Viewport`→`IViewport` + `RenderPartModels` gate (`:414`, after the prefix) — resolved by name, single overload → no impact | param name `inInstanceData` matches |
| C2 | Struct reinterpret (`Unsafe.As`) | EngineEmissivePatches.cs | `PartModelDynamic.PerInstanceData` — writes `Temperature`@**68**, `TfiThickness`@**72** | KSA/PartModelDynamic.cs | ✅ | **none** (struct byte-identical; game use of bytes 68–79 unchanged: `MeshIndirect.vert:82 outTemperature = instanceData.Temperature`) | ✅ mirror struct matches **exactly** |
| C3 | Typed | EngineEmissive.cs | `Part.Modules.Get<PartModelDynamicModule>()`; `PartModelDynamicModule.PartModelDynamic` (`required`) | KSA/PartModelDynamicModule.cs | ✅ | none | engine discovery via `PartHelpers.GetAllParts` |
| C4 | Shader path (read-only) | (effect) — no mod edit | Temperature→emissive LUT logic, formerly `DynamicMeshIndirect.frag`, now `MeshIndirect.frag` under `#ifdef ENABLE_TEMPERATURE` | Content/Core/Shaders/Mesh/MeshIndirect.frag:46-48 (decl: `inTemperature`@loc7, `temperatureLut` binding 9), :297-304 (LUT sample); vert:46-47,81-82 | ✅ | **MOVED** (4693): `DynamicMeshIndirect.frag/.vert` files **removed**; dynamic pipeline now compiles `MeshIndirectVert/Frag` with `ENABLE_TEMPERATURE` (PartModelRenderer.cs). Both shader files byte-identical 5348↔5402 | ✅ feature still works — game still reads `PerInstanceData.Temperature` |

### Game assets referenced

- **Fragment shaders patched in memory (never on disk):** `Content/Core/Shaders/Mesh/MeshIndirect.frag` and `Content/Core/Shaders/Mesh/MeshIndirectRaytraced.frag`. Matched by **file name** at `ShaderModuleUtils.FromFile` time; `"MeshIndirectFrag"` is also resolved by `ModLibrary` id for the pre-flight check. `MeshIndirect.vert` is **no longer touched at all**.
- **Glass parts are deliberately not painted** — `MeshGlassIndirect.frag` declares `inStateFlags` but ignores it, so windows stay clear.
- **GPU material buffer** (`GpuMaterialSystem.BigBuffer`) for Kitten Color (no asset path).
- **Removed assets the mod's design once assumed:** `DynamicMeshIndirect.vert/.frag` (gone, 4693), `ModelEye.frag`, `ModelGlass.frag` (gone, 4745). `ModelTranslucent.frag` is **new** (4747). None are referenced by the current implementation.

## kitten-animations

**Purpose** — Drives the controlled kitten's `CharacterAvatar`: plays every animation the game loaded
for it (the full ground/EVA locomotion set, the MMU set, the live blend samplers, the overlay poses),
triggers the five facial expressions, and exposes the animation-processor blend weights plus the
animation-facing slice of `KittenLocomotionTuning.Current`.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**Harmony (new this pass)** — one prefix on `AnimatedRenderable.UpdateAnimation(double dt)`
(`KittenAnimationPatches`). Applied from
`unscience/Patcher.cs` (`TryApply("kitten-animations", …)`) when embedded. Required because
`KittenRenderable.UpdateRenderData` calls `SetAnimation` unconditionally for nearly every locomotion
mode, so a clip set from a StarMap callback is discarded before it is sampled. The prefix runs for
every `AnimatedRenderable` in the scene and returns immediately unless the instance is the kitten body
model; it also re-applies processor knobs the game rewrites per frame and can scale `dt`.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Exact/controlled kitten, named clip, body/strength controls, expression/variant/envelope and tuning values. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

### Integration points

| # | Kind | Mod code (file) | Game target (Type.Member + sig) | Decomp path (NEW) | In NEW? | Risk/notes |
|---|---|---|---|---|---|---|
| 1 | Typed (via abstraction) | KittenAvatarAccessor.cs | `VehicleProvider.GetControlledVehicle()` → `Program.ControlledVehicle : Vehicle?`; pattern-match `is KittenEva` | KSA/Program.cs; KSA/KittenEva.cs | ✅ | unchanged |
| 2 | Typed | KittenAvatarAccessor.cs | `KittenEva.Renderable : KittenRenderable` (public property) | KSA/KittenEva.cs | ✅ | **replaces the old `_renderable` reflection** — added rev ≤5348 |
| 3 | Reflection (private) | KittenAvatarAccessor.cs | `KittenRenderable._characterAvatar : CharacterAvatar` | KSA/KittenRenderable.cs | ✅ | same field doh/garrys use |
| 4 | Typed | PlaybackSection.cs | `KittenEva.{LocomotionState, ControlMode, AnimPlaybackRate, AnimJumpChainStage, AnimJumpChainCountdown}` | KSA/KittenEva.cs | ✅ | read-only status; all public |
| 5 | Typed | PlaybackSection.cs, TuningSection.cs | `LocomotionState.{Mode, GroundSpeed, GravityMagnitude}`; `LocomotionMode`, `JumpChainStage` enums | KSA/LocomotionState.cs; LocomotionMode.cs; JumpChainStage.cs | ✅ | struct copy per frame |
| 6 | Reflection (private, cached `FieldInfo`) | KittenAnimationCatalog.cs | `KittenRenderable.{_groundIdleAnim,_groundWalkAnim,_groundRunAnim,_ladderAnim,_jumpIntroAnim,_flailAnim,_jumpLandAnim,_moonWalkAnim,_moonRunAnim,_swimAnim,_swimIdleAnim,_seatedIdleAnim} : AnimationAssetRef?`, `_seatedIdleActionAnims : List<AnimationAssetRef>?` | KSA/KittenRenderable.cs | ✅ | **the only route to the ground set** — it is not on `CharacterAvatar`. Unresolved names are collected and shown in the UI |
| 7 | Reflection (private, cached `FieldInfo`) | KittenAnimationCatalog.cs | `KittenRenderable.{_walkPairSampler,_runPairSampler,_swimPairSampler} : AnimationPairBlendSampler?`, `_blendSampler : AnimationDirectionalBlendSampler` | KSA/KittenRenderable.cs | ✅ | playable + `.Weight` readout |
| 8 | Typed | KittenAnimationCatalog.cs | `AnimationPairBlendSampler.Weight : float`; `AnimationDirectionalBlendSampler` (type) | KSA.Rendering/AnimationPairBlendSampler.cs; AnimationDirectionalBlendSampler.cs | ✅ | read-only display |
| 9 | Reflection (private) | KittenAnimProcessors.cs | `KittenRenderable.{_catPersonalityExpressionAnim,_catExpressionAnim} : CatExpressionAnim`, `_catEyeAnim : CatEyeAnim`, `_catEarAnim : CatEarAnim` | KSA/KittenRenderable.cs | ✅ | resolved **by name**, not `OfType<>()` — two of the four are the same type with different roles |
| 10 | Typed | KittenAnimationDriver.cs | `CatEarAnim.ExpressionWeight : float` | KSA/CatEarAnim.cs | ✅ | game sets it once at construction; mod value holds |
| 11 | Typed | KittenAnimationDriver.cs | `CatEyeAnim.{MaxLookAtAngle, LookPitchOffsetDeg} : float` | KSA/CatEyeAnim.cs | ✅ | `LookPitchOffsetDeg` is rewritten every frame by `UpdateLocomotionAnimationState`, so it is re-applied from the pose prefix |
| 12 | Typed | KittenAnimationDriver.cs | `CatExpressionAnim.ExpressionWeight : float` on the personality + reactive processors | KSA/CatExpressionAnim.cs | ✅ | reactive weight is damped from acceleration every frame in `UpdateRenderData`, so it can only be **capped**, never held |
| 13 | Typed | KittenExpressionController.cs | `new CatExpressionAnim { CharacterAvatar, ExpressionAnim, ExpressionWeight, Priority }` appended to `AnimatedRenderable.AnimProcessors : List<IAnimProcessor>` | KSA/CatExpressionAnim.cs; CatPostAnim.cs; AnimatedRenderable.cs | ✅ | **mod-owned processor** — required because the game rewrites its own. `CharacterAvatar` is a `required` member on `CatPostAnim` |
| 14 | Reflection (private, cached `FieldInfo`) | KittenExpressionController.cs | `CatExpressionAnim._expressionPose : TransformTRS[]?` (set null to bust the sampled-pose cache) | KSA/CatExpressionAnim.cs | ✅ | now busted on the mod's **own** processor; cache logic at `UpdateLocalPose`. File byte-identical 5348↔5402 |
| 15 | Typed | KittenExpressionController.cs | `CharacterAvatar.Expressions.{Angry,Awe,Happy,Sad,Scared} : List<AnimationAssetRef>?` | KSA/CharacterAvatar.cs | ✅ | per-variant selection, not just a random pick |
| 16 | Typed | KittenAnimationCatalog.cs | `CharacterAvatar.Animations.MmuAnimations.{MmuIdleDefaultAnim, MmuIdleActionsAnim, MmuMove{Left,Right,Forward,Backward,Up,Down}LoopAnim, MmuArmRetractAnim}` | KSA/CharacterAvatar.cs | ✅ | `MmuIdleActionsAnim` list + `MmuArmRetractAnim` are new to the mod this pass |
| 17 | Typed | KittenAnimationCatalog.cs | `CharacterAvatar.Animations.{HelmetMaskAnim, BlinkAnim} : AnimationAssetRef?` | KSA/CharacterAvatar.cs | ✅ | overlay poses |
| 18 | Typed | KittenAnimationCatalog.cs | `AnimationAssetRef.{Id, AnimLength, LoopPeriod}`; `IAnimation.{AnimLength, LoopPeriod}` | KSA/AnimationAssetRef.cs; IAnimation.cs | ✅ | `Id` needs a `Planet.Core` assembly reference (`Core.AssetName`) |
| 19 | Harmony prefix | KittenAnimationPatches.cs | `AnimatedRenderable.UpdateAnimation(double dt)` — `(AnimatedRenderable __instance, ref double dt)` | KSA/AnimatedRenderable.cs | ✅ | **the whole override mechanism**; runs for every animated renderable in the scene. 5402: single overload, signature unchanged; new early-out `if (SkinningPoseIsViewportInvariant && _lastSkinningFrameNumber == Program.FrameNumber)` (`:140-144`) — the flag (`:42`) is only ever set by `ChuteRenderable.cs` (parachutes), never on the kitten body model, so the prefix's per-frame behaviour is unchanged for the kitten |
| 20 | Typed | KittenAnimationDriver.cs | `AnimatedRenderable.{SetAnimation(IAnimation, float), PlayAnimation(IAnimation, float), FreezeAnimation}` | KSA/AnimatedRenderable.cs | ✅ | `SetAnimation` is a no-op when the clip is already current (`KSA/BoneAnimRuntime.SetAnimation`), so it is safe per frame |
| 21 | Typed | KittenAnimationsSubmod.cs | `CharacterAvatar.Core.CharacterModel : AnimatedRenderable`; `CharacterAvatar.Personality` | KSA/CharacterAvatar.cs | ✅ | model identity is what the prefix matches on |
| 22 | Typed (mutating global) | TuningSection.cs | `KittenLocomotionTuning.Current` (public static field) + `Default`; fields `AnimBlendTime`, `IdleSpeedThreshold`, `PlaybackRateMin/Max`, `Walk/Run/Ladder/TumbleClipNominalSpeed`, `Moonwalk*`, `NominalSwimAnimSpeed`, `SwimBlendFullSpeed/HalfLife`, `SwimEyePitchFactor`, `JumpLandDuration`, `JumpLandBounceIgnoreTime`, `LadderEyePitchDeg` | KSA/KittenLocomotionTuning.cs | ✅ | **global** — affects every kitten. The game ships the full editor at menu bar → Debug → Kitten Tuning (`Program.cs`) |
| 23 | Typed | TuningSection.cs | `KittenLocomotion.{ComputeMoonwalkWeight(float, in KittenLocomotionTuning), ResolveSwimBlend(float, in KittenLocomotionTuning)}` | KSA/KittenLocomotion.cs | ✅ | derived-weight readout only |

### Game assets referenced

- **None by string/path.** Every animation is a typed `AnimationAssetRef` / `IAnimation` already
  resolved by the game on the live `KittenRenderable` / `CharacterAvatar`; the mod selects from them
  and never loads an asset by id. No shader, material, or character-id references.

## Historical evidence

See [dated integration and upgrade reference](history/character-and-materials.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.
