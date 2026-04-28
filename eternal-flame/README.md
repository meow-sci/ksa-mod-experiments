# Eternal Flame - Infinite Fuel Hack

Keeps selected vehicles topped up by periodically calling `RefillConsumables()` at a configurable interval. Toggle the mod window with **F11**.

## Features

- **Filterable vehicle selector** — searchable combo box listing all vehicles in the current system
- **Monitored vehicle table** — shows all tracked vehicles with an active/inactive checkbox and a remove button
- **Refill interval slider** — drag slider (0–1000ms) controlling how often consumables are refilled
- **Background refill loop** — runs every frame regardless of window visibility; only does work when monitored vehicles exist and the accumulated delta time exceeds the configured interval
- **Per-vehicle toggle** — unchecking a vehicle in the table keeps it in the list but skips refills until re-enabled

## Files

| File | Purpose |
|------|---------|
| `Mod.cs` | StarMap mod class — UI rendering & game loop hook |
| `Patcher.cs` | Harmony patcher setup/teardown |
| `eternal-flame.csproj` | Main mod project |
| `../eternal-flame.lib/EternalFlameLib.cs` | Core refill logic (`FuelManager`, `MonitoredVehicle`) |

## Usage

1. Press **F11** to open the Eternal Flame window
2. Select a vehicle from the filterable dropdown and click **Add**
3. The vehicle appears in the monitored table with its checkbox enabled
4. Adjust the refill interval slider as desired (lower = more frequent refills)
5. Uncheck a vehicle to pause refills without removing it
6. Click **X** to remove a vehicle from monitoring entirely

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
- [unscience](../unscience) - Minimal template without .lib
- [stampy](../stampy) - Another template example
- Other mods for inspiration on complete implementations
