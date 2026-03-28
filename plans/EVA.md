# EVA System Reference — KittenEva On-Demand Vehicle Instantiation & IVA Door Ingress/Egress

> Research compiled from decompiled KSA source code under `decomp/ksa/KSA/`.

---

## Overview

KSA treats EVA kittens as full `Vehicle` entities. When a kitten exits a vehicle through an `EVADoor`, the game instantiates a new `KittenEva : Vehicle` on-demand at runtime, positioned near the door. When the kitten re-enters (or is otherwise removed), the `KittenEva` vehicle is destroyed. This system enables mods to programmatically spawn and destroy EVA kittens.

### Key Source Files

| File | Purpose |
|------|---------|
| `KittenEva.cs` | EVA vehicle class (extends `Vehicle`) |
| `EVADoor.cs` | Module on parts that enables EVA egress |
| `EVADoorTemplate.cs` | Template data for EVADoor module |
| `IVAController.cs` | First-person camera controller inside vehicles |
| `IVASeat.cs` | Seat position/orientation inside vehicles |
| `Vehicle.cs` | Base vehicle class with creation/destruction APIs |
| `CelestialSystem.cs` | Universe-level vehicle registration/deregistration |
| `VehicleTemplate.cs` | Vehicle blueprint (distinguishes KittenEva via `Character` field) |
| `VehicleData.cs` | Full runtime vehicle state for serialization |
| `CharacterAvatar.cs` | Character model, animations, expressions |
| `FlyController.cs` | Free-flight camera/movement controller |
| `Situation.cs` / `SituationEx.cs` | State machine for vehicle situation (Freefall, Landed, etc.) |

---

## KittenEva Class

```csharp
public class KittenEva : Vehicle
{
    public CharacterReference Character;          // Character identity (appearance, animations)
    private KittenRenderable _renderable;         // Kitten model renderer
    public override OrbitLineMode OrbitLineMode => OrbitLineMode.Dynamic;  // Dynamic orbit lines
}
```

- Inherits all `Vehicle` functionality (physics, orbit, parts, serialization)
- Uses `CameraReferenceFrame.Chase` (camera follows from behind)
- Root part is a backpack with MMH/NTO propellant for maneuvering thrusters

---

## Instantiation — 3 Creation Paths

### Path 1: On-Demand from EVADoor (Primary — User Action)

**Trigger:** Player clicks "EVA Kitten" button in the context menu of a part with an `EVADoor` module.

**Code flow in `EVADoor.cs`:**

```csharp
// 1. Generate kitten identity
VehicleData vehicleData = CreateKittenData();
//    - Name: "Kitten_1", "Kitten_2", etc. (auto-incrementing)
//    - Character: random from ModLibrary.AllCharacters

// 2. Create backpack equipment
Part backPackPart = GetBackPackPart();
//    - Loads "KittenBackPackPart" from ModLibrary.AllParts
//    - Configures tanks with MMH_NTO_1.6 combustion process
//    - Fills consumables

// 3. Instantiate KittenEva
KittenEva kittenEva = new KittenEva(
    Universe.CurrentSystem,       // Current celestial system
    vehicleData.Character,        // Character ID string
    vehicle.Body2Cce,             // Orientation from parent vehicle
    vehicle.BodyRates,            // Angular velocity from parent vehicle
    vehicle.Parent,               // Orbital parent body (planet, etc.)
    vehicleData.Id,               // Unique kitten ID
    backPackPart,                 // Backpack as root part
    vehicle.Orbit                 // Initial orbit (same as parent)
);

// 4. Position relative to door
double3 offset = (kittenEva.CenterOfMassAsmb - vehicle.CenterOfMassAsmb
    + Parent.PositionVehicleAsmb).Transform(vehicle.GetAsmb2Cci());

StateVectors stateVectors = vehicle.GetStateVectorsCci();
Orbit orbit = Orbit.CreateFromStateCci(
    vehicle.Parent,
    stateVectors.StateTime,
    stateVectors.PositionCci + offset,    // Door position offset
    stateVectors.VelocityCci,
    vehicle.OrbitColor
);

// 5. Teleport to calculated orbit and register
kittenEva.Teleport(orbit, null, null);
vehicle.Parent.Children.Add(kittenEva);
kittenEva.UpdatePerFrameData();

// 6. Transfer player control
Program.ControlledVehicle = kittenEva;
hoveredCamera.SetFollow(kittenEva, tidalLocking: true);
```

### Path 2: From VehicleTemplate (System Initialization)

```csharp
// In VehicleTemplate.CreateInto():
Vehicle vehicle = ((Character != null)
    ? KittenEva.CreateKittenEva(celestialSystem, this, parent, id)
    : Vehicle.CreateVehicle(celestialSystem, this, parent, id));
```

If a `VehicleTemplate` has a non-null `Character`, it creates a `KittenEva` instead of a regular `Vehicle`.

### Path 3: From Save Game (Deserialization)

