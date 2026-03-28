# Unladen Swallow RPC Feature Plan

> Comprehensive plan for expanding the Unladen Swallow HTTP RPC server (`0.0.0.0:7887`) to expose game mod functionality for external shell programs and automation.

## Design Principles

- **Vehicle ID is always a string, matched case-insensitively** — all endpoints accepting a `vehicleId` must use `StringComparison.OrdinalIgnoreCase` when resolving the vehicle.
- **All game-state MUTATIONS MUST run on the game thread** via `GameThread.Scheduler.Schedule(...)`.
- **Simple game state READ operations with NOT mutations involved in the logic DO NOT need to be on the game thread**
- **Game state reads mixed with MUTATIONS MUST  all run on the game thread**
- **Endpoints call into `.lib` projects** — never manipulate game internals directly from endpoint code.
- **Standard response envelope**: `ApiResponse<T> { Status: "ok", Data: T }` for success; `ProviderException` for errors mapped to JSON by `JsonErrorMapper`.
- **OpenAPI specs are split per feature group** to keep each spec file manageable for AI tooling.

---

## Shared Infrastructure Changes

### Case-Insensitive Vehicle Lookup Helper

Before adding any new vehicle-targeted endpoints, extract a shared helper into `unladen-swallow.lib` that all endpoints use.

**File**: `unladen-swallow.lib/VehicleLookup.cs`

```csharp
public static class VehicleLookup
{
    public static Vehicle FindOrThrow(string vehicleId)
    {
        var vehicles = Universe.CurrentSystem?.Vehicles.GetList()
            ?? Enumerable.Empty<Vehicle>();
        var vehicle = vehicles.FirstOrDefault(v =>
            string.Equals(v.Id, vehicleId, StringComparison.OrdinalIgnoreCase));
        if (vehicle is null)
            throw new ProviderException(ResponseStatus.NotFound,
                $"Vehicle not found: {vehicleId}.");
        return vehicle;
    }
}
```

**Tasks:**
1. Create `VehicleLookup.cs` in `unladen-swallow.lib`
2. Refactor existing `ActionIgnite`, `ActionShutdown`, `BlinkyAnimateEndpoint`, `BlinkyStaticEndpoint`, `BlinkyOffEndpoint` to use `VehicleLookup.FindOrThrow()`
3. All new endpoints use this helper exclusively

### Project Reference Additions

`unladen-swallow.lib.csproj` currently references `ksa-abstractions.lib`, `glass.lib`, and `blinky.lib`. New feature groups will require adding:

- `eternal-flame.lib`
- `garys-torch.lib`
- `i-feel-seen.lib`
- `zippo.lib`
- `camera-controller-override.lib`
- `kitten-animations.lib`
- `kiwis-marbles.lib`
- `average-twr.lib`
- `geeforce.lib`
- `skittles.lib`
- `byo-music.lib`

Each reference should be added only when implementing its corresponding feature set. Also update `unladen-swallow.csproj` (the mod entry assembly) to include matching DLL copy targets in `CopyCustomContent`, and update `mod.toml` `ImportedAssemblies` entries so the shared `.lib` assembly instances are the same between `grant` and `unladen-swallow`.

---

## Feature Set 1: Core Vehicle & Game State Queries

**Route prefix**: `/vehicles` and `/game`
**OpenAPI spec file**: `openapi-core.yaml`
**Lib dependency**: `ksa-abstractions.lib` (already referenced)

These are the foundational read-only endpoints that every shell program will need.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /vehicles` | GET | List all vehicles in the current system |
| `GET /vehicles/{vehicleId}` | GET | Get detailed info about a specific vehicle |
| `GET /vehicles/{vehicleId}/parts` | GET | List all parts on a vehicle |
| `GET /vehicles/controlled` | GET | Get the currently player-controlled vehicle |
| `GET /game/time` | GET | Get elapsed simulation time |
| `GET /game/celestials` | GET | List all celestial bodies |

### Response Types (ApiTypes.cs)

```csharp
public record VehicleSummary(string Id, string DisplayName, string ParentBody, bool IsControlled, bool IsKittenEva);
public record VehicleListResponse(VehicleSummary[] Vehicles);
public record VehicleDetail(string Id, string DisplayName, string ParentBody, bool IsControlled,
    bool IsKittenEva, bool IsEditedVehicle, int PartCount, double TotalMass);
