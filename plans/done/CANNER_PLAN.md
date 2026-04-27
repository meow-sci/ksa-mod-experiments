# CANNER — Multi-Grid Blinky Refactor Plan

## Overview

Refactor blinky to support **multiple named grids per vehicle**. Currently blinky stores one `GridState` per vehicle in `BlinkyGridManager._grids` keyed by `vehicleId`. After this refactor, each vehicle can have N named grids, each independently configured and operated. The RPC endpoints and ImGui UI must be adapted to work with the new `(vehicleId, gridName)` compound key.

---

## Current Architecture (What Exists)

### blinky.lib

| File | Role |
|---|---|
| `BlinkyGridManager.cs` | Static singleton. `Dictionary<string, GridState> _grids` keyed by `vehicleId`. Exposes Register/Unregister/Get/Clear plus scroll/static/off/pattern APIs taking `vehicleId` only. |
| `GridState` (in BlinkyGridManager.cs) | Per-vehicle state: `VehicleId`, `Vehicle`, `BlinkyPixelGrid`, `ScrollAnimation`, `ActivePixels` HashSet. |
| `BlinkySubmod.cs` | `ISubmod` implementation. Owns global grid config fields + per-vehicle `VehicleUiState` dict. `RenderContent()` renders a flat UI for the currently controlled vehicle's single grid. |
| `PixelGrid.cs` | Low-level grid: `Dictionary<(int row, int col), (Part a, Part b)> _grid` and cached `EngineController[]`. Scan by part ID `pixel_{row}_{col}_{a|b}`. |
| `LcdGridConfig.cs` | Config POCO: Layout, Width, Height, Spacing, Offset XYZ, EnginePartId, PartScale. |
| `LcdGridBuilder.cs` | `BuildGrid(Vehicle, LcdGridConfig)`, `DestroyGrid(Vehicle, BlinkyPixelGrid)`, `ScanExistingGrid(Vehicle, engineTemplateId)`. Part naming: `pixel_{row}_{col}_{a|b}`. |
| `ScrollAnimation.cs` | Per-grid scroll state. Start/Stop/Update. |
| `BlinkyPatches.cs` | Harmony patches to skip render for parts with ID starting `pixel_`. |
| `BlinkyPatchState.cs` | `static bool RenderPixelParts` toggle. |
| `BlinkyPixelGrid.cs` | Wrapper: `PixelGrid Grid` + `IReadOnlyList<Part> OwnedParts` + `bool IsOwned`. |
| `PixelPatterns.cs` | Pattern functions: AllOn, Checkerboard, AlternatingRows, AlternatingCols. |
| `BuiltInScrollPixels.cs` | Default scroll pixel data. |

### unladen-swallow.lib (RPC)

| File | Role |
|---|---|
| `ApiTypes.cs` | Request/response records. Blinky types: `BlinkyScrollRequest(VehicleId, Pixels, Speed)`, `BlinkyStaticRequest(VehicleId, Pixels, Reset)`, `BlinkyOffRequest(VehicleId)`, `BlinkyResult(VehicleId, Action)`. |
| `BlinkyAnimateEndpoint.cs` | `POST /blinky/animate` — calls `BlinkyGridManager.StartScroll(vehicleId, pixels, speed)`. |
| `BlinkyStaticEndpoint.cs` | `POST /blinky/static` — calls `BlinkyGridManager.DisplayStatic(vehicleId, pixels, reset)`. |
| `BlinkyOffEndpoint.cs` | `POST /blinky/off` — calls `BlinkyGridManager.TurnOff(vehicleId)`. |
| `SwallowServer.cs` | Route registration in `RegisterRoutes()`. |

### Consumers

- **blinky mod** (`blinky/Mod.cs`) — standalone mod, creates `BlinkySubmod`, renders window.
- **grant mod** (`grant/Mod.cs`) — supermod, creates `BlinkySubmod` as one of 14 submods.
- **unladen-swallow.lib** — RPC endpoints call `BlinkyGridManager` static methods.

---

## Target Architecture (What We Want)

### Key Changes

