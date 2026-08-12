# Pixel-Grid & Custom-Render Mods — Game Integration Scope

Permanent reference for detecting when KSA game updates break the pixel-grid and
custom-render mods (`blinky`, `its-so-shiny`, `thug-life`). Every game-facing member,
Harmony target, GPU/render API, shader, and part template these mods touch is
enumerated and verified against decompiled sources **and** the Content asset tree.

**Verified game versions**

- NEW decomp `2026.6.9.4750` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- OLD decomp `2026.6.8.4680` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`
- NEW Content root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\Content`
- OLD Content root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\Content`

Paths in the **Decomp/Content path (NEW)** column are relative to the NEW decomp root
(namespace-foldered, e.g. `KSA/Part.cs`) or the NEW Content root (e.g.
`Core/DefaultAssets.xml`). **Mod code** paths are relative to the repo root
`C:\Users\Alex\repos\meow-sci\unscience`.

**How these mods are hosted (all three)**

- All reusable game-facing logic lives in the `*.lib` project (`blinky.lib`,
  `its-so-shiny.lib`, `thug-life.lib`); each exposes an `ISubmod`
  (`MeowSci.KsaAbstractions.ISubmod`) consumed two ways:
  1. Standalone StarMap mod (`blinky/Mod.cs` F11, `its-so-shiny/Mod.cs` F11,
     `thug-life/Mod.cs` F12) — own ImGui window + own `Harmony` instance in its `Patcher.cs`.
  2. Embedded in the **unscience** supermod (`unscience/Mod.cs:65` `new BlinkySubmod()`,
     `:76` `new ItsSoShinySubmod()`, `:83` `new ThugLifeSubmod()`) as collapsible sections,
     with all three patch sets applied on the **single** supermod Harmony instance
     (`unscience/Patcher.cs:35` `ThugLifeRenderPatches.Apply`, `:38` `BlinkyPatches.Apply`,
     `:39` `ShinyPatches.Apply`).
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

## blinky

**Purpose** — Builds NxM LCD-style pixel grids out of real engine parts at runtime and
attaches them to a live vehicle. Each pixel is an a/b engine pair (net-zero thrust);
pixels are toggled by activating/deactivating their `EngineController`s. Supports
multiple named grids per vehicle, patterns, scrolling, static display, global scan, and
a render-skip performance toggle. Controllable via ImGui and via unladen-swallow RPC.

**Unscience integration** — `BlinkySubmod : ISubmod` (`blinky.lib/BlinkySubmod.cs:11`),
instantiated by the supermod (`unscience/Mod.cs:65`) and the standalone host
(`blinky/Mod.cs:27`). Static singleton `BlinkyGridManager` (`blinky.lib/BlinkyGridManager.cs:38`)
is the shared control surface for both the UI and RPC. Render-skip patches applied via
`BlinkyPatches.Apply` (`blinky/Patcher.cs:15` standalone, `unscience/Patcher.cs:38` embedded).

**UI/hotkeys** — Standalone window "blinky", 480x640, `MenuBar`, toggled by **F11**
(`blinky/Mod.cs:52,79`). Create form (size/spacing/scale/offset/layout/engine/vehicle/
grid-name), per-grid pattern + destroy + diagnose sections, "Render engine meshes"
checkbox, Debug menu "Scan for blinky grids". All ImGui via `Brutal.ImGuiApi`.

**Persistence** — None to disk. Grids live as real parts in the vehicle's `PartTree`
(so they survive in a saved vehicle as ordinary engine parts); the manager's in-memory
registry is rebuilt by the **global scan** which re-parses `pixel_{grid}_{row}_{col}_{a|b}`
part Ids. No StarMap save hooks.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature, or asset path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony prefix | `blinky.lib/BlinkyPatches.cs:26,30,60` | `PartModelModule.UpdateRenderData(in double4x4, bool, Viewport, int)` — return false skips submit | `KSA/PartModelModule.cs:79` | Yes | None (`:79`→`:79`) | Patched by name only; no overload (1 method). Game itself uses `Parent.FullPart.LightSwitch` here. |
| 2 | Harmony prefix | `blinky.lib/BlinkyPatches.cs:27,31,66` | `PartModelDynamicModule.UpdateRenderData(in double4x4, bool, Viewport, int)` | `KSA/PartModelDynamicModule.cs:55` | Yes | None (`:55`→`:55`) | Same shape as #1. |
| 3 | Harmony prefix | `blinky.lib/BlinkyPatches.cs:28,32,72` | `PartModelGlassModule.UpdateRenderData(in double4x4, bool, Viewport, int)` | `KSA/PartModelGlassModule.cs:69` | Yes | None (`:69`→`:69`) | 4745 merged ModelGlass+ModelEye **shaders**; the C# module class is unchanged. |
| 4 | Direct (in prefix) | `blinky.lib/BlinkyPatches.cs:63,69,75` | `Module.Parent`→`Part`; `Part.FullPart`; `Part.Id` (string `StartsWith("pixel_")`) | `KSA/Module.cs:268`, `KSA/Part.cs:659`, `Part.Id` field | Yes | None | `Module.Parent` is `required Part`; `FullPart => PartParent ?? this`. |
| 5 | Direct | `blinky.lib/LcdGridBuilder.cs:51` | `ModLibrary.Get<PartTemplate>(string id)` (string-keyed lookup) | `KSA/ModLibrary.cs:968` | Yes | None | `Get<T>(string) where T:IKeyed`; throws if id missing. Runtime string id (see assets). |
| 6 | Direct | `blinky.lib/LcdGridBuilder.cs:268` | `new Part(string inName, PartTemplate inTemplate, PartInstance?=null, Part?=null)` | `KSA/Part.cs:765` | Yes | None (`:764`→`:765`) | |
| 7 | Direct | `blinky.lib/LcdGridBuilder.cs:135,239`; `:149,247` | `PartTree.CreateFromNewPartTree(Part rootPart)`; `Vehicle.UpdateVehicleConfiguration()` | `KSA/PartTree.cs:117`; `KSA/Vehicle.cs:1263` | Yes | None (`:114`→`:117`; `:1218`→`:1263`) | Core build/destroy path; both unchanged. |
| 8 | Direct | `blinky.lib/LcdGridBuilder.cs:37,237,135` | `Vehicle.Parts` (PartTree, get+set); `PartTree.Root`; `PartTree.Parts` | `KSA/Vehicle.cs:264`; `KSA/PartTree.cs` | Yes | None | `Vehicle.Parts` is a public field. |
| 9 | Direct | `blinky.lib/LcdGridBuilder.cs:103-104,228-230` | `Part.TreeParent` (Part?); `Part.TreeChildren` (List<Part>) | `KSA/Part.cs:385,387` | Yes | None | Manual tree wiring. |
| 10 | Direct | `blinky.lib/LcdGridBuilder.cs:124,127` | `Part.SetStage(int)`; `Part.Stage` (get) | `KSA/Part.cs:731`, `:517` | Yes | None (`:730`→`:731`) | |
| 11 | Direct | `blinky.lib/LcdGridBuilder.cs:352,214,216` | `Part.Connection.Connect(IConnector, IConnector)`; `Part.Connections` (List<Connection>); `Connection.Disconnect()` | `KSA/Part.cs:285,391,301` | Yes | None (`:284`→`:285`) | `Connect` takes `IConnector` (Part implements it), **not** `(Part,Part)`. |
| 12 | Direct | `blinky.lib/LcdGridBuilder.cs:299,302,305` | `Part.PositionParentAsmb` (double3); `Part.Asmb2ParentAsmb` (doubleQuat); `Part.Scale` (double3) — all settable | `KSA/Part.cs:449,463,499` | Yes | None | |
| 13 | Direct | `blinky.lib/LcdGridBuilder.cs:327,469`; `PixelGrid.cs:47,90` | `Part.SubtreeModules` (ModuleList); `ModuleList.Get<T>()` for `Tank`, `EngineController` | `KSA/Part.cs:409`; `KSA/ModuleList.cs`; `KSA/Tank.cs`, `KSA/EngineController.cs` | Yes | None | `Get<T>()` returns array (`.Length`/index used). |
| 14 | Direct | `blinky.lib/LcdGridBuilder.cs:326,377,324` | `Part.IsSubPart`; `Part.Template` (PartTemplate); `Vehicle.Parts.Parts` | `KSA/Part.cs:657,323` | Yes | None | |
| 15 | Direct | `blinky.lib/LcdGridBuilder.cs:472` | `EngineController.MinimumThrottle` (float, settable) | `KSA/EngineController.cs` | Yes | None | |
| 16 | Direct | `blinky.lib/BlinkyGridManager.cs:224,252,266`; `NonLcdEngineCache.cs:46` | `EngineController.SetIsActive(Vehicle?, bool)` — pixel on/off | `KSA/EngineController.cs:46` | Yes | None | Called with `null` vehicle arg. |
| 17 | Direct | `blinky.lib/NonLcdEngineCache.cs:36` | `EngineController.IsActive` (get) | `KSA/EngineController.cs:24` | Yes | None | |
| 18 | Direct | `blinky.lib/BlinkyGridManager.cs:258` | `Vehicle.SetEnum(Enum?)` with `VehicleEngine.MainIgnite` | `KSA/Vehicle.cs:4838`; `KSA/VehicleEngine.cs:5` | Yes | None | Ignites vehicle before lighting pixels. |
| 19 | Reflection (string field names) | `blinky.lib/BlinkySubmod.cs:623-624` | `ResourceManagerBase.NearestToFurtherestNodeSameStage` / `NearestToFurtherestNode` (public instance fields, looked up via `rm.GetType().BaseType.GetField(name)`) | `KSA/PowerManager.cs` (base `ResourceManagerBase`), used throughout | Yes | None | **Diagnose-only debug path.** String-named; would silently print null if renamed. Not on any normal code path. |
| 20 | Direct (debug) | `blinky.lib/BlinkySubmod.cs:586-587,612-618,645` | `Vehicle.GetManualThrottle()`; `Vehicle.FlightComputer`; `EngineController.Cores` (RocketCore[]); `RocketCore.ResourceManager`; `ResourceManager.FlowRule`; `Connection.OtherPart(Part)` | `KSA/Vehicle.cs:822,415`; `KSA/EngineController.cs:18`; `KSA/RocketCore.cs:14`; `KSA/Part.cs:264` | Yes | None | Diagnose button only. `FlowRule` enum + `NearestToFurtherestSameStage` member present (`KSA/FlowRule.cs`). |
| 21 | Abstraction | `blinky.lib/BlinkyGridManager.cs:280`; `BlinkySubmod.cs` | `VehicleProvider.GetAllVehicles()` / `GetControlledVehicle()` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` (repo lib) | Yes | None | Game coupling lives in ksa-abstractions scope. |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `CorePropulsionA_Prefab_EngineA1` | Engine part template (default in `LcdGridConfig.cs:47`) | `ModLibrary.Get<PartTemplate>` id | `<Part Id=...>` in `Core/CorePropulsionAAssets.xml:466` | Yes | None | 
| `CorePropulsionA_Prefab_EngineA2..A6` | Engine part templates (UI presets `BlinkySubmod.cs:49-57`; default A3) | `ModLibrary.Get<PartTemplate>` id | `Core/CorePropulsionAAssets.xml` + `<PartGameData>` in `Core/CorePropulsionAGameData.xml:99,154,209,248,321` | Yes | None |