public record PartSummary(string Id, string DisplayName, bool IsSubPart, int ChildCount);
public record VehiclePartsResponse(string VehicleId, PartSummary[] Parts);
public record SimTimeResponse(double ElapsedSeconds);
public record CelestialSummary(string Id, double Mass, double MeanRadius);
public record CelestialListResponse(CelestialSummary[] Celestials);
```

### Implementation Tasks

1. Add response record types to `ApiTypes.cs`
2. Create `VehiclesEndpoint.cs` — handles `GET /vehicles` returning all vehicles as `VehicleSummary[]`
3. Create `VehicleDetailEndpoint.cs` — handles `GET /vehicles/{vehicleId}` with case-insensitive lookup
4. Create `VehiclePartsEndpoint.cs` — handles `GET /vehicles/{vehicleId}/parts` using `PartHelpers.GetAllParts()`
5. Create `ControlledVehicleEndpoint.cs` — handles `GET /vehicles/controlled` using `VehicleProvider.GetControlledVehicle()`
6. Create `GameTimeEndpoint.cs` — handles `GET /game/time` using `SimTimeProvider`
7. Create `CelestialsEndpoint.cs` — handles `GET /game/celestials` using `CelestialProvider.GetAllCelestials()`
8. Register all routes in `SwallowServer.RegisterRoutes()` under `vehicles` and `game` layouts
9. Write `openapi-core.yaml`

---

## Feature Set 2: Engine Control (Expanded)

**Route prefix**: `/vehicles/{vehicleId}/engines`
**OpenAPI spec file**: `openapi-engines.yaml`
**Lib dependency**: `ksa-abstractions.lib` (already referenced)

The existing `/vehicle/actions/ignite` and `/vehicle/actions/shutdown` endpoints work but use a flat namespace. This feature set expands engine control and migrates to the vehicle-scoped route pattern. The old routes remain as aliases for backwards compatibility.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `POST /vehicles/{vehicleId}/engines/ignite` | POST | Activate all engines on vehicle |
| `POST /vehicles/{vehicleId}/engines/shutdown` | POST | Deactivate all engines on vehicle |
| `GET /vehicles/{vehicleId}/engines` | GET | List all engines with status (active, thrust, fuel connection) |

### Response Types

```csharp
public record EngineSummary(string PartId, bool IsActive, float MinimumThrottle);
public record EngineListResponse(string VehicleId, EngineSummary[] Engines);
```

### Implementation Tasks

1. Add `EngineSummary`, `EngineListResponse` records to `ApiTypes.cs`
2. Create `EngineListEndpoint.cs` — iterate `vehicle.Parts.Modules.Get<EngineController>()` to list engines
3. Create `EngineIgniteEndpoint.cs` — calls `SetIsActive(null, true)` on all engines (reuse existing logic from `ActionIgnite`)
4. Create `EngineShutdownEndpoint.cs` — calls `SetIsActive(null, false)` on all engines
5. Register under `vehicles` → `{vehicleId}` → `engines` layout
6. Keep old `/vehicle/actions/ignite` and `/vehicle/actions/shutdown` routes as aliases
7. Write `openapi-engines.yaml`

---

## Feature Set 3: Fuel Management (Eternal Flame)

**Route prefix**: `/fuel`
**OpenAPI spec file**: `openapi-fuel.yaml`
**Lib dependency**: `eternal-flame.lib` (new reference)

Expose the infinite fuel system for programmatic control. Useful for automated testing and ensuring vehicles don't run out of fuel during scripted maneuvers.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /fuel/monitors` | GET | List all monitored vehicles with active status |
| `POST /fuel/monitors` | POST | Add a vehicle to the refill monitor list |
| `DELETE /fuel/monitors/{vehicleId}` | DELETE | Remove a vehicle from monitoring |
| `POST /fuel/monitors/{vehicleId}/toggle` | POST | Toggle active/inactive for a monitored vehicle |
| `GET /fuel/interval` | GET | Get current refill interval in ms |
| `POST /fuel/interval` | POST | Set refill interval (0–1000ms) |
| `POST /fuel/refill/{vehicleId}` | POST | Trigger an immediate one-shot refill of a vehicle's consumables |

### Request/Response Types

```csharp
public record FuelMonitor(string VehicleId, string DisplayName, bool Active);
public record FuelMonitorListResponse(FuelMonitor[] Monitors, int RefillIntervalMs);
public record FuelIntervalRequest(int IntervalMs);
public record FuelAddRequest(string VehicleId);
public record FuelToggleRequest(bool Active);
```

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `eternal-flame.lib`
2. **Expose `FuelManager` instance** — make `FuelManager` a static singleton (`FuelManager.Current`) set during `EternalFlameSubmod.Initialize()`. This is the preferred pattern for RPC access.
3. Add API types to `ApiTypes.cs`
4. Create `FuelMonitorsEndpoint.cs` — GET lists monitors via `FuelManager.Current.MonitoredVehicles`, POST adds vehicle
5. Create `FuelMonitorToggleEndpoint.cs` — POST toggles `MonitoredVehicle.Active`
6. Create `FuelIntervalEndpoint.cs` — GET/POST for `FuelManager.Current.RefillIntervalMs`
7. Create `FuelRefillEndpoint.cs` — POST triggers `vehicle.RefillConsumables()` once immediately on any vehicle
8. Register routes in `SwallowServer.RegisterRoutes()`
9. Write `openapi-fuel.yaml`

### Design Considerations

- The `FuelManager` is currently an instance field in `EternalFlameSubmod`. For RPC access, the best approach is a static `Current` property set during initialization.
- The one-shot refill endpoint (`POST /fuel/refill/{vehicleId}`) is independent of the monitor list — it just calls `RefillConsumables()` once on any vehicle by ID.
- Interval is clamped to 0–1000ms server-side.

---

## Feature Set 4: Vehicle Welding (Gary's Torch)

**Route prefix**: `/welds`
**OpenAPI spec file**: `openapi-welds.yaml`
**Lib dependency**: `garys-torch.lib` (new reference)

Expose the vehicle-to-vehicle welding system for scripted camera rigs, vehicle assemblies, and formation flying.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /welds` | GET | List all active vehicle welds |
| `POST /welds` | POST | Create a new weld between two vehicles |
| `DELETE /welds/{index}` | DELETE | Remove a weld by index (restores scale to 1.0) |
| `PATCH /welds/{index}` | PATCH | Update weld parameters (position, rotation, scale, lockRotation) |
| `GET /welds/presets` | GET | List available weld presets with their parameters |
| `POST /welds/from-preset` | POST | Create a weld using a named preset |

### Request/Response Types

```csharp
public record WeldCreateRequest(string SourceVehicleId, string TargetVehicleId,
    Float3Dto Position, Float3Dto Rotation, float Scale, bool LockRotation);
public record WeldUpdateRequest(Float3Dto? Position, Float3Dto? Rotation,
    float? Scale, bool? LockRotation);
public record WeldPresetCreateRequest(string SourceVehicleId, string TargetVehicleId, string PresetName);
public record Float3Dto(float X, float Y, float Z);
public record WeldInfo(int Index, string SourceVehicleId, string TargetVehicleId,
    Float3Dto Position, Float3Dto Rotation, float Scale, bool LockRotation);
public record WeldListResponse(WeldInfo[] Welds);
public record WeldPresetInfo(string Name, Float3Dto Position, Float3Dto Rotation,
    float Scale, bool LockRotation);
