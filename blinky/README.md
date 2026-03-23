# blinky — Dynamic LCD Engine Pixel Grid

A KSA mod that dynamically creates an LCD pixel grid of engine parts at runtime and attaches it to an existing vehicle. Provides the same runtime animation and pattern control as blinken, but without requiring a pre-built vehicle.

## Overview

**blinky** builds an NxM grid of engine parts on demand by:
1. Looking up an engine `PartTemplate` from `ModLibrary`
2. Creating `Part` instances via `new Part(name, template)` for each grid cell
3. Wiring them to the vehicle's root part via manual `TreeParent`/`TreeChildren` assignment
4. Rebuilding the `PartTree` once with `PartTree.CreateFromNewPartTree()` after all parts are added
5. Naming them `pixel_{row}_{col}_{a|b}` so that blinken's `PixelGrid.ScanFromVehicle()` can be reused
6. Enabling the scrolling LCD animation using blinken.lib's `LcdAnimation`

The `PartTree` is rebuilt only once at the end, avoiding the per-part `RecomputeAllDerivedData()` cost that `Merge()` would trigger.

## Controls

- **F11** — Toggle the blinky window

## Window Sections

| Section | Description |
|---------|-------------|
| **Grid Configuration** | Width, height, spacing, layout mode, position offset, engine template quick-select |
| **Build Control** | Build/Destroy buttons with status |
| **Patterns** | All On, All Off, Checkerboard, Alt Rows, Alt Cols |
| **Animation** | Scrolling LCD animation with adjustable speed |
| **Debug** | Runtime dump buttons for vehicle/part/grid inspection |

## Grid Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| Width (cols) | 16 | Number of pixel columns |
| Height (rows) | 8 | Number of pixel rows |
| Layout | Flat | Flat plane or Cylinder (sides only) |
| Spacing (m) | 0.5 | Metres between pixel centres (arc-length for cylinder) |
| Offset X/Y/Z | 0, 5, 2 | Offset from vehicle root origin (cylinder centre for cylinder mode) |
| Engine template | EngineA1 | Part template ID (A1–A6 quick-select) |

## Project Structure

```
blinky/                   ← Mod entry point (ImGui UI + lifecycle)
├── Mod.cs                ← Main mod class (F11 window, pattern/anim controls)
├── Patcher.cs            ← Harmony patch setup (currently empty)
├── blinky.csproj
└── mod.toml

blinky.lib/               ← Core reusable logic (headless)
├── LcdGridConfig.cs      ← Grid configuration data class
├── LcdGridBuilder.cs     ← Runtime Part creation and manual tree wiring
├── BlinkyPixelGrid.cs    ← PixelGrid wrapper with owned-parts lifecycle
└── blinky.lib.csproj
```

## Dependencies

- `blinken.lib` — Reuses `PixelGrid`, `LcdAnimation`, `PixelPatterns`
- `ksa-abstractions.lib` — `VehicleProvider`, `PartHelpers`

## Known Limitations

- Phase 0 debugging: use the **Debug** buttons in the blinky window to dump runtime state after running in game. If `PartTree.Merge` returns false or `PixelGrid.ScanFromVehicle` finds 0 parts, check the console log for diagnostic output.
- The engine grid stays attached to the vehicle until **Destroy Grid** is pressed. Switching vehicles resets the grid reference but does NOT automatically remove the parts.

  ↓
OnBeforeGui() / OnAfterUi()  → Render ImGui every frame
  ↓
Unload()                 → Cleanup, remove patches
```

## Architecture

### Mod.cs
Entry point for the mod with lifecycle management.

```csharp
public class Mod : StarMapMod
{
    public override void OnImmediateLoad()
    {
        // First initialization
        Console.WriteLine("Fixme-Mod-Name: OnImmediateLoad");
    }
    
    public override void OnFullyLoaded()
    {
        // All mods ready, initialize partnerships
        Patcher.Initialize();
    }
    
    public override void OnAfterUi()
    {
        // Render ImGui window every frame
        RenderWindow();
    }
    
    public override void Unload()
    {
        // Cleanup patches and resources
        Patcher.Cleanup();
    }
    