Note: `EngineA1` exists as a **`<Part>` template** (so the `ModLibrary.Get<PartTemplate>`
default resolves) but is **not** in the build-menu catalog (`CorePropulsionAGameData.xml`
lists only A2–A6 + `EngineA1_Dev`). This catalog asymmetry is identical in OLD and NEW —
not a 4750 delta — and is harmless because blinky bypasses the catalog and instantiates
the template directly. The UI default (A3) is fully cataloged.

**Update-risk findings (4750 -> 5018)**

- 🔴 **BREAKING (fixed): `RocketCore.ResourceManager` moved.** 5018 rewrote the fuel/resource feed
  model: `RocketCore` no longer owns a `ResourceManager` — it now exposes `FeedConnectors`,
  `ResolvedConsumerFeeds`, `TryPrepareDrain`/`TryAccumulateDrain` and a `DrainContext`. The
  `ResourceManager` (and its `FlowRule`) live on the **`Combustor`** subclass; the other
  `RocketCore` subclass, the new **`SolidMotor`**, legitimately has none. `BlinkySubmod.DiagnoseGrid`
  now tests `core is Combustor` before reading it. Diagnostics-only path — no functional impact.
- ✅ **Engine activation is unaffected by the rev-4914 control-module lockout.**
  `EngineController.SetIsActive(Vehicle?, bool)` and `ThrusterController.SetIsActive` are
  **byte-identical** to 4750 — the new lockout ("no vehicle control module" disables the Active
  checkboxes, Decouple and the staging key) was implemented in the **UI layer only**. blinky drives
  engines through `SetIsActive(null, on)` and is not gated by it.
