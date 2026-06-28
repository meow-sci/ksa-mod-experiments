# Character / Material / GPU-Customization Mods — Game Integration Scope

Permanent reference for detecting when KSA game updates break the kitten/character and
GPU-material customization mods (`doh`, `humble-arteest`, `kitten-animations`). Every
game-facing member, Harmony target, reflection string, GPU/Vulkan API, per-instance struct
byte-offset, and shader these mods touch is enumerated and verified against the decompiled
sources **and** the Content shader tree, in both game builds.

**Verified game versions**

- NEW decomp `2026.6.9.4750` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- OLD decomp `2026.6.8.4680` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`
- NEW Content root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\Content`
- OLD Content root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\Content`

Paths in the **Decomp/Content path (NEW)** column are relative to the NEW decomp root
(namespace-foldered, e.g. `KSA/PartModel.cs`) or the NEW Content root
(e.g. `Core/Shaders/Mesh/MeshIndirect.vert`). **Mod code** paths are relative to the repo
root `C:\Users\Alex\repos\meow-sci\unscience`. Every game target was grepped/read in BOTH
decomps and (for shaders) BOTH Content trees; "Δ vs OLD" records the real delta (line moves
are not deltas).

**How these mods are hosted (all three)**

- Reusable game-facing logic lives in the `*.lib` project (`doh.lib`, `humble-arteest.lib`,
  `kitten-animations.lib`); each exposes one or more `ISubmod`
  (`MeowSci.KsaAbstractions.ISubmod`) consumed two ways:
  1. **Standalone** StarMap mod (`doh/Mod.cs` **F8**, `humble-arteest/Mod.cs` **F11**,
     `kitten-animations/Mod.cs` **F11**) — own ImGui window + own `Harmony` in its `Patcher.cs`.
  2. **Embedded** in the **unscience** supermod as collapsible `ISubmod` sections sharing the
     single `Harmony("MeowSci.Unscience")` instance.
- Every top-level mod also applies shared `HotkeyGuard` (`ksa-abstractions.lib/HotkeyGuard.cs`,
  patches `GameSettings.OnKeyAll(GlfwKeyEvent):bool`) — catalogued elsewhere; one row per mod
  not repeated here.
- Vehicle/character enumeration is funneled through `ksa-abstractions.lib` (`VehicleProvider`
  → `Program.ControlledVehicle` / `Universe.CurrentSystem.All`; `CelestialProvider`), and
  part traversal through `PartHelpers.GetAllParts`.

**Summary of 4680 → 4750 risk**

| Mod | Verdict |
|---|---|
| **doh** | NO breaking deltas. Entire reflection + typed-API surface signature-identical; `MaterialData` byte-identical. Only additive change in scope: rev 4699 `KittenEva.IsControllable=>true`. |
| **humble-arteest** | **HIGH-RISK MOD.** *Kitten Color* and *Engine Emissive* are intact. *Vehicle Paint* shader-swap is **non-functional on both builds** (its GLSL anchor strings are absent from the shipped `MeshIndirect.vert/.frag` at 4680 *and* 4750); the **rev 4693 `DynamicMeshIndirect`→`MeshIndirect` merge** further diverged that shader (Temperature/TFI/Emissive folded behind `ENABLE_*` defines, varyings now occupy locations 5/6/7 that collide with the mod's intended 6/7/8). Deeper still, rev 4693 moved color-pipeline compilation to `ShaderReference.CompileVariantWithCustomOptions()`, which recompiles from disk and ignores the swapped `ShaderReference.Shader`. The C# Harmony write paths are unchanged. Now **self-guarded** (Phase 2): Vehicle Paint auto-disables with a UI notice and no longer clobbers `EmissiveColor`. |
| **kitten-animations** | NO breaking deltas. `CharacterAvatar.cs` + `CatExpressionAnim.cs` byte-identical; `AnimatedRenderable.cs` differs only by a trivial log line. |

**Key C# layout facts verified (load-bearing for the patches)**

- `KSA/PartModel.cs` — **byte-identical** OLD↔NEW (full-file diff). `PerInstanceData` = `float4x4 ModelMatrix`(0) · `int StateBitFlag`(64) · `uint EmissiveColor`(68) · `int packing1`(72) · `int packing2`(76).
- `KSA/PartModelDynamic.cs` — **byte-identical** OLD↔NEW. `PerInstanceData` = `float4x4 ModelMatrix`(0) · `int StateBitFlag`(64) · `float Temperature`(68) · `float TfiThickness`(72) · `int packing1`(76).
- `KSA/MaterialData.cs` — **byte-identical**, `[StructLayout(Sequential, Pack=1)]`: `int AlbedoTexture`(0) `int NormalTexture`(4) `int RoughMetallicAOTexture`(8) `int Sampler`(12) `float4 AlbedoColor`(**16**) `float4 RoughnessMetalScale`(32) `float4 ExtraData`(48) `int EmissiveTexture`(64).
- `KSA/CharacterAvatar.cs`, `KSA/CatExpressionAnim.cs`, `KSA/CatFurRenderable.cs`, `KSA/StaticMeshRenderable.cs`, `KSA/CharacterReference.cs`, `KSA/CharacterTexturesReference.cs`, `KSA/PbrMaterialReference.cs`, `KSA/GpuTextureSystem.cs`, `KSA/PartModelDynamicModule.cs` — all **byte-identical** OLD↔NEW.

---

## doh

**Purpose** — "Dynamically Originating Hominids": programmatically spawns `KittenEva` entities
near a vehicle (replicating `EVADoor.CreateKittenEva`), with optional per-kitten GPU material
cloning + `AlbedoColor` tint. Live recolor, batch spawn, "I'm Feeling Lucky" rainbow, despawn.

**Unscience integration** — `DohSubmod : ISubmod` (`doh.lib/DohSubmod.cs`). Spawning engine
`KittenSpawner` (`doh.lib/Spawning/`), material cloning `MaterialFactory` +
`MaterialSystemAccessor` + `KittenMaterialSet` (`doh.lib/Materials/`). Reaches the game via a
deep **reflection** bridge to `Program.Instance` render systems (no typed dependency on the
GPU material internals) plus a **typed** spawn path (`KittenEva`, `Orbit`, `Universe`, `Part`).

**UI / hotkeys** — Standalone **F8** window (`doh/Mod.cs:49`); embedded as `ISubmod` in unscience.
Vehicle/character filterable combos, offset/count, color picker + XKCD combo, per-kitten list.

**Persistence** — None. In-memory `SpawnedKittenRegistry` only; `DespawnAll()` + `MaterialFactory.Cleanup()` + `MaterialSystemAccessor.Cleanup()` on `Dispose`.

### Integration points

| # | Kind | Mod code (file:line) | Game target (Type.Member + sig / struct-offset) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Reflection (private) | MaterialSystemAccessor.cs:54,57 | `KSA.Program` type; `Program.Instance` static prop | KSA/Program.cs:371 | ✅ | none (was :370) | singleton root |
| 2 | Reflection | MaterialSystemAccessor.cs:64 | `Program.MaterialSystem : GpuMaterialSystem` (field) | KSA/Program.cs:94 | ✅ | none | |
| 3 | Reflection (hierarchy) | MaterialSystemAccessor.cs:68 | `AssetManager<T>.AssetMap` (protected `ConcurrentDictionary<AssetName,T>`) | KSA/AssetManager.cs:11 | ✅ | none | walks base types |
| 4 | Reflection | MaterialSystemAccessor.cs:72 | `GpuObjectSystem<T>.BigBuffer : BufferEx` (public get/protected set) | KSA/GpuObjectSystem.cs:18 | ✅ | none | |
| 5 | Reflection (hierarchy) | MaterialSystemAccessor.cs:76 | `GpuObjectSystem<T>.DeviceCtx : IVulkanContext` (protected field) | KSA/GpuObjectSystem.cs:16 | ✅ | none | |
| 6 | Reflection (hierarchy) | MaterialSystemAccessor.cs:79,124 | `GpuObjectSystem<T>.CreateObject(AssetName, T) : bool` | KSA/GpuObjectSystem.cs:45 | ✅ | none | `(AssetName)name, MaterialData` |
| 7 | Reflection (hierarchy) | MaterialSystemAccessor.cs:82,152 | `AssetManager<T>.GetOrLoad(AssetName) : T` | KSA/AssetManager.cs:49 | ✅ | none | returns `GpuObjectAssetRef` |
| 8 | Reflection | MaterialSystemAccessor.cs:155,184,250 | asset-ref `.Handle` (int) on `GpuObjectAssetRef` | KSA/GpuObjectAssetRef.cs | ✅ | none | map name→buffer index |
| 9 | Reflection | MaterialSystemAccessor.cs:85,88,91 | `Program.SuperMeshRenderSystem`; `.TextureSystem : GpuTextureSystem`; `GpuTextureSystem.GetOrLoad` | KSA/Program.cs:96; SuperMeshRenderSystem.cs:39 | ✅ | none | texture bindless lookup |
| 10 | GPU write (Vulkan) | MaterialSystemAccessor.cs:283-296; KittenMaterialSet.cs:19,84 | `BufferEx.VkBuffer`; `IVulkanContext.Device.CreateStagingPool`; `VkUtils.StageAndUploadToBuffer`; `ByteSize.Of<MaterialData>()`; `Marshal.OffsetOf<MaterialData>(AlbedoColor)`=16 | KSA/MaterialData.cs:17; Brutal.Vulkan | ✅ | none | writes `float4` at `handle*80+16` |
| 11 | Typed | MaterialFactory.cs:247-257 | `KSA.MaterialData` ctor fields (Albedo/Normal/RoughMetallicAO/Sampler/AlbedoColor/RoughnessMetalScale/Emissive/ExtraData) | KSA/MaterialData.cs:6-23 | ✅ | none | `Pack=1`, identical |
| 12 | Reflection | MaterialFactory.cs:219 | `ModLibrary.Get<PbrMaterialReference>(string)` | KSA/ModLibrary.cs:968 | ✅ | none (was :860) | |
| 13 | Typed | MaterialFactory.cs:382,390 | `ModLibrary.Get<CharacterReference>`; `CharacterReference.CharacterTextures : CharacterTexturesReference` | KSA/CharacterReference.cs:32 | ✅ | none (file identical) | |
| 14 | Reflection | MaterialFactory.cs:406-408 | `CharacterTexturesReference.{CharacterBodyMaterial,CharacterHeadMaterial,CharacterEyeMaterial} : PbrMaterialReference` | KSA/CharacterTexturesReference.cs:9,12,15 | ✅ | none (file identical) | |
| 15 | Reflection | MaterialFactory.cs:413-418,242-245 | `PbrMaterialReference.{DiffuseReference,NormalReference,PBRMap,EmissiveMap,Id}`; non-generic `.Get()` | KSA/PbrMaterialReference.cs:9-18 | ✅ | none (file identical) | `.BindlessHandle` off resolved `TextureReference` |
| 16 | Reflection | MaterialFactory.cs:504-525 | `Program.CharacterRenderSystem`; `CharacterRenderSystem._resources : CharacterRenderResources`; `.FurTexture/.CatFurMaskTexture` (`.BindlessHandle`), `.FurSampler` (`.BindlessIndex`) | KSA/CharacterRenderSystem.cs:7; CharacterRenderResources.cs:24-30 | ✅ | fields none; file diff is internal shader wiring only (see below) | fur `ExtraData` handles |
| 17 | Reflection | MaterialFactory.cs:541-577,592-593 | `GpuTextureSystem.{SamplerRepeatHandle,DefaultWhiteTexture,DefaultBlackTexture}`; `SuperMeshRenderSystem.GltfSystem`; `GltfPbrSystem.BlankMaterialTexture.BindlessHandle` | KSA/GpuTextureSystem.cs:26,32,34; GltfPbrSystem.cs:31 | ✅ | none (GpuTextureSystem.cs identical) | default-texture fallbacks |
| 18 | Reflection (internal field) | KittenSpawner.cs:322,329,333 | `ModLibrary.AllParts` (internal static `SerializedCollection<PartTemplate>`); `.Find(KeyHash) : PartTemplate` | KSA/ModLibrary.cs:86; SerializedCollection.cs:37 | ✅ | none (was :85) | `"KittenBackPackPart"` |
| 19 | Reflection (internal field) | KittenSpawner.cs:347,354,357 | `ModLibrary.AllCharacters` (internal static `SerializedCollection<CharacterReference>`); `.GetList() : List<T>` | KSA/ModLibrary.cs:90; SerializedCollection.cs:42 | ✅ | none (was :89) | character enumeration |
| 20 | Typed | KittenSpawner.cs:156 | `new KittenEva(CelestialSystem, string, doubleQuat, double3, IParentBody, string, Part, Orbit)` | KSA/KittenEva.cs:27 | ✅ | none (was :25) | 8-arg ctor identical |
| 21 | Typed (pattern) | KittenSpawner.cs:13-21 | mirrors `EVADoor.CreateKittenEva(Vehicle)` | KSA/EVADoor.cs:84 | ✅ | none (was :65; call shape identical) | |
| 22 | Typed | KittenSpawner.cs:278-289 | `new Part(id, PartTemplate)`; `Part.Tree.ReinitializeDerivedValues/RefillConsumables`; `Part.SubtreeModules.Get<Tank>()`; `Tank.ConfigureFor(IReactantMix)` | KSA/Tank.cs:382 | ✅ | none | backpack/propellant |
| 23 | Typed | KittenSpawner.cs:281 | `SubstanceLibrary.TryGetCombustionProcess(KeyHash)`; `KeyHash.Make` | KSA/SubstanceLibrary.cs:122 | ✅ | none | `"MMH_NTO_1.6"` |
| 24 | Typed | KittenSpawner.cs:169,258 | `Orbit.CreateFromStateCci(IParentBody, SimTime, double3, double3, byte4)`; `Orbit.OrbitLineColor` | KSA/Orbit.cs:1396 | ✅ | none | |
| 25 | Typed | KittenSpawner.cs:56,121,167 | `Universe.CurrentSystem : CelestialSystem?`; `Universe.GetElapsedSimTime() : SimTime` | KSA/Universe.cs:92,1991 | ✅ | none | |
| 26 | Typed | KittenSpawner.cs:231,239-242,230 | `Vehicle.GetAsmb2Cci()`; `.Body2Cce`; `.BodyRates`; `.Parent`; `.Orbit.StateVectors`(`.PositionCci/.VelocityCci`); `double3.Transform(doubleQuat)` | KSA/Vehicle.cs:2247,423,458 | ✅ | none | spawn positioning |
| 27 | Typed | KittenSpawner.cs:171,174,175 | `KittenEva.Teleport(Orbit?,doubleQuat?,double3?)`; `IParentBody.Children.Add`; `Vehicle.UpdatePerFrameData()` | KSA/Vehicle.cs:1594,1972 | ✅ | none | |
| 28 | Typed | KittenSpawner.cs:62,67,68 | `CelestialSystem.All.TryGet(string,out Astronomical)`; `CelestialSystem.Deregister(Vehicle)`; `Vehicle.Dispose()` | KSA/CelestialSystem.cs; Vehicle.cs | ✅ | none | despawn |
| 29 | Reflection | KittenSpawner.cs:506,513 | `KittenEva._renderable : KittenRenderable` → `._characterAvatar : CharacterAvatar` (both private) | KSA/KittenRenderable.cs:10 | ✅ | none | avatar root |
| 30 | Reflection (field path) | KittenSpawner.cs:388-408,523-537 | `CharacterAvatar.Core.CharacterModel.MaterialIndices`; `.Fur.CatFurRenderable.MaterialIndices`; `.Attachments.Helmet.HelmetMesh/.VisorMesh.MaterialIndices`; `.Attachments.Mmu.MmuMesh.MaterialIndices` | KSA/CharacterAvatar.cs (identical); AnimatedRenderable.cs:33; CatFurRenderable.cs:22; StaticMeshRenderable.cs:31 | ✅ | none | `MaterialIndices` is `protected int[]` on each renderable; in-place handle swap |
| 31 | Typed (context) | — (not referenced) | `KittenEva.IsControllable => true` / `Vehicle.IsControllable` (virtual) | KSA/KittenEva.cs:15; Vehicle.cs:526 | ✅ | **ADDED in NEW (rev 4699)** — absent in OLD | informational: spawned kittens now controllable; not a break |

### Game assets referenced

- **Characters** by id from `ModLibrary.AllCharacters` (e.g. `"Calico"`, user-selectable / random). No hardcoded character id.
- **Part template** `"KittenBackPackPart"` (`KittenSpawner.cs:275`).
- **Combustion process** `"MMH_NTO_1.6"` (`KittenSpawner.cs:281`).
- **Fur texture** `"FurNoise"` reached indirectly via `CharacterRenderResources.FurTexture` (game loads it; mod only reads `.BindlessHandle`).
- No shader files referenced directly. Tint takes effect through `ModelPbr.frag` → `MaterialSet.glsl` (`albedo = mat.albedoColor * texture(...)`, **identical** both builds).

### Update-risk findings (4680 → 4750)

- **No breaking deltas detected.** Every reflected member, internal field, and typed API is present in 4750 with identical signatures; only source line numbers shifted.
- `MaterialData` (the GPU write target) is byte-identical (`AlbedoColor` @ offset 16) — the staged Vulkan write remains correct.
- `CharacterRenderResources.cs` **does** differ between builds, but only in **internal shader wiring**: `using Brutal.ShaderCompilerApi` → `Brutal.ShaderCApi`, and the eye/glass technique now compiles `ModelTranslucentFrag` (with `CreateEyeCompileOptions`) instead of `ModelEyeFrag`/`ModelGlassFrag` (rev 4745 ModelGlass+ModelEye merge). The fur/eye **fields** doh reflects (`FurTexture`, `CatFurMaskTexture`, `FurSampler`, `EyeRenderer`) are unchanged → no impact on doh.
- rev 4699 added `KittenEva.IsControllable => true`; doh does not reference it, so additive only (spawned kittens gain controllability).

---

## humble-arteest

**Purpose** — Three independent visual-customization features matched to KSA's three rendering
data paths: **(A) Vehicle Paint** (per-part RGB tint via runtime GLSL recompile + per-instance
padding hijack), **(B) Kitten Color** (`AlbedoColor` writes to the GPU material buffer), **(C)
Engine Emissive** (Harmony override of the per-instance `Temperature`/`TfiThickness` engines glow).

**Unscience integration** — Three `ISubmod`s (`VehiclePaintSubmod`, `KittenColorSubmod`,
`EngineEmissiveSubmod`). Two Harmony prefixes applied through the shared instance:
`VehiclePaintPatches.Apply` (on `PartModel.AddInstance`) and `EngineEmissivePatches.Apply`
(on `PartModelDynamic.AddInstance`). `VehiclePaint.Cleanup()` + `EngineEmissive.Cleanup()` on
unload. Kitten Color has **no** Harmony patch (pure GPU buffer write, same bridge as doh).

**UI / hotkeys** — Standalone **F11** window (`humble-arteest/Mod.cs:66`); embedded in unscience.

**Persistence** — None. In-memory dictionaries keyed by `PartModel` / `PartModelDynamic`;
global toggles; all cleared on unload.

### Integration points

| # | Kind | Mod code (file:line) | Game target (Type.Member + sig / struct-offset / shader path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| A1 | Harmony PREFIX | VehiclePaintPatches.cs:36,46,65 | `PartModel.AddInstance(PerInstanceData instanceData, Viewport, int) : void` (prefix takes `ref PerInstanceData instanceData`) | KSA/PartModel.cs:375 | ✅ | **none** (PartModel.cs byte-identical) | Harmony binds by param name `instanceData`; attaches cleanly |
| A2 | Struct reinterpret (`Unsafe.As`) | VehiclePaintPatches.cs:25-32,70-73 | `PartModel.PerInstanceData` — writes `float` at **offset 68/72/76** | KSA/PartModel.cs:299-310 | ✅ | **none** (identical) | ⚠ **offset 68 = `uint EmissiveColor` (game-USED), not padding.** Mod's `PaintR` overwrites EmissiveColor; `PaintG`/`PaintB` (72/76) land in real `packing1/2`. Pre-existing data hazard, *unchanged* by update |
| A3 | Reflection | VehiclePaint.cs:158,159,187 | `ModLibrary.Get<ShaderReference>("MeshIndirectVert" / "MeshIndirectFrag")` | KSA/PartModelRenderer.cs:108-109,170-171 | ✅ | none | shader IDs still resolve |
| A4 | Reflection | VehiclePaint.cs:225,241-259,150,307-316 | `ShaderReference.Shader : VkShaderModule?` (private set); `.LocalPath`; `.ModPath`; `DoLoad()` (internal) | KSA/ShaderReference.cs:33,167 | ✅ | none | swap via setter / backing field |
| A5 | Reflection | VehiclePaint.cs:214,328-352 | `RenderCore.ShaderModuleUtils.FromFile(Device, string, out VkShaderStageFlags, …)` | RenderCore/ShaderModuleUtils.cs | ✅ | none | runtime GLSL→SPIR-V |
| A6 | Typed | VehiclePaint.cs:126,161 | `PartModelRenderer.ColorData.Rebuild() : void` (static) | KSA/PartModelRenderer.cs:228 | ✅ | none (was :202) | pipeline rebuild |
| A7 | Typed | VehiclePaint.cs:118 | `Program.GetRenderer() : Renderer` → `.Device` | KSA/Program.cs:450 | ✅ | none | Vulkan device |
| A8 | **Shader text edit** | VehiclePaint.cs:265-285 (`ModifyVertexShader` anchors :270,275,280) | `MeshIndirect.vert` — anchors `"    int Highlighted;\n};"`, `"layout(location = 5) out flat int outHighlighted;"`, `"outHighlighted = instanceData.Highlighted;"` | Content/Core/Shaders/Mesh/MeshIndirect.vert | ⚠ shader exists, **anchors DO NOT MATCH** | **DIVERGED** (4693 merge) | 🔴 **see findings.** None of the 3 anchors exist in the 4680 *or* 4750 shipped vert → `ActivateShaders()` returns false → **paint never renders** |
| A9 | **Shader text edit** | VehiclePaint.cs:288-303 (`ModifyFragmentShader` anchors :293,298) | `MeshIndirect.frag` — anchor `"layout (location = 5) in flat int inHighlighted;"` (**no match**), and `"vec3 sampledColor = gammaToLinear(texture(...drawData.diffuseTextureIndex...).xyz);"` (**matches both**) | Content/Core/Shaders/Mesh/MeshIndirect.frag | ⚠ partial | DIVERGED | 🔴 input-decl anchor absent → would yield undeclared `inPaintR/G/B`; moot (vert fails first) |
| B1 | Reflection | KittenColor.cs:56-74 | `Program.Instance`→`MaterialSystem`→`AssetMap`/`BigBuffer`/`DeviceCtx` (same chain as doh #1-5) | KSA/Program.cs:94; GpuObjectSystem.cs:16,18; AssetManager.cs:11 | ✅ | none | |
| B2 | GPU write (Vulkan) | KittenColor.cs:192-216 | `BigBuffer.VkBuffer` + `VkUtils.StageAndUploadToBuffer` at `handle*ByteSize.Of<MaterialData>() + OffsetOf(AlbedoColor=16)` | KSA/MaterialData.cs:17 | ✅ | none | tints fur/body/eyes |
| B3 | Shader path (read-only) | (effect) KittenColor.cs concept | `ModelPbr.frag` → `MaterialSet.glsl`: `albedo = mat.albedoColor * texture(...)`; alpha `discard` | Content/Core/Shaders/Mesh/ModelPbr.frag; Common/MaterialSet.glsl | ✅ | MaterialSet.glsl **identical**; ModelPbr.frag differs only in SSAO ordering (rev 4671) | tint path intact |
| C1 | Harmony PREFIX | EngineEmissivePatches.cs:40,51,70 | `PartModelDynamic.AddInstance(PerInstanceData inInstanceData, Viewport, int) : void` (prefix `ref … inInstanceData`) | KSA/PartModelDynamic.cs:379 | ✅ | **none** (PartModelDynamic.cs byte-identical) | param name `inInstanceData` matches |
| C2 | Struct reinterpret (`Unsafe.As`) | EngineEmissivePatches.cs:29-36,77-80 | `PartModelDynamic.PerInstanceData` — writes `Temperature`@**68**, `TfiThickness`@**72** | KSA/PartModelDynamic.cs:309-319 | ✅ | **none** (identical) | ✅ mirror struct matches **exactly** |
| C3 | Typed | EngineEmissive.cs:123,129,159 | `Part.Modules.Get<PartModelDynamicModule>()`; `PartModelDynamicModule.PartModelDynamic` (`required`) | KSA/PartModelDynamicModule.cs:32 | ✅ | none (file identical) | engine discovery via `PartHelpers.GetAllParts` |
| C4 | Shader path (read-only) | (effect) — no mod edit | Temperature→emissive LUT logic, formerly `DynamicMeshIndirect.frag`, now `MeshIndirect.frag` under `#ifdef ENABLE_TEMPERATURE` | Content/Core/Shaders/Mesh/MeshIndirect.frag:214-219 (vert:18-20,71-73) | ✅ | **MOVED** (4693): `DynamicMeshIndirect.frag/.vert` files **removed**; dynamic pipeline now compiles `MeshIndirectVert/Frag` with `ENABLE_TEMPERATURE` (PartModelRenderer.cs:170-171, was DynamicMeshIndirect* :156-157) | ✅ feature still works — game still reads `PerInstanceData.Temperature` |

