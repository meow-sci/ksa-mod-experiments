# Space Tape — In-Game Part Editor

## Overview

**Space Tape** is a Part editor mod for KSA that lets players compose new Parts from existing SubParts. SubParts are the atomic meshes in KSA (panels, screws, pipes, hinges, switches, etc.), and Parts are arrangements of SubParts with position/rotation/scale transforms. Today there is no in-game tooling for authoring Parts — they are hand-written in XML. Space Tape fills that gap.

The editor will be integrated into the **grant supermod** as a new `ISubmod`, following the same lifecycle and UI patterns as existing submods. It will render SubParts in an isolated 3D editing space (far from any celestials), provide 3D gizmos for translation/rotation/scale, ImGui panels for precise numeric input, a SubPart catalog with thumbnail browsing, and output saved Part definitions as standard KSA mod XML files that the game auto-loads on next start.

---

## Architecture Summary

```
grant/Mod.cs
  └─ _submods.Add(new SpaceTapeSubmod())

space-tape.lib/                          # Core library
  ├─ SpaceTapeSubmod.cs                  # ISubmod entry point
  ├─ PartEditorState.cs                  # Editor state machine & data model (Part + GameData)
  ├─ PartEditorScene.cs                  # 3D scene: VehicleEditingSpace, camera, gizmos
  ├─ PartEditorGizmos.cs                 # Gizmo management (translate, rotate, scale)
  ├─ SubPartCatalog.cs                   # SubPart discovery + thumbnail browsing
  ├─ PartEditorUi.cs                     # ImGui windows (catalog, hierarchy, properties, GameData)
  ├─ PartXmlSerializer.cs               # XML output for <Part> (Assets format)
  ├─ GameDataXmlSerializer.cs            # XML output for <PartGameData> (GameData format)
  ├─ PartModWriter.cs                    # Writes XML to game mods folder, file management
  └─ space-tape.lib.csproj
```

---

## Design Decisions (Resolved)

1. **No Connector editing** — SubParts are positioned absolutely. No attachment point / `<Connector>` authoring needed. (Connectors are a Part-level concept added by hand later if needed.)

2. **Full GameData editor UI** — Provide ImGui controls for mass, drag, editor tags, and other `<PartGameData>` fields. Serialize GameData to a separate XML file persisted alongside the Assets XML in the custom-parts mod folder.

3. **Multiple Parts per file** — Users pick a filename from a combobox of existing files or type a new name. Multiple `<Part>` definitions can live in a single Assets XML. Same for GameData XML.

4. **Hot-reload: experimental spike first** — Attempt runtime hot-reload of newly saved Parts into the current session (register PartTemplate into ModLibrary without restart). However, this is **gated on a manual test**: implement a minimal experimental spike, have the user test in-game to see if it works, and only commit to a full implementation if the spike succeeds. If hot-reload proves unreliable, fall back to requiring a game restart.

---

## Detailed Implementation Plan

---

### Phase 1: Project Scaffolding & SubPart Catalog

---

#### Task 1.1 — Create `space-tape.lib` project scaffold

**Goal:** Create the .lib project, ISubmod implementation, and register it in grant. After this task, `dotnet build` succeeds and "Space Tape" appears in Grant's Toolbox.

**Files to create:**

1. **`space-tape.lib/space-tape.lib.csproj`** — Copy the structure from `inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj`. Key differences:
   - `<AssemblyName>MeowSci.SpaceTapeLib</AssemblyName>`
   - `<RootNamespace>MeowSci.SpaceTapeLib</RootNamespace>`
   - Must include `<ProjectReference>` to both:
     - `../ksa-abstractions.lib/ksa-abstractions.lib.csproj`
     - `../inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj`
   - Copy the exact same `<Reference>` block for KSA game DLLs from `inanimate-carbon-rod.lib.csproj` — this includes: `Brutal.Core.Common`, `Brutal.Core.Numerics`, `Brutal.ImGui`, `Brutal.ImGui.Abstractions`, `Brutal.Core.Strings`, `KSA`, `Brutal.Vulkan`, `Brutal.Vulkan.Abstractions`, `Planet.Render.Core`
   - Same `<PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />`

2. **`space-tape.lib/SpaceTapeSubmod.cs`** — Minimal ISubmod implementation. Follow the exact pattern from `InanimateCarbonRodSubmod.cs` (inanimate-carbon-rod.lib/InanimateCarbonRodSubmod.cs):
   ```csharp
   using System;
   using Brutal.ImGuiApi;
   using Brutal.Numerics;
   using MeowSci.KsaAbstractions;

   namespace MeowSci.SpaceTapeLib;

   public sealed class SpaceTapeSubmod : ISubmod
   {
       public string Name => "Space Tape";
       public string Tooltip => "Build Parts from SubParts with a visual 3D editor.";

       public void Initialize() { }
       public void Update(double dt) { }

       public void RenderContent()
       {
           SubmodUI.BeginContentArea("##st_content");
           ImGui.Text("Space Tape editor — coming soon.");
           SubmodUI.EndContentArea();
       }

       public void RenderFloatingWindows() { }
       public void Dispose() { }
   }
   ```

**Files to modify:**

3. **`grant/grant.csproj`** — Add a new `<ProjectReference>` line in the existing `<ItemGroup>` that contains all the other .lib references (see lines 83-103 of grant.csproj):
   ```xml
   <ProjectReference Include="..\space-tape.lib\space-tape.lib.csproj" />
   ```

4. **`grant/Mod.cs`** — Two changes:
   - Add `using MeowSci.SpaceTapeLib;` to the using block (after the existing `using MeowSci.*` lines, ~line 24)
   - Add `_submods.Add(new SpaceTapeSubmod());` in `OnFullyLoaded()` after the existing submod registrations (~line 75, after the ZippoSubmod line)

5. **`ksa-mod-experiments.slnx`** — Add the new project to the solution file. Look at the existing `.lib` entries for the exact format and add a line for `space-tape.lib/space-tape.lib.csproj`.

**Validation:** Run `dotnet build` from the repository root. Must compile with zero errors.

---

#### Task 1.2 — SubPart catalog browser with thumbnails

**Goal:** Create a browsable SubPart catalog that discovers all SubPart templates from the game, generates thumbnails via the existing inanimate-carbon-rod thumbnail pipeline, and presents them in a filterable grid. Clicking a SubPart queues it for placement (actual placement happens in Phase 3).

**Files to create:**

1. **`space-tape.lib/SubPartCatalog.cs`** — SubPart discovery and thumbnail management.

   **SubPart discovery pattern** — Copy exactly from `inanimate-carbon-rod.lib/SubpartThumbnailGenerator.cs` lines 367-384:
   ```csharp
   using System.Reflection;
   using KSA;

   // Access ModLibrary.AllParts via reflection:
   FieldInfo field = typeof(ModLibrary).GetField("AllParts",
       BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
   object collection = field.GetValue(null);
   MethodInfo getList = collection.GetType().GetMethod("GetList");
   List<PartTemplate> allParts = (List<PartTemplate>)getList.Invoke(collection, null);

   // Filter to only SubParts:
   var subParts = allParts.Where(p => p.IsSubPart).ToList();
   // PartTemplate.IsSubPart is a public bool field (decomp/ksa/KSA/PartTemplate.cs line 83)
   ```

   **Thumbnail integration** — Use `SubpartThumbnailCache` from `inanimate-carbon-rod.lib/SubpartThumbnailCache.cs`:
   - Check `SubpartThumbnailCache.HasAny` to see if thumbnails are already generated
   - Access individual entries via `SubpartThumbnailCache.All` (Dictionary<string, SubpartThumbnailEntry>)
   - Each `SubpartThumbnailEntry` has `ThumbnailReference[] Views` — use `Views[0]` for static display
   - Call `view.CreateImGuiThumbnail(Program.LinearClampedSampler)` before using `view.ImGuiImageRef` for ImGui.Image()
   - Call `view.DestroyImGuiThumbnail()` when scrolled out of view (see InanimateCarbonRodSubmod.cs lines 581-588 for the virtual rendering pattern)

   If thumbnails aren't generated yet, show a "Generate Thumbnails" button that creates a `SubpartThumbnailGenerator` instance and calls `GenerateAll()`. The generator runs incrementally — call `_generator.Update()` each frame from the submod's `Update(dt)`.

   **Key class structure:**
   ```csharp
   public sealed class SubPartCatalog
   {
       private List<PartTemplate> _allSubParts = new();
       private SubpartThumbnailGenerator? _generator;
       private string? _selectedSubPartId;  // queued for placement
       private ImInputString _filterText = new ImInputString(256);

       public string? TakeSelectedSubPartId() { var id = _selectedSubPartId; _selectedSubPartId = null; return id; }
       public void Discover() { /* reflection to get all SubPart templates */ }
       public void Update() { _generator?.Update(); }
       public void RenderCatalogPanel() { /* ImGui grid with filter */ }
   }
   ```

   **ImGui rendering pattern** — Follow the grid layout from `InanimateCarbonRodSubmod.cs` `RenderThumbnailGrid()` (lines 536-651):
   - Use `ImGui.BeginChild("##st_catalog", ...)` with `ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY`
   - Calculate columns from `ImGui.GetContentRegionAvail().X / thumbnailSize`
   - Use `ImGui.Image(view.ImGuiImageRef, new float2(thumbSize))` for each thumbnail
   - Use `ImGui.InputText("##st_filter", _filterText)` for the text filter
   - Filter by `subPart.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase)`
   - On click: `if (ImGui.IsItemClicked()) _selectedSubPartId = subPart.Id;`
   - Show tooltip on hover: `if (ImGui.IsItemHovered()) ImGui.SetTooltip(subPart.Id);`

