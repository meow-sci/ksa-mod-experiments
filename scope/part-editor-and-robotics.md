# Part Editor & Robotics — Game Integration Scope

Permanent reference for how the **space-tape** (in-game Part editor) and **flexo**
(robotics / hinges) mods bind to the Kitten Space Agency (KSA) game, so that future
game updates that break them can be detected and root-caused quickly.

- **Game versions compared:** NEW = `2026.6.9.4750` · OLD = `2026.6.8.4680`
- **Decomp (source of truth):**
  - NEW `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
  - OLD `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`
- **Build status against NEW (4750):** `space-tape.lib` **does not compile** (4 break groups, all root-caused below). `flexo.lib` **compiles clean**.
- **Important:** several space-tape breaks predate 4680 (the mod was last built against an
  even older game build). The "Δ vs OLD" column states whether 4680 already had the NEW
  shape, so you can tell genuine 4680→4750 regressions from older drift.

Legend for *In NEW?*: ✅ present & signature-compatible · ⚠️ present but changed · ❌ removed/renamed.

---

## space-tape

### Purpose
In-game **Part editor**: compose new Parts by placing existing **SubParts** in an isolated 3D
scene (translate/rotate/scale gizmos), define tanks/connectors/power/coupling, and export a KSA
mod (Assets XML + GameData XML) under a managed `space-tape-parts` mod directory. Also owns
SubPart **thumbnail generation** (off-screen Vulkan rendering) and an animated SubPart browser.

### Unscience integration
- `SpaceTapeSubmod : ISubmod` (`space-tape.lib/SpaceTapeSubmod.cs`) is the entry point; appears as a
  panel in the Unscience Toolbox. `static Current` is read by the render Harmony prefix.
- `Initialize()` applies two Harmony patch sets (`PartRenderHelper.Patch()`, `PartEditorMenuBarPatch.Patch()`).
- Standalone path: `space-tape/Mod.cs` + `space-tape/Patcher.cs` (F11). `Patcher.Patch()` also calls
  `HotkeyGuard.Patch` + `IvaForceRender.Patch` (both from `MeowSci.KsaAbstractions`).

### UI / hotkeys
- F11 toggle (standalone). In-editor: `D`=+45° Y, `F`=+45° X, `P`=cycle pan-plane mode.
- Adds a top-level **"Part Editor"** game menu (menu-bar Harmony postfix) while the editor scene is active.
- Floating windows: Part Editor, SubParts browser (animated thumbnails), large SubPart Viewer.

### Persistence
- Writes **Assets XML** (`<Part>` + `<PartGameData>`) and manages **`mod.toml`** (Tomlyn) in the
  `space-tape-parts` output mod dir (`PartModWriter.cs`, `PartXmlSerializer.cs`, `GameDataXmlSerializer.cs`).
- Hot-reload spike registers saved `PartTemplate`s into `ModLibrary` at runtime.

### Integration points

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Harmony (prefix) | `PartRenderHelper.cs:9,13` | `PartModelRenderer.UpdateRenderData(Viewport, int)` **static void** | `KSA/PartModelRenderer.cs:658` | ✅ | none (OLD `:625`, identical) | Overload array `new[]{typeof(Viewport),typeof(int)}` MUST stay; this exact overload is the keystone render hook. |
| 2 | Harmony (postfix) | `PartEditorMenuBarPatch.cs:31` | `Program.DrawProgramMenusHook()` instance void | `KSA/Program.cs:3391` | ✅ | none | Also reads `Program.MainViewport` (`:403`) + `viewport.MenuBarInUse`. |
| 3 | Typed API (in prefix) | `PartRenderHelper.cs:23` | `PartTree.UpdateRenderData(ref readonly double4x4, bool isEditedVehicle, Viewport, int)` | `KSA/PartTree.cs:435` | ✅ | none (OLD `:431`) | Called as `(in matrix, false, viewport, frameIndex)`. |
| 4 | Reflection (private name) | `PartCatalog.cs:20-28`, `SubPartCatalog.cs:35-39`, `Thumbnails/SubpartThumbnailCache.cs:89-99`, `Thumbnails/SubpartThumbnailGenerator.cs:397-411`, `Thumbnails/SingleSubpartGenerator.cs:295-305` | `ModLibrary.AllParts` field → `SerializedCollection<PartTemplate>.GetList()` | `KSA/ModLibrary.cs` (`AllParts`), `KSA/SerializedCollection.cs:42` (`GetList`) | ✅ | none | String literals `"AllParts"`/`"GetList"`. `AllParts` is publicly reachable in decomp (`ModLibrary.AllParts.GetList()`), so the reflection still resolves; `PartCatalog` hard-casts to `SerializedCollection<PartTemplate>`. |
| 5 | Reflection (private field) | `PartEditorInteraction.cs:48`, `PartEditorUi.cs:48` | `Part._matrixAsmb` (private `double4x4`) | `KSA/Part.cs:325` | ✅ | none | Cache-invalidation safety only; name string. |
| 6 | Reflection (private field) | `PartEditorUi.cs:50` | `Part._matrixAsmb2Parent` (private `double4x4`) | `KSA/Part.cs:339` | ✅ | none | Name string. |
| 7 | Typed API (scene) | `PartEditorScene.cs:62,154,216`, `PartEditorInteraction.cs:70` | `VehicleEditingSpace(double3,doubleQuat,double,…)`, `.GetMatrixAsmb2Ego(Camera)`, `.Asmb2Ecl` | `KSA/VehicleEditingSpace.cs` | ✅ | none | Isolated editor space far from celestials. |
| 8 | Typed API (camera) | `PartEditorScene.cs:71-77,101,106-107` | `Program.GetCamera/SetCameraMode/GetHoveredCamera/MainViewport.{MapCamera,BaseCamera}/ControlledVehicle`; `Camera.SetFollow(IFollowable,bool,bool,bool alert)`, `.Following` | `KSA/Program.cs:403,450,…`, `KSA/Camera.cs` | ✅ | none | `alert:false` follow path avoids on-screen "Following…" spam. |
| 9 | Typed API (camera snap) | `CameraSnapController.cs:75-86` | `Camera.Following` → `IFollowable.OrbitView` → `OrbitView.Azimuth/Elevation` | `KSA/OrbitView.cs`, `KSA/IFollowable.cs` | ✅ | none | Snap views write Azimuth/Elevation. |
| 10 | Render/GPU (grid) | `CameraSnapController.cs:199-201` | `OrbitLinePass.AddLineVertex(Viewport, float3, byte4)` + `AddLineEnd(Viewport)` **static** | `KSA/OrbitLinePass.cs:284,275` | ✅ | none | Grid drawn via orbit-line renderer (alpha-correct, no shader edits). |
| 11 | Render/GPU (gizmos) | `ConnectorGizmo.cs:27-34,55-56`, `PartEditorScene.cs:65-68,161`, `PartEditorGizmos.cs` | `GenericGizmo(MeshReference, IGizmoRenderData, int)`, `.GetSegmentDataByViewport(Viewport) → PerSegmentData[]`, `GenericGizmo.Static.GenericGizmoRenderData`, `PerSegmentData{Active,PositionEgo,Body2Cce,Scale,Color}` | `KSA/GenericGizmo.cs:208,277,15,170` | ✅ | none | Connector cubes/arrows + origin axis + transform gizmos. |
| 12 | Game assets (mesh) | `ConnectorGizmo.cs:28,32`, `PartEditorScene.cs:66` | `ModLibrary.Get<MeshReference>("Box")`, `("ArrowMesh")` | `KSA/ModLibrary.cs` | ✅ | none | Hard-coded mesh ids; missing ids → gizmo creation fails (caught). |
| 13 | Typed API (build parts) | `PartEditorScene.cs:246-280` | `new Part(string, PartTemplate)`, `Part.{PositionParentAsmb,Asmb2ParentAsmb,Scale}`, `PartTree.CreateFromNewPartTree(Part)`, `Part.Modules.Get<MeshViewModule>()/<PartModelModule>()`, `Part.Modules.Add(...)`, `new MeshViewModule(string, MeshReference)`, `MeshReference.{PositionCompare,BoundingSphereRadius}` | `KSA/Part.cs`, `KSA/PartTree.cs`, `KSA/MeshViewModule.cs` | ✅ | none | Builds runtime `Part`s for the editor scene; ensures MeshView for raycasting. |
| 14 | Typed API (raycast/select) | `PartEditorInteraction.cs:86,105,116` | `Camera.ScreenToEgoRay(double2)`, `Part.RayCastEgoSubPart(in double4x4, Ray, out …)`, `Part.RayCastEgo(...)`, `Part.Selected` | `KSA/Part.cs`, `KSA/Camera.cs` | ✅ | none | Hover/click select + native highlight/selection shaders. |
| 15 | Typed API (import) | `PartImporter.cs` (see breaks) | `PartTemplate.{SubPartInstances,DisplayName,EditorTags,InertMasses,Components,Connectors,Batteries,Generators,PowerConsumers,Decoupler,DockingPort,EVADoor,IsSubPart,IsHidden,Thumbnail}` (**`Tank` removed in 5018** — tanks are now `Tank.TemplateData` entries inside `Components`); `EditorTag.Tag`; `Part.Connector.TemplateBase.{Id,Transform,Flags}`; `Part.Connector.Flag.{Internal,ToSurface,FromSurface}`; tank `Cylindrical/SphericalTankTemplate.{Length,OuterRadius,WallThickness,Material.Id}`; `CustomMassTemplate.Mass` | `KSA/PartTemplate.cs`, `KSA/EditorTag.cs`, `KSA/Part.cs:95-111`, `KSA/*TankTemplate.cs` | ⚠️ | energy/docking types changed (breaks #2/#3 below); rest unchanged | `EditorTag` is still `record struct` w/ `public readonly string Tag` (compiles). `Decoupler.Force` still `float`. |
| 16 | Render/GPU (thumbnails) | `Thumbnails/SubpartThumbnailGenerator.cs`, `SingleSubpartGenerator.cs`, `ThumbnailCameraState.cs` | `ThumbnailRenderer(Renderer)` (`.SIZE/.ColorFormat/.Sampler/.PerInstance…/.PerDraw…/.RecordPartRender`), `ThumbnailPart`, `ThumbnailRenderResources`, `ThumbnailReference`, `Program.{GetRenderer,RenderedViewport,LinearClampedSampler,LightSystem}`, `GameSettings.Current.Graphics.PartThumbnailSize` (ushort) | `KSA.Rendering.Thumbnails/*`, `KSA.Rendering/*`, `KSA/Program.cs:126,391,407,450` | ⚠️ | `ThumbnailReference`/`ThumbnailPart` APIs changed (breaks #1/#4) | Off-screen Vulkan render loop; rev 4694 thumbnail/offscreen rework, rev 4696 sizes. |
| 17 | Harmony (IVA) | `space-tape/Patcher.cs:20,35`; toggled `SubPartsWindow.cs:102-104` | via `IvaForceRender` → patches `PartModel..ctor(PartModelModule.Template)` + `PartModel.AddInstance(...)` | `ksa-abstractions.lib/IvaForceRender.cs`; `KSA/PartModel.cs` | ✅ | none | "Render IVA SubParts" toggle; depends on `PartModel` ctor overload + `AddInstance` name. |
| 18 | Lifecycle | `space-tape/Mod.cs`, `Patcher.cs`; `SpaceTapeSubmod.cs` | StarMap attributes; `ISubmod`; `HotkeyGuard` | `MeowSci.KsaAbstractions` | ✅ | none | Per CLAUDE.md HotkeyGuard rule. |

### Game assets referenced
- Gizmo meshes by id: **`"Box"`**, **`"ArrowMesh"`** (`ConnectorGizmo.cs`, `PartEditorScene.cs`).
- Default tank wall material id **`"Aluminum.2014(s)"`** (`PartImporter.cs:176`, `GameDataModels.cs:12`).
- Writes part **Assets XML / GameData XML** + **`mod.toml`** into the `space-tape-parts` mod folder.
- GameData XML element/attribute schema it emits (see runtime risks): `<Part>`, `<SubPart Id InstanceOf>`,
  `<Connector Id><Flags>…</Flags>`, `<PartGameData>`, `<EditorTag Value>`, `<CustomMass><Mass Kg>`,
  `<CylindricalTank|SphericalTank>`, `<Battery><MaximumCapacity KWh>`, `<Generator><Produced W>`,
  `<PowerConsumer><Consumed W>`, `<Decoupler ConnectorId Force>`, `<DockingPort ConnectorId Force>`, `<EVADoor ConnectorId>`.

### CONFIRMED BREAKS & fixes (4680→4750)

> All 4 listed groups reproduced and root-caused. No *additional* compile errors were found in
> the imports path (`EditorTag.Tag`, `StringReference→string`, `DecouplerTemplate.Force`, tank
> fields all still compile).

| Break | mod file:line | old API (what the mod calls) | new API (4750 decomp) | exact fix |
|-------|---------------|------------------------------|------------------------|-----------|
| **#1 Thumbnail texture register** | `Thumbnails/SubpartViewerWindow.cs:365,407`; `SubPartsWindow.cs:221,227` | `ThumbnailReference.CreateImGuiThumbnail(VkSampler)` (does not exist) | `ThumbnailReference.GetOrCreateImGuiTexture(VkSampler inSampler) → ImTextureRef` (`KSA.Rendering.Thumbnails/ThumbnailReference.cs:36`). `DestroyImGuiThumbnail()` + `ImGuiImageRef` prop unchanged. | Rename call `X.CreateImGuiThumbnail(Program.LinearClampedSampler)` → `X.GetOrCreateImGuiTexture(Program.LinearClampedSampler)`. Return value can be ignored; the subsequent `.ImGuiImageRef` reads still work (the method populates it). |
| **#2 Energy/power are now `double`** | `PartImporter.cs:95,104,113` (CS1503 double→float) | `float.IsNaN(b.MaximumCapacity.KWh)` / `g.Produced.W` / `pc.Consumed.W` — args are now `double` | `BatteryTemplate.MaximumCapacity` is `EnergyReference` (`KWh` is **double**, `KSA/EnergyReference.cs:32`); `GeneratorTemplate.Produced` & `PowerConsumerTemplate.Consumed` are `PowerReference` (`W` is **double**, `KSA/PowerReference.cs:10`). Both have implicit `operator double`. | Change `float.IsNaN(` → `double.IsNaN(` on lines 95, 104, 113. The `(double)…KWh`/`.W` and `(double)(float)<ref>` branches already compile (implicit double). |
| **#3 DockingPort pushoff is an impulse** | `PartImporter.cs:135` (`'DockingPortTemplate' does not contain 'Force'`) | `template.DockingPort.Force` (does not exist) | `DockingPortTemplate` (`KSA/DockingPortTemplate.cs`): `PushoffImpulse` (`ImpulseReference`, default 5000 Ns), `LatchingKineticEnergy` (`EnergyReference`, 50 J), `ConnectorId` now `StringReference` **[XmlElement]**. `ImpulseReference` has `GetNewtonSeconds()`/implicit `double` (Ns). | `Force = template.DockingPort.Force` → `Force = template.DockingPort.PushoffImpulse.GetNewtonSeconds()` (or `(double)template.DockingPort.PushoffImpulse`). Semantics change N→N·s; the mod's `DockingPortState.Force` (double) now stores newton-seconds — ideally rename to `PushoffImpulseNs`. **Also fix the writer** (see runtime risk R1) or saved docking ports won't load. |
| **#4 Bounding-sphere needs out-center** | `Thumbnails/SingleSubpartGenerator.cs:207`; `SubpartThumbnailGenerator.cs:244` | `root.ComputeBoundingSphereRadius()` (no overload) | `ThumbnailPart.ComputeBoundingSphereRadius(out float3 outCenter) → float` (`KSA.Rendering.Thumbnails/ThumbnailPart.cs:150`). Return type still `float`. | `root.ComputeBoundingSphereRadius()` → `root.ComputeBoundingSphereRadius(out _)`. (Quality option: capture `out float3 center` and offset like `ThumbnailCreator.MoveRootPart` `KSA.Rendering/ThumbnailCreator.cs:201-208` so off-origin SubParts frame centered.) |

**Δ vs OLD for the breaks:** #1 and #4 already had the NEW shape in **4680** (so they are pre-4680 drift,
not 4680→4750 regressions). #3: 4680 had `PushoffForce`+`LatchingImpulse` (floats, attributes) +
`ConnectorId` (string attribute) — still no `.Force` — and 4750 then replaced those with
`PushoffImpulse`/`LatchingKineticEnergy`/`StringReference`. **#2 is a genuine 4680→4750 regression**:
4680 `BatteryTemplate.MaximumCapacity`/`GeneratorTemplate.Produced` were `JoulesReference` whose
`KWh`/`W` were **float** (`KSA/JoulesReference.cs:10-16`), so `float.IsNaN(...)` compiled. In 4750
`JoulesReference` was split into `EnergyReference` + `PowerReference` with all fields **double**.

### Update-risk findings (4750 → 5018)

- 🔴 **BREAKING (fixed) — `PartTemplate.Tank` removed.** In 5018 a part's tank is no longer a single
  `AsmbTankTemplate? Tank` field. Tanks moved into the generic component list as
  `Tank.TemplateData` (`[XmlType(TypeName = "Tank")] class TemplateData : TemplateDataBase` with an
  `[XmlElement("CylindricalTank"|"SphericalTank")] AsmbTankTemplate? Tank`), reachable via
  `PartTemplate.Components`. The game itself now walks it that way
  (`PartTemplate.CalculateMass`/`AccumulateStorageVolume`, `KSA/PartTemplate.cs:633,675`).
  `PartImporter` now iterates `Components` and imports **every** `Tank.TemplateData` it finds — so
  multi-tank parts are supported, where the old single-field read could only ever see one.
  `AsmbTankTemplate` itself and both subclasses (`CylindricalTankTemplate`, `SphericalTankTemplate`)
  are unchanged, so `ImportTank` needed no edit.
  - ⚠ **Round-trip follow-up (not yet done):** `GameDataXmlSerializer` still emits tanks in the old
    shape. Verify the emitted `<CylindricalTank|SphericalTank>` is nested where 5018's deserializer
    expects a `Tank` component, or saved parts will lose their tanks — same class of runtime break as
    R1 below. **Needs a live save/load pass.**
- ✅ Everything else in this area is stable: `PartModelRenderer.UpdateRenderData(Viewport, int)` (the
  Harmony target shared with flexo) is signature-identical, `Part.Asmb2ParentAsmb` and
  `PartTree.RecomputeStaticMass` are unchanged, and `ThumbnailReference`/`ThumbnailPart` did not
  change at all 4750→5018.
- ⚠ Carried forward: the **staging → resource groups** rewrite deleted `StageList.cs`/`Staging.cs`
  and added `ResourceGroups`/`ResourceGroupList`/`ResourceGroupsPanel`. `Part.Stage`/`SetStage` are
  unchanged, but this is the game-side landing of the 4731/4741 "Stages"→"Resource Groups" editor
  rename already tracked in R2.

### Other update-risk findings
- **R1 (runtime, high) — DockingPort GameData XML mismatch:** `GameDataXmlSerializer.SerializeDockingPort`
  (`GameDataXmlSerializer.cs:89-92`) writes `<DockingPort ConnectorId="…" Force="…"/>` as **attributes**.
  In 4750 `DockingPortTemplate.ConnectorId` is an **`[XmlElement] StringReference`** and there is **no
  `Force`** (it's `PushoffImpulse`/`LatchingKineticEnergy` elements). Saved docking ports will silently
  deserialize with empty ConnectorId and no pushoff. Fix: emit `<DockingPort><ConnectorId Value="…"/><PushoffImpulse Ns="…"/></DockingPort>`.
- **R2 (runtime, med) — editor categories/tags drift (rev 4731/4741):** tags are now registered at
  startup from `CoreEditorTagsGameData.xml` via `VehicleEditor.RegisterTag` (`PartTemplate.cs:127-129`),
  and **"Interstage" was removed as a category** (rev 4741 → Coupling/Structural). `EditorTag.Interstage`
  still exists as a struct constant (`KSA/EditorTag.cs:20`) but is no longer a valid editor category. Tags
  the mod imports/round-trips that don't match a registered tag won't filter correctly. The mod's
  `<EditorTag Value="…">` write still maps to `EditorTagsStrings` (`[XmlElement("EditorTag")] StringReference`),
  so it deserializes, but the *value* must be a live tag. Also rev 4732 renamed "Stages"→"Resource Groups".
- **R3 (runtime, low) — part size filter (rev 4721):** `PartTemplate.Diameter` is now `DistanceReference`
  (`PartTemplate.cs:76-77`) and part-size data was added to part XML. The mod never writes `<Diameter>`,
  so mod-built parts may be missing from size-filtered lists.
- **R4 (med) — face-snapping connector semantics (rev 4687–4740):** the mod writes connector
  `ToSurface`/`FromSurface` flags (`PartXmlSerializer.cs:38-39`, `GameDataXmlSerializer.cs:77-78`). The
  flag enum is intact (`Part.Connector.Flag`), but face-snap behavior, the approved face-snap target list,
  and `NoFaceSnapping` tags changed — connectors authored by the mod may snap differently than before.
- **R5 (med) — private-name reflection** (`"AllParts"`, `"GetList"`, `Part._matrixAsmb`,
  `Part._matrixAsmb2Parent`): no compiler protection; a rename in any future build silently disables
  catalogs/thumbnails or the matrix-cache safety. All present in 4750 today.
- **R6 (low) — thumbnail framing:** fixing break #4 with `out _` discards the bounding center; off-origin
  SubParts will be slightly mis-framed vs the game's own thumbnail path.

---

## flexo

### Purpose
Adds articulated Parts (**hinges/rotors**) on top of KSA's static Part system. A dedicated editor
defines a hinge (fixed part, moving part, axis, degree range, resting angle, motor speed); at runtime
the mod scans the controlled vehicle, finds matching parts, and rotates the moving sub-tree each
physics step while keeping render + physics state coherent.

### Unscience integration
- `FlexoSubmod : ISubmod` (`flexo.lib/FlexoSubmod.cs`); `static Current` read by the solver prefix.
- `flexo/Patcher.cs` applies `HotkeyGuard.Patch` then `FlexoPatches.Apply(_harmony)`.
- `FlexoPatches.Apply` = `harmony.PatchAll(assembly)` (render prefix) + `FlexoSolverPatch.Apply` (manual prefix).

### UI / hotkeys
- F11 toggle (standalone). Runtime panel: Scan Vehicle, Reload Definitions, per-hinge Open/Close/Reset + angle/speed.
- Editor floating window reuses space-tape-style scene (camera snaps, lighting, origin gizmo, live preview).

### Persistence
- **TOML only** — `~/.flexo/flexo_part_*.toml` via Tomlyn (`flexo.lib/Data/FlexoDataManager.cs`). Flexo does
  **not** write any game part XML, so it has no game-XML-schema runtime risk.

### Integration points

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Harmony (prefix) | `FlexoPatches.cs:9,13` | `PartModelRenderer.UpdateRenderData(Viewport, int)` **static void** | `KSA/PartModelRenderer.cs:658` | ✅ | none (OLD `:625`) | Same keystone hook as space-tape; overload array `[Viewport,int]` must stay. |
| 2 | Harmony (prefix, by-name) | `FlexoPatches.cs:76-81,84` | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep)` **static** | `KSA/Universe.cs:1660` | ✅ | none (OLD `:1109`, same 2 params) | Patched via `AccessTools.Method(typeof(Universe), "ExecuteNextVehicleSolvers")` **without** a param array; prefix `BeforeVehicleSolvers(double dtPlayer)` injects `dtPlayer` **by name**. Works because there is one overload. If the method is ever overloaded, by-name resolution becomes ambiguous → patch fails. |
| 3 | Typed API (in prefix) | `FlexoPatches.cs:23` | `PartTree.UpdateRenderData(ref readonly double4x4, bool, Viewport, int)` | `KSA/PartTree.cs:435` | ✅ | none | Renders editor parts. |
| 4 | Reflection (private method) | `Runtime/HingeController.cs:186` | `PartTree.RecomputeStaticMass()` (**private** void) via `Traverse.Create(Vehicle.Parts).Method("RecomputeStaticMass")` | `KSA/PartTree.cs:306` | ✅ | none | Name string `"RecomputeStaticMass"`; no compiler protection. |
| 5 | Typed API (rotation) | `Runtime/HingeController.cs:34,103,107,122,143,171` | `Part.Asmb2ParentAsmb { get; set; }` (`doubleQuat`) | `KSA/Part.cs:463` | ✅ | none | Core hinge rotation write; settable property. |
| 6 | Typed API (rotation) | `Runtime/HingeController.cs:35,106,139,170` | `Part.PositionParentAsmb` (`double3`) | `KSA/Part.cs` | ✅ | none | Orbits descendants around pivot. |
| 7 | Typed API (physics) | `Runtime/HingeController.cs:124,152,172` | `Part.BoundingBoxVehicleAsmb { get; set; }` + `Part.ComputeBoundingBoxVehicleAsmb()` | `KSA/Part.cs:515,853` | ✅ | none | Keeps cached bounds coherent after rotation. |
| 8 | Typed API (tree) | `Runtime/HingeController.cs:201` | `Part.TreeChildren` (`List<Part>`) | `KSA/Part.cs:387` | ✅ | none | Collects rigid sub-tree to co-rotate. |
| 9 | Typed API (subparts) | `Runtime/HingeController.cs:168` | `Part.SubParts` (`ReadOnlySpan<Part>`) | `KSA/Part.cs:655` | ✅ | none | Touches setters to force cache invalidation (fragile, see R-flexo-2). |
| 10 | Typed API (vehicle) | `Runtime/HingeController.cs:186,191` | `Vehicle.Parts` (`PartTree`), `Vehicle.UpdateAfterPartTreeModification()` void | `KSA/Vehicle.cs:1277` | ✅ | none | Recomputes mass/aero/CoM after part-tree mutation. |
| 11 | Typed API (scan) | `Runtime/FlexoRuntime.cs:47,54,55,70` | `Part.Template.Id` (template-id match) | `KSA/Part.cs`, `KSA/PartTemplate.cs` | ✅ | none | Vehicle scan pairs fixed/moving by template id. |
| 12 | Typed API (connectivity) | `Runtime/FlexoRuntime.cs:114-117` | `Part.Connections`, `Connection.OtherPart(Part)` | `KSA/Part.cs`, `KSA/Connection.cs` | ✅ | none | `IsConnected` helper (defined, currently unused). |
| 13 | Render/GPU (editor) | `Editor/FlexoEditorScene.cs`, `Editor/FlexoCameraSnap.cs` | `OrbitLinePass.AddLineVertex/AddLineEnd`; `GenericGizmo(...)`; `VehicleEditingSpace`; `Program` camera APIs | `KSA/OrbitLinePass.cs:284,275`, `KSA/GenericGizmo.cs:208`, `KSA/VehicleEditingSpace.cs` | ✅ | none | Reuses space-tape scene patterns; compiled clean. |
| 14 | Abstractions | `Runtime/FlexoRuntime.cs:36,44` | `VehicleProvider.GetControlledVehicle()` (wraps `Program.ControlledVehicle`), `PartHelpers.GetAllParts(Vehicle)` | `ksa-abstractions.lib` | ✅ | none | Controlled-vehicle access goes through the shared abstraction. |
| 15 | Lifecycle | `flexo/Mod.cs`, `Patcher.cs`, `FlexoSubmod.cs` | StarMap; `ISubmod`; `HotkeyGuard` | `MeowSci.KsaAbstractions` | ✅ | none | HotkeyGuard applied per rule. |

### Game assets referenced
None written. Editor scene loads gizmo meshes (`"Box"`/`"ArrowMesh"`) the same way as space-tape (table #13). All persistence is local TOML.

### CONFIRMED BREAKS & fixes (4680→4750)
**None — `flexo.lib` compiles clean against 4750.** All four Harmony/typed targets the patch param
arrays and prefixes depend on are signature-identical in 4680 and 4750
(`PartModelRenderer.UpdateRenderData(Viewport,int)`, `PartTree.UpdateRenderData(ref readonly double4x4,bool,Viewport,int)`,
`Universe.ExecuteNextVehicleSolvers(double,SimStep)`, `Part.Asmb2ParentAsmb` settable property).

### Other update-risk findings
- **R-flexo-1 (med) — by-name solver patch:** table #2. `AccessTools.Method` by name + by-name prefix
  param injection tolerate the extra `SimStep simStep` param today, but are fragile to any overload or
  rename of `ExecuteNextVehicleSolvers`.
- **R-flexo-2 (high, behavioral) — private cache-invalidation contract:** `HingeController.ApplyRotation`
  depends on undocumented `Part` caching semantics — it re-assigns `PositionParentAsmb`/`Asmb2ParentAsmb`
  to *touch* setters (`InvalidateSubPartCaches`, `HingeController.cs:166-175`) and manually recomputes
  `BoundingBoxVehicleAsmb`. If the game changes how `_matrixAsmb`/`_asmb2VehicleAsmb`/bounds caches
  invalidate (`KSA/Part.cs:463-497,693-728`), hinges will visibly desync render vs physics with **no
  compile error**. Highest silent-breakage surface in flexo.
- **R-flexo-3 (med) — private method name:** `"RecomputeStaticMass"` (table #4) via Traverse; rename →
  runtime exception (caught/logged, mass just won't update).
- **R-flexo-4 (low) — mutating part trees off the solver phase:** the design comment notes
  `UpdateBeforeVehicleSolvers` is the only safe phase to mutate trees + call
  `UpdateAfterPartTreeModification()`. If the solver scheduling around
  `Universe.ExecuteNextVehicleSolvers` changes, timing-coherence assumptions could break.

---

## Quick re-verification checklist (run on each new game build)

1. `PartModelRenderer.UpdateRenderData(Viewport,int)` still **static** with that exact overload (both mods, keystone).
2. `PartTree.UpdateRenderData(ref readonly double4x4,bool,Viewport,int)` unchanged (both render prefixes).
3. `Program.DrawProgramMenusHook()` + `Viewport.MenuBarInUse` (space-tape menu).
4. `Universe.ExecuteNextVehicleSolvers` still single overload (flexo by-name patch).
5. `ThumbnailReference.GetOrCreateImGuiTexture(VkSampler)` + `ThumbnailPart.ComputeBoundingSphereRadius(out float3)` (space-tape thumbnails).
6. `DockingPortTemplate` shape (`PushoffImpulse`/`LatchingKineticEnergy`/`StringReference ConnectorId`) — importer **and** writer.
7. `BatteryTemplate.MaximumCapacity:EnergyReference` / `Generator.Produced` & `PowerConsumer.Consumed`:`PowerReference` (double).
8. Reflection names: `ModLibrary.AllParts`, `SerializedCollection.GetList`, `Part._matrixAsmb`, `Part._matrixAsmb2Parent`, `PartTree.RecomputeStaticMass`.
9. `Part.Asmb2ParentAsmb`/`PositionParentAsmb`/`BoundingBoxVehicleAsmb`/`TreeChildren`/`SubParts` + `Vehicle.UpdateAfterPartTreeModification` (flexo runtime).
10. `OrbitLinePass.AddLineVertex/AddLineEnd` + `GenericGizmo` ctor/`PerSegmentData`/`Static.GenericGizmoRenderData` (both grids/gizmos).
