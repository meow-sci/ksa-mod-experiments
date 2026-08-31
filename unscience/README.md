# Unscience — Unified Supermod

A unified supermod that consolidates 13 standalone KSA mods into a single ImGui window with collapsible headers. Each submod's content appears under its own header, and a gear icon context menu lets you toggle individual submod visibility.

## Included Submods

| Submod | Description |
|--------|-------------|
| Average TWR | Samples thrust-to-weight ratio at 100 Hz and displays statistics |
| Blinky — Dynamic LCD Grid | Builds and controls pixel grids on vehicle light parts |
| Camera Controller Override | 8 camera animation types (zoom, spiral, orbit, shake) with keyframe sequencing |
| Eternal Flame — Infinite Fuel | Monitors vehicles and periodically refills all fuel tanks |
| Garry's Torch | Welds vehicles together with position/rotation/scale offsets |
| G-Force Monitor | Records and displays g-forces at 40 Hz with history, peak detection, and jerk analysis |
| Glass — Camera Lens | Overrides camera FOV with presets or manual control |
| Graffiti — PNG Decals | Click-to-place projected PNG decals on vehicle hulls and terrain |
| I Feel Seen | Forces vehicle render data updates at any distance |
| Its So Shiny | Builds and controls Blinky-style pixel grids from built-in light parts |
| Kitten Animations | Plays kitten avatar MMU animations, expressions, and walking animations |
| Kiwi's Marbles | Welds celestial bodies to other orbiters with CCI offsets |
| Skittles — Theme Manager | Applies and saves ImGui themes with a built-in style editor |
| Unladen Swallow | HTTP RPC server for remote game control |
| Zippo — Light Control | Controls light part intensity and color on vehicles |

## Usage

- **F11** — Toggle the unscience window
- **Gear icon (⚙)** — Opens a popup to show/hide individual submods
- Each submod has a **collapsible header** that can be expanded or collapsed
- The **Skittles Theme Editor** opens in a separate window via the "Open Theme Editor" button

## Architecture

- **`ISubmod`** interface (from `ksa-abstractions.lib`) defines the submod contract: `Name`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- **`Mod.cs`** orchestrates all submods — instantiates lib submod classes directly, calls `Update()` every frame for all (even hidden), renders only visible ones
- **Hidden-HUD (F2) resilience**: StarMap's `[StarMapBeforeGui]`/`[StarMapAfterGui]` hooks target `Program.OnDrawUiFrame`/`OnDrawUiViewports`, which the game skips entirely while the HUD is hidden — so without help every submod `Update()` and the garrys-torch weld update would freeze on F2. `Mod.cs` factors that non-UI work into `UpdateSubmods(dt)` / `UpdateWelds(dt)` and registers both with `HiddenUiFrameHook` (`ksa-abstractions.lib`), whose `Program.OnDrawUiConsole` prefix replays them at the same frame phase only while `Program.DrawUI` is false. The unscience window, floating windows and the F11 toggle stay hidden along with the HUD.
- **`Patcher.cs`** consolidates Harmony patches from blinky (render-skip), camera-controller-override (sequence playback via `CameraControllerOverridePatches`), garrys-torch (safe pre-vehicle-solver weld updates), glass (FOV override), graffiti (projected-decal render pass via `GraffitiPatches`), i-feel-seen (render distance), pyro (exhaust submission via `PyroPatches`), skittles (hotkey blocking), and dont-stifle-me (editor scale clamp / per-axis scaling via `EditorScalePatches`), delegating to patch helpers in each lib
- **Garry's Torch update timing**: Unscience hosts `GarrysTorchSubmod` directly, so it applies the same `Universe.ExecuteNextVehicleSolvers` prefix as the standalone mod. The regular submod `Update(dt)` path is intentionally non-mutating for weld physics.
- Submod implementations live in their respective **`.lib` projects** (e.g. `AverageTwrSubmod` in `average-twr.lib`, `BlinkySubmod` + `BlinkyPatchState` in `blinky.lib`, `CameraControllerOverrideSubmod` in `camera-controller-override.lib`, `GeeForceSubmod` in `geeforce.lib`, `KittenAnimationsSubmod` in `kitten-animations.lib`)
- **`unscience/Submods/`** directory has been removed — no intermediate wrapper layer
- Each lib submod owns its own ImGui `RenderContent()` — unscience just calls it

## Dependencies

All `.lib` projects referenced: average-twr.lib, blinky.lib, camera-controller-override.lib, eternal-flame.lib, garrys-torch.lib, geeforce.lib, glass.lib, i-feel-seen.lib, its-so-shiny.lib, kitten-animations.lib, kiwis-marbles.lib, skittles.lib, unladen-swallow.lib, zippo.lib, ksa-abstractions.lib, and others.
