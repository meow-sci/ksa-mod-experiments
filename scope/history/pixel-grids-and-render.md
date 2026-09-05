# Historical reference snapshot

Captured before the documentation reconciliation following the workspace redesign. This is dated design/upgrade evidence, not current instructions or a claim of in-game validation. Use [current scope](../FULL_SCOPE.md) for active ownership. Source line numbers refer to the cited historical builds.

# Pixel-Grid & Custom-Render Mods — Game Integration Scope

## Workspace integration (current)

Active bundled features: **its-so-shiny, thug-life**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../../docs/WORKSPACE.md).

Blinky and its engine-grid patches/assets are removed. ShinyGridState records and render-mesh policy are live entries; global grid discovery is available in the policy inspector. Thug entries carry stable runtime IDs and their own controls. Exact target choices and next-create transforms remain authoring data. Shiny consumes shared `ksa-lights.lib`; it no longer references zippo.lib. Existing Shiny render prefixes and Thug render pass remain.

The tables below retain game-member and update-history detail. Older UI/persistence descriptions describe the pre-workspace implementation where they conflict with this section; the feature README and `*.Workspace.cs` / `*.Live.cs` define current ownership. No additional Harmony targets were introduced by the workspace split.


Permanent reference for detecting when KSA game updates break the pixel-grid and
custom-render mods (`blinky`, `its-so-shiny`, `thug-life`). Every game-facing member,
Harmony target, GPU/render API, shader, and part template these mods touch is
enumerated and verified against decompiled sources **and** the Content asset tree.

**Verified game versions**

- NEW decomp `2026.9.7.5402` root: `~/repos/meow-sci/ksa-game-assemblies/current/decomp`
- OLD decomp `2026.8.22.5348` root: `~/repos/meow-sci/ksa-game-assemblies_prev/current/decomp`
- NEW Content root: `~/repos/meow-sci/ksa-game-assemblies/current/Content`
- OLD Content root: `~/repos/meow-sci/ksa-game-assemblies_prev/current/Content`

Paths in the **Decomp/Content path (NEW)** column are relative to the NEW decomp root
(namespace-foldered, e.g. `KSA/Part.cs`) or the NEW Content root (e.g.
`Core/DefaultAssets.xml`). **Mod code** paths are relative to the repo root
`~/repos/meow-sci/unscience`. Line numbers were re-verified against 5402 on 2026-09-02; the
per-span "Δ vs OLD" cells are historical unless marked `@5402`.

**How these mods are hosted (all three)**

- All reusable game-facing logic lives in the `*.lib` project (`blinky.lib`,
  `its-so-shiny.lib`, `thug-life.lib`); each exposes an `ISubmod`
  (`MeowSci.KsaAbstractions.ISubmod`) consumed two ways:
  1. Standalone StarMap mod (`blinky/Mod.cs` F11, `its-so-shiny/Mod.cs` F11,
     `thug-life/Mod.cs` F12) — own ImGui window + own `Harmony` instance in its `Patcher.cs`.
  2. Embedded in the **unscience** supermod (`unscience/Mod.cs:69` `new BlinkySubmod()`,
     `:82` `new ItsSoShinySubmod()`, `:91` `new ThugLifeSubmod()`) as collapsible sections,
     with all three patch sets applied on the **single** supermod Harmony instance
     (`unscience/Patcher.cs:51` `ThugLifeRenderPatches.Apply`, `:57` `BlinkyPatches.Apply`,
     `:58` `ShinyPatches.Apply`, each wrapped in `TryApply`).
- `blinky` is also driven headlessly via RPC: `unladen-swallow.lib/Blinky*Endpoint.cs`
  call `BlinkyGridManager` (e.g. `BlinkyAnimateEndpoint.cs`, `BlinkyStaticEndpoint.cs`).
  Those endpoints are mod-to-mod (unladen-swallow), not direct game integration.
- `blinky` + `its-so-shiny` patch the **same three** render-data methods. Harmony allows
  multiple prefixes; `blinky` keys on `pixel_*` Ids and `its-so-shiny` on `shiny_*` Ids,
  so the prefixes never conflict (a part is skipped only if its own mod's prefix returns false).

**Summary of 4680 -> 4750 risk: NO breaking deltas detected.** Every patched method,
typed member, enum, shader id/path, and part-template id these mods use is
signature-identical between OLD and NEW; only source line numbers shifted. The
changelog's render-path churn (4693 MeshIndirect merge, 4745 ModelGlass+ModelEye merge,
4701/4747 MeshIndirect/ModelTranslucent `.frag` cleanups) touches `MeshIndirect.*` and
`Model*.*` shaders only — none of which these mods reference. The `dotnet build` against
the 4750 DLLs passes, which independently confirms the entire **direct typed + GPU** API
surface still compiles. Details per mod below.

---

## its-so-shiny

**Purpose** — Same grid concept as blinky but each pixel is a stock `LightPart` instead
of an engine. Pixels are toggled through the light's `PowerConsumer` light switch
(`LightIsActive`); color/intensity are applied per `PartTemplate` via Zippo's
`LightController`. Grids connect to battery-bearing parts for power.