```csharp
// In CelestialSystem loading:
if (string.IsNullOrEmpty(vehicleData.Character))
    Vehicle.CreateVehicleFromSaveGameData(this, vehicleData);
else
    KittenEva.CreateKittenFromSaveData(this, vehicleData);
```

The `Character` field in `VehicleData` distinguishes EVA kittens from regular vehicles during deserialization.

---

## Destruction

`KittenEva` does **not** override destruction methods — it uses the base `Vehicle` implementations:

### Vehicle.Dispose()
```csharp
public void Dispose()
{
    _groundTrackWindow?.Dispose();         // Dispose ground track display
    Parent?.Children.Remove(this);         // Remove from parent body's children
    GameAudio.Deregister(this);            // Deregister from audio system
    Parts.Dispose();                       // Dispose all parts
}
```

### Vehicle.Destroy(bool stopAudio, bool disposeParts)
```csharp
public void Destroy(bool stopAudio, bool disposeParts)
{
    _beingDestroyed = true;                // Flag to prevent further audio updates
    _groundTrackWindow?.Dispose();
    Parent?.Children.Remove(this);
    if (stopAudio) GameAudio.Deregister(this);
    if (disposeParts) Parts.Dispose();
}
```

### System-Level: CelestialSystem.Deregister()
```csharp
public void Deregister(Astronomical astronomical)
{
    _all.Deregister(astronomical);
    if (astronomical is Vehicle vehicle)
    {
        _vehicles.Deregister(vehicle);
        vehicle.Parent?.Children.Remove(vehicle);
    }
}
```

All destruction is **synchronous** — no async cleanup. Character avatar and animations are garbage collected.

---

## EVADoor Module — Part Integration

### Template Definition

`EVADoorTemplate` is minimal — presence in a `PartTemplate` enables the EVA door:

```csharp
public class EVADoorTemplate : IDataReference
{
    public virtual void OnDataLoad(Mod mod) { }
    public bool IsValid() { return true; }
}
```

In part XML: `<EVADoor></EVADoor>` element on a `PartTemplate` enables the door.

### Module Registration Pattern

```csharp
// EVADoor registers via the ModuleList system:
public static void CreateComponents(Part part, PartTemplate template, PartInstance? instance)
{
    if (template.EVADoor != null)
    {
        EVADoor module = new EVADoor { Parent = part };
        part.Modules.Add(module);
    }
}

// Registered globally:
ModuleList.Register(EVADoor.CreateComponents);
```

### Context Menu Integration

Parts iterate subtree modules to find `EVADoor` instances:
```csharp
Span<EVADoor> span = SubtreeModules.Get<EVADoor>();
foreach (ref EVADoor door in span)
    door.ShowContextMenu(vehicle);
```

---

## IVA System (Inside Vehicle)

### IVAController
Camera controller for first-person view inside vehicles:
- **`IVASeat Seat`** — Current seat reference
- **`OnFrame()`** — Updates camera position relative to seat in assembly coordinates
- **`OnKey()`** — Handles seat switching (`InputAction.IVASwitchToNextSeat`)
- **`OnSwitchOn/Off()`** — Enter/exit IVA mode

### IVASeat
Module representing a seat inside a vehicle:
- **`double3 PositionAsmb`** — Seat position in assembly-relative coordinates
- **`double3 ForwardAxisAsmb`** — Forward direction (default: X-axis)
- **`double3 UpAxisAsmb`** — Up direction (default: negative Z-axis)

---

## EVA Lifecycle & Situation States

### Situation State Machine

KittenEva participates in the standard vehicle situation system:

```
SurfaceContact.None   + OnRails=true  → Freefall     (default EVA state in orbit)
SurfaceContact.None   + OnRails=false → Maneuvering  (under thruster control)
SurfaceContact.Terrain + OnRails=false → Rolling      (sliding on surface)
SurfaceContact.Terrain + OnRails=true  → Landed       (stationary on terrain)
SurfaceContact.Ocean   + OnRails=false → Sailing      (moving in water)
SurfaceContact.Ocean   + OnRails=true  → Floating     (stationary in water)
```

### Character States

```csharp
public enum CharacterState
{
    Walking,   // Ground movement animation
    Mmu,       // EVA/MMU (Manned Maneuvering Unit) — jetpack state
    Cockpit    // Inside vehicle
}
```

### FlyController (EVA Free-Flight)

| Control | Speed |
|---------|-------|
| Forward/Backward/Left/Right/Up/Down | 50 m/s (normal), 100 m/s (sprint) |
| Roll Left/Right | 25 rad/s |
| Mouse scroll | Exponential speed scaling |

Camera reference frames: Surface (ENU), Orbit (LVLH), Chase (body-following), Stars (inertial).

---

## How to Use This in a Mod

### Programmatically Spawning a KittenEva

