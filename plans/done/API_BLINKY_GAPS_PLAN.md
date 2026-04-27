# Blinky API Gaps — Implementation Plan

## Overview

This plan closes the gap between blinky.lib's full feature set and what unladen-swallow currently exposes over HTTP RPC. The existing 4 endpoints grow to 13, structured around grid lifecycle, display control, and settings.

### Current Endpoints (4 — already implemented)
| Method | Path | Handler File | Status |
|--------|------|--------------|--------|
| GET | `/blinky/grids` | `BlinkyListEndpoint.cs` | ✅ Needs update (richer response) |
| POST | `/blinky/animate` | `BlinkyAnimateEndpoint.cs` | ✅ Needs update (add DELETE) |
| POST | `/blinky/static` | `BlinkyStaticEndpoint.cs` | ✅ Complete |
| POST | `/blinky/off` | `BlinkyOffEndpoint.cs` | ✅ Complete |

### New Endpoints (9 — to implement)
| Method | Path | Handler File | blinky.lib Method |
|--------|------|--------------|-------------------|
| POST | `/blinky/grids` | `BlinkyGridsEndpoint.cs` | `LcdGridBuilder.BuildGrid()` + `BlinkyGridManager.Register()` |
| DELETE | `/blinky/grids` | `BlinkyGridsEndpoint.cs` | `LcdGridBuilder.DestroyGrid()` + `BlinkyGridManager.Unregister()` |
| POST | `/blinky/grids/scan` | `BlinkyGridScanEndpoint.cs` | `PixelGrid.ScanFromVehicle()` or `LcdGridBuilder.ScanExistingGrid()` |
| POST | `/blinky/grids/scan-all` | `BlinkyGridScanAllEndpoint.cs` | `BlinkyGridManager.ScanAllVehicles()` |
| DELETE | `/blinky/animate` | `BlinkyAnimateEndpoint.cs` | `BlinkyGridManager.StopScroll()` |
| POST | `/blinky/animate/builtin` | `BlinkyBuiltInScrollEndpoint.cs` | `BlinkyGridManager.StartBuiltInScroll()` |
| POST | `/blinky/pattern` | `BlinkyPatternEndpoint.cs` | `BlinkyGridManager.ApplyPattern()` |
| GET/POST | `/blinky/render` | `BlinkyRenderEndpoint.cs` | `BlinkyPatchState.RenderPixelParts` |
| POST | `/blinky/engines/deactivate` | `BlinkyEngineDeactivateEndpoint.cs` | `NonLcdEngineCache.DeactivateAll()` |

### OpenAPI Spec
Updated at `unladen-swallow.lib/openapi/blinky.yml` (version 2.0.0).

---

## Task 1: Update ApiTypes.cs — Add New DTOs

**File:** `unladen-swallow.lib/ApiTypes.cs`

### 1a. Update existing `BlinkyGridInfo` record

Replace the current 5-field record with the enriched 8-field version. This is a **breaking change** to the `GET /blinky/grids` response — the 3 new fields (`pixelCount`, `isOwned`, `scrollSpeed`) are additive.

**Current:**
```csharp
public record BlinkyGridInfo(string VehicleId, string GridName, int Rows, int Cols, bool IsScrolling);
```

**Replace with:**
```csharp
public record BlinkyGridInfo(
    string VehicleId,
    string GridName,
    int Rows,
    int Cols,
    int PixelCount,
    bool IsOwned,
    bool IsScrolling,
    float ScrollSpeed);
```

### 1b. Add new request/response DTOs

Append the following records after the existing blinky types section (after `BlinkyGridListResult`):

