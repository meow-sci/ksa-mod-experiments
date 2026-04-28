# Zippo Light Animations — Implementation Plan

## Overview

Add a queue-based light animation system to the existing zippo mod. Animations interpolate both color and intensity between start/end values over a configurable duration using easing functions. All functionality is exposed both via ImGui UI and unladen-swallow HTTP RPC endpoints.

---

## Design Decisions (from clarification)

| Decision | Choice |
|---|---|
| Animation target | Selected light part only (not all parts at once) |
| Manual controls during animation | Disabled (locked) while animation is running on that part |
| Queue depth | Max 25 queued animations per part + clear queue button |
| Animation completion | Light stays at end color/intensity values |
| RPC coverage | Full: list lights, get/set color, get/set intensity, on/off, AND animations |
| Easing reuse | Extract shared `EasingHelper` into `ksa-abstractions.lib` |

---

## Architecture

### Existing Code (zippo.lib)

| File | Contents |
|---|---|
| `LightController.cs` | Static light manipulation: `GetLightParts()`, `ApplyIntensity()`, `ApplyColor()`, `ReadIntensity()`, `ReadColor()`, `GetLightComponents()`, `HasLights()` |
| `ZippoSubmod.cs` | `ISubmod` with ImGui UI: vehicle selector, light part selector, intensity slider, color preset combo, color picker, on/off toggle, debug dump |

### New/Modified File Map

| File | Action | Contents |
|---|---|---|
| `ksa-abstractions.lib/EasingHelper.cs` | **CREATE** | Shared `EasingType` enum + `EasingHelper.ApplyEasing()` static method |
| `zippo.lib/LightAnimation.cs` | **CREATE** | `LightAnimation` class: holds start/end color+intensity, duration, easing config, elapsed time, `Update(dt)` method |
| `zippo.lib/LightAnimationManager.cs` | **CREATE** | Per-part animation queue manager: `Enqueue()`, `Update(dt)`, `ClearQueue()`, `IsAnimating()`, queue depth enforcement |
| `zippo.lib/XkcdColorHelper.cs` | **CREATE** | Static helper to enumerate `KSAColor.Xkcd` colors via reflection, cache them, and look up by name. Reusable by both UI and RPC |
| `zippo.lib/ZippoSubmod.cs` | **MODIFY** | Add animation UI section, wire `Update(dt)` to tick animation manager, disable controls during animation, add clear-queue button |
| `zippo.lib/LightController.cs` | **MODIFY** | Add public static API methods for RPC: `GetLightPartInfos()`, `SetLightState()`, expose `ZippoSubmod.Instance` singleton |
| `unladen-swallow.lib/ApiTypes.cs` | **MODIFY** | Add zippo DTOs: request/response records for lights list, state, animation |
| `unladen-swallow.lib/ZippoLightsEndpoint.cs` | **CREATE** | `GET /zippo/lights`, `POST /zippo/lights/state` (set color/intensity/on-off) |
| `unladen-swallow.lib/ZippoAnimateEndpoint.cs` | **CREATE** | `POST /zippo/animate` (queue animation), `DELETE /zippo/animate` (clear queue) |
| `unladen-swallow.lib/SwallowServer.cs` | **MODIFY** | Register zippo routes |
| `unladen-swallow.lib/unladen-swallow.lib.csproj` | **MODIFY** | Add `<ProjectReference>` to `zippo.lib` |
| `unladen-swallow.lib/openapi/zippo.yml` | **CREATE** | OpenAPI 3.1.0 spec for all zippo endpoints |
| `unladen-swallow/mod.toml` | **MODIFY** | Add `MeowSci.ZippoLib` to `ImportedAssemblies` |
| `unscience/unscience.csproj` | **CHECK** | Already references `zippo.lib` — no change needed |
| `REPOSITORY_INDEX.md` | **MODIFY** | Update zippo entry with animation features and RPC endpoints |
| `zippo/README.md` | **MODIFY** | Document new animation system and RPC API |

---

## Task List

### Task 1: Extract Shared Easing Helper into ksa-abstractions.lib

**Goal:** Create a shared easing utility so zippo, garry's-torch, and camera-controller-override can all use the same code.

**File to create:** `ksa-abstractions.lib/EasingHelper.cs`

**Exact contents:**