2. **Update `space-tape.lib/SpaceTapeSubmod.cs`** — Wire up the catalog:
   ```csharp
   private readonly SubPartCatalog _catalog = new();
   private bool _catalogInitialized;

   public void Initialize() { }

   public void Update(double dt) { _catalog.Update(); }

   public void RenderContent()
   {
       SubmodUI.BeginContentArea("##st_content");
       if (!_catalogInitialized) { _catalog.Discover(); _catalogInitialized = true; }
       _catalog.RenderCatalogPanel();
       SubmodUI.EndContentArea();
   }
   ```

**Validation:** `dotnet build` succeeds. The SubPart catalog appears in the Space Tape section of Grant's Toolbox with thumbnail generation capability.

---

### Phase 2: Editor State Model & XML Serialization

---

#### Task 2.1 — Part editor state model

**Goal:** Define the in-memory data model for the Part being edited. This is the central data structure that all other components (UI, scene, serializer) read from and write to.

**File to create: `space-tape.lib/PartEditorState.cs`**

```csharp
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

/// <summary>Represents one placed SubPart instance in the editor.</summary>
public sealed class SubPartPlacement
{
    public string InstanceId { get; set; }          // unique per-part instance name (e.g. "panel_1")
    public string SubPartTemplateId { get; set; }   // references PartTemplate.Id where IsSubPart==true
    public double3 Position { get; set; }           // metres, Y-up Z-forward
    public doubleQuat Rotation { get; set; }        // quaternion (XML uses Euler XYZ radians)
    public double3 Scale { get; set; }              // dimensionless per-axis scale

    public SubPartPlacement Clone() => new()
    {
        InstanceId = InstanceId,
        SubPartTemplateId = SubPartTemplateId,
        Position = Position,
        Rotation = Rotation,
        Scale = Scale
    };
}
```

**Key design notes:**
- Position/Rotation/Scale match `Part.PositionParentAsmb`, `Part.Asmb2ParentAsmb`, `Part.Scale` (decomp/ksa/KSA/Part.cs line 640-692). The Part constructor reads these from `TransformReference.Create()` which returns a `Transform` struct with `Position`, `Rotation`, `Scale` fields.
- Rotation stored as `doubleQuat` internally. The game's `TransformReference` (decomp/ksa/KSA/TransformReference.cs) converts via `QuaternionEx.CreateFromXyzRadians()` for reading and `doubleQuat.ToXyzRadians()` for writing. Our XML serializer will use the same conversion.
- Scale default is `double3.One` (1,1,1) — see `TransformReference.ScaleValue` getter (line 48).

**EditingPart class** — the full Part being assembled:
```csharp
public sealed class EditingPart
{
    public string PartId { get; set; } = "NewPart";
    public List<SubPartPlacement> Placements { get; } = new();
    public PartGameDataState GameData { get; } = new();

    public EditingPart DeepClone() => new()
    {
        PartId = PartId,
        // clone all placements into the new list
        // clone GameData
    };
}
```

**PartGameDataState** — metadata fields matching `PartTemplate` attributes (decomp/ksa/KSA/PartTemplate.cs lines 12-85):
```csharp
public sealed class PartGameDataState
{
    public string DisplayName { get; set; } = "";
    public List<string> EditorTags { get; } = new();   // e.g. "Structural", "Command", "Cargo"
    // Mass: KSA supports multiple mass model types. Start with CustomMass (simplest).
    public double CustomMass { get; set; }              // kg — maps to <CustomMass Mass="..."/>
    public double CustomMassDryRatio { get; set; } = 1.0;
    // Additional optional fields (collapsible in UI):
    public double? BatteryCapacity { get; set; }
    public double? GeneratorOutput { get; set; }
}
```

**Editor state machine:**
```csharp
public enum EditorMode { Idle, Editing, Saving }

public sealed class PartEditorController
{
    public EditingPart CurrentPart { get; private set; } = new();
    public EditorMode Mode { get; set; } = EditorMode.Idle;
    public int SelectedPlacementIndex { get; set; } = -1;  // -1 = none selected
    public SubPartPlacement? SelectedPlacement =>
        SelectedPlacementIndex >= 0 && SelectedPlacementIndex < CurrentPart.Placements.Count
            ? CurrentPart.Placements[SelectedPlacementIndex] : null;

    // Undo stack: list of deep-cloned EditingPart snapshots
    private readonly List<EditingPart> _undoStack = new();
    private int _undoIndex = -1;
    private const int MaxUndoDepth = 50;

    public void PushUndo() { /* deep clone CurrentPart, add to stack, trim if > MaxUndoDepth */ }
    public void Undo() { /* restore from _undoStack[--_undoIndex] */ }
    public void Redo() { /* restore from _undoStack[++_undoIndex] */ }
    public void NewPart() { CurrentPart = new EditingPart(); SelectedPlacementIndex = -1; _undoStack.Clear(); }
}
```

**SubPartPlacement creation when adding from catalog:**
```csharp
public void AddSubPart(string subPartTemplateId)
{
    PushUndo();
    var placement = new SubPartPlacement
    {
        InstanceId = $"{subPartTemplateId}_{CurrentPart.Placements.Count + 1}",
        SubPartTemplateId = subPartTemplateId,
        Position = double3.Zero,
        Rotation = doubleQuat.Identity,
        Scale = double3.One
    };
    CurrentPart.Placements.Add(placement);
    SelectedPlacementIndex = CurrentPart.Placements.Count - 1;
}
```

**Validation:** `dotnet build` succeeds. Unit-testable data model (no game dependencies in this file — only Brutal.Numerics).

---

#### Task 2.2 — Part XML serializer

**Goal:** Serialize `EditingPart` to XML matching the KSA `<Part>` format. The output must be loadable by the game's XML parser.

**File to create: `space-tape.lib/PartXmlSerializer.cs`**

**Target XML format** — Based on decomp/ksa/Content/Core/CoreCommandAAssets.xml:
```xml
<Assets>
  <Part Id="MyCustomPart">
    <SubPart Id="panel_1" InstanceOf="CorePanel_A">
      <Transform>
        <Position X="0" Y="0.5" Z="0"/>
        <Rotation X="0" Y="0" Z="1.5708"/>
        <Scale X="1" Y="1" Z="1"/>
      </Transform>
    </SubPart>
    <SubPart Id="screw_1" InstanceOf="CoreScrew_B">
      <Transform>
        <Position X="0.1" Y="0.2" Z="-0.3"/>
      </Transform>
    </SubPart>
  </Part>
</Assets>
```

**Key implementation details:**
- Use `System.Xml.Linq` (XDocument/XElement) for generation — simpler than XmlSerializer for custom output
- The `<SubPart>` node inside `<Part>` has `Id` attribute (instance ID) and `InstanceOf` attribute (template ID) — see `PartInstance` class: `[XmlAttribute] public string InstanceOf` (decomp/ksa/KSA/PartInstance.cs line 16), and `SerializedId.Id` which provides the `Id` attribute
- `<Transform>` child contains `<Position X="" Y="" Z=""/>`, `<Rotation X="" Y="" Z=""/>`, `<Scale X="" Y="" Z=""/>` — all as XML elements with X/Y/Z attributes (see `TransformReference` decomp/ksa/KSA/TransformReference.cs: `Vector3Reference` with `[XmlAttribute] X, Y, Z`)
- **Rotation conversion**: Internal `doubleQuat` → Euler XYZ radians for XML. Use `doubleQuat.NormalizedOrIdentity().ToXyzRadians()` (this is what `TransformReference.RotationValue` setter uses at line 39). Note: `QuaternionEx.CreateFromXyzRadians()` is the reverse.
- **Optimization**: Omit `<Rotation>` if identity, omit `<Scale>` if (1,1,1), omit `<Position>` if (0,0,0). The game handles missing elements gracefully (returns zero/identity/one defaults — see `TransformReference` getters).
- Float formatting: Use `"G"` or `"R"` format specifier for round-trip fidelity. The game's XML uses values like `"0"`, `"0.5"`, `"1.5708"`.

