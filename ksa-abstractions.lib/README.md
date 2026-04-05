# KSA Abstractions Library

A foundational shared library providing common abstractions and utilities used across multiple KSA mods. This library contains no UI or mod-specific logic—it's purely headless functionality focused on reflection, part/vehicle access patterns, and simulation time utilities.

## Overview

`ksa-abstractions.lib` serves as a dependency for many other mods, providing reusable patterns for:
- **Part Tree Traversal**: Recursive scanning of vehicle parts and sub-parts
- **Reflection-Based Field Access**: Safe access to private/internal KSA fields
- **Vehicle Lookup**: Game state queries for vehicles and controlled vehicle
- **Simulation Time**: Wrapper around KSA's universe time

## Key Classes & Methods

### PartHelpers
Static utility class for vehicle part operations.

- `GetAllParts(Vehicle vehicle)` - Recursively collects all parts in a vehicle, including nested SubParts
- `GetPartsWhere(Vehicle vehicle, Func<Part, bool> predicate)` - Filters parts by custom predicate

**Key Pattern**: Handles the recursive nature of KSA's part tree, where parts can contain other parts via the `SubParts` collection.

### ReflectionHelpers
Static helper methods for accessing private/internal KSA fields via reflection.

- `GetFieldValue<T>(object target, string fieldName)` - Type-safe field read with null guard
- `SetFieldValue(object target, string fieldName, object value)` - Type-safe field write
- `GetPropertyValue<T>(object target, string propertyName)` - Type-safe property read

**Key Pattern**: Isolates reflection calls for KSA internals (e.g., accessing `_controlledVehicle` from game state objects).

### VehicleProvider
Static helpers for querying vehicle state from the game.

- `GetControlledVehicle()` - Returns the currently player-controlled vehicle (or null if none)
- `GetAllVehicles()` - Returns all vehicles in the current solar system
- `GetVehicleByName(string name)` - Looks up a vehicle by its display name

**Key Pattern**: Provides safe wrappers around KSA's game state queries.

### SimTimeProvider
Wrapper around the KSA universe's simulation time.

- `GetElapsedTime()` - Returns elapsed simulation time in seconds
- `GetDeltaTime()` - Returns delta time since last frame

## Architecture Notes

- **Reflection-Based**: Relies on reflection rather than HarmonyLib patching, making it stateless and non-invasive
- **Null-Safe**: All field/property reads protect against null references
- **Type-Safe Generics**: Methods use generics to avoid casting at call sites
- **No Side Effects**: All methods are pure utilities with no state mutation

## Usage Examples

### Iterating Vehicle Parts
```csharp
var vehicle = VehicleProvider.GetControlledVehicle();
if (vehicle != null)
{
    var allParts = PartHelpers.GetAllParts(vehicle);
    var enginesOnly = PartHelpers.GetPartsWhere(vehicle, p => p.PartTemplate.EngineModule != null);
}
```

### Reflection-Based Field Access
```csharp
// Access private field without knowing exact type
var value = ReflectionHelpers.GetFieldValue<float>(someObject, "_privateField");
ReflectionHelpers.SetFieldValue(someObject, "_internalState", newValue);
```

### Getting Vehicle State
```csharp
var player = VehicleProvider.GetControlledVehicle();
var allVehicles = VehicleProvider.GetAllVehicles();
var elapsedTime = SimTimeProvider.GetElapsedTime();
```

## Common Patterns

### Part Tree Traversal Pattern
Many mods use the same pattern:
```csharp
var parts = PartHelpers.GetAllParts(vehicle);
foreach (var part in parts)
{
    // Process each part
}
```

This is used by mods like:
- **blinken**: Scanning for pixel grid parts
- **zippo**: Finding light components
- **garrys-torch**: Vehicle inspection

### Reflection Pattern
Mods frequently access private KSA internals:
```csharp
var lightComponents = ReflectionHelpers.GetFieldValue<List<object>>(part, "_lightData");
```

This is used by:
- **zippo**: Reading/writing light intensity and color
- **glass**: Accessing camera FOV fields
- **kitten-animations**: Accessing avatar state

## Notes for Future Development

- When adding new reflection helpers, consider caching field/property lookups for performance
- Follow the null-guard pattern: field access should return default/null rather than throwing
- Keep methods generic and reusable—avoid mod-specific logic in this library
- Document the exact KSA class/field names being accessed for debugging purposes