```csharp
namespace MeowSci.KsaAbstractions;

public enum EasingType
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

public static class EasingHelper
{
    public static double ApplyEasing(double t, EasingType easingType,
        double powerStart = 3.0, double powerEnd = 3.0)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return easingType switch
        {
            EasingType.EaseIn => Math.Pow(t, powerStart),
            EasingType.EaseOut => 1.0 - Math.Pow(1.0 - t, powerEnd),
            EasingType.EaseInOut => t < 0.5
                ? Math.Pow(2 * t, powerStart) / 2.0
                : 1.0 - Math.Pow(2 * (1 - t), powerEnd) / 2.0,
            _ => t
        };
    }
}
```

**Then refactor existing consumers:**

1. **`garrys-torch.lib/WeldAnimation.cs`**: Remove the local `WeldEasingType` enum and `ApplyEasing()` method. Add `using MeowSci.KsaAbstractions;`. Replace `WeldEasingType` → `EasingType` throughout. Replace `ApplyEasing(rawT, Easing, ...)` → `EasingHelper.ApplyEasing(rawT, Easing, ...)`.

2. **`garrys-torch.lib/GarrysTorchSubmod.cs`**: Replace all `WeldEasingType` references with `EasingType` from `MeowSci.KsaAbstractions`.

3. **`camera-controller-override.lib/Animation/AnimationHelpers.cs`**: Remove the local `EasingType` enum and `ApplyEasing()` method body. Re-export `EasingType = MeowSci.KsaAbstractions.EasingType` via `using EasingType = MeowSci.KsaAbstractions.EasingType;` OR simply change all animation files to use `MeowSci.KsaAbstractions.EasingType` directly. The `AnimationHelpers.ApplyEasing()` method can be kept as a forwarding call to `EasingHelper.ApplyEasing()` to avoid changing all animation classes, OR remove it and update all callers. **Recommended approach:** Keep `AnimationHelpers.ApplyEasing()` as a thin wrapper that delegates to `EasingHelper.ApplyEasing()` and add a `using EasingType = MeowSci.KsaAbstractions.EasingType;` alias at the top of `AnimationHelpers.cs`. This way all existing animation code compiles without changes.

4. **`unladen-swallow.lib/ApiTypes.cs`**: The `TorchEasingType` and `CameraEasingType` enums in ApiTypes are API-facing DTOs and should remain separate from the shared enum (they are serialized to/from JSON). No changes needed here — the conversion happens at the endpoint boundary (e.g., `(EasingType)(int)easing.Easing`).

**Verify:** `dotnet build` the entire solution after this task.

---

### Task 2: Create XkcdColorHelper in zippo.lib

**Goal:** Provide a cached, reflection-based lookup for all `KSAColor.Xkcd` named colors. Used by both the ImGui UI (filterable combobox) and the RPC endpoint (name-to-RGB resolution).

**File to create:** `zippo.lib/XkcdColorHelper.cs`

**Design:**

```csharp
namespace MeowSci.ZippoLib;

public static class XkcdColorHelper
{
    // Lazy-cached sorted array of (name, float4) from KSAColor.Xkcd static properties
    private static (string Name, float4 Color)[]? _colors;

    public static (string Name, float4 Color)[] GetAll()
    {
        if (_colors != null) return _colors;
        // Reflect over typeof(KSAColor.Xkcd).GetProperties(Public | Static)
        // Cast each via: float4 val = (Color.Preset)prop.GetValue(null)!;
        // Sort alphabetically by name
        // Cache and return
        // Pattern copied from doh.lib/DohSubmod.cs lines 559-575
    }

    /// <summary>
    /// Look up an XKCD color by name (case-insensitive).
    /// Returns null if not found.
    /// </summary>
    public static float4? FindByName(string name)
    {
        var all = GetAll();
        foreach (var (n, c) in all)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                return c;
        return null;
    }

    /// <summary>Returns all color names as a string array (for combo filtering).</summary>
    public static string[] GetNames()
    {
        var all = GetAll();
        var names = new string[all.Length];
        for (int i = 0; i < all.Length; i++)
            names[i] = all[i].Name;
        return names;
    }
}
```

**Dependencies:** Needs `using System.Reflection;`, `using Brutal.Numerics;`, `using KSA;`. The `zippo.lib.csproj` already references KSA.dll.

**Verify:** `dotnet build zippo.lib`

---

### Task 3: Create LightAnimation in zippo.lib

**Goal:** A single animation step that interpolates color (float3) and intensity (float) from start values to end values over a duration using easing.

**File to create:** `zippo.lib/LightAnimation.cs`

**Design:**