**Method signatures:**
```csharp
public static class PartXmlSerializer
{
    /// <summary>Serializes a single Part to an XElement.</summary>
    public static XElement SerializePart(EditingPart part) { ... }

    /// <summary>Wraps one or more Part XElements in an <Assets> root.</summary>
    public static XDocument WrapInAssets(IEnumerable<XElement> partElements) { ... }

    /// <summary>Merges a Part into an existing Assets XML file (replaces if same Id, appends otherwise).</summary>
    public static XDocument MergeIntoAssets(XDocument existing, XElement newPart) { ... }
}
```

**Validation:** `dotnet build` succeeds. Write a simple test in the code (or as console output) that serializes a sample EditingPart and prints the XML to verify format.

---

#### Task 2.3 — GameData XML serializer

**Goal:** Serialize `PartGameDataState` to XML matching the KSA `<PartGameData>` format.

**File to create: `space-tape.lib/GameDataXmlSerializer.cs`**

**Target XML format** — Based on investigation of `PartTemplate.ApplyGameData()` (decomp/ksa/KSA/PartTemplate.cs lines 187-260) and `PartGameDataReference` (which extends PartTemplate with `_isGameData = true`):
```xml
<GameData>
  <PartGameData Id="MyCustomPart">
    <DisplayName>My Custom Part</DisplayName>
    <EditorTag>Structural</EditorTag>
    <EditorTag>Cargo</EditorTag>
    <CustomMass Mass="10.5" DryMassRatio="1.0"/>
    <!-- Optional: -->
    <Battery Capacity="100"/>
    <Generator Output="50"/>
  </PartGameData>
</GameData>
```

**Important**: The `<PartGameData Id="...">` must match the `<Part Id="...">` from the Assets XML. The game's `ModLibrary.AttachGameData()` merges them by matching IDs. The `PartGameDataReference` class sets `_isGameData = true` and during `OnDataLoad()` it calls `ModLibrary.Register(this)` — if registration fails (ID already exists), it finds the existing PartTemplate and calls `ApplyGameData(this)` to merge fields.

**Key XML elements from PartTemplate** (decomp/ksa/KSA/PartTemplate.cs):
- `[XmlAttribute] DisplayName` (line 13) — becomes attribute on root element
- `[XmlElement("EditorTag")] List<StringReference> EditorTagsStrings` (lines 24-25)
- `[XmlElement("CustomMass")] ... List<AsmbMassTemplate> InertMasses` (lines 36-47) — CustomMassTemplate has `Mass` and `DryMassRatio` attributes
- `[XmlElement("Battery")] List<BatteryTemplate> Batteries` (lines 53-54) — BatteryTemplate has `Capacity`
- `[XmlElement("Generator")] List<GeneratorTemplate> Generators` (lines 56-57) — GeneratorTemplate has `Output`

**Method signatures:**
```csharp
public static class GameDataXmlSerializer
{
    public static XElement SerializeGameData(string partId, PartGameDataState gameData) { ... }
    public static XDocument WrapInGameData(IEnumerable<XElement> gameDataElements) { ... }
    public static XDocument MergeIntoGameData(XDocument existing, XElement newGameData) { ... }
}
```

**Validation:** `dotnet build` succeeds.

---

#### Task 2.4 — Mod file writer

**Goal:** Write serialized Part + GameData XML files to the custom-parts mod directory so the game loads them on next start.

**File to create: `space-tape.lib/PartModWriter.cs`**

**Output directory** — Use `KsaPaths.UserDataDir` from `ksa-abstractions.lib/KsaPaths.cs`:
```csharp
string modsDir = Path.Combine(KsaPaths.UserDataDir, "mods", "space-tape-parts");
```
This maps to `%USERPROFILE%\Documents\My Games\Kitten Space Agency\mods\space-tape-parts\`.

**Mod directory structure:**
```
space-tape-parts/
  mod.toml              # Required by StarMap mod loader
  Assets/
    my-parts.xml        # <Assets><Part>...</Part></Assets>
  GameData/
    my-parts.xml        # <GameData><PartGameData>...</PartGameData></GameData>
```

**`mod.toml` template** — The exact format is defined by StarMap. Minimal:
```toml
[mod]
name = "Space Tape Custom Parts"
author = "Space Tape Editor"
version = "1.0.0"
```

**File picker state** — managed in `PartModWriter`:
```csharp
public sealed class PartModWriter
{
    private string _modDir;
    private string _assetsDir;
    private string _gameDataDir;

    // File selection state
    private string[] _existingFiles = Array.Empty<string>();
    private int _selectedFileIndex = 0;
    private ImInputString _newFileName = new ImInputString(128);
    private bool _useNewFile;

    public void EnsureModDirectory() { /* create dirs, write mod.toml if missing */ }
    public void RefreshFileList() { /* scan _assetsDir for *.xml files */ }

    /// <summary>Renders file picker UI (combobox + new file text input).</summary>
    public void RenderFilePicker() { ... }

    /// <summary>Saves Part + GameData to the selected file.</summary>
    public void SavePart(EditingPart part)
    {
        EnsureModDirectory();
        string fileName = _useNewFile ? _newFileName.ToString() + ".xml" : _existingFiles[_selectedFileIndex];

        // Assets XML
        string assetsPath = Path.Combine(_assetsDir, fileName);
        XDocument assetsDoc = File.Exists(assetsPath) ? XDocument.Load(assetsPath) : PartXmlSerializer.WrapInAssets(Array.Empty<XElement>());
        assetsDoc = PartXmlSerializer.MergeIntoAssets(assetsDoc, PartXmlSerializer.SerializePart(part));
        assetsDoc.Save(assetsPath);

        // GameData XML
        string gameDataPath = Path.Combine(_gameDataDir, fileName);
        XDocument gdDoc = File.Exists(gameDataPath) ? XDocument.Load(gameDataPath) : GameDataXmlSerializer.WrapInGameData(Array.Empty<XElement>());
        gdDoc = GameDataXmlSerializer.MergeIntoGameData(gdDoc, GameDataXmlSerializer.SerializeGameData(part.PartId, part.GameData));
        gdDoc.Save(gameDataPath);
    }
}
```

**ImGui file picker** — Use `ImGui.BeginCombo()` for existing files and `ImGui.InputText()` for new filename. Follow the combo pattern from `ZippoSubmod.cs` (lines 787-801 in the project-structure agent output): `ImGui.BeginCombo("##st_file", ...)` → `ImGui.Selectable(...)` → `ImGui.EndCombo()`.

**Merge logic for MergeIntoAssets**: Load the existing XDocument, find `<Part Id="X">` matching the Part being saved. If found, replace the element. If not found, append as new child of `<Assets>`.

**Validation:** `dotnet build` succeeds.

---

### Phase 3: 3D Editing Scene

---

#### Task 3.1 — Isolated editing scene setup

**Goal:** When the user clicks "Open Editor", create an isolated 3D editing space far from celestials, move the camera there, and render an origin marker. When closing, restore previous camera state.

**File to create: `space-tape.lib/PartEditorScene.cs`**

**VehicleEditingSpace creation** — Replicate the pattern from `VehicleEditor.Build()` (decomp/ksa/KSA/VehicleEditor.cs lines 254-334):
```csharp
using KSA;
using Brutal.Numerics;

// Position: 10× the sun's mean radius along Z-axis (same as vehicle editor)
double sunRadius = Universe.CurrentSystem.GetWorldSun().MeanRadius;
double3 editPos = new double3(0, 0, 10.0 * sunRadius);

// Create the editing space with no existing parts, 10m initial radius
// VehicleEditingSpace is a public class (decomp/ksa/KSA/VehicleEditingSpace.cs line 6)
var editingSpace = new VehicleEditingSpace(editPos, doubleQuat.Identity, 10.0, null);
```

**Camera setup** — Follow VehicleEditor.Build() lines 329-332:
```csharp
// Save current camera state for restoration later
IFollowable? _savedFollowing;
CameraMode _savedCameraMode;

void EnterEditor()
{
    var camera = Program.GetCamera();  // Program.GetCamera() or Program.MainViewport.GetCamera()
    _savedFollowing = camera.Following;
    _savedCameraMode = Program.MainViewport.Mode;

    Program.SetCameraMode(CameraMode.Orbit);

    // Set all 3 cameras to follow the editing space (from VehicleEditor.Build lines 330-332):
    Program.MainViewport.MapCamera.SetFollow(editingSpace, tidalLocking: false, changeControl: true, alert: false);
    Program.MainViewport.BaseCamera.SetFollow(editingSpace, tidalLocking: false, changeControl: true, alert: false);
    Program.GetHoveredCamera().SetFollow(editingSpace, tidalLocking: false, changeControl: true, alert: false);
}

