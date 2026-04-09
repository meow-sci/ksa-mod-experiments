# Garry's Torch HTTP RPC API — Implementation Plan

## Overview

Add HTTP RPC endpoints for the Garry's Torch vehicle welding system to the unladen-swallow server. This includes:

1. **Full CRUD API** for welds (create, read, modify, delete)
2. **Preset management API** (CRUD for named presets)
3. **New animation feature** in `garrys-torch.lib` for interpolating weld properties over time with easing
4. **Animated weld modification API** to transition weld state over a duration with configurable easing
5. **OpenAPI specification** at `unladen-swallow.lib/openapi/garrystorch.yml`

### Design Decisions (confirmed with user)

- **Weld identification**: By source vehicle ID (natural key — each source can only be welded once)
- **Animation scope**: Interpolate position, rotation, and scale (not lockRotation)
- **Concurrent animations**: Queue new animation after current one finishes
- **Animation status polling**: Not needed (fire and forget)
- **Static API pattern**: No separate static class needed — `GarrysTorchSubmod` already holds global state and is instantiated once inside the grant supermod. Unladen-swallow endpoint code can reference `garrys-torch.lib` types directly since they share the same assembly loader at runtime.

---

## Task 1: Add Public API Surface to `garrys-torch.lib/GarrysTorchSubmod.cs`

### Goal
Expose weld and preset operations as public methods so the RPC layer can call them. Currently all weld management is private.

### Changes to `garrys-torch.lib/GarrysTorchSubmod.cs`

#### 1a. Add static `Instance` property

Add at the top of the class, after the Name/Tooltip properties:

```csharp
/// <summary>Singleton instance set during Initialize, cleared on Dispose.</summary>
public static GarrysTorchSubmod? Instance { get; private set; }
```

In `Initialize()`, add as first line:
```csharp
Instance = this;
```

In `Dispose()`, add as last line:
```csharp
Instance = null;
```

#### 1b. Add public read-only access to welds

Add a public property exposing welds as read-only:

```csharp
/// <summary>Currently active welds (read-only view).</summary>
public IReadOnlyList<WeldEntry> Welds => _welds;
```

#### 1c. Add public `CreateWeld` method

Extract from the existing `InitiateWeld` private method. The new public method should:
- Accept `string sourceVehicleId`, `string targetVehicleId`, `float3 position`, `float3 rotation`, `float scale`, `bool lockRotation`
- Look up vehicles from `VehicleProvider.GetAllVehicles()`
- Validate source != target, source not already welded, both vehicles exist
- Return a result object indicating success/failure with error message
- On success, create the `WeldEntry`, add it, apply scale, sort, and return the created entry

Signature:
```csharp
public (WeldEntry? Weld, string? Error) CreateWeld(
    string sourceVehicleId, string targetVehicleId,
    float3 position, float3 rotation, float scale, bool lockRotation)
```

Implementation:
1. Get vehicles list from `VehicleProvider.GetAllVehicles()`
2. Find source/target by `Id` match
3. If source not found → return `(null, "Source vehicle not found: {id}")`
4. If target not found → return `(null, "Target vehicle not found: {id}")`
5. If source == target → return `(null, "Source and target must differ")`
6. If source already welded → return `(null, "Vehicle {id} is already welded as a source")`
7. Create `WeldEntry` with the provided values
8. Add to `_welds`, apply scale if != 1f, call `SortWelds()`
9. Log and return `(weldEntry, null)`

The existing private `InitiateWeld` method in the UI code should be refactored to call this new public method internally to avoid duplication.

#### 1d. Add public `FindWeld` method

```csharp
public WeldEntry? FindWeld(string sourceVehicleId)
{
    for (int i = 0; i < _welds.Count; i++)
        if (_welds[i].Source.Id == sourceVehicleId)
            return _welds[i];
    return null;
}
```

#### 1e. Add public `ModifyWeld` method (immediate)

For modifying an existing weld's properties immediately (same as what the ImGui UI does):

```csharp
public (WeldEntry? Weld, string? Error) ModifyWeld(
    string sourceVehicleId, float3? position, float3? rotation, float? scale, bool? lockRotation)
```

Implementation:
1. Find weld by source ID → return error if not found
2. If position provided, update `weld.Position`
3. If rotation provided, update `weld.Rotation`
4. If scale provided, update `weld.Scale` and call `WeldEngine.ApplyVehicleScale(weld.Source, scale.Value)`
5. If lockRotation provided, update `weld.LockRotation`
6. Return `(weld, null)`

#### 1f. Add public `RemoveWeld` method (by source ID)

```csharp
public bool RemoveWeld(string sourceVehicleId)
```

Implementation:
1. Find entry by source vehicle ID
2. If not found, return false
3. Call existing private `RemoveWeld(entry)` (which resets scale and removes)
4. Return true

Make the existing private `RemoveWeld(WeldEntry)` method stay private but ensure the new public method delegates to it.

