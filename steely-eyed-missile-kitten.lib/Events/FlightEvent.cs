using System;
using System.Collections.Generic;

namespace MeowSci.SteelyEyedMissileKittenLib.Events;

/// <summary>Represents a detected flight event. Immutable.</summary>
public sealed class FlightEvent
{
    public required FlightEventType Type { get; init; }
    public required string VehicleId { get; init; }
    public required string VehicleName { get; init; }
    public required double TimestampSec { get; init; }
    public required string ParentBodyId { get; init; }
    public required string Description { get; init; }

    /// <summary>Optional structured key-value details (e.g., old/new SOI, altitude).</summary>
    public Dictionary<string, string> Details { get; init; } = new();
}