public record WeldPresetListResponse(WeldPresetInfo[] Presets);
```

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `garys-torch.lib`
2. **Expose weld management API** in `garys-torch.lib`:
   - Make `GarysTorchSubmod` expose its `_welds` list via a public read-only property
   - Extract `InitiateWeld()` and `RemoveWeld()` into public methods (or a separate `WeldManager` class)
   - Add an `UpdateWeldParams(int index, ...)` method for PATCH support
3. Add API types to `ApiTypes.cs`
4. Create `WeldsEndpoint.cs` — GET lists welds, POST creates new weld
5. Create `WeldDetailEndpoint.cs` — DELETE removes weld, PATCH updates parameters
6. Create `WeldPresetsEndpoint.cs` — GET lists presets, POST creates weld from preset
7. Register routes in `SwallowServer.RegisterRoutes()`
8. Write `openapi-welds.yaml`

### Design Considerations

- Weld creation requires both source and target vehicles to share the same `Parent` celestial body. Validate server-side, return 400 if mismatched.
- Scale range clamped to 0.05–20.0 as enforced in the UI.
- Topological sort is automatically handled when welds are added/removed.
- Source cannot already be welded as a source in another weld — return 409 Conflict.

---

## Feature Set 5: Render Visibility (I Feel Seen)

**Route prefix**: `/visibility`
**OpenAPI spec file**: `openapi-visibility.yaml`
**Lib dependency**: `i-feel-seen.lib` (new reference)

Expose render distance override for programmatic control of which vehicles are always visible regardless of camera distance.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /visibility` | GET | List all tracked vehicles with visibility state |
| `POST /visibility` | POST | Add a vehicle to the visibility tracker |
| `DELETE /visibility/{vehicleId}` | DELETE | Remove a vehicle from tracking |
| `POST /visibility/{vehicleId}/toggle` | POST | Toggle `SeeMe` for a tracked vehicle |
| `POST /visibility/clear` | POST | Clear all tracked vehicles |

### Request/Response Types

```csharp
public record VisibilityAddRequest(string VehicleId);
public record VisibilityToggleRequest(bool SeeMe);
public record TrackedVehicleInfo(string VehicleId, bool SeeMe);
public record VisibilityListResponse(TrackedVehicleInfo[] Vehicles);
```

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `i-feel-seen.lib`
2. **Expose `VehicleTracker`** — `IFeelSeenSubmod` already has a public `Tracker` property. Add a static `Instance` accessor on the submod or tracker for RPC access.
3. Add API types to `ApiTypes.cs`
4. Create `VisibilityEndpoint.cs` — GET lists tracked vehicles, POST adds vehicle
5. Create `VisibilityToggleEndpoint.cs` — POST toggles SeeMe, DELETE removes
6. Create `VisibilityClearEndpoint.cs` — POST clears all
7. Register routes in `SwallowServer.RegisterRoutes()`
8. Write `openapi-visibility.yaml`

---

## Feature Set 6: Light Control (Zippo)

**Route prefix**: `/lights`
**OpenAPI spec file**: `openapi-lights.yaml`
**Lib dependency**: `zippo.lib` (new reference)

Expose vehicle light control for scripted light shows, signaling, and atmosphere.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /lights/{vehicleId}` | GET | List all light-equipped parts on a vehicle with current intensity/color |
| `POST /lights/{vehicleId}/intensity` | POST | Set intensity for all lights on a vehicle (0.0–1.0) |
| `POST /lights/{vehicleId}/color` | POST | Set color for all lights (RGB or preset name) |
| `POST /lights/{vehicleId}/part/{partId}/intensity` | POST | Set intensity for a specific light part |
| `POST /lights/{vehicleId}/part/{partId}/color` | POST | Set color for a specific light part |
| `GET /lights/presets` | GET | List available color presets with RGB values |

### Request/Response Types

```csharp
public record LightIntensityRequest(float Intensity);
public record LightColorRequest(float? R, float? G, float? B, string? PresetName);
public record LightPartInfo(string PartId, string DisplayName, float Intensity, Float3Dto Color);
public record LightListResponse(string VehicleId, LightPartInfo[] Lights);
public record LightPresetInfo(string Name, Float3Dto Color);
public record LightPresetListResponse(LightPresetInfo[] Presets);
```

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `zippo.lib`
2. Add API types to `ApiTypes.cs`
3. Create `LightListEndpoint.cs` — GET uses `LightController.GetLightParts(vehicle)` then reads intensity/color per part
4. Create `LightIntensityEndpoint.cs` — POST applies `LightController.ApplyIntensity()` to all or specific part
5. Create `LightColorEndpoint.cs` — POST applies `LightController.ApplyColor()` with RGB or resolved preset
6. Create `LightPresetsEndpoint.cs` — GET returns `LightController.ColorPresetNames` with RGB values
7. Register routes in `SwallowServer.RegisterRoutes()`
8. Validate: intensity clamped to 0.0–1.0, RGB values clamped to 0.0–1.0
9. Write `openapi-lights.yaml`

### Design Considerations

- Color can be set via direct RGB (`r`, `g`, `b` fields) or by preset name (`presetName`). If both are provided, preset takes precedence.
- `LightController` is fully static — no singleton access pattern needed.
- Light discovery uses reflection internally; wrap in try-catch.

---

## Feature Set 7: Camera FOV Control (Glass) — Expanded

**Route prefix**: `/camera/fov` (migrate from current `/fov`)
**OpenAPI spec file**: `openapi-camera.yaml` (shared with Feature Set 8)
**Lib dependency**: `glass.lib` (already referenced)

Expand the existing FOV endpoints with preset support and migrate to a cleaner route namespace.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /camera/fov` | GET | Get current FOV state (existing, remapped) |
| `POST /camera/fov` | POST | Set FOV override (existing, remapped) |
| `POST /camera/fov/preset` | POST | Apply a named lens preset |
| `GET /camera/fov/presets` | GET | List all lens presets with FOV values |
| `POST /camera/fov/reset` | POST | Reset to game default (50°) and disable override |

