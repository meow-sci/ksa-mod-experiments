# Flexo — Robotics Mod for KSA

## Problem Statement

KSA's Part/SubPart system is entirely static — Parts in a vehicle maintain fixed transforms relative to their parent. The user wants to introduce **robotic articulation** (hinges, rotors) that can move Parts at runtime, causing connected Parts to follow via the existing connector/tree hierarchy.

This must be **purely additive** — no changes to game data formats or engine code. The mod manages its own metadata externally (TOML files) and uses Harmony patches + runtime property mutation to achieve articulation.

## Core Design Decision: Part-Level, Not SubPart-Level

Connectors exist at the **Part level**, not the SubPart level. SubParts are visual mesh components within a Part — they share the Part's transform space and cannot rotate independently in a way that affects connections.

Therefore:
- The **editor** loads a **Vehicle** and lets the user pick **Parts** as robotic components
- A "hinge" is defined by selecting Parts within a vehicle that play specific roles (fixed side, moving side, pivot)
- **Runtime rotation** modifies a Part's `Asmb2ParentAsmb` quaternion, causing all child Parts (via `TreeChildren`) and connected Parts to follow automatically through the game's lazy transform chain

## Feasibility Proof

### Runtime Part Rotation Works
**File: `decomp/ksa/KSA/Part.cs` lines 363-378**

`Part.Asmb2ParentAsmb` has a public setter that invalidates all cached matrices:
```csharp
public doubleQuat Asmb2ParentAsmb {
    set {
        _backingField = value;
        _matrixAsmb = double4x4.Identity;           // invalidate
        _positionVehicleAsmb = new double3(NaN...);  // invalidate
        _matrixAsmb2Parent = double4x4.Identity;     // invalidate
        _asmb2VehicleAsmb = new doubleQuat(NaN...);  // invalidate
    }
}
```

Child Part transforms are computed lazily via composition:
```csharp
// Part.cs line 344
MatrixAsmb2VehicleAsmb => IsSubPart
    ? (MatrixAsmb2ParentAsmb * PartParent.MatrixAsmb2VehicleAsmb)
    : MatrixAsmb2ParentAsmb;
```

No explicit propagation is needed — children automatically pick up parent changes on next access.

### Gimbal System as Prior Art
**File: `decomp/ksa/KSA/Gimbal.cs` lines 109-113**

The game's Gimbal already does axis-angle rotation at runtime:
```csharp
state.Gimbal2Asmb = doubleQuat.Concatenate(
    doubleQuat.CreateFromAxisAngle(double3.UnitY, state.AngleY),
    doubleQuat.CreateFromAxisAngle(double3.UnitZ, state.AngleZ));
```

Our hinge implementation follows the same pattern — single-axis rotation via `doubleQuat.CreateFromAxisAngle`.

### Rendering Automatic
**File: `decomp/ksa/KSA/PartModelModule.cs` lines 76-98**

`PartModelRenderer.UpdateRenderData()` is called every frame and reads current Part transforms dynamically. No manual refresh needed after changing `Asmb2ParentAsmb`.

### `RecomputeAllDerivedData` NOT Required for Transforms
**File: `decomp/ksa/KSA/PartTree.cs` lines 208-215**

This method only handles fuel systems, mass properties, and staging — not Part transforms.

---

## Architecture

### Project Structure

```
flexo.lib/                              # Shared library (unscience integration)
├── FlexoSubmod.cs                      # ISubmod implementation — orchestrator
├── FlexoPatches.cs                     # Harmony patch class (PartRenderHelper equivalent)
├── Data/
│   ├── FlexoPartDefinition.cs          # TOML data model for a flexo part
│   ├── HingeDefinition.cs              # Hinge-specific data
│   ├── FlexoPartType.cs                # Enum: Hinge, Rotor (future)
│   └── FlexoDataManager.cs             # Load/save/list TOML files
├── Editor/
│   ├── FlexoEditorScene.cs             # VehicleEditingSpace, camera, lighting, rendering
│   ├── FlexoEditorInteraction.cs       # Hover/select Parts via raycasting
│   ├── FlexoEditorUi.cs               # Main editor ImGui window
│   ├── FlexoEditorState.cs            # Editor state machine (mode, selection, data entry)
│   ├── FlexoCameraSnap.cs             # Reuse CameraSnapController pattern
│   └── FlexoEditorLighting.cs         # Reuse EditorLighting pattern
├── Runtime/
│   ├── FlexoRuntime.cs                 # Startup TOML loading, vehicle scanning, articulation loop
│   ├── HingeController.cs              # Per-hinge-instance runtime state + rotation math
│   └── FlexoRuntimeUi.cs              # Runtime control panel UI (scan button, hinge sliders)
└── flexo.lib.csproj

flexo/                                   # Standalone mod entry point
├── Mod.cs                               # StarMapMod — F11 toggle, minimal
├── Patcher.cs                           # Harmony setup + HotkeyGuard
├── mod.toml                             # StarMap descriptor
└── flexo.csproj
```

### Unscience Integration

Add to `unscience/unscience.csproj`:
```xml
<ProjectReference Include="..\flexo.lib\flexo.lib.csproj" />
```

Add to `unscience/Mod.cs` in `OnFullyLoaded()`:
```csharp
_submods.Add(new FlexoSubmod());
```

Add to `unscience/Patcher.cs` in `Patch()`:
```csharp
FlexoPatches.Apply(_harmony);
```

### FlexoSubmod Pattern

Follows the space-tape pattern:
- `RenderContent()` — lightweight panel: scan button, matched hinge list, quick controls
- `RenderFloatingWindows()` — full editor window (when open)
- `Update(dt)` — runtime articulation loop (animate active hinges every frame)

---

## Data Model

### TOML Schema: Flexo Part Definition

**File location**: `$HOME/Documents/My Games/Kitten Space Agency/.flexo/flexo_part_[part_id].toml`

Example for a hinge:
```toml
[flexo]
part_type = "hinge"
display_name = "Solar Panel Hinge"
created_from_vehicle = "MyRocket"

[hinge]
# Part template IDs (from Part.Template.Id) for the components
fixed_part_template_id = "CoreStructuralA_Prefab_Beam"
moving_part_template_id = "CoreStructuralA_Prefab_Panel"

# Rotation axis in the moving part's local assembly space
# This is the axis around which rotation occurs
axis = [0.0, 1.0, 0.0]  # Y-axis rotation

# Degree limits
min_degrees = 0.0
max_degrees = 180.0
resting_degrees = 0.0

# Motor properties
speed_degrees_per_second = 45.0
```

### C# Data Model

