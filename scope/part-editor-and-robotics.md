# Part Editor & Robotics — Game Integration Scope

Permanent reference for how the **space-tape** (in-game Part editor), **flexo**
(robotics / hinges) and **parts-now** (runtime Part/SubPart loading) mods bind to the
Kitten Space Agency (KSA) game, so that future game updates that break them can be
detected and root-caused quickly.

- **Game versions compared:** NEW = `2026.6.9.4750` · OLD = `2026.6.8.4680`
- **Decomp (source of truth):**
  - NEW `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
  - OLD `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`
- **Build status against NEW (4750):** `space-tape.lib` **does not compile** (4 break groups, all root-caused below). `flexo.lib` **compiles clean**.
- **parts-now is newer than this file's 4680↔4750 diff.** It was written against **`2026.7.9.5018`**,
  the current `scope/FULL_SCOPE.md` baseline, and every decomp path in its table below is a line
  number in the **5018** tree (`…/ksa-game-assemblies/current/decomp`). Its "Δ vs OLD" column is
  therefore always `new` — the mod did not exist at 4750.
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
| 5 | Direct typed API | `PartEditorInteraction.cs:415`, `PartEditorUi.cs:801` | `Part.ResetCachedPosMatrixValues()` — `public void ResetCachedPosMatrixValues()` | `KSA/Part.cs:1047` | ✅ | **Replaced reflection (rev 5112)** | Clears all five transform caches (`_matrixAsmb`, `_positionVehicleAsmb`, `_matrixAsmb2Parent`, `_asmb2VehicleAsmb`, `_matrixAsmb2VehicleAsmb`). Public on both 5018 and 5117. Was `Part._matrixAsmb` reflection until rev 5112 changed the uncached sentinel — see *Update-risk findings*. |
| 6 | *(retired)* | — | was `Part._matrixAsmb2Parent` reflection | — | — | **Removed** | Folded into row 5; no reflection remains in space-tape's cache-invalidation path. |
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

### Update-risk findings (5018 → 5117)

- 🔴 **BREAKING (fixed) — `Double3Ex.{Up,Down,Left,Right,Forward,Backward}` removed (rev 5067).**
  15 CS0117 errors across `PartEditorGizmos.cs`, `PartEditorInteraction.cs`,
  `Thumbnails/SingleSubpartGenerator.cs`, `Thumbnails/SubpartThumbnailGenerator.cs`. Changelog:
  *"Removed Double3Ex Up/Forward/etc. vectors as they were misleading and often misused"* /
  *"Added named vectors to Camera as they were used legitimately for this purpose in a few cases."*
  The game kept view-frame equivalents (`Camera.ForwardView`/`RightView`/`UpView`,
  `KSA/Camera.cs:73-77`) and renamed its own accessors `GetForward`/`GetRight`/`GetUp` →
  `GetForwardEcl`/`GetRightEcl`/`GetUpEcl` — **no mod in this repo calls the renamed accessors.**
  `Double3Ex.One`/`Zero`/`NaN` survive and are still used by the thumbnail generators.
  **Fix applied:** the six constants now live in `ksa-abstractions.lib/Directions.cs` (identical
  values → zero behavior change); the 19 call sites use `Directions.*`. See
  [`00-architecture-and-abstractions.md`](00-architecture-and-abstractions.md).

- 🔴 **SILENT BREAK (fixed) — `Part` matrix-cache invalidation sentinel changed (rev 5112).**
  Compile-clean and actively corrupting. Rev 5112 (*"Added caching for Part.MatrixAsmb2VehicleAsmb,
  the calculation of which was a significant cost at high time warp"*) changed the "uncached"
  sentinel from `double4x4.Identity` to an all-NaN `Part.UncachedMatrix`, tested with
  `_matrixAsmb.M11.Equals(double.NaN)` (`KSA/Part.cs:536-552,688,732,1035`), and added three more
  cached fields (`_positionVehicleAsmb`, `_asmb2VehicleAsmb`, `_matrixAsmb2VehicleAsmb`).
  space-tape wrote `double4x4.Identity` into `_matrixAsmb`/`_matrixAsmb2Parent` at three sites to
  *invalidate* them — on 5117 that instead asserts **"the cached transform is identity,"** collapsing
  the part's transform with no build error. The guard went from harmless-redundant to corrupting.
  **Fix applied:** all three sites call the public `Part.ResetCachedPosMatrixValues()`
  (`KSA/Part.cs:1047`), which clears all five caches and was already public on 5018 — so the
  reflection was never necessary. `using System.Reflection` dropped from both files.
  *Not to be confused with flexo's R-flexo-2*: flexo touches the **property setters**, which call
  `ResetCachedPosMatrixValues()` internally (`KSA/Part.cs:706,720,758`), so flexo was never exposed.

- ⚠️ **Behavioral, needs live pass — `EVADoorTemplate` gained `SeatId` (rev 5085).** On 5018
  `EVADoorTemplate` had **no** serialized members; 5117 adds
  `[XmlAttribute("SeatId")] public string SeatId` (`KSA/EVADoorTemplate.cs:7-8`), and rev 5085 made
  the in-game **EVA button appear only when the door's aligned `IVASeat` is occupied**
  (`EVADoor.AlignedSeat`, `EVADoor.ResolveAlignedSeats(PartTree)`). space-tape's
  `GameDataXmlSerializer.SerializeEVADoor` (`space-tape.lib/GameDataXmlSerializer.cs:97-99`) emits
  `<EVADoor ConnectorId="…"/>` and has no `SeatId`, so **authored EVA doors will render but never
  offer EVA**. Separately, `ConnectorId` was **never** an `EVADoorTemplate` member on 5018 either —
  `XmlSerializer` silently ignores it. Pre-existing no-op; the `SeatId` gap is new. Fix requires
  adding `SeatId` to `EVADoorState`/the editor UI/the writer — **not done here** (out of scope for a
  build fix; needs a UI decision about how the user picks a seat id).

- ⚠️ **Watch item — `EditorTag` gained `Booster`/`Coupling`/`Cargo` (5117).** `KSA/EditorTag.cs:24-28`.
  parts-now's `BuiltInEditorTags` (`parts-now.lib/Runtime/GameRegistry.cs:43-46`) still lists the
  original six. **Harmless today**: the three new tags are *not* registered into
  `VehicleEditor._editorTagLookup` (which force-registers only All/Capsules/Hidden/Engines/Interstage/
  Radial, `KSA/VehicleEditor.cs:6151+`) and are *not* declared in `Content/Core/PartGameData.xml`, so
  they are dormant statics. If a future build starts registering them, V7 validation would reject
  bundles using them until `BuiltInEditorTags` is extended.

- `PartModelModule.Template`/`PartModelGlassModule.Template` gained `[DefaultValue]` attributes
  (5117). These affect only what the game's serializer **writes**, not what it reads, and space-tape
  emits its own XML — no impact.

---

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
- **R5 (med) — private-name reflection** (`"AllParts"`, `"GetList"`): no compiler protection; a rename
  in any future build silently disables catalogs/thumbnails. Both present in 5117.
  `Part._matrixAsmb`/`_matrixAsmb2Parent` **are no longer reflected** — 5117 retired that pair in
  favour of the public `Part.ResetCachedPosMatrixValues()` (row 5), removing this risk class from the
  transform path entirely.
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

## parts-now

### Purpose
Loads **Parts and SubParts into a running game** — no restart. Two flows: paste KSA `<Assets>` XML
into a new managed mod folder ("install"), or load / reload / unload an existing mod folder that KSA
did **not** load at boot. Runs the whole boot asset pipeline by hand on a per-mod basis: validate →
write folder → build a `Mod` → `AssetBundle.OnDataLoad` → run `ILoader`s on a worker → mesh-budget
check → `IBinder.Bind` (GPU upload) → incremental `PartGameData` attach → warm the `PartModel`
family → render a part-browser thumbnail per new Part → reset the editor's diameter cache. An
exact-inverse purge (12 numbered steps) makes unload and reload possible.

The two things that make it work at all are **mesh-buffer headroom reserved before KSA allocates its
one shared interleaved vertex/index buffer**, and a **single reflection choke point**
(`Runtime/GameRegistry.cs`) over KSA's `internal static` asset registries.

### Unscience integration
- `PartsNowSubmod : ISubmod` (`parts-now.lib/PartsNowSubmod.cs`) is the entry point; appears as a
  panel in the Unscience Toolbox. `Initialize()` runs `GameRegistry.SelfTest()` then
  `MeshBudget.Reserve()` — it **must** be called from `[StarMapAllModsLoaded]` (see U1 below).
- `Update(dt)` calls `MeshBudget.OnFirstFrame()` once, then `RuntimeModLoader.Step()` **exactly once
  per frame** — the loader's `Bind` and `Thumbnails` states submit command buffers and block on
  fences, which is only safe inside `Program.OnDrawUiFrame`.
- Standalone path: `parts-now/Mod.cs` (F10 by default, from `parts-now.toml`) + `parts-now/Patcher.cs`.
  parts-now patches **nothing** of its own; `Patcher.Patch()` applies only `HotkeyGuard.Patch`
  (`parts-now/Patcher.cs:23`, unpatched at `:36`).
- Threading rule, repeated at the top of every file: game thread only, except
  `RuntimeModLoader`'s `RunLoaders` worker, which touches only `ILoader.Load()`.

### UI / hotkeys
- F10 toggle (standalone; configurable via `hotkey` in `parts-now.toml`). No game menu is injected.
- Panels: Status (self-test, mesh budget, bindless-texture budget), Paste XML (3 tabs — Assets /
  Part / GameData), Mod folders (scan + Load/Reload/Unload), Results (per-part status + thumbnail).
- No floating windows; `RenderFloatingWindows()` is empty.

### Persistence
- Writes a real **KSA mod folder** under `ModLibrary.LocalModsFolderPath` — `mod.toml` (Tomlyn,
  merged if it already exists) plus up to three `<modId>-{assets,part,gamedata}.xml` files, each
  written atomically via a `.tmp` sibling (`Io/ModFolderWriter.cs`).
- Adds an **enabled, non-new `ModEntry`** to `ModLibrary.Manifest` and calls `ModManifest.Save()`
  so the mod also loads at the next launch (deliberately not `new ModEntry(id, count)`, which sets
  `Enabled=false, New=true` and pops the game's "confirm mods" dialog).
- Its own settings live in `<mods>/parts-now/parts-now.toml` (`Runtime/PartsNowSettings.cs:65`).
- `LoadedModRecord` is **session state only** — nothing about a runtime load is persisted.

### Integration points

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Reflection (internal static field, string) | `Runtime/GameRegistry.cs:72,292` | `ModLibrary.AllParts : internal static readonly SerializedCollection<PartTemplate>` | `KSA/ModLibrary.cs:86` | ✅ | new | Literal `"AllParts"`; `BindingFlags.Static\|NonPublic\|Public`. Fatal on miss → `IsHealthy=false` → all Load buttons disabled. |
| 2 | Reflection (internal static field, string) | `Runtime/GameRegistry.cs:73,292` | `ModLibrary.AllMeshes : SerializedCollection<MeshReference>` | `KSA/ModLibrary.cs:80` | ✅ | new | Literal `"AllMeshes"`. Fatal. |
| 3 | Reflection (internal static field, string) | `Runtime/GameRegistry.cs:74,292` | `ModLibrary.AllFiles : SerializedCollection<FileReference>` | `KSA/ModLibrary.cs:68` | ✅ | new | Literal `"AllFiles"`. Fatal. |
| 4 | Reflection (internal static field, string) | `Runtime/GameRegistry.cs:75,292` | `ModLibrary.AllMaterials : SerializedCollection<PbrMaterialReference>` | `KSA/ModLibrary.cs:70` | ✅ | new | Literal `"AllMaterials"`. Fatal. |
| 5 | Reflection (internal static field, string) | `Runtime/GameRegistry.cs:76,292` | `ModLibrary.AllPartGameDataReferences : SerializedCollection<PartGameDataReference>` | `KSA/ModLibrary.cs:78` | ✅ | new | Literal `"AllPartGameDataReferences"` — note the plural `References` suffix, unlike its siblings. Fatal. |
| 6 | Reflection (internal static field, string) | `Runtime/GameRegistry.cs:77,292` | `ModLibrary.AllEditorTagDefinitions : SerializedCollection<EditorTagDefinition>` | `KSA/ModLibrary.cs:134`; `KSA/EditorTagDefinition.cs:5` | ✅ | new | Literal `"AllEditorTagDefinitions"`. Fatal. Feeds V7's known-tag set. |
| 7 | Reflection (private instance field, string) | `Runtime/GameRegistry.cs:356-357` (`CollectionFields<T>`), used `:154-165` | `SerializedCollection<T>._collection : private readonly ConcurrentDictionary<KeyHash,T>` | `KSA/SerializedCollection.cs:14` | ✅ | new | Literal `"_collection"`, `Instance\|NonPublic`, probed once per closed generic at static-ctor time (`:83`). **The whole unload/reload story depends on it** — `SerializedCollection<T>` has no removal API (U4). Also type-checked: a non-`ConcurrentDictionary<KeyHash,T>` throws a descriptive error rather than corrupting the registry. |
| 8 | Reflection (private static field, string) | `Runtime/GameRegistry.cs:320` | `VehicleEditor._editorTagLookup : private static Dictionary<uint,string>` | `KSA/VehicleEditor.cs:399` | ✅ | new | Literal `"_editorTagLookup"`, type-checked against `Dictionary<uint,string>`. **Degraded, not fatal**: V7 falls back to the six built-in tags + `AllEditorTagDefinitions` ids. |
| 9 | Direct API (registry read/write) | `Runtime/GameRegistry.cs:152,170-188`; `Runtime/RuntimeModLoaderDeltas.cs:30-35,262` | `SerializedCollection<T>.{GetList() : List<T>, Find(KeyHash) : T?}` + `KeyHash.Make(ReadOnlySpan<char>)` | `KSA/SerializedCollection.cs:42,37`; `KSA/KeyHash.cs:15` | ✅ | new | `GetList()` returns the **live** backing list, which is what makes `.Remove(item)` a real unregister. `KeyHash.Make` lowercases → all parts-now id indexes are `OrdinalIgnoreCase`. |
| 10 | Direct API (mesh budget) | `Runtime/MeshBudget.cs:86-89,134-138,180-181,232-233` | `DeviceMeshInterleaved.Shared.{RunningVertexBufferSize, RunningIndexBufferSize} : public static uint` | `KSA/DeviceMeshInterleaved.cs:25,27` | ✅ | new | Written directly (inflate at reserve, rewind on the first frame, rewind again on rollback). Must stay **public static settable `uint`**. |
| 11 | Direct API (mesh budget) | `Runtime/MeshBudget.cs:80,83` | `DeviceMeshInterleaved.Shared.{VertexAllocation, IndexAllocation} : public static BufferEx` → `BufferEx.BufferSize` | `KSA/DeviceMeshInterleaved.cs:19,21`; `Brutal.VulkanApi.Abstractions/BufferEx.cs:90` | ✅ | new | Authoritative allocated size (as opposed to the running cursor). Sized from the running counters inside `BuildBuffers` (`KSA/DeviceMeshInterleaved.cs:55,63`). |
| 12 | Direct API (tripwire) | `Runtime/MeshBudget.cs:124,173` | `DeviceMeshInterleaved.Shared.IsBuilt : public static bool` | `KSA/DeviceMeshInterleaved.cs:31` | ✅ | new | Read as a **tripwire for U1**: must be `false` at `Reserve()` and `true` on the first frame. Both mismatches log a WARNING and keep going. |
| 13 | Behavioral (ordering invariant) | `parts-now/Mod.cs:40-46`; `parts-now.lib/PartsNowSubmod.cs:52-57` | `[StarMapAllModsLoaded]` = Harmony postfix on `ModLibrary.LoadAll()` (`Program.cs:956`), which runs **before** `ModLibrary.Bind(_renderer)` (`Program.cs:985`) → `IBinder.Bind` → `DeviceMeshInterleaved.Bind()` → `Shared.Build()` | `KSA/Program.cs:956,985`; `KSA/ModLibrary.cs:1732`; `KSA/DeviceMeshInterleaved.cs:195,33` | ✅ | new | 🔶 **U1 — the standing invariant.** `Build()` is one-shot and sizes both buffers from the counters as they stand at that instant. Reserve must land in between. Fails **silently**. |
| 14 | Direct API (mesh sizing) | `Runtime/MeshBudget.cs:267,276-277` | `MeshReference.DeviceMeshesInterleaved : DeviceMeshInterleaved[]` → `.VerticesSize` / `.IndicesSize : ByteSize` | `KSA/MeshReference.cs:32`; `KSA/DeviceMeshInterleaved.cs:115,125` | ✅ | new | Measured **before** `MeshReference.Dispose()` in purge step 6, for leak accounting. |
| 15 | Direct API (XML) | `Runtime/BundleParser.cs:89-90,102` | `XmlHelper.Serializers : public static Dictionary<Type, XmlSerializer>` → `[typeof(AssetBundle)]` | `KSA/XmlHelper.cs:13,46` | ✅ | new | **Must** use the game's instance: it carries the `XmlAttributeOverrides` mapping `<PartModel>`/`<Tank>`/`<Collider>`/`<Light>`… onto `PartTemplate.Components`. A hand-built `new XmlSerializer(typeof(AssetBundle))` silently drops every component. A missing entry is reported, not thrown. |
| 16 | Direct API (registration) | `Runtime/RuntimeModLoaderStates.cs:200`; `Runtime/BundleParserQueries.cs:38` | `AssetBundle.OnDataLoad(Mod) : override void`; `AssetBundle.Assets : List<SerializedId>` (field); `[XmlRoot("Assets")]` | `KSA/AssetBundle.cs:74,67,8` | ✅ | new | The single call that registers everything a bundle declares. Parsing stays side-effect free until this runs. |
| 17 | Direct API (mod object) | `Runtime/RuntimeModLoaderStates.cs:148,153,161-168` | `ModLibrary.MOD_TOML`, `ModLibrary.Find(string) : Mod?`, `Mod.MakeUsing(string id, string manifestPath) : static Mod`, `Mod.{DirectoryPath, Preload, Id}` | `KSA/ModLibrary.cs:136,175,170`; `KSA/Mod.cs:102,90,77,81` | ✅ | new | The `Mod` is deliberately **not** registered into `ModLibrary.Lookup` (only the boot path does that, `KSA/ModLibrary.cs:430`), so `ModLibrary.Find` stays a reliable "was this loaded at boot?" test (row 21). `Preload` is forced false — `FileReference.OnDataLoad` only calls `RegisterLoader` while it is false. |
| 18 | Direct API (loader/binder queues) | `Runtime/RuntimeModLoaderDeltas.cs:33,36,80,93`; `Runtime/RuntimeModPurgeSteps.cs:285-286` | `ModLibrary.Loaders : public static List<ILoader>`; `ModLibrary.Binders : public static List<IBinder>`; `ModLibrary.RegisterLoader/RegisterBinder` (indirect) | `KSA/ModLibrary.cs:144,146,180,209` | ✅ | new | Mark/delta bookkeeping, then `RemoveAll` on purge. KSA never clears either list, so leaving entries behind would make a later full re-run re-load freed objects. |
| 19 | Direct API (worker step) | `Runtime/RuntimeModLoaderStates.cs:256`; `Runtime/RuntimeModLoaderGpuStates.cs:93-94` | `ILoader.Load() : void`; `IBinder.Bind(Renderer, StagingPool) : void` | `KSA/ILoader.cs:7`; `KSA/IBinder.cs:8` | ✅ | new | `Load()` is the **only** thing parts-now runs off the game thread. `Bind()` mirrors `ModLibrary.Bind`'s per-binder body (`KSA/ModLibrary.cs:1732`) minus its `Parallel.ForEachAsync` — the stock method would re-bind *every* binder ever registered. |
| 20 | Behavioral (thread gate) | `Runtime/RuntimeModLoaderStates.cs:232-243` (design note) | `Loading.OnFrame()` early-returns on `!Program.IsMainThread()`; `Loading.{Task, PushTask, Current}` | `KSA/Loading.cs:90-94,50,36,23`; `KSA/Program.cs:520` | ✅ | new | 🔶 **U7.** `FileReference.Load()` → `Loading.Task()` → `PushTask()` → `Current.OnFrame()` renders and submits a whole ImGui frame. On a worker that whole chain is a no-op *only* because of the `IsMainThread()` guard. If it is removed, `RunLoaders` renders a second ImGui frame inside the game's own frame. |
| 21 | Direct API (boot-mod test) | `Runtime/RuntimeModLoaderApi.cs:280`; `Io/ModFolderScanner.cs:251`; `Io/ModIdValidator.cs:166` | `ModLibrary.Find(string) : Mod?` → `ModLibrary.Lookup` (internal `SerializedCollection<Mod>`) | `KSA/ModLibrary.cs:175,172,66` | ✅ | new | Refuses to load/reload a mod KSA loaded at boot — parts-now cannot account for what KSA registered on its behalf. Fails **closed** (an exception means "treat as boot-loaded"). |
| 22 | Direct API (file loading post-conditions) | `Runtime/RuntimeModLoaderDeltas.cs:194,196,203,217,220,223,232` | `FileReference.{LocalPath (field), IsReference() : override bool, Load() : void, Id, ModPath}`; `MeshAtlasFileReference.Meshes : List<MeshReference>`; `MeshFileReference.Mesh : MeshReference?`; `MeshReference.IsReference()` | `KSA/FileReference.cs:12,56,66,23`; `KSA/MeshAtlasFileReference.cs:10`; `KSA/MeshFileReference.cs:14`; `KSA/MeshReference.cs:65` | ✅ | new | `FileReference.Load()` **catches and logs its own exceptions instead of throwing**, so `VerifyLoadersProduced` re-derives each `DoLoad()` post-condition by hand. Every one of these is a silent-failure detector; if any changes shape, a half-loaded mod becomes invisible again. |
| 23 | Direct API (mesh atlas ids) | `Runtime/GlbMeshNames.cs:48-79`; `Runtime/BundleValidatorContext.cs:157` | Reproduces `MeshAtlasFileReference.DoLoad()`'s naming rule: one `MeshReference` per `GltfLoader.GltfJson.Meshes[i].Name`, skipping names starting with `'_'` | `KSA/MeshAtlasFileReference.cs:25-38` | ✅ | new | ⚠ **Duplicated game logic, not a call.** parts-now reads only the GLB's JSON chunk itself (no `Brutal.Gltf` reference) because V6 must know the mesh ids *before* anything loads. If KSA changes the skip rule or the id source, V6 silently mis-reports. |
| 24 | Direct API (GPU texture teardown) | `Runtime/RuntimeModPurgeSteps.cs:146-154` | `TextureReference.{BindlessHandle : int (get; private set), Texture : SimpleVkTexture, TextureAsset : TextureAsset, Dispose(Device)}` | `KSA/TextureReference.cs:67,61,58,74` | ✅ | new | `Dispose(Device)` calls `Program.Instance.BindlessTextures.FreeTexture(BindlessHandle)` then `Texture.Dispose()`/`TextureAsset.Dispose()` **with no null checks**, and handle `0` is the bindless library's shared *empty* texture. Hence the triple guard (`>0` + both objects non-null). The `Device` argument is ignored by the game; the type does **not** implement `IDisposable`. |
| 25 | Direct API (materials) | `Runtime/BundleParserQueries.cs:178-201`; `Runtime/RuntimeModLoaderGpuStates.cs:182-188`; `Runtime/BundleValidatorRulesSchema.cs:273-308` | `PbrMaterialReference.{DiffuseReference, NormalReference : TexturePowerReference?, PBRMap, EmissiveMap, ThinFilmMap}`; `_isReference = Diffuse==null && Normal==null && PBRMap==null` | `KSA/PbrMaterialReference.cs:9,12,15,18,21,64` | ✅ | new | V9 mirrors the `_isReference` test to tell a material *definition* from a *pointer*. See U3. |
| 26 | Direct API (part model) | `Runtime/RuntimeModLoaderGpuStates.cs:237,251,258`; `Runtime/RuntimeModPurgeSteps.cs:43-48`; `Runtime/PartThumbnailGenerator.cs:262,279,311-320` | `PartTemplate.{ApplyGameData(PartGameDataReference), ResolveConsumerFeedPoints(), Dispose(), Thumbnail : ThumbnailReference?, IsSubPart : bool, Components : List<ModuleBase.TemplateDataBase>, SubPartInstances : List<PartInstance>, EditorTagsStrings : List<StringReference>}` | `KSA/PartTemplate.cs:231,379,226,103,111,105,21,30` | ✅ | new | `ApplyGameData` is **additive** (`AddRange` on connectors/masses/rockets/components), which is why parts-now attaches incrementally instead of calling `ModLibrary.AttachGameData()` (`KSA/ModLibrary.cs:1746`) — the stock method walks *every* registered entry and would double every part attached at boot. `ResolveConsumerFeedPoints()` starts with `ConsumerFeeds.Clear()`, so re-running it **is** idempotent. `Dispose()` disposes only `Thumbnail`. |
| 27 | Direct API (model warm) | `Runtime/RuntimeModLoaderGpuStates.cs:297,301,305,166-168` | `PartModel.Get(PartModelModule.Template)`, `PartModelGlass.Get(PartModelGlassModule.Template)`, `PartModelDynamic.Get(PartModelDynamicModule.Template)` | `KSA/PartModel.cs:333`; `KSA/PartModelGlass.cs:482`; `KSA/PartModelDynamic.cs:341` | ✅ | new | Warming turns an unresolvable `<Mesh Id>` into a catchable load-time exception instead of a crash when the player first clicks the part. Note `Get` resolves by scanning `Instances` for a matching `Template.Id` (`KSA/PartModelGlass.cs:485-489`) — which is exactly why row 28 must prune those lists. |
| 28 | Direct API (static instance caches) | `Runtime/RuntimeModPurgeSteps.cs:109-120` | `PartModel.{Instances, InstancesRayTrace} : static List<PartModel>`; `PartModelGlass.{Instances, InstancesRayTrace}`; `PartModelDynamic.Instances`; `PartModelModule.Template.RayTracers : static List<Template>`; `PartModelGlassModule.Template.RayTracers` | `KSA/PartModel.cs:325,327`; `KSA/PartModelGlass.cs:474,476`; `KSA/PartModelDynamic.cs:335`; `KSA/PartModelModule.cs:21`; `KSA/PartModelGlassModule.cs:14` | ✅ | new | KSA **never** prunes these. `PartModelDynamic` has no `InstancesRayTrace` (dynamic models are never ray traced) and `PartModelDynamicModule.Template` has no `RayTracers` — both asymmetries are load-bearing. Matched by **object identity**, never by id (U5). |
| 29 | Direct API (component identity) | `Runtime/RuntimeModLoaderDeltas.cs:130-147`; `Runtime/LoadedModRecord.cs:91-105` | `ModuleBase.TemplateDataBase.Id : [XmlAttribute] public string = ""` | `KSA/ModuleBase.cs:8-11` | ✅ | new | 🔶 **U5.** Optional and not required to be unique → the purge collects the template **objects**. `ModelTemplateIds` exists for logging only. |
| 30 | Render/GPU (thumbnail framing) | `Runtime/PartThumbnailGenerator.cs:268,269,279,290,318` | `ThumbnailCreator.{ResetRootPart(ThumbnailPart), AddPart(ThumbnailPart, PartTemplate), MoveRootPart(ThumbnailPart, ThumbnailReference?, Camera), CollectDraws(ThumbnailPart, ThumbnailRenderResources), CreateThumbnailReference(Renderer, string) : ThumbnailReference}` | `KSA.Rendering/ThumbnailCreator.cs:213,176,189,123,150` | ✅ | new | Same framing the game's own `PreparePartThumbnails` uses (`:54`). `MoveRootPart(…, Camera)` forwards to the `(double fov, double nearPlane)` overload (`:194`) via `Camera.GetFieldOfView()` / `Camera.NearPlane`. `AddPart` only walks `SubPartInstances`, so a SubPart collects no draws — hence the explicit skip. |
| 31 | Render/GPU (thumbnail pipeline) | `Runtime/PartThumbnailGenerator.cs:131,281-286,322,339,350,514` | `ThumbnailRenderer(Renderer)` ctor; `.SIZE : static int` (= `GameSettings.Current.Graphics.PartThumbnailSize`); `.ColorFormat : static readonly VkFormat`; `.{PerInstanceDataDescriptorSetLayout, PerDrawDataDescriptorSetLayout, Sampler}`; `.RecordPartRender(CommandBuffer, ThumbnailReference, ThumbnailRenderResources, Viewport, string)`; `ThumbnailRenderResources(Renderer, DescriptorSetLayoutEx, DescriptorSetLayoutEx, VkSampler, int)`, `.DrawCommandVector.ElementCount`, `.UpdateDescriptorSets()`, `.Dispose()` | `KSA.Rendering.Thumbnails/ThumbnailRenderer.cs:33,31,13,25,27,29,111`; `KSA.Rendering.Thumbnails/ThumbnailRenderResources.cs:33,17,89` | ✅ | new | The three descriptor-set layouts/sampler are forwarded straight from `PartModelRenderer.ColorData` (`ThumbnailRenderer.cs:37-39`), so a change to the Part color pipeline reaches parts-now here. `ColorFormat` is consumed indirectly (image creation inside `CreateThumbnailReference`). |
| 32 | Render/GPU (thumbnail scene) | `Runtime/PartThumbnailGenerator.cs:143,270,456` | `ThumbnailPart(Camera inParent, PartInstance? = null)` ctor; `.Children : List<ThumbnailPart>?`; `.Dispose()` | `KSA.Rendering.Thumbnails/ThumbnailPart.cs:72,22,78` | ✅ | new | Root part is parented to the thumbnail viewport's camera, mirroring `ThumbnailCreator.PreparePartThumbnails`. |
| 33 | Render/GPU (thumbnail image) | `Runtime/PartThumbnailGenerator.cs:312,319`; `Runtime/RuntimeModPurgeSteps.cs:43-46`; `Ui/ResultsPanel.cs:125,133`; `Runtime/ThumbnailReadback.cs:52` | `ThumbnailReference.{ImageView : ImageViewEx (get; private set), ModelTransform : TransformReference?, GetOrCreateImGuiTexture(VkSampler) : ImTextureRef, Dispose(), CreateImageView(...)}` | `KSA.Rendering.Thumbnails/ThumbnailReference.cs:16,13,36,54,31`; `KSA/TransformReference.cs:6` | ✅ | new | ⚠ **`ImageView.IsNull()` is a load-bearing guard everywhere.** A `<Thumbnail>` that came from XML has a `ModelTransform` but **never had `CreateImageView` called**, so `Dispose()` NREs on a null captured `Device` and `GetOrCreateImGuiTexture` would hand ImGui a null view. parts-now also *preserves* a declared `ModelTransform` across regeneration, which the game's own `CreateThumbnailImage` (`ThumbnailCreator.cs:143`) drops. |
| 34 | Render/GPU (shared viewport) | `Runtime/PartThumbnailGenerator.cs:141,142,195`; `Runtime/RuntimeModUnloader.cs:110-116` | `Program.ThumbnailViewport : static Viewport` (index 1, `IsOffscreen`/`ShouldRenderGizmos=false`); `ThumbnailDynamic.UpdateGlobalCameraData(Viewport, Camera) : static`; `ThumbnailDynamic.SetSelectedPart(PartTemplate?)`; `VehicleEditor.DynamicThumbnail : ThumbnailDynamic?` | `KSA/Program.cs:445,966-967`; `KSA.Rendering.Thumbnails/ThumbnailDynamic.cs:272,89`; `KSA/VehicleEditor.cs:547` | ✅ | new | 🔶 **U6.** parts-now shares this viewport + camera with the part browser's hover preview. Safe only because parts-now submits in `Program.OnDrawUiFrame` and `ThumbnailDynamic.Render` (`ThumbnailDynamic.cs:167`) runs later in the **same** frame from `Editor.OnPreRender` (`KSA/VehicleEditor.cs:4261,4265` ← `KSA/Program.cs:2288`), each writing the camera UBO immediately before its own submit. **Never defer parts-now's submit to another frame phase.** |
| 35 | Render/GPU (camera) | `Runtime/PartThumbnailGenerator.cs:188-191` | `Camera.{Unfollow(bool changeControl = true), OnFrame(double), LocalPosition, LocalRotation, LocalScale}` (last three inherited from `Transform3D`); `Camera.{GetFieldOfView() : float, NearPlane : float}` (via `ThumbnailCreator.MoveRootPart`) | `KSA/Camera.cs:607,482,765,65`; `KSA/Transform3D.cs:9,13,11` | ✅ | new | ⚠ `Unfollow` **must** be called as `changeControl: false` — the defaulted overload nulls `Program.ControlledVehicle` and would drop the player's vessel mid-flight. INVARIANT: the camera is only ever re-asserted to origin/identity; the *part* is moved, never the camera. |
| 36 | Direct API (viewport) | `Runtime/PartThumbnailGenerator.cs:142,515` | `Viewport.GetCamera() : Camera`; `Viewport.Size : int2`; `Viewport.Index : int` (consumed indirectly by `UpdateGlobalCameraData`'s UBO slice) | `KSA/Viewport.cs:366,30,34` | ✅ | new | `Size` is only compared against `ThumbnailRenderer.SIZE` to warn when `PartThumbnailSize` changed since boot (both stay square, so framing is unaffected). |
| 37 | Render/GPU (Vulkan) | `Runtime/RuntimeModLoaderGpuStates.cs:85,93`; `Runtime/PartThumbnailGenerator.cs:129,135,138,341,356,361,368,372,390,493`; `Runtime/RuntimeModUnloader.cs:123-124`; `Runtime/ThumbnailReadback.cs:156` | `Program.GetRenderer() : Renderer`; `Renderer.{Allocator : KsaVmaAllocator, Graphics : Queue, Device : DeviceEx}`; `IBufferAllocator.CreateStagingPool(Queue, int)`; `Queue.Family`; `Queue.Submit(Span<VkSemaphore>, Span<VkPipelineStageFlags>, Span<CommandBuffer>, Span<VkSemaphore>, VkFence)`; `Device.{CreateCommandPool, AllocateCommandBuffer, CreateFence, WaitForFence, DestroyFence, FreeCommandBuffers, DestroyCommandPool, WaitIdle}` | `KSA/Program.cs:486`; `Core/Renderer.cs:14`; `Core/KSADeviceContextEx.cs:55,57,59`; `KSA/KsaVmaAllocator.cs:12`; `Brutal.VulkanApi.Abstractions/StagingPoolExtensions.cs:5`; `Brutal.VulkanApi/Queue.cs:10`; `Brutal.VulkanApi.Abstractions/QueueExtensions.cs:7`; `Brutal.VulkanApi.Abstractions/DeviceExtensions.cs:193,281,291,297`; `Brutal.VulkanApi/VkDevice.cs` | ✅ | new | parts-now owns a private **transient** command pool and one fence per thumbnail; the whole render is submit-and-wait on the game thread. `WaitIdle` gates purge step 1. Highest churn surface (Brutal Vulkan bumps) — but all compile-checked. |
| 38 | Direct API (editor refresh) | `Runtime/EditorRefresh.cs:41` (called `…GpuStates.cs:352`, `RuntimeModUnloader.cs:148`) | `VehicleEditor.ResetPartDiameterCache() : public static void` → clears `PartWindow._diameterCache` | `KSA/VehicleEditor.cs:6187,55` | ✅ | new | The **only** nudge the editor needs: `PartWindow.OnDrawUi` re-reads `ModLibrary.AllParts.GetList()` every frame, but `_diameterCache` is built lazily and reused. Never throws. |
| 39 | Direct API (unload safety gate) | `Runtime/RuntimeModUnloadGate.cs:74-79,98,105-110,119-124,148,154` | `VehicleProvider.GetAllVehicles()` + `PartHelpers.GetAllParts(Vehicle)` (abstractions); `Part.Template : PartTemplate`; `Part.SubParts : ReadOnlySpan<Part>`; `Program.Editor : static VehicleEditor?`; `VehicleEditor.{EditingSpace : VehicleEditingSpace, UnattachedPartTrees : List<PartTree>}`; `VehicleEditingSpace.AllParts => Parts?.Parts ?? default`; `PartTree.Parts` | `ksa-abstractions.lib`; `KSA/Part.cs:323,655`; `KSA/Program.cs:202`; `KSA/VehicleEditor.cs:407,529`; `KSA/VehicleEditingSpace.cs:32,16`; `KSA/PartTree.cs:67` | ✅ | new | Refuses to purge while any live vehicle or the open editor still holds one of the mod's parts. **Fails closed** — any exception while inspecting becomes a refusal. `VehicleEditingSpace.AllParts` is null-safe by construction. |
| 40 | Direct API (validation-only lookups) | `Runtime/BundleValidatorRulesReferences.cs:194,195,205,209,213,295` | `SubstanceLibrary.{AllReactions() : ReadOnlySpan<Reaction>, TryGetReaction(KeyHash) : Reaction?}`; `GrainGeometryLibrary.{All(), TryGet(KeyHash) : GrainGeometry?}`; `VolumetricExhaustTemplate.Get(string) : static VolumetricExhaustTemplate?`; `ModLibrary.Get<SoundBehavior>(string)` | `KSA/SubstanceLibrary.cs:62,218`; `KSA/GrainGeometryLibrary.cs:25,41`; `KSA/VolumetricExhaustTemplate.cs:49`; `KSA/ModLibrary.cs:975`; `KSA/SoundBehavior.cs:6` | ✅ | new | **Read-only.** V10 rejects `<Reaction Id>` / `<Grain Id>` / `<VolumetricExhaust Id>` / `<SoundEvent SoundId>` that name nothing, because parts-now cannot extend those libraries at runtime. `Get<SoundBehavior>` throws `NullReferenceException` on a miss and is the only public path (`AllSoundBehaviours` is internal, `TryGet<T>` takes the strict `IsSubclassOf` branch). The `AllReactions()/All()` probes downgrade to a warning when a library is empty. |
| 41 | Direct API (bindless budget) | `Runtime/BundleValidatorRulesIdentity.cs:221-222,231-232`; `Ui/StatusPanel.cs:201-210` | `Program.Instance : static Program`; `Program.BindlessTextures : BindlessTextureLibrary` (public field); `BindlessTextureLibrary.{TextureCount : int, MaxTextures : readonly int}` | `KSA/Program.cs:405,88,850`; `RenderCore.Systems/BindlessTextureLibrary.cs:41,19,54` | ✅ | new | V15 rule. The pool is `new FreeListIndexPool(maxTextures, allowResize: false)` with `maxTextures = 1024` (`Program.cs:850`), so exhausting it is **fatal, not slow** — parts-now holds 16 slots in reserve and refuses a load that would overrun. Not reflection: both members are public. |
| 42 | Direct API (asset classification) | `Runtime/BundleParserQueries.cs:40,51,73,81,92,108,116,216,242`; `Runtime/BundleValidatorRulesIdentity.cs:298-310` | Type hierarchy: `SubPartGameDataReference : PartGameDataReference : PartTemplate`; `SubPartTemplate : PartTemplate`; `MeshAtlasFileReference`/`MeshFileReference`/`TextureReference : FileReference`; `TexturePowerReference : TextureReference`; `PartInstance.InstanceOf`; `StringReference.Value`; `MeshViewModule.Template`; `SerializedId.Mod : Mod? { get; private set; }` | `KSA/SubPartGameDataReference.cs:3`; `KSA/PartGameDataReference.cs:5`; `KSA/SubPartTemplate.cs:3`; `KSA/PartInstance.cs:16,94`; `KSA/StringReference.cs:9`; `KSA/MeshViewModule.cs:9`; `KSA/SerializedId.cs:16` | ✅ | new | Every classifier tests **most-derived first** — a bare `is PartTemplate` matches all four part-shaped types. `SerializedId.Mod` names the owning mod in V3/V14 collision messages. Before `OnDataLoad`, `Hash` is `KeyHash.Zero` and `EditorTags` is empty, so validation reads `Id` strings and `EditorTagsStrings`. |
| 43 | Direct API (mod folder + manifest) | `Io/ModIdValidator.cs:158,175,181,214`; `Io/ModFolderWriter.cs:110,146,155,172-174`; `Runtime/PartsNowSettings.cs:65`; `Io/ModFolderScanner.cs:135` | `ModLibrary.{MOD_TOML, CONTENT_FOLDER, LocalModsFolderPath, LocalManifestPath, Manifest : public static ModManifest}`; `ModManifest.{Mods : List<ModEntry>, Save()}`; `ModEntry.{Id, Enabled, New}` | `KSA/ModLibrary.cs:136,138,166,168,148`; `KSA/ModManifest.cs:12,27`; `KSA/ModEntry.cs:24,9,21,40` | ✅ | new | `Manifest` is a public static field initialised to `null` and only filled by `PrepareManifest()`, so a null manifest is treated as "cannot prove the id is free" → **fail closed**. `new ModEntry { Id, Enabled = true, New = false }` is used deliberately instead of `new ModEntry(id, count)` (`ModEntry.cs:40`), which sets `Enabled=false, New=true`. |
| 44 | Lifecycle | `parts-now/Mod.cs:32-108`; `parts-now/Patcher.cs:23,36`; `parts-now.lib/PartsNowSubmod.cs` | StarMap `[StarMapMod]`/`[StarMapImmediateLoad]`/`[StarMapAllModsLoaded]`/`[StarMapBeforeGui]`/`[StarMapAfterGui]`/`[StarMapUnload]`; `ISubmod`; `HotkeyGuard` | `StarMap.API`; `MeowSci.KsaAbstractions` | ✅ | new | `ImmediateUnload => false` (parts-now holds GPU resources). `Dispose()` calls `RuntimeModLoader.AbandonForShutdown()`, which releases the in-flight job's `ThumbnailRenderer`, command pool and readback buffer **without** purging (a purge during shutdown would `WaitIdle` and free images while the game tears down). HotkeyGuard applied per the CLAUDE.md rule. |

### New game DLL references
Two game assemblies are referenced by `parts-now.lib.csproj` that **no other project in this repo
used before**, both purely to make typed access compile:

- **`Brutal.Vulkan.Vma.dll`** — `Renderer.Allocator` is a `KSA.KsaVmaAllocator`
  (`KSA/KsaVmaAllocator.cs:12`) which implements `Brutal.VulkanApi.Vma.IVmaAllocator`
  (`Brutal.VulkanApi.Vma/IVmaAllocator.cs:3`). Without the reference, `renderer.Allocator.…` does
  not bind.
- **`Planet.Render.Core.dll`** — `BindlessTextureLibrary` (`RenderCore.Systems/BindlessTextureLibrary.cs:11`),
  needed for the V15 texture-budget rule and the Status panel gauge.

Both are `<Private>false</Private>` HintPath references gated on `Exists('$(KSAFolder)…')`, exactly
like every other game DLL reference in the repo.

### Game assets referenced
- **None by id.** parts-now ships no asset and hard-codes no template/mesh/material/shader id.
- It *writes* a mod folder (`mod.toml` + up to three `<Assets>` XML documents) under
  `ModLibrary.LocalModsFolderPath`, and appends a `ModEntry` to `ModLibrary.Manifest`
  (`<user>/manifest.toml`).
- The XML **schema** it consumes is the game's own `<Assets>` bundle schema, parsed with the game's
  own serializer (row 15), so schema drift is handled by KSA rather than by parts-now — with the
  exception of the element names V8/V10/V11 match by string:
  `<Substance>`, `<MixtureReaction>`, `<FixedReaction>`, `<ThermalReaction>`, `<GrainGeometry>`,
  `<Situation>`, `<EditorTagDef>` (rejected as out of scope); `<Reaction Id>`, `<Grain Id>`,
  `<VolumetricExhaust Id>`, `<SoundEvent SoundId>`, `<Mesh Id>`, `<EditorTag Value>` and any
  `Path=` attribute (reference checks).

### Update-risk findings

> These are the standing invariants to re-verify on **every** game update. Each one fails **silently
> at runtime** — the mod compiles clean and the failure only shows up as corrupted geometry, a
> crash in someone else's code, or a mod that will not unload.

- 🔶 **U1 (fatal, silent) — `[StarMapAllModsLoaded]` must keep firing before `ModLibrary.Bind()`.**
  StarMap implements that attribute as a Harmony **postfix on `ModLibrary.LoadAll()`**
  (`KSA/Program.cs:956`); `ModLibrary.Bind(_renderer)` runs later at `KSA/Program.cs:985` and is
  where the first `IBinder.Bind` → `DeviceMeshInterleaved.Bind()` (`:195`) → `Shared.Build()`
  (`:33`) allocates the two shared buffers **exactly once**, sized from
  `RunningVertexBufferSize`/`RunningIndexBufferSize` as they stand at that instant. parts-now
  inflates those counters in between (`MeshBudget.Reserve`) and rewinds them on the first UI frame
  (`MeshBudget.OnFirstFrame`), which is what leaves the headroom free. **If that order ever
  changes, the reservation silently stops working and every runtime-created mesh writes past the
  end of the shared vertex buffer** — the tripwire (`Shared.IsBuilt`, table row 12) only *warns*.
  Re-check: (a) `Program.cs` still calls `LoadAll()` before `Bind()`; (b) StarMap still hooks
  `LoadAll` for `AllModsLoaded`; (c) the loading screen still never runs `Program.OnDrawUiFrame`
  (which is what guarantees the first `Update(dt)` lands after `Bind`).
- 🔶 **U2 (fatal, silent) — `Shared.Build()` must stay one-shot, and `Rebuild()` must not be usable
  to grow the buffers.** `Build()` is `Interlocked.CompareExchange`-guarded (`:33-39`) and
  `Rebuild()` (`:69`) only reacts to a raytracing usage-flag mismatch — and it copies
  `VertexAllocation.BufferSize` bytes out of the **old** buffer (`:82-83`), so it can never enlarge
  anything. If a future build makes the shared allocator growable or adds a free list, the entire
  headroom trick (and the leak accounting in purge step 6) becomes unnecessary and should be
  deleted rather than left running.
- 🔶 **U3 (crash, immediate) — `Material.DiffuseReference` / `.NormalReference` / `.PBRMap` must
  keep being dereferenced unguarded.** `ThumbnailRenderResources.AddDraw`
  (`KSA.Rendering.Thumbnails/ThumbnailRenderResources.cs:138-140`) and
  `PartModel(.Glass/.Dynamic).WriteInstancesToGpu` (`KSA/PartModel.cs:393`,
  `KSA/PartModelGlass.cs:539`, `KSA/PartModelDynamic.cs:385`) read
  `.BindlessHandle` off all three with **no null check** (only `EmissiveMap` is `?.`-guarded).
  Validation rule **V9** exists solely to stop the player authoring a part that takes the whole game
  down at the first thumbnail. **If KSA ever null-guards them, V9 becomes an unnecessary
  restriction worth relaxing** — check `AddDraw` and `WriteInstancesToGpu` on every update.
- 🔶 **U4 (blocks unload, silent) — `SerializedCollection<T>` must keep having no removal API.** It
  exposes `Register`/`Find`/`GetList` only (`KSA/SerializedCollection.cs:20,37,42`), so
  `GameRegistry.Unregister` removes from the live `GetList()` list **and** reflects into the private
  `_collection` `ConcurrentDictionary<KeyHash,T>` (`:14`) that backs `Find`. Removing from only one
  leaves `Find` resolving a purged item. **If KSA ever adds a real removal API, replace the
  reflection with it** and delete the `"_collection"` string. Also note parts-now deliberately does
  **not** take the collection's private `Lock` (`:12`) — single-threaded, game-thread-only access is
  what makes that safe.
- 🔶 **U5 (silent corruption) — `ModuleBase.TemplateDataBase.Id` stays optional and non-unique.** It
  is a plain `[XmlAttribute] public string Id = ""` (`KSA/ModuleBase.cs:10-11`). The purge therefore
  matches model templates by **object identity**, never by id: an id match would miss every id-less
  template (leaving a stale `PartModel` that `PartModel.Get` — which scans `Instances` for a
  matching `Template.Id` — would hand to the reloaded part, complete with the purged mesh's old
  shared-buffer offsets) and would evict *another* mod's instances on a collision. If KSA ever makes
  the id required and unique, the identity `HashSet<object>` can be simplified; until then it must
  not be.
- 🔶 **U6 (crash out of the render loop) — `ThumbnailDynamic.Render`'s framing block sits OUTSIDE its
  try/catch.** `ResetRootPart`/`AddPart`/`MoveRootPart` are at
  `KSA.Rendering.Thumbnails/ThumbnailDynamic.cs:184-186`; the `try` only opens at `:197`. `AddPart`
  reaches `PartInstance.GetTemplate()` → `ModLibrary.Get<PartTemplate>` (`KSA/PartInstance.cs:96`),
  which throws `NullReferenceException` on a miss — i.e. straight out of `Editor.OnPreRender`
  (`KSA/VehicleEditor.cs:4265`). **This is why purge step 0 calls
  `Program.Editor.DynamicThumbnail.SetSelectedPart(null)` first** (`RuntimeModUnloader.cs:110-116`),
  before anything is unregistered. If the game ever widens that try/catch the step becomes belt and
  braces; if it *narrows* further, re-audit.
- 🔶 **U7 (frame corruption) — `Loading.OnFrame()` must keep its `!Program.IsMainThread()`
  early-return** (`KSA/Loading.cs:90-94`). `FileReference.Load()` calls `Loading.Task()` →
  `Loading.PushTask()` → `Current.OnFrame()`, which renders and submits a complete ImGui frame.
  parts-now runs `ILoader.Load()` on a worker precisely because that guard makes the whole chain a
  no-op there. Never "fix" this by nulling `Loading.Current` instead — `LoadTask`'s field
  initialiser throws when it is null, and that throw escapes `FileReference.Load`'s try block.
- ⚠ **U8 (silent mis-validation) — `MeshAtlasFileReference.DoLoad`'s mesh-naming rule is duplicated,
  not called** (table row 23). `GlbMeshNames` reproduces "one `MeshReference` per glTF mesh node,
  named by the node, skipping names starting with `'_'`" (`KSA/MeshAtlasFileReference.cs:25-38`) by
  reading only the GLB JSON chunk. If the skip rule or the id source changes, V6 starts reporting
  the wrong mesh ids (it degrades its errors to warnings when an atlas is unreadable, but not when
  it reads it *and gets different names*).
- ⚠ **U9 (silent partial load) — `FileReference.Load()` still swallows its own exceptions**
  (`KSA/FileReference.cs:66-147`). Every check in
  `RuntimeModLoaderDeltas.VerifyLoadersProduced` is a hand-written post-condition of a successful
  `DoLoad()` (`_isReference` cleared, atlas `Meshes` non-empty, `MeshFileReference.Mesh` non-null,
  `TextureReference` registered as a binder, `MeshReference` no longer a reference). If any of those
  post-conditions changes shape, a half-loaded mod goes back to being invisible.
- ⚠ **U10 (leak, by design) — the shared interleaved buffer is a monotonic bump pointer with no free
  list.** An unload or a reload orphans its meshes' bytes until the game restarts;
  `MeshBudget.RecordLeak` tracks them and the Status panel warns past 50% of the reserved headroom.
  A rollback (nothing bound yet) rewinds the cursors instead, and `MeshBudget.RestoreCursors`
  refuses to rewind below the startup watermark — a `(0,0)` snapshot would otherwise hand the next
  runtime mesh offset 0 and its `vkCmdCopyBuffer` would overwrite the whole game's geometry.
- ⚠ **U11 (behavioral) — editor tags cannot be registered after boot.**
  `VehicleEditor.MarkEditorTagDefinitionsLoaded()` locks the list; `RegisterTag` then logs a warning
  and adds nothing, so a part carrying a new tag sits in a category button that does not exist.
  Rule V7 rejects such tags up front. This is the same drift space-tape tracks as **R2** — if the
  registered tag set changes again (e.g. another category removal like "Interstage"), V7's messages
  change with it automatically, but bundles that used to validate will start failing.
- ⚠ **U12 (behavioral, cosmetic) — `ThumbnailRenderer.SIZE` reads
  `GameSettings.Current.Graphics.PartThumbnailSize` live**, while the thumbnail viewport was sized at
  boot. parts-now warns on a mismatch and carries on (both are square, so framing is unaffected) and
  **never** mutates the game setting.

---

## Quick re-verification checklist (run on each new game build)

1. `PartModelRenderer.UpdateRenderData(Viewport,int)` still **static** with that exact overload (both mods, keystone).
2. `PartTree.UpdateRenderData(ref readonly double4x4,bool,Viewport,int)` unchanged (both render prefixes).
3. `Program.DrawProgramMenusHook()` + `Viewport.MenuBarInUse` (space-tape menu).
4. `Universe.ExecuteNextVehicleSolvers` still single overload (flexo by-name patch).
5. `ThumbnailReference.GetOrCreateImGuiTexture(VkSampler)` + `ThumbnailPart.ComputeBoundingSphereRadius(out float3)` (space-tape thumbnails).
6. `DockingPortTemplate` shape (`PushoffImpulse`/`LatchingKineticEnergy`/`StringReference ConnectorId`) — importer **and** writer.
7. `BatteryTemplate.MaximumCapacity:EnergyReference` / `Generator.Produced` & `PowerConsumer.Consumed`:`PowerReference` (double).
8. Reflection names: `ModLibrary.AllParts`, `SerializedCollection.GetList`, `PartTree.RecomputeStaticMass`. (`Part._matrixAsmb`/`_matrixAsmb2Parent` retired at 5117 — now the public `Part.ResetCachedPosMatrixValues()`.)
8b. `Part.ResetCachedPosMatrixValues()` still public and still resets **every** transform cache — space-tape's only invalidation path.
8c. `Double3Ex.One`/`Zero`/`NaN` still present (the six direction vectors are gone as of 5067; space-tape uses `MeowSci.KsaAbstractions.Directions`).
8d. `EVADoorTemplate.SeatId` — whether space-tape's `<EVADoor>` writer has caught up (open as of 5117).
9. `Part.Asmb2ParentAsmb`/`PositionParentAsmb`/`BoundingBoxVehicleAsmb`/`TreeChildren`/`SubParts` + `Vehicle.UpdateAfterPartTreeModification` (flexo runtime).
10. `OrbitLinePass.AddLineVertex/AddLineEnd` + `GenericGizmo` ctor/`PerSegmentData`/`Static.GenericGizmoRenderData` (both grids/gizmos).

parts-now (all silent at runtime — see *Update-risk findings* above for the full reasoning):

11. **U1** — `Program.cs` still calls `ModLibrary.LoadAll()` **before** `ModLibrary.Bind()`, and StarMap still implements `[StarMapAllModsLoaded]` as a postfix on `LoadAll`.
12. **U2** — `DeviceMeshInterleaved.Shared.Build()` still one-shot; `Rebuild()` still cannot grow; `RunningVertex/IndexBufferSize` still public static settable `uint`; `IsBuilt` still readable.
13. Reflection names: `ModLibrary.{AllParts, AllMeshes, AllFiles, AllMaterials, AllPartGameDataReferences, AllEditorTagDefinitions}`, `SerializedCollection<T>._collection`, `VehicleEditor._editorTagLookup` — plus **U4** (still no removal API on `SerializedCollection<T>`).
14. **U3** — `ThumbnailRenderResources.AddDraw` + `PartModel(.Glass/.Dynamic).WriteInstancesToGpu` still dereference `Material.DiffuseReference`/`.NormalReference`/`.PBRMap` unguarded (if not, relax V9).
15. **U6** — `ThumbnailDynamic.Render`'s `ResetRootPart`/`AddPart`/`MoveRootPart` block is still outside its try/catch; **U7** — `Loading.OnFrame()` still early-returns on `!Program.IsMainThread()`.
16. Thumbnail surface: `ThumbnailCreator.{ResetRootPart,AddPart,MoveRootPart,CollectDraws,CreateThumbnailReference}`, `ThumbnailRenderer.{SIZE,ColorFormat,RecordPartRender,*DescriptorSetLayout,Sampler}`, `ThumbnailReference.ImageView`, `ThumbnailDynamic.{UpdateGlobalCameraData,SetSelectedPart}`, `Program.ThumbnailViewport`, `Camera.Unfollow(bool)`.
17. Asset-pipeline surface: `XmlHelper.Serializers[typeof(AssetBundle)]`, `Mod.MakeUsing`/`Preload`, `ModLibrary.{Loaders,Binders,Manifest,LocalModsFolderPath,MOD_TOML,CONTENT_FOLDER}`, `ModManifest.Save`, `ModEntry`, `FileReference.{LocalPath,IsReference,Load}`, `TextureReference.Dispose(Device)` — and **U8/U9** (GLB mesh-naming rule, `Load()`'s swallowed exceptions).
18. `BindlessTextureLibrary.{TextureCount,MaxTextures}` (`Planet.Render.Core`) + `Renderer.Allocator : KsaVmaAllocator` (`Brutal.Vulkan.Vma`) still resolve — the two new game DLL references.