Keep old `/fov` route as an alias for backwards compatibility.

### Request/Response Types

```csharp
public record FovPresetRequest(string PresetName);
public record FovPresetInfo(string Name, float FovDegrees);
public record FovPresetListResponse(FovPresetInfo[] Presets);
// Existing: FovRequest, FovState
```

### Lens Presets

| Name | FOV |
|------|-----|
| SuperTelephoto | 15° |
| Telephoto | 20° |
| Portrait | 30° |
| Standard | 50° |
| WideAngle | 75° |
| UltraWide | 100° |
| Fisheye | 120° |

### Implementation Tasks

1. Add preset-related records to `ApiTypes.cs`
2. Create `FovPresetsEndpoint.cs` — GET lists presets, POST applies preset by name
3. Create `FovResetEndpoint.cs` — POST calls `FovController.ResetToDefault()` then `DisableOverride()`
4. Extract preset data from `GlassSubmod` into `glass.lib` as a shared constant (e.g., `FovPresets.All`)
5. Register new routes under `camera/fov` layout
6. Keep `/fov` as alias pointing to same handler
7. Update `openapi-camera.yaml`

---

## Feature Set 8: Camera Animations (Camera Controller Override)

**Route prefix**: `/camera/sequence`
**OpenAPI spec file**: `openapi-camera.yaml` (shared with Feature Set 7)
**Lib dependency**: `camera-controller-override.lib` (new reference)

Expose the keyframe sequence player for scripted cinematics. This is one of the most powerful RPC features — enabling external tools to build and play camera animations.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /camera/sequence` | GET | Get current sequence state (playing/paused/stopped, keyframe count, elapsed time) |
| `POST /camera/sequence/play` | POST | Start playback |
| `POST /camera/sequence/pause` | POST | Pause playback |
| `POST /camera/sequence/resume` | POST | Resume from pause |
| `POST /camera/sequence/stop` | POST | Stop and reset |
| `POST /camera/sequence/clear` | POST | Clear all keyframes |
| `GET /camera/sequence/keyframes` | GET | List all keyframes with parameters |
| `DELETE /camera/sequence/keyframes/{index}` | DELETE | Remove keyframe at index |
| `POST /camera/sequence/keyframes/move` | POST | Reorder a keyframe |
| `POST /camera/sequence/keyframes/add` | POST | Add an animation keyframe |
| `POST /camera/sequence/return-to-start` | POST | Configure return-to-start behavior |

### Animation Types for `/camera/sequence/keyframes/add`

The `type` field selects the animation and determines which parameters are required:

| Type | Required Parameters | Optional |
|------|-------------------|----------|
| `zoom-out` | `speed`, `duration` | `easing`, `easingPowerStart`, `easingPowerEnd` |
| `zoom-in` | `speed`, `duration` | `easing`, `easingPowerStart`, `easingPowerEnd` |
| `zoom-in-to-offset` | `speed`, `duration`, `offsetX`, `offsetY`, `offsetZ` | `easing`, `easingPowerStart`, `easingPowerEnd` |
| `orbit` | `degrees`, `duration` | `easing`, `easingPowerStart`, `easingPowerEnd` |
| `spiral-zoom-in` | `speed`, `duration`, `spiralDegrees` | `easing`, `easingPowerStart`, `easingPowerEnd` |
| `spiral-zoom-out` | `speed`, `duration`, `spiralDegrees` | `easing`, `easingPowerStart`, `easingPowerEnd` |
| `loopy-orbit` | `degrees`, `loopInterval`, `amplitude`, `duration` | `easing`, `easingPowerStart`, `easingPowerEnd` |
| `shake` | `duration`, `count`, `amplitude`, `speed` | `easing`, `easingPowerStart`, `easingPowerEnd` |

### Request/Response Types

```csharp
public record SequenceStatusResponse(string State, int KeyframeCount, double ElapsedTime,
    double TotalDuration, int CurrentKeyframeIndex, bool ReturnToStartEnabled);
public record KeyframeInfo(int Index, string AnimationType, double Duration, string Easing,
    Dictionary<string, string> Properties);
public record KeyframeListResponse(KeyframeInfo[] Keyframes);
public record AddKeyframeRequest(string Type, float? Speed, float? Duration, float? Degrees,
    float? SpiralDegrees, float? OffsetX, float? OffsetY, float? OffsetZ,
    float? LoopInterval, float? Amplitude, int? Count,
    string? Easing, float? EasingPowerStart, float? EasingPowerEnd);
public record MoveKeyframeRequest(int FromIndex, int ToIndex);
public record ReturnToStartRequest(bool Enabled, float? Duration, string? Easing,
    float? EasingPowerStart, float? EasingPowerEnd);