**Unscience integration** — `ItsSoShinySubmod : ISubmod`
(`its-so-shiny.lib/ItsSoShinySubmod.cs:11`), instantiated by the supermod
(`unscience/Mod.cs:82`) and standalone host (`its-so-shiny/Mod.cs:27`). Static
`ShinyGridManager` (`its-so-shiny.lib/ShinyGridManager.cs:31`) is the control surface.
Render-skip patches via `ShinyPatches.Apply` (`its-so-shiny/Patcher.cs:15`,
`unscience/Patcher.cs:58`). Color/intensity reuse `MeowSci.ZippoLib.LightController`
(sibling lib — the actual light-template reflection lives in the zippo scope).

**UI/hotkeys** — Standalone window "its-so-shiny", 500x640, `MenuBar`, **F11**
(`its-so-shiny/Mod.cs:52,79`). Create form (size/spacing/scale/offset/layout/intensity/
color/vehicle/grid-name), per-grid appearance + pattern + destroy sections, "Render
light meshes" checkbox (default off → mesh renders only while the light is active),
Debug menu "Scan for shiny grids".

**Persistence** — None to disk. Light pixels are real `LightPart`s in the vehicle's
`PartTree`; in-memory registry rebuilt by global scan re-parsing `shiny_{grid}_{row}_{col}`.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature, or asset path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs:25,29,66` | `PartModelModule.UpdateRenderData(in double4x4, bool, IViewport, int)` | `KSA/PartModelModule.cs:87` | Yes | **retyped @5402** (`Viewport` → `IViewport`; prefix takes only `__instance`) | Coexists with blinky #1 (different Id prefix). |
| 2 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs:26,30,69` | `PartModelDynamicModule.UpdateRenderData(in double4x4, bool, IViewport, int)` | `KSA/PartModelDynamicModule.cs:55` | Yes | **retyped @5402** | |
| 3 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs:27,31,72` | `PartModelGlassModule.UpdateRenderData(in double4x4, bool, IViewport, int)` | `KSA/PartModelGlassModule.cs:72` | Yes | **retyped @5402** | `PartModel(Glass).AddInstance` now gated on `ViewportOptionFlags.RenderPartModels` (see blinky #3) — every preset has it. |
| 4 | Direct (in prefix) | `its-so-shiny.lib/ShinyPatches.cs:58-66` | `Module.Parent`→`Part`; `Part.FullPart`; `Part.Id`; `Part.LightSwitch` (PowerConsumer?); `PowerConsumer.LightIsActive` (bool) | `KSA/ModuleBase.cs:31`; `KSA/Part.cs:1123,686`; `KSA/PowerConsumer.cs:30` | Yes | **game chain changed @5402** | Mesh shown iff `LightIsActive`. The game's own hide test in #1/#2 is now `Part.IsLightSwitchedOff()` (`KSA/Part.cs:1357`) = `!LightIsActive \|\| !lightSwitch.IsSwitchedOn()` (new `PowerConsumer.IsSwitchedOn()` `:50`, reads `Tree.PowerConsumers.States[StatesIdx].Active`), and returns false when `lightSwitch.Parent.Tree != Tree`. The mod deliberately ignores the powered state (a pixel the mod turned on shows its mesh even if the bus is dead); unchanged behaviour. `Part.ResetModuleProperties` (`:1803`) nulls and re-resolves `LightSwitch` — always read it live, never cache. |
| 5 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:27` | `ModLibrary.Get<PartTemplate>("LightPart")` | `KSA/ModLibrary.cs:1042` | Yes | None | Runtime string id (see assets). |
| 6 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:157` | `new Part(string, PartTemplate, ...)` | `KSA/Part.cs:1386` | Yes | None | |
| 7 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:94,147`; `:98,148` | `PartTree.CreateFromNewPartTree(Part)`; `Vehicle.UpdateVehicleConfiguration()` | `KSA/PartTree.cs:173`; `KSA/Vehicle.cs:1864` | Yes | None | |
| 8 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:17,146,76-77` | `Vehicle.Parts`/`PartTree.Root`; `Part.TreeParent`/`Part.TreeChildren` | `KSA/Vehicle.cs:604`; `KSA/PartTree.cs:97`; `KSA/Part.cs:664,666` | Yes | None | |
| 9 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:205,209-210` | `PartTree.Modules.Get<Battery>()`; `Battery.Parent`→`Part`; `Part.FullPart` | `KSA/PartTree.cs:37`; `KSA/ModuleList.cs:178`; `KSA/Battery.cs:9`; `KSA/ModuleBase.cs:31`; `KSA/Part.cs:1123` | Yes | None | Battery anchors for power partitioning. |
| 10 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:87,221,131,133` | `Part.SetStage(int)`; `Part.Connection.Connect(IConnector,IConnector)`; `Part.Connections`; `Connection.Disconnect()` | `KSA/Part.cs:1210,538,670,554` | Yes | None | |
| 11 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:181,185,186` | `Part.PositionParentAsmb`; `Part.Asmb2ParentAsmb`; `Part.Scale` | `KSA/Part.cs:752,766,815` | Yes | None | |
| 12 | Direct | `its-so-shiny.lib/ShinyPixelCell.cs:24,27`; `ShinyGridBuilder.cs:234,236` | `Part.LightSwitch` (PowerConsumer?); `PowerConsumer.LightIsActive` (set) | `KSA/Part.cs:686`; `KSA/PowerConsumer.cs:30` | Yes | None | Primary pixel on/off path. |
| 13 | Direct | `its-so-shiny.lib/ShinyPixelGrid.cs:147,150` | `Part.Template`; `Part.SubParts` (ReadOnlySpan<Part>) | `KSA/Part.cs:576,1079` | Yes | None | Recursive light-part discovery. |
| 14 | Transitive (ZippoLib) | `ShinyPixelCell.cs:31,36-37`; `ShinyGridManager.cs:218-223` | `LightController.{ApplyColor,ApplyIntensity,GetLightComponents,WriteColor,WriteIntensity,HasLights}` (operates on light template render data) | `MeowSci.ZippoLib` (repo lib; game coupling catalogued in zippo scope) | Yes | None | Color/intensity writes; verify in zippo scope file. |
| 15 | Abstraction | `ShinyGridManager.cs:155`; `ItsSoShinySubmod.cs` | `VehicleProvider.GetAllVehicles()`; `PartHelpers.GetAllParts` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` | Yes | None | |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `LightPart` | Light part template (default `ShinyGridConfig.cs:11`) | `ModLibrary.Get<PartTemplate>` id | `<Part Id="LightPart">` `Core/PartAssets.xml:21`; `<PartGameData Id="LightPart">` with `<PowerConsumer LightSwitch="true">` `Core/CoreElectricalAGameData.xml:165` | Yes | None (both files byte-identical 5348→5402) |

**Update-risk findings (4680 -> 4750)**

- No breaking deltas. Render-skip targets (#1–#3) identical to blinky's and unchanged.
  The `LightPart` template + its `PowerConsumer LightSwitch="true"` definition are
  identical in OLD and NEW Content.
- Part-tree / battery-power build path (#5–#13) unchanged.
- Color/intensity (#14) flows through Zippo's `LightController`; its game-side coupling
  is owned by the zippo scope file and should be checked there, but no signature change
  was observed in the `PartTemplate`/light render-data types it depends on.

---

## thug-life

**Purpose** — Draws the "thug life" sunglasses meme as a textured cut-out quad anchored
to any part/subpart of any vehicle, riding along in 3D. Pure custom GPU rendering: builds
its own Vulkan pipeline/descriptor/texture and injects draws into KSA's offscreen MSAA
main pass via a Harmony postfix. Does **not** create parts.

**Unscience integration** — `ThugLifeSubmod : ISubmod`
(`thug-life.lib/ThugLifeSubmod.cs:14`), instantiated by the supermod
(`unscience/Mod.cs:91`) and standalone host (`thug-life/Mod.cs:27`). The render postfix
is applied via `ThugLifeRenderPatches.Apply` (`thug-life/Patcher.cs:18`,
`unscience/Patcher.cs:51`) and dispatches to the static
`ThugLifeRenderManager.Instance`/`.Active` so each host (standalone vs supermod) drives
its own manager on its own assembly load context. GPU resources own pipeline + texture +
buffers; render disables itself on first error (`ThugLifeRenderManager.cs:106,134`).

**Init timing (load-order constraint)** — the GPU resources are built **lazily, on the
first entry** (`ThugLifeRenderManager.EnsureGpuResources`, `:91-115`), never in the
constructor. StarMap fires `[StarMapAllModsLoaded]` from a postfix on
`ModLibrary.LoadAll()` (`KSA/Program.cs:942`), but the game does not create
`Program.OffscreenTarget` until `BuildRenderTargets()` further down that same boot method
(`KSA/Program.cs:970`, decl `:1507`) — so building the pipeline at `Initialize()` dereferenced a null
`RenderTarget` and the submod reported *"init failed: Object reference not set to an
instance of an object"*. ⚠ Any future work that moves GPU allocation back into
`Initialize()` re-introduces this. Same discipline as the sibling gatOS port.

**UI/hotkeys** — Standalone window "Thug Life", 500x600, **F12**
(`thug-life/Mod.cs:51,78`). Create form (vehicle / part / optional subpart filtered
combos + position/rotation/width/height), per-entry transform + Visible + Remove sections.
An **animate thug** button (`ThugLifeSubmod.cs:204`) appears beside *Add Sunglasses*
only when the selected vehicle is a `KSA.KittenEva`; it applies the fixed pose in
`KittenGlassesPreset.cs` and slides the entry into place via `ThugLifeSlide`, driven from
`ThugLifeRenderManager.Update(dt)` (called from the submod's `Update`, i.e. `OnBeforeUi`
in both hosts). The slide is pure mod-side math — it touches no game API.

**Persistence** — None. Entries are in-memory only (`ThugLifeRenderManager._entries`);
lost on reload. No StarMap save hooks, no disk I/O.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature, or shader/asset path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `thug-life.lib/ThugLifeRenderPatches.cs:19-21,44` | `SuperMeshRenderSystem.RenderMainPass(CommandBuffer commandBuffer)` — postfix records quad draws into the active offscreen pass | `KSA/SuperMeshRenderSystem.cs:347` | Yes | `:338`→`:347` @5402; body gained only a profiler `TagRegion(SkinnedTwoSidedMeshes)` around the new two-sided skinned technique's draws (parachute canopies) | Single method, patched by name. Called 3x from `KSA/Program.cs`: `:4395` (`RenderViewport`, once per **non-main visible** viewport — secondary + crew-portrait), `:4656` (main flight scene), `:4856` (editor). The part-thumbnail viewport does **not** go through it. |
| 2 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs:114` | `ModLibrary.Get<ShaderReference>("UnlitMeshVert")` | id→path in `Core/DefaultAssets.xml:53`; file `Core/Shaders/Mesh/UnlitMesh.vert` | Yes | None (file byte-identical 5348→5402; `DefaultAssets.xml` only gained `StaticObjectPrePassIndirectFrag` at `:62`) | Stock shader; **not** MeshIndirect/Model* — untouched by 4693/4745. |
| 3 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs:115` | `ModLibrary.Get<ShaderReference>("UnlitMeshFrag")` | id→path in `Core/DefaultAssets.xml:54`; file `Core/Shaders/Mesh/UnlitMesh.frag` | Yes | None (byte-identical) | Frag hard-writes `alpha=1.0` (cut-out via geometry, per renderer comment). |
| 4 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:117` | `RenderTechnique.CreateShaderStages(Device, Span<ShaderReference>, Span<VkSpecializationInfo>=default)` | `RenderCore/RenderTechnique.cs:35` | Yes | None (file byte-identical) | `ShaderReference : FileReference, IKeyed, IComboable` (`KSA/ShaderReference.cs:21`). |
| 5 | Direct (render-pass) | `thug-life.lib/ThugLifeQuadRenderer.cs:152` | `Program.OffscreenTarget.SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)` — `RenderTarget : IRenderPassInfo` | `KSA/Program.cs:457`, `KSA.Rendering/RenderTarget.cs:356` | Yes | **REPLACED @5261** — was `Program.OffScreenPass` (`RenderPassState`) → `.SampleCount`, `.Pass`; unchanged @5402 (`RenderTarget.cs` byte-identical) | Game migrated the main scene pass to **dynamic rendering**; the old property no longer exists. **Null until `BuildRenderTargets()` (`Program.cs:970`), which runs AFTER `ModLibrary.LoadAll()` (`:942`) — i.e. after `[StarMapAllModsLoaded]`. Pipeline build must stay lazy (see *Init timing* above).** Non-main viewports own their targets but build them with the same `Program.Instance.ColorFormat` + `GameSettings.GetSampleCount()` (`KSA.Rendering/ViewportRenderSurface.cs:105-107`, `KSA/ViewportBase.cs:128`), so the main-target-built pipeline stays compatible in every `RenderMainPass` call. |
| 6 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:133,134,136` | `Presets.InputAssembly.TriangleList`; `Presets.Rasterization.Fill.CullNone`; `Presets.BlendState.BlendColorAlpha` | `RenderCore.Pipelines/SimplePipelineCreator.cs:15` (+ Brutal abstractions) | Yes | None | Pipeline state presets; compile-verified by build. |
| 7 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:135` | `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` | `KSA/RenderingPresets.cs:14` (used widely, e.g. `KSA.Rendering.Water.Rendering/OceanRenderer.cs:311`) | Yes | None (file byte-identical) | Reverse-Z depth test+write; the game still clears depth to `0f`. 4730/4733 depth-prepass changes did not alter this preset. |
| 8 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:130,131`; `:50`,`TextureFactory.cs:31,35,53` | `Renderer.{Device,Allocator,Graphics,DynamicStateInfo,ViewportState}` | `Core/Renderer.cs` (byte-identical) | Yes | None | Compile-verified. |
| 9 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:252,256` | `Program.GetRenderCamera()` (= `RenderedViewport.GetCamera()`); `Camera.MVP.viewProjection` | `KSA/Program.cs:642`, `:491` (`RenderedViewport : IViewport` @5402); `KSA/Camera.cs:63`, `KSA/ViewProjection.cs:13` | Yes | **CHANGED (mod-side @5348)** — was `Program.GetMainCamera()` (`:632`); `RenderedViewport` retyped `Viewport`→`IViewport` @5402 (mod never names the type) | `RenderMainPass` runs once per **visible viewport** (main + the two always-visible 128² crew-portrait viewports since 5261; @5402 `RenderViewport` sets `_renderedViewport = viewport` at `Program.cs:4315` before the call), and ego space is camera-relative, so the main camera drew the portrait passes with the wrong clip transform. Now uses the camera of the viewport being rendered. Mod null-checks defensively. |
| 10 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:264` | `Program.SetViewport(CommandBuffer)` | `KSA/Program.cs:4293` | Yes | None (body identical; sizes to `RenderedViewport.Size`) | |
| 11 | Direct | `thug-life.lib/ThugLifeRenderManager.cs:98` | `Program.GetRenderer()` (Renderer) | `KSA/Program.cs:558` | Yes | None | Called from the lazy `EnsureGpuResources()`, not from the constructor. |
| 12 | Direct (ego transform) | `thug-life.lib/ThugLifeQuadRenderer.cs:281,282,283` | `Vehicle.GetMatrixAsmb2Ego(Camera)`; `Part.PositionEgo(ref readonly double4x4)`; `Part.Asmb2Ego(doubleQuat)`; `Vehicle.Asmb2Ego` (doubleQuat) | `KSA/Vehicle.cs:1256,501`; `KSA/Part.cs:1155,1160` | Yes | None | Per-frame model-ego matrix; caller passes `in` to the `ref readonly` param. |
| 13 | Direct (UI) | `thug-life.lib/ThugLifeSubmod.cs:123,129,138` | `Vehicle.Parts.Parts`; `Part.Template.Id`; `Part.Id`; `Part.SubParts` | `KSA/Part.cs:576,698,1079`; `KSA/PartTree.cs:95` | Yes | None | Combo population. |
| 14 | GPU lib (Brutal/RenderCore) | `ThugLifeTextureFactory.cs:33,64`; `ThugLifeQuadRenderer.cs` (pipeline/descriptor/buffers) | `SimpleVkTexture`; `VkUtils.UploadBufferToImage`/`StageAndUploadToBuffer`; `BufferEx`, `DescriptorSetLayoutEx`, `DescriptorPoolEx`, `VertexInput`, `ShaderStages`, `CommandBuffer` | `Brutal.VulkanApi(.Abstractions)`, `RenderCore`, `Core` | Yes | None (`SimpleVkTexture.cs`, `VkUtils.cs` byte-identical @5402) | **4729 bumped Brutal packages** — highest churn surface; compile against 4750 DLLs passes, so the used API is intact. |
| 15 | Abstraction | `thug-life.lib/ThugLifeSubmod.cs:104` | `VehicleProvider.GetAllVehicles()` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` | Yes | None | |
| 16 | Direct (type test) | `thug-life.lib/KittenGlassesPreset.cs:35` | `KittenEva` (`: Vehicle`) — `vehicle is KittenEva`, gates the **animate thug** button | `KSA/KittenEva.cs:13` | Yes | **NEW (mod-side, 2026-08-23)** | Type identity only, no members touched. A rename/reparent of `KittenEva` is a compile error, not silent drift. Seated kittens are not vehicles and are out of scope here. |
| 17 | Direct (UI) | `thug-life.lib/ThugLifeSubmod.cs:308` | `Vehicle.Parts.Parts` — first top-level part as the kitten fallback anchor | `KSA/PartTree.cs:95` | Yes | **NEW (mod-side, 2026-08-23)** | A `KittenEva` is constructed around its MMU backpack part as root (`KSA/EVADoor.cs:209`), so this is non-empty in practice; null-checked regardless. |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `UnlitMeshVert` | Vertex shader | `ModLibrary.Get<ShaderReference>` id | `Core/DefaultAssets.xml:53` → `Core/Shaders/Mesh/UnlitMesh.vert` | Yes | None (id, path, file all identical; md5 `71cc48a5…` @5402) |
| `UnlitMeshFrag` | Fragment shader | `ModLibrary.Get<ShaderReference>` id | `Core/DefaultAssets.xml:54` → `Core/Shaders/Mesh/UnlitMesh.frag` | Yes | None (id, path, file all identical; md5 `4f4adaa1…` @5402) |

