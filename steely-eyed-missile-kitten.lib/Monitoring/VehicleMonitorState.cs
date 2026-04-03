using System;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.Monitoring;

/// <summary>Tracks per-vehicle monitoring state across sample ticks. Stores the previous snapshot for event comparison.</summary>
public sealed class VehicleMonitorState
{
    public string VehicleId { get; }
    public TelemetrySnapshot? PreviousSnapshot { get; set; }
    public TelemetrySnapshot? CurrentSnapshot { get; set; }

    // Debounce timers: track when each event type last fired for this vehicle
    public double LastSoiChangeTimeSec { get; set; } = double.MinValue;
    public double LastLandingTimeSec { get; set; } = double.MinValue;
    public double LastLiftoffTimeSec { get; set; } = double.MinValue;
    public double LastAtmosphereEntryTimeSec { get; set; } = double.MinValue;
    public double LastAtmosphereExitTimeSec { get; set; } = double.MinValue;
    public double LastStableOrbitTimeSec { get; set; } = double.MinValue;
    public double LastOrbitEscapeTimeSec { get; set; } = double.MinValue;
    public double LastSplashDownTimeSec { get; set; } = double.MinValue;

    public VehicleMonitorState(string vehicleId) => VehicleId = vehicleId;
}