void ExitEditor()
{
    if (_savedFollowing != null)
    {
        Program.SetCameraMode(_savedCameraMode);
        Program.MainViewport.MapCamera.SetFollow(_savedFollowing, tidalLocking: false, changeControl: true, alert: false);
        Program.MainViewport.BaseCamera.SetFollow(_savedFollowing, tidalLocking: false, changeControl: true, alert: false);
    }
}
```

**Note on `Program` access:** `Program` is a public static class. `Program.MainViewport` is a static property returning `Viewport`. `Program.SetCameraMode()` is a public static method. `Program.GetCamera()` returns the active camera. These are all public API from the game — check via `typeof(Program)` properties at runtime if any fail.

**`SetFollow` method** on `Camera` (see decomp): `public void SetFollow(IFollowable target, bool tidalLocking, bool changeControl = false, bool alert = false)`. VehicleEditingSpace implements `IFollowable`, `IPosition`, `IVelocity`, `IOrientation`, `IRadius` — all needed for camera following.

**Coordinate helpers:**
```csharp
public double4x4 GetMatrixAsmb2Ego(Viewport viewport)
{
    return _editingSpace.GetMatrixAsmb2Ego(viewport.GetCamera());
    // From VehicleEditingSpace.cs line 100-104:
    // double3 position = camera.EclToEgo(PositionEcl);
    // return double4x4.CreateFromQuaternion(Asmb2Ecl) * double4x4.CreateTranslation(position);
}
```

**Origin marker** — Use GenericGizmo with "Box" mesh to render 3 small colored boxes at the origin:
```csharp
// Create a single-use gizmo for the origin marker
var originGizmo = new GenericGizmo(ModLibrary.Get<MeshReference>("Box"),
    GenericGizmo.Static.GenericGizmoRenderData, 3);
// Update each frame:
var seg = originGizmo.GetSegmentDataByViewport(viewport);
// X-axis marker (red)
seg[0].Active = true; seg[0].PositionEgo = origin; seg[0].Scale = new double3(0.5, 0.02, 0.02);
seg[0].Color = new double4(1, 0, 0, 0.5); seg[0].Body2Cce = doubleQuat.Identity;
// Y-axis marker (green)
seg[1].Active = true; seg[1].PositionEgo = origin; seg[1].Scale = new double3(0.02, 0.5, 0.02);
seg[1].Color = new double4(0, 1, 0, 0.5); seg[1].Body2Cce = doubleQuat.Identity;
// Z-axis marker (blue)
seg[2].Active = true; seg[2].PositionEgo = origin; seg[2].Scale = new double3(0.02, 0.02, 0.5);
seg[2].Color = new double4(0, 0, 1, 0.5); seg[2].Body2Cce = doubleQuat.Identity;
```

**Key class structure:**
```csharp
public sealed class PartEditorScene : IDisposable
{
    private VehicleEditingSpace? _editingSpace;
    private GenericGizmo? _originGizmo;
    private IFollowable? _savedFollowing;
    private CameraMode _savedCameraMode;
    public bool IsActive { get; private set; }

    public void Enter() { /* create space, save camera, switch */ }
    public void Exit() { /* restore camera, dispose space */ }
    public void UpdateFrame(Viewport viewport) { /* update origin gizmo */ }
    public double4x4 GetMatrixAsmb2Ego(Viewport viewport) { ... }
    public VehicleEditingSpace EditingSpace => _editingSpace!;
    public void Dispose() { if (IsActive) Exit(); }
}
```

**Validation:** `dotnet build` succeeds. When the editor is opened in-game, the camera jumps to an empty space with origin axes visible.

---

#### Task 3.2 — SubPart rendering in editor

**Goal:** When SubParts are placed, create real runtime `Part` instances and render them in the editing space using the game's standard rendering pipeline.

**Modify: `space-tape.lib/PartEditorScene.cs`**

**Part creation** — Use the `Part(string, PartTemplate, PartInstance?)` constructor (decomp/ksa/KSA/Part.cs line 640):
```csharp
using KSA;

// For each SubPartPlacement in the EditingPart:
PartTemplate template = ModLibrary.Get<PartTemplate>(placement.SubPartTemplateId);
// The PartTemplate for SubParts has IsSubPart == true

// Create a Part instance (this creates the Part's PartTree, modules, bounding box, etc.)
Part part = new Part(placement.InstanceId, template);

// Set transforms from our placement data:
part.PositionParentAsmb = placement.Position;
part.Asmb2ParentAsmb = placement.Rotation;
part.Scale = placement.Scale;
```

**Part tree management** — The editing space needs a `PartTree` to hold parts. The VehicleEditor uses `EditingSpace.Parts` (a `PartTree`) and also `UnattachedPartTrees` for new parts. For our simpler editor:
```csharp
// When creating the first part:
if (_editingSpace.Parts == null)
{
    _editingSpace.Parts = part.Tree;
}
else
{
    // Add subsequent parts to the existing tree
    // Use: part.Tree = _editingSpace.Parts; then add as child
    // Or: keep separate PartTree instances in a list and render each
}
```

**IMPORTANT: Rendering happens automatically.** Once parts are in a `PartTree` that's associated with the editing space, the game's rendering pipeline picks them up via `PartModelModule.UpdateRenderData()` which is called in the main render loop. The key is that the camera is following our `VehicleEditingSpace`, so the matrix transforms are correct.

However, there's a subtlety: in the vehicle editor, `Vehicle.UpdateRenderData()` iterates all parts and calls their `PartModelModule.UpdateRenderData()`. For an isolated editing space WITHOUT a Vehicle, we may need to manually call this. The approach:

```csharp
// Each frame, for each Part in our editor:
public void UpdateRenderData(Viewport viewport, int frameIndex)
{
    var matrixAsmb2Ego = GetMatrixAsmb2Ego(viewport);
    foreach (var part in _editorParts)
    {
        // Get PartModelModule from the Part's modules
        var modules = part.Modules.Get<PartModelModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            modules[i].UpdateRenderData(in matrixAsmb2Ego, false, viewport, frameIndex);
        }

        // Also update subpart modules (Part constructor creates sub-parts from template)
        foreach (var subPart in part.SubParts)
        {
            var subModules = subPart.Modules.Get<PartModelModule>();
            for (int j = 0; j < subModules.Length; j++)
            {
                subModules[j].UpdateRenderData(in matrixAsmb2Ego, false, viewport, frameIndex);
            }
        }
    }
}
```

**Getting frame index and viewport** — Access via `Program.Instance`:
```csharp
Viewport viewport = Program.MainViewport;  // or Program.RenderedViewport
// Frame index: Program.Instance.ResourceFrameIndex (cycles 0..MaxFramesInFlight-1)
// Access via reflection if not public:
var resourceFrameIndex = (int)typeof(Program).GetProperty("ResourceFrameIndex")?.GetValue(Program.Instance);
```

**Part state properties** — These cascade to sub-parts automatically (from Part.cs):
```csharp
part.Selected = true;      // visual highlight for selected
part.Highlighted = true;   // hover highlight
part.Grabbed = false;      // drag state
part.FakeTranslucent = false;
```

**Keeping editor parts in sync with PartEditorState:**
```csharp
// Maintain a Dictionary<int, Part> mapping placement index → runtime Part
private readonly Dictionary<int, Part> _runtimeParts = new();

public void SyncParts(EditingPart editingPart)
{
    // Add new parts, remove deleted ones, update transforms for existing
    for (int i = 0; i < editingPart.Placements.Count; i++)
    {
        var placement = editingPart.Placements[i];
        if (!_runtimeParts.TryGetValue(i, out Part? part))
        {
            // Create new Part
            part = CreatePartFromPlacement(placement);
            _runtimeParts[i] = part;
        }
        // Update transforms
        part.PositionParentAsmb = placement.Position;
        part.Asmb2ParentAsmb = placement.Rotation;
        part.Scale = placement.Scale;
    }
    // Remove parts that no longer exist in placements
}
```

**Validation:** `dotnet build` succeeds. When a SubPart is added from the catalog, it appears in the 3D editing space.

---

### Phase 4: 3D Gizmos & Interaction

---

#### Task 4.1 — Gizmo management

**Goal:** Create translate/rotate/scale gizmos that appear at the selected SubPart's location and handle per-frame visual updates.

**File to create: `space-tape.lib/PartEditorGizmos.cs`**

**Gizmo creation** — Exactly replicates VehicleEditor.Build() lines 257-260:
```csharp
using KSA;
using Brutal.Numerics;

public sealed class PartEditorGizmos : IDisposable
{
    public enum GizmoMode { Translate, Rotate, Scale }

    private GenericGizmo _translateGizmo;
    private GenericGizmo _rotationGizmo;
    private GenericGizmo _scaleGizmo;
    public GizmoMode ActiveMode { get; set; } = GizmoMode.Translate;

