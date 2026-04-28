# Unscience Supermod — Unification Plan

## Overview

Unify 11 standalone mods into a single **unscience** supermod that presents one top-level ImGui window. Each submod's content appears under a collapsible header inside that window. A context menu (gear icon, top-right) controls which submods are visible. All business logic stays in (or is lifted to) the corresponding `.lib` projects; the standalone mod projects are refactored to become thin ImGui wrappers reusing that same `.lib` logic.

## Mods Being Unified Into Unscience

| Submod | .lib | Has Harmony Patches | Has `OnBeforeUi` Logic |
|--------|------|--------------------|-----------------------|
| average-twr | average-twr.lib | No (empty) | Yes — TWR sample collection at 100Hz |
| blinky | blinky.lib | Yes — render-skip patches (3 prefix patches on `PartModelModule`, `PartModelDynamicModule`, `PartModelGlassModule`) | No (ticks in `OnAfterUi`) |
| eternal-flame | eternal-flame.lib | No (empty) | Yes — `FuelManager.Update(dt)` |
| garrys-torch | garrys-torch.lib | No (empty) | No (welds update in `OnAfterUi`) |
| glass | glass.lib | Yes — `Camera.ChangeFieldOfView` prefix, `Camera.UpdateProjection` prefix | No |
| i-feel-seen | i-feel-seen.lib | Yes — `Vehicle.GetWorldMatrix` prefix, `Vehicle.UpdateRenderData` prefix (require `VehicleTracker` instance) | No |
| kiwis-marbles | kiwis-marbles.lib | No (empty) | No (welds update in `OnAfterUi`) |
| skittles | skittles.lib | Yes — `GameSettings.OnKeyAll` prefix (blocks hotkeys when text input focused) | No |
| unladen-swallow | unladen-swallow.lib | No (empty) | Yes — `GameThread.DrainOnGameThread()` |
| zippo | zippo.lib | No (empty) | No |
| ksa-abstractions.lib | (self) | N/A | N/A — shared utilities |

## Architecture

### Unscience Window Layout

```
┌─────────────────────────────────────────────────────────────┐
│  unscience Mod                                          [⚙] [X] │
│─────────────────────────────────────────────────────────────│
│  ▼ Average TWR                                              │
│    [TWR content: samples, stats, start/pause/reset]         │
│  ▼ Blinky — Dynamic LCD Grid                                │
│    [Grid config, build control, patterns, scroll, debug]    │
│  ▼ Eternal Flame — Infinite Fuel                            │
│    [Vehicle selector, refill slider, monitored table]       │
│  ▼ Garry's Torch                                             │
│    [Create weld, active welds list]                         │
│  ▼ Glass — Camera Lens                                      │
│    [Presets, manual FOV, reset]                             │
│  ▼ I Feel Seen                                              │
│    [Tracked vehicles, add vehicle]                          │
│  ▼ Kiwi's Marbles                                           │
│    [Create celestial weld, active welds]                    │
│  ▼ Skittles — Theme Manager                                 │
│    [Theme picker, quick apply]                              │
│  ▼ Unladen Swallow                                          │
│    [Server enable/disable, status]                          │
│  ▼ Zippo — Light Control                                    │
│    [Vehicle/part selection, intensity, color]               │
│─────────────────────────────────────────────────────────────│
│  [Close]                                                    │
└─────────────────────────────────────────────────────────────┘
```

The `[⚙]` button in the top-right opens a popup where each submod has a checkbox toggle for visibility in the main window.

### Additional Windows (Remain Separate)

- **Skittles Theme Editor** — `ImGui.ShowStyleEditor()` wrapping window (opened via "Open Theme Editor" button inside the Skittles collapsible header)

### Key Design Decisions

1. **Each submod gets its own `IUnscienceSubmod` render file** — a class that encapsulates state + `RenderContent()` + `Update(dt)` methods, living in the **unscience** project as a thin UI wrapper around the `.lib` logic. Target ~300 lines per file max.
2. **One unified Patcher.cs** in unscience — consolidates all Harmony patches from blinky, glass, i-feel-seen, and skittles. Uses a single `Harmony("MeowSci.Unscience")` instance.
3. **No changes to `.lib` projects** — all existing `.lib` code stays as-is. The unscience mod simply references and uses them.
4. **Standalone mods are NOT deleted** — they continue to work independently. The refactor makes their ImGui rendering reuse the same `.lib` logic but they remain separate deployable mods.
5. **Submod visibility state** is stored in a `Dictionary<string, bool>` keyed by submod name. Initially all visible.

---

## Task List

### Task 0: Define the `IUnscienceSubmod` Interface

