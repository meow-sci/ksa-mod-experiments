# GeeForce - G-Force Recorder and Display

Real-time g-force monitoring system with ring-buffer history, peak detection, and jerk analysis. Displays acceleration forces acting on your vehicle with configurable history duration and threshold detection.

## Overview

GeeForce lets you:
- **Monitor g-forces in real-time** - See current acceleration in Gs
- **Record g-force history** - Maintain configurable time window (30s to 1h)
- **Analyze acceleration events** - Identify peak g-forces and jerk spikes
- **Detect kill-gee events** - Track when g-forces exceed safety threshold
- **Visualize data** - Graph g-force over time with scrub slider
- **Fine-tune thresholds** - Configure kill-gee and jerk alert levels

## Features

- **40Hz sampling** - 25ms intervals for smooth history
- **Ring buffer storage** - Efficient memory usage up to 1 hour
- **Per-axis acceleration** - Separate X/Y/Z body frame components
- **Jerk detection** - Rate of change of acceleration
- **Kill-gee tracking** - Breach detection and statistics
- **Multiple views** - Live mode and historical scrubbing
- **Axis visualization** - Toggle to show individual acceleration axes
- **History durations** - 30s, 1m, 2m, 5m, 10m, 30m, 1h options

## Architecture

### Core Classes

#### GForceRecorder
Ring buffer for storing g-force samples with statistical analysis.

**Key Data**:
```csharp
public class GForceSample
{
    public double Time { get; set; }
    public float Magnitude { get; set; }      // Overall g-force
    public float X { get; set; }              // Longitudinal accel
    public float Y { get; set; }              // Lateral accel
    public float Z { get; set; }              // Normal (up/down) accel
    public float Jerk { get; set; }           // Rate of change of g-force
}
```

**Key Methods**:
- `RecordSample(Vehicle vehicle, double timestamp)` - Sample current vehicle acceleration
- `FindIndexAtOrAfter(double time)` - Binary search for sample by timestamp
- `CheckKillGeesBreaches(double threshold)` - Count threshold crossings
- `CheckJerkBreaches(double threshold)` - Count jerk spikes
- `ComputeJerk()` - Rate of change calculation

#### GForceUI
Static methods for graph rendering and UI state management.

**Methods**:
- `RenderContent(GForceRecorder recorder, double sampleIntervalSec)` - Renders ImGui content without Begin/End window framing (used by `GeeForceSubmod` and embeddable in grant)
- `Render(ref bool visible, GForceRecorder recorder, double sampleIntervalSec)` - Full standalone window with Begin/End (legacy; calls RenderContent internally)
- `GetRequiredCapacity(double sampleIntervalSec)` - Calculates buffer capacity for the current history window
- `GetSelectedHistorySeconds()` - Returns currently selected history duration

#### GeeForceSubmod
ISubmod implementation that owns the sampling loop and delegates rendering to GForceUI.

**Architecture**:
- Implements `ISubmod` (from `ksa-abstractions.lib`): `Name="G-Force Monitor"`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- Owns the fixed 40Hz accumulator/sampling loop in `Update(dt)`
- `RenderContent()` calls `GForceUI.RenderContent()` — no window framing
- Used standalone via `geeforce/Mod.cs` (which wraps it in its own ImGui window) and embedded in grant's collapsible header

### Sampling Details

#### Sample Rate
- **Frequency**: 40 Hz (25ms interval)
- **Duration**: Up to 1 hour with LimitToHours
- **Buffer Size**: ~144,000 samples max at 40Hz for 1 hour
- **Memory**: ~6-10 MB for full 1-hour buffer

#### Acceleration Calculation
```csharp
// Sample vehicle's body-frame acceleration
var bodyAccel = vehicle.Velocity.GetBodyFrameAcceleration();
var surfaceGravity = ComputeSurfaceGravity(vehicle);

// Convert to g-forces
var gX = bodyAccel.X / surfaceGravity;
var gY = bodyAccel.Y / surfaceGravity;
var gZ = bodyAccel.Z / surfaceGravity;
var magnitude = Sqrt(gX² + gY² + gZ²);
```

#### Jerk Computation
```csharp
// Jerk = rate of change of acceleration
if (previousSample != null)
{
    var timeDelta = currentTime - previousSample.Time;
    var accelDelta = magnitude - previousSample.Magnitude;
    jerk = accelDelta / timeDelta;  // g/second
}
```

