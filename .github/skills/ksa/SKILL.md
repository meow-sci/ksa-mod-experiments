---
name: ksa
description: details about the ksa game code and behavior
---

# KSA Mod Structure

**StarMap is a mod loader only.** It is used to run the game and link mods in at runtime. The only interaction with StarMap is through the C# lifecycle attribute annotations on the mod class — there is no other StarMap API to use.

Mods are C# 10 classes decorated with StarMap attributes:

```csharp
using StarMap.API;
using KSA;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  [StarMapImmediateLoad]  public void OnImmediateLoad() { }
  [StarMapAllModsLoaded]  public void OnFullyLoaded() { Patcher.Patch(); }
  [StarMapBeforeGui]      public void OnBeforeUi(double dt) { }
  [StarMapAfterGui]       public void OnAfterUi(double dt) { }
  [StarMapUnload]         public void Unload() { Patcher.Unload(); }
}
```

These attributes are the **complete** StarMap interface. Do not attempt to call other StarMap APIs or use StarMap for anything beyond these lifecycle hooks.

- HarmonyLib patching is done in `Patcher.cs`; call `Patcher.Patch()` in `OnFullyLoaded` and `Patcher.Unload()` in `Unload`
- Use `Console.WriteLine` for logging
- Guard all lifecycle methods with try/catch and log errors

## Researching KSA Game APIs

When you need to understand game types, APIs, or behavior:
- **Prefer the decompiled sources** in `decomp/ksa/` — they contain all available information and are much easier to read
- Do **not** attempt to inspect DLL files directly using shell commands or reflection tooling — use the decompiled sources instead

> **Important:** The decompiled sources may be outdated. The running binary can have a completely different internal structure — field names that appear in decompiled code may not exist at runtime. When in doubt, use the runtime reflection dump strategy to discover the real structure. See [debug.md](debug.md).

## Runtime Debugging

When decompiled source field names don't match the actual binary (reflection returns `null`, counts show `-1`, etc.):

- Use an ImGui **Dbg button** to trigger a reflection dump at runtime
- Walk the object graph, printing `GetType().FullName` and all fields via `BindingFlags.Public | NonPublic | Instance | DeclaredOnly`
- Pay special attention to `List<T>` / `IList` fields — the game may store typed components in a generic `Components` list rather than named fields
- Save the console output to a file (e.g. `<mod>/DEBUG`) for offline analysis

See [debug.md](debug.md) for complete helper code, the `DumpPartsWithComponents` pattern, and a worked example of how `LightModule+TemplateData` was discovered inside `PartTemplate.Components`.

# Universe & Vehicles

```csharp
var vehicles = Universe.CurrentSystem?.Vehicles.GetList(); // List<Vehicle>
Vehicle? controlled = Program.ControlledVehicle;           // currently player-controlled vehicle
double simTime = Universe.GetElapsedSimTime();
```

- `vehicle.Id` — string identifier
- `vehicle.Parent` — celestial body the vehicle orbits; must match between vehicles for teleport operations to be valid
- `vehicle.BodyRates` — `double3` angular velocity (rad/s); guard against NaN before use
- `vehicle.Body2Cce` — direct `doubleQuat` property (body frame → body-fixed frame)
- `vehicle.Orbit` — current orbital state; use `vehicle.Orbit.OrbitLineColor` when creating new orbits
- `vehicle.IsEditedVehicle` — `bool`, true when in VAB/editor

For physics data (AccelerationBody, NavBallData, FlightComputer, TotalMass, render override patching) see [vehicle-api.md](vehicle-api.md).

## Time

```csharp
var elapsed = Universe.GetElapsedSimTime(); // returns a time value
double seconds = elapsed.Seconds();          // convert to double seconds
```

## Celestial Body Properties

```csharp
vehicle.Parent.Mass        // double — body mass (kg)
vehicle.Parent.MeanRadius  // double — body mean radius (m)
vehicle.Parent.GetCci2Cce() // doubleQuat — CCI-to-CCE frame rotation
```

# Parts

## Regular Vehicles

Top-level parts are accessed via `vehicle.Parts.Parts`. Each `Part` has a `SubParts` collection forming a tree. Recurse to reach all parts:

