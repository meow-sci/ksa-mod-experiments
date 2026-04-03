using System;
using System.Collections.Generic;
using MeowSci.SteelyEyedMissileKittenLib.Monitoring;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.Events;

/// <summary>
/// Stateless event detector. Compares two consecutive TelemetrySnapshots and returns detected events.
/// All event detection logic is isolated here; state lives in VehicleMonitorState.
/// </summary>
public sealed class EventDetector
{
    private const double EventDebounceSec = 2.0;

    /// <summary>Detects events by comparing state.PreviousSnapshot to state.CurrentSnapshot.</summary>
    public List<FlightEvent> DetectEvents(VehicleMonitorState state)
    {
        var events = new List<FlightEvent>();
        var prev = state.PreviousSnapshot!;
        var curr = state.CurrentSnapshot!;

        CheckSoiChange(prev, curr, state, events);
        CheckLiftoff(prev, curr, state, events);
        CheckLanding(prev, curr, state, events);
        CheckSplashDown(prev, curr, state, events);
        CheckAtmosphereEntry(prev, curr, state, events);
        CheckAtmosphereExit(prev, curr, state, events);
        CheckStableOrbit(prev, curr, state, events);
        CheckOrbitEscape(prev, curr, state, events);

        return events;
    }

    private static bool CanFire(double lastTimeSec, double currentTimeSec)
        => currentTimeSec - lastTimeSec > EventDebounceSec;

    private static void CheckSoiChange(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        if (prev.ParentBodyId == curr.ParentBodyId) return;
        if (!CanFire(state.LastSoiChangeTimeSec, curr.TimestampSec)) return;

        state.LastSoiChangeTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.SoiChanged,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} entered {curr.ParentBodyId}'s sphere of influence",
            Details = new() { ["old_body"] = prev.ParentBodyId, ["new_body"] = curr.ParentBodyId }
        });
    }

    private static void CheckLiftoff(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        // Was landed AND now not on surface and not in a floating/sailing situation
        if (!prev.IsLanded) return;
        if (curr.HasSurfaceContact) return;
        // Exclude Floating/Sailing (splashdown scenario)
        if (curr.Situation is "Floating" or "Sailing") return;
        if (!CanFire(state.LastLiftoffTimeSec, curr.TimestampSec)) return;

        state.LastLiftoffTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.Liftoff,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} lifted off from {curr.ParentBodyId}",
            Details = new() { ["body"] = curr.ParentBodyId, ["altitude_m"] = curr.BarometricAltitudeM.ToString("F1") }
        });
    }

    private static void CheckLanding(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        // Was airborne AND now has terrain contact (but not ocean)
        if (prev.HasSurfaceContact) return;
        // Must have terrain contact (landed situation)
        if (curr.Situation != "Landed" && curr.Situation != "Rolling") return;
        if (!CanFire(state.LastLandingTimeSec, curr.TimestampSec)) return;

        state.LastLandingTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.Landed,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} landed on {curr.ParentBodyId}",
            Details = new() { ["body"] = curr.ParentBodyId, ["speed_mps"] = curr.SurfaceSpeedMps.ToString("F1") }
        });
    }

    private static void CheckSplashDown(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        // Was airborne AND now floating/sailing on ocean
        if (prev.HasSurfaceContact) return;
        if (curr.Situation != "Floating" && curr.Situation != "Sailing") return;
        if (!CanFire(state.LastSplashDownTimeSec, curr.TimestampSec)) return;

        state.LastSplashDownTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.SplashDown,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} splashed down in the ocean at {curr.ParentBodyId}",
            Details = new() { ["body"] = curr.ParentBodyId, ["speed_mps"] = curr.SurfaceSpeedMps.ToString("F1") }
        });
    }

    private static void CheckAtmosphereEntry(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        if (prev.IsInAtmosphere || !curr.IsInAtmosphere) return;
        if (!CanFire(state.LastAtmosphereEntryTimeSec, curr.TimestampSec)) return;

        state.LastAtmosphereEntryTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.AtmosphereEntered,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} entered {curr.ParentBodyId}'s atmosphere",
            Details = new() { ["body"] = curr.ParentBodyId, ["altitude_m"] = curr.BarometricAltitudeM.ToString("F0") }
        });
    }

    private static void CheckAtmosphereExit(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        if (!prev.IsInAtmosphere || curr.IsInAtmosphere) return;
        if (!CanFire(state.LastAtmosphereExitTimeSec, curr.TimestampSec)) return;

        state.LastAtmosphereExitTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.AtmosphereExited,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} exited {curr.ParentBodyId}'s atmosphere",
            Details = new() { ["body"] = curr.ParentBodyId, ["altitude_m"] = curr.BarometricAltitudeM.ToString("F0") }
        });
    }

    private static void CheckStableOrbit(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        // Periapsis crossed above atmosphere (or above surface if no atmosphere)
        // Must be a bound orbit (eccentricity < 1)
        if (curr.Eccentricity >= 1.0 || !double.IsFinite(curr.Eccentricity)) return;
        if (curr.PeriapsisAltitudeM <= 0) return;

        double safeAlt = curr.ParentAtmosphereHeightM > 0 ? curr.ParentAtmosphereHeightM : 0;
        bool prevPeBelow = prev.PeriapsisAltitudeM <= safeAlt;
        bool currPeAbove = curr.PeriapsisAltitudeM > safeAlt;

        if (!prevPeBelow || !currPeAbove) return;
        if (!CanFire(state.LastStableOrbitTimeSec, curr.TimestampSec)) return;

        state.LastStableOrbitTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.StableOrbitAchieved,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} achieved stable orbit around {curr.ParentBodyId}",
            Details = new()
            {
                ["body"] = curr.ParentBodyId,
                ["periapsis_m"] = curr.PeriapsisAltitudeM.ToString("F0"),
                ["apoapsis_m"] = curr.ApoapsisAltitudeM.ToString("F0")
            }
        });
    }

    private static void CheckOrbitEscape(TelemetrySnapshot prev, TelemetrySnapshot curr, VehicleMonitorState state, List<FlightEvent> events)
    {
        // Eccentricity crossed from < 1.0 to >= 1.0 (hyperbolic escape)
        if (prev.Eccentricity >= 1.0 || curr.Eccentricity < 1.0) return;
        if (!double.IsFinite(curr.Eccentricity)) return;
        if (!CanFire(state.LastOrbitEscapeTimeSec, curr.TimestampSec)) return;

        state.LastOrbitEscapeTimeSec = curr.TimestampSec;
        events.Add(new FlightEvent
        {
            Type = FlightEventType.OrbitEscaped,
            VehicleId = curr.VehicleId,
            VehicleName = curr.VehicleName,
            TimestampSec = curr.TimestampSec,
            ParentBodyId = curr.ParentBodyId,
            Description = $"{curr.VehicleName} escaped {curr.ParentBodyId}'s gravity (hyperbolic trajectory)",
            Details = new()
            {
                ["body"] = curr.ParentBodyId,
                ["eccentricity"] = curr.Eccentricity.ToString("F4"),
                ["speed_mps"] = curr.OrbitalSpeedMps.ToString("F1")
            }
        });
    }
}
