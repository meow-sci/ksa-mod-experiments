using System;
using System.Collections.Generic;

namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

public enum MissionStatus { Active, Completed, Failed, Abandoned }

/// <summary>Runtime state for an active mission on a specific vehicle.</summary>
public sealed class MissionState
{
    public MissionStatus Status { get; set; } = MissionStatus.Active;
    public double StartedAtSec { get; set; }
    public double? CompletedAtSec { get; set; }

    /// <summary>Tracks sequence progress: maps sub-condition index to completion state.</summary>
    public Dictionary<int, bool> SequenceProgress { get; set; } = new();
}