```csharp
namespace MeowSci.ZippoLib;

using MeowSci.KsaAbstractions;
using Brutal.Numerics;

public class LightAnimation
{
    public float3 StartColor { get; }
    public float3 EndColor { get; }
    public float StartIntensity { get; }
    public float EndIntensity { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    public double EasingPowerStart { get; }
    public double EasingPowerEnd { get; }
    public double ElapsedSeconds { get; private set; }
    public bool IsComplete => ElapsedSeconds >= DurationSeconds;

    public LightAnimation(
        float3 startColor, float3 endColor,
        float startIntensity, float endIntensity,
        double durationSeconds,
        EasingType easing = EasingType.Linear,
        double easingPowerStart = 3.0,
        double easingPowerEnd = 3.0)
    {
        StartColor = startColor;
        EndColor = endColor;
        StartIntensity = startIntensity;
        EndIntensity = endIntensity;
        DurationSeconds = Math.Max(0.001, durationSeconds);
        Easing = easing;
        EasingPowerStart = easingPowerStart;
        EasingPowerEnd = easingPowerEnd;
    }

    /// <summary>
    /// Advance animation by dt seconds. Returns (color, intensity) for this frame.
    /// When complete, returns exact end values.
    /// </summary>
    public (float3 Color, float Intensity) Update(double dt)
    {
        ElapsedSeconds += dt;

        if (ElapsedSeconds >= DurationSeconds)
        {
            return (EndColor, EndIntensity);
        }

        double rawT = ElapsedSeconds / DurationSeconds;
        float t = (float)EasingHelper.ApplyEasing(rawT, Easing, EasingPowerStart, EasingPowerEnd);

        var color = Lerp(StartColor, EndColor, t);
        float intensity = StartIntensity + (EndIntensity - StartIntensity) * t;
        return (color, intensity);
    }

    private static float3 Lerp(float3 a, float3 b, float t)
    {
        return new float3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
    }
}
```

**Notes:**
- The `Update()` method returns the interpolated values; the caller is responsible for applying them to the part via `LightController.ApplyColor()` / `ApplyIntensity()`.
- When `IsComplete` is true, the final call to `Update()` returns exact end values (no floating-point drift).
- The pattern mirrors `garrys-torch.lib/WeldAnimation.cs` closely.

**Verify:** `dotnet build zippo.lib`

---

### Task 4: Create LightAnimationManager in zippo.lib

**Goal:** Manages per-part animation queues. At most one animation runs per part at a time; additional animations queue (up to 25). On completion, the next animation starts with corrected start state captured from the part's current values.

**File to create:** `zippo.lib/LightAnimationManager.cs`

**Design:**

```csharp
namespace MeowSci.ZippoLib;

using KSA;

public class LightAnimationManager
{
    public const int MaxQueueDepth = 25;

    // Key = Part.Id (string), since Part references can change between frames
    private readonly Dictionary<string, LightAnimation> _active = new();
    private readonly Dictionary<string, Queue<LightAnimation>> _queues = new();

    /// <summary>Returns the currently active animation for the part, or null.</summary>
    public LightAnimation? GetActiveAnimation(string partId)
        => _active.TryGetValue(partId, out var anim) ? anim : null;

    /// <summary>Returns the number of queued (not active) animations for the part.</summary>
    public int GetQueueCount(string partId)
        => _queues.TryGetValue(partId, out var q) ? q.Count : 0;

    /// <summary>Returns true if an animation is active or queued for the part.</summary>
    public bool IsAnimating(string partId)
        => _active.ContainsKey(partId);

    /// <summary>
    /// Enqueue an animation for the part. If no animation is active, starts immediately.
    /// Returns false if queue is full (MaxQueueDepth reached).
    /// </summary>
    public bool Enqueue(string partId, LightAnimation animation)
    {
        if (!_active.ContainsKey(partId))
        {
            _active[partId] = animation;
            return true;
        }

        if (!_queues.TryGetValue(partId, out var queue))
        {
            queue = new Queue<LightAnimation>();
            _queues[partId] = queue;
        }

        if (queue.Count >= MaxQueueDepth)
            return false;

        queue.Enqueue(animation);
        return true;
    }

    /// <summary>
    /// Tick all active animations. For each completed animation, apply end values to the
    /// part and promote the next queued animation (re-capturing start state from the part's
    /// current color/intensity).
    /// 
    /// The caller must provide a resolver function that returns the Part for a given partId,
    /// or null if the part is no longer available.
    /// </summary>
    public void Update(double dt, Func<string, Part?> partResolver)
    {
        var keys = new List<string>(_active.Keys);
        foreach (var partId in keys)
        {
            var anim = _active[partId];
            var (color, intensity) = anim.Update(dt);

            var part = partResolver(partId);
            if (part == null)
            {
                // Part no longer exists — cancel animation
                _active.Remove(partId);
                _queues.Remove(partId);
                continue;
            }

            LightController.ApplyColor(part, color);
            LightController.ApplyIntensity(part, intensity);

            if (anim.IsComplete)
            {
                _active.Remove(partId);
                PromoteNext(partId, part);
            }
        }
    }

    /// <summary>Cancel all animations (active + queued) for a specific part.</summary>
    public void CancelAll(string partId)
    {
        _active.Remove(partId);
        _queues.Remove(partId);
    }

    /// <summary>Cancel all animations across all parts.</summary>
    public void Clear()
    {
        _active.Clear();
        _queues.Clear();
    }

    private void PromoteNext(string partId, Part part)
    {
        if (!_queues.TryGetValue(partId, out var queue) || queue.Count == 0)
        {
            _queues.Remove(partId);
            return;
        }

        var next = queue.Dequeue();
        if (queue.Count == 0)
            _queues.Remove(partId);

        // Re-capture start state from current part values for seamless chaining
        var currentColor = LightController.ReadColor(part.Template);
        var currentIntensity = LightController.ReadIntensity(part.Template);

        var corrected = new LightAnimation(
            currentColor, next.EndColor,
            currentIntensity, next.EndIntensity,
            next.DurationSeconds, next.Easing,
            next.EasingPowerStart, next.EasingPowerEnd);

        _active[partId] = corrected;
    }
}
```