```

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `camera-controller-override.lib`
2. **Access the SequencePlayer** — `CameraControllerOverridePatches.SequencePlayer` is already a public static property. Use this directly in endpoints.
3. Add API types to `ApiTypes.cs`
4. Create `SequenceStatusEndpoint.cs` — GET returns player state
5. Create `SequenceControlEndpoint.cs` — POST play/pause/resume/stop/clear
6. Create `SequenceKeyframesEndpoint.cs` — GET lists keyframes, DELETE removes, POST moves
7. Create `SequenceAddKeyframeEndpoint.cs` — POST creates the appropriate `IKeyframeAnimation` based on `type` field and adds it to the player
8. Create a factory method `AnimationFactory.Create(AddKeyframeRequest)` that maps the `type` string to the correct animation constructor
9. Create `SequenceReturnToStartEndpoint.cs` — POST configures return-to-start
10. Register all routes under `camera/sequence` layout
11. Validate easing names map to `EasingType` enum via `Enum.TryParse`
12. Update `openapi-camera.yaml`

### Design Considerations

- The sequence player is the same static instance used by the ImGui UI. RPC and UI can coexist but may interfere if both try to control playback simultaneously — document this.
- Easing type is a string in the API (`"Linear"`, `"EaseIn"`, `"EaseOut"`, `"EaseInOut"`) — parse with `Enum.TryParse<EasingType>()`.
- Default easing powers: `easingPowerStart = 3.0`, `easingPowerEnd = 3.0` if not specified.
- Use `GetDisplayProperties()` on each animation to populate the `KeyframeInfo.Properties` dictionary for the keyframe listing endpoint.

---

## Feature Set 9: LCD Pixel Display (Blinky) — Expanded

**Route prefix**: `/blinky` (existing)
**OpenAPI spec file**: `openapi-blinky.yaml`
**Lib dependency**: `blinky.lib` (already referenced)

Expand the existing blinky endpoints with grid management, pattern presets, and grid building/destruction.

### New Endpoints (in addition to existing animate/static/off)

| Route | Method | Description |
|-------|--------|-------------|
| `GET /blinky/grids` | GET | List all registered vehicle grids with dimensions and status |
| `GET /blinky/grids/{vehicleId}` | GET | Get detailed grid info (dimensions, active pixels, scroll status) |
| `POST /blinky/pattern` | POST | Apply a preset pattern (allOn, checkerboard, alternatingRows, alternatingCols) |
| `POST /blinky/build` | POST | Build a new pixel grid on a vehicle |
| `POST /blinky/destroy` | POST | Destroy/remove a pixel grid from a vehicle |
| `POST /blinky/scroll-speed` | POST | Update scroll speed on a running animation |

### Request/Response Types

```csharp
public record BlinkyGridInfo(string VehicleId, int Rows, int Cols, bool IsScrolling, float ScrollSpeed,
    int ActivePixelCount);
public record BlinkyGridListResponse(BlinkyGridInfo[] Grids);
public record BlinkyPatternRequest(string VehicleId, string Pattern);
public record BlinkyBuildRequest(string VehicleId, string Layout, int Width, int Height,
    float Spacing, float OffsetX, float OffsetY, float OffsetZ,
    string? EnginePartId, double? PartScale);
public record BlinkyDestroyRequest(string VehicleId);
public record BlinkyScrollSpeedRequest(string VehicleId, float Speed);
```

### Implementation Tasks

1. Add new API types to `ApiTypes.cs`
2. Create `BlinkyGridsEndpoint.cs` — GET lists all grids from `BlinkyGridManager.Grids`
3. Create `BlinkyGridDetailEndpoint.cs` — GET returns detailed state for a specific vehicle grid
4. Create `BlinkyPatternEndpoint.cs` — POST applies `BlinkyGridManager.ApplyPattern()` with the appropriate `PixelPatterns` predicate
5. Create `BlinkyBuildEndpoint.cs` — POST calls `LcdGridBuilder.BuildGrid()` with config from request, then `BlinkyGridManager.Register()`
6. Create `BlinkyDestroyEndpoint.cs` — POST calls `BlinkyGridManager.Unregister()` then `LcdGridBuilder.DestroyGrid()`
7. Create `BlinkyScrollSpeedEndpoint.cs` — POST updates `GridState.Scroll.ScrollSpeed`
8. Register new routes alongside existing blinky routes
9. Write `openapi-blinky.yaml`

### Design Considerations

- Grid building is a heavy operation (creates W×H×2 parts). Document that this is expensive and may cause a frame spike.
- Pattern names: `"allOn"`, `"checkerboard"`, `"alternatingRows"`, `"alternatingCols"` — mapped to `PixelPatterns` static methods.
- Grid build uses `LcdGridConfig` defaults for any optional fields omitted from the request. The `Layout` field accepts `"flat"` or `"cylinder"`.

---

## Feature Set 10: Kitten Avatar Animations

**Route prefix**: `/kitten`
**OpenAPI spec file**: `openapi-kitten.yaml`
**Lib dependency**: `kitten-animations.lib` (new reference)

Expose kitten avatar animation control for scripted machinima, reactions, and choreography.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /kitten/status` | GET | Get current avatar status (is EVA active, current expression) |
| `POST /kitten/expression` | POST | Trigger a facial expression |
| `POST /kitten/animation` | POST | Play a body/MMU animation |
| `POST /kitten/walk` | POST | Play a walking animation (walk or run) |
| `GET /kitten/expressions` | GET | List available expression types |
| `GET /kitten/animations` | GET | List available body and walking animations |

### Request/Response Types

```csharp
public record KittenExpressionRequest(string Expression, float? Duration);
public record KittenAnimationRequest(string Animation);
public record KittenWalkRequest(string Animation);
public record KittenStatusResponse(bool IsEvaActive, string? ActiveExpression, float ExpressionDuration);
public record KittenExpressionsResponse(string[] Expressions);
public record KittenAnimationsResponse(string[] BodyAnimations, string[] WalkAnimations);
```

### Available Animations

**Body Animations**: `idle`, `moveLeft`, `moveRight`, `moveForward`, `moveBackward`, `moveUp`, `moveDown`

**Expressions**: `angry`, `awe`, `happy`, `sad`, `scared`

**Walking**: `walk`, `run`

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `kitten-animations.lib`
2. **Expose programmatic API** — `KittenAnimationController` needs convenience methods callable without the ImGui UI:
   - `TriggerExpression()` is already public
   - `PlayAvatarAnimation()` is already public static
   - Add a `PlayBodyAnimation(string name, CharacterAvatar avatar)` method that maps string names to animation objects
   - Add a `PlayWalkAnimation(string name, CharacterAvatar avatar)` method
   - Add static `Instance` accessor on `KittenAnimationsSubmod` or controller