    // Interaction state
    public GenericGizmo? HighlightedGizmo { get; set; }
    public int HighlightedGizmoSegmentIndex { get; set; } = -1;
    public bool GizmoGrabbed { get; set; }

    private static readonly double4 GIZMO_HIGHLIGHT = new(1.0, 1.0, 1.0, 0.75);

    public void Initialize()
    {
        _translateGizmo = new GenericGizmo(
            ModLibrary.Get<MeshReference>("ArrowMesh"),
            GenericGizmo.Static.GenericGizmoRenderData, 3);
        _rotationGizmo = new GenericGizmo(
            ModLibrary.Get<MeshReference>("CircleMesh"),
            GenericGizmo.Static.GenericGizmoRenderData, 4);
        _scaleGizmo = new GenericGizmo(
            ModLibrary.Get<MeshReference>("BoxedArrowMesh"),
            GenericGizmo.Static.GenericGizmoRenderData, 3);
    }
    // ...
}
```

**Per-frame gizmo update** — Replicates `UpdateTranslateGizmo()` from VehicleEditor.cs lines 920-969. The pattern is the same for all three gizmo types:
```csharp
public void UpdateGizmos(Part? selectedPart, ref readonly double4x4 matrixAsmb2Ego,
    doubleQuat asmb2Ecl, Viewport viewport)
{
    // Deactivate all segments if no selection
    if (selectedPart == null)
    {
        DeactivateAllSegments(viewport);
        return;
    }

    GenericGizmo activeGizmo = ActiveMode switch
    {
        GizmoMode.Translate => _translateGizmo,
        GizmoMode.Rotate => _rotationGizmo,
        GizmoMode.Scale => _scaleGizmo,
        _ => _translateGizmo
    };

    // Deactivate inactive gizmos
    DeactivateGizmo(_translateGizmo != activeGizmo ? _translateGizmo : null, viewport);
    DeactivateGizmo(_rotationGizmo != activeGizmo ? _rotationGizmo : null, viewport);
    DeactivateGizmo(_scaleGizmo != activeGizmo ? _scaleGizmo : null, viewport);

    // Position the active gizmo at the selected part
    doubleQuat orientation = selectedPart.Asmb2Ego(asmb2Ecl);
    double3 positionEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
    var seg = activeGizmo.GetSegmentDataByViewport(viewport);

    // X-axis (Red) — segment 0
    seg[0].Active = true;
    seg[0].PositionEgo = positionEgo;
    seg[0].Body2Cce = orientation;  // default arrow points along X
    seg[0].Scale = new double3(2.0, 2.0, 2.0);
    seg[0].Color = new double4(1.0, 0.0, 0.0, 0.75);

    // Y-axis (Green) — segment 1, rotated 90° around Z from X
    seg[1].Active = true;
    seg[1].PositionEgo = positionEgo;
    seg[1].Body2Cce = doubleQuat.CreateFromAxisAngle(
        Double3Ex.Backward.Transform(orientation), Math.PI / 2.0) * orientation;
    seg[1].Scale = new double3(2.0, 2.0, 2.0);
    seg[1].Color = new double4(0.0, 1.0, 0.0, 0.75);

    // Z-axis (Blue) — segment 2, rotated -90° around Y from X
    seg[2].Active = true;
    seg[2].PositionEgo = positionEgo;
    seg[2].Body2Cce = doubleQuat.CreateFromAxisAngle(
        Double3Ex.Down.Transform(orientation), Math.PI / 2.0) * orientation;
    seg[2].Scale = new double3(2.0, 2.0, 2.0);
    seg[2].Color = new double4(0.0, 0.0, 1.0, 0.75);

    // Highlight hovered axis
    if (HighlightedGizmo == activeGizmo && HighlightedGizmoSegmentIndex >= 0)
    {
        seg[HighlightedGizmoSegmentIndex].Color = double4.Lerp(
            seg[HighlightedGizmoSegmentIndex].Color, GIZMO_HIGHLIGHT, 0.5);
    }

    // For rotation gizmo: segment 3 is the "free rotation" ring (optional, can skip initially)
    if (ActiveMode == GizmoMode.Rotate && seg.Length > 3)
        seg[3].Active = false;  // hide 4th segment for now
}
```

The axis rotation math is taken directly from VehicleEditor.cs `UpdateTranslateGizmo()` lines 942-963. `Double3Ex.Backward` and `Double3Ex.Down` are built-in unit direction helpers from the game.

**Raycast for hover detection:**
```csharp
public void UpdateRaycast(Ray ray, Viewport viewport)
{
    double closestT = double.MaxValue;
    GenericGizmo? closestGizmo = null;
    int closestSegment = -1;

    GenericGizmo activeGizmo = ActiveMode switch { ... };

    if (activeGizmo.RaycastEgo(ray, viewport, out double t, out int segIdx) && t < closestT)
    {
        closestT = t;
        closestGizmo = activeGizmo;
        closestSegment = segIdx;
    }

    HighlightedGizmo = closestGizmo;
    HighlightedGizmoSegmentIndex = closestSegment;
}
```

**Validation:** `dotnet build` succeeds. Gizmos render at the selected part's position.

---

#### Task 4.2 — Mouse interaction (selection & gizmo dragging)

**Goal:** Implement click-to-select SubParts, click+drag gizmo axes for transform manipulation, and click-to-place new SubParts from catalog.

**File to create: `space-tape.lib/PartEditorInteraction.cs`**

**Screen-to-ray conversion** (from VehicleEditor.cs line 571):
```csharp
Camera camera = viewport.GetCamera();
double2 cursorPos = /* ImGui cursor position or game cursor */;
Ray ray = camera.ScreenToEgoRay(cursorPos);
ray.Direction = ray.Direction.NormalizeOrZero();
```

**Cursor position** — The VehicleEditor uses `CursorPositionScreen` (a `double2`). Access the game cursor via `Program.GetCursorMode()` check and `ImGui.GetMousePos()` or the game's input system. `ImGui.GetMousePos()` returns screen-space coordinates.

**Part selection** (from VehicleEditor.cs OnFrame lines 596-669):
```csharp
// If no gizmo is highlighted, check for part hits
if (HighlightedGizmo == null)
{
    Part? hitPart = null;
    double closestT = double.MaxValue;

    foreach (var part in _runtimeParts.Values)
    {
        // RayCastEgo is on Part — checks mesh intersection
        if (part.RayCastEgo(in matrixAsmb2Ego, ray,
            out double nearT, out double farT,
            out double3 nearPos, out double3 nearNorm,
            out double3 farPos, out double3 farNorm,
            out Part closestSub, out Part farthestSub))
        {
            if (nearT < closestT)
            {
                closestT = nearT;
                hitPart = part;
            }
        }
    }

    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hitPart != null)
    {
        // Select this part
        _controller.SelectedPlacementIndex = IndexOfPart(hitPart);
    }
}
```

**Translate gizmo dragging** — Replicate VehicleEditor.cs lines 1056-1106:
```csharp
// During drag (mouse held):
double3 prevNearPlane = camera.ScreenToEgoNearPlane(cursorPosLastFrame);
double num = prevNearPlane.Length();
double3 currNearPlane = camera.ScreenToEgoNearPlane(cursorPos);
double3 delta = currNearPlane - prevNearPlane;

// Get the axis direction from gizmo segment data
var segData = _translateGizmo.GetSegmentDataByViewport(viewport);
double3 axisDir = Double3Ex.Right.Transform(segData[highlightedSegment].Body2Cce).NormalizeOrZero();

// Project mouse delta onto the axis
double3 projectedDelta = double3.Dot(delta, axisDir) * axisDir;

// Scale by distance ratio (nearer = smaller movement)
double3 partPosEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
double distRatio = partPosEgo.Length() / num;
double3 worldDelta = projectedDelta * distRatio;

// Convert to parent assembly space and apply
double4x4 invParent = selectedPart.MatrixParentAsmb2Ego(in matrixAsmb2Ego).Invert();
// ... apply to PositionParentAsmb
selectedPart.PositionParentAsmb += worldDeltaInParentSpace;
// Also update the SubPartPlacement in PartEditorState
```

**Rotation gizmo dragging** — Replicate VehicleEditor.cs lines 1107-1160:
```csharp
// Calculate rotation angle from screen delta
double3 prev = camera.ScreenToEgoNearPlane(cursorPosLastFrame);
double3 curr = camera.ScreenToEgoNearPlane(cursorPos);
double angle = MathEx.SafeAcos(double3.Dot(prev, curr) / (prev.Length() * curr.Length()));

// Get rotation axis from gizmo segment
var segData = _rotationGizmo.GetSegmentDataByViewport(viewport);
double3 axis = Double3Ex.Right.Transform(segData[highlightedSegment].Body2Cce).NormalizeOrZero();

