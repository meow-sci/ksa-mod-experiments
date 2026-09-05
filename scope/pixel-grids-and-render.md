# Pixel-Grid & Custom-Render Mods — Game Integration Scope

## Workspace integration (current)

Active bundled features: **its-so-shiny, thug-life**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

Blinky and its engine-grid patches/assets are removed. ShinyGridState records and render-mesh policy are live entries; global grid discovery is available in the policy inspector. Thug entries carry stable runtime IDs and their own controls. Exact target choices and next-create transforms remain authoring data. Shiny consumes shared `ksa-lights.lib`; it no longer references zippo.lib. Existing Shiny render prefixes and Thug render pass remain.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break the pixel-grid and
custom-render mods (`its-so-shiny`, `thug-life`). Every game-facing member,
Harmony target, GPU/render API, shader, and part template these mods touch is
enumerated and verified against decompiled sources **and** the Content asset tree.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard remains in `unscience/Patcher.cs`; feature Harmony groups are registered by their owning libraries through `ConfigureRuntime`. See [architecture](00-architecture-and-abstractions.md).

## its-so-shiny

**Purpose** — A grid whose pixels are stock `LightPart` instances instead
of an engine. Pixels are toggled through the light's `PowerConsumer` light switch
(`LightIsActive`); color/intensity are applied per `PartTemplate` via Zippo's
`LightController`. Grids connect to battery-bearing parts for power.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Vehicle, grid name, layout, dimensions, spacing, offsets, scale, intensity and color. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature, or asset path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs` | `PartModelModule.UpdateRenderData(in double4x4, bool, IViewport, int)` | `KSA/PartModelModule.cs` | Yes | **retyped @5402** (`Viewport` → `IViewport`; prefix takes only `__instance`) | Applies only to Shiny grid parts. |
| 2 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs` | `PartModelDynamicModule.UpdateRenderData(in double4x4, bool, IViewport, int)` | `KSA/PartModelDynamicModule.cs` | Yes | **retyped @5402** | |
| 3 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs` | `PartModelGlassModule.UpdateRenderData(in double4x4, bool, IViewport, int)` | `KSA/PartModelGlassModule.cs` | Yes | **retyped @5402** | `PartModel(Glass).AddInstance` now gated on `ViewportOptionFlags.RenderPartModels` (the Shiny prefixes retain that viewport gate) — every preset has it. |
| 4 | Direct (in prefix) | `its-so-shiny.lib/ShinyPatches.cs` | `Module.Parent`→`Part`; `Part.FullPart`; `Part.Id`; `Part.LightSwitch` (PowerConsumer?); `PowerConsumer.LightIsActive` (bool) | `KSA/ModuleBase.cs`; `KSA/Part.cs`; `KSA/PowerConsumer.cs` | Yes | **game chain changed @5402** | Mesh shown iff `LightIsActive`. The game's own hide test in #1/#2 is now `Part.IsLightSwitchedOff()` (`KSA/Part.cs`) = `!LightIsActive \|\| !lightSwitch.IsSwitchedOn()` (new `PowerConsumer.IsSwitchedOn()` `:50`, reads `Tree.PowerConsumers.States[StatesIdx].Active`), and returns false when `lightSwitch.Parent.Tree != Tree`. The mod deliberately ignores the powered state (a pixel the mod turned on shows its mesh even if the bus is dead); unchanged behaviour. `Part.ResetModuleProperties` (`:1803`) nulls and re-resolves `LightSwitch` — always read it live, never cache. |
| 5 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs` | `ModLibrary.Get<PartTemplate>("LightPart")` | `KSA/ModLibrary.cs` | Yes | None | Runtime string id (see assets). |
| 6 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs` | `new Part(string, PartTemplate, ...)` | `KSA/Part.cs` | Yes | None | |
| 7 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs`; `:98,148` | `PartTree.CreateFromNewPartTree(Part)`; `Vehicle.UpdateVehicleConfiguration()` | `KSA/PartTree.cs`; `KSA/Vehicle.cs` | Yes | None | |
| 8 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs` | `Vehicle.Parts`/`PartTree.Root`; `Part.TreeParent`/`Part.TreeChildren` | `KSA/Vehicle.cs`; `KSA/PartTree.cs`; `KSA/Part.cs` | Yes | None | |
| 9 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs` | `PartTree.Modules.Get<Battery>()`; `Battery.Parent`→`Part`; `Part.FullPart` | `KSA/PartTree.cs`; `KSA/ModuleList.cs`; `KSA/Battery.cs`; `KSA/ModuleBase.cs`; `KSA/Part.cs` | Yes | None | Battery anchors for power partitioning. |
| 10 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs` | `Part.SetStage(int)`; `Part.Connection.Connect(IConnector,IConnector)`; `Part.Connections`; `Connection.Disconnect()` | `KSA/Part.cs` | Yes | None | |
| 11 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs` | `Part.PositionParentAsmb`; `Part.Asmb2ParentAsmb`; `Part.Scale` | `KSA/Part.cs` | Yes | None | |
| 12 | Direct | `its-so-shiny.lib/ShinyPixelCell.cs`; `ShinyGridBuilder.cs` | `Part.LightSwitch` (PowerConsumer?); `PowerConsumer.LightIsActive` (set) | `KSA/Part.cs`; `KSA/PowerConsumer.cs` | Yes | None | Primary pixel on/off path. |
| 13 | Direct | `its-so-shiny.lib/ShinyPixelGrid.cs` | `Part.Template`; `Part.SubParts` (ReadOnlySpan<Part>) | `KSA/Part.cs` | Yes | None | Recursive light-part discovery. |
| 14 | Transitive (ZippoLib) | `ShinyPixelCell.cs`; `ShinyGridManager.cs` | `LightController.{ApplyColor,ApplyIntensity,GetLightComponents,WriteColor,WriteIntensity,HasLights}` (operates on light template render data) | `MeowSci.ZippoLib` (repo lib; game coupling catalogued in zippo scope) | Yes | None | Color/intensity writes; verify in zippo scope file. |
| 15 | Abstraction | `ShinyGridManager.cs`; `ItsSoShinySubmod.cs` | `VehicleProvider.GetAllVehicles()`; `PartHelpers.GetAllParts` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` | Yes | None | |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `LightPart` | Light part template (default `ShinyGridConfig.cs`) | `ModLibrary.Get<PartTemplate>` id | `<Part Id="LightPart">` `Core/PartAssets.xml:21`; `<PartGameData Id="LightPart">` with `<PowerConsumer LightSwitch="true">` `Core/CoreElectricalAGameData.xml:165` | Yes | None (both files byte-identical 5348→5402) |

## thug-life

**Purpose** — Draws the "thug life" sunglasses meme as a textured cut-out quad anchored
to any part/subpart of any vehicle, riding along in 3D. Pure custom GPU rendering: builds
its own Vulkan pipeline/descriptor/texture and injects draws into KSA's offscreen MSAA
main pass via a Harmony postfix. Does **not** create parts.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**Init timing (load-order constraint)** — the GPU resources are built **lazily, on the
first entry** (`ThugLifeRenderManager.EnsureGpuResources`, `:91-115`), never in the
constructor. StarMap fires `[StarMapAllModsLoaded]` from a postfix on
`ModLibrary.LoadAll()` (`KSA/Program.cs`), but the game does not create
`Program.OffscreenTarget` until `BuildRenderTargets()` further down that same boot method
(`KSA/Program.cs`, decl `:1507`) — so building the pipeline at `Initialize()` dereferenced a null
`RenderTarget` and the submod reported *"init failed: Object reference not set to an
instance of an object"*. ⚠ Any future work that moves GPU allocation back into
`Initialize()` re-introduces this. Same discipline as the sibling gatOS port.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Exact/controlled vehicle, verified part/subpart and transform/size. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature, or shader/asset path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `thug-life.lib/ThugLifeRenderPatches.cs` | `SuperMeshRenderSystem.RenderMainPass(CommandBuffer commandBuffer)` — postfix records quad draws into the active offscreen pass | `KSA/SuperMeshRenderSystem.cs` | Yes | `:338`→`:347` @5402; body gained only a profiler `TagRegion(SkinnedTwoSidedMeshes)` around the new two-sided skinned technique's draws (parachute canopies) | Single method, patched by name. Called 3x from `KSA/Program.cs`: `:4395` (`RenderViewport`, once per **non-main visible** viewport — secondary + crew-portrait), `:4656` (main flight scene), `:4856` (editor). The part-thumbnail viewport does **not** go through it. |
| 2 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `ModLibrary.Get<ShaderReference>("UnlitMeshVert")` | id→path in `Core/DefaultAssets.xml:53`; file `Core/Shaders/Mesh/UnlitMesh.vert` | Yes | None (file byte-identical 5348→5402; `DefaultAssets.xml` only gained `StaticObjectPrePassIndirectFrag` at `:62`) | Stock shader; **not** MeshIndirect/Model* — untouched by 4693/4745. |
| 3 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `ModLibrary.Get<ShaderReference>("UnlitMeshFrag")` | id→path in `Core/DefaultAssets.xml:54`; file `Core/Shaders/Mesh/UnlitMesh.frag` | Yes | None (byte-identical) | Frag hard-writes `alpha=1.0` (cut-out via geometry, per renderer comment). |
| 4 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `RenderTechnique.CreateShaderStages(Device, Span<ShaderReference>, Span<VkSpecializationInfo>=default)` | `RenderCore/RenderTechnique.cs` | Yes | None (file byte-identical) | `ShaderReference : FileReference, IKeyed, IComboable` (`KSA/ShaderReference.cs`). |
| 5 | Direct (render-pass) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `Program.OffscreenTarget.SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)` — `RenderTarget : IRenderPassInfo` | `KSA/Program.cs`, `KSA.Rendering/RenderTarget.cs` | Yes | **REPLACED @5261** — was `Program.OffScreenPass` (`RenderPassState`) → `.SampleCount`, `.Pass`; unchanged @5402 (`RenderTarget.cs` byte-identical) | Game migrated the main scene pass to **dynamic rendering**; the old property no longer exists. **Null until `BuildRenderTargets()` (`Program.cs`), which runs AFTER `ModLibrary.LoadAll()` (`:942`) — i.e. after `[StarMapAllModsLoaded]`. Pipeline build must stay lazy (see *Init timing* above).** Non-main viewports own their targets but build them with the same `Program.Instance.ColorFormat` + `GameSettings.GetSampleCount()` (`KSA.Rendering/ViewportRenderSurface.cs`, `KSA/ViewportBase.cs`), so the main-target-built pipeline stays compatible in every `RenderMainPass` call. |
| 6 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `Presets.InputAssembly.TriangleList`; `Presets.Rasterization.Fill.CullNone`; `Presets.BlendState.BlendColorAlpha` | `RenderCore.Pipelines/SimplePipelineCreator.cs` (+ Brutal abstractions) | Yes | None | Pipeline state presets; compile-verified by build. |
| 7 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` | `KSA/RenderingPresets.cs` (used widely, e.g. `KSA.Rendering.Water.Rendering/OceanRenderer.cs`) | Yes | None (file byte-identical) | Reverse-Z depth test+write; the game still clears depth to `0f`. 4730/4733 depth-prepass changes did not alter this preset. |
| 8 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs`; `:50`,`TextureFactory.cs` | `Renderer.{Device,Allocator,Graphics,DynamicStateInfo,ViewportState}` | `Core/Renderer.cs` (byte-identical) | Yes | None | Compile-verified. |
| 9 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `Program.GetRenderCamera()` (= `RenderedViewport.GetCamera()`); `Camera.MVP.viewProjection` | `KSA/Program.cs`, `:491` (`RenderedViewport : IViewport` @5402); `KSA/Camera.cs`, `KSA/ViewProjection.cs` | Yes | **CHANGED (mod-side @5348)** — was `Program.GetMainCamera()` (`:632`); `RenderedViewport` retyped `Viewport`→`IViewport` @5402 (mod never names the type) | `RenderMainPass` runs once per **visible viewport** (main + the two always-visible 128² crew-portrait viewports since 5261; @5402 `RenderViewport` sets `_renderedViewport = viewport` at `Program.cs` before the call), and ego space is camera-relative, so the main camera drew the portrait passes with the wrong clip transform. Now uses the camera of the viewport being rendered. Mod null-checks defensively. |
| 10 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `Program.SetViewport(CommandBuffer)` | `KSA/Program.cs` | Yes | None (body identical; sizes to `RenderedViewport.Size`) | |
| 11 | Direct | `thug-life.lib/ThugLifeRenderManager.cs` | `Program.GetRenderer()` (Renderer) | `KSA/Program.cs` | Yes | None | Called from the lazy `EnsureGpuResources()`, not from the constructor. |
| 12 | Direct (ego transform) | `thug-life.lib/ThugLifeQuadRenderer.cs` | `Vehicle.GetMatrixAsmb2Ego(Camera)`; `Part.PositionEgo(ref readonly double4x4)`; `Part.Asmb2Ego(doubleQuat)`; `Vehicle.Asmb2Ego` (doubleQuat) | `KSA/Vehicle.cs`; `KSA/Part.cs` | Yes | None | Per-frame model-ego matrix; caller passes `in` to the `ref readonly` param. |
| 13 | Direct (UI) | `thug-life.lib/ThugLifeSubmod.cs` | `Vehicle.Parts.Parts`; `Part.Template.Id`; `Part.Id`; `Part.SubParts` | `KSA/Part.cs`; `KSA/PartTree.cs` | Yes | None | Combo population. |
| 14 | GPU lib (Brutal/RenderCore) | `ThugLifeTextureFactory.cs`; `ThugLifeQuadRenderer.cs` (pipeline/descriptor/buffers) | `SimpleVkTexture`; `VkUtils.UploadBufferToImage`/`StageAndUploadToBuffer`; `BufferEx`, `DescriptorSetLayoutEx`, `DescriptorPoolEx`, `VertexInput`, `ShaderStages`, `CommandBuffer` | `Brutal.VulkanApi(.Abstractions)`, `RenderCore`, `Core` | Yes | None (`SimpleVkTexture.cs`, `VkUtils.cs` byte-identical @5402) | **4729 bumped Brutal packages** — highest churn surface; compile against 4750 DLLs passes, so the used API is intact. |
| 15 | Abstraction | `thug-life.lib/ThugLifeSubmod.cs` | `VehicleProvider.GetAllVehicles()` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` | Yes | None | |
| 16 | Direct (type test) | `thug-life.lib/KittenGlassesPreset.cs` | `KittenEva` (`: Vehicle`) — `vehicle is KittenEva`, gates the **animate thug** button | `KSA/KittenEva.cs` | Yes | **NEW (mod-side, 2026-08-23)** | Type identity only, no members touched. A rename/reparent of `KittenEva` is a compile error, not silent drift. Seated kittens are not vehicles and are out of scope here. |
| 17 | Direct (UI) | `thug-life.lib/ThugLifeSubmod.cs` | `Vehicle.Parts.Parts` — first top-level part as the kitten fallback anchor | `KSA/PartTree.cs` | Yes | **NEW (mod-side, 2026-08-23)** | A `KittenEva` is constructed around its MMU backpack part as root (`KSA/EVADoor.cs`), so this is non-empty in practice; null-checked regardless. |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `UnlitMeshVert` | Vertex shader | `ModLibrary.Get<ShaderReference>` id | `Core/DefaultAssets.xml:53` → `Core/Shaders/Mesh/UnlitMesh.vert` | Yes | None (id, path, file all identical; md5 `71cc48a5…` @5402) |
| `UnlitMeshFrag` | Fragment shader | `ModLibrary.Get<ShaderReference>` id | `Core/DefaultAssets.xml:54` → `Core/Shaders/Mesh/UnlitMesh.frag` | Yes | None (id, path, file all identical; md5 `4f4adaa1…` @5402) |

Texture is generated programmatically (`ThugLifeTexturePattern.cs`, `R8G8B8A8UNorm`) — no
external texture asset dependency.

## Historical evidence

See [dated integration and upgrade reference](history/pixel-grids-and-render.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.

## Current runtime release behavior

Grids acquire shared light-baseline leases. Release/unload destroys and disposes owned created parts after waiting for vehicle solvers, while scanned parts are retained and their appearance/switch state restored. Rescanning does not replace an existing ownership record. Removing the last attachment disables render dispatch, waits for prior GPU work and releases its texture and quad resources. A later attachment can allocate them again. Shiny destruction now calls Part.Dispose after detaching owned parts and rebuilding the vehicle tree; JobSystems.VehicleSolver.Wait protects the mutation.

Feature hook targets retain their existing signatures; patch ownership now follows explicit demand through the shared runtime coordinator. Native acceptance remains outstanding.