#### 1g. Add public preset API methods

These are already mostly public on `PresetManager`, but the submod wraps them. Add pass-throughs:

```csharp
public string[] GetPresetNames() => _presetManager.GetPresetNames();
public WeldPreset? GetPreset(string name) => _presetManager.GetPreset(name);
public bool PresetExists(string name) => _presetManager.PresetExists(name);
public bool SavePreset(string name, WeldPreset preset) => _presetManager.SavePreset(name, preset);
public bool DeletePreset(string name) => _presetManager.DeletePreset(name);
```

---

## Task 2: Add Weld Animation System to `garrys-torch.lib`

### Goal
Add the ability to smoothly interpolate a weld's position, rotation, and scale from current values to target values over a specified duration using configurable easing functions.

### New File: `garrys-torch.lib/WeldAnimation.cs`

This file contains the easing type enum (local to garrys-torch — do NOT depend on camera-controller-override.lib) and the animation state class.

```csharp
using System;
using Brutal.Numerics;

namespace MeowSci.GarrysTorchLib;

/// <summary>Easing function types for weld animation interpolation.</summary>
public enum WeldEasingType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}

/// <summary>
/// Represents an active animation interpolating a weld's properties from start to target values.
/// Managed per-weld; at most one animation can be active per weld, with a queue of pending animations.
/// </summary>
public sealed class WeldAnimation
{
    // Start state (captured when animation begins)
    public float3 StartPosition { get; }
    public float3 StartRotation { get; }
    public float StartScale { get; }

    // Target state
    public float3 TargetPosition { get; }
    public float3 TargetRotation { get; }
    public float TargetScale { get; }

    // Timing
    public double DurationSeconds { get; }
    public double ElapsedSeconds { get; private set; }

    // Easing configuration
    public WeldEasingType Easing { get; }
    public double EasingPowerStart { get; }
    public double EasingPowerEnd { get; }

    /// <summary>True when the animation has finished.</summary>
    public bool IsComplete => ElapsedSeconds >= DurationSeconds;

    public WeldAnimation(
        float3 startPosition, float3 startRotation, float startScale,
        float3 targetPosition, float3 targetRotation, float targetScale,
        double durationSeconds, WeldEasingType easing,
        double easingPowerStart = 3.0, double easingPowerEnd = 3.0)
    {
        StartPosition = startPosition;
        StartRotation = startRotation;
        StartScale = startScale;
        TargetPosition = targetPosition;
        TargetRotation = targetRotation;
        TargetScale = targetScale;
        DurationSeconds = durationSeconds;
        Easing = easing;
        EasingPowerStart = easingPowerStart;
        EasingPowerEnd = easingPowerEnd;
    }

    /// <summary>
    /// Advances the animation by dt seconds and writes interpolated values to the weld entry.
    /// Returns true if the animation is still running, false if complete.
    /// </summary>
    public bool Update(WeldEntry weld, double dt)
    {
        ElapsedSeconds += dt;

        double t = Math.Clamp(ElapsedSeconds / DurationSeconds, 0.0, 1.0);
        double easedT = ApplyEasing(t, Easing, EasingPowerStart, EasingPowerEnd);

        float ft = (float)easedT;

        weld.Position = Lerp(StartPosition, TargetPosition, ft);
        weld.Rotation = Lerp(StartRotation, TargetRotation, ft);

        float newScale = StartScale + (TargetScale - StartScale) * ft;
        if (newScale != weld.Scale)
        {
            weld.Scale = newScale;
            WeldEngine.ApplyVehicleScale(weld.Source, newScale);
        }

        if (IsComplete)
        {
            // Snap to exact target values
            weld.Position = TargetPosition;
            weld.Rotation = TargetRotation;
            if (weld.Scale != TargetScale)
            {
                weld.Scale = TargetScale;
                WeldEngine.ApplyVehicleScale(weld.Source, TargetScale);
            }
            return false;
        }

        return true;
    }

    private static float3 Lerp(float3 a, float3 b, float t)
    {
        return new float3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
    }

    /// <summary>
    /// Applies easing to a normalized [0,1] time value.
    /// Same mathematical formulas as camera-controller-override's AnimationHelpers.ApplyEasing.
    /// </summary>
    internal static double ApplyEasing(double t, WeldEasingType easingType, double powerStart = 3.0, double powerEnd = 3.0)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return easingType switch
        {
            WeldEasingType.EaseIn => Math.Pow(t, powerStart),
            WeldEasingType.EaseOut => 1.0 - Math.Pow(1.0 - t, powerEnd),
            WeldEasingType.EaseInOut => t < 0.5
                ? Math.Pow(2 * t, powerStart) / 2.0
                : 1.0 - Math.Pow(2 * (1 - t), powerEnd) / 2.0,
            _ => t
        };
    }
}
```