3. Add API types to `ApiTypes.cs`
4. Create `KittenStatusEndpoint.cs` — GET checks if EVA is active via `KittenAvatarAccessor.GetKitten()`
5. Create `KittenExpressionEndpoint.cs` — POST triggers expression with optional duration
6. Create `KittenAnimationEndpoint.cs` — POST plays body animation by name
7. Create `KittenWalkEndpoint.cs` — POST plays walking animation by name
8. Create `KittenListEndpoint.cs` — GET lists available expressions and animations
9. Register routes under `kitten` layout
10. Write `openapi-kitten.yaml`

### Design Considerations

- These endpoints operate on the currently controlled KittenEva vehicle. If the player is not on EVA, return 409 Conflict with a descriptive message.
- Expression duration defaults to the controller's current `ExpressionDuration` if not specified in the request (configurable 1.0–5.0s).
- Avatar access uses reflection internally — wrap all calls in try-catch.

---

## Feature Set 11: Celestial Welding (Kiwi's Marbles)

**Route prefix**: `/celestial-welds`
**OpenAPI spec file**: `openapi-celestial-welds.yaml`
**Lib dependency**: `kiwis-marbles.lib` (new reference)

Expose celestial body repositioning for scripted astronomical events, custom solar systems, and visual effects.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /celestial-welds` | GET | List all active celestial welds |
| `POST /celestial-welds` | POST | Create a new celestial weld |
| `DELETE /celestial-welds/{index}` | DELETE | Remove a weld (restores original orbit) |
| `PATCH /celestial-welds/{index}` | PATCH | Update weld offset |

### Request/Response Types

```csharp
public record CelestialWeldCreateRequest(string SourceCelestialId, string TargetOrbiterId,
    Double3Dto Offset, string? Units);
public record CelestialWeldUpdateRequest(Double3Dto? Offset, string? Units);
public record Double3Dto(double X, double Y, double Z);
public record CelestialWeldInfo(int Index, string SourceId, string TargetId,
    Double3Dto OffsetMeters);
public record CelestialWeldListResponse(CelestialWeldInfo[] Welds);
```

### Unit Scale Options

| Unit String | Multiplier |
|-------------|-----------|
| `"m"` | 1.0 |
| `"km"` | 1,000 |
| `"Mm"` | 1,000,000 |
| `"Gm"` | 1,000,000,000 |

Default is `"km"` if not specified.

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `kiwis-marbles.lib`
2. **Expose weld management API** in `kiwis-marbles.lib`:
   - `KiwisMarblesSubmod` needs public methods for create/remove/update welds
   - Extract a `CelestialWeldManager` or add public methods + static `Instance` on the submod
   - The `_welds` list and `InitiateWeld()`/`RemoveWeld()` methods need to be accessible
3. Add API types to `ApiTypes.cs`
4. Create `CelestialWeldsEndpoint.cs` — GET lists welds, POST creates
5. Create `CelestialWeldDetailEndpoint.cs` — DELETE removes (restores orbit), PATCH updates offset
6. Validate source is a known celestial, target is a known orbiter
7. Convert offset using unit scale multiplier before storing (always stored in meters internally)
8. Register routes under `celestial-welds` layout
9. Write `openapi-celestial-welds.yaml`

### Design Considerations

- Celestial welding modifies actual orbital state — restoring the original orbit on DELETE is critical.
- Topological sort is automatically handled when welds are added/removed.
- Source cannot already be welded as a source in another weld — return 409 Conflict.

---

## Feature Set 12: Telemetry — TWR & G-Force

**Route prefix**: `/telemetry`
**OpenAPI spec file**: `openapi-telemetry.yaml`
**Lib dependencies**: `average-twr.lib` (new reference), `geeforce.lib` (new reference)

Expose real-time flight telemetry for dashboards, logging, and automated monitoring.

### TWR Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /telemetry/twr` | GET | Get current TWR and max acceleration for controlled vehicle |
| `GET /telemetry/twr/stats` | GET | Get accumulated TWR statistics (mean, stddev, etc.) |
| `POST /telemetry/twr/reset` | POST | Reset the TWR sample accumulator |

### G-Force Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /telemetry/gforce` | GET | Get current g-force reading (magnitude + per-axis) |
| `GET /telemetry/gforce/stats` | GET | Get peak, min, avg, max jerk, breach counts |
| `GET /telemetry/gforce/history` | GET | Get recent g-force sample history (optional time range query params) |
| `POST /telemetry/gforce/clear` | POST | Clear the g-force recorder buffer |

### Request/Response Types

```csharp
// TWR
public record TwrCurrentResponse(double Twr, double MaxAcceleration, double SurfaceGravity);
public record TwrStatsResponse(int SampleCount, double MeanTwr, double StdDevTwr,
    double HarmonicMeanTwr, double MeanAccel);

// G-Force
public record GForceSampleDto(double TimeSec, double Magnitude, double X, double Y, double Z, double Jerk);
public record GForceCurrentResponse(GForceSampleDto Current);
public record GForceStatsResponse(double PeakG, double MinG, double AvgG, double MaxJerk,
    int KillGeesBreaches, int JerkBreaches, int SampleCount);
public record GForceHistoryResponse(GForceSampleDto[] Samples, int TotalAvailable);
```

### Implementation Tasks

1. Add project references: `unladen-swallow.lib` → `average-twr.lib`, `geeforce.lib`
2. **Expose TWR data** — `AverageTwrSubmod` needs to expose its `TwrSampleAccumulator` via static accessor
3. **Expose G-Force data** — `GeeForceSubmod` needs to expose its `GForceRecorder` via static accessor. The recorder's indexer and stat properties are already public.
4. Add API types to `ApiTypes.cs`
5. Create `TwrEndpoint.cs` — GET reads current TWR via `TwrDataReader.ReadTwr()` and `ComputeMaxAcceleration()`
6. Create `TwrStatsEndpoint.cs` — GET computes statistics from the accumulator using `TwrStatistics`
7. Create `TwrResetEndpoint.cs` — POST calls `accumulator.Reset()`
8. Create `GForceCurrentEndpoint.cs` — GET returns `recorder.Latest`
9. Create `GForceStatsEndpoint.cs` — GET returns stats properties from recorder
10. Create `GForceHistoryEndpoint.cs` — GET returns samples from the ring buffer, optionally filtered by time range query parameters; default last 100 samples
11. Create `GForceClearEndpoint.cs` — POST calls `recorder.Clear()`
12. Register all under `telemetry` layout with `twr` and `gforce` sub-layouts
13. Write `openapi-telemetry.yaml`