### Game assets referenced

- **Shaders by `ModLibrary` id:** `"MeshIndirectVert"` / `"MeshIndirectFrag"` → `Content/Core/Shaders/Mesh/MeshIndirect.vert` / `.frag` (read from disk via `ShaderReference.ModPath`/`LocalPath`, edited in-memory, recompiled). **Both files exist** in 4750.
- **GPU material buffer** (`GpuMaterialSystem.BigBuffer`) for Kitten Color (no asset path).
- **Removed assets the mod's design assumed:** `DynamicMeshIndirect.vert/.frag` (gone, 4693), `ModelEye.frag`, `ModelGlass.frag` (gone, 4745). `ModelTranslucent.frag` is **new** (4747). humble-arteest does not recompile these by id, so removal is not a hard reference break — but the README's narrative around `DynamicMeshIndirect.frag` is now stale.

### Update-risk findings (4680 → 4750)

- 🔴 **Vehicle Paint shader-swap is non-functional on both builds (highest-priority finding).**
  Verified by reading `MeshIndirect.vert`/`.frag` in *both* Content trees: the three
  `ModifyVertexShader` anchor strings are **absent at 4680 and 4750**. The shipped `InstanceData`
  struct already carries `uint EmissiveColor` after `int Highlighted` at **4680**; at **4750** the
  **rev 4693 merge** further changed it to `int Highlighted;` followed by `#ifdef ENABLE_EMISSIVE
  uint EmissiveColor #endif` + `#ifdef ENABLE_TEMPERATURE float Temperature` + `#ifdef
  ENABLE_THIN_FILM float TfiThickness`. The varying that was `outHighlighted` no longer exists
  (state goes through `outStateFlags`@loc4 / `outEmissiveColor`@loc5). Result: `ModifyVertexShader`
  returns the source unchanged → `CompileAndSwapShader` aborts with *"Modification had no effect on
  MeshIndirectVert"* → no paint is rendered. (The C# `PartModel.AddInstance` write at A2 still runs.)