**Key design notes:**
- Own `WeldEasingType` enum local to this library — does NOT import `camera-controller-override.lib`
- Same easing math as `AnimationHelpers.ApplyEasing` from camera-controller-override (duplicated intentionally to avoid cross-mod dependency)
- Lerp for position and rotation (Euler angles are just floats, lerp is appropriate for small-to-moderate angle changes)
- Scale is lerped linearly
- Snaps to exact target values when complete to avoid floating-point drift

### New File: `garrys-torch.lib/WeldAnimationManager.cs`

Manages active animations and queued animations per-weld.

```csharp
using System;
using System.Collections.Generic;

namespace MeowSci.GarrysTorchLib;

/// <summary>
/// Manages active and queued animations for welds.
/// Each weld can have at most one active animation and a queue of pending ones.
/// </summary>
public sealed class WeldAnimationManager
{
    private readonly Dictionary<WeldEntry, WeldAnimation> _active = new();
    private readonly Dictionary<WeldEntry, Queue<WeldAnimation>> _queues = new();

    /// <summary>Returns the currently active animation for a weld, or null.</summary>
    public WeldAnimation? GetActiveAnimation(WeldEntry weld)
    {
        return _active.TryGetValue(weld, out var anim) ? anim : null;
    }

    /// <summary>
    /// Enqueues an animation for a weld. If no animation is currently active, it starts immediately.
    /// </summary>
    public void Enqueue(WeldEntry weld, WeldAnimation animation)
    {
        if (!_active.ContainsKey(weld))
        {
            _active[weld] = animation;
            return;
        }

        if (!_queues.TryGetValue(weld, out var queue))
        {
            queue = new Queue<WeldAnimation>();
            _queues[weld] = queue;
        }
        queue.Enqueue(animation);
    }

    /// <summary>
    /// Updates all active animations. Called once per frame from GarrysTorchSubmod.Update().
    /// </summary>
    public void Update(double dt)
    {
        // Collect completed entries to avoid modifying during iteration
        List<WeldEntry>? completed = null;

        foreach (var (weld, anim) in _active)
        {
            bool stillRunning = anim.Update(weld, dt);
            if (!stillRunning)
            {
                completed ??= new List<WeldEntry>();
                completed.Add(weld);
            }
        }

        if (completed != null)
        {
            foreach (var weld in completed)
            {
                _active.Remove(weld);

                // Start next queued animation if any
                if (_queues.TryGetValue(weld, out var queue) && queue.Count > 0)
                {
                    // The next animation's start state should be captured from the weld's current values
                    // But animation was already created with start state at enqueue time.
                    // For queued animations, we need to reconstruct with current weld state.
                    var next = queue.Dequeue();
                    // Create a new animation with current weld state as start
                    var corrected = new WeldAnimation(
                        weld.Position, weld.Rotation, weld.Scale,
                        next.TargetPosition, next.TargetRotation, next.TargetScale,
                        next.DurationSeconds, next.Easing,
                        next.EasingPowerStart, next.EasingPowerEnd);
                    _active[weld] = corrected;

                    if (queue.Count == 0)
                        _queues.Remove(weld);
                }
            }
        }
    }

    /// <summary>Removes all animations for a given weld (called on unweld).</summary>
    public void CancelAll(WeldEntry weld)
    {
        _active.Remove(weld);
        _queues.Remove(weld);
    }

    /// <summary>Removes all animations for all welds.</summary>
    public void Clear()
    {
        _active.Clear();
        _queues.Clear();
    }
}
```