```csharp
// ── Blinky Grid Management Types ────────────────────────────────────────────

/// <summary>Request body for POST /blinky/grids — builds a new pixel grid.</summary>
public record BlinkyBuildGridRequest(
    string VehicleId,
    string GridName,
    int? Width,
    int? Height,
    string? Layout,
    float? Spacing,
    float? OffsetX,
    float? OffsetY,
    float? OffsetZ,
    string? EnginePartId,
    double? PartScale);

/// <summary>Request body for POST /blinky/grids/scan — scans a vehicle for a grid.</summary>
public record BlinkyScanGridRequest(
    string VehicleId,
    string GridName,
    string? EnginePartId);

/// <summary>Result for POST /blinky/grids/scan-all.</summary>
public record BlinkyScanAllResult(int Discovered, string[] Grids);

/// <summary>Request body for POST /blinky/pattern.</summary>
public record BlinkyPatternRequest(
    string VehicleId,
    string GridName,
    string Pattern);

/// <summary>Request body for POST /blinky/animate/builtin.</summary>
public record BlinkyBuiltInScrollRequest(
    string VehicleId,
    string GridName,
    float Speed);

// ── Blinky Settings Types ───────────────────────────────────────────────────

/// <summary>Request body for POST /blinky/render.</summary>
public record BlinkyRenderSettingsRequest(bool RenderPixelParts);

/// <summary>Response for GET/POST /blinky/render.</summary>
public record BlinkyRenderSettings(bool RenderPixelParts);

/// <summary>Request body for POST /blinky/engines/deactivate.</summary>
public record BlinkyEngineDeactivateRequest(string VehicleId);
```

**Grid name validation** is NOT done in DTOs — it's done in endpoint handlers using `PixelGrid.IsValidGridName(string)`.

---

## Task 2: Update BlinkyListEndpoint.cs — Enrich Grid Info

**File:** `unladen-swallow.lib/BlinkyListEndpoint.cs`

Update the `GET` handler to return the enriched `BlinkyGridInfo` with all 8 fields.

**Current mapping (inside the foreach loop):**
```csharp
grids.Add(new BlinkyGridInfo(
    state.VehicleId,
    state.GridName,
    state.BlinkyGrid.Grid.Rows,
    state.BlinkyGrid.Grid.Cols,
    state.Scroll.IsActive));
```

**Replace with:**
```csharp
grids.Add(new BlinkyGridInfo(
    state.VehicleId,
    state.GridName,
    state.BlinkyGrid.Grid.Rows,
    state.BlinkyGrid.Grid.Cols,
    state.BlinkyGrid.Grid.Count,
    state.BlinkyGrid.IsOwned,
    state.Scroll.IsActive,
    state.Scroll.IsActive ? state.Scroll.ScrollSpeed : 0f));
```

**Key property mappings:**
- `PixelCount` → `state.BlinkyGrid.Grid.Count` (the `PixelGrid.Count` property = number of pixel pairs)
- `IsOwned` → `state.BlinkyGrid.IsOwned` (true when `OwnedParts.Count > 0`)
- `ScrollSpeed` → `state.Scroll.ScrollSpeed` (only meaningful when `IsActive`)

---

## Task 3: Create BlinkyGridsEndpoint.cs — Build & Destroy Grids

**File:** `unladen-swallow.lib/BlinkyGridsEndpoint.cs` (NEW)

This endpoint handles `POST` (build) and `DELETE` (destroy) on `/blinky/grids`. The existing `GET` remains in `BlinkyListEndpoint.cs` — route restructuring in Task 10 will wire them together under one path.

### 3a. POST handler — Build Grid