- ✅ **Staging reads survive the staging rewrite.** `StageList.cs` and `Staging.cs` were deleted in
  favour of `ResourceGroups`/`ResourceGroupList`/`SequencePerformance*`, but `Part.Stage` and
  `Part.SetStage(int)` are unchanged, so `LcdGridBuilder`'s stage alignment is still correct. Note
  rev 4873 changed `SetStage`'s internals (bulk-guarded rebuilds — previously every `SetStage` rebuilt
  every resource manager), so bulk stage assignment is now cheaper, not different.
- ✅ All three render-skip targets (#1–#3) are still byte-identical in signature; the
  `Parent.FullPart.LightSwitch.LightIsActive` chain is intact (`LightIsActive` is on `PowerConsumer`).
- ✅ `PartTree.CreateFromNewPartTree(Part)` and the part-tree build/destroy API are unchanged.

#### Carried over from the 4680 -> 4750 review

- No breaking deltas. All three render-skip targets (#1–#3) are byte-identical in
  signature and even line number; the `Parent.FullPart.LightSwitch.LightIsActive` chain
  the game uses inside those very methods is intact.
- 4693 (MeshIndirect merge) / 4745 (ModelGlass+ModelEye shader merge) do **not** affect
  blinky: it prefix-returns `false` to skip the *entire* `UpdateRenderData`, never
  touching MeshIndirect internals, shader layouts, or the glass module's render body.
- The whole part-tree build/destroy API (#5–#14, the actual functional core) is unchanged.
- Only latent fragility is the **Diagnose-only** string-field reflection (#19); not on
  any normal path and currently valid in NEW.

---

## its-so-shiny

**Purpose** — Same grid concept as blinky but each pixel is a stock `LightPart` instead
of an engine. Pixels are toggled through the light's `PowerConsumer` light switch
(`LightIsActive`); color/intensity are applied per `PartTemplate` via Zippo's
`LightController`. Grids connect to battery-bearing parts for power.

**Unscience integration** — `ItsSoShinySubmod : ISubmod`
(`its-so-shiny.lib/ItsSoShinySubmod.cs:11`), instantiated by the supermod
(`unscience/Mod.cs:76`) and standalone host (`its-so-shiny/Mod.cs:27`). Static
`ShinyGridManager` (`its-so-shiny.lib/ShinyGridManager.cs:31`) is the control surface.
Render-skip patches via `ShinyPatches.Apply` (`its-so-shiny/Patcher.cs:15`,
`unscience/Patcher.cs:39`). Color/intensity reuse `MeowSci.ZippoLib.LightController`
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
| 1 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs:25,29,66` | `PartModelModule.UpdateRenderData(in double4x4, bool, Viewport, int)` | `KSA/PartModelModule.cs:79` | Yes | None (`:79`→`:79`) | Coexists with blinky #1 (different Id prefix). |
| 2 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs:26,30,69` | `PartModelDynamicModule.UpdateRenderData(in double4x4, bool, Viewport, int)` | `KSA/PartModelDynamicModule.cs:55` | Yes | None (`:55`→`:55`) | |
| 3 | Harmony prefix | `its-so-shiny.lib/ShinyPatches.cs:27,31,72` | `PartModelGlassModule.UpdateRenderData(in double4x4, bool, Viewport, int)` | `KSA/PartModelGlassModule.cs:69` | Yes | None (`:69`→`:69`) | |
| 4 | Direct (in prefix) | `its-so-shiny.lib/ShinyPatches.cs:57-63` | `Module.Parent`→`Part`; `Part.FullPart`; `Part.Id`; `Part.LightSwitch` (PowerConsumer?); `PowerConsumer.LightIsActive` (bool) | `KSA/Module.cs:268`; `KSA/Part.cs:659,407`; `KSA/PowerConsumer.cs:28` | Yes | None | Mesh shown iff light active; the same chain the game uses in #1. |
| 5 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:27` | `ModLibrary.Get<PartTemplate>("LightPart")` | `KSA/ModLibrary.cs:968` | Yes | None | Runtime string id (see assets). |
| 6 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:157` | `new Part(string, PartTemplate, ...)` | `KSA/Part.cs:765` | Yes | None | |
| 7 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:94,147`; `:98,148` | `PartTree.CreateFromNewPartTree(Part)`; `Vehicle.UpdateVehicleConfiguration()` | `KSA/PartTree.cs:117`; `KSA/Vehicle.cs:1263` | Yes | None | |
| 8 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:17,146,76-77` | `Vehicle.Parts`/`PartTree.Root`; `Part.TreeParent`/`Part.TreeChildren` | `KSA/Vehicle.cs:264`; `KSA/Part.cs:385,387` | Yes | None | |
| 9 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:205,211` | `PartTree.Modules.Get<Battery>()`; `Battery.Parent`→`Part`; `Part.FullPart` | `KSA/Battery.cs:7`; `KSA/Module.cs:268`; `KSA/Part.cs:659` | Yes | None | Battery anchors for power partitioning. |
| 10 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:87,221,131,133` | `Part.SetStage(int)`; `Part.Connection.Connect(IConnector,IConnector)`; `Part.Connections`; `Connection.Disconnect()` | `KSA/Part.cs:731,285,391,301` | Yes | None | |
| 11 | Direct | `its-so-shiny.lib/ShinyGridBuilder.cs:181,185,186` | `Part.PositionParentAsmb`; `Part.Asmb2ParentAsmb`; `Part.Scale` | `KSA/Part.cs:449,463,499` | Yes | None | |
| 12 | Direct | `its-so-shiny.lib/ShinyPixelCell.cs:24,27`; `ShinyGridBuilder.cs:234,236` | `Part.LightSwitch` (PowerConsumer?); `PowerConsumer.LightIsActive` (set) | `KSA/Part.cs:407`; `KSA/PowerConsumer.cs:28` | Yes | None | Primary pixel on/off path. |
| 13 | Direct | `its-so-shiny.lib/ShinyPixelGrid.cs:147,150`; `ShinyGridManager.cs:195` | `Part.Template`; `Part.SubParts` (ReadOnlySpan<Part>) | `KSA/Part.cs:323,655` | Yes | None | Recursive light-part discovery. |
| 14 | Transitive (ZippoLib) | `ShinyPixelCell.cs:31,36-37`; `ShinyGridManager.cs:218-223` | `LightController.{ApplyColor,ApplyIntensity,GetLightComponents,WriteColor,WriteIntensity,HasLights}` (operates on light template render data) | `MeowSci.ZippoLib` (repo lib; game coupling catalogued in zippo scope) | Yes | None | Color/intensity writes; verify in zippo scope file. |
| 15 | Abstraction | `ShinyGridManager.cs:155`; `ItsSoShinySubmod.cs` | `VehicleProvider.GetAllVehicles()`; `PartHelpers.GetAllParts` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` | Yes | None | |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `LightPart` | Light part template (default `ShinyGridConfig.cs:11`) | `ModLibrary.Get<PartTemplate>` id | `<Part Id="LightPart">` `Core/PartAssets.xml:19`; `<PartGameData Id="LightPart">` with `<PowerConsumer LightSwitch="true">` `Core/CoreElectricalAGameData.xml:221` | Yes | None |

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
(`unscience/Mod.cs:83`) and standalone host (`thug-life/Mod.cs:27`). The render postfix
is applied via `ThugLifeRenderPatches.Apply` (`thug-life/Patcher.cs:18`,
`unscience/Patcher.cs:35`) and dispatches to the static
`ThugLifeRenderManager.Instance`/`.Active` so each host (standalone vs supermod) drives
its own manager on its own assembly load context. GPU resources own pipeline + texture +
buffers; render disables itself on first error (`ThugLifeRenderManager.cs:80-84`).

**UI/hotkeys** — Standalone window "Thug Life", 500x600, **F12**
(`thug-life/Mod.cs:51,78`). Create form (vehicle / part / optional subpart filtered
combos + position/rotation/width/height), per-entry transform + Visible + Remove sections.

**Persistence** — None. Entries are in-memory only (`ThugLifeRenderManager._entries`);
lost on reload. No StarMap save hooks, no disk I/O.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature, or shader/asset path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `thug-life.lib/ThugLifeRenderPatches.cs:19-21,44` | `SuperMeshRenderSystem.RenderMainPass(CommandBuffer commandBuffer)` — postfix records quad draws into the active offscreen pass | `KSA/SuperMeshRenderSystem.cs:329` | Yes | None (`:329`→`:329`) | Single method, patched by name. Called 3x from `KSA/Program.cs` (3902/4104/4248). |
| 2 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs:114` | `ModLibrary.Get<ShaderReference>("UnlitMeshVert")` | id→path in `Core/DefaultAssets.xml:66`; file `Core/Shaders/Mesh/UnlitMesh.vert` | Yes | None (`:62`→`:66`) | Stock shader; **not** MeshIndirect/Model* — untouched by 4693/4745. |
| 3 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs:115` | `ModLibrary.Get<ShaderReference>("UnlitMeshFrag")` | id→path in `Core/DefaultAssets.xml:67`; file `Core/Shaders/Mesh/UnlitMesh.frag` | Yes | None (`:63`→`:67`) | Frag hard-writes `alpha=1.0` (cut-out via geometry, per renderer comment). |
| 4 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:117` | `RenderTechnique.CreateShaderStages(Device, Span<ShaderReference>, Span<VkSpecializationInfo>=default)` | `RenderCore/RenderTechnique.cs:37` | Yes | None (`:36`→`:37`) | `ShaderReference : FileReference, IKeyed` (`KSA/ShaderReference.cs:20`). |
| 5 | Direct (render-pass) | `thug-life.lib/ThugLifeQuadRenderer.cs:139` | `Program.OffscreenTarget.SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)` — `RenderTarget : IRenderPassInfo` | `KSA/Program.cs:432`, `KSA.Rendering/RenderTarget.cs` | Yes | **REPLACED @5261** — was `Program.OffScreenPass` (`RenderPassState`) → `.SampleCount`, `.Pass` | Game migrated the main scene pass to **dynamic rendering**; the old property no longer exists. See *Update-risk findings (5117 → 5261)*. |
| 6 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:140,141,143` | `Presets.InputAssembly.TriangleList`; `Presets.Rasterization.Fill.CullNone`; `Presets.BlendState.BlendColorAlpha` | `RenderCore.Pipelines/SimplePipelineCreator.cs:15` (+ Brutal abstractions) | Yes | None | Pipeline state presets; compile-verified by build. |
| 7 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:142` | `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` | `RenderCore` (used widely, e.g. `KSA.Rendering.Water.Rendering/OceanRenderer.cs:292`) | Yes | None | Reverse-Z depth test+write. 4730/4733 depth-prepass changes did not alter this preset. |
| 8 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:137,138`; `:50`,`TextureFactory.cs:30,34,53` | `Renderer.{Device,Allocator,Graphics,DynamicStateInfo,ViewportState}` | `KSA` Renderer / RenderCore | Yes | None | Compile-verified. |
| 9 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:238,263,240` | `Program.GetMainCamera()` (Camera); `Camera.MVP.viewProjection` | `KSA/Program.cs:489`; `Camera.MVP` (used `Program.cs:2394`) | Yes | None (`:488`→`:489`) | `GetMainCamera()` returns non-null `Camera`; mod null-checks defensively. |
| 10 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:248` | `Program.SetViewport(CommandBuffer)` | `KSA/Program.cs:3781` | Yes | None (`:3724`→`:3781`) | |
| 11 | Direct | `thug-life.lib/ThugLifeRenderManager.cs:38` | `Program.GetRenderer()` (Renderer) | `KSA/Program.cs:450` | Yes | None (`:449`→`:450`) | |
| 12 | Direct (ego transform) | `thug-life.lib/ThugLifeQuadRenderer.cs:267,268,269` | `Vehicle.GetMatrixAsmb2Ego(Camera)`; `Part.PositionEgo(ref readonly double4x4)`; `Part.Asmb2Ego(doubleQuat)`; `Vehicle.Asmb2Ego` (doubleQuat) | `KSA/Vehicle.cs:833,449`; `KSA/Part.cs:677,682` | Yes | None | Per-frame model-ego matrix; caller passes `in` to the `ref readonly` param. |
| 13 | Direct (UI) | `thug-life.lib/ThugLifeSubmod.cs:120,126,135` | `Vehicle.Parts.Parts`; `Part.Template.Id`; `Part.Id`; `Part.SubParts` | `KSA/Part.cs:323,655`; `KSA/PartTree.cs` | Yes | None | Combo population. |
| 14 | GPU lib (Brutal/RenderCore) | `ThugLifeTextureFactory.cs:33,64`; `ThugLifeQuadRenderer.cs` (pipeline/descriptor/buffers) | `SimpleVkTexture`; `VkUtils.UploadBufferToImage`/`StageAndUploadToBuffer`; `BufferEx`, `DescriptorSetLayoutEx`, `DescriptorPoolEx`, `VertexInput`, `ShaderStages`, `CommandBuffer` | `Brutal.VulkanApi(.Abstractions)`, `RenderCore`, `Core` | Yes | None | **4729 bumped Brutal packages** — highest churn surface; compile against 4750 DLLs passes, so the used API is intact. |
| 15 | Abstraction | `thug-life.lib/ThugLifeSubmod.cs:101` | `VehicleProvider.GetAllVehicles()` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` | Yes | None | |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `UnlitMeshVert` | Vertex shader | `ModLibrary.Get<ShaderReference>` id | `Core/DefaultAssets.xml:66` → `Core/Shaders/Mesh/UnlitMesh.vert` | Yes | None (id, path, file all identical) |
| `UnlitMeshFrag` | Fragment shader | `ModLibrary.Get<ShaderReference>` id | `Core/DefaultAssets.xml:67` → `Core/Shaders/Mesh/UnlitMesh.frag` | Yes | None (id, path, file all identical) |

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
  for the **"blinky broken"** entry in [`../ISSUES.md`](../ISSUES.md) — the 5117 pass checked blinky's
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
