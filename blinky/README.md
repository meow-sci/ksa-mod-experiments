# blinky — Dynamic LCD Engine Pixel Grid

A KSA mod that dynamically creates an LCD pixel grid of engine parts at runtime and attaches it to an existing vehicle. Provides scrolling animation, static pixel display, and pattern control — all controllable via both ImGui UI and HTTP RPC endpoints.

## Overview

**blinky** builds an NxM grid of engine parts on demand by:
1. Looking up an engine `PartTemplate` from `ModLibrary`
2. Creating `Part` instances for each grid cell (a/b pairs for balanced thrust)
3. Wiring them to the vehicle's root part via manual `TreeParent`/`TreeChildren` assignment
4. Rebuilding the `PartTree` once with `PartTree.CreateFromNewPartTree()`
5. Naming them `pixel_{row}_{col}_{a|b}` for grid lookup

## Controls

- **F11** — Toggle the blinky window

## Window Sections

| Section | Description |
|---------|-------------|
| **Grid Configuration** | Width, height, spacing, layout mode, position offset, engine template quick-select |
| **Build Control** | Build/Destroy/Scan buttons with status |
| **Patterns** | All On, All Off, Checkerboard, Alt Rows, Alt Cols |
| **Scroll** | Scrolling LCD animation with adjustable speed (built-in pixel art) |
| **Debug** | Runtime dump buttons for vehicle/part/grid inspection |

## Features

### Scroll Animation
Scrolls a static pixel image horizontally across the grid. The built-in animation is available via the UI Start button. Custom pixel data can be supplied via the RPC API.

### Static Display
Paints a set of pixels directly without animation. Supports intelligent reset mode that only changes the pixels that need updating (diffs current vs new state).

### Off
Turns off all pixels and stops any running scroll animation.

### RPC Endpoints (via unladen-swallow)
All endpoints require a `vehicleId` to identify which vehicle's grid to control.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/blinky/animate` | POST | Start a scrolling animation with custom pixel data and speed |
| `/blinky/static` | POST | Display a static set of pixels (with optional reset/diff) |
| `/blinky/off` | POST | Turn off all pixels and stop any scroll |

## Grid Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| Width (cols) | 16 | Number of pixel columns |
| Height (rows) | 8 | Number of pixel rows |
| Layout | Flat | Flat plane or Cylinder (sides only) |
| Spacing (m) | 5.0 | Metres between pixel centres |
| Offset X/Y/Z | 0, 5, 2 | Offset from vehicle root origin |
| Engine template | EngineA1 | Part template ID (A1–A6 quick-select) |

## Project Structure

```
blinky/                       ← Mod entry point (ImGui UI + lifecycle)
├── Mod.cs                    ← Main mod class (F11 window, UI controls)
├── Patcher.cs                ← Harmony render-skip patches for pixel parts
├── blinky.csproj
└── mod.toml

blinky.lib/                   ← Core reusable logic (headless, no blinken.lib dependency)
├── BlinkyGridManager.cs      ← Static singleton: scroll/static/off/pattern APIs
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
- Grids are registered by vehicle ID and discoverable from any consumer
- The mod UI (`Mod.cs`) is a thin ImGui layer that delegates all logic to `BlinkyGridManager`
- **`BlinkySubmod`** lives in `blinky.lib` and implements `ISubmod` from `ksa-abstractions.lib`; it is instantiated directly by the grant supermod