// Determine rotation direction from cross product
double3 crossVec = double3.Cross(segData[highlightedSegment].PositionEgo, prev - segData[highlightedSegment].PositionEgo);
int signDelta = Math.Sign(double3.Dot(curr - prev, crossVec));
int signAxis = Math.Sign(double3.Dot(axis, prev));

// Create rotation quaternion and apply
double3 localAxis = axis.Transform(doubleQuat.Inverse(selectedPart.ParentAsmb2Ego(vehicleAsmb2Ego)));
doubleQuat rot = doubleQuat.CreateFromAxisAngle(localAxis, angle * signDelta * signAxis);
selectedPart.Asmb2ParentAsmb = doubleQuat.Multiply(rot, selectedPart.Asmb2ParentAsmb);
```

**Scale gizmo dragging** — Replicate VehicleEditor.cs lines 1161-1197:
```csharp
// Similar to translate but applies to Scale components
double3 scale = selectedPart.Scale;
double scaleAmount = (projectedDelta * distRatio).Length() * sign;
if (highlightedSegment == 0) scale.X = double.Clamp(scale.X + scaleAmount, double.Epsilon, double.MaxValue);
else if (highlightedSegment == 1) scale.Y = double.Clamp(scale.Y + scaleAmount, double.Epsilon, double.MaxValue);
else if (highlightedSegment == 2) scale.Z = double.Clamp(scale.Z + scaleAmount, double.Epsilon, double.MaxValue);
selectedPart.Scale = scale;
```

**Writeback to PartEditorState** — After any gizmo drag, sync the Part's runtime transform back to the `SubPartPlacement`:
```csharp
placement.Position = part.PositionParentAsmb;
placement.Rotation = part.Asmb2ParentAsmb;
placement.Scale = part.Scale;
```

**New SubPart placement** — When a SubPart is selected from the catalog:
```csharp
// Place at a default position in front of camera
double3 cameraForward = camera.ScreenToEgoRay(screenCenter).Direction.NormalizeOrZero();
double3 placementPos = cameraForward * 5.0; // 5 metres in front of camera
_controller.AddSubPart(selectedTemplateId);
// Set the new placement's position
_controller.SelectedPlacement.Position = placementPos;
```

**Validation:** `dotnet build` succeeds. Click-to-select, gizmo dragging, and SubPart placement all work.

---

#### Task 4.3 — ImGui property panel & hierarchy

**Goal:** ImGui windows for the SubPart hierarchy tree, per-SubPart transform properties with numeric inputs, and a toolbar.

**File to create/modify: `space-tape.lib/PartEditorUi.cs`**

This is the main UI file. It should contain all the ImGui rendering for the editor (called from `SpaceTapeSubmod.RenderFloatingWindows()`).

**Window structure** — The editor UI should be a floating window (rendered in `RenderFloatingWindows()`, NOT in `RenderContent()`). This ensures it stays visible even when the Grant Toolbox's Space Tape section is collapsed.

```csharp
public sealed class PartEditorUi
{
    private bool _editorWindowOpen;

