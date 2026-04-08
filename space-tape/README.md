# Space Tape

In-game Part editor for KSA. Compose new Parts by placing existing SubParts into a 3D scene with transform controls, then save the result as a KSA mod XML Part definition.

## Overview

Space Tape lets you build custom vehicle parts interactively inside the game without a separate editor. You browse the SubPart catalog, drag pieces into a 3D editing scene, adjust transforms with gizmos (translate / rotate / scale), edit GameData fields in an ImGui property panel, and export a ready-to-use Part XML that can be hot-reloaded into the game.

## Planned Features

- **SubPart catalog browser** — filterable list with thumbnail previews (via inanimate-carbon-rod thumbnail pipeline)
- **3D editing scene** — place SubParts in world space with interactive gizmos
- **Transform controls** — per-SubPart position, rotation, and scale inputs
- **GameData property panel** — edit attach-node positions, mass, drag, and other fields
- **Part XML export** — writes a KSA-compatible Part definition to the `space-tape-parts` mod directory
- **Hot-reload spike** — registers newly saved parts at runtime so you can test without a restart

## Integration

Space Tape is integrated into the **grant** supermod as `SpaceTapeSubmod`. It appears as a collapsible panel in the Grant Toolbox window alongside other submods.

## Project Structure

| File | Purpose |
|------|---------|
| `Mod.cs` | Standalone mod entry — F11 window toggle |
| `Patcher.cs` | Harmony setup with HotkeyGuard |
| `mod.toml` | StarMap mod descriptor |
| `space-tape.lib/SpaceTapeSubmod.cs` | ISubmod entry point for grant integration |

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
        harmony = new Harmony("MeowSci.SpaceTape");
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
public static class SpaceTapeLib
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
space-tape → your-cool-mod
SpaceTape → YourCoolMod
MeowSci.SpaceTape → MeowSci.YourCoolMod
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
