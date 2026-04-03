using System;

namespace MeowSci.SteelyEyedMissileKittenLib.Telemetry;

/// <summary>Speed reference frame for display purposes.</summary>
public enum SpeedFrame
{
    Orbital,   // CCI-frame velocity magnitude
    Surface,   // Velocity relative to rotating body surface
    Inertial   // Ecliptic-frame velocity magnitude
}
