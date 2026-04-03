using System;
using System.Collections.Generic;
using MeowSci.SteelyEyedMissileKittenLib.Events;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

public enum ConditionType
{
    AltitudeAbove,
    AltitudeBelow,
    SpeedAbove,
    SpeedBelow,
    ApoapsisAbove,
    PeriapsisAbove,
    PeriapsisBelow,
    EccentricityBelow,
    InclinationBetween,
    EventOccurred,
    InSoiOf,
    OnSurfaceOf,
    AllOf,
    AnyOf,
    Sequence,
}

/// <summary>A single mission condition node, deserialized from YAML.</summary>
public sealed class MissionCondition
{
    public ConditionType Type { get; set; }

    // For threshold conditions
    public double? Value { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public SpeedFrame? SpeedFrame { get; set; }

    // For event conditions
    public FlightEventType? EventType { get; set; }

    // For location conditions
    public string? BodyId { get; set; }

    // For composite conditions
    public List<MissionCondition>? SubConditions { get; set; }

    // Display text
    public string? Description { get; set; }
}