    private void RenderWindow()
    {
        if (!showWindow) return;
        
        ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Fixme-Mod-Name", ref showWindow))
        {
            ImGui.Text("Hello, World!");
            if (ImGui.Button("Click Me!"))
            {
                Console.WriteLine("Button clicked!");
            }
            ImGui.End();
        }
    }
}
```

### Patcher.cs
Harmony-based runtime method patching initialization.

```csharp
public static class Patcher
{
    private static Harmony harmony;
    
    public static void Initialize()
    {
        harmony = new Harmony("MeowSci.Blinky");
        harmony.PatchAll();  // Patches defined in assembly
    }
    
    public static void Cleanup()
    {
        harmony?.UnpatchAll();
    }
}
```

### Library Project (optional)
Separate `.lib` project for reusable, headless logic:

```csharp
public static class BlinkyLib
{
    public static void DoSomething()
    {
        // Reusable functionality
    }
}
```

## Getting Started with This Template

### Step 1: Rename
```
blinky → your-cool-mod
Blinky → YourCoolMod
MeowSci.Blinky → MeowSci.YourCoolMod
```

### Step 2: Update Project Files
- Rename `.csproj` files
- Update assembly names
- Update namespace declarations

### Step 3: Implement Mod Logic
Replace template code with actual mod features:
- Define what should happen in each lifecycle method
- Add ImGui controls in `RenderWindow()`
- Implement Harmony patches in `Patcher.cs`

### Step 4: Document
Refer to this README structure and update with:
- Mod overview
- Features
- Architecture explanation
- Usage examples
- Implementation details

## Standard Mod Pattern

Most mods follow this pattern:

1. **Mod.cs**: UI + Lifecycle (StarMapMod subclass)
2. **Patcher.cs**: Runtime patches (Harmony setup)
3. **Lib project**: Reusable logic (separate assembly)
4. **README.md**: Documentation (what you're reading)

## ImGui Window Pattern

Standard toggle pattern:

```csharp
private bool showWindow = false;

public override void OnAfterUi()
{
    // F11 toggles window visibility
    if (Input.GetKeyDown(KeyCode.F11))
        showWindow = !showWindow;
    
    if (!showWindow) return;
    
    ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);
    if (ImGui.Begin("Mod Name", ref showWindow))
    {
        // Render content here
        ImGui.End();
    }
}
```

## Harmony Patching Pattern

Basic patch structure:

```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.TargetMethod))]
public static class TargetMethodPatch
{
    public static bool Prefix(/* method parameters */)
    {
        // Prefix runs before original, return false to skip original
        Console.WriteLine("Before TargetMethod");
        return true;
    }
    
    public static void Postfix(/* method parameters */)
    {
        // Postfix runs after original
        Console.WriteLine("After TargetMethod");
    }
}
```

## Key Files for Reference

When developing from this template, refer to:

1. **[REPOSITORY_INDEX.md](../REPOSITORY_INDEX.md)** - All mods documentation
2. **sibling mod READMEs** - Similar mods for reference implementation
3. **HarmonyLib docs** - Runtime patching patterns
4. **ImGui API docs** - UI widget reference

## Next Steps

1. Copy this entire folder
2. Rename appropriately
3. Implement your feature logic
4. Test with `dotnet build`
5. Update this README with your mod's actual purpose and features

## Testing

Build the solution:
```bash
dotnet build
```

Check for compilation errors before continuing with implementation.

## Common Issues

- **Namespace mismatches**: Update everywhere (csproj, Mod.cs, Patcher.cs)
- **Project references**: Add library project reference to main mod
- **Harmony ID conflicts**: Each Harmony instance needs unique ID string
- **ImGui crashes**: Ensure ImGui calls only happen in OnAfterUi

## Notes for Developers

- Keep UI separate from logic (UI in Mod.cs, logic in Lib project)
- Use Console.WriteLine for debugging
- Test Harmony patches carefully—they affect game runtime
- Document your Harmony patches explaining what they do
- Consider performance impact of per-frame operations

## Related Mods

See similar template mods:
- [grant](../grant) - Minimal template without .lib
- [stampy](../stampy) - Another template example
- Other mods for inspiration on complete implementations