**Goal:** Create a simple interface/base pattern that each submod renderer will implement in the unscience project.

**Files to create:**
- `unscience/IUnscienceSubmod.cs`

**Interface definition:**
```csharp
namespace MeowSci.Unscience;

/// <summary>
/// Interface for a submod panel rendered inside the unscience supermod window.
/// </summary>
internal interface IUnscienceSubmod
{
    /// <summary>Display name shown in the collapsible header and context menu.</summary>
    string Name { get; }

    /// <summary>
    /// Called once during OnFullyLoaded. Initialize state, create instances of .lib classes, etc.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Called every frame in OnBeforeUi for submods that need pre-UI computation
    /// (e.g., TWR sampling, fuel manager ticking, game thread draining).
    /// </summary>
    void Update(double dt);

    /// <summary>
    /// Renders this submod's ImGui content. Called between Begin/End of the
    /// main unscience window — do NOT call ImGui.Begin/ImGui.End for the main content.
    /// Additional popup/child windows (like Skittles editor) are fine.
    /// </summary>
    void RenderContent();

    /// <summary>Called during Unload to clean up resources.</summary>
    void Dispose();
}
```

**Acceptance criteria:**
- File compiles as part of the unscience project
- Interface is internal to `MeowSci.Unscience` namespace

---

### Task 1: Create `AverageTwrSubmod` 

**Goal:** Implement the Average TWR submod panel for unscience.

**Files to create:**
- `unscience/Submods/AverageTwrSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `average-twr.lib` → `TwrSampleAccumulator`, `TwrDataReader`, `TwrStatistics`
- `ksa-abstractions.lib` → `VehicleProvider`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Average TWR"`
- `Initialize()` — creates a `TwrSampleAccumulator` instance
- `Update(dt)` — replicates the sampling logic from `average-twr/Mod.cs` `OnBeforeUi`: accumulate dt, every 10ms sample the controlled vehicle's TWR and max accel via `TwrDataReader`, feed to `TwrSampleAccumulator`
- `RenderContent()` — replicates the ImGui rendering from `average-twr/Mod.cs` `RenderWindow()` method **without** `ImGui.Begin`/`ImGui.End` — just the content between them: sample count, TWR stats, accel stats, start/pause button, reset button
- `Dispose()` — no-op

**State fields (copied from `average-twr/Mod.cs`):**
- `TwrSampleAccumulator _accumulator`
- `double _timeSinceLastSample`
- `bool _isCollecting`
- `const double SampleInterval = 0.01`

**ImGui content to render (replicate from `average-twr/Mod.cs` `RenderWindow` method):**
- `Samples: {n}` text
- Separator
- TWR stats block (Mean, Std Dev, Harmonic, Brachi) — same formatting
- Accel stats block — same formatting
- Start/Pause toggle button
- Reset button

**Acceptance criteria:**
- Compiles with unscience project
- No `ImGui.Begin`/`ImGui.End` calls for the main window
- Uses only `.lib` types, no copy-paste of business logic

---

### Task 2: Create `BlinkySubmod`

**Goal:** Implement the Blinky LCD grid submod panel for unscience.

**Files to create:**
- `unscience/Submods/BlinkySubmod.cs`

**Dependencies (already exist, no changes needed):**
- `blinky.lib` → `BlinkyGridManager`, `LcdGridBuilder`, `LcdGridConfig`, `GridLayout`, `PixelGrid`, `BlinkyPixelGrid`, `PixelPatterns`, `ScrollAnimation`
- `ksa-abstractions.lib` → `VehicleProvider`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Blinky — Dynamic LCD Grid"`
- `Initialize()` — no-op
- `Update(dt)` — calls `BlinkyGridManager.TickAll(dt)` to advance scroll animations
- `RenderContent()` — replicates the full ImGui content from `blinky/Mod.cs` `RenderWindow()` method **without** `ImGui.Begin`/`ImGui.End`. This includes:
  - Vehicle status display
  - Grid Configuration collapsible header (width, height, layout, spacing, scale, offset, engine preset)
  - Build Control collapsible header (build/destroy/scan buttons, status messages, render toggle)
  - Patterns section (All On/Off, Checkerboard, Alt Rows/Cols)
  - Scroll section (Start/Stop, speed slider, active scroll indicator)
  - Active Vehicles Summary
  - Debug section (dump buttons)
- `Dispose()` — calls `BlinkyGridManager.Clear()`

