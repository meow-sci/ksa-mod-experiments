# Historical navball research

Reference notes; verify game symbols against the current sibling game-assemblies checkout before using them. See [current integration baseline](scope/FULL_SCOPE.md).

# NavBall — Decompiled Code Analysis

> Research summary of KSA's NavBall system, intended to inform any KROC feature that reads attitude data or adds custom overlays.

---

## Key Files

| File | Size | Purpose |
|------|------|---------|
| `decomp/ksa/KSA/NavBallRenderer.cs` | ~3.5 KB | Vulkan sphere renderer + transform math |
| `decomp/ksa/KSA/NavBallData.cs` | ~600 B | Data struct (fields below) |
| `decomp/ksa/KSA/Vehicle.cs` | large | `UpdateNavBallData()` ~L1238–L1345 |
| `decomp/ksa/KSA/GaugeButtonNavBallMode.cs` | ~1.5 KB | HUD button that cycles reference frames |
| `decomp/ksa/KSA/UboVesselData.cs` | small | GPU UBO containing the NavBall rotation matrix |
| `decomp/ksa/KSA/FlightComputerAttitudeTrackTargetEx.cs` | small | Maps autopilot targets → reference frames |

---

## Rendering Architecture

The NavBall is **not ImGui** — it is a pure **Vulkan** render using `UnlitMeshRenderTechnique` shaders.

Two mesh passes are drawn every frame:

| Pass | Mesh asset | Texture asset | Transform |
|------|-----------|--------------|-----------|
| 0 | `"NavballMesh"` | `"Navball"` | Rotation + Translation + Scale (rotates with attitude) |
| 1 | `"NavballMeshDetails"` | `"NavballDetails"` | Translation + Scale only (static overlay) |

Pass 1 is a static decal layer (horizon line, heading marks). All markings are **baked into textures** — there are no separate marker objects.

The render uses an **orthographic projection** (`viewport.Width × viewport.Height`, z-range `[0.01, 1000]`). The sphere sits at `z = -1` in HUD space (scale `10f` world units).

### Screen Position

```
center_screen_px = (241,  viewport.Height − 209)   // from top-left
sphere_radius_px ≈ 91                               // (NAVBALL_WIDTH − NAVBALL_WIDTH_MARGIN) / 2
```

Constants in `NavBallRenderer`:

```csharp
NAVBALL_WIDTH         = 282f
NAVBALL_HEIGHT        = 258f
NAVBALL_WIDTH_MARGIN  = 100f
NAVBALL_HEIGHT_MARGIN =  80f
```

---

## `NavBallData` Struct

Lives as `private NavBallData _navBallData` on `Vehicle`, exposed as:

```csharp
public ref readonly NavBallData NavBallData { get; }
```

| Field | Type | Meaning |
|-------|------|---------|
| `Frame` | `VehicleReferenceFrame` | Active reference frame |
| `Navball2Body` | `doubleQuat` | Quaternion: reference-frame → body space |
| `AttitudeAngles` | `int3` | Roll / Pitch / Yaw in integer degrees |
| `AttitudeRates` | `double3` | Angular rates in rad/s |
| `Speed` | `double` | Speed in current frame (m/s) |
| `Altitude` | `double` | Altitude (barometric or radar) in metres |
| `DeltaVInVacuum` | `double` | Remaining ΔV (Tsiolkovsky) |
| `ThrustWeightRatio` | `double` | Current T/W ratio |

---

## `VehicleReferenceFrame` Enum

```csharp
EclBody   // Ecliptic / inertial (fixed stars)
EnuBody   // East-North-Up (surface-relative)
Lvlh      // Local Vertical Local Horizontal (orbital)
VlfBody   // Velocity Local Frame (prograde = up)
BurnBody  // Active maneuver / burn direction
Dock      // Target-relative (docking)
```

---

## How `Navball2Body` is Computed

`Vehicle.UpdateNavBallData()` runs each physics tick:

$$q_{Navball2Body} = \text{Concat}(q_{frame→CCI},\; q_{body→CCI}^{-1})$$

So `Navball2Body` represents *"how is the reference frame oriented relative to the vehicle body."*

---

## Transform: Quaternion → Render Matrix

