# Zippo - Vehicle Light Control System

A lighting system that allows you to select vehicles and their light components, then control light intensity and color in real-time using an XKCD color palette.

## Overview

Zippo lets you:
- **Select vehicles and individual light parts** - Browse vehicle hierarchy and identify light components
- **Control light intensity** - Slider from 0.0 (off) to 1.0 (full brightness)
- **Apply color presets** - Marine, HotPink, RadioactiveGreen, BabyPurple
- **Toggle lights on/off** - Quickly disable/enable all selected lights
- **Real-time updates** - Changes apply immediately in-game

## Features

- **Reflection-based light access** - Finds and manipulates internal KSA light components
- **Vehicle/part selection dropdowns** - Easy navigation of vehicle hierarchy
- **Color preset system** - Pre-configured colors for quick application
- **Custom color support** - Extensible for adding new color presets
- **Recursive part search** - Automatically finds light components nested in part trees

## Architecture

### Core Classes

#### LightController
Provides reflection-based access to KSA's light system.

**Key Methods**:
- `GetLightParts(Vehicle vehicle)` - Finds all parts with light components in a vehicle
- `HasLights(PartTemplate part)` - Checks if a part template has light components
- `ReadIntensity(PartTemplate part)` - Reads current light intensity value
- `ReadColor(PartTemplate part)` - Reads current light color (RGB)
- `WriteIntensity(List<object> lights, float intensity)` - Sets intensity on light objects
- `WriteColor(List<object> lights, float3 color)` - Sets RGB color on light objects
- `ApplyIntensity(Part part, float intensity)` - Updates intensity for a vehicle part
- `ApplyColor(Part part, float3 color)` - Updates color for a vehicle part

### Reflection Pattern

Zippo uses reflection to access private KSA light components:

```csharp
// Access internal KSA.LightModule+TemplateData components
var lightComponents = ReflectionHelpers.GetFieldValue<List<object>>(part, "_lightData");
```

After mutating color properties, `OnDataLoad()` is called to recompute internal KSA state:
```csharp
lightComponent.OnDataLoad();
```

### UI (Mod.cs)

ImGui window with:
- **Vehicle Selector** - Dropdown to choose which vehicle to modify
- **Light Part Selector** - Dropdown of all light-containing parts in selected vehicle
- **Intensity Slider** - 0.0 to 1.0 with preview
- **Color Presets** - Buttons for Marine, HotPink, RadioactiveGreen, BabyPurple
- **Custom Color Option** - RGB sliders for custom colors
- **Apply/Toggle Buttons** - Apply settings, quickly toggle all lights on/off

## Light Components

KSA lights are accessed through:
- **Part Template**: Defines what lights a part type has (static, design-time)
- **Light Objects**: Individual light components on parts (instances at runtime)
- **TemplateData**: Contains intensity, color, and other light properties

### Light Properties

| Property | Type | Range | Notes |
|----------|------|-------|-------|
| Intensity | float | 0.0 - 1.0 | Brightness level |
| Color (R/G/B) | float3 | 0.0 - 1.0 | RGB color values |
| Enabled | bool | true/false | Light on/off |

## Color Presets

Pre-defined colors using XKCD naming:

```csharp
new float3(0.0f, 0.5f, 0.7f)      // Marine
new float3(1.0f, 0.0f, 0.6f)      // HotPink
new float3(0.4f, 1.0f, 0.0f)      // RadioactiveGreen
new float3(0.7f, 0.3f, 1.0f)      // BabyPurple
```

Adding new presets is as simple as:
1. Define new float3 RGB values in the color preset list
2. Add button to ImGui window
3. Call `WriteColor()` with new values

## Implementation Details

### Part Scanning
```csharp
var lightParts = LightController.GetLightParts(vehicle);
// Returns only parts with light components, cached for performance
```

### Intensity Update
```csharp
LightController.ApplyIntensity(part, 0.5f);  // Set to 50% brightness
```

### Color Update
```csharp
var newColor = new float3(1.0f, 0.0f, 0.0f);  // Red
LightController.ApplyColor(part, newColor);
```

## Usage Example

```csharp
// Find vehicle to modify
var vehicle = VehicleProvider.GetControlledVehicle();

// Get all light parts
var lightParts = LightController.GetLightParts(vehicle);

// Set all lights to 80% intensity with HotPink color
foreach (var part in lightParts)
{
    LightController.ApplyIntensity(part, 0.8f);
    LightController.ApplyColor(part, new float3(1.0f, 0.0f, 0.6f));
}
```

## Notes for Future Development

- **Performance**: Light updates are reflected immediately; consider batching for many lights
- **Animation**: Could extend to support color/intensity transitions over time
- **Save/Load**: No persistence currently; could save/load light configurations
- **Part Naming**: Light parts are identified by KSA's part template system; no manual naming needed
- **Asset Colors**: Could load colors from external XKCD color database instead of hardcoding

## Dependencies

- **MeowSci.KsaAbstractions**: For vehicle and part queries
- **HarmonyLib**: For initialization/cleanup
- **KSA Game**: For light component access via reflection