**Key design choices:**
- Keys are `string` (Part.Id) not `Part` references, since Part objects may be stale across frames.
- `partResolver` delegate lets the manager work without directly holding Vehicle/Part references.
- Re-captures start state on queue promotion (same pattern as `WeldAnimationManager.PromoteNext()`).
- Returns `false` on full queue so the UI/RPC can show an error.

**Verify:** `dotnet build zippo.lib`

---

### Task 5: Add Static API to ZippoSubmod for RPC Access

**Goal:** Expose a singleton instance and public API methods on `ZippoSubmod` so that `unladen-swallow.lib` can call zippo functionality from HTTP handlers.

**File to modify:** `zippo.lib/ZippoSubmod.cs`

**Changes:**

1. **Add static instance singleton** (same pattern as `GarrysTorchSubmod.Instance`):
   ```csharp
   public static ZippoSubmod? Instance { get; private set; }
   ```
   Set `Instance = this` in `Initialize()`, set `Instance = null` in `Dispose()`.

2. **Add `LightAnimationManager` field:**
   ```csharp
   private readonly LightAnimationManager _animationManager = new();
   ```

3. **Wire `Update(double dt)`** to tick the animation manager:
   ```csharp
   public void Update(double dt)
   {
       _animationManager.Update(dt, partId =>
       {
           // Resolve partId to Part from the currently known light parts on all vehicles
           // Search across all vehicles, not just the selected one
           foreach (var v in VehicleProvider.GetAllVehicles())
           {
               var parts = LightController.GetLightParts(v);
               foreach (var p in parts)
                   if (p.Id == partId) return p;
           }
           return null;
       });
   }
   ```

4. **Add public API methods** for RPC use:
   ```csharp
   /// <summary>List all light parts on a vehicle with their current state.</summary>
   public List<LightPartInfo> GetLightPartInfos(string vehicleId) { ... }

   /// <summary>Set color and/or intensity on a specific light part. Returns error string or null on success.</summary>
   public string? SetLightState(string vehicleId, string partId, float3? color, float? intensity, bool? enabled) { ... }

   /// <summary>Queue a light animation on a specific part. Returns error string or null on success.</summary>
   public string? QueueAnimation(string vehicleId, string partId, LightAnimation animation) { ... }

   /// <summary>Clear animation queue for a specific part.</summary>
   public string? ClearAnimationQueue(string vehicleId, string partId) { ... }

   /// <summary>Check if a part has an active animation.</summary>
   public bool IsAnimating(string partId) => _animationManager.IsAnimating(partId);
   ```

5. **Add `LightPartInfo` record** (in same file or a new `LightPartInfo.cs`):
   ```csharp
   public record LightPartInfo(
       string PartId,
       string DisplayName,
       float Intensity,
       float3 Color,
       bool IsEnabled,
       bool IsAnimating,
       int QueuedAnimations);
   ```