**Key design note on queued animations**: When an animation completes and the next queued animation starts, the queued animation's *start state is re-captured from the weld's current values* (which is the previous animation's target state). This ensures smooth chaining even if intermediate state was affected.

### Modify `garrys-torch.lib/GarrysTorchSubmod.cs` — Integrate Animation Manager

#### Add field
```csharp
private readonly WeldAnimationManager _animationManager = new();
```

#### Add public property
```csharp
public WeldAnimationManager AnimationManager => _animationManager;
```

#### Modify `Update(double dt)`

Add animation manager update **before** the weld engine update loop:

```csharp
public void Update(double dt)
{
    _animationManager.Update(dt);

    var toRemove = new List<WeldEntry>();
    foreach (var weld in _welds)
        if (!WeldEngine.UpdateWeld(weld)) toRemove.Add(weld);
    foreach (var weld in toRemove)
        RemoveWeld(weld);
}
```

The order matters: animations update weld properties first, then the weld engine uses those properties to teleport vehicles.

#### Modify private `RemoveWeld(WeldEntry)`

Add cleanup of animations when a weld is removed:

```csharp
private void RemoveWeld(WeldEntry entry)
{
    _animationManager.CancelAll(entry);
    WeldEngine.ApplyVehicleScale(entry.Source, 1.0f);
    Console.WriteLine($"garrys-torch: Unwelded {entry.Source.Id} from {entry.Target.Id}");
    _welds.Remove(entry);
}
```

#### Modify `Dispose()`

Clear animation manager on dispose:

```csharp
public void Dispose()
{
    _animationManager.Clear();
    foreach (var weld in _welds)
        WeldEngine.ApplyVehicleScale(weld.Source, 1.0f);
    _welds.Clear();
    Instance = null;
}
```

#### Add public `AnimateWeld` method

```csharp
public string? AnimateWeld(
    string sourceVehicleId,
    float3 targetPosition, float3 targetRotation, float targetScale,
    double durationSeconds, WeldEasingType easing,
    double easingPowerStart = 3.0, double easingPowerEnd = 3.0)
{
    var weld = FindWeld(sourceVehicleId);
    if (weld == null)
        return $"No active weld found with source: {sourceVehicleId}";

    if (durationSeconds <= 0)
        return "Duration must be greater than 0";

    var animation = new WeldAnimation(
        weld.Position, weld.Rotation, weld.Scale,
        targetPosition, targetRotation, targetScale,
        durationSeconds, easing, easingPowerStart, easingPowerEnd);

    _animationManager.Enqueue(weld, animation);
    return null; // success
}
```

---

## Task 3: Add DTO Types to `unladen-swallow.lib/ApiTypes.cs`

### Goal
Add all request/response record types for the Garry's Torch RPC endpoints.

### Append to `unladen-swallow.lib/ApiTypes.cs`

```csharp
// ── Garry's Torch — Easing ──────────────────────────────────────────────────

public enum TorchEasingType
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

// ── Garry's Torch — Core Data Models ────────────────────────────────────────

/// <summary>3D vector for position (meters) or rotation (degrees).</summary>
public record Vec3(float X, float Y, float Z);

/// <summary>Full weld configuration data (used in create, modify, presets).</summary>
public record WeldData(
    Vec3 Position,
    Vec3 Rotation,
    float Scale = 1f,
    bool LockRotation = true
);

/// <summary>Describes an active weld in API responses.</summary>
public record WeldInfo(
    string SourceVehicleId,
    string TargetVehicleId,
    Vec3 Position,
    Vec3 Rotation,
    float Scale,
    bool LockRotation
);

// ── Garry's Torch — Weld CRUD ───────────────────────────────────────────────

/// <summary>Request body for creating a new weld.</summary>
public record TorchCreateWeldRequest(
    string SourceVehicleId,
    string TargetVehicleId,
    WeldData? Data = null,
    string? PresetName = null
);

/// <summary>
/// Request body for modifying an existing weld (immediate).
/// Only provided fields are updated; omitted fields remain unchanged.
/// </summary>
public record TorchModifyWeldRequest(
    Vec3? Position = null,
    Vec3? Rotation = null,
    float? Scale = null,
    bool? LockRotation = null
);

/// <summary>Easing configuration for animated weld transitions.</summary>
public record TorchEasingConfig(
    TorchEasingType Easing = TorchEasingType.EaseInOut,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>Request body for animating a weld transition over time.</summary>
public record TorchAnimateWeldRequest(
    double DurationSeconds,
    WeldData? Data = null,
    string? PresetName = null,
    TorchEasingConfig? Easing = null
);

// ── Garry's Torch — Preset CRUD ─────────────────────────────────────────────

/// <summary>Preset data as returned by the API.</summary>
public record TorchPresetInfo(
    string Name,
    Vec3 Position,
    Vec3 Rotation,
    float Scale,
    bool LockRotation
);

/// <summary>Request body for creating or updating a preset.</summary>
public record TorchSavePresetRequest(
    WeldData Data
);

// ── Garry's Torch — API Responses ───────────────────────────────────────────

public record TorchWeldResult(WeldInfo Weld);
public record TorchWeldListResult(WeldInfo[] Welds);
public record TorchPresetResult(TorchPresetInfo Preset);
public record TorchPresetListResult(TorchPresetInfo[] Presets);
public record TorchDeleteResult(string Message);
public record TorchAnimateResult(string SourceVehicleId, string Status);
```

### Key Design Notes

- `Vec3` is a simple, TS-friendly 3D vector record. Much cleaner than exposing `float3` which is a KSA game type
- `WeldData` is the canonical shape for weld configuration — used by create, modify, preset save, and animate
- Create and Animate requests accept **either** inline `Data` or a `PresetName` (validated to have exactly one)
- Modify request uses nullable fields so only specified values are updated (PATCH semantics via POST)
- All response models wrap in `ApiResponse<T>` as per the existing pattern
- `TorchEasingType` is separate from `CameraEasingType` to maintain independence

---

## Task 4: Create RPC Endpoint Handlers in `unladen-swallow.lib`

### New File: `unladen-swallow.lib/TorchWeldsEndpoint.cs`

Handles `GET /torch/welds` and `POST /torch/welds`.

```csharp
using System;
using System.Linq;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.GarrysTorchLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class TorchWeldsEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GarrysTorchSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Garry's Torch mod is not loaded.");

                        var welds = submod.Welds;
                        var infos = new WeldInfo[welds.Count];
                        for (int i = 0; i < welds.Count; i++)
                        {
                            var w = welds[i];
                            infos[i] = new WeldInfo(
                                w.Source.Id, w.Target.Id,
                                new Vec3(w.Position.X, w.Position.Y, w.Position.Z),
                                new Vec3(w.Rotation.X, w.Rotation.Y, w.Rotation.Z),
                                w.Scale, w.LockRotation);
                        }
                        return new TorchWeldListResult(infos);
                    });
                    return (object)new ApiResponse<TorchWeldListResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error listing welds.", ex);
                }
            })
            .Post(async (TorchCreateWeldRequest body) =>
            {
                // Validate: must have Data or PresetName, not both, not neither
                if (body.Data == null && string.IsNullOrWhiteSpace(body.PresetName))
                    throw new ProviderException(ResponseStatus.BadRequest,
                        "Either 'data' or 'presetName' must be provided.");
                if (body.Data != null && !string.IsNullOrWhiteSpace(body.PresetName))
                    throw new ProviderException(ResponseStatus.BadRequest,
                        "Provide either 'data' or 'presetName', not both.");
                if (string.IsNullOrWhiteSpace(body.SourceVehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing sourceVehicleId.");
                if (string.IsNullOrWhiteSpace(body.TargetVehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing targetVehicleId.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GarrysTorchSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Garry's Torch mod is not loaded.");

                        WeldData data;
                        if (body.Data != null)
                        {
                            data = body.Data;
                        }
                        else
                        {
                            var preset = submod.GetPreset(body.PresetName!);
                            if (preset == null)
                                throw new ProviderException(ResponseStatus.NotFound,
                                    $"Preset not found: '{body.PresetName}'");
                            var p = preset.Value;
                            data = new WeldData(
                                new Vec3(p.Position.X, p.Position.Y, p.Position.Z),
                                new Vec3(p.Rotation.X, p.Rotation.Y, p.Rotation.Z),
                                p.Scale, p.LockRotation);
                        }

                        var pos = new Brutal.Numerics.float3(data.Position.X, data.Position.Y, data.Position.Z);
                        var rot = new Brutal.Numerics.float3(data.Rotation.X, data.Rotation.Y, data.Rotation.Z);
                        var (weld, error) = submod.CreateWeld(
                            body.SourceVehicleId, body.TargetVehicleId,
                            pos, rot, data.Scale, data.LockRotation);

                        if (weld == null)
                            throw new ProviderException(ResponseStatus.BadRequest, error!);

                        return new TorchWeldResult(new WeldInfo(
                            weld.Source.Id, weld.Target.Id,
                            new Vec3(weld.Position.X, weld.Position.Y, weld.Position.Z),
                            new Vec3(weld.Rotation.X, weld.Rotation.Y, weld.Rotation.Z),
                            weld.Scale, weld.LockRotation));
                    });
                    return (object)new ApiResponse<TorchWeldResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error creating weld.", ex);
                }
            })
            .Build();
    }
}
```

### New File: `unladen-swallow.lib/TorchWeldByIdEndpoint.cs`

Handles `GET /torch/welds/{sourceVehicleId}`, `POST /torch/welds/{sourceVehicleId}` (modify), `DELETE /torch/welds/{sourceVehicleId}` (unweld).

**Important GenHTTP note**: The `Inline` handler's `.Get()` / `.Post()` / `.Delete()` methods can accept path parameters using `[FromPath]` attribute on string parameters. However, for this project's existing pattern, path parameters are NOT used (e.g., blinky endpoints pass vehicleId in the body). Since GenHTTP functional handlers with inline builders don't natively support path params in the same builder, we need a different approach.

**Recommended approach**: Use a separate endpoint registered per-weld-id via a custom strategy. However, analyzing the existing codebase patterns, all endpoints use body parameters or query strings. For consistency and simplicity:

- `GET /torch/welds` — list all welds
- `POST /torch/welds` — create a weld (body contains sourceVehicleId, targetVehicleId)
- `POST /torch/welds/get` — get a specific weld (body: `{ "sourceVehicleId": "..." }`)
- `POST /torch/welds/modify` — modify a weld immediately (body contains sourceVehicleId + optional fields)
- `POST /torch/welds/animate` — animate a weld transition (body contains sourceVehicleId + target state)
- `DELETE /torch/welds` — unweld (body: `{ "sourceVehicleId": "..." }`)

Wait — looking at the existing patterns more carefully:
- Camera uses `DELETE /camera/stop` (no body, just delete semantics)
- Vehicle uses `POST /vehicle/actions/ignite` with `VehicleActionRequest` body containing `VehicleId`

So the pattern is: **identification via request body**, not path params. Let's keep it consistent but use semantic HTTP methods:

**Final Route Design:**

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/torch/welds` | List all active welds |
| `POST` | `/torch/welds` | Create a new weld |
| `POST` | `/torch/welds/modify` | Modify weld immediately (body has sourceVehicleId) |
| `POST` | `/torch/welds/animate` | Animate weld to target state (body has sourceVehicleId) |
| `DELETE` | `/torch/welds` | Delete/unweld (body has sourceVehicleId) |
| `GET` | `/torch/presets` | List all presets |
| `POST` | `/torch/presets` | Create/update a preset (body has name + data) |
| `DELETE` | `/torch/presets` | Delete a preset (body has name) |

### File: `unladen-swallow.lib/TorchWeldModifyEndpoint.cs`

```csharp
public static class TorchWeldModifyEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (TorchModifyWeldRequest body) =>
            {
                // body must include sourceVehicleId — wait, TorchModifyWeldRequest 
                // doesn't have it. Need to add it or use a wrapper.
            })
            .Build();
    }
}
```

**Correction**: The `TorchModifyWeldRequest` needs a `SourceVehicleId` field. Update the DTO:

```csharp
public record TorchModifyWeldRequest(
    string SourceVehicleId,
    Vec3? Position = null,
    Vec3? Rotation = null,
    float? Scale = null,
    bool? LockRotation = null
);
```

Endpoint implementation:
1. Validate `SourceVehicleId` not empty
2. Schedule on game thread
3. Get submod instance, verify loaded
4. Call `submod.ModifyWeld(...)` with nullable float3 conversions
5. Return updated `WeldInfo`

### File: `unladen-swallow.lib/TorchWeldAnimateEndpoint.cs`

```csharp
public static class TorchWeldAnimateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (TorchAnimateWeldRequest body) =>
            {
                // Validate sourceVehicleId, duration > 0, data XOR presetName
                // Schedule on game thread
                // Resolve data from body.Data or body.PresetName
                // Call submod.AnimateWeld(...)
                // Return TorchAnimateResult
            })
            .Build();
    }
}
```

**Correction**: `TorchAnimateWeldRequest` also needs `SourceVehicleId`. Update:

```csharp
public record TorchAnimateWeldRequest(
    string SourceVehicleId,
    double DurationSeconds,
    WeldData? Data = null,
    string? PresetName = null,
    TorchEasingConfig? Easing = null
);
```

Implementation:
1. Validate sourceVehicleId not empty
2. Validate durationSeconds > 0
3. Validate data XOR presetName
4. Schedule on game thread
5. Resolve target data from either Data or preset lookup
6. Convert `TorchEasingConfig` to `WeldEasingType` enum
7. Call `submod.AnimateWeld(...)`
8. If error returned, throw `ProviderException(BadRequest, error)`
9. Return `TorchAnimateResult(sourceVehicleId, "animation_queued")`

### File: `unladen-swallow.lib/TorchWeldDeleteEndpoint.cs`

```csharp
public static class TorchWeldDeleteEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Delete(async (TorchDeleteWeldRequest body) =>
            {
                // Validate sourceVehicleId
                // Schedule on game thread
                // Call submod.RemoveWeld(sourceVehicleId)
                // Return success/not-found
            })
            .Build();
    }
}
```

New DTO needed:
```csharp
public record TorchDeleteWeldRequest(string SourceVehicleId);
```

### File: `unladen-swallow.lib/TorchPresetsEndpoint.cs`

Handles `GET /torch/presets` (list all) and `POST /torch/presets` (create/update).

GET implementation:
1. Schedule on game thread
2. Get all preset names
3. For each name, get preset data
4. Return `TorchPresetListResult` with array of `TorchPresetInfo`

POST implementation:
1. Validate name not empty, data not null in body
2. Schedule on game thread
3. Convert `WeldData` to `WeldPreset` (float3 conversions)
4. Call `submod.SavePreset(name, preset)`
5. Return saved preset info

New request DTO:
```csharp
public record TorchSavePresetRequest(string Name, WeldData Data);
```
(Updated — `Name` field added)

### File: `unladen-swallow.lib/TorchPresetDeleteEndpoint.cs`

Handles `DELETE /torch/presets`.

```csharp
public record TorchDeletePresetRequest(string Name);
```

Implementation:
1. Validate name not empty
2. Schedule on game thread
3. Call `submod.DeletePreset(name)`
4. If false (not found), throw NotFound
5. Return `TorchDeleteResult("Preset '{name}' deleted")`

---

## Task 5: Register Routes in `SwallowServer.cs`

### Modify `unladen-swallow.lib/SwallowServer.cs` — `RegisterRoutes()`

Add after the camera routes:

```csharp
// GET    /torch/welds           — list all active welds
// POST   /torch/welds           — create a new weld
// POST   /torch/welds/modify    — modify weld immediately
// POST   /torch/welds/animate   — animate weld to target state
// DELETE /torch/welds            — unweld (delete a weld)
// GET    /torch/presets          — list all presets
// POST   /torch/presets          — save/update a preset
// DELETE /torch/presets           — delete a preset
api.Add("torch", Layout.Create()
    .Add("welds", Layout.Create()
        .Add(TorchWeldsEndpoint.Create())
        .Add("modify", TorchWeldModifyEndpoint.Create())
        .Add("animate", TorchWeldAnimateEndpoint.Create()))
    .Add("presets", Layout.Create()
        .Add(TorchPresetsEndpoint.Create())
        .Add(TorchPresetDeleteEndpoint.Create())));