### Ring Buffer Implementation

Efficient circular buffer for fixed memory consumption:

```csharp
public class RingBuffer<T>
{
    private T[] buffer;
    private int writeIndex = 0;
    
    public void Write(T item)
    {
        buffer[writeIndex] = item;
        writeIndex = (writeIndex + 1) % buffer.Length;
    }
}
```

**Advantages**:
- Fixed memory footprint regardless of time window
- Oldest samples automatically overwritten
- O(1) insertion time
- Efficient for real-time streaming data

## UI (Mod.cs)

ImGui window with:
- **Real-time g-force display** - Current magnitude and per-axis values
- **History graph** - Line plot of g-force over time
- **Scrubber slider** - Jump to specific point in history
- **Duration selector** - 30s, 1m, 2m, 5m, 10m, 30m, 1h options
- **Statistics panel** - Peak g, mean g, max jerk
- **Threshold configuration** - Kill-gee limit and jerk alert level
- **Axis toggle** - Show/hide individual X/Y/Z acceleration lines
- **Mode indicator** - Live vs. scrubbed history view

## Statistical Measures

### Peak G-Force
```
peak = max(samples[i].Magnitude)
```
Maximum g-force experienced in history.

### Mean G-Force
```
mean = Σ(magnitude) / count
```
Average g-force over the history window.

### Kill-Gee Breaches
```
breaches = count(magnitude > kill_threshold)
```
Number of samples exceeding kill-gee threshold (default 9.0g).

### Jerk Events
```
jerk_breaches = count(|jerk| > jerk_threshold)
```
Number of samples where jerk spike exceeds threshold.

## Usage Example

```csharp
// Configure recorder
var recorder = new GForceRecorder(capacity: 144000);

// Sample each frame
recorder.RecordSample(vehicle, Time.time);

// Query history
var killGeeCount = recorder.CheckKillGeesBreaches(9.0f);
var peakG = recorder.GetPeakGForce();

// Display
Console.WriteLine($"Peak G: {peakG:F2}g");
Console.WriteLine($"Kill-Gee Events: {killGeeCount}");
```

## Configuration

Configurable via ImGui:

| Setting | Range | Notes |
|---------|-------|-------|
| Kill-Gee Threshold | 0.1 to 50.0g | Alert threshold |
| Jerk Threshold | 0.1 to 10.0g/s | Rate of change alert |
| History Duration | 30s - 1h | Time window |
| Show Axes | true/false | Display X/Y/Z lines |
| Show Jerk | true/false | Overlay jerk curve |

## Performance Characteristics

- **Sampling Overhead**: Minimal—one acceleration read, a few calculations
- **Buffer Memory**: ~6-10 MB peak at 1 hour, 40Hz
- **Graph Rendering**: Linear in visible window (typically ~200 points)
- **Search Performance**: Binary search O(log n) for timestamp lookup

## Safety Considerations

### Kill-Gee Limit
```
typical human tolerance: ~9 Gs (can lose consciousness)
```
Default threshold of 9.0g provides warning system.

### Jerk Limit
```
typical discomfort: > 2-3 g/s
```
Sudden acceleration changes can be dangerous.

## Implementation Details

### Binary Search for Timestamp
```csharp
public int FindIndexAtOrAfter(double time)
{
    // Search for first sample with Time >= query time
    // O(log n) complexity
    int left = 0, right = buffer.Length - 1;
    while (left < right)
    {
        int mid = (left + right) / 2;
        if (buffer[mid].Time < time)
            left = mid + 1;
        else
            right = mid;
    }
    return left;
}
```

### Surface Gravity Calculation
```csharp
// Varies by altitude and celestial body
surfaceGravity = GravitationalParameter / (distance²)
```

## Notes for Future Development

- **Data export**: CSV export for external analysis
- **Recording**: Save/load g-force history sessions
- **Alerts**: Audio/visual warnings for threshold breaches
- **Prediction**: Estimate peak g based on current trajectory
- **Comparison**: Compare g-force profiles between maneuvers
- **Real-time analysis**: Detect specific flight events (landing, dock, etc.)
- **Multi-vehicle**: Track g-forces for multiple vehicles simultaneously

## Dependencies

- **MeowSci.KsaAbstractions**: For vehicle state access
- **KSA Game**: Vehicle acceleration and gravity