1. **Compound key**: `BlinkyGridManager` changes from `Dictionary<string, GridState>` to `Dictionary<(string vehicleId, string gridName), GridState>`.
2. **GridState** gains a `GridName` property.
3. **All public APIs** on `BlinkyGridManager` change to take `(string vehicleId, string gridName)` instead of just `string vehicleId`.
4. **Part naming** changes from `pixel_{row}_{col}_{a|b}` to `pixel_{gridName}_{row}_{col}_{a|b}` so grids are independently identifiable via scan.
5. **BlinkyPatches** prefix check changes from `StartsWith("pixel_")` to still use `StartsWith("pixel_")` (unchanged — all grids use this prefix).
6. **BlinkySubmod UI** restructured: global config at top, then each grid under its own collapsible header with full controls.
7. **RPC endpoints** gain a `GridName` field in all request types.
8. **Grid name validation**: names must be non-empty, contain only `[a-zA-Z0-9-]` (no underscores — they're the part ID delimiter), and be unique per vehicle.
9. **Vehicle scan discovers all grids**: `PixelGrid.ScanAllFromVehicle(vehicle)` parses all `pixel_{gridName}_{row}_{col}_{a|b}` parts, groups by `gridName`, and returns a `Dictionary<string, PixelGrid>` — one entry per discovered grid name. This lets the UI offer a "Scan All Grids" button that auto-discovers and registers every grid on a vehicle.

---

## Tasks

### Task 1: Refactor `GridState` to include grid name

**File:** `blinky.lib/BlinkyGridManager.cs`

**Changes:**
- Add `public string GridName { get; }` property to `GridState`.
- Update constructor to accept and store `gridName`.

**Before:**
```csharp
public class GridState
{
    public string VehicleId { get; }
    public Vehicle Vehicle { get; }
    public BlinkyPixelGrid BlinkyGrid { get; }
    public ScrollAnimation Scroll { get; } = new();
    internal HashSet<(int row, int col)> ActivePixels { get; } = new();

    public GridState(string vehicleId, Vehicle vehicle, BlinkyPixelGrid grid)
    {
        VehicleId = vehicleId;
        Vehicle = vehicle;
        BlinkyGrid = grid;
    }
}
```

**After:**
```csharp
public class GridState
{
    public string VehicleId { get; }
    public string GridName { get; }
    public Vehicle Vehicle { get; }
    public BlinkyPixelGrid BlinkyGrid { get; }
    public ScrollAnimation Scroll { get; } = new();
    internal HashSet<(int row, int col)> ActivePixels { get; } = new();

    public GridState(string vehicleId, string gridName, Vehicle vehicle, BlinkyPixelGrid grid)
    {
        VehicleId = vehicleId;
        GridName = gridName;
        Vehicle = vehicle;
        BlinkyGrid = grid;
    }
}
```

---

### Task 2: Refactor `BlinkyGridManager` to use compound key `(vehicleId, gridName)`

**File:** `blinky.lib/BlinkyGridManager.cs`

**Changes:**

1. Change internal dictionary type:
   ```csharp
   // Before:
   private static readonly Dictionary<string, GridState> _grids = new();
   // After:
   private static readonly Dictionary<(string vehicleId, string gridName), GridState> _grids = new();
   ```

2. Change `Grids` property type:
   ```csharp
   // Before:
   public static IReadOnlyDictionary<string, GridState> Grids => _grids;
   // After:
   public static IReadOnlyDictionary<(string vehicleId, string gridName), GridState> Grids => _grids;
   ```

3. Add a convenience method to get all grids for a vehicle:
   ```csharp
   /// <summary>Returns all grids registered for the given vehicle ID.</summary>
   public static IEnumerable<GridState> GetAllForVehicle(string vehicleId)
   {
       foreach (var state in _grids.Values)
           if (state.VehicleId == vehicleId)
               yield return state;
   }
   ```

4. Refactor **every public method** to take `(string vehicleId, string gridName)` instead of `string vehicleId`. These methods must be updated:

   | Method | Old Signature | New Signature |
   |---|---|---|
   | `Register` | `Register(Vehicle vehicle, BlinkyPixelGrid grid)` | `Register(Vehicle vehicle, string gridName, BlinkyPixelGrid grid)` |
   | `Unregister` | `Unregister(string vehicleId)` | `Unregister(string vehicleId, string gridName)` |
   | `Get` | `Get(string vehicleId)` | `Get(string vehicleId, string gridName)` |
   | `StartScroll` | `StartScroll(string vehicleId, ...)` | `StartScroll(string vehicleId, string gridName, ...)` |
   | `StartBuiltInScroll` | `StartBuiltInScroll(string vehicleId, ...)` | `StartBuiltInScroll(string vehicleId, string gridName, ...)` |
   | `StopScroll` | `StopScroll(string vehicleId)` | `StopScroll(string vehicleId, string gridName)` |
   | `DisplayStatic` | `DisplayStatic(string vehicleId, ...)` | `DisplayStatic(string vehicleId, string gridName, ...)` |
   | `TurnOff` | `TurnOff(string vehicleId)` | `TurnOff(string vehicleId, string gridName)` |
   | `ApplyPattern` | `ApplyPattern(string vehicleId, ...)` | `ApplyPattern(string vehicleId, string gridName, ...)` |

5. The internal `_grids` dictionary key becomes `(vehicleId, gridName)`. Update all `.ContainsKey`, `.TryGetValue`, `.Remove`, indexer calls to use tuples.

6. Update `Register`:
   ```csharp
   public static GridState Register(Vehicle vehicle, string gridName, BlinkyPixelGrid grid)
   {
       var id = vehicle.Id;
       var key = (id, gridName);
       if (_grids.ContainsKey(key))
           Console.WriteLine($"blinky: replacing existing grid '{gridName}' for vehicle '{id}'");

       var state = new GridState(id, gridName, vehicle, grid);
       _grids[key] = state;
       Console.WriteLine($"blinky: registered grid '{gridName}' for vehicle '{id}' ({grid.Grid.Cols}x{grid.Grid.Rows})");
       return state;
   }
   ```

7. Update `Unregister`:
   ```csharp
   public static void Unregister(string vehicleId, string gridName)
   {
       var key = (vehicleId, gridName);
       if (_grids.TryGetValue(key, out var state))
       {
           state.Scroll.Stop();
           _grids.Remove(key);
           Console.WriteLine($"blinky: unregistered grid '{gridName}' for vehicle '{vehicleId}'");
       }
   }
   ```

8. Update `Get`:
   ```csharp
   public static GridState? Get(string vehicleId, string gridName)
   {
       _grids.TryGetValue((vehicleId, gridName), out var state);
       return state;
   }
   ```

9. `Clear()` stays the same (iterates all values, clears dictionary).

10. `TickAll(double dt)` stays the same (iterates all values, updates scroll).

11. Update all remaining methods (`StartScroll`, `StartBuiltInScroll`, `StopScroll`, `DisplayStatic`, `TurnOff`, `ApplyPattern`) to use `Get(vehicleId, gridName)` instead of `Get(vehicleId)`.

---

### Task 3: Update part naming to include grid name

**File:** `blinky.lib/LcdGridBuilder.cs`

**Changes:**

1. `BuildGrid` signature change:
   ```csharp
   // Before:
   public static BlinkyPixelGrid? BuildGrid(Vehicle vehicle, LcdGridConfig config)
   // After:
   public static BlinkyPixelGrid? BuildGrid(Vehicle vehicle, string gridName, LcdGridConfig config)
   ```

2. Part ID generation changes from:
   ```csharp
   $"pixel_{row}_{col}_{slot}"
   ```
   to:
   ```csharp
   $"pixel_{gridName}_{row}_{col}_{slot}"
   ```
   Find all occurrences in the build loop where part IDs are constructed and update them.

3. `ScanExistingGrid` signature change:
   ```csharp
   // Before:
   public static BlinkyPixelGrid? ScanExistingGrid(Vehicle vehicle, string engineTemplateId)
   // After:
   public static BlinkyPixelGrid? ScanExistingGrid(Vehicle vehicle, string gridName, string engineTemplateId)
   ```
   The template-based scan doesn't rely on part IDs so it doesn't need a name filter, but the `gridName` parameter is needed so the caller can register the result correctly. Pass it through unchanged.

**File:** `blinky.lib/PixelGrid.cs`

4. **Update part ID parsing** in `ScanFromVehicle`. Currently it splits on `_` and expects 4 segments: `["pixel", row, col, slot]`. After change, expect 5 segments: `["pixel", gridName, row, col, slot]`.

   Change `ScanFromVehicle` to accept a grid name and filter by it:

   ```csharp
   // Before:
   public static PixelGrid ScanFromVehicle(Vehicle vehicle)
   // After:
   public static PixelGrid ScanFromVehicle(Vehicle vehicle, string gridName)
   ```

   Only include parts where segment[1] matches the requested `gridName`.

5. **Add `ScanAllFromVehicle`** — a new method that discovers ALL grids on a vehicle by parsing every `pixel_*` part and grouping by grid name:

   ```csharp
   /// <summary>
   /// Scans a vehicle for ALL named pixel grids.
   /// Parses part IDs matching pixel_{gridName}_{row}_{col}_{a|b},
   /// groups by gridName, and returns a PixelGrid per discovered grid.
   /// </summary>
   public static Dictionary<string, PixelGrid> ScanAllFromVehicle(Vehicle vehicle)
   ```

   **Implementation logic:**
   - Recursively walk `vehicle.Parts.Parts` (same as current `ScanFromVehicle`).
   - For each part, split `part.Id` on `_`.
   - If segments.Length == 5 AND segments[0] == "pixel" AND segments[4] is "a" or "b" AND segments[2]/segments[3] parse as ints → extract `gridName = segments[1]`.
   - Group parts into `Dictionary<string, Dictionary<(int row, int col), (Part a, Part b)>>` keyed by grid name.
   - For each grid name group, construct a `PixelGrid` and call `RefreshEngineControllers()`.
   - Return `Dictionary<string, PixelGrid>` mapping grid name → populated PixelGrid.

   This enables the UI to offer a "Scan All Grids" button that auto-discovers every grid on a vehicle without the user needing to know grid names in advance.

6. **Grid name validation.** Grid names must match `[a-zA-Z0-9-]` (alphanumeric and hyphens only, no underscores) — this keeps the `_`-delimited part ID parsing simple and unambiguous. Add a static validation method:

   ```csharp
   // In a new or existing utility location (e.g. BlinkyGridManager or a small GridNameValidator class)
   public static bool IsValidGridName(string name)
   {
       if (string.IsNullOrWhiteSpace(name)) return false;
       foreach (char c in name)
           if (!char.IsLetterOrDigit(c) && c != '-') return false;
       return true;
   }
   ```

---

### Task 4: Refactor `BlinkySubmod` UI for multiple grids

**File:** `blinky.lib/BlinkySubmod.cs`

This is the largest UI change. The current flat UI shows one grid for the controlled vehicle. The new UI must:

1. **Keep global grid configuration at the top** (width, height, spacing, offset, engine template, layout mode, part scale) — these settings apply when building the *next* grid.

2. **Add a "Create Grid" section** with:
   - A text input for the new grid name (required, validated against `[a-zA-Z0-9-]`, no duplicates for same vehicle).
   - A "Build Grid" button that builds using the global config and the entered name.
   - A "Scan Grid" button that scans for an existing grid matching the entered name (uses `PixelGrid.ScanFromVehicle(vehicle, gridName)`).
   - A "Scan All Grids" button that auto-discovers ALL grids on the vehicle (uses `PixelGrid.ScanAllFromVehicle(vehicle)`), registers each one in `BlinkyGridManager`, and reports how many were found. This does NOT require a name input — it discovers names from part IDs.
   - Error/success messages.

3. **Replace the flat per-vehicle grid controls with a loop over all grids for the current vehicle.** For each grid, render under an `ImGui.CollapsingHeader($"{gridName}##grid_{vehicleId}_{gridName}")`:
   - Grid info line: `"16x8 (256 parts)"`.
   - Pattern buttons: All Off, All On, Checkerboard, Alt Rows, Alt Cols.
   - Scroll controls: Start/Stop, speed slider, status.
   - Destroy Grid button.
   - Rescan Grid button.

4. **Refactor per-vehicle UI state** to per-grid UI state:
   ```csharp
   // Before:
   private readonly Dictionary<string, VehicleUiState> _uiStates;
   
   // After:
   private readonly Dictionary<(string vehicleId, string gridName), GridUiState> _uiStates;
   ```
   
   The `GridUiState` class (renamed from `VehicleUiState`) stores:
   ```csharp
   private class GridUiState
   {
       public string BuildMessage = "";
       public bool BuildMessageIsError;
       public float ScrollSpeed = 3f;
   }
   ```

5. **Add state for the grid name text input:**
   ```csharp
   private string _newGridName = "";
   ```

6. **Update `DoBuildGrid`** to accept and pass through the grid name:
   ```csharp
   private void DoBuildGrid(Vehicle vehicle, string gridName, GridUiState ui)
   ```
   - Pass `gridName` to `LcdGridBuilder.BuildGrid(vehicle, gridName, config)`.
   - Pass `gridName` to `BlinkyGridManager.Register(vehicle, gridName, grid)`.

7. **Update `DoScanVehicle`** to accept and use the grid name:
   ```csharp
   private void DoScanVehicle(Vehicle vehicle, string gridName, GridUiState ui)
   ```
   - Pass `gridName` to `PixelGrid.ScanFromVehicle(vehicle, gridName)`.
   - Pass `gridName` to `LcdGridBuilder.ScanExistingGrid(vehicle, gridName, _enginePartId)`.
   - Pass `gridName` to `BlinkyGridManager.Register(vehicle, gridName, ...)`.

7b. **Add `DoScanAllGrids`** — a new method invoked by the "Scan All Grids" button:
   ```csharp
   private void DoScanAllGrids(Vehicle vehicle)
   ```
   - Call `PixelGrid.ScanAllFromVehicle(vehicle)` to get `Dictionary<string, PixelGrid>`.
   - For each `(gridName, pixelGrid)` in the result:
     - Call `pixelGrid.RefreshEngineControllers()`.
     - Wrap in `new BlinkyPixelGrid(pixelGrid, new List<Part>())` (scanned, not owned).
     - Call `BlinkyGridManager.Register(vehicle, gridName, blinkyGrid)`.
   - Display a summary message: `"Discovered {count} grid(s): {names}"`.
   - If none found, show error: `"No pixel grids found on vehicle"`.

8. **Update deferred destroy actions** to use `(vehicleId, gridName)`:
   - In the destroy button handler, capture both `vehicleId` and `gridName`.
   - Call `BlinkyGridManager.TurnOff(vehicleId, gridName)` and `BlinkyGridManager.Unregister(vehicleId, gridName)`.

9. **Update `RenderActiveVehiclesSummary`** to show grid names:
   - Iterate `BlinkyGridManager.Grids` and group by vehicle, showing each grid name.

10. **Update `Dispose`** — `BlinkyGridManager.Clear()` stays the same. Change `_uiStates.Clear()` to use the new dict type.

11. **Grid name validation in UI**: When the user presses "Build Grid" or "Scan Vehicle", validate the name:
    - Non-empty.
    - Matches `[a-zA-Z0-9-]` only.
    - Not already registered for this vehicle in `BlinkyGridManager`.
    - Show error message in UI if validation fails.

**Approximate UI layout:**

```
blinky — Dynamic LCD engine pixel grid
────────────────────────────────────────
Vehicle: <vehicleId>
Grids: 2 registered

▶ Grid Configuration            [collapsing header, default open]
    Width / Height / Layout / Spacing / Part Scale / Offset / Engine Template
    ─────────────────────
    New Grid Name: [____________]
    [Build Grid]  [Scan Grid]  [Scan All Grids]
    (status message)

▶ my-grid-1                      [collapsing header, default open]
    Grid: 16x8 (256 parts)
    [All Off] [All On] [Checkerboard] [Alt Rows] [Alt Cols]
    ── Scroll ──
    [Start/Stop]  Speed: [====3.0====]
    Scrolling offset=12.3 image 67x64
    ──
    [Destroy Grid]  [Rescan Grid]

▶ side-display                   [collapsing header, default open]
    Grid: 8x4 (64 parts)
    ...

────────────────────────────────────────
Show engine meshes: [ ]
Tracked: 1 vehicle(s), 2 grid(s), 1 scrolling
```

---

### Task 5: Update RPC API types for grid name

**File:** `unladen-swallow.lib/ApiTypes.cs`

**Changes:**

Add `GridName` field to all blinky request types:

```csharp
// Before:
public record BlinkyScrollRequest(string VehicleId, PixelCoord[] Pixels, float Speed);
public record BlinkyStaticRequest(string VehicleId, PixelCoord[] Pixels, bool Reset);
public record BlinkyOffRequest(string VehicleId);
public record BlinkyResult(string VehicleId, string Action);

// After:
public record BlinkyScrollRequest(string VehicleId, string GridName, PixelCoord[] Pixels, float Speed);
public record BlinkyStaticRequest(string VehicleId, string GridName, PixelCoord[] Pixels, bool Reset);
public record BlinkyOffRequest(string VehicleId, string GridName);
public record BlinkyResult(string VehicleId, string GridName, string Action);
```

---

### Task 6: Update RPC endpoints to pass grid name

**File:** `unladen-swallow.lib/BlinkyAnimateEndpoint.cs`

**Changes:**
- Add validation: `if (string.IsNullOrWhiteSpace(body.GridName)) throw ... "Missing gridName."`.
- Change call: `BlinkyGridManager.StartScroll(body.VehicleId, body.GridName, pixels, body.Speed)`.
- Change result: `new BlinkyResult(body.VehicleId, body.GridName, "scroll_started")`.
- Update error message to include grid name: `$"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}."`.

**File:** `unladen-swallow.lib/BlinkyStaticEndpoint.cs`

**Changes:**
- Add validation: `if (string.IsNullOrWhiteSpace(body.GridName)) throw ... "Missing gridName."`.
- Change call: `BlinkyGridManager.DisplayStatic(body.VehicleId, body.GridName, pixels, body.Reset)`.
- Change result: `new BlinkyResult(body.VehicleId, body.GridName, ...)`.
- Update error message to include grid name.

**File:** `unladen-swallow.lib/BlinkyOffEndpoint.cs`

**Changes:**
- Add validation: `if (string.IsNullOrWhiteSpace(body.GridName)) throw ... "Missing gridName."`.
- Change call: `BlinkyGridManager.TurnOff(body.VehicleId, body.GridName)`.
- Change result: `new BlinkyResult(body.VehicleId, body.GridName, "off")`.
- Update error message to include grid name.

---

### Task 7: Add a "list grids" RPC endpoint

**New file:** `unladen-swallow.lib/BlinkyListEndpoint.cs`

Add a new endpoint so RPC clients can discover which grids exist for a vehicle (or all vehicles).

**Route:** `GET /blinky/grids?vehicleId=<optional>`

**Behavior:**
- If `vehicleId` query param is provided, return grids for that vehicle only.
- If omitted, return all grids across all vehicles.

**Response type** (add to `ApiTypes.cs`):
```csharp
public record BlinkyGridInfo(string VehicleId, string GridName, int Rows, int Cols, bool IsScrolling);
public record BlinkyGridListResult(BlinkyGridInfo[] Grids);
```

**Implementation:**
```csharp
public static class BlinkyListEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async (string? vehicleId) =>
            {
                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    var grids = new List<BlinkyGridInfo>();
                    foreach (var state in BlinkyGridManager.Grids.Values)
                    {
                        if (vehicleId != null && state.VehicleId != vehicleId) continue;
                        grids.Add(new BlinkyGridInfo(
                            state.VehicleId,
                            state.GridName,
                            state.BlinkyGrid.Grid.Rows,
                            state.BlinkyGrid.Grid.Cols,
                            state.Scroll.IsActive));
                    }
                    return new BlinkyGridListResult(grids.ToArray());
                });
                return (object)new ApiResponse<BlinkyGridListResult>("ok", result);
            })
            .Build();
    }
}
```

**Register in `SwallowServer.cs`:**
```csharp
api.Add("blinky", Layout.Create()
    .Add("grids", BlinkyListEndpoint.Create())    // NEW
    .Add("animate", BlinkyAnimateEndpoint.Create())
    .Add("static", BlinkyStaticEndpoint.Create())
    .Add("off", BlinkyOffEndpoint.Create()));
```

---

### Task 8: Update `openapi.yml` in unladen-swallow.lib

**File:** `unladen-swallow.lib/openapi.yml`

Update all blinky endpoint schemas to include the `gridName` field in request bodies and responses. Add the new `GET /blinky/grids` endpoint documentation with optional `vehicleId` query parameter.

---

### Task 9: Update grant supermod (if needed)

**Files:** `grant/Mod.cs`, `grant/Patcher.cs`

The grant supermod creates `new BlinkySubmod()` and calls its `ISubmod` interface methods. Since the `ISubmod` interface hasn't changed (same `Initialize`, `Update`, `RenderContent`, `Dispose`), **no changes are needed in grant** itself. The `BlinkySubmod` internal changes (Task 4) handle everything.

Verify by building — if grant compiles without changes, this task is complete.

---

### Task 10: Update README and REPOSITORY_INDEX

**Files:**
- `blinky/README.md` — Update to document multi-grid support, grid naming, new part ID format.
- `REPOSITORY_INDEX.md` — Update blinky and unladen-swallow entries to mention multi-grid per vehicle and the new `/blinky/grids` endpoint.

---

### Task 11: Build and verify compilation

Run `dotnet build` on the entire solution. Fix any compilation errors. Ensure all projects compile cleanly:
- `blinky.lib`
- `blinky`
- `unladen-swallow.lib`
- `unladen-swallow`
- `grant`
- All other mods (should be unaffected but verify no transitive breakage)

---

## Execution Order

Tasks should be executed in this order due to dependencies:

1. **Task 1** — GridState gets gridName (no downstream breakage yet, just adds a field)
2. **Task 3** — Part naming + PixelGrid scan + grid name validation (prepare the foundation)
3. **Task 2** — BlinkyGridManager refactored to compound key (this breaks all callers)
4. **Task 4** — BlinkySubmod UI refactored (fixes the blinky.lib caller)
5. **Task 5** — RPC API types updated (fixes types for endpoints)
6. **Task 6** — RPC endpoints updated (fixes unladen-swallow.lib callers)
7. **Task 7** — New list endpoint added
8. **Task 8** — openapi.yml updated
9. **Task 9** — Verify grant compiles
10. **Task 11** — Full build verification
11. **Task 10** — Documentation updates

---

## Design Decisions

| Decision | Rationale |
|---|---|
| Grid names restricted to `[a-zA-Z0-9-]` | Part IDs use `_` as delimiter (`pixel_{name}_{row}_{col}_{slot}`), so grid names must not contain `_` to keep parsing unambiguous. Hyphens allowed for readability. |
| Compound tuple key `(vehicleId, gridName)` | Simple, efficient, no custom key class needed. Tuples support equality and hashing by default in C#. |
| Global config shared across grids | User adjusts config once, then builds multiple grids with those settings. Each grid stores its own config at build time via the parts it creates. Config doesn't need to be persisted per-grid since it's baked into part positions/scale. |
| Grid name text input at build time only | Grid names are immutable after creation (they're embedded in part IDs). No rename feature needed. |
| `GetAllForVehicle()` as convenience method | Avoids every UI and consumer needing to filter the dictionary manually. Linear scan is fine for expected grid counts (1-10 per vehicle). |
| `ScanAllFromVehicle()` for auto-discovery | Lets users recover all grids after save/load or when grids were built in a previous session. Parses grid names from part IDs so no user input needed. |
| No changes to `BlinkyPatches` | The render-skip prefix check `StartsWith("pixel_")` already covers all grids regardless of name. No change needed. |
| `BlinkyPatchState.RenderPixelParts` remains global | Toggling render visibility per-grid would require per-grid patch state and more complex prefix matching. Global toggle is simpler and sufficient — user either wants to see all engine meshes or none. |
