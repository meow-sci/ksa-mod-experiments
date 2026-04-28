# Con-Man — Layout Manager

Game UI layout manager for KSA gauge canvases. Save and restore HUD gauge visibility, position, and scale to named layouts.

## Features

- **Save layouts** — Capture current gauge state (enabled, position offset, scale) for all gauge canvases
- **Load layouts** — Apply saved layouts to instantly restore gauge positions and visibility
- **Startup default** — Set a layout to automatically apply when the game starts
- **Delete layouts** — Remove saved layouts with confirmation dialog
- **Filtered selectors** — All comboboxes support text filtering for quick selection
- **Live gauge summary** — Collapsible table showing real-time state of all GaugeCanvas instances

## Architecture

| File | Purpose |
|------|---------|
| `con-man/Mod.cs` | Standalone mod entry point — thin window shim around `ConManSubmod` |
| `con-man.lib/ConManSubmod.cs` | `ISubmod` implementation with full ImGui UI |
| `con-man.lib/LayoutManager.cs` | Orchestrates save/load/delete/apply operations and startup default |
| `con-man.lib/GaugeStateAccessor.cs` | Reflection wrapper for GaugeCanvas private fields |
| `con-man.lib/LayoutSerializer.cs` | TOML serialization for layout files and config |

## Data Storage

Layouts and configuration are stored in:

```
Documents/My Games/Kitten Space Agency/.con-man/
  config.toml          # startup default setting
  layouts/
    my-layout.toml     # saved gauge layouts
```

## How It Works

Each `GaugeCanvas` in the game has private fields controlling its appearance:
- `_enabled` (bool) — whether the gauge is visible
- `_customOffset` (float2) — drag offset from base position
- `_customScale` (float2) — resize scale relative to base size

Con-man uses reflection (via `GaugeStateAccessor`) to read and write these fields. Layouts are identified by the stable `SerializedId.Id` property, so they persist across game sessions.

## Controls

- **F11** — Toggle window visibility (standalone mode)

## Usage in Unscience Supermod

Con-man is available as a submod in the Unscience unified toolbox. All functionality lives in `con-man.lib` and is shared between the standalone mod and the unscience supermod via the `ISubmod` interface.
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