- 🔴 **A fix is now harder than a string update.** Post-4693, `MeshIndirect.vert`/`.frag` already
  use out/in varyings at **locations 5/6/7** (`outEmissiveColor`/`outTfiThickness`/`outTemperature`),
  which **collide** with humble-arteest's intended paint locations **6/7/8**. A correct fix must
  target the new `#ifdef`-gated struct, pick non-colliding varying locations, and reconcile with
  the merged dynamic/static pipeline. Also reconsider A2: writing `PaintR` into offset 68 clobbers
  `EmissiveColor`.
- 🔴 **Root cause is architectural, not just the anchors (verified in `PartModelRenderer.cs` +
  `ShaderReference.cs`, NEW).** `PartModelRenderer.ColorData.BuildPipelineModel`/`BuildPipelineDynamic`
  (NEW `:104-164` / `:166-226`) compile MeshIndirect via
  `ShaderReference.CompileVariantWithCustomOptions(options)` (NEW `ShaderReference.cs:119`) with macro
  variants (`ENABLE_EMISSIVE`+`ENABLE_THIN_FILM` for static parts; `ENABLE_TEMPERATURE`+`ENABLE_THIN_FILM`
  for dynamic) and **destroy the module immediately** after pipeline creation. That method reads GLSL
  fresh from `base.ModPath` (disk) and **never consults `ShaderReference.Shader`**. So the mod's "modify
  in memory → swap `ShaderReference.Shader` → `ColorData.Rebuild()`" sequence is inert even with correct
  anchors — `Rebuild()` recompiles from the unmodified disk file. (`PrePassData.BuildPipelineModel` still
  uses `.Shader` for the *depth* prepass, but that pass does no color/paint.) A real fix must
  Harmony-patch `CompileVariantWithCustomOptions` (or the pipeline builders) for `MeshIndirectVert/Frag`
  — touching the rendering of *every* part — and be GPU-validated.