**State fields (copied from `blinky/Mod.cs`):**
- `Dictionary<string, VehicleUiState> _uiStates` (define `VehicleUiState` as private nested class with `BuildMessage`, `BuildMessageIsError`, `ScrollSpeed` fields)
- Grid config fields: `_configWidth`, `_configHeight`, `_configSpacing`, `_configOffsetX/Y/Z`, `_configPartScale`, `_enginePartId`, `_configLayoutIndex`
- `static readonly string[] EnginePresets` array

**Important notes:**
- The `Patcher.RenderPixelParts` static bool must be accessible. The unscience Patcher will have this field. Reference it as `Patcher.RenderPixelParts` within unscience's namespace.
- All debug helper methods (`DoBuildGrid`, `DoScanVehicle`, `DumpVehiclePartsType`, `DumpRootPart`, `ListEngineTemplates`, `DumpGridEngines`, `DumpEngineActiveStates`, `ForceSetIsActiveAllOn`, `RescanGrid`, `DumpEngineComparison`, `DumpSingleEngine`, `DumpAllFields`, `SetBuildMessage`) should be replicated as private methods in this class.
- All ImGui IDs must be unique (already use `##blinky` suffixes which is good).

**Acceptance criteria:**
- Compiles with unscience project
- Full blinky UI functionality available via collapsible header
- No `ImGui.Begin`/`ImGui.End` for main window

---

### Task 3: Create `EternalFlameSubmod`

**Goal:** Implement the Eternal Flame infinite fuel submod panel for unscience.

**Files to create:**
- `unscience/Submods/EternalFlameSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `eternal-flame.lib` → `FuelManager`, `MonitoredVehicle`
- `ksa-abstractions.lib` → `VehicleProvider` (used indirectly via `Universe.CurrentSystem`)

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Eternal Flame — Infinite Fuel"`
- `Initialize()` — creates `FuelManager` instance
- `Update(dt)` — calls `_fuelManager.Update(dt)` exactly like `eternal-flame/Mod.cs` `OnBeforeUi`
- `RenderContent()` — replicates ImGui content from `eternal-flame/Mod.cs` `RenderWindow()` without `ImGui.Begin`/`ImGui.End`:
  - Orange header text + refill interval indicator
  - Vehicle selector with filterable combo (uses `ImGuiTextFilter`)
  - Add button
  - Refill interval drag slider
  - "Monitored Vehicles" separator text
  - Monitored vehicles table (Active checkbox, Vehicle name, Remove button)
- `Dispose()` — no-op

**State fields (copied from `eternal-flame/Mod.cs`):**
- `FuelManager _fuelManager`
- `ImGuiTextFilter _vehicleFilter`
- `int _selectedVehicleIndex`
- `int _refillIntervalMs`

**ImGui ID suffixes:** Keep existing `##selector`, `##active_{i}`, etc. or add `##ef` prefix to avoid collisions with other submods if needed.

**Acceptance criteria:**
- Compiles with unscience project
- Vehicle monitoring and refill loop works via `Update(dt)` → `FuelManager.Update(dt)`

---

### Task 4: Create `GarrysTorchSubmod`

**Goal:** Implement the Garry's Torch vehicle welding submod panel for unscience.

**Files to create:**
- `unscience/Submods/GarrysTorchSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `garrys-torch.lib` → `WeldEntry`, `WeldEngine`, `WeldPreset`
- `ksa-abstractions.lib` → `VehicleProvider`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Garry's Torch"`
- `Initialize()` — no-op
- `Update(dt)` — updates all active welds by calling `WeldEngine.UpdateWeld(weld)` for each, removes failed welds (same logic as `garrys-torch/Mod.cs` `OnAfterUi`). Note: this MUST run every frame even if not visible, because welds must be maintained.
- `RenderContent()` — replicates ImGui content from `garrys-torch/Mod.cs` `RenderWindow()` without `ImGui.Begin`/`ImGui.End`:
  - "Create Weld" section with source/target combo boxes
  - "Starting Data" collapsible with position, rotation, scale, lock rotation controls
  - Preset selector + "I'm feeling lucky" button
  - "Active Welds" section with per-weld collapsible headers
  - Per-weld editable position/rotation/scale/lock rotation + unweld button
- `Dispose()` — unweld all active welds (reset scales to 1.0)

**State fields (copied from `garrys-torch/Mod.cs`):**
- `List<WeldEntry> _welds`
- `int _pendingSourceIndex`, `_pendingTargetIndex`
- `float3 _pendingPosition`, `_pendingRotation`
- `float _pendingScale`
- `bool _pendingLockRotation`
- `string? _weldError`
- `int _selectedPresetIndex`