`NavBallRenderer.GetNavBallTransform(Vehicle)` applies a **coordinate-axis permutation** before inverting:

$$M_{axes} = \begin{pmatrix} 0 & 1 & 0 \\ 0 & 0 & 1 \\ -1 & 0 & 0 \end{pmatrix}$$

i.e. NavBall-X ← game-Y, NavBall-Y ← game-Z, NavBall-Z ← −game-X.

```csharp
renderMatrix = M_axes × float4x4.CreateFromQuaternion(rotation.Inverse())
```

This matrix is also stored in `_vesselData[viewport.Index].LocalRotation` and uploaded to the GPU.

There is a second overload `GetNavBallTransform(Viewport, out float4x4 rotation, out float4x4 translation, out float4x4 scale)` that returns all three components separately.

Both overloads are **`public static`** — accessible from mod code.

---

## Projecting a Direction Vector onto the NavBall

To place a screen-space marker for any unit direction vector `v` (in the same space as `Navball2Body`):

```csharp
// 1. Get the render rotation matrix
float4x4 rot = NavBallRenderer.GetNavBallTransform(vehicle);

// 2. Transform the direction into NavBall-screen space
float3 navPos = float3.Transform(v.Normalized(), rot);

// 3. Only draw if the point faces the viewer (front hemisphere)
if (navPos.Z > 0)
{
    const float radius = 91f;  // sphere radius in screen pixels
    float2 center = new float2(241f, viewport.Height - 209f);
    // screen-Y is inverted vs orthographic-Y
    float2 screenPos = center + new float2(navPos.X, -navPos.Y) * radius;
    // draw marker at screenPos
}
```

---

## Adding Custom Markers — Recommended Approach

There is **no mod hook or extension API** on `NavBallRenderer`. The sphere is fully Vulkan, with no ImGui overlay sitting on top of it. The options are:

### Option A — `ImGui.GetForegroundDrawList()`
Draws on top of all Vulkan and ImGui content. Available inside any ImGui frame:

```csharp
ImDrawListPtr dl = ImGui.GetForegroundDrawList();

// Example: cyan dot at NavBall centre
dl.AddCircleFilled(new System.Numerics.Vector2(241f, viewport.Height - 209f), 6f, 0xFFFFFF00u);

// Marker for an arbitrary direction vector
float4x4 rot = NavBallRenderer.GetNavBallTransform(vehicle);
float3 navPos = float3.Transform(direction, rot);
if (navPos.Z > 0)
{
    float2 pos = center + new float2(navPos.X, -navPos.Y) * 91f;
    dl.AddCircle(new System.Numerics.Vector2(pos.X, pos.Y), 8f, 0xFF00FF00u, 24, 2f);
}
```

### Option B — Asset Replacement
`ModLibrary` is used in `NavBallRenderer` to load `"Navball"` and `"NavballDetails"` textures. If the KSA mod loader allows asset replacement, the detail texture could be swapped to add permanent baked-in markings.

### Option C — Exposing Data via HTTP (KROC)
Read `Program.ControlledVehicle.NavBallData` and serve it as JSON. Clients can then render overlays externally (e.g. in a web dashboard). This is the lowest-risk path and fits the KROC architecture.

---

## Public API Surface Available to Mods

```csharp
// Attitude data
var data = Program.ControlledVehicle.NavBallData;
// data.Navball2Body, data.AttitudeAngles, data.AttitudeRates, data.Frame …

// Change reference frame
vehicle.SetNavBallFrame(VehicleReferenceFrame.Lvlh);

// Rendering matrices (for screen-space projection)
float4x4 rot = NavBallRenderer.GetNavBallTransform(vehicle);
bool ok = NavBallRenderer.GetNavBallTransform(viewport, out float4x4 rotation, out float4x4 translation, out float4x4 scale);
```

---

## What Does NOT Exist

- No `AddMarker()` / `RegisterIndicator()` / `INavBallOverlay` API
- No event hooks on `NavBallRenderer`
- No separate prograde/retrograde/normal marker objects — all markings are baked textures
- No ImGui draw-list that overlays the NavBall natively (the Vulkan pass and ImGui layers are separate)