Texture is generated programmatically (`ThugLifeTexturePattern.cs`, `R8G8B8A8UNorm`) — no
external texture asset dependency.

**Update-risk findings (5117 → 5261)**

- **CONFIRMED COMPILE BREAK — `Program.OffScreenPass` removed.** `ThugLifeQuadRenderer.cs:127,133`
  read `Program.OffScreenPass.SampleCount` and `.Pass` → **2× CS0117**.

  **This is an architecture change, not a rename.** KSA migrated the main scene pass from classic
  Vulkan render passes to **dynamic rendering**. The offscreen target is now
  `Program.OffscreenTarget` (`RenderTarget : IRenderPassInfo`) — the same object assigned to
  `PassContext.MainOpaquePass` — and it has **no `.Pass` and no `.SampleCount`** (it exposes
  `Samples`). `IRenderPassInfo` now declares exactly one member:
  `SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)`, which:
  - chains a `VkPipelineRenderingCreateInfo` (colour/depth/stencil formats) onto `info.Next`,
  - sets `info.RenderPass = VkRenderPass.NullHandle`,
  - overwrites `MultisampleState` with the target's `Samples`,
  - supplies `ViewportState` when absent.

  → **Fix:** `BuildPipeline` no longer sets `RenderPass`, `Subpass` or a hand-rolled
  `MultisampleState`; it calls `Program.OffscreenTarget.SetupGraphicsPipeline(ref info)` immediately
  before `CreateGraphicsPipeline`. This mirrors the game's own main-pass pipelines
  (`KSA/GenericMeshRenderer.cs:305`, `KSA/PartModelRenderer.cs:184,269`, `KSA/PartModelGlass.cs:269`).
  **The call must stay immediately before pipeline creation** — the structures it points `pNext` at
  are owned by the `RenderTarget` and overwritten on every call.

