# Blinken - LCD Animation System

A vehicle animation system that drives LCD pixel grids using part naming conventions. Animates pixels by toggling engine states on/off, enabling pixel animations and text scrolling effects on display panels.

## Overview

Blinken lets you:
- **Animate pixel displays** - Create LCD animations using vehicle parts
- **Scroll text animations** - Horizontal scrolling text effects
- **Pattern generation** - Pre-defined patterns (all-on, checkerboard, alternating rows/cols)
- **Debug vehicle structure** - Inspect vehicle part hierarchies
- **Test engine controllers** - Toggle engine states individually

## Features

- **Pixel grid scanning** - Automatically finds pixel parts in vehicles
- **Engine-based animation** - Uses EngineController to toggle pixels on/off
- **Scroll animations** - Smooth horizontal scrolling at configurable speed
- **Pattern library** - Reusable pattern generators
- **Per-frame updates** - Real-time animation playback
- **Vehicle debug output** - Reflection-based vehicle inspection
- **Column-based optimization** - Only updates when scroll column changes

## Architecture

### Core Classes

#### PixelGrid
Scans a vehicle for pixel display parts and manages EngineController access.

**Pixel Naming Convention**:
```
pixel_{row}_{column}_{a,b}
```

Example parts:
- `pixel_0_0_a` - Row 0, Column 0, First engine
- `pixel_0_0_b` - Row 0, Column 0, Second engine (LED pair)
- `pixel_1_5_a` - Row 1, Column 5, First engine

**Key Methods**:
- `ScanFromVehicle(Vehicle vehicle)` - Parse parts, cache EngineControllers
- `GetFirstController(int row, int col)` - Retrieve controller for grid cell
- `ToggleAll(bool enabled)` - Turn all pixels on/off

**Implementation**:
```csharp
public void ScanFromVehicle(Vehicle vehicle)
{
    var parts = PartHelpers.GetAllParts(vehicle);
    foreach (var part in parts)
    {
        if (part.PartTemplate.PartName.StartsWith("pixel_"))
        {
            var match = Regex.Match(
                part.PartTemplate.PartName, 
                @"pixel_(\d+)_(\d+)_([ab])"
            );
            
            int row = int.Parse(match.Groups[1].Value);
            int col = int.Parse(match.Groups[2].Value);
            
            CacheController(row, col, part.GetEngineController());
        }
    }
}
```

#### LcdAnimation
Manages animation playback and pixel updates.

**State**:
```csharp
public class LcdAnimation
{
    public int Width { get; set; }               // Grid width
    public int Height { get; set; }              // Grid height
    public double ScrollOffset { get; set; }     // Current scroll position
    public bool Enabled { get; set; }            // Animation active
    public float ScrollSpeed { get; set; }       // Pixels/second
    public PixelGrid PixelGrid { get; set; }     // Grid reference
}
```

**Key Methods**:
- `Init(PixelGrid grid)` - Initialize with pixel grid
- `Update(double deltaTime)` - Advance scroll, apply pixel updates
- `SetPixelData(bool[,] pattern)` - Set animation pattern

**Update Logic**:
```csharp
public void Update(double deltaTime)
{
    if (!Enabled) return;
    
    var previousColumn = (int)(ScrollOffset);
    ScrollOffset += ScrollSpeed * deltaTime;
    var currentColumn = (int)(ScrollOffset);
    
    // Only update if column changed
    if (currentColumn != previousColumn)
    {
        ApplyColumnToGrid(currentColumn);
    }
}
```

#### LcdAnimationPixels
Pre-defined animation pixel data (hardcoded or pattern-generated).

**Pattern Examples**:
- Scrolling text frames
- Filled rows/columns
- Animated sequences

#### PixelPatterns
Static utility for generating animation patterns.

**Pattern Types**:
- `AllOn` - All pixels lit
- `Checkerboard` - Alternating lit/dark
- `AlternatingRows` - Odd/even rows lit
- `AlternatingColumns` - Odd/even columns lit
- `ScrollingText` - Text frames for scrolling

## UI (Mod.cs)

ImGui window with:
- **Vehicle dump** - Debug hierarchy of selected vehicle
- **Animation controls** - Play/pause/stop buttons
- **Speed control** - Scroll speed slider (0.5 to 10.0 px/s)
- **Pattern selector** - Choose animation pattern
- **Engine test panel** - Toggle individual engines on/off for debugging
- **Grid visualization** - Real-time pixel state display

## Pixel Animation Workflow

### 1. Vehicle Setup
Design a vehicle with parts named following the `pixel_row_col_a/b` convention.

### 2. PixelGrid Scan
```csharp
var grid = new PixelGrid();
grid.ScanFromVehicle(vehicle);
// Now grid has EngineController references cached
```

### 3. Pattern Definition
Define animation pattern (hardcoded or generated):
```csharp
var pattern = new bool[height, width];
for (int r = 0; r < height; r++)
    for (int c = 0; c < width; c++)
        pattern[r, c] = (r + c) % 2 == 0;  // Checkerboard
```

### 4. Animation Playback
```csharp
var animation = new LcdAnimation { Pattern = pattern };
animation.Init(grid);

// Each frame:
animation.Update(deltaTime);
// Animation updates engine states
```

## Implementation Details

### Column-Based Update Optimization
Only pixels in the currently visible column are updated:

```csharp
// Instead of updating entire grid each frame:
if (int(ScrollOffset) != int(previousScrollOffset))
{
    int column = (int)(ScrollOffset) % width;
    for (int row = 0; row < height; row++)
    {
        bool pixelState = pattern[row, column];
        grid.GetFirstController(row, column)?.SetActive(pixelState);
    }
}
```

**Benefit**: O(height) updates instead of O(height × width)

### Wrapping Scroll
Scrolling with half-grid gap for seamless repeat:

```
[Pattern...] [Gap] [Pattern...] [Gap]
│           │      │           │
Seamless repeat        ^ScrollOffset
```

### Engine Controller Caching
Engine controllers are cached at scan time for efficiency:

```csharp
// Fast lookup: no part tree traversal each frame
var controller = cachedControllers[row][col];
controller?.SetActive(pixelState);
```

## Usage Example

```csharp
// Set up animation
var grid = new PixelGrid();
grid.ScanFromVehicle(playerVehicle);

var animation = new LcdAnimation();
animation.Init(grid);
animation.Enabled = true;
animation.ScrollSpeed = 3.0f;

// Update each frame
animation.Update(deltaTime);

// Check pixel state
bool pixelLit = grid.GetFirstController(0, 0)?.IsActive ?? false;
```

## Notes for Future Development

- **Resource loading**: Load animation patterns from files (JSON, CSV, images)
- **Text rendering**: Dynamically convert text to animated pixel patterns
- **Synchronization**: Sync animations across multiple vehicle displays
- **Performance**: Consider LOD for many pixels (update every N frames)
- **Color support**: If engine variants support color, extend to RGB pixels

## Technical Constraints

- **Part naming**: Strict `pixel_row_col_a/b` convention required
- **Engine-based**: Requires actual engine parts to drive pixels
- **Updates**: Only when integer column changes (efficient but coarse)
- **No physics**: Animations are purely visual, no vehicle behavior affected

## Dependencies

- **MeowSci.KsaAbstractions**: For part/vehicle access
- **KSA Game**: Engine controller and part system
