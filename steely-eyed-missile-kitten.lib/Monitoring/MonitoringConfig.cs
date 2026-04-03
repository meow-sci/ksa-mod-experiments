using System;

namespace MeowSci.SteelyEyedMissileKittenLib.Monitoring;

/// <summary>Configuration for the telemetry monitoring loop.</summary>
public sealed class MonitoringConfig
{
    private double _sampleIntervalSec = 0.5;

    /// <summary>Interval between telemetry samples in seconds. Default 0.5s (2 Hz). Clamped to [MinIntervalSec, MaxIntervalSec].</summary>
    public double SampleIntervalSec
    {
        get => _sampleIntervalSec;
        set => _sampleIntervalSec = Math.Clamp(value, MinIntervalSec, MaxIntervalSec);
    }

    /// <summary>Minimum allowed sample interval (50ms = 20 Hz max).</summary>
    public const double MinIntervalSec = 0.05;

    /// <summary>Maximum allowed sample interval (10 seconds).</summary>
    public const double MaxIntervalSec = 10.0;
}
