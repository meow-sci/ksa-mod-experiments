# Grant — Unified Supermod

A unified supermod that consolidates 10 standalone KSA mods into a single ImGui window with collapsible headers. Each submod's content appears under its own header, and a gear icon context menu lets you toggle individual submod visibility.

## Included Submods

| Submod | Description |
|--------|-------------|
| Average TWR | Samples thrust-to-weight ratio at 100 Hz and displays statistics |
| Blinky — Dynamic LCD Grid | Builds and controls pixel grids on vehicle light parts |
| Eternal Flame — Infinite Fuel | Monitors vehicles and periodically refills all fuel tanks |
| Gary's Torch | Welds vehicles together with position/rotation/scale offsets |
| Glass — Camera Lens | Overrides camera FOV with presets or manual control |
| I Feel Seen | Forces vehicle render data updates at any distance |
| Kiwi's Marbles | Welds celestial bodies to other orbiters with CCI offsets |
| Skittles — Theme Manager | Applies and saves ImGui themes with a built-in style editor |
| Unladen Swallow | HTTP RPC server for remote game control |
| Zippo — Light Control | Controls light part intensity and color on vehicles |

## Usage

- **F11** — Toggle the grant window
- **Gear icon (⚙)** — Opens a popup to show/hide individual submods
- Each submod has a **collapsible header** that can be expanded or collapsed
- The **Skittles Theme Editor** opens in a separate window via the "Open Theme Editor" button

## Architecture

- **`IGrantSubmod`** interface defines the submod contract: `Name`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- **`Mod.cs`** orchestrates all submods — creates them, calls `Update()` every frame for all (even hidden), renders only visible ones
- **`Patcher.cs`** consolidates Harmony patches from blinky (render-skip), glass (FOV override), i-feel-seen (render distance), and skittles (hotkey blocking)
- Submod files live in **`grant/Submods/`**, each wrapping `.lib` business logic with ImGui UI
- All business logic stays in the existing `.lib` projects — no duplication

## Dependencies

All `.lib` projects referenced: average-twr.lib, blinky.lib, eternal-flame.lib, garys-torch.lib, glass.lib, i-feel-seen.lib, kiwis-marbles.lib, skittles.lib, unladen-swallow.lib, zippo.lib, ksa-abstractions.lib, and others.