```csharp
// Get system references
CelestialSystem system = Universe.CurrentSystem;
Vehicle parentVehicle = Program.ControlledVehicle;

// 1. Create backpack part
PartTemplate? backpackTemplate = ModLibrary.AllParts.Find("KittenBackPackPart");
Part backpackPart = Part.Create(backpackTemplate, null);
// Configure tanks if needed
if (SubstanceLibrary.TryGetCombustionProcess("MMH_NTO_1.6", out var combustion))
{
    // Configure propellant tanks on the backpack
    backpackPart.Tree.RefillConsumables();
}

// 2. Pick a character
CharacterReference character = ModLibrary.AllCharacters[randomIndex];

// 3. Create the KittenEva
KittenEva eva = new KittenEva(
    system,
    character.Id,
    parentVehicle.Body2Cce,
    parentVehicle.BodyRates,
    parentVehicle.Parent,
    "MyMod_Kitten_1",           // Unique ID
    backpackPart,
    parentVehicle.Orbit
);

// 4. Position relative to parent vehicle (offset from center of mass)
double3 offset = /* calculate desired offset */;
StateVectors sv = parentVehicle.GetStateVectorsCci();
Orbit orbit = Orbit.CreateFromStateCci(
    parentVehicle.Parent,
    sv.StateTime,
    sv.PositionCci + offset,
    sv.VelocityCci,
    parentVehicle.OrbitColor
);

// 5. Place and register
eva.Teleport(orbit, null, null);
parentVehicle.Parent.Children.Add(eva);
eva.UpdatePerFrameData();

// 6. Optionally give player control
Program.ControlledVehicle = eva;
```

### Programmatically Destroying a KittenEva

```csharp
CelestialSystem system = Universe.CurrentSystem;

// Find by ID
if (system.Vehicles.TryGet("MyMod_Kitten_1", out Vehicle vehicle))
{
    system.Deregister(vehicle);
    vehicle.Dispose();
}

// Or destroy all EVA kittens
var vehicles = system.Vehicles.GetList();
for (int i = vehicles.Count - 1; i >= 0; i--)
{
    if (vehicles[i] is KittenEva eva)
    {
        system.Deregister(eva);
        eva.Dispose();
    }
}
```

### Creating a Custom EVA Door Module

Follow the `EVADoor` pattern to create a custom module that can trigger EVA:

```csharp
public class MyCustomEVADoor : Module<MyCustomEVADoor>, IDisposable
{
    // Register via ModuleList:
    public static void CreateComponents(Part part, PartTemplate template, PartInstance? instance)
    {
        if (/* template has your custom data */)
        {
            var module = new MyCustomEVADoor { Parent = part };
            part.Modules.Add(module);
        }
    }

    public bool ShowContextMenu(Vehicle vehicle)
    {
        // Add your ImGui button and EVA spawn logic
    }
}

// Register globally during mod init:
ModuleList.Register(MyCustomEVADoor.CreateComponents);
```

### Key Integration Points

1. **`ModuleList.Register(CreateModulesFn)`** — Hook into part module creation
2. **`CelestialSystem.Register/Deregister`** — Vehicle lifecycle management
3. **`Vehicle.Teleport()`** — Position vehicles in space
4. **`Program.ControlledVehicle`** — Transfer player control
5. **`ModuleBase.OnDrawUi()`** — Override for custom ImGui UI
6. **`ModuleBase.GetSaveData()/ApplySaveData()`** — Persistence hooks

### Serialization Note

The system distinguishes `KittenEva` from `Vehicle` by the `Character` field:
- **Save:** If vehicle is `KittenEva`, `VehicleSaveData.Character` is set to the character ID
- **Load:** If `VehicleData.Character` is non-empty, `KittenEva.CreateKittenFromSaveData()` is used instead of `Vehicle.CreateVehicleFromSaveGameData()`

This means any mod that creates `KittenEva` instances gets save/load support automatically, as long as the character ID is valid.

---

## Summary Flow Diagram

```
┌─────────────────┐
│  Player clicks   │
│  "EVA Kitten"    │
│  on EVADoor part │
└────────┬────────┘
         │
         ▼
┌─────────────────────┐
│  EVADoor.            │
│  ShowContextMenu()   │
│  → CreateKittenEva() │
└────────┬────────────┘
         │
         ├──► Create VehicleData (name + random character)
         ├──► Create backpack Part (KittenBackPackPart + MMH_NTO fuel)
         ├──► new KittenEva(system, char, orientation, rates, parent, id, backpack, orbit)
         ├──► Calculate door-offset orbit position
         ├──► kittenEva.Teleport(offsetOrbit)
         ├──► parent.Children.Add(kittenEva)
         └──► Program.ControlledVehicle = kittenEva
                    │
                    ▼
         ┌─────────────────┐
         │  KittenEva is    │
         │  now an active   │
         │  Vehicle in the  │
         │  CelestialSystem │
         └────────┬────────┘
                  │
         ┌────────┴─────────┐
         │                  │
         ▼                  ▼
  ┌────────────┐    ┌──────────────┐
  │ Freefall / │    │  Destruction  │
  │ Maneuvering│    │  via Dispose()│
  │ / Landed   │    │  or Deregister│
  └────────────┘    └──────────────┘
```