**Important:** The public API methods find Vehicle/Part by ID using `VehicleProvider.GetAllVehicles()` + `LightController.GetLightParts()`. They do NOT rely on the UI selection state. The RPC endpoints call these methods inside `GameThread.Scheduler.Schedule()`.

**Verify:** `dotnet build zippo.lib`

---

### Task 6: Update ZippoSubmod ImGui UI for Animations

**Goal:** Add animation configuration UI below the existing light controls, with proper disable behavior during active animations.

**File to modify:** `zippo.lib/ZippoSubmod.cs`

**UI additions (added after the existing "Light Controls" section):**

```
┌───────────────────────────────────────────────┐
│ ▼ Light Animation                             │
│                                               │
│ Start Color   [■][________XKCD combo_______]  │
│ End Color     [■][________XKCD combo_______]  │
│ Start Intens. [====0.8====]                   │
│ End Intens.   [====1.0====]                   │
│ Duration (s)  [====2.0====]                   │
│ Easing        [EaseInOut ▼]                   │
│ Power (Start) [===3.0===]  ← show if EaseIn   │
│ Power (End)   [===3.0===]     or EaseInOut     │
│                                               │
│ [ Queue Animation ]  [ Clear Queue ]          │
│                                               │
│ Status: Playing 1/3 (0.8s / 2.0s)            │
│ ████████████░░░░░ 40%                         │
└───────────────────────────────────────────────┘
```

**New state fields to add:**
```csharp
// Animation UI state
private float3 _animStartColor = new(1f, 1f, 1f);
private float3 _animEndColor = new(1f, 1f, 1f);
private float _animStartIntensity = 1.0f;
private float _animEndIntensity = 1.0f;
private float _animDuration = 2.0f;
private int _animEasingIdx = 3; // EaseInOut
private float _animPowerStart = 3.0f;
private float _animPowerEnd = 3.0f;

// XKCD color combo state for animation start/end
private int _animStartXkcdIdx = -1; // -1 = not using named color
private int _animEndXkcdIdx = -1;
private ImGuiTextFilter _animStartColorFilter = new();
private ImGuiTextFilter _animEndColorFilter = new();

// For wrapping the color picker + xkcd combo per color field
private float4 _animStartColor4 = new(1f, 1f, 1f, 1f);
private float4 _animEndColor4 = new(1f, 1f, 1f, 1f);
```

**UI behavior rules:**
1. The **existing controls** (intensity slider, color preset combo, color picker, on/off button) are wrapped in `ImGui.BeginDisabled()` / `ImGui.EndDisabled()` when `_animationManager.IsAnimating(selectedPartId)` is true.
2. The **"Queue Animation" button** creates a `LightAnimation` from the UI state fields and calls `_animationManager.Enqueue()`.
3. The **"Clear Queue" button** calls `_animationManager.CancelAll(partId)`.
4. The **progress bar** shows `_animationManager.GetActiveAnimation(partId).ElapsedSeconds / DurationSeconds`.
5. The **status text** shows: "Playing N/M (elapsed / duration)" where N = current index (1) and M = 1 + queue count.
6. Each color field has BOTH a small color picker button AND a filterable XKCD combobox side by side. When the user picks from the XKCD combo, the color picker updates. When the user edits the color picker directly, the XKCD combo resets to "(Custom)".
7. The Easing combo contains: `["Linear", "Ease In", "Ease Out", "Ease In-Out"]`.
8. Power Start slider shows if Easing is EaseIn or EaseInOut. Power End slider shows if Easing is EaseOut or EaseInOut. Range 1.0–6.0.
9. When a light part is first selected, set `_animStartColor` and `_animStartIntensity` from the part's current values using `LightController.ReadColor()` / `ReadIntensity()`.

**Layout pattern:** Use the standard 2-column proportional table (`SizingStretchProp`, `NoPadOuterX`, 1:3 label:widget ratio) matching the existing controls section. The two color fields each need a row with a `ColorEdit4` (NoInputs|NoLabel) + SameLine + XKCD combo side by side in the widget column.

**Verify:** `dotnet build zippo.lib`

---

### Task 7: Add Zippo DTOs to unladen-swallow.lib ApiTypes.cs

**Goal:** Add all request/response records for the zippo RPC API.

**File to modify:** `unladen-swallow.lib/ApiTypes.cs`

**Add the following after the Garry's Torch section:**