### Design Considerations

- TWR and g-force readings are for the **currently controlled vehicle**. These are not vehicle-ID-scoped because the recorders track the controlled vehicle automatically.
- G-force history can be large (40 Hz × 1 hour = 144,000 samples). Cap the response with a `maxSamples` query parameter. Default to last 100 samples if no range specified.
- Consider adding a `?vehicleId=` query parameter to TWR for reading TWR of any vehicle (not just controlled).

---

## Feature Set 13: Theme Management (Skittles)

**Route prefix**: `/themes`
**OpenAPI spec file**: `openapi-themes.yaml`
**Lib dependency**: `skittles.lib` (new reference)

Expose theme management for remote UI customization and theme distribution.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `GET /themes` | GET | List all available themes (built-in and custom) |
| `GET /themes/active` | GET | Get the currently active theme name |
| `POST /themes/apply` | POST | Apply a theme by name |
| `POST /themes/restore-default` | POST | Restore game default theme |
| `POST /themes/refresh` | POST | Rescan disk for custom theme files |

### Request/Response Types

```csharp
public record ThemeApplyRequest(string ThemeName);
public record ThemeListEntry(string Name, bool IsBuiltIn);
public record ThemeListResponse(ThemeListEntry[] Themes, string ActiveThemeName);
public record ActiveThemeResponse(string ThemeName);
```

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `skittles.lib`
2. **Expose ThemeManager** — `SkittlesSubmod` manages a `ThemeManager` instance. Add static `Instance` accessor.
3. Add API types to `ApiTypes.cs`
4. Create `ThemesEndpoint.cs` — GET lists themes from `ThemeManager.AvailableThemes`, POST applies theme
5. Create `ActiveThemeEndpoint.cs` — GET returns `ThemeManager.ActiveThemeName`
6. Create `ThemeRestoreEndpoint.cs` — POST calls `ThemeManager.RestoreDefaults()`
7. Create `ThemeRefreshEndpoint.cs` — POST calls `ThemeManager.RefreshThemeList()`
8. Register routes under `themes` layout
9. Write `openapi-themes.yaml`

### Design Considerations

- Theme application affects the global ImGui style. This is visual-only and has no gameplay impact.
- Theme names are case-sensitive (they map to filenames on disk).
- Built-in themes: Game Default, Dark, Light, Classic, Inanimate Carbon Rod.

---

## Feature Set 14: Music Control (BYO Music)

**Route prefix**: `/music`
**OpenAPI spec file**: `openapi-music.yaml`
**Lib dependency**: `byo-music.lib` (new reference)

Expose music playback control for scripted events and atmosphere.

### Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `POST /music/play` | POST | Play a playlist by asset ID |
| `POST /music/stop` | POST | Stop current music playback |

### Request/Response Types

```csharp
public record MusicPlayRequest(string AssetId);
public record MusicPlayResponse(string AssetId, string Action);
```

### Implementation Tasks

1. Add project reference: `unladen-swallow.lib` → `byo-music.lib`
2. Add API types to `ApiTypes.cs`
3. Create `MusicEndpoint.cs` — POST play calls `MusicPlayer.Play(MusicPlayer.GetPlaylist(assetId))`
4. Create `MusicStopEndpoint.cs` — POST stop (investigate if `MusicPlayList` or `ChannelWrapper` has a stop method; if not, extend the `MusicPlayer` facade to capture and manage the `ChannelWrapper`)
5. Register routes under `music` layout
6. Write `openapi-music.yaml`

### Design Considerations

- The current `MusicPlayer` facade is minimal (play only). Stop/pause may require extending the wrapper to capture the `ChannelWrapper` returned by `PlayMusic()`.
- Available playlist asset IDs depend on what's defined in `Assets.xml`. Return 404 if the asset ID isn't found.

---

## OpenAPI Specification Strategy

### File Organization

Each feature group gets its own OpenAPI spec file, stored in `unladen-swallow.lib/openapi/`:

