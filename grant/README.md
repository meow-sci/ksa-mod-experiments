# Grant - Minimal Template Mod

A minimal template mod demonstrating the simplest possible KSA mod structure without a separate library project. Use as a starting point for quick experiments or simple mods.

## Overview

This is a **minimal template mod** showing:
- Very basic mod lifecycle (OnFullyLoaded, OnAfterUi, Unload)
- Simple ImGui window with button
- Harmony patcher with empty patch setup
- No separate library project (all logic in main assembly)

## What This Mod Contains

### Files

| File | Purpose |
|------|---------|
| `Mod.cs` | Main mod class with UI and lifecycle |
| `Patcher.cs` | Harmony initialization/cleanup |
| `grant.csproj` | Single project (no .lib split) |

## Architecture

### Mod.cs
Minimal implementation:

```csharp
public class Mod : StarMapMod
{
    private bool showWindow = false;
    
    public override void OnFullyLoaded()
    {
        // Initialize when all mods loaded
        Patcher.Initialize();
    }
    
    public override void OnAfterUi()
    {
        // Render UI every frame
        if (Input.GetKeyDown(KeyCode.F11))
            showWindow = !showWindow;
        
        if (!showWindow) return;
        
        ImGui.SetNextWindowSize(new Vector2(300, 150), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Grant", ref showWindow))
        {
            ImGui.Text("Hello!");
            if (ImGui.Button("Click Me!"))
            {
                Console.WriteLine("Clicked!");
            }
            ImGui.End();
        }
    }
    
    public override void Unload()
    {
        Patcher.Cleanup();
    }
}
```

### Patcher.cs
Minimal Harmony setup:

```csharp
public static class Patcher
{
    private static Harmony harmony;
    
    public static void Initialize()
    {
        harmony = new Harmony("MeowSci.Grant");
        // harmony.PatchAll();  // Uncomment when patches are needed
    }
    
    public static void Cleanup()
    {
        harmony?.UnpatchAll();
    }
}
```

## Differences from Template Mod

| Aspect | Grant | Fixme-Mod-Name |
|--------|-------|-----------------|
| Library | None | .lib project |
| Complexity | Minimal | Standard |
| Use case | Quick experiments | Production mods |
| Logic reusability | Manual (copy code) | Easy (reference lib) |
| Project files | 1 | 2 |

**When to use Grant as template**:
- Simple, single-purpose mod
- Don't need reusable library
- Quick prototype/experiment
- Can afford code duplication

**When to use Fixme-Mod-Name instead**:
- Want reusable library
- Complex feature set
- Plan to share code with other mods
- Production-quality mod

## Extending Grant

### Add Harmony Patch

```csharp
// In Patcher.cs, define a patch class:

[HarmonyPatch(typeof(Vehicle), nameof(Vehicle.GetWorldMatrix))]
public static class VehicleMatrixPatch
{
    public static void Postfix(Vehicle __instance, ref Transform3D __result)
    {
        // Modify world matrix
    }
}

// Then uncomment harmony.PatchAll() in Initialize()
```

### Add More UI

```csharp
// In Mod.cs OnAfterUi():

if (ImGui.Button("Do Something"))
{
    DoSomething();
}

if (ImGui.SliderFloat("Value", ref myValue, 0, 100))
{
    // Slider changed
}
```

### Add Logic

Keep it simple—just add methods to `Mod.cs`:

```csharp
private void DoSomething()
{
    var vehicle = VehicleProvider.GetControlledVehicle();
    if (vehicle != null)
    {
        Console.WriteLine($"Vehicle mass: {vehicle.TotalMass}");
    }
}
```

## Minimal Lifecycle

Grant uses the **minimal** lifecycle:

```
OnFullyLoaded()    → Initialize (patches, etc.)
   ↓
OnAfterUi()        → Every frame, render ImGui
   ↓
Unload()           → Cleanup
```

Note: No `OnBeforeGui()` or `OnImmediateLoad()` unless needed.

## Dependencies Pattern

For quick access to common utilities:

```csharp
// Add using statements
using MeowSci.KsaAbstractions;

// Then use directly in Mod.cs methods
var vehicle = VehicleProvider.GetControlledVehicle();
var parts = PartHelpers.GetAllParts(vehicle);
```

## Configuration

All settings in ImGui window:

```csharp
private float myValue = 1.0f;
private bool myFlag = false;

// In UI rendering:
ImGui.SliderFloat("My Value", ref myValue, 0, 100);
ImGui.Checkbox("My Flag", ref myFlag);
```

## Debugging

Use Console.WriteLine for quick debugging:

```csharp
Console.WriteLine("Grant: Something happened!");
Console.WriteLine($"Vehicle name: {vehicle?.DisplayName}");
```

Output appears in game console or debug output.

## Common Patterns

### Toggle Window
```csharp
if (Input.GetKeyDown(KeyCode.F11))
    showWindow = !showWindow;
```

### Button with Action
```csharp
if (ImGui.Button("Do It!"))
{
    Console.WriteLine("Action triggered!");
}
```

### Per-Frame Update
```csharp
private double timer = 0;

public override void OnAfterUi()
{
    timer += Time.deltaTime;
    if (timer > 1.0)
    {
        // Every 1 second
        Console.WriteLine("Tick!");
        timer = 0;
    }
}
```

## Next Steps

1. Copy this folder as basis for new mod
2. Rename appropriately
3. Add UI elements via ImGui
4. Add logic to Mod.cs
5. Build and test
6. If complexity grows, consider refactoring to library project (fixme-mod-name pattern)

## When to Upgrade to Template Pattern

If grant becomes too complex, upgrade to fixme-mod-name pattern:
1. Create `.lib` project
2. Move reusable logic to library
3. Keep UI in main Mod.cs
4. Reference library project

## Related Mods

- [fixme-mod-name](../fixme-mod-name) - Full template with library
- [stampy](../stampy) - Another minimal template
- Other complete mods for implementation reference

## Notes

- No hidden complexity—all code visible in single file
- Easy to understand what happens each frame
- Scales up to moderate complexity
- Consider refactoring if approaching 500+ lines