```csharp
// FlexoPartDefinition.cs
public sealed class FlexoPartDefinition
{
    public FlexoPartType PartType { get; set; }
    public string DisplayName { get; set; } = "";
    public string CreatedFromVehicle { get; set; } = "";

    // Type-specific data (only one will be non-null)
    public HingeDefinition? Hinge { get; set; }
    // Future: public RotorDefinition? Rotor { get; set; }
}

// FlexoPartType.cs
public enum FlexoPartType { Hinge, Rotor }

// HingeDefinition.cs
public sealed class HingeDefinition
{
    public string FixedPartTemplateId { get; set; } = "";
    public string MovingPartTemplateId { get; set; } = "";
    public double3 Axis { get; set; } = new double3(0, 1, 0);
    public double MinDegrees { get; set; } = 0;
    public double MaxDegrees { get; set; } = 180;
    public double RestingDegrees { get; set; } = 0;
    public double SpeedDegreesPerSecond { get; set; } = 45;
}
```

### TOML Read/Write Pattern

Follow `garrys-torch.lib/PresetManager.cs` and `unscience/UnscienceState.cs`:

```csharp
// Writing
var root = new TomlTable();
var flexoTable = new TomlTable { ["part_type"] = "hinge", ... };
root["flexo"] = flexoTable;
var hingeTable = new TomlTable { ["fixed_part_template_id"] = def.Hinge.FixedPartTemplateId, ... };
root["hinge"] = hingeTable;
File.WriteAllText(path, Toml.FromModel(root));

// Reading
var toml = File.ReadAllText(path);
if (!Toml.TryToModel<TomlTable>(toml, out var root, out var diag)) { /* error */ }
// Extract values from TomlTable manually (same pattern as UnscienceState.cs)
```

Uses `Tomlyn` NuGet package (already in use across the project). Must add `<PackageReference Include="Tomlyn" Version="0.19.0" />` to `flexo.lib.csproj`.

---

## Editor Design

### Scene Setup

**Reference: `space-tape.lib/PartEditorScene.cs` lines 43-88**

The flexo editor reuses the exact same pattern as space-tape:

1. **VehicleEditingSpace** — create an isolated editing space far from celestial bodies
   ```csharp
   var sunRadius = Universe.CurrentSystem.Sun.Radius;
   var positionEcl = new double3(0, 0, sunRadius * 10);
   _editingSpace = new VehicleEditingSpace(positionEcl, doubleQuat.Identity, scale: 10.0, null);
   ```

2. **Camera setup** — save current follow target, switch to Orbit mode, set follow to editing space
   ```csharp
   _savedFollowTarget = MainViewport.MapCamera.Following;
   MainViewport.MapCamera.SetMode(CameraMode.Orbit);
   MainViewport.MapCamera.SetFollow(_editingSpace, tidalLocking: false, changeControl: true, alert: false);
   ```

3. **Origin gizmo** — 3-axis indicator using `GenericGizmo` with red/green/blue line segments (same as space-tape)

4. **Exit** — restore camera to previous follow target

### Vehicle Loading

Unlike space-tape (which loads individual Parts), the flexo editor loads an **entire Vehicle's part tree** into the editing space.

**Approach:**
1. Get all vehicles via `VehicleProvider.GetAllVehicles()`
2. User picks a vehicle from an ImGui combo box
3. Clone the vehicle's Part tree into the editing space:
   - For each Part in `vehicle.Parts.Parts`, create a new `Part(instanceId, template)` in the editing space
   - Set position/rotation/scale to match the vehicle's current state
   - Call `PartTree.CreateFromNewPartTree(part)` for each Part to populate modules
   - Ensure `MeshViewModule` exists for raycasting (same fix as space-tape)

**Reference: `space-tape.lib/PartEditorScene.cs` lines 222-268** — `CreatePartFromPlacement()` shows how to instantiate Parts from templates with transforms.

### Part Selection / Interaction

**Reference: `space-tape.lib/PartEditorInteraction.cs` lines 85-135**

Reuse the hover/select pattern:
1. Build ray from camera: `camera.ScreenToEgoRay(cursorPos)`
2. Raycast all editor Parts: `part.RayCastEgoSubPart()` and `part.RayCastEgo()`
3. Track closest hit, set `Part.Highlighted = true/false` for hover feedback
4. Click to select: set `Part.Selected = true/false`

**Key difference from space-tape**: No gizmo interaction (no translate/rotate/scale). The flexo editor is read-only for Part transforms — the user only selects Parts, not moves them.

### Part List Panel

ImGui panel showing all Parts in the loaded vehicle as a scrollable list:
```
[Part] CoreStructuralA_Prefab_Beam (instance: beam_1)
[Part] CoreStructuralA_Prefab_Panel (instance: panel_1)
[Part] CorePropulsionB_Prefab_Engine (instance: engine_1)
...
```

Click on a list item to select that Part (synced with 3D viewport selection). Selected item highlighted in the list.

**Reference: Space-tape's hierarchy section in `PartEditorUi.cs`** — uses `ImGui.BeginChild()` with `ImGuiChildFlags.AutoResizeY` and `ImGui.Selectable()` for each item.

### Camera & Lighting

**Camera snaps**: Reuse `CameraSnapController.cs` pattern exactly — 6 snap buttons (Front/Back/Left/Right/Top/Bottom) using OrbitView's Azimuth/Elevation. Grid overlay via `GizmosRenderer.DrawLine()`.

**Lighting**: Reuse `EditorLighting.cs` pattern — BoxCorners or Sphere arrangement of `PointLight` instances created per-frame via `Program.LightSystem.CreateLightInstance()`.

Both of these can be implemented as simple classes with the same API as space-tape's versions. If time allows, factor them into shared utilities in `ksa-abstractions.lib`, but for now just implement them directly in `flexo.lib`.

### Hinge Creator Mode

The editor operates as a state machine:

```
enum FlexoEditorMode {
    Idle,           // Vehicle loaded, browsing
    SelectFixed,    // Waiting for user to click the fixed Part
    SelectMoving,   // Waiting for user to click the moving Part
    ConfigureHinge, // Both parts selected, entering hinge parameters
    ReadyToSave     // All data entered, save button available
}
```

**Workflow:**

1. **Idle** → User clicks "New Hinge" button
2. **SelectFixed** → Status text: "Click the FIXED part (stationary side of hinge)"
   - User clicks a Part in 3D viewport or list → store as `fixedPart`
   - Selected Part gets a distinct color/highlight (e.g., green)
   - Transition to SelectMoving
3. **SelectMoving** → Status text: "Click the MOVING part (the part that rotates)"
   - User clicks a Part → store as `movingPart`
   - Must be different from fixedPart (validate)
   - Selected Part highlighted in different color (e.g., blue)
   - Transition to ConfigureHinge