```csharp
using System;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class BlinkyGridsEndpoint
{
    public static IHandlerBuilder Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyBuildGridRequest body) =>
            {
                // ── Validate on HTTP thread ──────────────────────────────
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (!PixelGrid.IsValidGridName(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest,
                        $"Invalid gridName '{body.GridName}'. Allowed: a-z, A-Z, 0-9, hyphen.");

                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    // ── Check for conflict ───────────────────────────────
                    if (BlinkyGridManager.Get(body.VehicleId, body.GridName) != null)
                        throw new ProviderException(ResponseStatus.Conflict,
                            $"Grid '{body.GridName}' already registered for vehicle '{body.VehicleId}'.");

                    // ── Find vehicle ─────────────────────────────────────
                    var vehicle = VehicleProvider.FindVehicle(body.VehicleId);
                    if (vehicle == null)
                        throw new ProviderException(ResponseStatus.NotFound,
                            $"Vehicle not found: {body.VehicleId}.");

                    // ── Build config with defaults ───────────────────────
                    var config = new LcdGridConfig
                    {
                        Width = body.Width ?? 16,
                        Height = body.Height ?? 8,
                        Layout = ParseLayout(body.Layout),
                        Spacing = body.Spacing ?? 5.0f,
                        OffsetX = body.OffsetX ?? 0f,
                        OffsetY = body.OffsetY ?? 5f,
                        OffsetZ = body.OffsetZ ?? 2f,
                        EnginePartId = body.EnginePartId ?? "CorePropulsionA_Prefab_EngineA3",
                        PartScale = body.PartScale ?? 0.010,
                    };

                    // ── Build grid ───────────────────────────────────────
                    var blinkyGrid = LcdGridBuilder.BuildGrid(vehicle, body.GridName, config);
                    if (blinkyGrid == null)
                        throw new ProviderException(ResponseStatus.InternalServerError,
                            "Grid build failed. Check server logs for details.");

                    // ── Register ─────────────────────────────────────────
                    var state = BlinkyGridManager.Register(vehicle, body.GridName, blinkyGrid);

                    return new BlinkyGridInfo(
                        state.VehicleId,
                        state.GridName,
                        state.BlinkyGrid.Grid.Rows,
                        state.BlinkyGrid.Grid.Cols,
                        state.BlinkyGrid.Grid.Count,
                        state.BlinkyGrid.IsOwned,
                        state.Scroll.IsActive,
                        0f);
                });

                return (object)new ApiResponse<BlinkyGridInfo>("ok", result);
            })
            .Delete(async (string vehicleId, string gridName) =>
            {
                // ── query-param DELETE ────────────────────────────────────
                // GenHTTP binds primitive method params from query string.
                // DELETE /blinky/grids?vehicleId=x&gridName=y

                if (string.IsNullOrWhiteSpace(vehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId query parameter.");
                if (string.IsNullOrWhiteSpace(gridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName query parameter.");

                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    var state = BlinkyGridManager.Get(vehicleId, gridName);
                    if (state == null)
                        throw new ProviderException(ResponseStatus.NotFound,
                            $"No grid '{gridName}' registered for vehicle '{vehicleId}'.");

                    bool wasOwned = state.BlinkyGrid.IsOwned;

                    if (wasOwned)
                        LcdGridBuilder.DestroyGrid(state.Vehicle, state.BlinkyGrid);

                    BlinkyGridManager.Unregister(vehicleId, gridName);

                    string action = wasOwned ? "grid_destroyed" : "grid_unregistered";
                    return new BlinkyResult(vehicleId, gridName, action);
                });

                return (object)new ApiResponse<BlinkyResult>("ok", result);
            })
            .Build();
    }

    private static GridLayout ParseLayout(string? layout)
    {
        if (string.IsNullOrWhiteSpace(layout) ||
            layout.Equals("flat", StringComparison.OrdinalIgnoreCase))
            return GridLayout.Flat;
        if (layout.Equals("cylinder", StringComparison.OrdinalIgnoreCase))
            return GridLayout.Cylinder;
        throw new ProviderException(ResponseStatus.BadRequest,
            $"Invalid layout '{layout}'. Must be 'flat' or 'cylinder'.");
    }
}
```

### Key implementation details:

- **Vehicle lookup:** Uses `VehicleProvider.FindVehicle(string vehicleId)` from `ksa-abstractions.lib`. This is a helper that searches all vehicles by ID. If this method doesn't exist, use `VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == body.VehicleId)` instead.
- **Conflict check:** `BlinkyGridManager.Get(vehicleId, gridName) != null` → 409
- **Layout parsing:** Case-insensitive string → `GridLayout` enum
- **BuildGrid returns null on failure** — the method logs details itself; the endpoint returns a generic 500
- **DestroyGrid + Unregister:** Two separate calls. `DestroyGrid` removes parts. `Unregister` removes from the manager dictionary. Both must be called on the game thread.
- **DELETE uses query params:** GenHTTP Inline binds `(string vehicleId, string gridName)` from query string automatically, same as GET parameters work in `BlinkyListEndpoint`.

---

## Task 4: Create BlinkyGridScanEndpoint.cs — Per-Vehicle Scan

**File:** `unladen-swallow.lib/BlinkyGridScanEndpoint.cs` (NEW)

