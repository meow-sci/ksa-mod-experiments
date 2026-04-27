# Flexo Library

Shared library for the Flexo robotics mod. Contains the editor, runtime logic, data persistence, and grant submod integration.

## Architecture

```
flexo.lib/
├── FlexoSubmod.cs              # ISubmod — orchestrator for grant integration
├── FlexoPatches.cs             # Harmony patches (editor rendering)
├── Data/
│   ├── FlexoPartType.cs        # Enum: Hinge, Rotor (future)
│   ├── FlexoPartDefinition.cs  # Top-level TOML data model
│   ├── HingeDefinition.cs      # Hinge-specific properties
│   └── FlexoDataManager.cs     # Load/save/list TOML definitions
├── Runtime/
│   ├── FlexoRuntime.cs         # Startup loading, vehicle scanning, animation loop
│   ├── HingeController.cs      # Per-instance rotation state + math
│   └── FlexoRuntimeUi.cs       # ImGui runtime control panel
└── Editor/
    ├── FlexoEditorScene.cs     # VehicleEditingSpace, camera, gizmos
    ├── FlexoEditorInteraction.cs # Hover/select Parts via raycasting
    ├── FlexoEditorState.cs     # Editor state machine
    ├── FlexoEditorUi.cs        # ImGui editor window
    ├── FlexoCameraSnap.cs      # Camera snap views
    └── FlexoEditorLighting.cs  # Editor lighting setup
```

## Key Classes

- **FlexoSubmod**: Implements `ISubmod` for grant integration. Manages runtime and editor lifecycle.
- **FlexoDataManager**: Reads/writes `~/.flexo/flexo_part_*.toml` TOML files using Tomlyn.
- **HingeController**: Manages per-hinge-instance animation. Uses `doubleQuat.CreateFromAxisAngle()` to rotate Parts via `Part.Asmb2ParentAsmb`.
- **FlexoEditorScene**: Isolated 3D editing space using `VehicleEditingSpace`. Loads vehicle Parts for selection.
- **FlexoPatches**: Harmony prefix on `PartModelRenderer.UpdateRenderData` for rendering editor Parts.

## Data Format

Flexo definitions are TOML files stored in `~/Documents/My Games/Kitten Space Agency/.flexo/`:

```toml
[flexo]
part_type = "hinge"
display_name = "Solar Panel Hinge"
created_from_vehicle = "MyRocket"

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

## Adding New Robotic Part Types

1. Add variant to `FlexoPartType` enum
2. Create a definition class (e.g. `RotorDefinition.cs`)
3. Add nullable property to `FlexoPartDefinition`
4. Extend `FlexoDataManager` serialization/deserialization
5. Create a controller class (e.g. `RotorController.cs`)
6. Add UI in `FlexoRuntimeUi` and `FlexoEditorUi`