4. **ConfigureHinge** → ImGui panel shows:
   - Display of selected parts (template IDs and instance IDs)
   - **Axis picker**: combo box with presets (Y-axis, X-axis, Z-axis) + manual float3 input
   - **Degree range**: `DragFloat` for min (-360 to 360) and max (-360 to 360)
   - **Resting position**: `DragFloat` (must be within min/max range)
   - **Motor speed**: `DragFloat` (degrees/second, 1-360 range)
   - **Display name**: `InputText`
   - **Live preview**: Apply rotation to the moving Part in the editor viewport as user drags the degree sliders, so they can see the hinge motion in real-time
   - When all required fields are filled → transition to ReadyToSave
5. **ReadyToSave** → "Save" button enabled
   - On click: write TOML file via `FlexoDataManager`
   - Show success/error status message

### Live Preview in Editor

While in ConfigureHinge mode, the editor applies rotation to the moving Part in real-time:

```csharp
// In editor Update loop, when in ConfigureHinge mode:
double angleRad = previewAngleDegrees * Math.PI / 180.0;
doubleQuat rotation = doubleQuat.CreateFromAxisAngle(hingeAxis, angleRad);
doubleQuat originalRotation = _movingPartOriginalRotation;
_movingEditorPart.Asmb2ParentAsmb = doubleQuat.Concatenate(rotation, originalRotation);
```

This lets the user visually verify the hinge axis and range before saving.

### Save Workflow

When saving, `FlexoDataManager.SaveDefinition(def)` writes the TOML file:

```csharp
public void SaveDefinition(FlexoPartDefinition def)
{
    Directory.CreateDirectory(_flexoDir);  // ~/.flexo/
    string filename = $"flexo_part_{SanitizeId(def.DisplayName)}.toml";
    string path = Path.Combine(_flexoDir, filename);
    // ... serialize to TOML using Tomlyn ...
    File.WriteAllText(path, Toml.FromModel(root));
}
```

**Directory**: `Path.Combine(KsaPaths.UserDataDir, ".flexo")`

---

## Runtime Design

### Startup: Load All Flexo Definitions

In `FlexoRuntime.Initialize()`:

```csharp
public void Initialize()
{
    _flexoDir = Path.Combine(KsaPaths.UserDataDir, ".flexo");
    ReloadDefinitions();
}

public void ReloadDefinitions()
{
    _definitions.Clear();
    if (!Directory.Exists(_flexoDir)) return;

    foreach (var file in Directory.GetFiles(_flexoDir, "flexo_part_*.toml"))
    {
        var def = FlexoDataManager.LoadDefinition(file);
        if (def != null)
            _definitions.Add(def);
    }
    Console.WriteLine($"flexo: Loaded {_definitions.Count} flexo part definition(s)");
}
```

### Vehicle Scanning

When the user presses "Scan" in the runtime panel:

```csharp
public List<HingeInstance> ScanVehicleForHinges(Vehicle vehicle)
{
    var instances = new List<HingeInstance>();
    var allParts = PartHelpers.GetAllParts(vehicle);

    foreach (var def in _definitions.Where(d => d.PartType == FlexoPartType.Hinge))
    {
        var hinge = def.Hinge!;

        // Find Parts matching the template IDs
        var fixedParts = allParts.Where(p => p.Template.Id == hinge.FixedPartTemplateId).ToList();
        var movingParts = allParts.Where(p => p.Template.Id == hinge.MovingPartTemplateId).ToList();

        // For each fixed part, check if it's connected to a matching moving part
        foreach (var fixedPart in fixedParts)
        {
            foreach (var movingPart in movingParts)
            {
                // Check if they're connected (directly via Connections)
                bool connected = fixedPart.Connections.Any(c => c.OtherPart(fixedPart) == movingPart)
                              || movingPart.Connections.Any(c => c.OtherPart(movingPart) == fixedPart);

                // Also check tree parent/child relationship
                bool treeRelated = movingPart.TreeParent == fixedPart
                                || fixedPart.TreeParent == movingPart;

                if (connected || treeRelated)
                {
                    instances.Add(new HingeInstance
                    {
                        Definition = def,
                        FixedPart = fixedPart,
                        MovingPart = movingPart,
                        CurrentDegrees = hinge.RestingDegrees,
                        OriginalRotation = movingPart.Asmb2ParentAsmb,
                    });
                }
            }
        }
    }

    return instances;
}
```

**Key references:**
- `Part.Template` (line 220 of Part.cs) — has `Template.Id` for template ID matching
- `Part.Connections` (line 288 of Part.cs) — list of Connection objects
- `Connection.OtherPart(Part)` (line 161 of Part.cs) — get connected Part
- `Part.TreeParent` / `Part.TreeChildren` (lines 282-284 of Part.cs) — tree hierarchy
- `PartHelpers.GetAllParts(Vehicle)` from `ksa-abstractions.lib/PartHelpers.cs`

### Hinge Rotation at Runtime

**HingeController** manages per-instance animation:

```csharp
public sealed class HingeController
{
    public FlexoPartDefinition Definition { get; }
    public Part FixedPart { get; }
    public Part MovingPart { get; }

    private doubleQuat _originalRotation;
    private double _currentDegrees;
    private double _targetDegrees;
    private bool _isAnimating;

    public double CurrentDegrees => _currentDegrees;
    public double TargetDegrees => _targetDegrees;
    public bool IsAnimating => _isAnimating;

    public void SetTarget(double degrees)
    {
        var hinge = Definition.Hinge!;
        _targetDegrees = Math.Clamp(degrees, hinge.MinDegrees, hinge.MaxDegrees);
        _isAnimating = Math.Abs(_targetDegrees - _currentDegrees) > 0.01;
    }

    public void Open() => SetTarget(Definition.Hinge!.MaxDegrees);
    public void Close() => SetTarget(Definition.Hinge!.MinDegrees);

    /// <summary>Called every frame from FlexoRuntime.Update(dt).</summary>
    public void Update(double dt)
    {
        if (!_isAnimating) return;

        var hinge = Definition.Hinge!;
        double speed = hinge.SpeedDegreesPerSecond;
        double delta = speed * dt;

        if (_currentDegrees < _targetDegrees)
        {
            _currentDegrees = Math.Min(_currentDegrees + delta, _targetDegrees);
        }
        else
        {
            _currentDegrees = Math.Max(_currentDegrees - delta, _targetDegrees);
        }

        if (Math.Abs(_currentDegrees - _targetDegrees) < 0.01)
        {
            _currentDegrees = _targetDegrees;
            _isAnimating = false;
        }

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        var hinge = Definition.Hinge!;
        double angleRad = _currentDegrees * Math.PI / 180.0;
        var axis = new double3(hinge.Axis.x, hinge.Axis.y, hinge.Axis.z);
        var hingeRotation = doubleQuat.CreateFromAxisAngle(axis, angleRad);

        // Concatenate hinge rotation with the part's original rotation
        MovingPart.Asmb2ParentAsmb = doubleQuat.Concatenate(hingeRotation, _originalRotation);
    }
}
```