Handles `POST /blinky/grids/scan`. Two scan modes based on whether `enginePartId` is provided.

```csharp
using System;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class BlinkyGridScanEndpoint
{
    public static IHandlerBuilder Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyScanGridRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (!PixelGrid.IsValidGridName(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest,
                        $"Invalid gridName '{body.GridName}'. Allowed: a-z, A-Z, 0-9, hyphen.");

                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    var vehicle = VehicleProvider.FindVehicle(body.VehicleId);
                    if (vehicle == null)
                        throw new ProviderException(ResponseStatus.NotFound,
                            $"Vehicle not found: {body.VehicleId}.");

                    BlinkyPixelGrid blinkyGrid;

                    if (!string.IsNullOrWhiteSpace(body.EnginePartId))
                    {
                        // Template-based scan
                        var scanned = LcdGridBuilder.ScanExistingGrid(
                            vehicle, body.GridName, body.EnginePartId);
                        if (scanned == null)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No grid found on vehicle '{body.VehicleId}' matching template '{body.EnginePartId}'.");
                        blinkyGrid = scanned;
                    }
                    else
                    {
                        // ID-based scan
                        var pixelGrid = PixelGrid.ScanFromVehicle(vehicle, body.GridName);
                        if (pixelGrid.Count == 0)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No grid named '{body.GridName}' found on vehicle '{body.VehicleId}'.");
                        pixelGrid.RefreshEngineControllers();
                        blinkyGrid = new BlinkyPixelGrid(pixelGrid, new System.Collections.Generic.List<KSA.Part>());
                    }

                    var state = BlinkyGridManager.Register(vehicle, body.GridName, blinkyGrid);

                    return new BlinkyGridInfo(
                        state.VehicleId,
                        state.GridName,
                        state.BlinkyGrid.Grid.Rows,
                        state.BlinkyGrid.Grid.Cols,
                        state.BlinkyGrid.Grid.Count,
                        state.BlinkyGrid.IsOwned,
                        state.Scroll.IsActive,
                        0f);
                });

                return (object)new ApiResponse<BlinkyGridInfo>("ok", result);
            })
            .Build();
    }
}
```

### Key implementation details:

- **ID-based scan:** `PixelGrid.ScanFromVehicle(vehicle, gridName)` — looks for parts named `pixel_{gridName}_{row}_{col}_{a|b}`. Returns a `PixelGrid` with `Count == 0` if nothing found. Must call `RefreshEngineControllers()` after scan. Wraps in `BlinkyPixelGrid` with empty owned parts list (scanned, not owned).
- **Template-based scan:** `LcdGridBuilder.ScanExistingGrid(vehicle, gridName, enginePartId)` — finds small-scale engines matching template, groups by position, reconstructs grid spatially. Returns null if not found.
- **BlinkyPixelGrid constructor:** `new BlinkyPixelGrid(pixelGrid, ownedPartsList)` — empty list → `IsOwned = false`.

---

## Task 5: Create BlinkyGridScanAllEndpoint.cs — Global Vehicle Scan

**File:** `unladen-swallow.lib/BlinkyGridScanAllEndpoint.cs` (NEW)

Handles `POST /blinky/grids/scan-all`. No request body needed.

```csharp
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class BlinkyGridScanAllEndpoint
{
    public static IHandlerBuilder Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async () =>
            {
                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    var (discovered, names) = BlinkyGridManager.ScanAllVehicles();
                    return new BlinkyScanAllResult(discovered, names.ToArray());
                });

                return (object)new ApiResponse<BlinkyScanAllResult>("ok", result);
            })
            .Build();
    }
}
```

### Key implementation details:

- **`ScanAllVehicles()`** calls `VehicleProvider.GetAllVehicles()` internally, iterates all vehicles, calls `PixelGrid.ScanAllFromVehicle()` on each, and registers every discovered grid via `BlinkyGridManager.Register()`.
- Returns `(int discovered, List<string> names)` — names are formatted as `"{gridName} on {vehicleId}"`.
- No body needed — GenHTTP Inline `.Post(async () => ...)` with zero params is valid.

---

## Task 6: Update BlinkyAnimateEndpoint.cs — Add DELETE for Stop Scroll

