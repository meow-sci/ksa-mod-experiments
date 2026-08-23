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
| **humble-arteest** | *Kitten Color* and *Engine Emissive* intact. *Vehicle Paint* was **rebuilt for 5018** (2026-07-25) and works again: the dead shader-swap was replaced by a `ShaderModuleUtils.FromFile` prefix that compiles a patched `MeshIndirect(.Raytraced).frag` in memory, with the color carried in the free `StateBitFlag` bits 11..31. It no longer touches `MeshIndirect.vert` and no longer clobbers `EmissiveColor`/`Temperature`/`TfiThickness`/`Wetness`. Remaining game-coupling to watch: those free state-flag bits, and the `vec3 sampledColor` anchor in the two fragment shaders. |
| **kitten-animations** | NO breaking deltas. `CharacterAvatar.cs` + `CatExpressionAnim.cs` byte-identical; `AnimatedRenderable.cs` differs only by a trivial log line. |

**Key C# layout facts verified (load-bearing for the patches)**

- `KSA/PartModel.cs` @5018 — `PerInstanceData` (80 B) = `float4x4 ModelMatrix`(0) · `int StateBitFlag`(64) · `uint EmissiveColor`(68) · `int packing1`(72, read as `TfiThickness` by the shader) · `float Wetness`(76).
- `KSA/PartModelDynamic.cs` @5018 — `PerInstanceData` (80 B) = `float4x4 ModelMatrix`(0) · `int StateBitFlag`(64) · `float Temperature`(68) · `float TfiThickness`(72) · `float Wetness`(76).
- **`StateBitFlag` bit map** (identical in the static, dynamic and glass structs): game uses bits **0..10** (0 highlighted · 1 grabbed · 2 fake-translucent · 3 selected · 4 edited-vehicle/no-celestial-shadow · 5 IVA/no-planet-shine · 6 no-emissive · 7 add-emissive-color · 8 selected-connected · 9 selected-disconnected · 10 fuel-flow highlight). **Bits 11..31 are free** and are what humble-arteest's Vehicle Paint uses.
- **std430 stride is exactly 80 B in every variant** — the maximal enabled combination (static: `EMISSIVE`+`THIN_FILM`+`WETNESS`; dynamic: `TEMPERATURE`+`THIN_FILM`+`WETNESS`) fills it precisely, so there is **no spare trailing space** to append a field to. Any future per-instance mod data must reuse free bits, not new fields.
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
| 1 | Reflection (private) | MaterialSystemAccessor.cs:53,56 | `KSA.Program` type; `Program.Instance` static prop | KSA/Program.cs:371 | ✅ | none (was :370) | singleton root |
| 2 | Reflection | MaterialSystemAccessor.cs:63 | `Program.MaterialSystem : GpuMaterialSystem` (field) | KSA/Program.cs:94 | ✅ | none | |
| 3 | Reflection (hierarchy) | MaterialSystemAccessor.cs:67 | `AssetManager<T>.AssetMap` (protected `ConcurrentDictionary<AssetName,T>`) | KSA/AssetManager.cs:11 | ✅ | none | walks base types |
| 4 | Reflection | MaterialSystemAccessor.cs:71 | `GpuObjectSystem<T>.BigBuffer : BufferEx` (public get/protected set) | KSA/GpuObjectSystem.cs:18 | ✅ | none | |
| 5 | Reflection (hierarchy) | MaterialSystemAccessor.cs:75 | `GpuObjectSystem<T>.DeviceCtx : IVulkanContext` (protected field) | KSA/GpuObjectSystem.cs:16 | ✅ | none | |
| 6 | Reflection (hierarchy) | MaterialSystemAccessor.cs:78,123 | `GpuObjectSystem<T>.CreateObject(AssetName, T) : bool` | KSA/GpuObjectSystem.cs:45 | ✅ | none | `(AssetName)name, MaterialData` |
| 7 | Reflection (hierarchy) | MaterialSystemAccessor.cs:81,151 | `AssetManager<T>.GetOrLoad(AssetName) : T` | KSA/AssetManager.cs:49 | ✅ | none | returns `GpuObjectAssetRef` |
| 8 | Reflection | MaterialSystemAccessor.cs:154,183,249 | asset-ref `.Handle` (int) on `GpuObjectAssetRef` | KSA/GpuObjectAssetRef.cs | ✅ | none | map name→buffer index |
| 9 | Reflection | MaterialSystemAccessor.cs:84,87,90 | `Program.SuperMeshRenderSystem`; `.TextureSystem : GpuTextureSystem`; `GpuTextureSystem.GetOrLoad` | KSA/Program.cs:96; SuperMeshRenderSystem.cs:39 | ✅ | none | texture bindless lookup |
| 10 | GPU write (Vulkan) | MaterialSystemAccessor.cs:282-295; KittenMaterialSet.cs:19,84 | `BufferEx.VkBuffer`; `IVulkanContext.Device.CreateStagingPool`; `VkUtils.StageAndUploadToBuffer`; `ByteSize.Of<MaterialData>()`; `Marshal.OffsetOf<MaterialData>(AlbedoColor)`=16 | KSA/MaterialData.cs:17; Brutal.Vulkan | ✅ | none | writes `float4` at `handle*80+16`; span→bytes via BCL `MemoryMarshal.AsBytes` (no `CommunityToolkit.HighPerformance` reference) |
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
- **Reaction** `"MMH_NTO"` (`KittenSpawner.cs:281` → `TryGetReactantMix`), defined in
  `Core/Reactions.xml` as `<MixtureReaction Id="MMH_NTO">` with `DefaultMixtureRatio` 1.65.
  Was the combustion process `"MMH_NTO_1.6"` before 5018 — the mixture ratio is no longer part of
  the id, so the old id resolves to nothing.