```csharp
// ── Zippo — Light Control ───────────────────────────────────────────────────

/// <summary>Easing types for light animations (mirrors MeowSci.KsaAbstractions.EasingType).</summary>
public enum ZippoEasingType
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

/// <summary>RGB color (0-1 per channel).</summary>
public record ZippoColor(float R, float G, float B);

/// <summary>Describes a light part on a vehicle.</summary>
public record ZippoLightPartInfo(
    string PartId,
    string DisplayName,
    float Intensity,
    ZippoColor Color,
    bool IsEnabled,
    bool IsAnimating,
    int QueuedAnimations);

/// <summary>Response for GET /zippo/lights.</summary>
public record ZippoLightsListResult(
    string VehicleId,
    ZippoLightPartInfo[] Lights);

/// <summary>
/// Request body for POST /zippo/lights/state.
/// Sets color and/or intensity on a specific light part. Only provided fields are updated.
/// Color can be specified as RGB values OR as a named KSAColor.Xkcd color constant (not both).
/// </summary>
public record ZippoSetStateRequest(
    string VehicleId,
    string PartId,
    ZippoColor? Color = null,
    string? ColorName = null,
    float? Intensity = null,
    bool? Enabled = null);

/// <summary>Result after setting light state.</summary>
public record ZippoSetStateResult(
    string PartId,
    ZippoColor Color,
    float Intensity,
    bool IsEnabled);

/// <summary>Easing configuration for light animations.</summary>
public record ZippoEasingConfig(
    ZippoEasingType Easing = ZippoEasingType.EaseInOut,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0);

/// <summary>
/// Color specification for animation endpoints. Provide EITHER rgb OR colorName, not both.
/// If neither is provided, the light's current color is used as the value.
/// </summary>
public record ZippoAnimColor(
    ZippoColor? Rgb = null,
    string? ColorName = null);

/// <summary>
/// Request body for POST /zippo/animate.
/// Queues a light animation that interpolates color and intensity from start to end values.
/// If start color/intensity are omitted, the light's current values are used.
/// </summary>
public record ZippoAnimateRequest(
    string VehicleId,
    string PartId,
    double DurationSeconds,
    ZippoAnimColor? StartColor = null,
    ZippoAnimColor? EndColor = null,
    float? StartIntensity = null,
    float? EndIntensity = null,
    ZippoEasingConfig? Easing = null);

/// <summary>Result after queuing an animation.</summary>
public record ZippoAnimateResult(
    string PartId,
    string Status,
    int QueuePosition);

/// <summary>Request body for DELETE /zippo/animate (clear queue).</summary>
public record ZippoClearAnimationRequest(
    string VehicleId,
    string PartId);

/// <summary>Result after clearing the animation queue.</summary>
public record ZippoClearAnimationResult(
    string PartId,
    string Status);
```

**Design notes:**
- `ZippoAnimColor` supports both RGB and named color, matching the requirement. The endpoint validates mutual exclusivity.
- Start values default to current part state when omitted (convenience for "animate from current to target" use case).
- The easing config record follows the exact pattern of `TorchEasingConfig`.

**Verify:** `dotnet build unladen-swallow.lib`

---

### Task 8: Create Zippo RPC Endpoints in unladen-swallow.lib

**Goal:** Implement HTTP endpoints for full zippo light control.

#### 8a: Create `unladen-swallow.lib/ZippoLightsEndpoint.cs`

Handles:
- `GET /zippo/lights?vehicleId=xxx` — list all light parts on a vehicle with current state
- `POST /zippo/lights/state` — set color/intensity/enabled on a specific part

**GET handler logic:**
1. Validate `vehicleId` query param is present
2. `await GameThread.Scheduler.Schedule(() => { ... })`
3. Inside: find vehicle by ID, call `ZippoSubmod.Instance.GetLightPartInfos(vehicleId)`
4. Map `LightPartInfo` → `ZippoLightPartInfo` DTOs
5. Return `ApiResponse<ZippoLightsListResult>`

**POST handler logic:**
1. Validate request body fields (`VehicleId`, `PartId` required; `Color` and `ColorName` mutually exclusive)
2. If `ColorName` is provided, resolve to RGB via `XkcdColorHelper.FindByName()` inside the game thread schedule
3. Call `ZippoSubmod.Instance.SetLightState(vehicleId, partId, color, intensity, enabled)`
4. Return `ApiResponse<ZippoSetStateResult>`

