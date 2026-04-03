using System;

namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>A complete mission definition loaded from YAML.</summary>
public sealed class MissionDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public int Difficulty { get; set; } = 1;
    public MissionCondition? Objective { get; set; }
}