**File:** `unladen-swallow.lib/BlinkyAnimateEndpoint.cs`

Add a `.Delete()` handler to the existing Inline chain. The POST handler stays unchanged.

**Current structure:**
```csharp
return Inline.Create()
    .Serializers(Serialization.Default())
    .Post(async (BlinkyScrollRequest body) => { ... })
    .Build();
```

**Add DELETE before `.Build()`:**
```csharp
return Inline.Create()
    .Serializers(Serialization.Default())
    .Post(async (BlinkyScrollRequest body) => { /* existing code unchanged */ })
    .Delete(async (string vehicleId, string gridName) =>
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
            throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId query parameter.");
        if (string.IsNullOrWhiteSpace(gridName))
            throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName query parameter.");

        var result = await GameThread.Scheduler.Schedule(() =>
        {
            if (!BlinkyGridManager.StopScroll(vehicleId, gridName))
                throw new ProviderException(ResponseStatus.NotFound,
                    $"No blinky grid '{gridName}' registered for vehicle: {vehicleId}.");

            return new BlinkyResult(vehicleId, gridName, "scroll_stopped");
        });

        return (object)new ApiResponse<BlinkyResult>("ok", result);
    })
    .Build();
```

### Key implementation details:

- **`StopScroll`** stops the scroll animation but does NOT turn off pixels. Pixels remain in their last-rendered state. This is distinct from `TurnOff` which both stops scroll AND turns off all pixels.
- DELETE uses query params: `DELETE /blinky/animate?vehicleId=x&gridName=y`

---

## Task 7: Create BlinkyBuiltInScrollEndpoint.cs

**File:** `unladen-swallow.lib/BlinkyBuiltInScrollEndpoint.cs` (NEW)

Handles `POST /blinky/animate/builtin`.

```csharp
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class BlinkyBuiltInScrollEndpoint
{
    public static IHandlerBuilder Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyBuiltInScrollRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (body.Speed <= 0)
                    throw new ProviderException(ResponseStatus.BadRequest, "Speed must be greater than 0.");

                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    if (!BlinkyGridManager.StartBuiltInScroll(body.VehicleId, body.GridName, body.Speed))
                        throw new ProviderException(ResponseStatus.NotFound,
                            $"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                    return new BlinkyResult(body.VehicleId, body.GridName, "builtin_scroll_started");
                });

                return (object)new ApiResponse<BlinkyResult>("ok", result);
            })
            .Build();
    }
}
```

### Key implementation details:

- **`StartBuiltInScroll`** uses `BuiltInScrollPixels.Pixels` — a pre-defined 800+ pixel array embedded in blinky.lib. The client doesn't need to supply pixel data.
- Same validation pattern as `BlinkyAnimateEndpoint`.

---

## Task 8: Create BlinkyPatternEndpoint.cs

**File:** `unladen-swallow.lib/BlinkyPatternEndpoint.cs` (NEW)

Handles `POST /blinky/pattern`.

```csharp
using System;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class BlinkyPatternEndpoint
{
    public static IHandlerBuilder Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyPatternRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (string.IsNullOrWhiteSpace(body.Pattern))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing pattern.");

                var selector = ResolvePattern(body.Pattern);

                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    if (!BlinkyGridManager.ApplyPattern(body.VehicleId, body.GridName, selector))
                        throw new ProviderException(ResponseStatus.NotFound,
                            $"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                    return new BlinkyResult(body.VehicleId, body.GridName, $"pattern_{body.Pattern}");
                });

                return (object)new ApiResponse<BlinkyResult>("ok", result);
            })
            .Build();
    }

    private static Func<(int row, int col), bool> ResolvePattern(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "allon"        => PixelPatterns.AllOn,
            "alloff"       => _ => false,
            "checkerboard" => PixelPatterns.Checkerboard,
            "altrows"      => PixelPatterns.AlternatingRows,
            "altcols"      => PixelPatterns.AlternatingCols,
            _ => throw new ProviderException(ResponseStatus.BadRequest,
                $"Unknown pattern '{name}'. Valid: allOn, allOff, checkerboard, altRows, altCols.")
        };
    }
}
```

