# Space Tape

In-game Part editor for KSA. Compose new Parts by placing existing SubParts into a 3D scene with transform controls, define fuel tanks, connectors, and power systems, then save the result as a KSA mod XML Part definition.

## Features

- **Grant submod minimal panel** — two-button flow for `Load SubParts` and `Open/Close Part Editor`
- **Load SubParts modal** — configure `Images per SubPart` + thumbnail image size, run Generate/Re-generate, and monitor progress/error state
- **SubParts floating window** — dedicated browser with thumbnail size, animation delay, filter, and view-subparts toggle
- **Large SubPart viewer** — open a higher-detail floating preview from the SubParts window
- **Load/import 2x2 combo table** — compact filterable selectors for load/import workflows
- **Save modal in toolbar** — save now opens from the top editor toolbar instead of an inline save section
- **3D editing scene** — place SubParts in world space with interactive gizmos (translate/rotate/scale) and origin axis marker
- **Hover highlight** — SubParts highlight when hovered using the game's native highlight shader
- **Click-to-select** — click any SubPart in the 3D viewport to select it for editing
- **Selection visual feedback** — selected SubPart shows the game's native selection shader
- **Quick-flip rotation** — D key rotates +45° around Y-axis, F key rotates +45° around X-axis (cumulative)
- **Plane-locked drag** — P key cycles through pan modes (Normal / YZ / XZ / XY plane), click-and-drag to move SubParts constrained to a plane; respects grid snap when enabled
- **Camera snap views** — six orthographic-style snap buttons (Front, Back, Left, Right, Top, Bottom) instantly orient the camera to standard vantage points
- **Grid plane overlay** — translucent origin-centered reference grids with independent X/Y/Z plane toggles plus configurable size, spacing, regular color, and axis-line color/alpha
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

## Grant Submod Panel

When running inside grant, the Space Tape panel is intentionally minimal:

- `Load SubParts` opens the generation modal
- `Open Part Editor` / `Close Part Editor` toggles the editor lifecycle

All catalog browsing and SubPart placement controls are moved out of the grant panel and into floating editor windows.

## Load SubParts Modal

The Load SubParts modal is the entry point for thumbnail generation:

- Set `Images per SubPart` (camera angle count per thumbnail set)
- Set thumbnail `Image Size` (resolution selector)
- Click `Generate` for first-time generation or `Re-generate` to rebuild from scratch
- Watch live generation progress while work is running
- If generation fails, the modal surfaces the latest error text

Generated thumbnails are then used by the SubParts window for animated previews.

## SubParts Window

SubPart browsing now lives in a dedicated floating window tied to the Part Editor:

- Auto-opens with the editor and closes when the editor closes
- Supports adjustable thumbnail size and animation delay
- Supports text filtering to narrow large SubPart catalogs
- Includes a `View SubParts` toggle and large viewer launch path for detailed inspection

## Part Editor Notes

- Save is now performed from a toolbar button that opens a save modal
- Load/import flows use a compact 2x2 filterable combo table for selecting source category/part
- Import still pulls full supported data (SubParts, Connectors, Tank, Power, Coupling) into the current editing state
- Import skips and logs individual bad SubPart records instead of aborting the whole import
- Connector direction markers use the game's interleaved gizmo mesh path, with a render safety patch active while the editor is open to prevent invalid gizmo mesh data from crashing the game
- Grid lines use KSA's orbit line renderer instead of the shared gizmo line shader, so grid opacity is driven by the configured RGBA alpha without modifying game shader files.
- Visual grid lines are positioned from the part origin outward at `n * spacing` in both directions. If the configured size is not an even multiple of spacing, the drawn edge stops at the outermost generated line so the grid does not leave dangling boundary segments.

## Saving Parts (Modal Flow)

Saving follows a modal flow from the editor toolbar:

1. Click `Save` in the toolbar.
2. Pick or filter the target output mod/file using combo selectors.
3. Confirm save in the modal.
4. Space Tape writes Assets XML + GameData XML and updates mod metadata as needed.
5. Newly saved content is available for hot-reload testing.

## Thumbnail Generation

Space Tape owns SubPart thumbnail generation and caching. Thumbnails are generated on demand from within the Space Tape workflow and reused by the SubPart browser to provide animated previews without requiring a separate thumbnail mod.

Thumbnail generation temporarily reconfigures the rendered viewport camera for off-screen SubPart views, then restores the user's camera and controlled vehicle state quietly. Restores use KSA's `alert: false` follow path so the load flow does not spam on-screen `Following ...` timed alerts while batches are generated.

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
| `CameraSnapController.cs` | Camera snap-to-view state machine and grid plane drawing via OrbitLinePass |
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
| `Thumbnails/ThumbnailCameraState.cs` | Captures/restores camera, viewport, and control state around quiet thumbnail rendering |

## Architecture

```
PartEditorUi (ImGui window)
├── RenderImportSection      → compact 2x2 filterable combo table
├── RenderPartIdSection      → basic part identity fields
├── Toolbar Save             → modal save flow
├── RenderSubPartList        → placed SubParts with transforms
├── GameDataEditorUi
│   ├── RenderTankSection
│   ├── RenderPowerSection
│   ├── RenderConnectorsSection
│   └── RenderCouplingSection

SubPartsWindow (floating)
├── SubPartCatalogUi         → filterable browser + animated thumbnails
├── View mode controls       → grid/list + thumb size + animation delay
└── Large viewer launcher    → SubpartViewerWindow

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