- ✅ **GUARDED (Phase 2).** `VehiclePaint.IsSupported` (`VehiclePaint.cs`) probes the on-disk
  `MeshIndirect.vert` for the feature-gating / missing anchors and disables the feature with a clear UI
  notice (`VehiclePaintSubmod.RenderBody`); `ActivateShaders()` short-circuits; and
  `VehiclePaintPatches.AddInstancePrefix` now early-outs unless `ShadersActive`, so the `EmissiveColor`@68
  clobber (A2) can no longer occur. Engine Emissive / Kitten Color untouched.
- ✅ **Vehicle Paint Harmony plumbing is intact:** `PartModel.cs` is byte-identical OLD↔NEW;
  `AddInstance` signature and `PerInstanceData` layout unchanged; all shader-swap reflection
  targets (`ShaderReference.Shader/DoLoad/ModPath`, `ShaderModuleUtils.FromFile`,
  `PartModelRenderer.ColorData.Rebuild`, `Program.GetRenderer`, the two shader ids) resolve.
- ✅ **Engine Emissive intact:** `PartModelDynamic.cs` + `PartModelDynamicModule.cs` byte-identical;
  mirror struct (`Temperature`@68, `TfiThickness`@72) exact; the Temperature→emissive path survived
  the 4693 merge (now inside `MeshIndirect.frag` under `ENABLE_TEMPERATURE`), so no shader edit
  needed and the feature still works.
