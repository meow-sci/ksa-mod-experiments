# Gary's Torch - Vehicle Welding Mod

A mod that "welds" two vehicles together by continuously updating one vehicle's position and orientation to maintain a fixed offset relative to another vehicle.

## Concept

Since KSA doesn't support physically attaching two separate vehicles (without docking), we fake it:

1. User selects a **source** vehicle (the one that gets moved) and a **target** vehicle (the anchor)
2. Record the positional and rotational offset between them at the moment of "welding"
3. Every frame, compute where the source *should* be relative to the target's current position/orientation
4. Teleport the source vehicle to that computed position

## Key API Discoveries

### Accessing Vehicles

```csharp
// Currently controlled vehicle
Vehicle controlled = Program.ControlledVehicle;

// All vehicles in the system
var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
```

### Vehicle Position (CCI Frame - Inertial)

Position and velocity in the **CCI** (Celestial Coordinate Inertial) frame are the most stable reference for computing offsets between two vehicles orbiting the same parent body.

```csharp
// Position in inertial frame relative to parent body
double3 posCci = vehicle.GetPositionCci();   // wraps Orbit.GetPositionCci()
double3 velCci = vehicle.GetVelocityCci();   // wraps Orbit.GetVelocityCci()

// Orientation
doubleQuat body2Cce = vehicle.Body2Cce;               // body-to-parent-surface frame
doubleQuat body2Cci = vehicle.GetBody2Cci();           // = Body2Cce ⊙ Parent.GetCce2Cci()
double3 bodyRates   = vehicle.BodyRates;               // angular velocity in body frame
```

### Creating an Orbit from Position + Velocity

`Orbit.CreateFromStateCci` constructs a full orbit from raw state vectors — this is how we'll position the source vehicle each frame:

```csharp
Orbit newOrbit = Orbit.CreateFromStateCci(
    parent: targetVehicle.Parent,     // IParentBody (the celestial body they orbit)
    stateTime: Universe.GetElapsedSimTime(),
    positionCci: newPositionCci,      // double3 - where we want the vehicle
    velocityCci: newVelocityCci,      // double3 - matching velocity
    orbitLineColor: sourceVehicle.Orbit.OrbitLineColor
);
```

### Teleporting a Vehicle

The `Vehicle.Teleport` method is the main entry point for moving vehicles:

```csharp
public void Teleport(Orbit? orbit, doubleQuat? body2Cce, double3? bodyRates)
{
    // Creates FlightPlan from orbit, computes trajectory
    // Updates Body2Cce and BodyRates if provided
    // Calls _lastKinematicStates.UpdateFromAnalytic(...)
    // Schedules planet update
}
```

Full teleport with all three parameters:
```csharp
sourceVehicle.Teleport(newOrbit, newBody2Cce, newBodyRates);
```

### Coordinate Frame Chain

```
Body Frame  --[Body2Cce]-->  CCE (parent surface)  --[Cce2Cci]-->  CCI (inertial)
                                                                         |
                                                           [Parent.GetPositionEcl()]
                                                                         v
                                                                    ECL (ecliptic)
```

For computing offsets between two vehicles orbiting the **same parent body**, CCI is the correct frame — it's inertial, so offsets don't rotate with the parent body.

## Architecture

### State Machine

```
IDLE  -->  SELECTING_TARGET  -->  WELDED  -->  IDLE
  ^                                  |
  +----------------------------------+
           (user unweld / unload)
```

### Data Model

```csharp
// Weld state stored in Mod class
Vehicle? _sourceVehicle;    // vehicle being moved (the controlled vehicle when weld initiated)
Vehicle? _targetVehicle;    // vehicle acting as the anchor

// Offset captured at weld time, in target's body frame
double3 _offsetInTargetBody;      // positional offset in target body-local coords
doubleQuat _rotationOffset;       // relative rotation: source.Body2Cci * target.Body2Cci.Inverse()
```

**Why store offset in target's body frame?** Because as the target rotates (due to attitude control, tumbling, etc.), we want the source to rotate with it — like a real weld. If we stored the offset in CCI, the target could spin and the source would stay fixed in space.

## Detailed Task List

### Task 1: Vehicle Picker UI

**File: `Mod.cs` — `RenderWindow()` method**

Replace the placeholder UI with a vehicle selection list. When in IDLE state, show a list of all vehicles (excluding the controlled one) the user can select as the weld target.