### Key implementation details:

- **Pattern mapping:** String name → `Func<(int row, int col), bool>` delegate from `PixelPatterns` static class.
- **`allOff`** has no dedicated function in `PixelPatterns` — use inline `_ => false`. Alternatively, `allOff` could call `BlinkyGridManager.TurnOff()` instead of `ApplyPattern` — but `ApplyPattern` with `_ => false` achieves the same result (stops scroll + sets all pixels off).
- **`ApplyPattern`** stops any running scroll before applying.

---

## Task 9: Create BlinkyRenderEndpoint.cs and BlinkyEngineDeactivateEndpoint.cs

### 9a. BlinkyRenderEndpoint.cs (NEW)

**File:** `unladen-swallow.lib/BlinkyRenderEndpoint.cs`

Handles `GET` and `POST /blinky/render`.

```csharp
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class BlinkyRenderEndpoint
{
    public static IHandlerBuilder Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                var result = await GameThread.Scheduler.Schedule(() =>
                    new BlinkyRenderSettings(BlinkyPatchState.RenderPixelParts));

                return (object)new ApiResponse<BlinkyRenderSettings>("ok", result);
            })
            .Post(async (BlinkyRenderSettingsRequest body) =>
            {
                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    BlinkyPatchState.RenderPixelParts = body.RenderPixelParts;
                    return new BlinkyRenderSettings(BlinkyPatchState.RenderPixelParts);
                });

                return (object)new ApiResponse<BlinkyRenderSettings>("ok", result);
            })
            .Build();
    }
}
```

### Key implementation details:

- **`BlinkyPatchState.RenderPixelParts`** is a static bool in `blinky.lib/BlinkyPatchState.cs`. When `false` (default), Harmony prefixes skip `UpdateRenderData` calls for pixel parts, hiding their meshes.
- Setting it to `true` makes pixel engine meshes visible in-game. Setting to `false` hides them.
- This is a simple property toggle — no complex game state mutation needed.

### 9b. BlinkyEngineDeactivateEndpoint.cs (NEW)

**File:** `unladen-swallow.lib/BlinkyEngineDeactivateEndpoint.cs`

Handles `POST /blinky/engines/deactivate`.

```csharp
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class BlinkyEngineDeactivateEndpoint
{
    public static IHandlerBuilder Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyEngineDeactivateRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");

                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    var vehicle = VehicleProvider.FindVehicle(body.VehicleId);
                    if (vehicle == null)
                        throw new ProviderException(ResponseStatus.NotFound,
                            $"Vehicle not found: {body.VehicleId}.");

                    NonLcdEngineCache.DeactivateAll(vehicle);

                    return new BlinkyResult(body.VehicleId, "", "engines_deactivated");
                });

                return (object)new ApiResponse<BlinkyResult>("ok", result);
            })
            .Build();
    }
}
```

### Key implementation details:

- **`NonLcdEngineCache.DeactivateAll(Vehicle)`** lazily scans for non-pixel engines, then calls `SetIsActive(null, false)` on each. The cache is invalidated when grids change.
- The `gridName` field in `BlinkyResult` is empty string since this applies to a vehicle, not a specific grid.
- **Vehicle lookup** same as Task 3 — `VehicleProvider.FindVehicle(vehicleId)`. If this helper doesn't exist in `ksa-abstractions.lib`, add a private helper: `VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == vehicleId)`.

---

## Task 10: Update SwallowServer.cs — Route Registration

**File:** `unladen-swallow.lib/SwallowServer.cs`

Replace the existing blinky route block in `RegisterRoutes()` with the new expanded structure.

### Current:
```csharp
// GET  /blinky/grids
// POST /blinky/animate
// POST /blinky/static
// POST /blinky/off
api.Add("blinky", Layout.Create()
    .Add("grids", BlinkyListEndpoint.Create())
    .Add("animate", BlinkyAnimateEndpoint.Create())
    .Add("static", BlinkyStaticEndpoint.Create())
    .Add("off", BlinkyOffEndpoint.Create()));
```