    public void RenderEditorWindow(PartEditorController controller, PartEditorScene scene,
        PartEditorGizmos gizmos, SubPartCatalog catalog, PartModWriter writer)
    {
        if (!_editorWindowOpen) return;

        ImGui.SetNextWindowSize(new float2(400, 700), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Space Tape — Part Editor##st_editor", ref _editorWindowOpen))
        {
            RenderToolbar(controller, gizmos, writer);
            ImGui.Separator();
            RenderPartIdInput(controller);
            ImGui.Separator();
            RenderHierarchyPanel(controller);
            ImGui.Separator();
            RenderPropertiesPanel(controller);
            ImGui.Separator();
            RenderGameDataPanel(controller);
        }
        ImGui.End();
    }
}
```

**Toolbar** — gizmo mode toggles, undo/redo, save, new:
```csharp
void RenderToolbar(PartEditorController controller, PartEditorGizmos gizmos, PartModWriter writer)
{
    // Gizmo mode radio buttons
    bool isTranslate = gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Translate;
    if (ImGui.RadioButton("Translate (W)", isTranslate)) gizmos.ActiveMode = GizmoMode.Translate;
    ImGui.SameLine();
    bool isRotate = gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Rotate;
    if (ImGui.RadioButton("Rotate (E)", isRotate)) gizmos.ActiveMode = GizmoMode.Rotate;
    ImGui.SameLine();
    bool isScale = gizmos.ActiveMode == PartEditorGizmos.GizmoMode.Scale;
    if (ImGui.RadioButton("Scale (R)", isScale)) gizmos.ActiveMode = GizmoMode.Scale;

    // Undo/Redo
    if (ImGui.Button("Undo")) controller.Undo();
    ImGui.SameLine();
    if (ImGui.Button("Redo")) controller.Redo();
    ImGui.SameLine();

    // New / Save
    if (ImGui.Button("New Part")) controller.NewPart();
    ImGui.SameLine();
    if (ImGui.Button("Save")) { /* trigger save workflow */ }
}
```

**Part ID input:**
```csharp
void RenderPartIdInput(PartEditorController controller)
{
    ImGui.Text("Part ID:");
    ImGui.SameLine();
    // Use ImInputString for the part ID
    // Bind to controller.CurrentPart.PartId
}
```

**Hierarchy panel** — tree list of all placed SubParts:
```csharp
void RenderHierarchyPanel(PartEditorController controller)
{
    if (!ImGui.CollapsingHeader("SubParts##st_hierarchy", ImGuiTreeNodeFlags.DefaultOpen))
        return;

    for (int i = 0; i < controller.CurrentPart.Placements.Count; i++)
    {
        var p = controller.CurrentPart.Placements[i];
        bool isSelected = controller.SelectedPlacementIndex == i;
        if (ImGui.Selectable($"{p.InstanceId} ({p.SubPartTemplateId})##st_h{i}", isSelected))
            controller.SelectedPlacementIndex = i;
    }
}
```

**Properties panel** — Transform editing for the selected SubPart. Follow the pattern from VehicleEditor.DrawTransformMenu() (lines 2885-3003):
```csharp
void RenderPropertiesPanel(PartEditorController controller)
{
    if (!ImGui.CollapsingHeader("Properties##st_props", ImGuiTreeNodeFlags.DefaultOpen))
        return;

    var placement = controller.SelectedPlacement;
    if (placement == null) { ImGui.TextDisabled("No SubPart selected."); return; }

    // Instance ID (editable)
    ImGui.Text("Instance ID:");
    // ... InputText bound to placement.InstanceId

    // Position (3 × InputDouble)
    if (ImGui.TreeNode("Position (XYZ)##st_pos"))
    {
        double x = placement.Position.X, y = placement.Position.Y, z = placement.Position.Z;
        bool changed = false;
        ImGui.PushItemWidth(-1);
        changed |= ImGui.InputDouble("##posX", ref x);
        changed |= ImGui.InputDouble("##posY", ref y);
        changed |= ImGui.InputDouble("##posZ", ref z);
        ImGui.PopItemWidth();
        if (changed) placement.Position = new double3(x, y, z);
        ImGui.TreePop();
    }

    // Rotation (3 × InputDouble in DEGREES, converted to/from radians)
    if (ImGui.TreeNode("Rotation (XYZ degrees)##st_rot"))
    {
        // Convert quat to Euler degrees for display
        double3 eulerRad = placement.Rotation.NormalizedOrIdentity().ToXyzRadians();
        double3 eulerDeg = eulerRad * (180.0 / Math.PI);
        bool changed = false;
        ImGui.PushItemWidth(-1);
        changed |= ImGui.InputDouble("##rotX", ref eulerDeg.X);
        changed |= ImGui.InputDouble("##rotY", ref eulerDeg.Y);
        changed |= ImGui.InputDouble("##rotZ", ref eulerDeg.Z);
        ImGui.PopItemWidth();
        if (changed)
        {
            double3 newRad = eulerDeg * (Math.PI / 180.0);
            placement.Rotation = QuaternionEx.CreateFromXyzRadians(newRad);
        }
        ImGui.TreePop();
    }

    // Scale (3 × InputDouble, with uniform toggle)
    if (ImGui.TreeNode("Scale (XYZ)##st_scale"))
    {
        double x = placement.Scale.X, y = placement.Scale.Y, z = placement.Scale.Z;
        // ... InputDouble for each, with optional "Lock Uniform" checkbox
        ImGui.TreePop();
    }

    // Action buttons
    if (ImGui.Button("Delete##st_del"))
    {
        controller.PushUndo();
        controller.CurrentPart.Placements.RemoveAt(controller.SelectedPlacementIndex);
        controller.SelectedPlacementIndex = -1;
    }
    ImGui.SameLine();
    if (ImGui.Button("Duplicate##st_dup"))
    {
        controller.PushUndo();
        var clone = placement.Clone();
        clone.InstanceId = placement.InstanceId + "_copy";
        clone.Position += new double3(0.5, 0, 0); // offset slightly
        controller.CurrentPart.Placements.Add(clone);
        controller.SelectedPlacementIndex = controller.CurrentPart.Placements.Count - 1;
    }
}
```

**Rotation conversion** — `QuaternionEx.CreateFromXyzRadians(double3)` and `doubleQuat.ToXyzRadians()` are the game's Euler↔Quat converters (from `TransformReference.cs` lines 35 and 39).

**Validation:** `dotnet build` succeeds. The property panel shows and allows editing of selected SubPart transforms.

---

### Phase 5: Save/Load, GameData UI & Runtime Preview

---

#### Task 5.1 — Save workflow

**Goal:** Complete save pipeline from ImGui "Save" button through to XML files on disk.

**Modify: `space-tape.lib/PartEditorUi.cs`** — Add save dialog:
```csharp
void RenderSaveDialog(PartEditorController controller, PartModWriter writer)
{
    // Validation
    if (controller.CurrentPart.Placements.Count == 0)
    {
        ImGui.TextColored(new float4(1, 0.3f, 0.3f, 1), "Part has no SubParts!");
        return;
    }
    if (string.IsNullOrWhiteSpace(controller.CurrentPart.PartId))
    {
        ImGui.TextColored(new float4(1, 0.3f, 0.3f, 1), "Part ID is required!");
        return;
    }

    // File picker (from PartModWriter)
    writer.RenderFilePicker();

    // Save button
    if (ImGui.Button("Save to Disk##st_save"))
    {
        try
        {
            writer.SavePart(controller.CurrentPart);
            _saveMessage = "Saved successfully!";
            _saveMessageColor = new float4(0.3f, 1, 0.3f, 1);
        }
        catch (Exception ex)
        {
            _saveMessage = $"Save failed: {ex.Message}";
            _saveMessageColor = new float4(1, 0.3f, 0.3f, 1);
        }
    }
    if (_saveMessage != null)
        ImGui.TextColored(_saveMessageColor, _saveMessage);
}
```

**Validation:** `dotnet build` succeeds. Saving writes correctly formatted XML to the mod directory.

---

#### Task 5.2 — GameData editor UI

**Goal:** ImGui panel for editing PartGameData fields (display name, mass, editor tags, etc.).

**Modify: `space-tape.lib/PartEditorUi.cs`** — Add GameData panel:
```csharp
void RenderGameDataPanel(PartEditorController controller)
{
    if (!ImGui.CollapsingHeader("Game Data##st_gd"))
        return;

    var gd = controller.CurrentPart.GameData;

    // Display Name
    ImGui.Text("Display Name:");
    // InputText bound to gd.DisplayName

    // Editor Tags — combo of known tags + ability to add
    // Known EditorTag values from the game (via VehicleEditor.RegisterTag):
    // "Command", "Structural", "Cargo", "Propulsion", "Aero", "Electrical",
    // "Thermal", "Science", "Coupling", "Ground", "Payload"
    ImGui.Text("Editor Tags:");
    for (int i = 0; i < gd.EditorTags.Count; i++)
    {
        ImGui.BulletText(gd.EditorTags[i]);
        ImGui.SameLine();
        if (ImGui.SmallButton($"X##st_tag{i}")) { gd.EditorTags.RemoveAt(i); i--; }
    }
    // Combo to add new tag from known list

    // Mass
    ImGui.Text("Mass (CustomMass):");
    double mass = gd.CustomMass;
    if (ImGui.InputDouble("##st_mass", ref mass)) gd.CustomMass = mass;
    double dryRatio = gd.CustomMassDryRatio;
    if (ImGui.InputDouble("Dry Mass Ratio##st_dryratio", ref dryRatio)) gd.CustomMassDryRatio = dryRatio;

    // Optional advanced fields (collapsible)
    if (ImGui.TreeNode("Advanced##st_gd_adv"))
    {
        // Battery capacity, generator output, etc.
        ImGui.TreePop();
    }
}
```

**Validation:** `dotnet build` succeeds. GameData panel renders and edits are reflected in save output.

---

#### Task 5.3 — Load/edit existing Parts

**Goal:** Load previously saved Part XML files from the mod directory back into the editor for further editing.

**Modify: `space-tape.lib/PartModWriter.cs`** — Add load functionality:
```csharp
public List<(string partId, string fileName)> ListSavedParts()
{
    var results = new List<(string, string)>();
    if (!Directory.Exists(_assetsDir)) return results;

    foreach (var file in Directory.GetFiles(_assetsDir, "*.xml"))
    {
        var doc = XDocument.Load(file);
        foreach (var partEl in doc.Root.Elements("Part"))
        {
            string? id = partEl.Attribute("Id")?.Value;
            if (id != null) results.Add((id, Path.GetFileName(file)));
        }
    }
    return results;
}

public EditingPart LoadPart(string partId, string fileName)
{
    // Parse Assets XML
    var assetsDoc = XDocument.Load(Path.Combine(_assetsDir, fileName));
    var partEl = assetsDoc.Root.Elements("Part").FirstOrDefault(e => e.Attribute("Id")?.Value == partId);
    // Deserialize SubPart instances from XML into SubPartPlacement objects
    // For each <SubPart> child:
    //   InstanceId = Id attribute
    //   SubPartTemplateId = InstanceOf attribute
    //   Position/Rotation/Scale from <Transform> child

    // Parse GameData XML if exists
    var gdPath = Path.Combine(_gameDataDir, fileName);
    if (File.Exists(gdPath)) { /* parse PartGameDataState from <PartGameData> element */ }

    return editingPart;
}
```

**XML parsing for SubPartPlacement:**
```csharp
foreach (var spEl in partEl.Elements("SubPart"))
{
    var placement = new SubPartPlacement
    {
        InstanceId = spEl.Attribute("Id")?.Value ?? "",
        SubPartTemplateId = spEl.Attribute("InstanceOf")?.Value ?? "",
        Position = double3.Zero,
        Rotation = doubleQuat.Identity,
        Scale = double3.One
    };

    var transformEl = spEl.Element("Transform");
    if (transformEl != null)
    {
        var posEl = transformEl.Element("Position");
        if (posEl != null)
        {
            placement.Position = new double3(
                double.Parse(posEl.Attribute("X")?.Value ?? "0"),
                double.Parse(posEl.Attribute("Y")?.Value ?? "0"),
                double.Parse(posEl.Attribute("Z")?.Value ?? "0"));
        }
        var rotEl = transformEl.Element("Rotation");
        if (rotEl != null)
        {
            double3 eulerRad = new double3(
                double.Parse(rotEl.Attribute("X")?.Value ?? "0"),
                double.Parse(rotEl.Attribute("Y")?.Value ?? "0"),
                double.Parse(rotEl.Attribute("Z")?.Value ?? "0"));
            placement.Rotation = QuaternionEx.CreateFromXyzRadians(eulerRad);
        }
        var scaleEl = transformEl.Element("Scale");
        if (scaleEl != null)
        {
            placement.Scale = new double3(
                double.Parse(scaleEl.Attribute("X")?.Value ?? "1"),
                double.Parse(scaleEl.Attribute("Y")?.Value ?? "1"),
                double.Parse(scaleEl.Attribute("Z")?.Value ?? "1"));
        }
    }
    editingPart.Placements.Add(placement);
}
```

**Add a "Load Part" UI** in the editor window — combo of saved parts, "Load" button that calls `LoadPart()` and replaces `controller.CurrentPart`.

**Validation:** `dotnet build` succeeds. Parts can be saved and reloaded for editing.

---

#### Task 5.4 — Hot-reload experimental spike (human-in-the-loop)

**Goal:** Attempt to register a newly saved PartTemplate into ModLibrary at runtime so the Part appears in the vehicle editor without restarting. This is an EXPERIMENTAL SPIKE — implement minimal code, add a test button, and STOP for the user to verify.

**File to create: `space-tape.lib/HotReloadSpike.cs`**

**Runtime registration approach** — Based on `PartTemplate.OnDataLoad()` (decomp/ksa/KSA/PartTemplate.cs line 166-169):
```csharp
// The game registers parts during data load:
// if (base.IsReferenceable && !_isGameData) { ModLibrary.Register(this); }
// ModLibrary.Register returns bool (false if ID already exists).

public static class HotReloadSpike
{
    public static (bool success, string message) TryRegisterPart(EditingPart editingPart)
    {
        try
        {
            // Create a PartTemplate programmatically
            // This is the tricky part — PartTemplate normally comes from XML deserialization
            // We need to create one and populate its fields

            // Option 1: Create PartTemplate via reflection, set fields manually
            var template = new PartTemplate();
            // Set Id via SerializedId base class (reflection)
            typeof(SerializedId).GetProperty("Id")?.SetValue(template, editingPart.PartId);

            // Add SubPartInstances
            foreach (var placement in editingPart.Placements)
            {
                var instance = new PartInstance();
                typeof(SerializedId).GetProperty("Id")?.SetValue(instance, placement.InstanceId);
                instance.InstanceOf = placement.SubPartTemplateId;
                instance.Transform = new TransformReference
                {
                    PositionValue = placement.Position,
                    RotationValue = placement.Rotation,
                    ScaleValue = placement.Scale
                };
                template.SubPartInstances.Add(instance);
            }

            // Try to register
            bool registered = ModLibrary.Register(template);

            if (registered)
                return (true, $"Registered '{editingPart.PartId}' into ModLibrary successfully.");
            else
                return (false, $"Registration failed — ID '{editingPart.PartId}' may already exist.");
        }
        catch (Exception ex)
        {
            return (false, $"Hot-reload exception: {ex.Message}");
        }
    }