**Pattern:** Follow `TorchWeldsEndpoint.cs` exactly — `Inline.Create().Serializers(Serialization.Default()).Get(...).Build()` for the list endpoint, separate handler for state.

**Note:** Since GenHTTP `Inline` handlers can only have one GET and one POST per handler, split into TWO handler classes:
- `ZippoLightsEndpoint` — handles `GET /zippo/lights` (list)
- `ZippoLightStateEndpoint` — handles `POST /zippo/lights/state` (set state)

#### 8b: Create `unladen-swallow.lib/ZippoAnimateEndpoint.cs`

Handles:
- `POST /zippo/animate` — queue a light animation
- `DELETE /zippo/animate` — clear animation queue for a part

**POST handler logic:**
1. Validate: `VehicleId`, `PartId` required; `DurationSeconds > 0`; color specs validate mutual exclusivity (rgb vs colorName)
2. `await GameThread.Scheduler.Schedule(() => { ... })`
3. Resolve any named colors to RGB via `XkcdColorHelper.FindByName()`
4. If start color/intensity not provided, read current values from the part
5. Create `LightAnimation` instance
6. Call `ZippoSubmod.Instance.QueueAnimation(vehicleId, partId, animation)`
7. Return `ApiResponse<ZippoAnimateResult>` with queue position

**DELETE handler logic:**
1. Validate: `VehicleId`, `PartId` required
2. Call `ZippoSubmod.Instance.ClearAnimationQueue(vehicleId, partId)`
3. Return `ApiResponse<ZippoClearAnimationResult>`

**Verify:** `dotnet build unladen-swallow.lib`

---

### Task 9: Register Zippo Routes in SwallowServer

**File to modify:** `unladen-swallow.lib/SwallowServer.cs`

**Add to `RegisterRoutes()` method**, between the torch section and the CORS line:

```csharp
// GET    /zippo/lights          — list light parts on a vehicle
// POST   /zippo/lights/state    — set color/intensity/enabled
// POST   /zippo/animate         — queue light animation
// DELETE /zippo/animate         — clear animation queue
api.Add("zippo", Layout.Create()
    .Add("lights", Layout.Create()
        .Add(ZippoLightsEndpoint.Create())
        .Add("state", ZippoLightStateEndpoint.Create()))
    .Add("animate", ZippoAnimateEndpoint.Create()));
```

**Verify:** `dotnet build unladen-swallow.lib`

---

### Task 10: Add zippo.lib ProjectReference to unladen-swallow.lib

**File to modify:** `unladen-swallow.lib/unladen-swallow.lib.csproj`

**Add to the `<ItemGroup>` with other ProjectReferences:**

```xml
<ProjectReference Include="..\zippo.lib\zippo.lib.csproj" />
```

---

### Task 11: Update unladen-swallow mod.toml for Assembly Sharing

**File to modify:** `unladen-swallow/mod.toml`

**Add `MeowSci.ZippoLib` to the `ImportedAssemblies` list** in the `[[StarMap.ModDependencies]]` section for zippo, OR add a new dependency block:

```toml
[[StarMap.ModDependencies]]
ModId = "zippo"
Optional = true
ImportedAssemblies = [
    "MeowSci.ZippoLib"
]
```

This ensures unladen-swallow's ALC delegates `MeowSci.ZippoLib` loading to zippo's ALC, so they share the same static state (e.g., `ZippoSubmod.Instance`).

**Check:** `unladen-swallow/mod.toml` to see if there are already dependency entries. Add this alongside existing ones.

---

### Task 12: Create OpenAPI Spec for Zippo Endpoints

**File to create:** `unladen-swallow.lib/openapi/zippo.yml`

**Structure:** Follow the exact pattern of `garrystorch.yml`:
- OpenAPI 3.1.0
- Server: `http://localhost:7887`
- Paths:
  - `GET /zippo/lights` — query param `vehicleId` (required)
  - `POST /zippo/lights/state` — request body `ZippoSetStateRequest`
  - `POST /zippo/animate` — request body `ZippoAnimateRequest`
  - `DELETE /zippo/animate` — request body `ZippoClearAnimationRequest`
- Components/Schemas:
  - `ZippoColor` (R, G, B floats)
  - `ZippoLightPartInfo`
  - `ZippoLightsListResponse`
  - `ZippoSetStateRequest` / `ZippoSetStateResponse`
  - `ZippoAnimColor`
  - `ZippoEasingConfig` / `ZippoEasingType` enum
  - `ZippoAnimateRequest` / `ZippoAnimateResponse`
  - `ZippoClearAnimationRequest` / `ZippoClearAnimationResponse`
  - Standard `ApiResponseBase`, `ErrorResponse`