- ✅ **Kitten Color intact:** `MaterialData` byte-identical (`AlbedoColor`@16); `MaterialSet.glsl`
  identical; `ModelPbr.frag` changed only in SSAO ordering (rev 4671). Note rev 4745 routes
  eye/glass through `ModelTranslucentFrag`; the buffer-write mechanism is unaffected, though
  glass/eye tint appearance may shift since those materials render via a different (merged) shader.

---

## kitten-animations

**Purpose** — Drives the controlled kitten's `CharacterAvatar`: MMU body animations, walking/running,
and five facial expressions (Angry/Awe/Happy/Sad/Scared) with a 250 ms quadratic ease-in.

**Unscience integration** — `KittenAnimationsSubmod : ISubmod`
(`kitten-animations.lib/KittenAnimationsSubmod.cs`) owns a `KittenAnimationController`;
`Update(dt)` (from `[StarMapBeforeGui]`) eases expression weight each frame. Avatar reached via
`KittenAvatarAccessor` (reflection). No Harmony patches of its own (only `PatchAll` no-op +
`HotkeyGuard`).

**UI / hotkeys** — Standalone **F11** window (`kitten-animations/Mod.cs:51`); embedded in unscience.
Expression buttons, MMU-animation grid, walking buttons, duration slider.

**Persistence** — None.

