# blinky — Dynamic LCD Engine Pixel Grid

A KSA mod that dynamically creates LCD pixel grids of engine parts at runtime and attaches them to existing vehicles. Supports **multiple named grids per vehicle**, each independently configured and controlled. Provides static pixel display and pattern control — all controllable via both ImGui UI and HTTP RPC endpoints.

## Overview

**blinky** builds NxM grids of engine parts on demand by:
1. Looking up an engine `PartTemplate` from `ModLibrary`
2. Creating `Part` instances for each grid cell (a/b pairs for balanced thrust)
3. Wiring them to the vehicle's root part via manual `TreeParent`/`TreeChildren` assignment
4. Rebuilding the `PartTree` once with `PartTree.CreateFromNewPartTree()`
5. Naming them `pixel_{gridName}_{row}_{col}_{a|b}` for grid lookup

Each vehicle can have multiple grids, distinguished by a user-chosen **grid name**. Grid names must contain only alphanumeric characters and hyphens (`[a-zA-Z0-9-]`) — underscores are reserved as the part ID delimiter.

## Controls

- **F11** — Toggle the blinky window

## Window Sections

| Section | Description |
|---------|-------------|
| **Menu Bar** | Debug menu with global "Scan for blinky grids" across all vehicles |
| **Create Blinky Grid** | Collapsible 4-column table: grid size, spacing, engine scale, position, layout, engine preset, grid name, vehicle selector, and Create button |
| **Per-Grid Sections** | Collapsible header per registered grid with info table, pattern buttons, and destroy |

## Features

### Multi-Grid Support
Each vehicle can host multiple independent named grids. Grids are keyed by `(vehicleId, gridName)` throughout the system. The UI shows collapsible sections for each registered grid.

### Pattern Presets
Built-in pattern buttons per grid: All On, Off, Alternating Rows, Alternating Cols, Checkerboard.

### Static Display
Paints a set of pixels directly. Supports intelligent reset mode that only changes the pixels that need updating (diffs current vs new state). Available via RPC API.

### Global Scan (Debug Menu)
Auto-discovers all named blinky grids on all loaded vehicles by parsing `pixel_{gridName}_{row}_{col}_{a|b}` part IDs and registering each discovered grid.

### Render Toggle
Checkbox to toggle engine mesh rendering for a significant performance boost — hides part meshes while keeping the pixel grid fully functional.

### RPC Endpoints (via unladen-swallow)
All endpoints require a `vehicleId` and `gridName` to identify which grid to control.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/blinky/animate` | POST | Start a scrolling animation with custom pixel data and speed |
| `/blinky/static` | POST | Display a static set of pixels (with optional reset/diff) |
| `/blinky/off` | POST | Turn off all pixels and stop any scroll |
| `/blinky/grids` | GET | List all registered grids (optional `vehicleId` query filter) |

## Grid Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| Columns | 8 | Number of pixel columns |
| Rows | 8 | Number of pixel rows |
| Layout | Flat | Flat plane or Cylinder (sides only) |
| Spacing (m) | 5.0 | Metres between pixel centres |
| Position X/Y/Z | 0, 0, 0 | Offset from vehicle root origin |
| Engine Scale | 0.010 | Scale factor for engine part meshes |
| Engine | EngineA3 | Part template ID (A1–A6 filtered quick-select) |

## Grid Naming

Grid names are user-chosen identifiers that distinguish multiple grids on the same vehicle.

| Rule | Detail |
|------|--------|
| Allowed characters | `a-z`, `A-Z`, `0-9`, `-` (hyphen) |
| Not allowed | `_` (underscore) — reserved as part ID delimiter |
| Part ID format | `pixel_{gridName}_{row}_{col}_{a\|b}` |

## Project Structure

```
blinky/                       ← Mod entry point (ImGui UI + lifecycle)
├── Mod.cs                    ← Main mod class (F11 window, UI controls)
├── Patcher.cs                ← Harmony render-skip patches for pixel parts
├── blinky.csproj
└── mod.toml

blinky.lib/                   ← Core reusable logic (headless, no blinken.lib dependency)
├── BlinkyGridManager.cs      ← Static singleton: compound (vehicleId, gridName) key APIs
├── ScrollAnimation.cs        ← Scrolling animation engine
├── PixelGrid.cs              ← Vehicle pixel grid scanner + engine controller cache
├── PixelPatterns.cs           ← Built-in pattern functions
├── LcdGridConfig.cs          ← Grid configuration data class
├── LcdGridBuilder.cs         ← Runtime Part creation and manual tree wiring
├── BlinkyPixelGrid.cs        ← PixelGrid wrapper with owned-parts lifecycle
├── BuiltInScrollPixels.cs    ← Default built-in scroll animation pixel data
└── blinky.lib.csproj
```

## Dependencies

- `ksa-abstractions.lib` — `VehicleProvider`, `PartHelpers`, `GameThread`

## Architecture

- **blinky.lib** is fully self-contained — it does NOT depend on blinken.lib
- **BlinkyGridManager** is a static singleton shared between the mod UI and RPC endpoints
- Grids are registered by compound key `(vehicleId, gridName)` and discoverable from any consumer
- Multiple grids per vehicle are fully independent (own config, scroll state, active pixels)
- The mod UI (`Mod.cs`) is a thin ImGui layer that delegates all logic to `BlinkyGridManager`
- **`BlinkySubmod`** lives in `blinky.lib` and implements `ISubmod` from `ksa-abstractions.lib`; it is instantiated directly by the grant supermod