### Replace with:
```csharp
// ── Blinky endpoints ─────────────────────────────────────────────────────
// GET    /blinky/grids                     — list grids
// POST   /blinky/grids                     — build grid
// DELETE /blinky/grids?vehicleId&gridName  — destroy grid
// POST   /blinky/grids/scan               — scan single vehicle
// POST   /blinky/grids/scan-all           — scan all vehicles
// POST   /blinky/animate                   — start scroll
// DELETE /blinky/animate?vehicleId&gridName — stop scroll
// POST   /blinky/animate/builtin           — start built-in scroll
// POST   /blinky/static                    — display static pixels
// POST   /blinky/pattern                   — apply pattern
// POST   /blinky/off                       — turn off
// GET    /blinky/render                    — get render settings
// POST   /blinky/render                    — set render settings
// POST   /blinky/engines/deactivate        — deactivate non-LCD engines
api.Add("blinky", Layout.Create()
    .Add("grids", Layout.Create()
        .Index(BlinkyListEndpoint.Create())
        .Add(BlinkyGridsEndpoint.Create())
        .Add("scan", BlinkyGridScanEndpoint.Create())
        .Add("scan-all", BlinkyGridScanAllEndpoint.Create()))
    .Add("animate", Layout.Create()
        .Index(BlinkyAnimateEndpoint.Create())
        .Add("builtin", BlinkyBuiltInScrollEndpoint.Create()))
    .Add("static", BlinkyStaticEndpoint.Create())
    .Add("pattern", BlinkyPatternEndpoint.Create())
    .Add("off", BlinkyOffEndpoint.Create())
    .Add("render", BlinkyRenderEndpoint.Create())
    .Add("engines", Layout.Create()
        .Add("deactivate", BlinkyEngineDeactivateEndpoint.Create())));
```

### Route wiring details:

The GenHTTP `Layout.Index()` method matches only the **exact root** of that layout (e.g., `/blinky/grids` but NOT `/blinky/grids/scan`). The `Layout.Add(handler)` without a path segment acts as a fallback handler.

**For `/blinky/grids`:**
- `Index(BlinkyListEndpoint.Create())` — handles `GET /blinky/grids` (list)
- `Add(BlinkyGridsEndpoint.Create())` — fallback handles `POST /blinky/grids` (build) and `DELETE /blinky/grids` (destroy)
- `Add("scan", ...)` — handles `POST /blinky/grids/scan`
- `Add("scan-all", ...)` — handles `POST /blinky/grids/scan-all`

**IMPORTANT:** If `.Index()` + `.Add()` fallback doesn't correctly split GET vs POST/DELETE on the same path, an alternative approach is to **merge all three methods into one handler file**:

### Alternative: Single combined endpoint

If the Index + fallback approach doesn't work, create a single `BlinkyGridsEndpoint` that handles GET, POST, and DELETE:

```csharp
// BlinkyGridsEndpoint.cs — handles GET/POST/DELETE /blinky/grids
return Inline.Create()
    .Serializers(Serialization.Default())
    .Get(async (string? vehicleId) => { /* existing list logic from BlinkyListEndpoint */ })
    .Post(async (BlinkyBuildGridRequest body) => { /* build logic */ })
    .Delete(async (string vehicleId, string gridName) => { /* destroy logic */ })
    .Build();
```

Then the route becomes:
```csharp
.Add("grids", Layout.Create()
    .Index(BlinkyGridsEndpoint.Create())           // GET/POST/DELETE /blinky/grids
    .Add("scan", BlinkyGridScanEndpoint.Create())  // POST /blinky/grids/scan
    .Add("scan-all", BlinkyGridScanAllEndpoint.Create()))  // POST /blinky/grids/scan-all
```

If this alternative is chosen, delete `BlinkyListEndpoint.cs` and move its GET logic into `BlinkyGridsEndpoint.cs`.

Similarly for `/blinky/animate`, merge the POST (scroll start) and DELETE (scroll stop) into `BlinkyAnimateEndpoint.cs` (already done in Task 6):
```csharp
.Add("animate", Layout.Create()
    .Index(BlinkyAnimateEndpoint.Create())                   // POST/DELETE /blinky/animate
    .Add("builtin", BlinkyBuiltInScrollEndpoint.Create()))   // POST /blinky/animate/builtin
```

---

