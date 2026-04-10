# Space Tape

In-game Part editor for KSA. Compose new Parts by placing existing SubParts into a 3D scene with transform controls, define fuel tanks, connectors, and power systems, then save the result as a KSA mod XML Part definition.

## Features

- **SubPart catalog browser** — filterable list with animated thumbnail previews (grid/list modes)
- **Import from existing parts** — load any game part into the editor with full SubPart, Connector, Tank, Battery, Generator, and Coupling data
- **3D editing scene** — place SubParts in world space with interactive gizmos (translate/rotate/scale) and origin axis marker
- **Hover highlight** — SubParts highlight when hovered using the game's native highlight shader
- **Click-to-select** — click any SubPart in the 3D viewport to select it for editing
- **Selection visual feedback** — selected SubPart shows the game's native selection shader
- **Quick-flip rotation** — D key rotates +45° around Y-axis, F key rotates +45° around X-axis (cumulative)
- **Plane-locked drag** — P key cycles through pan modes (Normal / YZ / XZ / XY plane), click-and-drag to move SubParts constrained to a plane; respects grid snap when enabled
- **Camera snap views** — six orthographic-style snap buttons (Front, Back, Left, Right, Top, Bottom) instantly orient the camera to standard vantage points
- **Grid plane overlay** — translucent reference grid drawn in 3D on the plane facing the snapped camera direction, with configurable size and spacing
- **Connector visualization** — 3D gizmo cubes color-coded by flag type (yellow=Internal, cyan=ToSurface, magenta=FromSurface, green=selected)
- **Transform controls** — per-SubPart position, rotation, and scale with grid snap and rotation snap
- **Gizmo sizing** — single slider to uniformly scale all transform gizmos (translate/rotate/scale)
- **Editor lighting** — add helper point lights around the workspace in Box Corners (8 lights) or Sphere (configurable rings and lights per ring) arrangements with adjustable radius, intensity, range, and color
- **Fuel tanks** — Cylindrical or Spherical tank definitions with material, density, wall thickness, and dome height
- **Connectors** — define attachment points with position, rotation, scale, and flag types
- **Coupling** — Decoupler, Docking Port, and EVA Door with connector references
- **Power** — multiple Batteries (kWh), Generators (W), and Power Consumers (W) per part
- **GameData property panel** — ImGui editor with sections for Basic Info, Tank, Power, Connectors, and Coupling
- **Part XML export** — writes Assets XML + GameData XML to `space-tape-parts` mod directory with auto-managed mod.toml
- **Hot-reload spike** — registers newly saved parts at runtime so you can test without a restart

## Hotkeys

| Key | Action | Context |
|-----|--------|----------|
| F11 | Toggle editor window | Global |
| D | Rotate +45° around Y-axis | SubPart selected |
| F | Rotate +45° around X-axis | SubPart selected |
| P | Cycle pan mode (Normal → YZ → XZ → XY → Normal) | Editor active |

## Integration

Space Tape is integrated into the **grant** supermod as `SpaceTapeSubmod`. It appears as a collapsible panel in the Grant Toolbox window alongside other submods. It can also run standalone via `Mod.cs` (F11 toggle).

## Project Structure

### space-tape/ (mod entry)

| File | Purpose |
|------|---------|
| `Mod.cs` | Standalone mod entry — F11 window toggle |
| `Patcher.cs` | Harmony setup with HotkeyGuard |
| `mod.toml` | StarMap mod descriptor |

### space-tape.lib/ (shared library)

| File | Purpose |
|------|---------|
| `SpaceTapeSubmod.cs` | ISubmod entry point for grant integration |
| `EditorLighting.cs` | Manages helper point lights around the editor workspace (Box Corners / Sphere modes) |
| `CameraSnapController.cs` | Camera snap-to-view state machine and grid plane drawing via GizmosRenderer |
| `PartEditorState.cs` | Core state models — EditingPart, SubPartPlacement, PartGameDataState |
| `GameDataModels.cs` | State classes for Tank, Connector, Coupling, Battery, Generator, PowerConsumer |
| `PartEditorUi.cs` | Main ImGui editor window with import, SubPart catalog, and GameData editing |
| `GameDataEditorUi.cs` | ImGui sections for Tank, Power, Connectors, and Coupling editing |
| `PartEditorScene.cs` | 3D editing scene with gizmos, camera, and origin marker |
| `ConnectorGizmo.cs` | 3D gizmo cubes for connector visualization, color-coded by flag type |
| `PartCatalog.cs` | Loads non-SubPart, non-Hidden parts from ModLibrary for import |
| `PartImporter.cs` | Deep-reads a PartTemplate into an EditingPart for the editor |
| `GameDataXmlSerializer.cs` | Serializes PartGameDataState to GameData XML |
| `PartXmlSerializer.cs` | Serializes EditingPart to Assets XML (SubParts + Connector geometry) |
| `PartModWriter.cs` | Reads/writes Part XML files and manages the output mod directory |
| `HotReloadSpike.cs` | Registers edited PartTemplates into ModLibrary at runtime |
| `SubPartCatalog.cs` | Loads SubPart catalog with thumbnail previews |
| `SubPartCatalogUi.cs` | Filterable SubPart browser with grid/list layout modes |

## Architecture

```
PartEditorUi (ImGui window)
├── RenderImportSection      → PartCatalog + PartImporter
├── RenderPartIdSection      → basic part identity fields
├── SubPartCatalogUi         → SubPart browser + placement
├── RenderSubPartList        → placed SubParts with transforms
├── GameDataEditorUi
│   ├── RenderTankSection
│   ├── RenderPowerSection
│   ├── RenderConnectorsSection
│   └── RenderCouplingSection
└── RenderSaveSection        → PartXmlSerializer + GameDataXmlSerializer + PartModWriter

PartEditorScene (3D viewport)
├── GenericGizmo             → translate/rotate/scale for SubParts
├── ConnectorGizmo           → color-coded connector cubes
├── CameraSnapController     → snap views + grid plane overlay
├── PartEditorInteraction    → hover highlight, click-select, gizmo drag, quick-flip, plane drag
└── Origin marker            → axis lines at part origin

HotReloadSpike               → registers saved parts into game at runtime
```

## Building

```bash
dotnet build space-tape/space-tape.csproj
```

Check for compilation errors before continuing with implementation.

## Common Issues

- **Namespace mismatches**: Update everywhere (csproj, Mod.cs, Patcher.cs)
- **Project references**: Add library project reference to main mod
- **Harmony ID conflicts**: Each Harmony instance needs unique ID string
- **ImGui crashes**: Ensure ImGui calls only happen in OnAfterUi

## Notes for Developers

- Keep UI separate from logic (UI in Mod.cs, logic in Lib project)
- Use Console.WriteLine for debugging
- Test Harmony patches carefully—they affect game runtime
- Document your Harmony patches explaining what they do
- Consider performance impact of per-frame operations

## Related Mods

See similar template mods:
- [grant](../grant) - Minimal template without .lib
- [stampy](../stampy) - Another template example
- Other mods for inspiration on complete implementations