**Private methods to replicate:**
- `InitiateWeld(...)` — create weld, sort, apply initial scale
- `RemoveWeld(...)` — reset scale, remove from list
- `SortWelds()` — call `WeldEngine.TopologicalSort`

**Important:** The `Update(dt)` method must be called EVERY frame regardless of visibility because active welds need per-frame position updates. The `RenderContent()` method only runs when visible.

**Acceptance criteria:**
- Compiles with unscience project
- Welds persist and update even when the submod header is collapsed or hidden

---

### Task 5: Create `GlassSubmod`

**Goal:** Implement the Glass camera FOV submod panel for unscience.

**Files to create:**
- `unscience/Submods/GlassSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `glass.lib` → `FovController`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Glass — Camera Lens"`
- `Initialize()` — no-op
- `Update(dt)` — calls `FovController.ApplyFov()` every frame (same as `glass/Mod.cs` `OnAfterUi` logic)
- `RenderContent()` — replicates ImGui content from `glass/Mod.cs` `RenderWindow()` without `ImGui.Begin`/`ImGui.End`:
  - Cyan header text
  - Current FOV display in degrees
  - "Lens Presets" section with 8 radio buttons
  - "Manual FOV" section with checkbox + drag float slider
  - "Reset to Game Default" button
- `Dispose()` — calls `FovController.DisableOverride()` to restore game FOV control

**State fields (copied from `glass/Mod.cs`):**
- `int _selectedPreset`
- `float _manualFov`
- `bool _manualMode`
- `static readonly (string Name, float Fov)[] Presets` — same 8 presets

**Important:** `FovController.ApplyFov()` in `Update(dt)` must run every frame even when UI is hidden, because an active FOV override should persist until explicitly disabled.

**Acceptance criteria:**
- FOV override persists across visibility toggles
- All 8 presets + manual mode work

---

### Task 6: Create `IFeelSeenSubmod`

**Goal:** Implement the I Feel Seen vehicle render distance override submod panel for unscience.

**Files to create:**
- `unscience/Submods/IFeelSeenSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `i-feel-seen.lib` → `VehicleTracker`, `TrackedVehicle`
- `ksa-abstractions.lib` → `VehicleProvider`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"I Feel Seen"`
- `Initialize()` — creates `VehicleTracker` instance. **Exposes `Tracker` property** so the unscience Patcher can access it for the Harmony patches.
- `Update(dt)` — no-op (Harmony patches handle rendering)
- `RenderContent()` — replicates ImGui content from `i-feel-seen/Mod.cs` `RenderWindow()` without `ImGui.Begin`/`ImGui.End`:
  - Green header "Vehicle Render Distance Override"
  - Tracked vehicles list with per-vehicle checkbox + Remove button
  - Separator
  - Vehicle selector combo
  - "Add Vehicle" button
- `Dispose()` — calls `_tracker.Clear()`

**State fields (copied from `i-feel-seen/Mod.cs`):**
- `VehicleTracker _tracker`
- `int _pendingVehicleIndex`

**Important:** The `VehicleTracker` instance must be publicly accessible (via property) because the unscience `Patcher.cs` needs to pass it to the Harmony patches for `Vehicle.GetWorldMatrix` and `Vehicle.UpdateRenderData`. The Patcher will reference `IFeelSeenSubmod.Tracker`.

**Acceptance criteria:**
- Compiles with unscience project
- VehicleTracker instance accessible to Patcher

---

### Task 7: Create `KiwisMarblesSubmod`

**Goal:** Implement the Kiwi's Marbles celestial welding submod panel for unscience.

**Files to create:**
- `unscience/Submods/KiwisMarblesSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `kiwis-marbles.lib` → `CelestialWeldEntry`, `CelestialWeldEngine`
- `ksa-abstractions.lib` → `CelestialProvider`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Kiwi's Marbles"`
- `Initialize()` — no-op
- `Update(dt)` — updates all active celestial welds by calling `CelestialWeldEngine.UpdateWeld(weld)` for each, removes failed welds (same logic as `kiwis-marbles/Mod.cs` `OnAfterUi`). Must run every frame even when hidden.
- `RenderContent()` — replicates ImGui content from `kiwis-marbles/Mod.cs` `RenderWindow()` without `ImGui.Begin`/`ImGui.End`:
  - "Create Weld" section with source (celestial) and target (any orbiter) filterable dropdowns
  - Surface placement helper buttons
  - CCI offset input with unit scale selector (m/km/Mm/Gm)
  - Computed offset display in meters
  - "Create Weld" button
  - "Active Welds" section with per-weld collapsible headers
  - Per-weld editable offset with unit scale + "Unweld" button
- `Dispose()` — no-op

