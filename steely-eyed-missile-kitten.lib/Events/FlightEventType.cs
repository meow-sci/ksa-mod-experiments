using System;

namespace MeowSci.SteelyEyedMissileKittenLib.Events;

/// <summary>Types of detectable flight events.</summary>
public enum FlightEventType
{
    // SOI changes
    SoiChanged,

    // Surface transitions
    Liftoff,
    Landed,
    SplashDown,

    // Atmosphere transitions
    AtmosphereEntered,
    AtmosphereExited,

    // Orbital milestones
    StableOrbitAchieved,
    OrbitEscaped,
}