```

**Note**: The `TorchWeldsEndpoint` handles GET+POST+DELETE on `/torch/welds` root. The `modify` and `animate` sub-paths handle their respective POST operations. `TorchPresetsEndpoint` handles GET+POST on `/torch/presets` root, and `TorchPresetDeleteEndpoint` handles DELETE.

**Important**: Verify that GenHTTP `Inline.Create()` with `.Delete()` works on the same builder as `.Get()` and `.Post()`. Looking at existing code, `CameraStopEndpoint` uses `.Delete()` in a standalone handler. For `TorchWeldsEndpoint`, we need GET + POST + DELETE on the same path. Test this — if the Inline builder supports chaining all three, combine them. If not, use a separate endpoint for DELETE.

**If GenHTTP doesn't support GET+POST+DELETE on one Inline builder**, split:
- `TorchWeldsEndpoint.cs` → GET + POST
- `TorchWeldDeleteEndpoint.cs` → DELETE only

And register:
```csharp
api.Add("torch", Layout.Create()
    .Add("welds", Layout.Create()
        .Add(TorchWeldsEndpoint.Create())       // GET + POST on /torch/welds
        .Add(TorchWeldDeleteEndpoint.Create())   // DELETE on /torch/welds
        .Add("modify", TorchWeldModifyEndpoint.Create())
        .Add("animate", TorchWeldAnimateEndpoint.Create()))
    .Add("presets", Layout.Create()
        .Add(TorchPresetsEndpoint.Create())          // GET + POST on /torch/presets
        .Add(TorchPresetDeleteEndpoint.Create())));  // DELETE on /torch/presets
