# Its So Shiny Lib

Reusable implementation for the `its-so-shiny` light-part pixel grid mod.

## Purpose

This library contains the submod UI and all game-facing behavior so the standalone mod, unscience, and future RPC integrations can reuse the same state and APIs.

## Main Types

- `ItsSoShinySubmod` - `ISubmod` implementation with the Blinky-style ImGui create/manage UI
- `ShinyGridBuilder` - creates and destroys runtime `LightPart` grids on vehicles
- `ShinyGridManager` - registers grids and exposes off, pattern, static display, and scroll operations
- `ShinyPixelGrid` - scans `shiny_{gridName}_{row}_{col}` parts back into a grid
- `ShinyPixelCell` - wraps one host `LightPart` and its actual light-bearing subpart
- `ShinyGridConfig` - layout and placement settings for grid creation

## Implementation Notes

Each pixel is one built-in `LightPart`. The builder attaches created light parts under the vehicle root, connects them to battery-bearing parts when available, rebuilds the part tree once, and then controls pixel state through each light's stock `PowerConsumer` light switch. Color and intensity reuse Zippo's `LightController` helper.