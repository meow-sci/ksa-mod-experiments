# overview

- Refactor the mod to be current vehicle agnostic, it should show grid data for all lcd grids across all vehicles regardless of what vehicle is the current
- Add a menu bar to the mod child window with a menu item "Debug" and move the scan for all vehicles for blinky grids functionality here
- Get rid of the per-vehicle scan functionality in favor of a global one, we only need this after loading a save game and we want to detect everything in the whole game
- The mod content (after the menu) should be a collapsible header (open by default) named "Create Blinky Grid (?)" with a tooltip help on the quesitonmark about what this mod does (if we can add the toltip there, im not sure, if you can't, dont do it)
- for the data from columns down through vehicle use a table with 4 columns to do the layout, where the comboboxes (always with filters) are 3x cols and the grid name text input is 3x cols
- the create grid button and total parts dynamic string should also be in the table, the total parts string can be 3x cols as well, and have a `(?)` tooltip explaining that the parts are (cols x rows x 2 because cancel thrust explanation)
- on create fail, show an error status message on a new line
- have a blank line
- have a separator with text that is dynamic with counts of # vehicles and total # grid we have
- checkmark (off by default) for the render engine meshes option with a `(?)` tooltip explaining there's a perf boost by not rendering the expensive engine part meshes
- a collapsible header per blinky grid, the title is dynamic based on the [grid name] "on" [vehicle name]
- under each collapsible header is a 4 col table with the shown data, the status message can be 3x cols
- the 5 function buttons can be a regular row and buttons
- destroy can be a regular row and button (color its border #ff0000 / red)




# plan

## resolved questions

1. **Scroll UI**: ✅ Remove entirely — was a built-in hack. RPC scroll still works.
2. **Rescan Grid button**: ✅ Remove — only the global scan in Debug menu remains.
3. **Scan modes**: ✅ Single global scan across ALL vehicles, discovering N grids per vehicle.
4. **Grid header close button**: ~~CollapsingHeader close button~~ — removed, just a regular CollapsingHeader. Destroy button in the content area is sufficient.
5. **Part Scale → Engine Scale**: ✅ Confirmed rename.
6. **Position defaults**: ✅ Change to `(0, 0, 0)`.

---

## files changed

| file | change type | description |
|------|------------|-------------|
| `blinky/Mod.cs` | modify | Add `ImGuiWindowFlags.MenuBar` to `ImGui.Begin`, update window title |
| `blinky.lib/BlinkySubmod.cs` | **major rewrite** | New UI layout, vehicle-agnostic grid display, menu bar, table layouts, global scan |
| `blinky.lib/BlinkyGridManager.cs` | modify | Add `ScanAllVehicles()` helper that iterates all vehicles and auto-discovers grids |
| `blinky/README.md` | modify | Update docs to reflect new UI and behavior |
| `REPOSITORY_INDEX.md` | modify | Update blinky entry if feature description changes |

---

## phase 1 — behavioral changes (BlinkySubmod + BlinkyGridManager)

### 1.1 vehicle-agnostic grid display
- **Current**: `RenderContent()` calls `VehicleProvider.GetControlledVehicle()`, shows "No controlled vehicle" guard, and only iterates grids for that vehicle via `BlinkyGridManager.GetAllForVehicle(vehicleId)`
- **New**: Iterate ALL grids from `BlinkyGridManager.Grids` (across all vehicles). No dependency on controlled vehicle for displaying existing grids. Group/display per grid with vehicle name in the header.

### 1.2 vehicle combobox for grid creation
- **Current**: Build/Scan always targets `VehicleProvider.GetControlledVehicle()`
- **New**: Add a vehicle selection combobox (with filter via `ImGuiTextFilter`) for the "Create" section. The selected vehicle is the target for `DoBuildGrid()`. Populate from `VehicleProvider.GetAllVehicles()`.
- **New state fields**: `_vehicleFilter` (ImGuiTextFilter), `_selectedVehicleIndex` (int), cached vehicle list (refreshed each frame or on demand)

### 1.3 global scan (all vehicles)
- **Current**: `DoScanAllGrids(Vehicle vehicle)` scans one vehicle
- **New**: Add `BlinkyGridManager.ScanAllVehicles()` that iterates `VehicleProvider.GetAllVehicles()`, calls `PixelGrid.ScanAllFromVehicle(vehicle)` on each, and registers all discovered grids. This replaces both `DoScanGrid` and the old `DoScanAllGrids`.
- Remove `DoScanGrid()` (single named grid scan) from submod
- Remove `DoScanAllGrids()` (single vehicle scan) from submod
- Add `DoGlobalScan()` method that calls the new `BlinkyGridManager.ScanAllVehicles()`

### 1.4 remove per-grid scroll UI
- Remove scroll Start/Stop button, speed slider, and scroll status text from `RenderGridControls()`
- Scroll functionality remains in `BlinkyGridManager` and `ScrollAnimation` for RPC use
- Remove `ScrollSpeed` field from `GridUiState`

### 1.5 remove per-grid rescan button
- Remove "Rescan Grid" button from `RenderGridControls()`

---

## phase 2 — UI layout changes (BlinkySubmod rendering)

### 2.1 menu bar
- In `RenderContent()`, add `ImGui.BeginMenuBar()` / `ImGui.EndMenuBar()` block
- Add "Debug" menu via `ImGui.BeginMenu("Debug")`
- Add "Scan for blinky grids" menu item that calls `DoGlobalScan()`
- **Requires**: `Mod.cs` to pass `ImGuiWindowFlags.MenuBar` to `ImGui.Begin()`

### 2.2 "Create Blinky Grid (?)" collapsible header
- Replace existing "Grid Configuration" header with `ImGui.CollapsingHeader("Create Blinky Grid (?)", ImGuiTreeNodeFlags.DefaultOpen)`
- Add tooltip on the "(?)": use `ImGui.SameLine` + `ImGui.TextDisabled("(?)")` + `ImGui.SetItemTooltip(...)` explaining what the mod does
  - Note: CollapsingHeader doesn't support inline tooltip text natively. Alternative: render the `(?)` as a separate `ImGui.TextDisabled` after the header text using SameLine, but this can't go inside the header label. Instead, inside the collapsible body, add an info line at the top. OR: just put the `?` in the label text `"Create Blinky Grid (?)"` and it won't have a hover tooltip since it's part of the header text. A separate `(?)` with tooltip can follow the header on the same line if possible.

### 2.3 create form — 4-column table layout

Replace the current create section layout with a 4-column table:

```
Table "##blinky_create_config" — 4 columns
  Col 0: label (fixed width ~100px)
  Col 1: widget (stretch)
  Col 2: label (fixed width ~100px)
  Col 3: widget (stretch)
```

**Row layout:**

| Row | Col 0 | Col 1 | Col 2 | Col 3 |
|-----|-------|-------|-------|-------|
| 1 | "Columns" | DragInt | "Rows" | DragInt |
| 2 | "Spacing (m)" | DragFloat | "Engine Scale" | DragFloat (renamed from "Part scale") |
| 3 | "Position" | DragFloat X | DragFloat Y | DragFloat Z |
| 4 | (radio) Flat (x) | | Cylinder ( ) | |
| 5 | "Engine" | [filtered combo — 3 cols wide] | — | — |
| 6 | "Grid Name" | [text input — 3 cols wide] | — | — |
| 7 | "Vehicle" | [filtered vehicle combo — 3 cols wide] | — | — |
| 8 | [Create Grid btn] | "Total parts: N (?)" — tooltip explaining cols×rows×2 thrust pairs | — | — |

For "3-col wide" items (Engine, Grid Name, Vehicle), render the label in col 0 and the widget in col 1 with `SetNextItemWidth(-1)`. Cols 2-3 will be empty for that row. If the visual result isn't wide enough, consider using a mixed table/non-table approach (table for paired rows, regular layout for full-width combos).

### 2.4 status message for create errors
- After the table, if `_createMessage` is non-empty and is an error, display colored error text
- Only display on create failure (not success, since success clears naturally)

### 2.5 blank line + separator with dynamic counts
- `ImGui.Spacing()` for visual gap
- `ImGui.SeparatorText($"blinky grids ({vehicleCount} vehicle(s), {gridCount} grid(s))")` with counts from `BlinkyGridManager.Grids`

### 2.6 render engine meshes checkbox
- `ImGui.Checkbox("Render engine meshes", ref renderEngines)` — off by default (already the case: `BlinkyPatchState.RenderPixelParts = false`)
- After checkbox, add `ImGui.SameLine(); ImGui.TextDisabled("(?)"); ImGui.SetItemTooltip("...")` explaining the performance benefit of not rendering expensive engine part meshes

### 2.7 per-grid collapsible headers
- Iterate ALL entries from `BlinkyGridManager.Grids`
- Each grid gets a regular `ImGui.CollapsingHeader` (no close button)
  - Label: `$"{gridName} on {vehicleName}##grid_{vehicleId}_{gridName}"`
- **Under each header**: 4-column table showing:

| Row | Col 0 | Col 1 | Col 2 | Col 3 |
|-----|-------|-------|-------|-------|
| 1 | "Name" | {gridName} | | |
| 2 | "Layout" | {Flat/Cylinder} | "Size" | {rows} rows by {cols} cols |
| 3 | "Status" | {status message — 3 cols} | — | — |

### 2.8 pattern buttons (5 function buttons)
- Single row after the table: `Off`, `All`, `Rows`, `Cols`, `Checkers`
  - Off → `BlinkyGridManager.TurnOff(vehicleId, gridName)`
  - All → `BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.AllOn)`
  - Rows → `BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.AlternatingRows)`
  - Cols → `BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.AlternatingCols)`
  - Checkers → `BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.Checkerboard)`
- Use `ImGui.SameLine()` between buttons

### 2.9 destroy button (red border)
- Separate row: `ImGui.Destroy` button with red border
- Use `ImGui.PushStyleColor(ImGuiCol.Border, new float4(1f, 0f, 0f, 1f))` / `PopStyleColor` for red border
- Keep the existing 2-step deferred destroy logic (turn off → wait 2s → remove parts)

---

## phase 3 — Mod.cs changes

### 3.1 window flags
- Change `ImGui.Begin("blinky", ref _windowVisible)` to `ImGui.Begin("blinky", ref _windowVisible, ImGuiWindowFlags.MenuBar)`
- Update window title: remove the "— Dynamic LCD Grid" subtitle (mockup just shows "blinky")

---

## phase 4 — state field changes summary

### new fields in BlinkySubmod
```csharp
private ImGuiTextFilter _vehicleFilter = new ImGuiTextFilter();
private int _selectedVehicleIndex = -1;
```

### removed fields from BlinkySubmod
- `ScrollSpeed` in `GridUiState` — scroll UI removed

### removed methods from BlinkySubmod
- `DoScanGrid()` — replaced by global scan
- `DoScanAllGrids()` — replaced by global scan

### new methods in BlinkySubmod
- `DoGlobalScan()` — scan all vehicles for all grids
- `RenderMenuBar()` — menu bar rendering
- `RenderCreateSection()` — replaces `RenderGridConfiguration()`
- `RenderGridSection(GridState gs)` — replaces `RenderGridControls()`

### new methods in BlinkyGridManager
- `public static (int discovered, List<string> names) ScanAllVehicles()` — iterates all vehicles, discovers all grids, registers them

---

## phase 5 — documentation updates

### blinky/README.md
- Update "Controls" section: menu bar with Debug menu
- Update "Window Sections" table
- Remove references to per-vehicle scan
- Note that grid display is now vehicle-agnostic

### REPOSITORY_INDEX.md
- Update blinky entry to reflect vehicle-agnostic UI and global scan

---

## execution order

1. Add `ScanAllVehicles()` to `BlinkyGridManager.cs`
2. Rewrite `BlinkySubmod.cs` (new state fields, remove old scan methods, new render methods)
3. Update `Mod.cs` (window flags + title)
4. `dotnet build` to verify compilation
5. Update `README.md` and `REPOSITORY_INDEX.md`
6. Final `dotnet build` to confirm