- ⚠️ **Originated in the unvalidated 5118–5168 window**, not in 5261: `Program.OffScreenPass` exists
  at tag `2026.8.3.5117` (`Program.cs:411`) and is absent from both `5168` and `5261`. It is **not**
  a regression introduced by this build.
- ⚠️ **Needs a live pass (F12).** thug-life drives its own Vulkan pipeline, descriptor set, VB/IB and
  texture upload; only an in-game look confirms the quad still rasterizes. The mod's reverse-Z depth
  preset (`RenderingPresets.ReverseZDepthStencil.DepthTestWrite`) and blend state are unchanged.
- ✅ `SuperMeshRenderSystem.RenderMainPass(CommandBuffer)` — **signature-identical** (line shift only),
  so the postfix still attaches. `UnlitMesh.vert` / `UnlitMesh.frag` and both shader ids
  (`UnlitMeshVert`, `UnlitMeshFrag`) are **byte-identical** this span.
- ✅ `PartTree.CreateFromNewPartTree`, `EngineController.SetIsActive(Vehicle?, bool)`, the three
  `*Module.UpdateRenderData` prefixes and `PartModel.AddInstance` are all signature-identical.
  `PerInstanceData`'s byte layout is **identical**, so the padding-byte hijack remains safe.
- ⚠️ **blinky's default engine part id does not exist** — `"CorePropulsionA_Prefab_EngineA1"`
  (`blinky.lib/LcdGridConfig.cs:47`, `BlinkySubmod.cs:51`). It was removed from
  `Content/Core/CorePropulsionAAssets.xml` **between 5018 and 5117** (absent at 5117/5168/5261); only
  `EngineA2`–`EngineA6` exist (`A2` "LR91 Sea", `A3`/`A6` "LR91 Vac", `A4` "VTR-10", `A5` "LR91 Vac +
  Verniers"). `ModLibrary.Get` throws on a missing id, making this a concrete candidate explanation
  for the **"blinky broken"** entry in [`../ISSUES.md`](../../ISSUES.md) — the 5117 pass checked blinky's
  patch targets (all byte-identical) but never its part id. **Pre-existing; recommend defaulting to
  `EngineA2`. Not changed here** (behavioral, outside the compile-blocking scope).

**Update-risk findings (4750 -> 5018)**

- ✅ **No breaking deltas.** `SuperMeshRenderSystem.RenderMainPass(CommandBuffer)` is
  signature-identical. `SuperMeshRenderSystem.cs` did change (+32 lines) but **only in the
  shadow/CSM path**: `RenderShadowPass` gained a `cascadeIndex` parameter and pushes it as a push
  constant, depth pipelines switched `SetPushConstant<InstanceData>` → `SetPushConstant<int>`, a
  `SetCsmFilterSpecConstant` helper was added, and several `AddMacroDefinition` calls were collapsed
  to the shorter overload. thug-life postfixes the **main** pass and touches none of it.
- ✅ **`UnlitMesh.vert` / `UnlitMesh.frag` are byte-identical 4750→5018** (they do not appear in the
  Content diff at all), and their `DefaultAssets.xml` ids are unchanged.
- ✅ **Pipeline assumptions are read dynamically, so render-state churn is absorbed.**
  `ThugLifeQuadRenderer` reads `Program.OffScreenPass.SampleCount`, `Program.OffScreenPass.Pass` and
  `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` at build time rather than hard-coding an MSAA
  count or depth mode — an MSAA/format change would be picked up automatically.
- ⚠ **Watch (visual-only, needs a live pass):** this span added a lot of render work around the
  offscreen pass — screenspace particles (`ScreenspaceParticleRenderer` + new `Composite.frag`),
  `MilkyWayRenderer`, volumetric trails, extruded shadow-cascade frusta (rev 4982), and CSM filter
  spec constants. None of it moves an API thug-life binds to, but a depth/MSAA behavioral change here
  manifests as a mis-drawn quad rather than a crash. **Re-verify visually.**

#### Carried over from the 4680 -> 4750 review

- No breaking deltas. The single Harmony target (`SuperMeshRenderSystem.RenderMainPass`)
  is signature-identical and even same-line; the offscreen-pass + camera + ego-transform
  APIs all match.
- **Shader merges do not affect thug-life.** 4693 (MeshIndirect merge), 4745
  (ModelGlass+ModelEye), 4701/4747 (`MeshIndirect.frag` / `ModelTranslucent.frag`
  cleanups) all touch `MeshIndirect.*` / `Model*.*`; thug-life uses only
  `UnlitMesh.vert`/`UnlitMesh.frag`, whose ids, paths, and files are unchanged.
- **Watch items (low, currently green):** (a) 4729 Brutal package bump is the largest
  potential break surface for the Vulkan calls in #14, but the 4750 build passes; (b)
  4694 offscreen/thumbnail-viewport fixes and 4730/4733 depth-prepass changes share the
  offscreen pass thug-life renders into — signatures intact, but a *behavioral* depth/MSAA
  change here would manifest as a visual glitch rather than a crash. Re-verify visually
  after large render-system updates.

---

## Area summary — Update-risk findings (5261 → 5348)

- ⚠️ **thug-life — the render environment moved under it; two items are not statically clearable.**
  Every binding is intact: `SuperMeshRenderSystem.RenderMainPass(CommandBuffer)` signature unchanged,
  `Program.OffscreenTarget` unchanged (`KSA/Program.cs:438`) so the 5261 dynamic-rendering rebuild still
  applies, and `UnlitMesh.vert`/`UnlitMesh.frag` are **byte-identical** with their `UnlitMeshVert` /
  `UnlitMeshFrag` ids still in `Content/Core/DefaultAssets.xml`. What changed around it:
  1. **Rev 5315 — Vulkan 1.3 → 1.4.** thug-life builds its own pipeline, descriptor set, VB/IB and
     texture upload against the game's device. 1.4 is backward compatible; exercise it once in game.
  2. **Rev 5283 — UI coverage culling.** `Content/Core/Shaders/UiCoverage/*` (seven new ids in
     `DefaultAssets.xml`) plus `GaugeCanvas.RegisterOpaqueCoverage`; expensive shaders are skipped behind
     opaque UI. thug-life's quad is a **postfix on the main pass** and registers no coverage, so it should
     be unaffected — but a mis-culled or z-fighting quad only shows in game.
- ✅ **blinky / its-so-shiny — the O(N³) power DFS is gone, and this is favourable.** Rev 5326 reworked
  vehicle power onto `PartTree.ElectricalCircuits` and moved `PowerManager.PopulateGraph` out of the
  constructor (`KSA/PowerManager.cs:14` @5261) into `OnDrawUi`, behind
  `if (base.ShowFlow && !_displayGraphBuilt)` (`:130-138` @5348) — **the graph is now built only when
  "Draw Graph" is ticked in the part window.** The changelog reports a 4500-consumer craft going from
  3.3 s to ~0.3 ms per power rebuild.

  `blinky.lib/LcdGridBuilder.cs:319` splits grids specifically *"to reduce ResourceManager.PopulateGraph
  cost from O(N³) to O(N³/K²)"* (see also `:62`, `:114`), and `its-so-shiny.lib/ShinyGridBuilder.cs:42`
  places *"distinct battery anchors [for] the per-PowerConsumer DFS in PowerManager.PopulateGraph"*.
  Both now solve a problem the game no longer has. **No change made** — removing those optimisations is a
  behavioral change to grid construction and belongs in its own task.

  Counterweight: `Part.Modules` is now `new ModuleList(keepModuleIdIndex: true)` for **every** part, so
  blinky's thousands of pixel parts each carry an id index. Worth a perf glance in game.
- ✅ **blinky's diagnostics still resolve.** `ResourceManagerBase.PopulateGraph` remains and
  `ResourceManager` is still reached via the `core is Combustor` test. (The former reflective read of
  `NearestToFurtherestNode*` was replaced by the typed `ConsumptionOrder` property on 2026-08-23 — #19;
  no string reflection remains in `blinky.lib`.)
- ✅ **All three render-skip prefixes unchanged** —
  `PartModelModule`/`PartModelDynamicModule`/`PartModelGlassModule.UpdateRenderData(in double4x4, bool,
  Viewport, int)`. `PartTree.CreateFromNewPartTree(Part)` and `EngineController.SetIsActive(Vehicle?, bool)`
  are also unchanged (`EngineController` additionally gained `ISequenced` + a `Sequence` property, rev 5329
  — additive).
- ⚠️ **Lights now register for every viewport** (rev 5301 `ViewportLightModes`): `LightModule.cs:125,141`
  went from `else if (viewport == Program.MainViewport)` to a bare `else`. its-so-shiny's grids light more
  viewports than before (including crew-portrait cams) — check for double-lighting or a cost spike.
- ℹ️ Rev 5333 fixed *"deactivating an engine mid-burn would leave it stuck on forever"*, and rev 5318 fixed
  *"assigning a part to sequence 0 silently zeroing the vehicle's delta-v and TWR"* — both are game bug
  fixes in paths blinky drives.
- ✅ **Closed 2026-08-23 — `LcdGridConfig.EnginePartId` now defaults to `EngineA3`**, and `EngineA1`
  is gone from `BlinkySubmod.EnginePresets`. It was absent from Content since before 5117 and
  `ModLibrary.Get` throws for it.
- ✅ **Closed 2026-08-23 — the real *"blinky broken"* root cause was the propellant feed**, not the
  part id. See *Root cause of "blinky broken"* above and integration points #11, #22–#24. The part-id
  bug only bit callers that used the config default; the feed bug killed every grid regardless.

---

## Area summary — Update-risk findings (5348 → 5402)

Revisions 5349–5400 are **unlogged** in any changelog (the only recorded commit is rev 5401 *"Fixed
crash for incorrect data stride for thumbnail rendering"*); everything below comes from the source
diff. Headline: the `Viewport` class was replaced by `IViewport`/`IGameViewport`/`GameViewport`/
`ViewportBase`/`ViewportRegistry` with a fixed pool of 8 shader slots and per-viewport
`ViewportOptionFlags`. The solution builds clean against 5402; none of these three mods needed a
code change.

- ✅ **blinky / its-so-shiny — clean.** All three render-skip targets are still single, name-resolvable
  methods; only their 3rd parameter was retyped `Viewport` → `IViewport`
  (`KSA/PartModelModule.cs:87`, `PartModelDynamicModule.cs:55`, `PartModelGlassModule.cs:72`) and the
  prefixes bind nothing but `__instance`. `PartModel.AddInstance` / `PartModelGlass.AddInstance` gained
  an early-return on `!viewport.HasAny(ViewportOptionFlags.RenderPartModels)` (`PartModel.cs:410`,
  `PartModelGlass.cs:504`) and the raytracing gate became `viewport.HasAll(UseRaytracing)` — all four
  presets in `KSA/ViewportPresets.cs` (main, secondary, part-thumbnail, character-portrait) carry
  `RenderPartModels` and only the main one `UseRaytracing`, so nothing changes for pixel parts.
  `PartModel.PerInstanceData` is **byte-identical** (`float4x4 ModelMatrix; int StateBitFlag; uint
  EmissiveColor; int packing1; float Wetness`). `EngineController.cs`, `RocketCore.cs`,
  `ResourceGroupList.cs`, `Battery.cs`, `Module.cs` are byte-identical; `Part.cs`, `PartTree.cs`,
  `Vehicle.cs`, `ResourceManager(Base).cs` changed only by `IViewport` signatures on UI methods plus
  additive members (parachutes, `PartStructuralLimits`/`CrashTolerance`, `SubPartGroupRef`,
  `IsLightSwitchedOff`). `CreateFromNewPartTree`, `SetIsActive`, `Connection.Connect`, `SetStage`,
  `UpdateVehicleConfiguration` bodies are unchanged.
- ℹ️ **its-so-shiny — the game's light-hide chain moved.** `PartModelModule`/`PartModelDynamicModule`
  now call `Part.IsLightSwitchedOff()` (`KSA/Part.cs:1357`, also requires the new
  `PowerConsumer.IsSwitchedOn()` `:50` and same-tree ownership) instead of the inline
  `LightSwitch.LightIsActive` + power-state chain. `ShinyPatches.ShouldRenderShinyPart` reads
  `LightIsActive` only, exactly as before — unchanged behaviour, but it now diverges from the game's
  test when the bus is unpowered (mesh shown, light dark). `LightModule.UpdateRenderData` still
  registers a light instance for every viewport (`LightModule.cs:118,134`); `LightPart` content
  byte-identical.
- ✅ **thug-life — bindings intact, environment moved again; needs a live pass (F12).**
  `SuperMeshRenderSystem.RenderMainPass(CommandBuffer)` is signature-identical (`:338`→`:347`); the only
  body change wraps draws of the new `MeshRendererSkinnedPbrTwoSided` technique (parachute canopies —
  `ModelPbr.frag` now flips the normal on `gl_FrontFacing`) in a profiler tag. `Program.OffscreenTarget`
  (`:457`), `GetRenderCamera()` (`:642`), `SetViewport` (`:4293`), `RenderTarget.SetupGraphicsPipeline`
  (byte-identical) and `RenderingPresets` (byte-identical, reverse-Z) are unchanged.
  `UnlitMesh.vert`/`.frag` **and** `MeshIndirect.vert`/`.frag` are **byte-identical**; `DefaultAssets.xml`
  gained one `StaticObjectPrePassIndirectFrag` line, moving nothing thug-life reads. What moved:
  `RenderMainPass` is still called once per visible non-main viewport from `RenderViewport`
  (`Program.cs:4395`) with `_renderedViewport` set first (`:4315`), but those viewports now **own** their
  render surfaces (`KSA.Rendering/ViewportRenderSurface.cs`) instead of sharing `Viewport.OffscreenTarget`
  construction — built with the same `ColorFormat` and `GameSettings.GetSampleCount()` (`:105-107`,
  `ViewportBase.cs:128`), so the pipeline built against the main target remains compatible; the
  part-thumbnail viewport (rev 5401's stride fix) never runs `RenderMainPass`. Only an in-game look
  confirms the quad still rasterizes in the main view and the crew portraits.
- ✅ **Content.** `CorePropulsionAAssets.xml`'s only change is `CrashTolerance="3e6"` on
  `EngineA2..A6` (`:446,531,616,648,770`, new `PartTemplate.CrashTolerance` → `Part.CrashTolerancePascals`
  for the new `PartFailure` system); `CorePropulsionAGameData.xml`, `PartAssets.xml`,
  `CoreElectricalAGameData.xml` byte-identical. ⚠ Pixel engines now carry a crash tolerance — a grid
  scraped along the ground could shed parts under the new structural-limit rules; not statically
  clearable.
- ℹ️ Not this area's surface, for context: `Cursor.InputRay` was removed (graffiti fix, see
  `decals.md`), `GridPass`/`SingleToMultisamplePass`/`GlobalShaderBindings` went to a fixed 8 shader
  slots, `GizmosRenderer.MAX_GIZMO_INSTANCES` 131072 → 655360, and `GenericGizmo.GetSegmentDataByViewport`
  takes `IViewport` (dont-stifle-me, see `part-editor-and-robotics.md`).
- **Needs a live pass:** thug-life quad in main + portrait viewports (above); a blinky grid lit
  once (the render path is unchanged, but rev 5401 and the per-viewport render-surface rework are
  render-loop changes); a shiny grid toggled with "Render light meshes" off to confirm the mesh still
  follows `LightIsActive`.
