using System;
using System.Collections.Generic;
using System.Linq;
using MeowSci.SteelyEyedMissileKittenLib.Events;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>Evaluates mission condition trees against current telemetry and event history.</summary>
public static class MissionEvaluator
{
    public static bool Evaluate(
        MissionCondition condition,
        TelemetrySnapshot snapshot,
        List<FlightEvent> eventHistory,
        MissionState state)
    {
        if (condition == null) return false;

        return condition.Type switch
        {
            ConditionType.AltitudeAbove     => snapshot.BarometricAltitudeM > condition.Value.GetValueOrDefault(),
            ConditionType.AltitudeBelow     => snapshot.BarometricAltitudeM < condition.Value.GetValueOrDefault(),
            ConditionType.SpeedAbove        => GetSpeed(snapshot, condition.SpeedFrame) > condition.Value.GetValueOrDefault(),
            ConditionType.SpeedBelow        => GetSpeed(snapshot, condition.SpeedFrame) < condition.Value.GetValueOrDefault(),
            ConditionType.ApoapsisAbove     => snapshot.ApoapsisAltitudeM > condition.Value.GetValueOrDefault(),
            ConditionType.PeriapsisAbove    => snapshot.PeriapsisAltitudeM > condition.Value.GetValueOrDefault(),
            ConditionType.PeriapsisBelow    => snapshot.PeriapsisAltitudeM < condition.Value.GetValueOrDefault(),
            ConditionType.EccentricityBelow => snapshot.Eccentricity < condition.Value.GetValueOrDefault(),
            ConditionType.InclinationBetween => snapshot.Inclination >= condition.MinValue.GetValueOrDefault()
                                               && snapshot.Inclination <= condition.MaxValue.GetValueOrDefault(Math.PI),
            ConditionType.InSoiOf           => string.Equals(snapshot.ParentBodyId, condition.BodyId, StringComparison.OrdinalIgnoreCase),
            ConditionType.OnSurfaceOf       => snapshot.IsLanded && string.Equals(snapshot.ParentBodyId, condition.BodyId, StringComparison.OrdinalIgnoreCase),
            ConditionType.EventOccurred     => eventHistory.Any(e => e.Type == condition.EventType && e.VehicleId == snapshot.VehicleId),
            ConditionType.AllOf             => condition.SubConditions?.All(c => Evaluate(c, snapshot, eventHistory, state)) == true,
            ConditionType.AnyOf             => condition.SubConditions?.Any(c => Evaluate(c, snapshot, eventHistory, state)) == true,
            ConditionType.Sequence          => EvaluateSequence(condition, snapshot, eventHistory, state),
            _                               => false
        };
    }

    private static double GetSpeed(TelemetrySnapshot snap, SpeedFrame? frame)
    {
        return frame switch
        {
            SpeedFrame.Surface  => snap.SurfaceSpeedMps,
            SpeedFrame.Inertial => snap.InertialSpeedMps,
            _                   => snap.OrbitalSpeedMps,
        };
    }

    private static bool EvaluateSequence(MissionCondition condition, TelemetrySnapshot snapshot, List<FlightEvent> eventHistory, MissionState state)
    {
        if (condition.SubConditions == null || condition.SubConditions.Count == 0)
            return true;

        // Find the first incomplete sub-condition
        for (int i = 0; i < condition.SubConditions.Count; i++)
        {
            if (state.SequenceProgress.TryGetValue(i, out bool done) && done)
                continue; // Already completed

            // Try to complete this step
            bool met = Evaluate(condition.SubConditions[i], snapshot, eventHistory, state);
            if (met)
                state.SequenceProgress[i] = true;
            else
                return false; // Must complete in order - stop at first unmet
        }

        return true; // All steps completed
    }
}