```csharp
void SetPartScaleRecursive(Part part, float factor)
{
    part.Scale = new double3(factor, factor, factor);
    foreach (var sub in part.SubParts)
        SetPartScaleRecursive(sub, factor);
}

// Apply to all parts on a vehicle:
foreach (var part in vehicle.Parts.Parts)
    SetPartScaleRecursive(part, factor);
```

`part.Scale` is a `double3` — set all three axes to the same value for uniform scaling.

### Part Properties

- `part.Id` — string identifier (e.g. `"pixel_3_7_a"`)
- `part.DisplayName` — human-readable name
- `part.IsSubPart` — whether this is a child subpart
- `part.PartParent` — parent `Part` in the tree (nullable)
- `part.TreeChildren` — `IList<Part>` direct children

### Modules & Components

Parts contain typed modules accessed via generic `Get<T>()` calls:

```csharp
// All modules of type T on a single part and its subtree:
EngineController[] engines = part.SubtreeModules.Get<EngineController>();

// All modules of type T across the entire vehicle:
EngineController[] engines = vehicle.Parts.Modules.Get<EngineController>();
```

After modifying module state (e.g. activating/deactivating engines), call:

```csharp
vehicle.Parts.RecomputeAllDerivedData();
```

For engine control details see [vehicle-api.md](vehicle-api.md).

## KittenEva (EVA Kitten/Kerbal)

`KittenEva` is a special vehicle subtype. Detect it via:

```csharp
vehicle.GetType().Name == "KittenEva"
```

KittenEva renders through `CharacterAvatar.Core.Scale` (a `float` where `0.01f` = 1:1 game scale, i.e. multiply your desired factor by `0.01f`). Access it via reflection since it is not part of the public `Vehicle` API:

```csharp
var allFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
var renderable = vehicle.GetType().GetField("_renderable", allFlags)?.GetValue(vehicle);
var avatar = renderable?.GetType().GetField("_characterAvatar", allFlags)?.GetValue(renderable);
var coreField = avatar?.GetType().GetField("Core", allFlags);
var core = coreField?.GetValue(avatar);

// Try field first, then property
var scaleField = core?.GetType().GetField("Scale", allFlags);
var scaleProp  = core?.GetType().GetProperty("Scale", allFlags);
if (scaleField != null && scaleField.FieldType == typeof(float))
{
    scaleField.SetValue(core, factor * 0.01f);
    coreField!.SetValue(avatar, core); // write struct back
}
else if (scaleProp != null && scaleProp.PropertyType == typeof(float))
{
    scaleProp.SetValue(core, factor * 0.01f);
    coreField!.SetValue(avatar, core);
}
```

`vehicle.Parts.Parts` still iterates KittenEva parts but scaling them has no visual effect — the `Core.Scale` path above is what drives rendering. Apply both when doing a generic "scale any vehicle" implementation.

For the full KittenEva API including animations, expressions, and casting patterns see [kitten-eva.md](kitten-eva.md).

# 3D Positioning — Physics-Bypass Teleport

KSA uses double-precision coordinate frames:
- **CCI** (Celestial-Centered Inertial) — inertial absolute frame; used for positions and velocities
- **CCE** (Celestial-Centered Earth-fixed, i.e. body-fixed) — rotates with the parent body; used for orientation stored on vehicles
- **Body frame** — the vehicle's own local frame; `body2Cci` quaternion converts from it to CCI

To move a vehicle to an absolute position, bypassing all physics simulation, call `Teleport`. The pattern (e.g. "weld" source to target):

```csharp
double3 tgtPosCci     = target.GetPositionCci();
double3 tgtVelCci     = target.GetVelocityCci();
doubleQuat tgtBody2Cci = target.GetBody2Cci();

// Offset expressed in target's body frame (metres):
double3 offsetCci = new double3(offsetX, offsetY, offsetZ).Transform(tgtBody2Cci);
double3 newPosCci = tgtPosCci + offsetCci;

// Orientation: compose delta rotation with target orientation, then convert to CCE
doubleQuat deltaRot   = EulerDegreesToQuat(pitchDeg, yawDeg, rollDeg);
doubleQuat newBody2Cci = doubleQuat.Concatenate(deltaRot, tgtBody2Cci);
doubleQuat cci2Cce     = source.Parent.GetCci2Cce();
doubleQuat newBody2Cce = doubleQuat.Concatenate(newBody2Cci, cci2Cce).NormalizedOrZero();

Orbit newOrbit = Orbit.CreateFromStateCci(
    source.Parent,
    Universe.GetElapsedSimTime(),
    newPosCci,
    tgtVelCci,                 // match target velocity to stay co-moving
    source.Orbit.OrbitLineColor
);

source.Teleport(newOrbit, newBody2Cce, target.BodyRates);
```

