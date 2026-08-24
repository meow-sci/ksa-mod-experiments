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
| 11 | Direct | `blinky.lib/LcdGridBuilder.cs:466` (connect); `:243,245` (disconnect) | `Part.Connection.Connect(IConnector, IConnector)`; `Part.Connections` (List<Connection>); `Connection.Disconnect()`; `Connector.CanConnect()`; `Connector.Connection` | `KSA/Part.cs:530,391,546,343,238` | Yes | None | 🔴 **Semantics matter, not just the signature.** The engine side MUST be the engine's own declared feed `Connector` — a bare `Part`↔`Part` connection is rejected by `ResourceManager.CanFlowAcross` (see #22). The fuel side stays a `Part` (`Part.CanConnect()` is always `true`, `KSA/Part.cs:1887`), so one tank anchors the whole grid. |
| 12 | Direct | `blinky.lib/LcdGridBuilder.cs:299,302,305` | `Part.PositionParentAsmb` (double3); `Part.Asmb2ParentAsmb` (doubleQuat); `Part.Scale` (double3) — all settable | `KSA/Part.cs:449,463,499` | Yes | None | |
| 13 | Direct | `blinky.lib/LcdGridBuilder.cs:327,469`; `PixelGrid.cs:47,90` | `Part.SubtreeModules` (ModuleList); `ModuleList.Get<T>()` for `Tank`, `EngineController` | `KSA/Part.cs:409`; `KSA/ModuleList.cs`; `KSA/Tank.cs`, `KSA/EngineController.cs` | Yes | None | `Get<T>()` returns array (`.Length`/index used). |
| 14 | Direct | `blinky.lib/LcdGridBuilder.cs:326,377,324` | `Part.IsSubPart`; `Part.Template` (PartTemplate); `Vehicle.Parts.Parts` | `KSA/Part.cs:657,323` | Yes | None | |
| 15 | Direct | `blinky.lib/LcdGridBuilder.cs:654` | `EngineController.MinimumThrottle` (float, settable) | `KSA/EngineController.cs:38` | Yes | None | Set **before** the PartTree rebuild — `PartTree.RecomputeRocketControls` (`KSA/PartTree.cs:762-770`) folds it into `PartTree.EngineThrottleMin`, which clamps the vehicle's manual throttle. |
| 16 | Direct | `blinky.lib/BlinkyGridManager.cs:224,252,266`; `NonLcdEngineCache.cs:46` | `EngineController.SetIsActive(Vehicle?, bool)` — pixel on/off | `KSA/EngineController.cs:46` | Yes | None | Called with `null` vehicle arg. |
| 17 | Direct | `blinky.lib/NonLcdEngineCache.cs:36` | `EngineController.IsActive` (get) | `KSA/EngineController.cs:24` | Yes | None | |
| 18 | Direct | `blinky.lib/BlinkyGridManager.cs:258` | `Vehicle.SetEnum(Enum?)` with `VehicleEngine.MainIgnite` | `KSA/Vehicle.cs:4838`; `KSA/VehicleEngine.cs:5` | Yes | None | Ignites vehicle before lighting pixels. |
| 19 | Direct (diagnostics) | `blinky.lib/BlinkySubmod.cs:712,753,760` | `Combustor.ResourceManager` (field); `ResourceManagerBase.ConsumptionOrder` (`Tank[][]?` property); `ResourceManagerBase.FlowRule` | `KSA/Combustor.cs:13`; `KSA/ResourceManagerBase.cs:69,25` | Yes | New this change | **Replaced the old string-reflection probe** of `NearestToFurtherestNode*`: `ConsumptionOrder` is public and already resolves the active `FlowRule`, so the diagnose path is now fully typed and no longer fails silently on a rename. |
| 20 | Direct (debug) | `blinky.lib/BlinkySubmod.cs:664-666,762` | `Vehicle.GetManualThrottle()`; `Vehicle.FlightComputer`; `Vehicle.IsSet<VehicleEngine>(T, bool)`; `EngineController.Cores` (RocketCore[]); `Connection.OtherPart(Part)` | `KSA/Vehicle.cs:1193,461,5989`; `KSA/EngineController.cs:36` (`Cores`); `KSA/Part.cs:493` | Yes | `IsSet` newly used | `IsSet(VehicleEngine.MainIgnite, false)` routes to the private `Vehicle.IsEngine` (`KSA/Vehicle.cs:6041-6055`) and reads `_manualControlInputs.EngineOn` — the only public read of the ignition flag. |
| 21 | Abstraction | `blinky.lib/BlinkyGridManager.cs:280`; `BlinkySubmod.cs` | `VehicleProvider.GetAllVehicles()` / `GetControlledVehicle()` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` (repo lib) | Yes | None | Game coupling lives in ksa-abstractions scope. |
| 22 | Direct | `blinky.lib/LcdGridBuilder.cs:491-501` | `RocketCore.FeedConnectors` (`Part.Connector[]`, bound in `RocketCore.OnFullPartCreated` → `BindFeedPoints` from the template's `ConsumerFeedWiring`/`FeedsFrom`) | `KSA/RocketCore.cs:20,26,61,96` | Yes | New this change | 🔴 **The load-bearing dependency of the whole ignition path.** `ResourceManager.CanFlowAcross` (`KSA/ResourceManager.cs:274-282`) rejects the first hop out of the consumer part unless the connection sits on one of these connectors (`IsDeclaredFeedConnection`, `:305`). If the template wiring resolves to nothing, `FeedConnectors` is empty and the engine reaches no propellant. |
| 23 | Direct | `blinky.lib/LcdGridBuilder.cs:628`; `BlinkySubmod.cs:712` | `Combustor` type test on `RocketCore`; `Combustor.ResourceManager`; `ResourceManagerBase.ConsumptionOrder` | `KSA/Combustor.cs:7,13`; `KSA/ResourceManagerBase.cs:69` | Yes | New this change | Post-build propellant verification. `Combustor.ComputePropellantAvailable` (`KSA/Combustor.cs:60`) is `ResourceManager?.ResourceAvailable(...) ?? false`, so an empty `ConsumptionOrder` means the pixel can never light. `SolidMotor` cores legitimately have no `ResourceManager`. |
| 24 | Direct | `blinky.lib/LcdGridBuilder.cs:307` | `PartTree.ResourceGroupList` (public field); `ResourceGroupList.CalculateStages(bool = false)` | `KSA/PartTree.cs:27`; `KSA/ResourceGroupList.cs:100` | Yes | New this change | Public trigger for the **internal** `PartTree.RecreateResourceManagers` (`KSA/PartTree.cs:592`) — used by `RepairFuelFeeds` to rebuild the fuel graphs without rebuilding the part tree. If `CalculateStages` stops calling it, repair silently no-ops. |

**Game assets referenced**

| Asset | Kind | Referenced as | Content path (NEW) | In NEW? | Δ vs OLD |
|---|---|---|---|---|---|
| `CorePropulsionA_Prefab_EngineA1` | Engine part template (default in `LcdGridConfig.cs:47`) | `ModLibrary.Get<PartTemplate>` id | `<Part Id=...>` in `Core/CorePropulsionAAssets.xml:466` | Yes | None | 
| `CorePropulsionA_Prefab_EngineA2..A6` | Engine part templates (UI presets `BlinkySubmod.cs:49-57`; default A3) | `ModLibrary.Get<PartTemplate>` id | `Core/CorePropulsionAAssets.xml` + `<PartGameData>` in `Core/CorePropulsionAGameData.xml:99,154,209,248,321` | Yes | None |

Note (superseded): the 4750 pass recorded `EngineA1` as present-as-`<Part>` but absent from
the build-menu catalog. At **5348 it is gone from Content entirely** — neither
`CorePropulsionAAssets.xml` nor `CorePropulsionAGameData.xml` mentions it, and only A2–A6 exist.
`ModLibrary.Get` throws for it. Both the UI preset list and `LcdGridConfig.EnginePartId` now
default to **A3**, and `EngineA1` has been removed from `BlinkySubmod.EnginePresets`.

---

### 🔴 Root cause of "blinky broken" — the propellant feed (fixed 2026-08-23)

This is the long-standing `ISSUES.md` entry. The grid built and changed vehicle mass, but no
pixel ever lit, because **the pixel engines could not reach propellant**, so
`EngineControllerState.IsPropellantAvailable` stayed false and
`FlightComputer.CommandEngineThrottles` (`KSA/FlightComputer.cs:421-443`) never commanded a
throttle no matter how many times the engine was activated. With no plume there is nothing to
see — the meshes themselves are scaled to ~1% and are effectively invisible by design.

The 5018 fuel/resource rewrite added two gates that blinky's original wiring fails:

1. **`ResourceManager.CanFlowAcross` (`KSA/ResourceManager.cs:279-282`)** rejects the first hop
   out of the consumer part unless the connection sits on a connector declared in the part
   template's `ConsumerFeedWiring`/`FeedsFrom` (`IsDeclaredFeedConnection`, `:305`).
2. **`ResourceManagerBase.CanFlowAcross` (`KSA/ResourceManagerBase.cs:209-212`)** requires the
   connection to carry the combustor's `PlumbingCapability` — `BulkFluid` for these liquid engines.

blinky connected `Part`↔`Part` (`Part.Connection.Connect(pixelPart, fuelPart)`). `Part` implements
`Connection.IConnector`, but `Part.EndpointCapabilities` (`KSA/Part.cs:1066`) is `null` for anything
that is not a fuel-port part, and `ConnectorCapabilityExtensions.Intersect(null, null)` yields
`Electricity | ServiceFluid` — no `BulkFluid`. The connection also is not a declared feed connection.
Both gates fail, `PopulateGraph` never leaves the engine, `ConsumptionOrder` is empty, and
`Combustor.ComputePropellantAvailable` returns false forever.

**Fix** — connect the engine's own declared feed `Connector` (`RocketCore.FeedConnectors`, e.g.
EngineA3's `_connector3`, authored `<Capabilities>BulkFluid</Capabilities>` in
`Core/CorePropulsionAGameData.xml:189-193`) to the fuel `Part`. `Intersect(connectorCaps, null)`
returns the connector's own capabilities, so `BulkFluid` survives, and the connection is by
definition a declared feed connection. The fuel side stays a bare `Part` because
`Part.CanConnect()` is unconditionally `true` — one tank can anchor every pixel in the grid.
See integration points #11, #22–#24.

This was **not** a 5348 regression: the same two gates are present at 5261, so blinky has been
dark since the 5018 feed rewrite.

⚠️ **Second, softer requirement — the tank must hold the right propellant.** Every liquid
`CorePropulsionA_Prefab_EngineA2..A6` thrust chamber is authored `<Reaction Id="Hydrolox">`
(`Core/CorePropulsionAGameData.xml:10,39,68,137,201,313,392`), and `ResourceManager.CreateOrders`
(`KSA/ResourceManager.cs:335`) only admits tanks where `tank.ContainsAny(Mix)`. A vehicle whose tanks
carry anything else will still show a dark grid despite correct wiring, so
`VerifyPropellantFeeds` names the desired mix in its warning. blinky calls
`ResourceGroupList.CalculateStages()` with `reconfigureTankContents: false`, so it never rewrites the
vehicle's tank mixes out from under the real engines.

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
buffers; render disables itself on first error (`ThugLifeRenderManager.cs:101-121`).

**Init timing (load-order constraint)** — the GPU resources are built **lazily, on the
first entry** (`ThugLifeRenderManager.EnsureGpuResources`, `:76-97`), never in the
constructor. StarMap fires `[StarMapAllModsLoaded]` from a postfix on
`ModLibrary.LoadAll()` (`KSA/Program.cs:897`), but the game does not create
`Program.OffscreenTarget` until `BuildRenderTargets()` further down that same boot method
(`KSA/Program.cs:934`) — so building the pipeline at `Initialize()` dereferenced a null
`RenderTarget` and the submod reported *"init failed: Object reference not set to an
instance of an object"*. ⚠ Any future work that moves GPU allocation back into
`Initialize()` re-introduces this. Same discipline as the sibling gatOS port.

**UI/hotkeys** — Standalone window "Thug Life", 500x600, **F12**
(`thug-life/Mod.cs:51,78`). Create form (vehicle / part / optional subpart filtered
combos + position/rotation/width/height), per-entry transform + Visible + Remove sections.
An **animate thug** button (`ThugLifeSubmod.cs:198-210`) appears beside *Add Sunglasses*
only when the selected vehicle is a `KSA.KittenEva`; it applies the fixed pose in
`KittenGlassesPreset.cs` and slides the entry into place via `ThugLifeSlide`, driven from
`ThugLifeRenderManager.Update(dt)` (called from the submod's `Update`, i.e. `OnBeforeUi`
in both hosts). The slide is pure mod-side math — it touches no game API.

**Persistence** — None. Entries are in-memory only (`ThugLifeRenderManager._entries`);
lost on reload. No StarMap save hooks, no disk I/O.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature, or shader/asset path) | Decomp/Content path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|---|---|---|---|---|---|---|
| 1 | Harmony postfix | `thug-life.lib/ThugLifeRenderPatches.cs:19-21,44` | `SuperMeshRenderSystem.RenderMainPass(CommandBuffer commandBuffer)` — postfix records quad draws into the active offscreen pass | `KSA/SuperMeshRenderSystem.cs:329` | Yes | None (`:329`→`:329`) | Single method, patched by name. Called 3x from `KSA/Program.cs` (3902/4104/4248). |
| 2 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs:114` | `ModLibrary.Get<ShaderReference>("UnlitMeshVert")` | id→path in `Core/DefaultAssets.xml:66`; file `Core/Shaders/Mesh/UnlitMesh.vert` | Yes | None (`:62`→`:66`) | Stock shader; **not** MeshIndirect/Model* — untouched by 4693/4745. |
| 3 | Render asset (shader) | `thug-life.lib/ThugLifeQuadRenderer.cs:115` | `ModLibrary.Get<ShaderReference>("UnlitMeshFrag")` | id→path in `Core/DefaultAssets.xml:67`; file `Core/Shaders/Mesh/UnlitMesh.frag` | Yes | None (`:63`→`:67`) | Frag hard-writes `alpha=1.0` (cut-out via geometry, per renderer comment). |
| 4 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:117` | `RenderTechnique.CreateShaderStages(Device, Span<ShaderReference>, Span<VkSpecializationInfo>=default)` | `RenderCore/RenderTechnique.cs:37` | Yes | None (`:36`→`:37`) | `ShaderReference : FileReference, IKeyed` (`KSA/ShaderReference.cs:20`). |
| 5 | Direct (render-pass) | `thug-life.lib/ThugLifeQuadRenderer.cs:152` | `Program.OffscreenTarget.SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)` — `RenderTarget : IRenderPassInfo` | `KSA/Program.cs:438`, `KSA.Rendering/RenderTarget.cs` | Yes | **REPLACED @5261** — was `Program.OffScreenPass` (`RenderPassState`) → `.SampleCount`, `.Pass` | Game migrated the main scene pass to **dynamic rendering**; the old property no longer exists. **Null until `BuildRenderTargets()` (`Program.cs:934`), which runs AFTER `ModLibrary.LoadAll()` (`:897`) — i.e. after `[StarMapAllModsLoaded]`. Pipeline build must stay lazy (see *Init timing* above).** |
| 6 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:140,141,143` | `Presets.InputAssembly.TriangleList`; `Presets.Rasterization.Fill.CullNone`; `Presets.BlendState.BlendColorAlpha` | `RenderCore.Pipelines/SimplePipelineCreator.cs:15` (+ Brutal abstractions) | Yes | None | Pipeline state presets; compile-verified by build. |
| 7 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:142` | `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` | `RenderCore` (used widely, e.g. `KSA.Rendering.Water.Rendering/OceanRenderer.cs:292`) | Yes | None | Reverse-Z depth test+write. 4730/4733 depth-prepass changes did not alter this preset. |
| 8 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:137,138`; `:50`,`TextureFactory.cs:30,34,53` | `Renderer.{Device,Allocator,Graphics,DynamicStateInfo,ViewportState}` | `KSA` Renderer / RenderCore | Yes | None | Compile-verified. |
| 9 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:252,256,277` | `Program.GetRenderCamera()` (= `RenderedViewport.GetCamera()`); `Camera.MVP.viewProjection` | `KSA/Program.cs:594`, `:472`; `Camera.MVP` | Yes | **CHANGED (mod-side)** — was `Program.GetMainCamera()` (`:584`) | `RenderMainPass` runs once per **visible viewport** (main + the two always-visible 128² crew-portrait viewports since 5261), and ego space is camera-relative, so the main camera drew the portrait passes with the wrong clip transform. Now uses the camera of the viewport being rendered. Mod null-checks defensively. |
| 10 | Direct (render) | `thug-life.lib/ThugLifeQuadRenderer.cs:264` | `Program.SetViewport(CommandBuffer)` | `KSA/Program.cs:3781` | Yes | None (`:3724`→`:3781`) | |
| 11 | Direct | `thug-life.lib/ThugLifeRenderManager.cs:81` | `Program.GetRenderer()` (Renderer) | `KSA/Program.cs:535` | Yes | None | Called from the lazy `EnsureGpuResources()`, not from the constructor. |
| 12 | Direct (ego transform) | `thug-life.lib/ThugLifeQuadRenderer.cs:281,282,283` | `Vehicle.GetMatrixAsmb2Ego(Camera)`; `Part.PositionEgo(ref readonly double4x4)`; `Part.Asmb2Ego(doubleQuat)`; `Vehicle.Asmb2Ego` (doubleQuat) | `KSA/Vehicle.cs:833,449`; `KSA/Part.cs:677,682` | Yes | None | Per-frame model-ego matrix; caller passes `in` to the `ref readonly` param. |
| 13 | Direct (UI) | `thug-life.lib/ThugLifeSubmod.cs:122,128,137` | `Vehicle.Parts.Parts`; `Part.Template.Id`; `Part.Id`; `Part.SubParts` | `KSA/Part.cs:323,655`; `KSA/PartTree.cs` | Yes | None | Combo population. |
| 14 | GPU lib (Brutal/RenderCore) | `ThugLifeTextureFactory.cs:33,64`; `ThugLifeQuadRenderer.cs` (pipeline/descriptor/buffers) | `SimpleVkTexture`; `VkUtils.UploadBufferToImage`/`StageAndUploadToBuffer`; `BufferEx`, `DescriptorSetLayoutEx`, `DescriptorPoolEx`, `VertexInput`, `ShaderStages`, `CommandBuffer` | `Brutal.VulkanApi(.Abstractions)`, `RenderCore`, `Core` | Yes | None | **4729 bumped Brutal packages** — highest churn surface; compile against 4750 DLLs passes, so the used API is intact. |
| 15 | Abstraction | `thug-life.lib/ThugLifeSubmod.cs:103` | `VehicleProvider.GetAllVehicles()` (ksa-abstractions.lib) | `MeowSci.KsaAbstractions` | Yes | None | |
| 16 | Direct (type test) | `thug-life.lib/KittenGlassesPreset.cs:38` | `KittenEva` (`: Vehicle`) — `vehicle is KittenEva`, gates the **animate thug** button | `KSA/KittenEva.cs:13` | Yes | **NEW (mod-side, 2026-08-23)** | Type identity only, no members touched. A rename/reparent of `KittenEva` is a compile error, not silent drift. Seated kittens are not vehicles and are out of scope here. |
| 17 | Direct (UI) | `thug-life.lib/ThugLifeSubmod.cs:308` | `Vehicle.Parts.Parts` — first top-level part as the kitten fallback anchor | `KSA/PartTree.cs:67` | Yes | **NEW (mod-side, 2026-08-23)** | A `KittenEva` is constructed around its MMU backpack part as root (`KSA/EVADoor.cs:210`), so this is non-empty in practice; null-checked regardless. |

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
- ✅ **blinky's diagnostics still resolve.** `ResourceManagerBase.PopulateGraph` remains, as do the
  `NearestToFurtherestNode` / `NearestToFurtherestNodeSameStage` fields read reflectively at
  `BlinkySubmod.cs:625-626`, and `ResourceManager` is still reached via the `core is Combustor` test.
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