- **Fur texture** `"FurNoise"` reached indirectly via `CharacterRenderResources.FurTexture` (game loads it; mod only reads `.BindlessHandle`).
- No shader files referenced directly. Tint takes effect through `ModelPbr.frag` → `MaterialSet.glsl` (`albedo = mat.albedoColor * texture(...)`, **identical** both builds).

### Update-risk findings (5117 → 5261)

- **CONFIRMED COMPILE BREAK (rev 5211) — doh.** `doh.lib/Spawning/KittenSpawner.cs:167,257` called
  `Universe.GetElapsedSimTime()`, renamed to `Universe.GetElapsedTime()` when `SimTime` became
  `UniverseTime` (`Int128` nanoseconds). Both sites feed the value straight into
  `Orbit.CreateFromStateCci(parent, <time>, pos, vel, colour)`, whose parameter followed the same
  rename, so the fix is the method name only — **no precision or arithmetic handling changed**.
- ✅ **The whole avatar reflection chain still resolves** — `KittenEva` (compared by type-name string),
  `KittenEva._renderable`, `KittenRenderable._characterAvatar`, `CharacterAvatar.Core` (still a
  **value-type field**, which garrys-torch's `SetValue` depends on), `CharacterCore.Scale`, and
  `CatExpressionAnim._expressionPose` (still declared on `CatExpressionAnim` itself, byte-identical
  OLD↔NEW).
- ✅ **`KittenEva` changes are purely additive** — rev 5179 added `KittenControlMode` (View/Direct),
  revs 5203/5233/5249 added ladder grab/board and jump/tumble/landing state. New members only
  (`LadderHost`, `HasGrabCandidate`, `AnimPlaybackRate`, `AnimJumpChainStage`, `SetControlMode`, …);
  **nothing doh, garrys-torch or kitten-animations reads was removed.**
- ✅ **GPU layouts identical** — `MaterialData` is byte-identical, so doh's staged `BigBuffer` writes
  at `handle*80+16` (`AlbedoColor` at offset 16, stride 80) still land correctly; `PerInstanceData`
  is byte-identical, so humble-arteest's padding-byte hijack is safe. The render bridge
  (`Program.MaterialSystem`/`SuperMeshRenderSystem`/`CharacterRenderSystem`,
  `GpuObjectSystem.{BigBuffer,DeviceCtx,CreateObject}`, `AssetManager.AssetMap`) all still resolves.
- ✅ **humble-arteest Engine Emissive unaffected.** `Content/Core/Shaders/Mesh/MeshIndirect.frag`
  changed by **exactly one line** — rev 5196 added
  `lightColor += SamplePortraitLight(inWorldPosition, N, sampledColor, metallic);` for IVA portrait
  lights. The mod's `vec3 sampledColor …;` anchor (line 114) and its `inStateFlags` guard both still
  match, and the `ENABLE_TEMPERATURE` LUT still lives in that file.
  `MeshIndirect.vert` is **byte-identical** (mesh-deform's anchor target).
- ❌ **humble-arteest Vehicle Paint / mesh-deform remain dead by design** (rev 4693
  `CompileVariantWithCustomOptions` recompiles from disk and ignores `ShaderReference.Shader`). Both
  self-detect and disable. `ShaderReference.{Shader,DoLoad,ModPath,LocalPath}` and
  `RenderCore.ShaderModuleUtils.FromFile` all still resolve, so the probes still work.
- ⚠️ **Behavioral watch items (need a live pass):** rev 5230 *"Fixed Fur not rendering after a bug was
  introduced when setting up lights for the kitten cam"* — re-check doh's fur/attachment
  `MaterialIndices` path. Revs 5203/5233/5235/5244/5249 add ladder and jump/tumble anim states, the
  prime suspect area for the **"kitten animations always the same expression"** entry in
  [`../ISSUES.md`](../ISSUES.md). Rev 5193 added kitten cameras + portrait UI, and rev 5198 fixed
  drawn kittens leaking into the editor — both touch doh's spawn/render assumptions.

### Update-risk findings (4750 → 5018)

- **BREAKING (fixed):** the combustion model was replaced by a reaction model.
  `SubstanceLibrary.TryGetCombustionProcess` is gone, along with `CombustionObject`,
  `CombustionProcess`, `CombustionProcessTemplate` and `CombustionTable`. The replacements are
  `SubstanceLibrary.TryGetReaction(KeyHash) → Reaction`, with `FixedReaction : Reaction, IReactantMix`
  carrying a `ReactantMix` directly and `MixtureReaction : Reaction` exposing
  `AtMixtureRatio(float) → FixedReaction` plus `DefaultMixtureRatio`. `Tank.ConfigureFor` now takes a
  `ReactantMix` (`ConfigureFor(ReactantMix, bool recreateResourceManagers = true)`).
  `KittenSpawner.CreateBackpackPart` was rewritten accordingly (`TryGetReactantMix` helper).
- **Everything else clean.** `CharacterAvatar.cs`, `GpuMaterialSystem.cs`, `KittenEva.cs`,
  `KittenRenderable.cs` and `CharacterRenderSystem.cs` are **unchanged** 4750→5018.
- `MaterialData` (the GPU write target) is byte-identical (`AlbedoColor` @ offset 16, stride 80) — the
  staged Vulkan write remains correct. `ModelPbr.frag` and `Common/MaterialSet.glsl` are also
  byte-identical, so Kitten Color is unaffected.
- `CharacterAvatar.CharacterCore` is still a **struct** with `public float Scale` — garrys-torch's
  boxed `SetValue` write-back pattern still works.

#### Carried over from the 4680 → 4750 review
- `CharacterRenderResources.cs` **does** differ between builds, but only in **internal shader wiring**: `using Brutal.ShaderCompilerApi` → `Brutal.ShaderCApi`, and the eye/glass technique now compiles `ModelTranslucentFrag` (with `CreateEyeCompileOptions`) instead of `ModelEyeFrag`/`ModelGlassFrag` (rev 4745 ModelGlass+ModelEye merge). The fur/eye **fields** doh reflects (`FurTexture`, `CatFurMaskTexture`, `FurSampler`, `EyeRenderer`) are unchanged → no impact on doh.
- rev 4699 added `KittenEva.IsControllable => true`; doh does not reference it, so additive only (spawned kittens gain controllability).

---

## humble-arteest

**Purpose** — Three independent visual-customization features matched to KSA's three rendering
data paths: **(A) Vehicle Paint** (per-part-instance albedo tint — color packed into the free high
bits of `PerInstanceData.StateBitFlag`, applied by a runtime-patched copy of the part fragment
shaders), **(B) Kitten Color** (`AlbedoColor` writes to the GPU material buffer), **(C)
Engine Emissive** (Harmony override of the per-instance `Temperature`/`TfiThickness` engines glow).

**Unscience integration** — Three `ISubmod`s (`VehiclePaintSubmod`, `KittenColorSubmod`,
`EngineEmissiveSubmod`). Harmony patches applied through the shared instance:
`VehiclePaintPatches.Apply` (five seams — see A1–A6) and `EngineEmissivePatches.Apply`
(on `PartModelDynamic.AddInstance`). `VehiclePaint.Cleanup()` + `EngineEmissive.Cleanup()` on
unload. Kitten Color has **no** Harmony patch (pure GPU buffer write, same bridge as doh).

**UI / hotkeys** — Standalone **F11** window (`humble-arteest/Mod.cs:66`); embedded in unscience.

**Persistence** — None. In-memory dictionaries keyed by `Part` (per-instance) and part template id;
global toggle + color; all cleared on unload.

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

| # | Kind | Mod code (file:line) | Game target (Type.Member + sig / struct-offset / shader path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| A1 | Harmony PREFIX | VehiclePaintPatches.cs `ResolveFromFile`/`FromFilePrefix` | `RenderCore.ShaderModuleUtils.FromFile(Device, string filePath, out VkShaderStageFlags shaderStage, CompileOptions? options) : VkShaderModule` (**static**) | RenderCore/ShaderModuleUtils.cs:115 | ✅ | n/a (new seam) | The only interception point that works ≥4693: part pipelines recompile per variant straight from disk. Prefix returns `true` (stock behavior) for every non-target path and on any error |
| A2 | Typed call | VehiclePaintPatches.cs `FromFilePrefix` | `ShaderModuleUtils.FromString(Device, ReadOnlySpan<byte>, VkShaderStageFlags, CompileOptions?, ReadOnlySpan<byte> debugName)`; `ShaderStageFromFileExtension(string)` | RenderCore/ShaderModuleUtils.cs:77,198 | ✅ | n/a (new) | `debugName` = the original file path (NUL-terminated) so relative `#include`s resolve exactly as stock; `options` passed through unmodified |
| A3 | Harmony PREFIX | VehiclePaintPatches.cs `PartModelModulePrefix` | `PartModelModule.UpdateRenderData(in double4x4, bool, Viewport, int) : void`; reads `Module<T>.Parent : Part` | KSA/PartModelModule.cs:79; KSA/Module.cs:419 | ✅ | none | Records which `Part` is about to submit; it is the **only** caller of `PartModel.AddInstance` |
| A4 | Harmony PREFIX | VehiclePaintPatches.cs `PartModelDynamicModulePrefix` | `PartModelDynamicModule.UpdateRenderData(in double4x4, bool, Viewport, int) : void` | KSA/PartModelDynamicModule.cs:55 | ✅ | none | Same hand-off for dynamic parts; only caller of `PartModelDynamic.AddInstance` |
| A5 | Harmony PREFIX | VehiclePaintPatches.cs `AddInstancePrefix` | `PartModel.AddInstance(PerInstanceData instanceData, Viewport, int) : void` — ORs paint into `instanceData.StateBitFlag` | KSA/PartModel.cs:375 (struct :299-310) | ✅ | none | Binds by param name `instanceData`; **no** `Unsafe.As` mirror struct any more — writes the public field directly |
| A6 | Harmony PREFIX | VehiclePaintPatches.cs `AddInstanceDynamicPrefix` | `PartModelDynamic.AddInstance(PerInstanceData inInstanceData, Viewport, int) : void` | KSA/PartModelDynamic.cs:379 (struct :309-320) | ✅ | none | Param name `inInstanceData` |
| A7 | Typed | VehiclePaintShaders.cs `RequestRendererRebuild` | `Program.RendererRebuildNeeded : bool` (public static) | KSA/Program.cs:383 (consumed at :2080 `PrepareFrame`) | ✅ | n/a (new) | The game's own deferred-rebuild flag — the same path a Frost/Water graphics-setting change takes, so pipelines are destroyed at a frame boundary, not mid-record |
| A8 | Typed | VehiclePaintShaders.cs `TryResolveShaderPath` | `ModLibrary.Get<ShaderReference>("MeshIndirectFrag")` → `FileReference.ModPath : string` | KSA/PartModelRenderer.cs:109; KSA/FileReference.cs:23 | ✅ | none | Pre-flight check only, so a shader change fails visibly at "Enable" instead of silently |
| A9 | **Shader text edit** (in memory) | VehiclePaintShaders.cs `Inject`/`BuildSnippet` | `MeshIndirect.frag` **and** `MeshIndirectRaytraced.frag` — anchor = first line starting `vec3 sampledColor` and ending `;`; also requires the `inStateFlags` varying | Content/Core/Shaders/Mesh/MeshIndirect.frag:114; MeshIndirectRaytraced.frag:156 | ✅ | n/a (new anchors) | Anchored on the albedo *declaration*, not an exact line, so incidental upstream edits do not break it. Snippet appends after the sample so paint flows through thin film / frost / PBR. Uses `gammaToLinear` (Common/Shared.glsl:203) |
| A10 | **Per-instance bit budget** | VehiclePaint.cs `EncodeBits` (`PaintBitShift`=11, 7:7:7) | `PerInstanceData.StateBitFlag` **bits 11..31** — game writes only bits 0..10 | writers: KSA/PartModelModule.cs:82-133, PartModelDynamicModule.cs:81-107; readers: MeshIndirect.frag:308-353, MeshIndirectRaytraced.frag:290-333 | ✅ | none | 🔶 **The one thing to re-check on every game update:** if KSA starts using bit 11 or above, paint and that feature will corrupt each other. `RayTraceInstance.StateFlags` is `int` (RaytracingRenderer.cs:32), so the bits survive the RT path too |
| A11 | Typed | PaintTargets.cs | `Program.Editor : VehicleEditor?`; `VehicleEditor.EditingSpace.Parts : PartTree?`; `.UnattachedPartTrees : List<PartTree>`; `PartTree.Parts : ReadOnlySpan<Part>`; `Part.SubParts/Id/DisplayName/Modules` | KSA/Program.cs:202; VehicleEditor.cs:407,529; PartTree.cs:80; Part.cs:622 | ✅ | n/a (new) | Enumerates paint targets in both flight (via `VehicleProvider`) and the editor — mirrors the two sources `Program` itself walks at :4019-4029 |
| B1 | Reflection | KittenColor.cs:55-73 | `Program.Instance`→`MaterialSystem`→`AssetMap`/`BigBuffer`/`DeviceCtx` (same chain as doh #1-5) | KSA/Program.cs:94; GpuObjectSystem.cs:16,18; AssetManager.cs:11 | ✅ | none | |
| B2 | GPU write (Vulkan) | KittenColor.cs:191-215 | `BigBuffer.VkBuffer` + `VkUtils.StageAndUploadToBuffer` at `handle*ByteSize.Of<MaterialData>() + OffsetOf(AlbedoColor=16)` | KSA/MaterialData.cs:17 | ✅ | none | tints fur/body/eyes; span→bytes via BCL `MemoryMarshal.AsBytes` (no `CommunityToolkit.HighPerformance` reference) |
| B3 | Shader path (read-only) | (effect) KittenColor.cs concept | `ModelPbr.frag` → `MaterialSet.glsl`: `albedo = mat.albedoColor * texture(...)`; alpha `discard` | Content/Core/Shaders/Mesh/ModelPbr.frag; Common/MaterialSet.glsl | ✅ | MaterialSet.glsl **identical**; ModelPbr.frag differs only in SSAO ordering (rev 4671) | tint path intact |
| C1 | Harmony PREFIX | EngineEmissivePatches.cs:40,51,70 | `PartModelDynamic.AddInstance(PerInstanceData inInstanceData, Viewport, int) : void` (prefix `ref … inInstanceData`) | KSA/PartModelDynamic.cs:379 | ✅ | **none** (PartModelDynamic.cs byte-identical) | param name `inInstanceData` matches |
| C2 | Struct reinterpret (`Unsafe.As`) | EngineEmissivePatches.cs:29-36,77-80 | `PartModelDynamic.PerInstanceData` — writes `Temperature`@**68**, `TfiThickness`@**72** | KSA/PartModelDynamic.cs:309-319 | ✅ | **none** (identical) | ✅ mirror struct matches **exactly** |
| C3 | Typed | EngineEmissive.cs:123,129,159 | `Part.Modules.Get<PartModelDynamicModule>()`; `PartModelDynamicModule.PartModelDynamic` (`required`) | KSA/PartModelDynamicModule.cs:32 | ✅ | none (file identical) | engine discovery via `PartHelpers.GetAllParts` |
| C4 | Shader path (read-only) | (effect) — no mod edit | Temperature→emissive LUT logic, formerly `DynamicMeshIndirect.frag`, now `MeshIndirect.frag` under `#ifdef ENABLE_TEMPERATURE` | Content/Core/Shaders/Mesh/MeshIndirect.frag:214-219 (vert:18-20,71-73) | ✅ | **MOVED** (4693): `DynamicMeshIndirect.frag/.vert` files **removed**; dynamic pipeline now compiles `MeshIndirectVert/Frag` with `ENABLE_TEMPERATURE` (PartModelRenderer.cs:170-171, was DynamicMeshIndirect* :156-157) | ✅ feature still works — game still reads `PerInstanceData.Temperature` |

### Game assets referenced

- **Fragment shaders patched in memory (never on disk):** `Content/Core/Shaders/Mesh/MeshIndirect.frag` and `Content/Core/Shaders/Mesh/MeshIndirectRaytraced.frag`. Matched by **file name** at `ShaderModuleUtils.FromFile` time; `"MeshIndirectFrag"` is also resolved by `ModLibrary` id for the pre-flight check. `MeshIndirect.vert` is **no longer touched at all**.
- **Glass parts are deliberately not painted** — `MeshGlassIndirect.frag` declares `inStateFlags` but ignores it, so windows stay clear.
- **GPU material buffer** (`GpuMaterialSystem.BigBuffer`) for Kitten Color (no asset path).
- **Removed assets the mod's design once assumed:** `DynamicMeshIndirect.vert/.frag` (gone, 4693), `ModelEye.frag`, `ModelGlass.frag` (gone, 4745). `ModelTranslucent.frag` is **new** (4747). None are referenced by the current implementation.

### Update-risk findings (as of 5018)

- ✅ **Vehicle Paint REBUILT for 5018 (2026-07-25).** The 4693-era shader-swap mechanism is gone;
  see "Vehicle Paint mechanism" above and rows A1–A11. What this bought:
  - **No per-instance field is clobbered any more.** The old design wrote floats at offsets 68/72/76,
    which by 5018 were the game-used `EmissiveColor`/`TfiThickness`(`packing1`)/`Wetness` (static) and
    `Temperature`/`TfiThickness`/`Wetness` (dynamic). The new design writes **only** free bits of
    `StateBitFlag`@64, so battery status lights, engine heat glow, thin film and vessel wetness all
    keep working while painted.
  - **No varying-location collisions.** `inStateFlags`@loc4 already exists in every part fragment
    shader, so nothing is added to the vertex shader and locations 5–10
    (`outEmissiveColor`/`outTfiThickness`/`outTemperature`/`outWetness`/frost) are untouched.
  - **Works with the per-variant compile.** Interception moved from `ShaderReference.Shader` down to
    `ShaderModuleUtils.FromFile`, which every variant compile goes through, so
    `ENABLE_EMISSIVE`/`ENABLE_TEMPERATURE`/`ENABLE_THIN_FILM`/`ENABLE_WETNESS`/`ENABLE_FROST` all
    build correctly and the mod never has to reason about which variant it is patching.
  - **Granularity:** per **part instance** (the finest unit the render path exposes), plus per part
    template and a global fallback. A part with several model modules paints as a unit — the modules
    of one `Part` share its paint.
  - **Precision:** 7 bits per channel (128 steps, quantized in sRGB). Not a limitation of the design
    — it is the whole free-bit budget in `StateBitFlag`.
- 🔶 **The one paint invariant to re-check every game update:** `StateBitFlag` bits **11..31** must
  stay unused by KSA. Writers to audit: `PartModelModule.UpdateRenderData` and
  `PartModelDynamicModule.UpdateRenderData`. Readers to audit: the `inStateFlags` bit tests in
  `MeshIndirect.frag`, `MeshIndirectRaytraced.frag`, `MeshGlassIndirect(.Raytraced).frag` and
  `Selected.comp`. At 5018 the game uses bits 0,1,2,3,4,5,6,7,8,9,10 only.
- 🔶 **Secondary paint anchors:** the `vec3 sampledColor = …;` line in `MeshIndirect.frag` (:114) and
  `MeshIndirectRaytraced.frag` (:156). If either moves or is renamed, `Enable` fails loudly with a UI
  message and rendering falls back to stock — it cannot half-apply.
- ✅ **Engine Emissive's LUT survived.** The `#ifdef ENABLE_TEMPERATURE` block (with
  `temperatureLut` sampler and `inTemperature`@loc7) is still present in `MeshIndirect.frag`.
- ✅ `GpuMaterialSystem.cs` and `MaterialData` are **unchanged** → Kitten Color's staged Vulkan write
  is still correct.
- ℹ️ **`PerInstanceData` fields the mod must NOT write (4750→5018 change, still true).**
  `PartModel.PerInstanceData.packing2` → **`public float Wetness`** and
  `PartModelDynamic.PerInstanceData.packing1` → **`public float Wetness`**, feeding the
  `ENABLE_WETNESS` variant (`outWetness`/`inWetness`@loc8) compiled when
  `GameSettings.Current.Graphics.VesselWater` is on; a sibling `ENABLE_FROST` variant arrived with it.
  Engine Emissive writes only `Temperature`/`TfiThickness`, whose offsets did not move.

#### Carried over from the 4680 → 4750 review

- ✅ **Vehicle Paint Harmony plumbing is intact:** `PartModel.cs` is byte-identical OLD↔NEW;
  `AddInstance` signature and `PerInstanceData` layout unchanged.
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

### Update-risk findings (4750 → 5018)

- ⚠ **The animation pipeline was reworked — no break, but investigate the standing bug here.**
  `IAnimProcessor` gained `void UpdateLocalPose(float4x4, Skeleton, Span<TransformTRS>, float)`
  alongside the existing `UpdateSkeleton`. `CatExpressionAnim.MixPose` was replaced by
  `MixPoseLocal`, and the expression mix now runs from `UpdateLocalPose` against a caller-supplied
  local-pose span. `AnimatedRenderable` now allocates a reusable `_processedPose` buffer, copies
  `RuntimeAnim.Transforms` into it, lets each processor mutate the *local pose*, and only then calls
  `Skeleton.UpdateLocalTransforms`/`UpdateWorldTransforms`.
  - **kitten-animations does not implement `IAnimProcessor`**, so the added interface member is not a
    compile break, and `avatar.Core.CharacterModel.SetAnimation` is unchanged.
  - The reflected cache field `CatExpressionAnim._expressionPose` (`private TransformTRS[]?`) is
    **still present and still the correct bust target** — `UpdateLocalPose` re-samples when it is
    `null` or `!CanCacheAnimation`, exactly as before.
  - 🔎 **This rework is the prime suspect for the `ISSUES.md` entry "kitten animations don't properly
    play each one, always the same".** Needs a live in-game pass to confirm whether the cache-bust
    still lands before `UpdateLocalPose` reads it.
- `CharacterAvatar.cs` is **unchanged** 4750→5018; `CharacterCore` is still a struct with
  `public float Scale`.

#### Carried over from the 4680 → 4750 review

- **No breaking deltas detected.** `CharacterAvatar.cs` and `CatExpressionAnim.cs` are
  **byte-identical** OLD↔NEW, so every expression list, MMU/walking animation field, the
  `ExpressionAnim`/`ExpressionWeight` members, and the private `_expressionPose` cache field used
  for reflection are unchanged.
- `AnimatedRenderable.cs` differs only by one trivial logging line (`GltfAssetRef.Id.ToString()`
  vs `.ToString().AsSpan()`); `SetAnimation`, `AnimProcessors`, `MaterialIndices` are unaffected.
- rev 4699 (`KittenEva.IsControllable => true`) is additive and *helps* `GetControlledVehicle()`
  return a `KittenEva`; not a break.

---

## Area summary — Update-risk findings (5261 → 5348)

- ⚠️ **kitten-animations vs the new per-frame pose guard (rev 5278) — the headline finding this span.**
  `KSA/AnimatedRenderable.cs` gained `private ulong _lastPoseUpdateFrameNumber = ulong.MaxValue;` and the
  pose path is now gated on `if (Program.FrameNumber != _lastPoseUpdateFrameNumber)` — previously
  `if (!FreezeAnimation)`. The changelog entry: *"Fixed seated crew and EVA crew animation updating once
  per visible viewport instead of once per frame. Base pose sampling and full skeleton propagation now
  run once per frame."* `CatExpressionAnim` is **byte-identical** and `_expressionPose` still resolves,
  but the mod's cache-bust forces a **second pose evaluation in the same frame, which is now dropped**.
  This is the first concrete mechanism found for the standing *"kitten animations don't properly play
  each one, always the same"* entry in [`../ISSUES.md`](../ISSUES.md). **Open — live pass required.**
- ✅ **doh's MMU attachment survives a retype.** Rev 5269 (MMU fold-away anim) changed
  `CharacterAvatar.Attachments.Mmu.MmuMesh` from `StaticMeshRenderable` to `AnimatedRenderable`, and
  `CharacterAvatar` now builds it with `MeshRendererSkinnedPbr` + an `AnimationScrubSampler ArmScrub`.
  `doh.lib/Spawning/KittenSpawner.cs:542-556` walks by **field name** and then finds `MaterialIndices`
  anywhere in the runtime type hierarchy — and `AnimatedRenderable` declares
  `protected readonly int[] MaterialIndices` (`:35`) exactly as `StaticMeshRenderable` does (`:31`).
  **No change needed**, but confirm the MMU still recolours in game.
- ✅ **The whole KittenEva reflection chain is intact and still field-shaped.**
  `KittenEva` (type-name compare) → `KittenEva._renderable` (`:15`) → `KittenRenderable._characterAvatar`
  (`:12`) → `CharacterAvatar.Core` (`public CharacterCore Core;`, `:209`) → `CharacterCore.Scale`
  (`public float Scale = 0.01f;`). **`Core` and `Scale` are both still plain fields**, so garrys-torch's
  `FieldInfo.SetValue` path still works. Rev 5329 turned `Module.Parent` into a property — audited, and
  no mod in this area reflects on it.
- ✅ **GPU byte layouts identical.** `MaterialData` and both `PerInstanceData` structs diff clean, so
  doh's `handle*80+16` `BigBuffer` writes and humble-arteest's `StateBitFlag` padding-byte hijack are
  safe. The render bridge (`Program.{Instance,MaterialSystem,SuperMeshRenderSystem,CharacterRenderSystem}`,
  `GpuObjectSystem.{BigBuffer,DeviceCtx,CreateObject}`, `AssetManager.*`, `BindlessHandle`, `Handle`)
  resolves unchanged.
- ✅ **humble-arteest Engine Emissive unaffected.** `MeshIndirect.frag` changed by exactly one line
  (`SamplePortraitLight` → `SampleMeshForwardLights`, rev 5301); the `vec3 sampledColor` anchor is still
  at `:114`, `inStateFlags` is intact, and the `ENABLE_TEMPERATURE` LUT still lives in that file.
  `MeshIndirect.vert` is **byte-identical**. `PartModel/PartModelDynamic.AddInstance` signatures unchanged.
- ❌ **humble-arteest Vehicle Paint** — still dead by design since rev 4693; self-disables. Unchanged.
- ⚠️ **doh — new raytracing paths.** Rev 5312 added receive-only raytracing for IVA kittens;
  `ModelPbr.frag` gained a `RAYTRACED_REFLECTIONS` variant (three new samplers at set 7) and
  `AnimatedRenderable` gained `RaytracedMeshBucketHandles`. The `albedo` path doh's material clones drive
  is unchanged and `Common/MaterialSet.glsl` is untouched — but confirm cloned materials still read
  correctly with raytracing on.
- ℹ️ Additive kitten work this span, no binding impact: seated idle + fidget (5268), MMU fold-away (5269),
  low-gravity walk/run (5284), swimming + `KittenLocomotion` swim state (5314), crew-portrait bone
  tracking and FOV (5270/5273), vehicle destruction now kills crew (5316).
