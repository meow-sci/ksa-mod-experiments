# Average TWR - Thrust-to-Weight Ratio Calculator

Real-time TWR (Thrust-to-Weight Ratio) calculator and statistics display. Monitors vehicle performance metrics including TWR, maximum acceleration, and derived statistics at 100Hz sample rate.

## Overview

Average TWR lets you:
- **Monitor TWR in real-time** - Displays current and historical thrust-to-weight ratio
- **Track maximum acceleration** - Compute and display vehicle's max linear acceleration
- **Collect detailed statistics** - Mean, standard deviation, harmonic mean, and Brachi mean
- **Control sampling** - Start/pause/reset collection at any time
- **High-frequency monitoring** - 100Hz sample rate for fine-grained data

## Features

- **Real-time statistics** - Instantly available TWR and acceleration metrics
- **100Hz sampling** - High frequency data collection (10ms intervals)
- **Advanced statistics** - Multiple statistical measures for different analysis needs
- **Manual control** - Start/pause/reset collection via ImGui buttons
- **Harmonic mean** - For efficiency-weighted average TWR
- **Brachi mean** - Non-linear mean for acceleration analysis
- **No patching** - Pure data collection, no runtime patching required

## Architecture

### Core Classes

#### TwrDataReader
Static methods for reading TWR and computing acceleration from vehicle state.

**Key Methods**:
- `ReadTwr(Vehicle vehicle)` - Returns `vehicle.NavBallData.ThrustWeightRatio`
- `ComputeSurfaceGravity(Vehicle vehicle)` - Gets gravity at vehicle altitude
- `ComputeMaxAcceleration(Vehicle vehicle)` - Returns `TotalThrust / TotalMass` in m/s²

**Implementation**:
```csharp
public static float ReadTwr(Vehicle vehicle)
{
    return vehicle.NavBallData.ThrustWeightRatio;
}

public static float ComputeMaxAcceleration(Vehicle vehicle)
{
    return vehicle.TotalThrust / vehicle.TotalMass;
}
```

#### TwrSampleAccumulator
Accumulates samples and computes running statistics.

**State Tracked**:
```csharp
public int SampleCount { get; set; }
public float SumTwr { get; set; }
public float SumAccel { get; set; }
public float SumTwrSquared { get; set; }
public float SumAccelSquared { get; set; }
public float SumInverseTwrSqrt { get; set; }  // For harmonic mean
public float HarmonicMeanHelper { get; set; }  // For Brachi mean
```

**Sample Method**:
```csharp
public void AddSample(float twr, float accel)
{
    SampleCount++;
    SumTwr += twr;
    SumAccel += accel;
    SumTwrSquared += twr * twr;
    SumAccelSquared += accel * accel;
    // ... harmonic/brachi calculations
}
```

#### TwrStatistics
Static methods for computing various statistical measures.

**Statistics Computed**:
1. **Mean** - Arithmetic average
2. **Standard Deviation** - Measure of variance
3. **Harmonic Mean** - For efficiency/resistance analysis
4. **Brachi Mean** - Non-linear mean for acceleration

### UI (Mod.cs)

ImGui window with:
- **Start/Pause/Reset Buttons** - Collection control
- **Sample Count Display** - Number of samples collected
- **TWR Display** - Current TWR, mean TWR, std dev, harmonic mean, brachi mean
- **Acceleration Display** - Current accel, mean accel, std dev
- **Sampling Rate Indicator** - 100Hz @ 10ms intervals
- **Statistics Table** - Formatted display of all metrics

## Statistical Measures

### Mean (Average)
```
mean = Σx / n
```
Simple arithmetic average of all samples.

### Standard Deviation
```
stddev = √(Σ(x - mean)² / n)
       = √(Σx² / n - mean²)
```
Measures dispersion around the mean. Calculated efficiently using sum-of-squares formula.

### Harmonic Mean
```
harmonic_mean = n / Σ(1/x)
              = 1 / mean(1/x)
```
Used for rates and ratios. Emphasis on lower values. Useful for analyzing efficiency over varied conditions.

### Brachi Mean
```
brachi_mean = (1 / mean(1/√(x)))²
            = (Σ√(1/x) / n)⁻²
```
Non-linear mean based on inverse square roots. Emphasizes mid-range values over extremes.

## Sampling Details

### Sample Rate
- **Frequency**: 100 Hz (10ms intervals)
- **Precision**: Fine-grained vehicle state capture
- **Overhead**: Minimal; pure calculation, no I/O

### Sample Collection Loop
```csharp
if (IsCollecting && deltaTime >= 0.01f)  // 100Hz = 10ms
{
    var twr = TwrDataReader.ReadTwr(vehicle);
    var accel = TwrDataReader.ComputeMaxAcceleration(vehicle);
    accumulator.AddSample(twr, accel);
    deltaTime -= 0.01f;  // Reset timer
}
```

## Usage Example

```csharp
// Start collecting samples
accumulator.Clear();
isCollecting = true;

// ... some time passes, samples accumulate ...

// Read statistics
var meanTwr = TwrStatistics.ComputeMean(accumulator.SumTwr, accumulator.SampleCount);
var stdDev = TwrStatistics.ComputeStdDev(
    accumulator.SumTwrSquared, 
    accumulator.SumTwr, 
    accumulator.SampleCount
);

// Display or log
Console.WriteLine($"Mean TWR: {meanTwr:F2}");
Console.WriteLine($"Std Dev: {stdDev:F2} ({stdDev/meanTwr*100:F1}%)");
```

## Output Format

Display shows all metrics with context:

```
TWR Analysis:
  Current TWR:        3.45
  Mean TWR:           3.21 ± 0.52
  Harmonic Mean:      3.10
  Brachi Mean:        3.18
  Samples:            1,243

Acceleration Analysis:
  Current Accel:      33.8 m/s²
  Mean Accel:         31.2 m/s² ± 2.1
  Peak Accel:         36.5 m/s²
```

## Implementation Details

### Standard Deviation Formula
To avoid recalculating mean:
```
stddev = √((Σx² / n) - mean²)

where:
  mean = (Σx / n)
  Σx² = sum of squares
```

### Harmonic Mean Guard
```csharp
if (value > 0.0001f)  // Guard against division by zero
    sumInversed += 1f / value;
```

### Brachi Mean Guard
```csharp
if (value > 0.0001f)
    sumInverseSqrt += 1f / Mathf.Sqrt(value);

brachi = 1f / Mathf.Pow(sumInverseSqrt / count, 2f);
```

## Notes for Future Development

- **Persistence**: Save/load collected samples to file
- **Visualization**: Graph TWR over time
- **Thresholds**: Alert when TWR drops below certain values
- **Recording**: Automatic recording during specific scenarios (ascent, descent, etc.)
- **Export**: CSV export for external analysis
- **Real-time analysis**: Detect thrust curves, efficiency transitions

## Performance Characteristics

- **Memory**: Accumulator constants regardless of sample count
- **CPU**: Lightweight—one division, a few additions per sample
- **Storage**: No sample history kept; only running statistics
- **Accuracy**: Sufficient for most flight analysis use cases

## Dependencies

- **MeowSci.KsaAbstractions**: For vehicle access
- **KSA Game**: Vehicle class and NavBallData