    // Verify registration:
    public static bool VerifyRegistration(string partId)
    {
        try
        {
            var template = ModLibrary.Get<PartTemplate>(partId);
            return template != null;
        }
        catch { return false; }
    }
}
```

**Add test button to editor UI:**
```csharp
if (ImGui.Button("Test Hot-Reload##st_hotreload"))
{
    var (success, message) = HotReloadSpike.TryRegisterPart(controller.CurrentPart);
    _hotReloadMessage = message;
    _hotReloadSuccess = success;

    if (success)
    {
        bool verified = HotReloadSpike.VerifyRegistration(controller.CurrentPart.PartId);
        _hotReloadMessage += verified ? " (Verified in ModLibrary)" : " (NOT found in ModLibrary after registration!)";
    }
}
if (_hotReloadMessage != null)
    ImGui.TextWrapped(_hotReloadMessage);
```

**⚠️ STOP HERE** — This task produces the spike code with a test button. The user must:
1. Build and load the mod in-game
2. Create a Part, save it, click "Test Hot-Reload"
3. Check if the Part appears in the vehicle editor's part catalog
4. Report back whether hot-reload works

**Do NOT implement full hot-reload integration without user confirmation.**

**Validation:** `dotnet build` succeeds. Test button appears in UI.

---

### Phase 6: Polish & Quality of Life

---

#### Task 6.1 — Keyboard shortcuts

**Goal:** Add keyboard shortcuts for common operations.

**Modify: `space-tape.lib/PartEditorInteraction.cs`** or **`PartEditorUi.cs`**

Use `ImGui.IsKeyPressed(ImGuiKey.X)` and `ImGui.IsKeyDown(ImGuiKey.LeftCtrl)`:
```csharp
void HandleKeyboardShortcuts(PartEditorController controller, PartEditorGizmos gizmos, PartModWriter writer)
{
    if (!_editorWindowOpen) return;

    bool ctrl = ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl);

    if (ctrl && ImGui.IsKeyPressed(ImGuiKey.S)) { /* save */ }
    if (ctrl && ImGui.IsKeyPressed(ImGuiKey.Z)) controller.Undo();
    if (ctrl && ImGui.IsKeyPressed(ImGuiKey.Y)) controller.Redo();
    if (ImGui.IsKeyPressed(ImGuiKey.W)) gizmos.ActiveMode = GizmoMode.Translate;
    if (ImGui.IsKeyPressed(ImGuiKey.E)) gizmos.ActiveMode = GizmoMode.Rotate;
    if (ImGui.IsKeyPressed(ImGuiKey.R)) gizmos.ActiveMode = GizmoMode.Scale;
    if (ImGui.IsKeyPressed(ImGuiKey.Delete)) { /* delete selected */ }
    if (ctrl && ImGui.IsKeyPressed(ImGuiKey.D)) { /* duplicate selected */ }
    if (ImGui.IsKeyPressed(ImGuiKey.F)) { /* focus camera on selected */ }
}
```

**Note:** The HotkeyGuard from ksa-abstractions blocks game hotkeys when typing in ImGui text inputs. This is already applied by grant's Patcher.cs. However, when NOT in a text input, these shortcuts may conflict with game hotkeys. Consider only processing shortcuts when the editor window is focused: `ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows)`.

**Validation:** `dotnet build` succeeds.

---

#### Task 6.2 — Copy/paste XML to clipboard

**Goal:** "Copy Part XML" and "Copy GameData XML" buttons that copy the serialized XML to the system clipboard.

**Modify: `space-tape.lib/PartEditorUi.cs`**

```csharp
if (ImGui.Button("Copy Part XML##st_cpxml"))
{
    var partXml = PartXmlSerializer.SerializePart(controller.CurrentPart);
    var doc = PartXmlSerializer.WrapInAssets(new[] { partXml });
    ImGui.SetClipboardText(doc.ToString());
}
ImGui.SameLine();
if (ImGui.Button("Copy GameData XML##st_cpgd"))
{
    var gdXml = GameDataXmlSerializer.SerializeGameData(
        controller.CurrentPart.PartId, controller.CurrentPart.GameData);
    var doc = GameDataXmlSerializer.WrapInGameData(new[] { gdXml });
    ImGui.SetClipboardText(doc.ToString());
}
```

`ImGui.SetClipboardText()` is available in the KSA ImGui bindings.

**Validation:** `dotnet build` succeeds.

---

#### Task 6.3 — Visual aids

**Goal:** Add origin marker, optional grid, bounding box for selected SubPart.

**Modify: `space-tape.lib/PartEditorScene.cs`**

The origin marker gizmo was already described in Task 3.1. Additional visual aids:

**Bounding box** — Use a "Box" GenericGizmo for the selected part's bounding box:
```csharp
if (selectedPart != null)
{
    var (bbMin, bbMax) = selectedPart.ComputeBoundingBoxPartAsmb();
    double3 center = (bbMin + bbMax) * 0.5;
    double3 size = bbMax - bbMin;
    // Use the Box gizmo
    var bbSeg = _boxGizmo.GetSegmentDataByViewport(viewport);
    bbSeg[0].Active = true;
    bbSeg[0].PositionEgo = center; // (transformed to ego space)
    bbSeg[0].Scale = size;
    bbSeg[0].Color = new double4(1, 1, 0, 0.3); // yellow, semi-transparent
    bbSeg[0].Body2Cce = selectedPart.Asmb2Ego(asmb2Ecl);
}
```

`Part.ComputeBoundingBoxPartAsmb()` is a public method (decomp/ksa/KSA/Part.cs line 699).

**Validation:** `dotnet build` succeeds.

---

## Technical Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **GenericGizmo access from mod code** — `GenericGizmo.Static` and mesh references may be inaccessible | High | These are public API — `ModLibrary.Get<MeshReference>("ArrowMesh")` and `GenericGizmo.Static.GenericGizmoRenderData` are used in VehicleEditor which is public. If inaccessible, use reflection. |
| **VehicleEditingSpace creation** — Constructor may fail outside game editor mode | Medium | Constructor is public (decomp confirmed). If it fails, create a lightweight `IFollowable` implementation with the same fields. |
| **Part rendering without Vehicle** — `PartModelModule.UpdateRenderData` may expect vehicle context | High | The method signature takes `ref readonly double4x4 matrixVehicleAsmb2Ego` which we provide from `VehicleEditingSpace.GetMatrixAsmb2Ego()`. If auto-rendering doesn't work, manually call `UpdateRenderData()` per Part per frame. Fallback: use ThumbnailPart pipeline from inanimate-carbon-rod. |
| **Camera state corruption** — Switching camera follow target may break game state | Medium | Save full camera state (Following, CameraMode, OrbitView) on enter, restore on exit. |
| **Hot-reload viability** — `ModLibrary.Register()` may not integrate with rendering pipeline | High | Experimental spike with human verification gate. Accept game-restart if spike fails. |
| **SubPart thumbnail VRAM** — Generating thumbnails uses GPU memory | Low | Already solved by inanimate-carbon-rod with configurable sizes. |

---

## File Dependencies

```
space-tape.lib/
  ├─ References: ksa-abstractions.lib (ISubmod, SubmodUI, KsaPaths, ReflectionHelpers)
  ├─ References: inanimate-carbon-rod.lib (SubpartThumbnailGenerator, SubpartThumbnailCache)
  └─ References: KSA game assemblies (Part, PartTemplate, PartInstance, TransformReference,
  │              GenericGizmo, VehicleEditingSpace, ModLibrary, MeshReference, Camera,
  │              Viewport, Program, Universe, CameraMode, OrbitView, PartTree, Ray, etc.)
  │
grant/
  ├─ References: space-tape.lib (ProjectReference in grant.csproj)
  └─ Registers SpaceTapeSubmod in Mod.cs OnFullyLoaded()
```