| Spec File | Feature Sets Covered |
|-----------|---------------------|
| `openapi-core.yaml` | Vehicle queries, game state, health |
| `openapi-engines.yaml` | Engine ignite/shutdown/list |
| `openapi-fuel.yaml` | Fuel monitoring and refill |
| `openapi-welds.yaml` | Vehicle welding (Gary's Torch) |
| `openapi-visibility.yaml` | Render distance override |
| `openapi-lights.yaml` | Light control (Zippo) |
| `openapi-camera.yaml` | FOV control + camera animation sequences |
| `openapi-blinky.yaml` | LCD pixel display |
| `openapi-kitten.yaml` | Kitten avatar animations |
| `openapi-celestial-welds.yaml` | Celestial body welding |
| `openapi-telemetry.yaml` | TWR + G-force telemetry |
| `openapi-themes.yaml` | Theme management |
| `openapi-music.yaml` | Music playback |

### Spec Format

- OpenAPI 3.1.0
- Each spec is self-contained with its own `info`, `paths`, and `components/schemas`
- Common schemas (like `ApiResponse`, `Float3Dto`) defined in each spec that uses them (duplication preferred over cross-file `$ref` for AI manageability)
- Each spec includes example requests and responses

### Implementation Tasks

1. Create `unladen-swallow.lib/openapi/` directory
2. Write each spec file as the corresponding feature set is implemented
3. Each spec should be written alongside (or immediately after) the endpoint code

### Serving OpenAPI at Runtime (Optional Enhancement)

Add an endpoint that lists and serves the OpenAPI spec files at runtime:

| Route | Method | Description |
|-------|--------|-------------|
| `GET /openapi` | GET | List all available spec files |
| `GET /openapi/{specName}` | GET | Serve a specific OpenAPI spec as YAML |

This requires embedding the YAML files as resources in the assembly or reading them from a known disk location. Implement after the core feature sets are in place.

---

## Submod Instance Access Pattern

Many feature sets require accessing the `.lib` submod instances (e.g., `EternalFlameSubmod.FuelManager`, `GarysTorchSubmod._welds`). Since `grant` instantiates all submods and `unladen-swallow.lib` runs in the same process, we need a cross-submod access pattern.

### Recommended Approach: Static Singleton on Manager/Submod

For each `.lib` that needs RPC exposure, add a static `Current`/`Instance` property:

```csharp
// Option A: Singleton on manager class (preferred when manager is primary API)
public class FuelManager
{
    public static FuelManager? Current { get; private set; }
    public FuelManager() { Current = this; }
}

// Option B: Singleton on submod (when submod directly exposes API)
public class EternalFlameSubmod : ISubmod
{
    public static EternalFlameSubmod? Instance { get; private set; }
    public void Initialize() { Instance = this; }
}
```

**Use Option A** when the manager is the primary API surface.
**Use Option B** when the submod directly exposes the API.

Null-check the singleton in every endpoint and return 503 Service Unavailable if the submod isn't loaded yet.

---

## Implementation Priority Order

Suggested order based on utility, complexity, and dependencies:

1. **Shared Infrastructure** — `VehicleLookup.cs`, case-insensitive matching
2. **Feature Set 1: Core Vehicle Queries** — foundation for everything else
3. **Feature Set 2: Engine Control** — expands existing functionality
4. **Feature Set 7: Camera FOV** — expands existing `/fov`
5. **Feature Set 9: Blinky Expanded** — expands existing blinky endpoints
6. **Feature Set 12: Telemetry** — read-only, low risk
7. **Feature Set 6: Light Control** — straightforward static API
8. **Feature Set 3: Fuel Management** — needs singleton pattern
9. **Feature Set 5: Render Visibility** — needs singleton pattern
10. **Feature Set 8: Camera Animations** — already has static access via patches
11. **Feature Set 10: Kitten Animations** — reflection-heavy, needs careful error handling
12. **Feature Set 4: Vehicle Welding** — complex state management
13. **Feature Set 11: Celestial Welding** — high-impact orbital modifications
14. **Feature Set 13: Theme Management** — UI-only, low priority
15. **Feature Set 14: Music Control** — limited API surface currently

---

## Complete Route Tree

```
GET    /health
GET    /vehicles
GET    /vehicles/controlled
GET    /vehicles/{vehicleId}
GET    /vehicles/{vehicleId}/parts
GET    /vehicles/{vehicleId}/engines
POST   /vehicles/{vehicleId}/engines/ignite
POST   /vehicles/{vehicleId}/engines/shutdown
GET    /game/time
GET    /game/celestials
GET    /camera/fov
POST   /camera/fov
POST   /camera/fov/preset
GET    /camera/fov/presets
POST   /camera/fov/reset
GET    /camera/sequence
POST   /camera/sequence/play
POST   /camera/sequence/pause
POST   /camera/sequence/resume
POST   /camera/sequence/stop
POST   /camera/sequence/clear
GET    /camera/sequence/keyframes
POST   /camera/sequence/keyframes/add
POST   /camera/sequence/keyframes/move
DELETE /camera/sequence/keyframes/{index}
POST   /camera/sequence/return-to-start
GET    /fuel/monitors
POST   /fuel/monitors
DELETE /fuel/monitors/{vehicleId}
POST   /fuel/monitors/{vehicleId}/toggle
GET    /fuel/interval
POST   /fuel/interval
POST   /fuel/refill/{vehicleId}
GET    /welds
POST   /welds
DELETE /welds/{index}
PATCH  /welds/{index}
GET    /welds/presets
POST   /welds/from-preset
GET    /visibility
POST   /visibility
DELETE /visibility/{vehicleId}
POST   /visibility/{vehicleId}/toggle
POST   /visibility/clear
GET    /lights/{vehicleId}
POST   /lights/{vehicleId}/intensity
POST   /lights/{vehicleId}/color
POST   /lights/{vehicleId}/part/{partId}/intensity
POST   /lights/{vehicleId}/part/{partId}/color
GET    /lights/presets
GET    /blinky/grids
GET    /blinky/grids/{vehicleId}
POST   /blinky/animate                    (existing)
POST   /blinky/static                     (existing)
POST   /blinky/off                        (existing)
POST   /blinky/pattern
POST   /blinky/build
POST   /blinky/destroy
POST   /blinky/scroll-speed
GET    /kitten/status
POST   /kitten/expression
POST   /kitten/animation
POST   /kitten/walk
GET    /kitten/expressions
GET    /kitten/animations
GET    /celestial-welds
POST   /celestial-welds
DELETE /celestial-welds/{index}
PATCH  /celestial-welds/{index}
GET    /telemetry/twr
GET    /telemetry/twr/stats
POST   /telemetry/twr/reset
GET    /telemetry/gforce
GET    /telemetry/gforce/stats
GET    /telemetry/gforce/history
POST   /telemetry/gforce/clear
GET    /themes
GET    /themes/active
POST   /themes/apply
POST   /themes/restore-default
POST   /themes/refresh
POST   /music/play
POST   /music/stop
GET    /openapi
GET    /openapi/{specName}
GET    /fov                               (legacy alias)
POST   /fov                               (legacy alias)
POST   /vehicle/actions/ignite            (legacy alias)
POST   /vehicle/actions/shutdown          (legacy alias)
```