**Critical implementation note**: Setting `Part.Asmb2ParentAsmb` automatically invalidates all cached transforms. Child Parts recompute their positions lazily via `MatrixAsmb2VehicleAsmb`. The rendering pipeline reads current transforms every frame. No additional refresh/update calls are needed.

### Runtime Control Panel UI

The unscience submod's `RenderContent()` panel shows:

```
[Flexo]
  ┌─────────────────────────────────────────┐
  │ [Scan Vehicle]  [Reload Definitions]    │
  │ Definitions loaded: 3                    │
  │ Active hinges: 2                         │
  │                                          │
  │ ▼ Solar Panel Hinge                      │
  │   Fixed: CoreStructuralA_Prefab_Beam     │
  │   Moving: CoreStructuralA_Prefab_Panel   │
  │   Angle: [====|========] 45.0°           │
  │   Speed: [==|==========] 45 °/s          │
  │   [Open] [Close] [Reset]                 │
  │                                          │
  │ ▼ Cargo Bay Door                         │
  │   Fixed: ...                             │
  │   Moving: ...                             │
  │   Angle: [===========|=] 170.0°          │
  │   Speed: [====|======] 90 °/s            │
  │   [Open] [Close] [Reset]                 │
  └─────────────────────────────────────────┘
```

**ImGui widgets per hinge:**
- `ImGui.CollapsingHeader(def.DisplayName)` — collapsible section
- `ImGui.Text()` — fixed/moving part template IDs
- `ImGui.DragFloat("Angle", ...)` — manual angle control (min/max from definition)
- `ImGui.DragFloat("Speed", ...)` — override speed
- `ImGui.Button("Open")` → `controller.Open()`
- `ImGui.Button("Close")` → `controller.Close()`
- `ImGui.Button("Reset")` → `controller.SetTarget(def.Hinge.RestingDegrees)`

**Reference: imgui-design skill patterns** — use `SubmodUI.BeginContentArea/EndContentArea()`, `ImGui.CollapsingHeader()` with `DefaultOpen`, `ImGui.DragFloat()` for sliders.

---

## Harmony Patches

### PartRenderHelper for Editor

**Reference: `space-tape.lib/PartRenderHelper.cs`**

The flexo editor needs the same Harmony prefix on `PartModelRenderer.UpdateRenderData()` to render Parts in the editor viewport:

```csharp
[HarmonyPatch(typeof(PartModelRenderer), nameof(PartModelRenderer.UpdateRenderData))]
static class PatchPartModelRenderer
{
    static void Prefix(Viewport viewport, uint frameIndex)
    {
        var scene = FlexoEditorScene.Current;
        if (scene == null || !scene.IsActive) return;

        double4x4 matrix = scene.GetMatrixAsmb2Ego(viewport);
        foreach (var part in scene.EditorParts)
        {
            part.Tree.UpdateRenderData(in matrix, isEditedVehicle: false, viewport, frameIndex);
        }
    }
}
```

### FlexoPatches Class

```csharp
public static class FlexoPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.PatchAll(typeof(FlexoPatches).Assembly);
    }

    public static void Remove(Harmony harmony)
    {
        harmony.UnpatchAll(typeof(FlexoPatches).Assembly.GetName().Name);
    }
}
```

Register in `unscience/Patcher.cs`:
```csharp
FlexoPatches.Apply(_harmony);
```

---

## Project Configuration

### flexo.lib.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\Directory.Build.props" />
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.FlexoLib</AssemblyName>
    <RootNamespace>MeowSci.FlexoLib</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Tomlyn" Version="0.19.0" />
  </ItemGroup>
  <!-- KSA DLL references: same as space-tape.lib.csproj -->
</Project>
```

**Copy the `<Reference>` items and `<PackageReference Include="StarMap.API">` from `space-tape.lib/space-tape.lib.csproj`.**

### flexo.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\Directory.Build.props" />
  <PropertyGroup>
    <AssemblyName>MeowSci.Flexo</AssemblyName>
    <RootNamespace>MeowSci.Flexo</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\flexo.lib\flexo.lib.csproj" />
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>
  <!-- Same StarMap.API, Lib.Harmony, KSA DLL refs as space-tape.csproj -->
  <!-- CopyCustomContent target to copy Tomlyn.dll -->
</Project>
```

### mod.toml

```toml
name = "flexo"
description = "Robotics mod — hinges, rotors, and more"
version = "0.1.0"
author = "meow sci"

[StarMap]
EntryAssembly = "MeowSci.Flexo"
```

---

## Implementation Tasks

### Phase 1: Project Scaffolding

#### Task 1.1: Create flexo.lib project

Create the `flexo.lib/` directory with:
- `flexo.lib.csproj` — Library project, references `ksa-abstractions.lib`, `Tomlyn 0.19.0`, StarMap.API, KSA DLLs. Use `space-tape.lib/space-tape.lib.csproj` as the template — copy ALL `<Reference>` items for game DLLs (KSA, Brutal.*, etc.) and the StarMap.API PackageReference. Set `<AssemblyName>MeowSci.FlexoLib</AssemblyName>`, `<RootNamespace>MeowSci.FlexoLib</RootNamespace>`.
- `FlexoSubmod.cs` — Implement `ISubmod` interface (from `MeowSci.KsaAbstractions`):
  - `Name => "Flexo"`
  - `Tooltip => "Robotics — hinges, rotors, and articulated parts."`
  - `Initialize()` — call `FlexoRuntime.Initialize()` to load TOML definitions
  - `Update(dt)` — call `FlexoRuntime.Update(dt)` to animate active hinges
  - `RenderContent()` — wrap in `SubmodUI.BeginContentArea("##flexo_panel")` / `EndContentArea()`, call `FlexoRuntimeUi.Render()`
  - `RenderFloatingWindows()` — call `FlexoEditorUi.RenderEditorWindow()` when editor is open
  - `Dispose()` — cleanup
  - Static `Current` property (same pattern as `SpaceTapeSubmod.Current`)

#### Task 1.2: Create flexo standalone mod project