**State fields (copied from `kiwis-marbles/Mod.cs`):**
- `List<CelestialWeldEntry> _welds`
- `int _pendingSourceIndex`, `_pendingTargetIndex`
- `float3 _pendingOffset`
- `int _pendingOffsetScaleIndex`
- `string? _weldError`
- `Dictionary<int, (float3 proxy, int scaleIndex)> _weldEditState`
- `ImGuiTextFilter _sourceFilter`, `_targetFilter`
- `static readonly string[] OffsetScaleLabels`
- `static readonly double[] OffsetScaleFactors`

**Private methods to replicate:**
- `InitiateWeld(...)`, `RemoveWeld(...)`, `SortWelds()`, `FormatKm()`

**Acceptance criteria:**
- Celestial welds update every frame regardless of visibility
- Full weld creation and management UI

---

### Task 8: Create `SkittlesSubmod`

**Goal:** Implement the Skittles theme manager submod panel for unscience.

**Files to create:**
- `unscience/Submods/SkittlesSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `skittles.lib` → `ThemeManager`, `ThemeEntry`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Skittles — Theme Manager"`
- `Initialize()` — creates `ThemeManager`, calls `Initialize()`, finds initial theme index. **Exposes `HasFocusedTextInput` bool** for the unscience Patcher's hotkey blocking patch.
- `Update(dt)` — no-op
- `RenderContent()` — replicates the ImGui content from BOTH `skittles/Mod.cs` `RenderMainWindow()` content (the main theme picker) without `ImGui.Begin`/`ImGui.End`. Specifically:
  - Green header + "Global Theme Manager" disabled text
  - Active theme display
  - Theme selector with filter (filterable combo)
  - "Open Theme Editor" button (sets `_editorVisible = true`)
  - Quick apply buttons (Dark, Light, Classic, Rod, Reset)
  - Tracks whether any text input in this content has keyboard focus → sets `HasFocusedTextInput`
  
  **Also renders the editor window** as a separate `ImGui.Begin`/`ImGui.End` window when `_editorVisible` is true (this is the additional window case — the editor MUST be its own window because it wraps `ImGui.ShowStyleEditor()` which needs significant space).
- `Dispose()` — calls `_themeManager.RestoreDefaults()`

**State fields (copied from `skittles/Mod.cs`):**
- `ThemeManager _themeManager`
- `int _selectedThemeIndex`
- `ImInputString _filterInput`
- `ImInputString _themeNameInput`
- `bool _showSaveInput`
- `bool _editorVisible`
- `bool HasFocusedTextInput` (public, read by Patcher)

**Private methods to replicate:**
- `UpdateSelectedIndex()`, `FindThemeIndex()`

**Important:** The `HasFocusedTextInput` bool must be accessible from the unscience Patcher for the `GameSettings.OnKeyAll` patch. Set it per-frame during render.

**Acceptance criteria:**
- Theme picker works inside collapsible header
- Theme editor opens as separate ImGui window
- Hotkey blocking works when Skittles text inputs focused

---

### Task 9: Create `UnladenSwallowSubmod`

**Goal:** Implement the Unladen Swallow HTTP RPC server submod panel for unscience.

**Files to create:**
- `unscience/Submods/UnladenSwallowSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `unladen-swallow.lib` → `SwallowServer`
- `ksa-abstractions.lib` → `GameThread`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Unladen Swallow"`
- `Initialize()` — creates `SwallowServer` instance
- `Update(dt)` — calls `GameThread.DrainOnGameThread()` every frame (required for HTTP→game-thread scheduling)
- `RenderContent()` — replicates ImGui content from `unladen-swallow/Mod.cs` `RenderWindow()` without `ImGui.Begin`/`ImGui.End`:
  - Gold header "Unladen Swallow"
  - "HTTP RPC Server" separator
  - Enable/disable checkbox
  - Server status indicator (green "Running" or gray "Stopped")
- `Dispose()` — stops server if running: `_server.StopAsync().GetAwaiter().GetResult()`

**State fields (copied from `unladen-swallow/Mod.cs`):**
- `SwallowServer? _server`
- `bool _serverEnabled`

**Important:** `GameThread.DrainOnGameThread()` must run every frame in `Update(dt)` regardless of visibility.

**Acceptance criteria:**
- HTTP server starts/stops correctly
- Game thread drain runs every frame

---

### Task 10: Create `ZippoSubmod`

**Goal:** Implement the Zippo light control submod panel for unscience.

**Files to create:**
- `unscience/Submods/ZippoSubmod.cs`

**Dependencies (already exist, no changes needed):**
- `zippo.lib` → `LightController`
- `ksa-abstractions.lib` → `VehicleProvider`

