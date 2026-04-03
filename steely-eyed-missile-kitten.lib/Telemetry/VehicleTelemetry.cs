using System;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.SteelyEyedMissileKittenLib.Telemetry;

/// <summary>Reads live telemetry from a KSA Vehicle and produces immutable snapshots.</summary>
public static class VehicleTelemetry
{
    private const double StandardGravity = 9.80665;

    /// <summary>Captures a full telemetry snapshot from the given vehicle at the given sim time.</summary>
    /// <returns>A populated snapshot, or a safe empty snapshot if the vehicle throws.</returns>
    public static TelemetrySnapshot CaptureSnapshot(Vehicle vehicle, double simTimeSec)
    {
        try
        {
            return BuildSnapshot(vehicle, simTimeSec);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VehicleTelemetry] Error capturing snapshot for vehicle: {ex.Message}");
            return BuildEmptySnapshot(vehicle?.Id ?? "unknown", simTimeSec);
        }
    }

    /// <summary>Computes the 3D Euclidean distance between two snapshots using ecliptic coordinates.</summary>
    public static double ComputeDistance(TelemetrySnapshot a, TelemetrySnapshot b)
    {
        double dx = a.PosEclX - b.PosEclX;
        double dy = a.PosEclY - b.PosEclY;
        double dz = a.PosEclZ - b.PosEclZ;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static TelemetrySnapshot BuildSnapshot(Vehicle vehicle, double simTimeSec)
    {
        // Identity
        string vehicleId   = vehicle.Id;
        string vehicleName = vehicle.Id;

        // Parent body
        IParentBody? parent   = vehicle.Parent;
        string parentBodyId   = parent?.Id ?? "";
        string parentBodyName = parent?.Id ?? "";
        double parentMeanRadius = parent?.MeanRadius ?? 0.0;

        // Atmosphere
        AtmosphereReference? atmo     = parent?.GetAtmosphereReference();
        bool   parentHasAtmosphere    = atmo != null;
        double parentAtmosphereHeightM = atmo?.Physical.Height.InMeters() ?? 0.0;

        // Altitude
        double baroAlt  = vehicle.GetBarometricAltitude();
        double radarAlt = vehicle.GetRadarAltitude();

        // Speed
        double orbSpeed   = vehicle.OrbitalSpeed;
        double surfSpeed  = vehicle.GetSurfaceSpeed();
        double inertSpeed = vehicle.GetInertialSpeed();

        // Orbital parameters
        double apoRaw  = vehicle.Orbit.Apoapsis;
        double periRaw = vehicle.Orbit.Periapsis;
        double apoM    = double.IsFinite(apoRaw)  ? apoRaw  : 0.0;
        double periM   = double.IsFinite(periRaw) ? periRaw : 0.0;
        double apoAltM  = double.IsFinite(apoRaw)  ? apoRaw  - parentMeanRadius : 0.0;
        double periAltM = double.IsFinite(periRaw) ? periRaw - parentMeanRadius : 0.0;
        double ecc     = double.IsFinite(vehicle.Orbit.Eccentricity) ? vehicle.Orbit.Eccentricity : 0.0;
        double inc     = double.IsFinite(vehicle.Orbit.Inclination)  ? vehicle.Orbit.Inclination  : 0.0;
        double period  = double.IsFinite(vehicle.Orbit.Period)       ? vehicle.Orbit.Period       : 0.0;
        double sma     = double.IsFinite(vehicle.Orbit.SemiMajorAxis) ? vehicle.Orbit.SemiMajorAxis : 0.0;

        // Mass (float → double implicit)
        double totalMass = vehicle.TotalMass;
        double inertMass = vehicle.InertMass;
        double propMass  = vehicle.PropellantMass;

        // G-forces
        double3 acc = vehicle.AccelerationBody;
        double gMag = acc.Length() / StandardGravity;

        // Situation / state
        Situation sit          = vehicle.Situation;
        bool hasSurfaceContact = sit.HasAnyContact();
        bool isLanded          = sit == Situation.Landed
                               || sit == Situation.Floating
                               || sit == Situation.Sailing;
        bool isInAtm = parentHasAtmosphere
                       && baroAlt >= 0.0
                       && baroAlt < parentAtmosphereHeightM;

        // Atmospheric pressure & density at current altitude
        double atmPressure = 0.0;
        double atmDensity  = 0.0;
        if (atmo != null)
        {
            try
            {
                atmPressure = atmo.Physical.GetAtmosphericPressureAtAltitude(baroAlt);
                atmDensity  = atmo.Physical.GetAtmosphericDensityAtAltitude(baroAlt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VehicleTelemetry] Atmosphere query failed: {ex.Message}");
            }
        }

        // Position (ecliptic frame)
        double3 pos = vehicle.GetPositionEcl();

        return new TelemetrySnapshot
        {
            VehicleId   = vehicleId,
            VehicleName = vehicleName,
            TimestampSec = simTimeSec,

            ParentBodyId            = parentBodyId,
            ParentBodyName          = parentBodyName,
            ParentHasAtmosphere     = parentHasAtmosphere,
            ParentAtmosphereHeightM = parentAtmosphereHeightM,

            BarometricAltitudeM = baroAlt,
            RadarAltitudeM      = radarAlt,

            OrbitalSpeedMps  = orbSpeed,
            SurfaceSpeedMps  = surfSpeed,
            InertialSpeedMps = inertSpeed,

            ApoapsisM         = apoM,
            PeriapsisM        = periM,
            ApoapsisAltitudeM  = apoAltM,
            PeriapsisAltitudeM = periAltM,
            Eccentricity      = ecc,
            Inclination       = inc,
            OrbitalPeriodSec  = period,
            SemiMajorAxisM    = sma,

            TotalMassKg      = totalMass,
            InertMassKg      = inertMass,
            PropellantMassKg = propMass,

            GForceMagnitude = gMag,
            AccelX          = acc.X,
            AccelY          = acc.Y,
            AccelZ          = acc.Z,

            Situation         = sit.ToString(),
            HasSurfaceContact = hasSurfaceContact,
            IsLanded          = isLanded,
            IsInAtmosphere    = isInAtm,
            AtmosphericPressurePa = atmPressure,
            AtmosphericDensity    = atmDensity,

            PosEclX = pos.X,
            PosEclY = pos.Y,
            PosEclZ = pos.Z,
        };
    }

    private static TelemetrySnapshot BuildEmptySnapshot(string vehicleId, double simTimeSec)
    {
        return new TelemetrySnapshot
        {
            VehicleId   = vehicleId,
            VehicleName = vehicleId,
            TimestampSec = simTimeSec,

            ParentBodyId            = "",
            ParentBodyName          = "",
            ParentHasAtmosphere     = false,
            ParentAtmosphereHeightM = 0.0,

            BarometricAltitudeM = 0.0,
            RadarAltitudeM      = 0.0,

            OrbitalSpeedMps  = 0.0,
            SurfaceSpeedMps  = 0.0,
            InertialSpeedMps = 0.0,

            ApoapsisM         = 0.0,
            PeriapsisM        = 0.0,
            ApoapsisAltitudeM  = 0.0,
            PeriapsisAltitudeM = 0.0,
            Eccentricity      = 0.0,
            Inclination       = 0.0,
            OrbitalPeriodSec  = 0.0,
            SemiMajorAxisM    = 0.0,

            TotalMassKg      = 0.0,
            InertMassKg      = 0.0,
            PropellantMassKg = 0.0,

            GForceMagnitude = 0.0,
            AccelX          = 0.0,
            AccelY          = 0.0,
            AccelZ          = 0.0,

            Situation         = "",
            HasSurfaceContact = false,
            IsLanded          = false,
            IsInAtmosphere    = false,
            AtmosphericPressurePa = 0.0,
            AtmosphericDensity    = 0.0,

            PosEclX = 0.0,
            PosEclY = 0.0,
            PosEclZ = 0.0,
        };
    }
}