Key points:
- `Teleport` takes `(Orbit, doubleQuat body2Cce, double3 bodyRates)` — it overwrites physics state completely each frame
- Always call `.NormalizedOrZero()` on computed quaternions before passing to `Teleport`
- `doubleQuat.Concatenate(q1, q2)` composes rotations (q2 applied first, then q1 — same convention as `Quaternion.Concatenate` in .NET)
- `source.Parent` must equal `target.Parent`; validate before teleporting or the coordinate math is invalid
- To maintain a locked relative position, call `Teleport` every frame (e.g. in `OnAfterUi`)
- Guard `BodyRates` for NaN, especially when rotation is unlocked: `if (double.IsNaN(rates.X) || ...) rates = double3.zero;`

## Euler to Quaternion (ZYX intrinsic)

```csharp
doubleQuat EulerDegreesToQuat(float pitchDeg, float yawDeg, float rollDeg)
{
    double cp = Math.Cos(pitchDeg * Math.PI / 360), sp = Math.Sin(pitchDeg * Math.PI / 360);
    double cy = Math.Cos(yawDeg   * Math.PI / 360), sy = Math.Sin(yawDeg   * Math.PI / 360);
    double cr = Math.Cos(rollDeg  * Math.PI / 360), sr = Math.Sin(rollDeg  * Math.PI / 360);
    var qPitch = new doubleQuat(sp, 0,  0,  cp);
    var qYaw   = new doubleQuat(0,  sy, 0,  cy);
    var qRoll  = new doubleQuat(0,  0,  sr, cr);
    return doubleQuat.Concatenate(doubleQuat.Concatenate(qYaw, qPitch), qRoll);
}
// doubleQuat constructor: (x, y, z, w)
```

# Colors

`KSAColor.Xkcd` provides named colors. Cast to `(float4)` for ImGui:

```csharp
ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "label");
ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32((float4)KSAColor.Xkcd.HotPink));
```

Notable names: `Custard`, `RadioactiveGreen`, `Orangeish`, `GreenApple`, `OrangishRed`, `BrightMagenta`, `HotPink`, `CanaryYellow`, `BrightLightBlue`.

# Camera Controller Patching

KSA cameras (`OrbitController`, `FlyController`) can be intercepted via Harmony prefix on `OnFrame`. Return `false` to suppress default camera behavior. Camera uses **ECL (Ecliptic)** coordinates (distinct from vehicle CCI/CCE frames).

See [camera.md](camera.md) for full details including `Transform3D`, `Controller.Camera.Following`, orbit math, and look-at helpers.

# Numerics

## Types

| Precision | Scalar | Vector | Matrix | Quaternion |
|-----------|--------|--------|--------|------------|
| 32-bit | `float` | `float2`, `float3`, `float4` | `float4x4` | `floatQuat` |
| 64-bit | `double` | `double3`, `double4` | `double4x4` | `doubleQuat` |

All from `Brutal.Numerics`.

## Common Operations

```csharp
double3.Normalize(v)     // normalize vector
v.Length()               // vector magnitude
double3.Dot(a, b)        // dot product
double3.Cross(a, b)      // cross product
double3.Lerp(a, b, t)    // linear interpolation (t ∈ [0,1])
doubleQuat.Slerp(a, b, t) // spherical linear interpolation
v.Transform(quat)        // rotate vector by quaternion
float3.Pack(in double3)  // double3 → float3
floatQuat.Pack(doubleQuat) // doubleQuat → floatQuat
float4x4.CreateTranslation(float3)
float4x4.CreateFromQuaternion(floatQuat)
```

# Audio

```csharp
var music = ModLibrary.Get<MusicPlayList>("AssetName");
music.PlayMusic(out ChannelWrapper? channel);

var sound = ModLibrary.Get<MultiSound>("AssetName");
sound.Play();
```

Assets are defined in an `Assets.xml` file in the mod directory.