**What this class does:**
- Implements `IUnscienceSubmod`
- `Name` = `"Zippo — Light Control"`
- `Initialize()` — no-op
- `Update(dt)` — no-op
- `RenderContent()` — replicates ImGui content from `zippo/Mod.cs` `RenderWindow()` without `ImGui.Begin`/`ImGui.End`:
  - Yellow header text
  - Vehicle combo selector
  - Light part combo selector + Dbg button
  - On/off toggle button
  - Intensity drag slider
  - Color preset combo
  - Color picker (ColorEdit4)
- `Dispose()` — no-op

**State fields (copied from `zippo/Mod.cs`):**
- `List<Vehicle> _vehicles`
- `string[] _vehicleComboItems`
- `int _vehicleComboIdx`
- `List<Part> _lightParts`
- `string[] _lightPartComboItems`
- `int _lightPartComboIdx`
- `float _intensity`, `_savedIntensity`
- `bool _lightEnabled`
- `int _colorComboIdx`
- `float4 _currentColor`

**Private methods to replicate:**
- `SelectedVehicle` property, `SelectedLightPart` property
- `RefreshVehicles()`, `ClearLightParts()`, `RebuildLightParts()`, `OnPartSelected()`

**Acceptance criteria:**
- Compiles with unscience project
- Light control works for selected vehicle/part

---

### Task 11: Update `unscience/Patcher.cs` — Consolidate All Harmony Patches

**Goal:** Merge all necessary Harmony patches from blinky, glass, i-feel-seen, and skittles into the unscience Patcher.

**File to modify:**
- `unscience/Patcher.cs`

**What to implement:**

The Patcher uses a SINGLE Harmony instance `"MeowSci.Unscience"` and consolidates these patches:

1. **Blinky render-skip patches** (from `blinky/Patcher.cs`):
   - `PartModelModule.UpdateRenderData` prefix — skip `pixel_*` parts
   - `PartModelDynamicModule.UpdateRenderData` prefix — skip `pixel_*` parts
   - `PartModelGlassModule.UpdateRenderData` prefix — skip `pixel_*` parts
   - Requires a `public static bool RenderPixelParts = false;` field

2. **Glass FOV patches** (from `glass/Patcher.cs`):
   - `Camera.ChangeFieldOfView` prefix — block game FOV input when override active
   - `Camera.UpdateProjection` prefix — inject override FOV radians via `_fovRadians` field
   - Requires caching `_fovRadiansField` via `AccessTools.Field(typeof(Camera), "_fovRadians")`

3. **I Feel Seen render patches** (from `i-feel-seen/Patcher.cs`):
   - `Vehicle.GetWorldMatrix` prefix — custom world matrix for tracked vehicles
   - `Vehicle.UpdateRenderData` prefix — force render data update for tracked vehicles
   - Requires a `VehicleTracker?` reference (set from `IFeelSeenSubmod.Tracker` during initialization)

4. **Skittles hotkey blocking** (from `skittles/Patcher.cs`):
   - `GameSettings.OnKeyAll` prefix — block game hotkeys when Skittles text input is focused
   - Requires a `bool` reference (read from `SkittlesSubmod.HasFocusedTextInput`)

**Patcher structure:**
```csharp
internal static class Patcher
{
    private static Harmony? _harmony;
    
    // Blinky
    public static bool RenderPixelParts = false;
    
    // Glass
    private static FieldInfo? _fovRadiansField;
    
    // I Feel Seen
    public static VehicleTracker? IFeelSeenTracker;
    
    // Skittles
    public static Func<bool>? SkittlesHasFocusedTextInput;
    
    public static void Patch() { ... }
    public static void Unload() { ... }
    
    // Patch classes for each set of patches...
}
```

**Important design:**
- `IFeelSeenTracker` is set by `Mod.cs` after creating the `IFeelSeenSubmod` instance, before calling `Patch()`.
- `SkittlesHasFocusedTextInput` is a `Func<bool>` delegate pointing to `() => skittlesSubmod.HasFocusedTextInput`, set by `Mod.cs` before calling `Patch()`.

**Acceptance criteria:**
- All 4 sets of Harmony patches work correctly
- Single Harmony instance with proper cleanup
- No regressions from standalone mod behavior

---

### Task 12: Update `unscience/unscience.csproj` — Add Missing Project References

**Goal:** Add project references for all `.lib` projects needed by the new submods.

**File to modify:**
- `unscience/unscience.csproj`