### Integration points

| # | Kind | Mod code (file:line) | Game target (Type.Member + sig) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Typed (via abstraction) | KittenAvatarAccessor.cs:11 | `VehicleProvider.GetControlledVehicle()` → `Program.ControlledVehicle : Vehicle?`; pattern-match `is KittenEva` | KSA/Program.cs:254 | ✅ | none | rev 4699 makes a controlled KittenEva more reachable (beneficial) |
| 2 | Reflection (private) | KittenAvatarAccessor.cs:24-25 | `KittenEva._renderable : KittenRenderable` → `._characterAvatar : CharacterAvatar` | KSA/KittenRenderable.cs:10 | ✅ | none | same field path doh uses |
| 3 | Typed | KittenAnimationController.cs:79; Submod:127-149 | `CharacterAvatar.Core.CharacterModel : AnimatedRenderable`; `.SetAnimation(IAnimation, blend=0.2f)` | KSA/CharacterAvatar.cs:204,31; AnimatedRenderable.cs:97 | ✅ | none (CharacterAvatar identical; AnimatedRenderable diff = 1 log line) | body/MMU/walk playback |
| 4 | Typed | KittenAnimationController.cs:115 | `AnimatedRenderable.AnimProcessors : List<IAnimProcessor>`; `.OfType<CatExpressionAnim>().LastOrDefault()` | KSA/AnimatedRenderable.cs:41 | ✅ | none | locate expression processor |
| 5 | Typed | KittenAnimationController.cs:48,98,100 | `CatExpressionAnim : CatPostAnim`; `.ExpressionAnim : AnimationAssetRef?`; `.ExpressionWeight : float` | KSA/CatExpressionAnim.cs:8,14,12 | ✅ | none (file identical) | apply + ease expression |
| 6 | Reflection (private, cached `FieldInfo`) | KittenAnimationController.cs:14-16,120-128 | `CatExpressionAnim._expressionPose : TransformTRS[]?` (set null to bust the sampled-pose cache) | KSA/CatExpressionAnim.cs:16 | ✅ | none (file identical) | so each expression re-samples; cache logic at CatExpressionAnim.cs:42-47 |
| 7 | Typed | Submod:75-102 | `CharacterAvatar.Expressions.{Angry,Awe,Happy,Sad,Scared} : List<AnimationAssetRef>?` | KSA/CharacterAvatar.cs:187-195 | ✅ | none | random pick per click |
| 8 | Typed | Submod:127-149 | `CharacterAvatar.Animations.MmuAnimations.{MmuIdleDefaultAnim, MmuMoveLeft/Right/Forward/Backward/Up/DownLoopAnim} : AnimationAssetRef?` | KSA/CharacterAvatar.cs:155-172 | ✅ | none | |
| 9 | Typed | Submod:174,180 | `CharacterAvatar.Animations.WalkingAnimations.{RunningAnim, WalkingAnim} : AnimationAssetRef?` | KSA/CharacterAvatar.cs:174-179 | ✅ | none | |

### Game assets referenced

- **None by string/path.** All animations are typed `AnimationAssetRef` fields on the live
  `CharacterAvatar` (populated by the game per character); the mod selects from them, it does not
  load assets by id. No shader, material, or character-id references.

### Update-risk findings (4680 → 4750)

- **No breaking deltas detected.** `CharacterAvatar.cs` and `CatExpressionAnim.cs` are
  **byte-identical** OLD↔NEW, so every expression list, MMU/walking animation field, the
  `ExpressionAnim`/`ExpressionWeight` members, and the private `_expressionPose` cache field used
  for reflection are unchanged.
- `AnimatedRenderable.cs` differs only by one trivial logging line (`GltfAssetRef.Id.ToString()`
  vs `.ToString().AsSpan()`); `SetAnimation`, `AnimProcessors`, `MaterialIndices` are unaffected.
- rev 4699 (`KittenEva.IsControllable => true`) is additive and *helps* `GetControlledVehicle()`
  return a `KittenEva`; not a break.
