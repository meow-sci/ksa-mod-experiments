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
- `GetAllVehicles(bool includeDebris = false)` - Returns the vehicles in the current solar system.
  KSA `2026.9.7.5402` added structural part failure, which sheds fragments as real `Vehicle` objects
  flagged `IsDebris` in the same system collection as crewed craft; they are filtered out by default
  so they don't fill every mod's vehicle picker. Pass `includeDebris: true` when debris is a
  legitimate target — a safety gate that must see everything, or a click/raycast that should hit
  whatever is visible.
- `GetVehicleByName(string name)` - Looks up a vehicle by its display name

**Key Pattern**: Provides safe wrappers around KSA's game state queries.

### SimTimeProvider
Wrapper around the KSA universe's simulation time.

- `GetElapsedTime()` - Returns elapsed simulation time in seconds
- `GetDeltaTime()` - Returns delta time since last frame

### HotkeyGuard
Mandatory Harmony prefix on `GameSettings.OnKeyAll` that swallows game hotkeys while any ImGui text input has focus (bypassed while the dev console is open). Every top-level mod applies it via `HotkeyGuard.Patch(harmony)` / `HotkeyGuard.Unpatch(harmony)`.

### HiddenUiFrameHook
Keeps per-frame mod work alive while the game HUD is hidden (**F2** / `InputAction.ToggleUi`).

**Why it exists:** StarMap dispatches `[StarMapBeforeGui]` as a prefix of `Program.OnDrawUiFrame` and `[StarMapAfterGui]` as a postfix of `Program.OnDrawUiViewports`. Both sit inside `if (Program.DrawUI)` in `Program.OnFrame`, so while the HUD is hidden neither game method is called and neither StarMap hook fires — every `Update(dt)`-driven feature (weld physics, fuel refill, animations, RPC queue drain, …) silently freezes.

**What it does:** Harmony-prefixes `Program.OnDrawUiConsole(double dt)`, which the game calls unconditionally in the same frame phase (after `PrepareFrame`, inside the ImGui `NewFrame`…`Render` window, before `OnPreRender`). The prefix is a no-op while `Program.DrawUI` is true; when it is false it invokes the registered `BeforeGui` then `AfterGui` callbacks. `DrawUI` only changes during input polling in `PrepareFrame`, so a frame never fires both StarMap's hooks and this fallback.

- `HiddenUiFrameHook.BeforeGui` / `.AfterGui` (`Action<double>?`) — register the non-ImGui parts of your `[StarMapBeforeGui]` / `[StarMapAfterGui]` bodies **before** calling `Patch`
- `HiddenUiFrameHook.Patch(harmony)` / `.Unpatch(harmony)` — apply/remove alongside `HotkeyGuard`; `Unpatch` clears the callbacks
- `HiddenUiFrameHook.IsUiHidden` — `!Program.DrawUI`

ImGui *is* valid inside the callbacks, but hosts should keep window rendering out of them so mod windows honour the hidden HUD.

```csharp
// Mod.cs
[StarMapBeforeGui] public void OnBeforeUi(double dt) => UpdateSubmods(dt);
[StarMapAfterGui]  public void OnAfterUi(double dt)  { RenderWindows(); UpdateWelds(dt); }

// in [StarMapAllModsLoaded], before Patcher.Patch():
HiddenUiFrameHook.BeforeGui = UpdateSubmods;
HiddenUiFrameHook.AfterGui  = UpdateWelds;

// Patcher.cs
HiddenUiFrameHook.Patch(_harmony);    // in Patch(), next to HotkeyGuard.Patch
HiddenUiFrameHook.Unpatch(_harmony);  // in Unload()
```

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
- **blinky**: Scanning for pixel grid parts
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