**Current references (already present):**
- `average-twr.lib`
- `blinken.lib`
- `byo-music.lib`
- `camera-controller-override.lib`
- `garrys-torch.lib`
- `geeforce.lib`
- `i-feel-seen.lib`
- `kitten-animations.lib`
- `ksa-abstractions.lib`
- `zippo.lib`

**References to ADD:**
- `blinky.lib` — `<ProjectReference Include="..\blinky.lib\blinky.lib.csproj" />`
- `eternal-flame.lib` — `<ProjectReference Include="..\eternal-flame.lib\eternal-flame.lib.csproj" />`
- `glass.lib` — `<ProjectReference Include="..\glass.lib\glass.lib.csproj" />`
- `kiwis-marbles.lib` — `<ProjectReference Include="..\kiwis-marbles.lib\kiwis-marbles.lib.csproj" />`
- `skittles.lib` — `<ProjectReference Include="..\skittles.lib\skittles.lib.csproj" />`
- `unladen-swallow.lib` — `<ProjectReference Include="..\unladen-swallow.lib\unladen-swallow.lib.csproj" />`

**Also update the CopyCustomContent target** to include MeowSci assemblies (the auto-copy pattern):
```xml
<ItemGroup>
  <MeowSciAssemblies Include="$(TargetDir)MeowSci.*.dll;$(TargetDir)MeowSci.*.pdb" />
</ItemGroup>
<Copy SourceFiles="@(MeowSciAssemblies)" DestinationFolder="$(DistDir)" Condition="'@(MeowSciAssemblies)' != ''" />
```

**Acceptance criteria:**
- All `.lib` references compile
- All MeowSci assemblies copy to dist

---

### Task 13: Rewrite `unscience/Mod.cs` — Main Supermod Orchestrator

**Goal:** Rewrite the unscience Mod.cs to be the supermod orchestrator that manages all submods and the unified ImGui window.

**File to modify:**
- `unscience/Mod.cs`

**What this class does:**

1. **Fields:**
   - `List<IUnscienceSubmod> _submods` — ordered list of all submod instances
   - `Dictionary<string, bool> _submodVisibility` — tracks which submods are shown (keyed by `IUnscienceSubmod.Name`)
   - `bool _windowVisible` — F11 toggle for main window
   - `bool _isInitialized`, `bool _isDisposed`

2. **`OnFullyLoaded()`:**
   - Create all submod instances in display order:
     1. `AverageTwrSubmod`
     2. `BlinkySubmod`
     3. `EternalFlameSubmod`
     4. `GarrysTorchSubmod`
     5. `GlassSubmod`
     6. `IFeelSeenSubmod`
     7. `KiwisMarblesSubmod`
     8. `SkittlesSubmod`
     9. `UnladenSwallowSubmod`
     10. `ZippoSubmod`
   - Set Patcher static fields:
     - `Patcher.IFeelSeenTracker = iFeelSeenSubmod.Tracker`
     - `Patcher.SkittlesHasFocusedTextInput = () => skittlesSubmod.HasFocusedTextInput`
   - Call `Patcher.Patch()`
   - Call `submod.Initialize()` for each
   - Initialize `_submodVisibility` with all submods visible (`true`)

3. **`OnBeforeUi(dt)`:**
   - Call `submod.Update(dt)` for **ALL** submods (even hidden ones — they may have frame-critical logic like weld updates, fuel refills, FOV application, game thread draining)

4. **`OnAfterUi(dt)`:**
   - F11 toggle for `_windowVisible`
   - If `_windowVisible`, call `RenderWindow()`

5. **`RenderWindow()`:**
   - `ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver)`
   - `ImGui.Begin("unscience Mod", ref _windowVisible)`
   - Render header: `"unscience"` in green + separator
   - **Context menu button** (top-right corner):
     - Use `ImGui.GetWindowWidth()` and `ImGui.SetCursorPosX()` to position a `[⚙]` button in the top-right
     - On click: `ImGui.OpenPopup("##unscience_context")`
     - In `ImGui.BeginPopup("##unscience_context")`:
       - For each submod: `ImGui.MenuItem(submod.Name, "", ref visible)` where `visible` is `_submodVisibility[submod.Name]`
     - `ImGui.EndPopup()`
   - **Submod content rendering:**
     - For each submod where `_submodVisibility[submod.Name]` is true:
       - `if (ImGui.CollapsingHeader(submod.Name, ImGuiTreeNodeFlags.DefaultOpen))`
       - `{  ImGui.Indent(); submod.RenderContent(); ImGui.Unindent(); }`
       - `ImGui.Separator()`
   - Close button
   - `ImGui.End()`

