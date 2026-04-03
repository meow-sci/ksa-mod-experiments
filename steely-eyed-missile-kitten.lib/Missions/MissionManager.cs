using System;
using System.Collections.Generic;
using System.Linq;
using MeowSci.SteelyEyedMissileKittenLib.Events;
using MeowSci.SteelyEyedMissileKittenLib.Persistence;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>Manages mission lifecycle: activating, evaluating, and completing missions per vehicle.</summary>
public sealed class MissionManager
{
    private readonly List<MissionDefinition> _definitions;
    private readonly Dictionary<(string missionId, string vehicleId), MissionState> _activeMissions = new();
    private readonly EventDatabase _db;
    private readonly List<FlightEvent> _recentEvents = new();
    private const int MaxRecentEvents = 1000;

    public MissionManager(List<MissionDefinition> definitions, EventDatabase db)
    {
        _definitions = definitions;
        _db = db;
    }

    public IReadOnlyList<MissionDefinition> Definitions => _definitions;
    public IReadOnlyDictionary<(string, string), MissionState> ActiveMissions => _activeMissions;

    /// <summary>Feed incoming events to the mission manager for EventOccurred condition evaluation.</summary>
    public void OnEvent(FlightEvent evt)
    {
        _recentEvents.Add(evt);
        if (_recentEvents.Count > MaxRecentEvents)
            _recentEvents.RemoveAt(0);
    }

    /// <summary>Activate a mission for a specific vehicle.</summary>
    public void ActivateMission(string missionId, string vehicleId, double simTimeSec)
    {
        var key = (missionId, vehicleId);
        if (_activeMissions.ContainsKey(key)) return;

        _activeMissions[key] = new MissionState
        {
            Status = MissionStatus.Active,
            StartedAtSec = simTimeSec
        };
        Console.WriteLine($"[MissionManager] Activated mission '{missionId}' for vehicle '{vehicleId}'");
    }

    /// <summary>Abandon an active mission.</summary>
    public void AbandonMission(string missionId, string vehicleId)
    {
        var key = (missionId, vehicleId);
        if (_activeMissions.TryGetValue(key, out var state))
        {
            state.Status = MissionStatus.Abandoned;
            _activeMissions.Remove(key);
        }
    }

    /// <summary>Evaluate all active missions. Call each monitoring tick.</summary>
    public void EvaluateAll(IReadOnlyDictionary<string, TelemetrySnapshot> currentSnapshots)
    {
        var completedKeys = new List<(string, string)>();

        foreach (var (key, state) in _activeMissions)
        {
            if (state.Status != MissionStatus.Active) continue;

            var (missionId, vehicleId) = key;
            if (!currentSnapshots.TryGetValue(vehicleId, out var snapshot)) continue;

            var definition = _definitions.FirstOrDefault(d => d.Id == missionId);
            if (definition?.Objective == null) continue;

            var vehicleEvents = _recentEvents.Where(e => e.VehicleId == vehicleId).ToList();

            try
            {
                if (MissionEvaluator.Evaluate(definition.Objective, snapshot, vehicleEvents, state))
                {
                    state.Status = MissionStatus.Completed;
                    state.CompletedAtSec = snapshot.TimestampSec;
                    completedKeys.Add(key);

                    _db.SaveMissionProgress(missionId, vehicleId, "completed", state.StartedAtSec, state.CompletedAtSec, null);
                    Console.WriteLine($"[MissionManager] Mission '{missionId}' completed by vehicle '{vehicleId}'!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MissionManager] Error evaluating mission '{missionId}': {ex.Message}");
            }
        }

        // Remove completed missions from active list
        foreach (var key in completedKeys)
            _activeMissions.Remove(key);
    }
}
