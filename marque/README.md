# Marque - Orbit Line Visibility Manager

A KSA mod that adds a **Marque** submenu to the game's View menu bar, providing quick toggle controls for orbit line visibility on vehicles and celestial bodies.

## Features

### Vehicle Orbit Lines
- **All / None** — bulk enable/disable orbit lines for every vehicle
- **Individual toggles** — alphabetically sorted list of all vehicles with checkmark indicators

### Celestial Orbit Lines
- **All / None** — bulk enable/disable orbit lines for every celestial body in the system
- **Hierarchical submenus** — celestials organized by SOI (sphere of influence) hierarchy
  - Planets with moons open as submenus with their own All/None controls
  - Leaf celestials (no children) are direct toggle items
- **Recursive depth** — moons with sub-moons get nested submenus automatically

### UX
- Menus **stay open** after clicking — toggle multiple items quickly without re-navigating
- Checkmarks show current orbit line visibility state

## Architecture

| File | Purpose |
|------|---------|
| `Mod.cs` | StarMapMod lifecycle (F11 debug window) |
| `Patcher.cs` | Harmony prefix on `GaugeCanvas.OnDrawMenuBar` to inject menu items |
| `marque.lib/MarqueLib.cs` | All menu rendering logic — vehicles menu, celestials hierarchy, orbit toggling |

## How It Works

The mod patches `GaugeCanvas.OnDrawMenuBar` with a Harmony prefix that calls `MarqueLib.DrawMarqueMenus()`. This runs between `ImGui.BeginMenu("View")` and `ImGui.EndMenu()`, adding the Marque submenu into the game's existing View menu.

Orbit visibility is toggled directly via `IOrbiter.ShowOrbit` on each vehicle or celestial body. The celestial hierarchy is built by walking `IParentBody.Children` starting from the `StellarBody` (sun).