```csharp
// Get all vehicles
var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
if (vehicles == null) return;

var controlled = Program.ControlledVehicle;
if (controlled == null)
{
    ImGui.Text("No controlled vehicle");
    return;
}

// List other vehicles as selectable targets
foreach (var v in vehicles)
{
    if (v == controlled) continue;
    if (ImGui.Selectable(v.Id))
    {
        _targetVehicle = v;
        _sourceVehicle = controlled;
        // Proceed to weld
    }
}
```

### Task 2: Capture Weld Offset

**File: `Mod.cs` — new method `InitiateWeld()`**

When user clicks a target, capture the offset between source and target in the target's body frame.

```csharp
private void InitiateWeld()
{
    // Get positions in CCI (inertial frame, relative to shared parent body)
    double3 srcPosCci = _sourceVehicle.GetPositionCci();
    double3 tgtPosCci = _targetVehicle.GetPositionCci();

    // Offset in CCI
    double3 offsetCci = srcPosCci - tgtPosCci;

    // Transform offset into target's body frame so it rotates with the target
    doubleQuat tgtBody2Cci = _targetVehicle.GetBody2Cci();
    doubleQuat cci2TgtBody = tgtBody2Cci.Inverse();
    _offsetInTargetBody = offsetCci.Transform(cci2TgtBody);

    // Capture relative rotation: how source is oriented relative to target
    doubleQuat srcBody2Cci = _sourceVehicle.GetBody2Cci();
    _rotationOffset = doubleQuat.Concatenate(srcBody2Cci, cci2TgtBody);
    // _rotationOffset = srcBody2Cci * tgtBody2Cci^-1
    // To recover: srcBody2Cci = _rotationOffset * tgtBody2Cci

    _isWelded = true;

    Console.WriteLine($"garys-torch: Welded {_sourceVehicle.Id} to {_targetVehicle.Id}");
    Console.WriteLine($"garys-torch: Offset (target body): {_offsetInTargetBody}");
}
```

### Task 3: Per-Frame Position Update

**File: `Mod.cs` — called from `OnAfterUi(double dt)` when `_isWelded`**

Each frame, recompute where the source should be and teleport it there.

```csharp
private void UpdateWeld()
{
    if (_sourceVehicle == null || _targetVehicle == null) return;

    // Check vehicles are still valid and share the same parent body
    if (_sourceVehicle.Parent != _targetVehicle.Parent)
    {
        Console.WriteLine("garys-torch: Parent body mismatch, unwelding");
        Unweld();
        return;
    }

    // Current target state in CCI
    double3 tgtPosCci = _targetVehicle.GetPositionCci();
    double3 tgtVelCci = _targetVehicle.GetVelocityCci();
    doubleQuat tgtBody2Cci = _targetVehicle.GetBody2Cci();

    // Compute source position: transform stored body-frame offset back to CCI
    double3 offsetCci = _offsetInTargetBody.Transform(tgtBody2Cci);
    double3 newSrcPosCci = tgtPosCci + offsetCci;

    // Match velocity (welded = same velocity as target, plus any rotational contribution)
    // For simplicity, just match target velocity — proper rigid body would add ω×r
    double3 newSrcVelCci = tgtVelCci;

    // Compute source orientation
    // srcBody2Cci = _rotationOffset ⊙ tgtBody2Cci
    doubleQuat newSrcBody2Cci = doubleQuat.Concatenate(_rotationOffset, tgtBody2Cci);

    // Convert Body2Cci back to Body2Cce (what Teleport expects)
    doubleQuat cci2Cce = _sourceVehicle.Parent.GetCci2Cce();
    doubleQuat newSrcBody2Cce = doubleQuat.Concatenate(newSrcBody2Cci, cci2Cce);

    // Match body rates (angular velocity) from target
    double3 newBodyRates = _targetVehicle.BodyRates;

    // Create new orbit from computed CCI state vectors
    Orbit newOrbit = KSA.Orbit.CreateFromStateCci(
        _sourceVehicle.Parent,
        Universe.GetElapsedSimTime(),
        newSrcPosCci,
        newSrcVelCci,
        _sourceVehicle.Orbit.OrbitLineColor
    );

    // Teleport source to new position
    _sourceVehicle.Teleport(newOrbit, newSrcBody2Cce, newBodyRates);
}
```