Create the `flexo/` directory with:
- `flexo.csproj` — References `flexo.lib.csproj` and `ksa-abstractions.lib.csproj`. Copy the full project structure from `space-tape/space-tape.csproj` as template including StarMap.API, Lib.Harmony PackageReferences and all KSA DLL References. Must include `CopyCustomContent` target that copies `Tomlyn.dll`. Set `<AssemblyName>MeowSci.Flexo</AssemblyName>`, `<RootNamespace>MeowSci.Flexo</RootNamespace>`.
- `Mod.cs` — StarMapMod entry point. Follow `space-tape/Mod.cs` pattern. F11 toggles visibility. Creates FlexoSubmod, calls lifecycle methods.
- `Patcher.cs` — Harmony setup. Follow `space-tape/Patcher.cs` pattern. **MUST** include `HotkeyGuard.Patch(_harmony)` in `Patch()` and `HotkeyGuard.Unpatch(_harmony)` in `Unload()`.
- `mod.toml` — StarMap descriptor with `EntryAssembly = "MeowSci.Flexo"`.

#### Task 1.3: Register with unscience

- Add `<ProjectReference Include="..\flexo.lib\flexo.lib.csproj" />` to `unscience/unscience.csproj`
- Add `using MeowSci.FlexoLib;` to `unscience/Mod.cs`
- Add `_submods.Add(new FlexoSubmod());` in the submod instantiation section of `OnFullyLoaded()`
- Add `FlexoPatches.Apply(_harmony);` to `unscience/Patcher.cs` in the `Patch()` method
- Add `FlexoPatches.Remove(_harmony);` to `unscience/Patcher.cs` in the `Unload()` method (if there's an unload section)

#### Task 1.4: Add to solution and repository index

- Add `flexo.lib/flexo.lib.csproj` and `flexo/flexo.csproj` to `ksa-mod-experiments.slnx`
- Update `REPOSITORY_INDEX.md` with flexo and flexo.lib entries
- Create `flexo/README.md` and `flexo.lib/README.md`

#### Task 1.5: Verify build

Run `dotnet build` from solution root. Fix any compilation errors. The initial build should succeed with stub implementations (empty method bodies, `throw new NotImplementedException()` for complex methods is OK at this stage but prefer minimal working stubs).

---

### Phase 2: Data Layer

#### Task 2.1: Implement FlexoPartType enum

Create `flexo.lib/Data/FlexoPartType.cs`:
```csharp
namespace MeowSci.FlexoLib.Data;

public enum FlexoPartType
{
    Hinge,
    // Future: Rotor
}
```

#### Task 2.2: Implement HingeDefinition

Create `flexo.lib/Data/HingeDefinition.cs`:
```csharp
namespace MeowSci.FlexoLib.Data;

public sealed class HingeDefinition
{
    public string FixedPartTemplateId { get; set; } = "";
    public string MovingPartTemplateId { get; set; } = "";

    // Rotation axis in moving part's local space
    public double AxisX { get; set; } = 0;
    public double AxisY { get; set; } = 1;
    public double AxisZ { get; set; } = 0;

    // Degree constraints
    public double MinDegrees { get; set; } = 0;
    public double MaxDegrees { get; set; } = 180;
    public double RestingDegrees { get; set; } = 0;

    // Motor
    public double SpeedDegreesPerSecond { get; set; } = 45;
}
```

#### Task 2.3: Implement FlexoPartDefinition

Create `flexo.lib/Data/FlexoPartDefinition.cs`:
```csharp
namespace MeowSci.FlexoLib.Data;

public sealed class FlexoPartDefinition
{
    public string FileName { get; set; } = "";  // TOML filename (without path)
    public FlexoPartType PartType { get; set; } = FlexoPartType.Hinge;
    public string DisplayName { get; set; } = "";
    public string CreatedFromVehicle { get; set; } = "";

    public HingeDefinition? Hinge { get; set; }
}
```

#### Task 2.4: Implement FlexoDataManager

Create `flexo.lib/Data/FlexoDataManager.cs`:

Responsibilities:
- `FlexoDir` property → `Path.Combine(KsaPaths.UserDataDir, ".flexo")`
- `LoadAll()` → scan `flexo_part_*.toml` files, parse each into `FlexoPartDefinition`
- `LoadDefinition(string filePath)` → parse single TOML file
- `SaveDefinition(FlexoPartDefinition def)` → write TOML file
- `DeleteDefinition(string fileName)` → delete TOML file
- `ListDefinitions()` → return cached list

**TOML parsing**: Use `Tomlyn.Toml.TryToModel<TomlTable>()` for reading (same pattern as `UnscienceState.LoadSubmodState()`). Use `new TomlTable { ... }` + `Toml.FromModel()` for writing (same pattern as `UnscienceState.SaveSubmodState()` and `PresetManager.Save()`).

**TOML schema** (see Data Model section above for full format):
```toml
[flexo]
part_type = "hinge"
display_name = "My Hinge"
created_from_vehicle = "RocketName"

[hinge]
fixed_part_template_id = "TemplateId1"
moving_part_template_id = "TemplateId2"
axis_x = 0.0
axis_y = 1.0
axis_z = 0.0
min_degrees = 0.0
max_degrees = 180.0
resting_degrees = 0.0
speed_degrees_per_second = 45.0
```

Sanitize display name for filename: replace spaces/special chars with underscores, lowercase.

#### Task 2.5: Verify data layer

Write a quick test: instantiate `FlexoDataManager`, call `SaveDefinition()` with a test definition, then `LoadAll()` and verify round-trip. Can be done in `Initialize()` temporarily, then removed. Verify `dotnet build` passes.

---

### Phase 3: Runtime — Vehicle Scanning & Hinge Control

#### Task 3.1: Implement HingeController

Create `flexo.lib/Runtime/HingeController.cs`:

Properties:
- `FlexoPartDefinition Definition` — the flexo definition this instance was created from
- `Part FixedPart` — the fixed side Part reference
- `Part MovingPart` — the moving side Part reference
- `double CurrentDegrees` — current angle
- `double TargetDegrees` — target angle for animation
- `bool IsAnimating` — whether currently moving toward target
- `doubleQuat OriginalRotation` — stored at scan time, the moving Part's initial Asmb2ParentAsmb

Methods:
- `SetTarget(double degrees)` — clamp to min/max, set target
- `Open()` → `SetTarget(MaxDegrees)`
- `Close()` → `SetTarget(MinDegrees)`
- `Reset()` → `SetTarget(RestingDegrees)`
- `SetImmediate(double degrees)` — jump to angle without animation (for manual slider)
- `Update(double dt)` — animate toward target at configured speed
- `ApplyRotation()` — compute `doubleQuat.CreateFromAxisAngle(axis, angleRad)`, concatenate with original rotation, assign to `MovingPart.Asmb2ParentAsmb`

Implementation details:
- Use `doubleQuat.CreateFromAxisAngle(new double3(axisX, axisY, axisZ), angleRadians)` — same approach as Gimbal (see `decomp/ksa/KSA/Gimbal.cs` line 113)
- Concatenate: `MovingPart.Asmb2ParentAsmb = doubleQuat.Concatenate(hingeRotation, OriginalRotation)` — Concatenate applies hingeRotation first, then OriginalRotation
- Convert degrees to radians: `angleRad = degrees * Math.PI / 180.0`

#### Task 3.2: Implement FlexoRuntime

Create `flexo.lib/Runtime/FlexoRuntime.cs`:

Properties:
- `FlexoDataManager DataManager` — manages TOML files
- `List<FlexoPartDefinition> Definitions` — loaded definitions
- `List<HingeController> ActiveHinges` — currently active hinge instances
- `bool HasScanned` — whether a scan has been performed

Methods:
- `Initialize()` — create `FlexoDataManager`, call `LoadAll()`
- `ReloadDefinitions()` — reload TOML files
- `ScanVehicle()` — get controlled vehicle via `VehicleProvider.GetControlledVehicle()`, scan for matching hinge configurations
- `ClearScan()` — remove all active hinges, reset state
- `Update(double dt)` — iterate `ActiveHinges`, call `Update(dt)` on each

**Scanning algorithm** (see Runtime Design → Vehicle Scanning section above):
1. Get all Parts via `PartHelpers.GetAllParts(vehicle)`
2. For each hinge definition, find Parts matching `FixedPartTemplateId` and `MovingPartTemplateId` by checking `part.Template.Id`
3. Check connectivity (via `Part.Connections` list, using `connection.OtherPart(part)` method, or via `Part.TreeParent`/`Part.TreeChildren` relationship)
4. Create `HingeController` for each matched pair
5. Store `MovingPart.Asmb2ParentAsmb` as the `OriginalRotation` at scan time

#### Task 3.3: Implement FlexoRuntimeUi

Create `flexo.lib/Runtime/FlexoRuntimeUi.cs`:

Single `Render(FlexoRuntime runtime)` method that renders in the unscience panel:

1. **Header row**: `[Scan Vehicle]` button + `[Reload Definitions]` button
2. **Status text**: "Definitions loaded: N" + "Active hinges: N"
3. **Per-hinge sections** (only if scanned):
   - `ImGui.CollapsingHeader(def.DisplayName, ImGuiTreeNodeFlags.DefaultOpen)`
   - Inside:
     - `ImGui.Text("Fixed: " + controller.FixedPart.Template.Id)`
     - `ImGui.Text("Moving: " + controller.MovingPart.Template.Id)`
     - `ImGui.DragFloat("Angle", ref angle, 1.0f, minDeg, maxDeg)` — calls `controller.SetImmediate(angle)` on change
     - `ImGui.DragFloat("Speed", ref speed, 1.0f, 1.0f, 360.0f)` — override speed (changes `Definition.Hinge.SpeedDegreesPerSecond`)
     - Row of buttons: `[Open]` `[Close]` `[Reset]`

**ImGui patterns**:
- Use `SubmodUI.BeginContentArea("##flexo_runtime")` / `EndContentArea()`
- Use `ImGui.SameLine()` between buttons
- Use `ImGui.Separator()` between sections
- Use `ImGui.TextDisabled()` for status text

#### Task 3.4: Wire runtime into FlexoSubmod

Update `FlexoSubmod.cs`:
- `Initialize()` → `_runtime.Initialize()`
- `Update(dt)` → `_runtime.Update(dt)`
- `RenderContent()` → `FlexoRuntimeUi.Render(_runtime)`

#### Task 3.5: Verify runtime build and basic functionality

`dotnet build` must pass. Test cycle:
1. No TOML files → "Definitions loaded: 0" displayed
2. Manually create a test TOML in `.flexo/` → reload → definitions appear
3. Scan with a vehicle → should either find matches or show "Active hinges: 0"

---

### Phase 4: Editor — Scene & Interaction

#### Task 4.1: Implement FlexoEditorScene

Create `flexo.lib/Editor/FlexoEditorScene.cs`:

**Reference: `space-tape.lib/PartEditorScene.cs`** — reuse the same patterns.

Properties:
- `static FlexoEditorScene? Current` — for Harmony patch access
- `bool IsActive` — whether editor scene is currently active
- `List<Part> EditorParts` — Parts rendered in editor space
- `VehicleEditingSpace _editingSpace`

Methods:
- `Enter()` — create VehicleEditingSpace, save camera state, switch camera to orbit mode following editing space. Create origin gizmo. Same code as `PartEditorScene.Enter()` (lines 43-88 of `space-tape.lib/PartEditorScene.cs`).
- `Exit()` — restore camera to previous follow target, clear editor parts, set `Current = null`
- `LoadVehicleParts(Vehicle vehicle)` — for each Part in `vehicle.Parts.Parts`, recursively clone into editor space:
  - `var part = new Part(originalPart.Id, originalPart.Template)`
  - `part.PositionParentAsmb = originalPart.PositionParentAsmb`
  - `part.Asmb2ParentAsmb = originalPart.Asmb2ParentAsmb`
  - `part.Scale = originalPart.Scale`
  - `PartTree.CreateFromNewPartTree(part)` — populates Modules for rendering
  - Ensure `MeshViewModule` exists (same fix as space-tape `PartEditorScene.EnsureMeshViewModule()`)
  - Add to `EditorParts`
- `GetMatrixAsmb2Ego(Viewport viewport)` — return the assembly-to-ego matrix for rendering
- `UpdateScene(Viewport viewport)` — called from render patch, updates origin gizmo, lighting

#### Task 4.2: Implement FlexoEditorInteraction

Create `flexo.lib/Editor/FlexoEditorInteraction.cs`:

**Reference: `space-tape.lib/PartEditorInteraction.cs` lines 85-135**

Simplified version of space-tape's interaction (no gizmo dragging, no transform manipulation):

Properties:
- `Part? HighlightedPart` — currently hovered
- `Part? SelectedPart` — currently clicked/selected

Methods:
- `Update(FlexoEditorScene scene)` — per-frame:
  1. Build ray from camera cursor position
  2. Raycast all editor Parts (using `part.RayCastEgoSubPart()` and `part.RayCastEgo()`)
  3. Update `HighlightedPart` (set `Part.Highlighted = true/false`)
  4. On left click while hovering: update `SelectedPart` (set `Part.Selected = true/false`)
  5. Return selected Part for editor state machine consumption

#### Task 4.3: Implement FlexoCameraSnap

Create `flexo.lib/Editor/FlexoCameraSnap.cs`:

**Reference: `space-tape.lib/CameraSnapController.cs`** — same snap modes and grid overlay.

Implement:
- `CameraSnapMode` enum: None, Front, Back, Left, Right, Top, Bottom
- `SnapTo(mode)` — set OrbitView Azimuth/Elevation per mode
- `DrawGrid()` — draw grid lines via `GizmosRenderer.DrawLine()` when snap is active
- Grid properties: Width, Height, Spacing, Color
- UI rendering: 6 snap buttons + grid checkbox + grid controls

#### Task 4.4: Implement FlexoEditorLighting

Create `flexo.lib/Editor/FlexoEditorLighting.cs`:

**Reference: `space-tape.lib/EditorLighting.cs`** — same light arrangement patterns.

Implement:
- `LightArrangement` enum: Off, BoxCorners, Sphere
- `UpdateLights(double4x4 matrixAsmb2Ego)` — create point lights per frame via `Program.LightSystem.CreateLightInstance(PointLight)`
- Properties: Arrangement, Intensity, Range, Color, Radius

#### Task 4.5: Implement FlexoPatches (render helper)

Create `flexo.lib/FlexoPatches.cs`:

```csharp
using HarmonyLib;
using KSA;

namespace MeowSci.FlexoLib;

public static class FlexoPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.PatchAll(typeof(FlexoPatches).Assembly);
    }

    public static void Remove(Harmony harmony)
    {
        harmony.UnpatchAll(typeof(FlexoPatches).Assembly.GetName().Name);
    }
}

[HarmonyPatch(typeof(PartModelRenderer), nameof(PartModelRenderer.UpdateRenderData))]
static class PatchPartModelRendererForFlexo
{
    static void Prefix(Viewport viewport, uint frameIndex)
    {
        var scene = FlexoEditorScene.Current;
        if (scene == null || !scene.IsActive) return;

        double4x4 matrix = scene.GetMatrixAsmb2Ego(viewport);
        foreach (var part in scene.EditorParts)
        {
            part.Tree.UpdateRenderData(in matrix, isEditedVehicle: false, viewport, frameIndex);
        }
    }
}
```

**Note**: This patch must not conflict with space-tape's identical patch. If both are active simultaneously, both will render their own editor parts. This is fine — only one editor should be active at a time, and the `IsActive` guard prevents double-rendering.

#### Task 4.6: Verify editor scene works

`dotnet build` must pass. When the editor is opened:
- VehicleEditingSpace is created
- Camera transitions to orbit mode around editing space
- Parts from selected vehicle are visible in the 3D viewport
- Hover highlights Parts
- Click selects Parts

---

### Phase 5: Editor — Hinge Creator UI

#### Task 5.1: Implement FlexoEditorState

Create `flexo.lib/Editor/FlexoEditorState.cs`:

```csharp
public enum FlexoEditorMode
{
    Idle,           // Vehicle loaded, browsing
    SelectFixed,    // Waiting for fixed part selection
    SelectMoving,   // Waiting for moving part selection
    ConfigureHinge, // Both parts selected, entering parameters
    ReadyToSave     // All required data entered
}
```

Properties:
- `FlexoEditorMode Mode`
- `Vehicle? LoadedVehicle`
- `Part? FixedPart`
- `Part? MovingPart`
- `HingeDefinition WorkingHinge` — the hinge being configured
- `string DisplayName`
- `float PreviewAngle` — for live preview slider
- `string? StatusMessage` — feedback text
- `bool StatusIsError` — red vs green status

Methods:
- `StartNewHinge()` — reset state, set Mode = SelectFixed
- `OnPartSelected(Part part)` — handle selection based on current mode
- `Reset()` — return to Idle, clear all state
- `IsValid()` — check if all required fields are filled

#### Task 5.2: Implement FlexoEditorUi

Create `flexo.lib/Editor/FlexoEditorUi.cs`:

This is a floating ImGui window (rendered from `FlexoSubmod.RenderFloatingWindows()`):

```csharp
ImGui.SetNextWindowSize(new float2(450, 600), ImGuiCond.FirstUseEver);
if (ImGui.Begin("Flexo Editor##flexo_editor", ref _editorOpen))
{
    RenderToolbar();           // Camera snaps, lighting, grid
    RenderVehicleLoader();     // Vehicle combo + Load button
    RenderPartList();          // Scrollable Part hierarchy
    RenderHingeCreator();      // Mode-driven hinge creation workflow
    RenderSaveSection();       // Save button + file name
}
ImGui.End();
```

**Sections:**

1. **Toolbar** — Camera snap buttons (2×3 grid), grid toggle, lighting mode selector
   - Reference: space-tape `PartEditorUi.RenderToolbar()` pattern
   - Use `ImGui.BeginTable("##toolbar", 3)` with checkboxes and buttons

2. **Vehicle Loader**
   - `ImGui.CollapsingHeader("Vehicle", ImGuiTreeNodeFlags.DefaultOpen)`
   - Combo box listing all vehicles (`VehicleProvider.GetAllVehicles()`) by display name
   - "Load" button → loads selected vehicle into editor scene
   - "Close Editor" button → exits scene, returns to game

3. **Part List**
   - `ImGui.CollapsingHeader("Parts", ImGuiTreeNodeFlags.DefaultOpen)`
   - `ImGui.BeginChild("##part_list", new float2(0, 200), ImGuiChildFlags.Border)`
   - For each Part in scene: `ImGui.Selectable($"{part.Template.Id} ({part.Id})", isSelected)`
   - Click to select (syncs with 3D selection)

4. **Hinge Creator**
   - `ImGui.CollapsingHeader("Hinge", ImGuiTreeNodeFlags.DefaultOpen)`
   - Mode-dependent content:
     - **Idle**: "New Hinge" button
     - **SelectFixed**: instruction text + "Cancel" button; fixed part shown when selected
     - **SelectMoving**: instruction text + "Cancel" button; both parts shown when fixed selected
     - **ConfigureHinge**:
       - Selected parts display (template IDs)
       - Axis picker: combo (Y-axis/X-axis/Z-axis/Custom) + optional float3 input
       - `ImGui.DragFloat("Min Degrees", ...)` range -360 to 360
       - `ImGui.DragFloat("Max Degrees", ...)` range -360 to 360
       - `ImGui.DragFloat("Resting Degrees", ...)` clamped to min/max
       - `ImGui.DragFloat("Speed (°/s)", ...)` range 1 to 360
       - `ImGui.DragFloat("Preview", ...)` range min to max — applies live rotation in viewport
       - `ImGui.InputText("Display Name", ...)`
     - **ReadyToSave**: all of above + save section enabled

5. **Save Section**
   - Only visible in ReadyToSave mode
   - `ImGui.Button("Save Flexo Part")` → calls `FlexoDataManager.SaveDefinition()`
   - Status message (green on success, red on error)

**Live preview**: When `PreviewAngle` changes in ConfigureHinge mode, apply rotation to the moving Part in the editor scene (same approach as described in Editor Design → Live Preview section).

#### Task 5.3: Wire editor into FlexoSubmod

Update `FlexoSubmod.cs`:
- Add `FlexoEditorScene`, `FlexoEditorInteraction`, `FlexoEditorUi`, `FlexoEditorState` as owned components
- `RenderFloatingWindows()` → call editor UI render when open
- `Update(dt)` → if editor active, also update interaction, camera, lighting
- Add "Open Editor" button in `RenderContent()` panel

#### Task 5.4: Verify editor hinge workflow

Full test:
1. Open editor → scene appears
2. Load a vehicle → parts rendered
3. Click "New Hinge" → enter selection mode
4. Click fixed part → highlighted, recorded
5. Click moving part → highlighted, recorded
6. Configure hinge parameters → preview shows rotation
7. Save → TOML file created in `.flexo/` directory
8. Close editor → camera restores

---

### Phase 6: Integration Testing & Polish

#### Task 6.1: End-to-end test

1. Open game, open unscience panel
2. Flexo panel shows in unscience with "0 definitions"
3. Open flexo editor, load vehicle, create hinge, save
4. Close editor
5. "Reload Definitions" → shows 1 definition
6. "Scan Vehicle" → finds matching hinge
7. Click "Open" → Part rotates to max angle
8. Click "Close" → Part rotates back
9. Drag slider → manual control
10. Verify connected Parts follow the rotation

#### Task 6.2: Edge case handling

- No vehicle controlled → scan shows "No active vehicle"
- No flexo definitions → "No definitions found. Use the editor to create one."
- Vehicle doesn't have matching parts → "No flexo parts found in current vehicle"
- TOML parse error → log warning, skip file, don't crash
- Editor: selecting the same Part as both fixed and moving → error message, block
- Editor: saving with empty display name → error message, block

#### Task 6.3: Update REPOSITORY_INDEX.md

Add entries for:
- `flexo/` — Standalone robotics mod (Mod.cs, Patcher.cs, mod.toml)
- `flexo.lib/` — Flexo library: editor for designing robotic parts (hinges, rotors), runtime articulation, TOML persistence, unscience submod integration

#### Task 6.4: Create README files

- `flexo/README.md` — User-facing documentation: what flexo does, how to use the editor, how to use runtime controls
- `flexo.lib/README.md` — Developer documentation: architecture, data model, key classes, how to add new robotic part types

---

## Key References

### Decomp Sources (decomp/ksa/KSA/)
| File | Key Content | Lines |
|------|------------|-------|
| `Part.cs` | `Asmb2ParentAsmb` setter (cache invalidation) | 363-378 |
| `Part.cs` | `MatrixAsmb2VehicleAsmb` (lazy composition) | 344 |
| `Part.cs` | `Part.Template` (PartTemplate reference) | 220 |
| `Part.cs` | `Connector` inner class | 32-133 |
| `Part.cs` | `Connection` inner class | 135-210 |
| `Part.cs` | `TreeParent`, `TreeChildren` | 282-284 |
| `Part.cs` | `Connections` list | 288 |
| `Part.cs` | `Highlighted`, `Selected` properties | 429+ |
| `PartTree.cs` | `CreateFromNewPartTree()` | ~line 401 |
| `PartTree.cs` | `UpdateRenderData()` | ~line 401-407 |
| `PartTree.cs` | `RecomputeAllDerivedData()` (NOT for transforms) | 208-215 |
| `Gimbal.cs` | `UpdateState()` — axis-angle rotation | 109-113 |
| `Gimbal.cs` | `doubleQuat.CreateFromAxisAngle` usage | 113 |
| `PartModelModule.cs` | `UpdateRenderData()` (reads transforms each frame) | 76-98 |
| `Vehicle.cs` | `Parts` (PartTree field) | 193 |
| `ModLibrary.cs` | `Register(PartTemplate)` | — |

### Space-Tape Sources (space-tape.lib/)
| File | Reuse Pattern |
|------|--------------|
| `SpaceTapeSubmod.cs` | ISubmod implementation pattern, static Current, lifecycle |
| `PartEditorScene.cs` | VehicleEditingSpace creation, camera setup, Enter/Exit |
| `PartEditorInteraction.cs` | Raycast hover/select, Part.Highlighted/Selected |
| `CameraSnapController.cs` | 6-direction snap via OrbitView, grid overlay |
| `EditorLighting.cs` | BoxCorners/Sphere point light arrangements |
| `PartRenderHelper.cs` | Harmony prefix on PartModelRenderer.UpdateRenderData |
| `PartModWriter.cs` | File I/O pattern, mod.toml management |
| `PartXmlSerializer.cs` | XML serialization if needed |
| `PartEditorUi.cs` | ImGui UI patterns, toolbar, section organization |
| `PartImporter.cs` | Reading PartTemplate data |

### Abstractions (ksa-abstractions.lib/)
| File | Usage |
|------|-------|
| `ISubmod.cs` | Interface for unscience submods |
| `SubmodUI.cs` | `BeginContentArea/EndContentArea` |
| `VehicleProvider.cs` | `GetControlledVehicle()`, `GetAllVehicles()` |
| `PartHelpers.cs` | `GetAllParts(vehicle)`, `GetPartsWhere(vehicle, predicate)` |
| `KsaPaths.cs` | `UserDataDir` for .flexo directory |
| `HotkeyGuard.cs` | Required in Patcher.cs |

### Other Mod References
| File | Pattern |
|------|---------|
| `garrys-torch.lib/PresetManager.cs` | TOML read/write with Tomlyn |
| `unscience/UnscienceState.cs` | TOML read/write, KsaPaths usage |
| `unscience/Mod.cs` | Submod registration, lifecycle orchestration |
| `unscience/Patcher.cs` | Harmony patch consolidation |

---

## Open Questions / Future Work

1. **Physics interaction**: Changing `Asmb2ParentAsmb` at runtime affects visual rendering (confirmed). Whether it also affects aerodynamics/collision is unknown. For v1, visual-only articulation is acceptable — physics integration is a stretch goal.

2. **Rotor support**: The architecture supports multiple `FlexoPartType` values. A rotor would use continuous rotation (no min/max, just speed and direction). The TOML schema has a `[rotor]` section ready to be defined. The `HingeController` pattern can be adapted for `RotorController`.

3. **Multiple hinges per vehicle**: The scanning algorithm already handles multiple matches. The UI renders a collapsible section per hinge.

4. **Undo/redo in editor**: Not planned for v1. Space-tape has a clone-based undo system, but the flexo editor's state is simple enough that "Cancel" + "Start Over" suffices initially.

5. **Hot reload of TOML**: The "Reload Definitions" button in the runtime panel handles this. No file watcher needed.
