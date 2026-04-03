using System;

namespace MeowSci.SteelyEyedMissileKittenLib.Telemetry;

/// <summary>Immutable point-in-time snapshot of all telemetry for a single vehicle.</summary>
public sealed class TelemetrySnapshot
{
    // Identity
    public required string VehicleId    { get; init; }
    public required string VehicleName  { get; init; }
    public required double TimestampSec { get; init; }

    // Parent body
    public required string ParentBodyId              { get; init; }
    public required string ParentBodyName            { get; init; }
    public required bool   ParentHasAtmosphere       { get; init; }
    public required double ParentAtmosphereHeightM   { get; init; }

    // Altitude
    public required double BarometricAltitudeM { get; init; }
    public required double RadarAltitudeM      { get; init; }

    // Speed
    public required double OrbitalSpeedMps  { get; init; }
    public required double SurfaceSpeedMps  { get; init; }
    public required double InertialSpeedMps { get; init; }

    // Orbital parameters
    public required double ApoapsisM        { get; init; }
    public required double PeriapsisM       { get; init; }
    public required double ApoapsisAltitudeM  { get; init; }
    public required double PeriapsisAltitudeM { get; init; }
    public required double Eccentricity     { get; init; }
    public required double Inclination      { get; init; }
    public required double OrbitalPeriodSec { get; init; }
    public required double SemiMajorAxisM   { get; init; }

    // Mass
    public required double TotalMassKg     { get; init; }
    public required double InertMassKg     { get; init; }
    public required double PropellantMassKg { get; init; }

    // G-forces
    public required double GForceMagnitude { get; init; }
    public required double AccelX          { get; init; }
    public required double AccelY          { get; init; }
    public required double AccelZ          { get; init; }

    // State
    public required string Situation           { get; init; }
    public required bool   HasSurfaceContact   { get; init; }
    public required bool   IsLanded            { get; init; }
    public required bool   IsInAtmosphere      { get; init; }
    public required double AtmosphericPressurePa { get; init; }
    public required double AtmosphericDensity  { get; init; }

    // Position (ecliptic frame)
    public required double PosEclX { get; init; }
    public required double PosEclY { get; init; }
    public required double PosEclZ { get; init; }
}