```

Looking at the existing `FovEndpoint`, it chains `.Get(...)` and `.Post(...)` on the same `Inline.Create()` builder. The `CameraStopEndpoint` uses `.Delete(...)` alone. Based on the GenHTTP functional handler API, all HTTP methods can be chained on one builder. So combining GET + POST + DELETE in one endpoint should work.

**Final approach**: Combine where possible:
- `TorchWeldsEndpoint.cs` → GET (list) + POST (create) + DELETE (unweld)
- `TorchWeldModifyEndpoint.cs` → POST (modify immediately)
- `TorchWeldAnimateEndpoint.cs` → POST (animate transition)
- `TorchPresetsEndpoint.cs` → GET (list) + POST (save) + DELETE (delete)

---

## Task 6: Add Project Reference to `unladen-swallow.lib.csproj`

### Modify `unladen-swallow.lib/unladen-swallow.lib.csproj`

Add project reference to garrys-torch.lib:

```xml
<ProjectReference Include="..\garrys-torch.lib\garrys-torch.lib.csproj" />
```

Add in the existing `<ItemGroup>` with other project references, alongside `glass.lib`, `blinky.lib`, and `camera-controller-override.lib`.

---

## Task 7: Create OpenAPI Specification

### New File: `unladen-swallow.lib/openapi/garrystorch.yml`

Create following the same style as `camera.yml` and `fov.yml`. Full specification below.

**Structure**:
- `info` block with title, version, description
- `servers` block pointing to localhost:7887
- `paths` for each endpoint with full request/response schemas
- `components/schemas` for all data models
- `components/responses` for shared error responses
- Examples for each endpoint

**Paths**:

1. `POST /torch/welds` — Create weld
   - Request: `TorchCreateWeldRequest` (sourceVehicleId, targetVehicleId, data xor presetName)
   - Response 200: `ApiResponse<TorchWeldResult>`
   - Examples: create with inline data, create from preset

2. `GET /torch/welds` — List all welds
   - Response 200: `ApiResponse<TorchWeldListResult>`

3. `DELETE /torch/welds` — Delete/unweld
   - Request: `TorchDeleteWeldRequest` (sourceVehicleId)
   - Response 200: `ApiResponse<TorchDeleteResult>`

4. `POST /torch/welds/modify` — Modify weld immediately
   - Request: `TorchModifyWeldRequest` (sourceVehicleId, optional fields)
   - Response 200: `ApiResponse<TorchWeldResult>`

5. `POST /torch/welds/animate` — Animate weld transition
   - Request: `TorchAnimateWeldRequest` (sourceVehicleId, durationSeconds, data xor presetName, easing)
   - Response 200: `ApiResponse<TorchAnimateResult>`

6. `GET /torch/presets` — List all presets
   - Response 200: `ApiResponse<TorchPresetListResult>`

7. `POST /torch/presets` — Create/update preset
   - Request: `TorchSavePresetRequest` (name, data)
   - Response 200: `ApiResponse<TorchPresetResult>`

8. `DELETE /torch/presets` — Delete preset
   - Request: `TorchDeletePresetRequest` (name)
   - Response 200: `ApiResponse<TorchDeleteResult>`

**Schemas**:
- `Vec3` — `{ x: number, y: number, z: number }`
- `WeldData` — `{ position: Vec3, rotation: Vec3, scale: number, lockRotation: boolean }`
- `WeldInfo` — `{ sourceVehicleId: string, targetVehicleId: string, position: Vec3, rotation: Vec3, scale: number, lockRotation: boolean }`
- `TorchEasingType` — enum `[linear, easeIn, easeOut, easeInOut]`
- `TorchEasingConfig` — `{ easing: TorchEasingType, easingPowerStart: number, easingPowerEnd: number }`
- All request/response records as defined in Task 3

---

## Task 8: Update `UnladenSwallowSubmod.cs` Endpoint List

### Modify `unladen-swallow.lib/UnladenSwallowSubmod.cs`

In the `RenderContent()` method, inside the "Available Endpoints" collapsing header, add the new torch endpoints:

```csharp
ImGui.TextDisabled("GET    /torch/welds              — list active welds");
ImGui.TextDisabled("POST   /torch/welds              — create a weld");
ImGui.TextDisabled("POST   /torch/welds/modify       — modify weld immediately");
ImGui.TextDisabled("POST   /torch/welds/animate      — animate weld transition");
ImGui.TextDisabled("DELETE /torch/welds              — unweld (remove a weld)");
ImGui.TextDisabled("GET    /torch/presets             — list presets");
ImGui.TextDisabled("POST   /torch/presets             — save/update a preset");
ImGui.TextDisabled("DELETE /torch/presets             — delete a preset");
```

---

## Task 9: Build and Verify Compilation

Run `dotnet build` from the solution root to verify everything compiles.

Fix any issues that arise from:
- Missing usings
- Type mismatches between `float3` (KSA) and `Vec3` (API DTOs)
- GenHTTP handler chaining issues
- Nullable reference type warnings

---

## Task 10: Update Documentation

### 10a. Update `REPOSITORY_INDEX.md`

Add entry for the new garry's torch RPC integration, noting that garrys-torch.lib now has animation support and unladen-swallow.lib now includes torch endpoints.

### 10b. Update `garrys-torch/README.md`

Add section documenting the new animation system (API-only, no ImGui UI).

### 10c. Update `unladen-swallow/README.md`

Add documentation for all new torch endpoints.

---

## Summary of All Files Changed/Created

### New Files
| File | Description |
|------|-------------|
| `garrys-torch.lib/WeldAnimation.cs` | Animation state class + easing functions |
| `garrys-torch.lib/WeldAnimationManager.cs` | Per-weld animation queue manager |
| `unladen-swallow.lib/TorchWeldsEndpoint.cs` | GET/POST/DELETE /torch/welds |
| `unladen-swallow.lib/TorchWeldModifyEndpoint.cs` | POST /torch/welds/modify |
| `unladen-swallow.lib/TorchWeldAnimateEndpoint.cs` | POST /torch/welds/animate |
| `unladen-swallow.lib/TorchPresetsEndpoint.cs` | GET/POST/DELETE /torch/presets |
| `unladen-swallow.lib/openapi/garrystorch.yml` | OpenAPI 3.1 specification |

### Modified Files
| File | Changes |
|------|---------|
| `garrys-torch.lib/GarrysTorchSubmod.cs` | Add Instance, public API methods, animation manager integration |
| `unladen-swallow.lib/ApiTypes.cs` | Add all torch DTOs |
| `unladen-swallow.lib/SwallowServer.cs` | Register torch routes |
| `unladen-swallow.lib/unladen-swallow.lib.csproj` | Add garrys-torch.lib reference |
| `unladen-swallow.lib/UnladenSwallowSubmod.cs` | Add torch endpoints to UI list |
| `REPOSITORY_INDEX.md` | Update with new feature info |
| `garrys-torch/README.md` | Document animation system |
| `unladen-swallow/README.md` | Document torch endpoints |

---

## Execution Order

1. **Task 1** — Expose public API on GarrysTorchSubmod (prerequisite for everything)
2. **Task 2** — Add animation system (WeldAnimation, WeldAnimationManager, integrate into submod)
3. **Task 3** — Add DTOs to ApiTypes.cs
4. **Task 4** — Create endpoint handlers
5. **Task 5** — Register routes in SwallowServer
6. **Task 6** — Add project reference
7. **Task 7** — Create OpenAPI spec
8. **Task 8** — Update endpoint list in UI
9. **Task 9** — Build and verify
10. **Task 10** — Update documentation