- Include examples for each request body

---

### Task 13: Update UnladenSwallowSubmod UI Endpoint List

**File to modify:** `unladen-swallow.lib/UnladenSwallowSubmod.cs`

The submod's `RenderContent()` has a collapsing header that lists all endpoints. Add the new zippo endpoints to this list:

```
GET  /zippo/lights          — list light parts on a vehicle
POST /zippo/lights/state    — set color/intensity/enabled on a light
POST /zippo/animate         — queue a light animation
DEL  /zippo/animate         — clear animation queue
```

---

### Task 14: Update Documentation

**14a: Update `REPOSITORY_INDEX.md`**

Update the zippo entry to reflect the new animation system and RPC endpoints:

```markdown
### [zippo](zippo) / [zippo.lib](zippo.lib)
Light control and animation system. Selects vehicles and light parts, then controls their intensity and color using XKCD color palette. Supports queued single-step animations that interpolate both color and intensity with configurable easing functions.
- Vehicle and light part selection
- Light intensity control (0-1 slider)
- Light color: XKCD named color palette (950+ colors via filterable combobox) + custom color picker
- On/off toggle for lights
- **Animation system**: Queue-based single-step animations interpolating color and intensity from start to end values
  - Start/end color via color picker or named XKCD color
  - Start/end intensity
  - Configurable easing (Linear, EaseIn, EaseOut, EaseInOut) with power control
  - Duration in fractional seconds
  - Uninterruptible with max 25-deep queue per part
  - Manual controls locked during active animation
- Recursive part tree search for light components
- Real-time light property updates
- **Public API**: `ZippoSubmod.Instance` singleton, `GetLightPartInfos()`, `SetLightState()`, `QueueAnimation()`, `ClearAnimationQueue()` — exposed for use by `unladen-swallow.lib` RPC endpoints
```

Also update the unladen-swallow entry to list the new zippo endpoints.

**14b: Update `zippo/README.md`**

Add sections covering:
- The new animation system (architecture, usage, queue behavior)
- RPC API documentation with example requests/responses
- XKCD color helper
- Easing functions reference

---

### Task 15: Build and Verify

Run `dotnet build` on the entire solution to verify the implementation compiles clean:

```bash
dotnet build ksa-mod-experiments.slnx
```

Fix any compilation errors.

---

## Dependency Graph

```
ksa-abstractions.lib
    └── EasingHelper.cs (NEW - shared easing)
        ├── used by zippo.lib/LightAnimation.cs
        ├── used by garrys-torch.lib/WeldAnimation.cs (refactored)
        └── used by camera-controller-override.lib/AnimationHelpers.cs (refactored)

zippo.lib
    ├── LightController.cs (existing, minor additions)
    ├── XkcdColorHelper.cs (NEW)
    ├── LightAnimation.cs (NEW)
    ├── LightAnimationManager.cs (NEW)
    └── ZippoSubmod.cs (modified - animation UI + static API)

unladen-swallow.lib
    ├── ApiTypes.cs (modified - zippo DTOs)
    ├── ZippoLightsEndpoint.cs (NEW)
    ├── ZippoLightStateEndpoint.cs (NEW)
    ├── ZippoAnimateEndpoint.cs (NEW)
    ├── SwallowServer.cs (modified - route registration)
    ├── UnladenSwallowSubmod.cs (modified - endpoint list)
    └── openapi/zippo.yml (NEW)
```

## Risk Assessment

| Risk | Mitigation |
|---|---|
| Easing refactor breaks existing mods | Keep forwarding wrappers in camera-controller-override.lib; use `using` alias for type compatibility |
| Part.Id may not be stable across frames | Use Part.Id strings as keys (not Part references); resolve fresh each frame via `partResolver` |
| Reflection-based color lookup slow | Cache on first access (same as doh mod pattern); ~950 colors is trivial to cache |
| Queue full returns error | Return error string from `Enqueue()` so UI shows feedback and RPC returns 400 |
| Animation timing jitter | Use accumulated `ElapsedSeconds` with clamped output; snap to exact end values on completion |
| Thread safety for RPC | All game state access goes through `GameThread.Scheduler.Schedule()`; animation ticking happens on game thread via `Update(dt)` |

## Implementation Order

Execute tasks 1→15 in order. Each task should compile cleanly before moving to the next. Commit after each task per the mod-impl skill instructions.