## Task 11: Verify VehicleProvider.FindVehicle Helper

Multiple new endpoints need to find a vehicle by ID. Check if `VehicleProvider.FindVehicle(string vehicleId)` exists in `ksa-abstractions.lib`.

**File to check:** `ksa-abstractions.lib/VehicleProvider.cs`

If it does NOT exist, add it:

```csharp
/// <summary>Finds a vehicle by its ID string. Returns null if not found.</summary>
public static Vehicle? FindVehicle(string vehicleId)
{
    foreach (var v in GetAllVehicles())
    {
        if (v.Id == vehicleId)
            return v;
    }
    return null;
}
```

If `VehicleProvider` already has this, no changes needed. If the method exists under a different name, update the endpoint code to use that name instead.

---

## Task 12: Build & Verify Compilation

Run `dotnet build` from the solution root to verify everything compiles.

```
dotnet build ksa-mod-experiments.slnx
```

Fix any compilation errors. Common issues to watch for:
- Missing `using` statements for `MeowSci.BlinkyLib` namespace in new endpoint files
- Missing `using GenHTTP.Api.Content` namespace (for `IHandlerBuilder` return type)
- `IHandlerBuilder` vs `IHandler` return type — GenHTTP Inline `.Build()` returns `IHandler`, but `.Create()` returns `InlineBuilder` which implements `IHandlerBuilder`. The endpoint pattern returns `IHandlerBuilder` from the `Create()` static method, NOT calling `.Build()` at the call site. Check existing endpoints for the exact return pattern.
- Namespace for `ProviderException` and `ResponseStatus` — `GenHTTP.Api.Protocol`

---

## Task 13: Update README and REPOSITORY_INDEX

### 13a. Update `unladen-swallow.lib/README.md`

Add the new endpoints to the endpoint listing. Add a "Blinky Grid Management" section and "Blinky Settings" section alongside the existing "Blinky Display Control" section.

### 13b. Update `REPOSITORY_INDEX.md`

Ensure the unladen-swallow entry mentions the expanded blinky API surface (13 endpoints, grid lifecycle management, render settings, engine control).

---

## File Summary

| File | Action | Task |
|------|--------|------|
| `unladen-swallow.lib/ApiTypes.cs` | MODIFY | Task 1 |
| `unladen-swallow.lib/BlinkyListEndpoint.cs` | MODIFY | Task 2 |
| `unladen-swallow.lib/BlinkyGridsEndpoint.cs` | CREATE | Task 3 |
| `unladen-swallow.lib/BlinkyGridScanEndpoint.cs` | CREATE | Task 4 |
| `unladen-swallow.lib/BlinkyGridScanAllEndpoint.cs` | CREATE | Task 5 |
| `unladen-swallow.lib/BlinkyAnimateEndpoint.cs` | MODIFY | Task 6 |
| `unladen-swallow.lib/BlinkyBuiltInScrollEndpoint.cs` | CREATE | Task 7 |
| `unladen-swallow.lib/BlinkyPatternEndpoint.cs` | CREATE | Task 8 |
| `unladen-swallow.lib/BlinkyRenderEndpoint.cs` | CREATE | Task 9a |
| `unladen-swallow.lib/BlinkyEngineDeactivateEndpoint.cs` | CREATE | Task 9b |
| `unladen-swallow.lib/SwallowServer.cs` | MODIFY | Task 10 |
| `ksa-abstractions.lib/VehicleProvider.cs` | VERIFY/MODIFY | Task 11 |
| `unladen-swallow.lib/openapi/blinky.yml` | DONE | Already updated |
| `unladen-swallow.lib/README.md` | MODIFY | Task 13a |
| `REPOSITORY_INDEX.md` | MODIFY | Task 13b |

## Dependency Order

Tasks can be parallelized in groups:

1. **Group A (no dependencies):** Task 1 (DTOs), Task 11 (verify VehicleProvider)
2. **Group B (depends on Task 1):** Tasks 2–9 (all endpoint files)
3. **Group C (depends on Group B):** Task 10 (route wiring)
4. **Group D (depends on Group C):** Task 12 (build/verify)
5. **Group E (depends on Group D):** Task 13 (docs)