### Task 4: Unweld Action

**File: `Mod.cs` — new method `Unweld()`**

```csharp
private void Unweld()
{
    if (_sourceVehicle != null && _targetVehicle != null)
        Console.WriteLine($"garys-torch: Unwelded {_sourceVehicle.Id} from {_targetVehicle.Id}");

    _sourceVehicle = null;
    _targetVehicle = null;
    _isWelded = false;
    _offsetInTargetBody = default;
    _rotationOffset = default;
}
```

### Task 5: Full UI Layout

**File: `Mod.cs` — `RenderWindow()` method**

Three states to render:

1. **No controlled vehicle**: Message telling user to control a vehicle first
2. **IDLE (not welded)**: Show vehicle picker list with "Weld To" buttons
3. **WELDED**: Show weld status (source name, target name, offset), "Unweld" button

```csharp
private void RenderWindow()
{
    ImGui.SetNextWindowSize(new float2(400, 300), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Gary's Torch###garys-torch", ref _windowVisible))
    {
        var controlled = Program.ControlledVehicle;
        if (controlled == null)
        {
            ImGui.Text("Control a vehicle first.");
        }
        else if (_isWelded)
        {
            // Welded state
            ImGui.TextColored(new float4(0f, 1f, 0f, 1f), "WELDED");
            ImGui.Text($"Source: {_sourceVehicle?.Id}");
            ImGui.Text($"Target: {_targetVehicle?.Id}");
            ImGui.Separator();
            if (ImGui.Button("Unweld"))
                Unweld();
        }
        else
        {
            // Vehicle picker
            ImGui.Text($"Controlled: {controlled.Id}");
            ImGui.Separator();
            ImGui.Text("Select target to weld to:");

            var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
            if (vehicles != null)
            {
                foreach (var v in vehicles)
                {
                    if (v == controlled) continue;
                    if (ImGui.Button($"Weld to: {v.Id}"))
                    {
                        _sourceVehicle = controlled;
                        _targetVehicle = v;
                        InitiateWeld();
                    }
                }
            }
        }
    }
    ImGui.End();
}
```

### Task 6: Integration in Game Loop

**File: `Mod.cs` — `OnAfterUi(double dt)` method**

```csharp
[StarMapAfterGui]
public void OnAfterUi(double dt)
{
    if (!_isInitialized || _isDisposed) return;

    if (ImGui.IsKeyPressed(ImGuiKey.F11))
        _windowVisible = !_windowVisible;

    // Per-frame weld update
    if (_isWelded)
        UpdateWeld();

    if (_windowVisible)
        RenderWindow();
}
```

## Implementation Order

1. **Add fields** to `Mod.cs` for weld state (`_sourceVehicle`, `_targetVehicle`, `_offsetInTargetBody`, `_rotationOffset`, `_isWelded`)
2. **Implement `InitiateWeld()`** — capture offset
3. **Implement `UpdateWeld()`** — per-frame teleport
4. **Implement `Unweld()`** — cleanup
5. **Update `RenderWindow()`** — vehicle picker + weld status UI
6. **Update `OnAfterUi()`** — call `UpdateWeld()` in game loop
7. **Build and test** — `dotnet build`

## Known Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Vehicles have different parent bodies | Check `Parent` match before welding; auto-unweld on SOI change |
| Physics simulation fights the teleport (orbit propagation moves vehicle away each frame) | We teleport every frame in `OnAfterUi`, overriding physics. May cause visual jitter — monitor. |
| `Teleport()` calls `FlightPlan.ComputeCompleteTrajectory()` which is expensive | May need to throttle updates or find lighter-weight position-setting approach if performance is an issue |
| Source vehicle is the controlled vehicle and player tries to maneuver | The weld loop will override any player input. This is expected behavior while welded. |
| Quaternion math order wrong | KSA uses `doubleQuat.Concatenate(a, b)` which likely follows row-major convention. Test and verify offset direction. |
| Time warp | At high warp, frame rate drops. If `OnAfterUi` runs less frequently, source may visibly lag behind target. Not a launch blocker. |

## Potential Future Enhancements

- Adjustable offset (nudge controls in UI)
- Multiple weld pairs
- Weld to celestial bodies (not just vehicles)
- Velocity matching including rotational contribution (ω × r term)
- Persist weld state across save/load