6. **`Unload()`:**
   - Call `submod.Dispose()` for each submod
   - Call `Patcher.Unload()`

**Acceptance criteria:**
- F11 toggles the unified window
- Context menu (gear button) shows/hides individual submods
- All submods' `Update(dt)` runs every frame regardless of visibility
- Only visible submods' `RenderContent()` is called
- All submods' `Dispose()` called on unload

---

### Task 14: Update Standalone Mods to Reuse `.lib` Logic (Optional/Future)

**Goal:** Refactor the standalone mod projects (average-twr, blinky, etc.) so their Mod.cs files become thin wrappers that reuse the same `.lib` types — eliminating duplication. This is a housekeeping step and is **lower priority** than getting unscience working.

**This task is informational only — it does NOT need to be done as part of the initial unscience unification.** The standalone mods already work and reference their `.lib` projects. The main risk of NOT doing this is divergent ImGui code, but since the `.lib` business logic is shared, this is cosmetic.

If done later, each standalone mod's Mod.cs would be simplified to create its own `XxxSubmod` instance, call `Initialize()` / `Update(dt)` / `RenderContent()` (wrapped in `ImGui.Begin`/`End`), and `Dispose()`.

---

### Task 15: Update `unscience/README.md`

**Goal:** Update the unscience README to document the supermod functionality.

**File to modify:**
- `unscience/README.md`

**Content to write:**
- Brief description of unscience as a unified supermod
- List of all 10 included submods with one-line descriptions
- F11 toggle, context menu usage
- Note that each submod's behavior matches its standalone counterpart
- Architecture notes (IUnscienceSubmod interface, submod files in `unscience/Submods/`)
- Dependencies (all `.lib` projects + ksa-abstractions.lib)

**Acceptance criteria:**
- README accurately describes the supermod

---

### Task 16: Update `REPOSITORY_INDEX.md`

**Goal:** Update the repository index to reflect that unscience is now a unified supermod.

**File to modify:**
- `REPOSITORY_INDEX.md`

**Changes:**
- Update the unscience section description from "Minimal Template Mod" to "Unified Supermod" with description of all included submod functionality
- Mention that standalone mods still work independently
- List all `.lib` dependencies

**Acceptance criteria:**
- REPOSITORY_INDEX accurately reflects the new unscience supermod

---

### Task 17: Build and Verify

**Goal:** Ensure the entire solution compiles cleanly.

**Steps:**
1. Run `dotnet build` from the solution root
2. Fix any compilation errors
3. Verify no regressions in standalone mod compilation

**Acceptance criteria:**
- `dotnet build` succeeds with 0 errors for the unscience project
- All other projects in the solution continue to compile cleanly

---

## Task Execution Order

Tasks can be executed in this suggested order, though Tasks 1–10 are independent of each other and can be done in any order:

1. **Task 0** — Define `IUnscienceSubmod` interface
2. **Task 12** — Update `unscience.csproj` with all `.lib` references
3. **Tasks 1–10** — Create all submod implementations (independent, any order)
4. **Task 11** — Consolidate Patcher.cs with all Harmony patches
5. **Task 13** — Rewrite Mod.cs as orchestrator
6. **Task 17** — Build and verify compilation
7. **Task 15** — Update unscience/README.md
8. **Task 16** — Update REPOSITORY_INDEX.md
9. **Task 14** — (Optional/Future) Refactor standalone mods

## File Summary

| File | Action | Task |
|------|--------|------|
| `unscience/IUnscienceSubmod.cs` | Create | 0 |
| `unscience/Submods/AverageTwrSubmod.cs` | Create | 1 |
| `unscience/Submods/BlinkySubmod.cs` | Create | 2 |
| `unscience/Submods/EternalFlameSubmod.cs` | Create | 3 |
| `unscience/Submods/GarrysTorchSubmod.cs` | Create | 4 |
| `unscience/Submods/GlassSubmod.cs` | Create | 5 |
| `unscience/Submods/IFeelSeenSubmod.cs` | Create | 6 |
| `unscience/Submods/KiwisMarblesSubmod.cs` | Create | 7 |
| `unscience/Submods/SkittlesSubmod.cs` | Create | 8 |
| `unscience/Submods/UnladenSwallowSubmod.cs` | Create | 9 |
| `unscience/Submods/ZippoSubmod.cs` | Create | 10 |
| `unscience/Patcher.cs` | Rewrite | 11 |
| `unscience/unscience.csproj` | Modify | 12 |
| `unscience/Mod.cs` | Rewrite | 13 |
| `unscience/README.md` | Rewrite | 15 |
| `REPOSITORY_INDEX.md` | Modify | 16 |