using System;
using System.Collections.Generic;
using KSA;
using MeowSci.KsaAbstractions;
using MeowSci.SteelyEyedMissileKittenLib.Events;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.Monitoring;

/// <summary>Accumulator-based monitoring loop. Call Update(dt) every frame from OnBeforeUi.</summary>
public sealed class MonitoringLoop
{
    private readonly MonitoringConfig _config;
    private readonly EventDetector _detector;
    private readonly EventBus _eventBus;
    private readonly Dictionary<string, VehicleMonitorState> _vehicleStates = new();
    private double _accumulator;

    /// <summary>Expose current snapshots so UI can read them.</summary>
    public IReadOnlyDictionary<string, TelemetrySnapshot> CurrentSnapshots { get; private set; } =
        new Dictionary<string, TelemetrySnapshot>();

    public MonitoringLoop(MonitoringConfig config, EventDetector detector, EventBus eventBus)
    {
        _config = config;
        _detector = detector;
        _eventBus = eventBus;
    }

    /// <summary>Called every frame. Advances the accumulator and calls SampleAllVehicles when interval elapses.</summary>
    public void Update(double dt)
    {
        _accumulator += dt;
        while (_accumulator >= _config.SampleIntervalSec)
        {
            _accumulator -= _config.SampleIntervalSec;
            SampleAllVehicles();
        }
    }

    private void SampleAllVehicles()
    {
        try
        {
            double simTime = SimTimeProvider.GetElapsedTime().Seconds();
            var vehicles = VehicleProvider.GetAllVehicles();

            // Prune states for vehicles no longer in the system
            PruneStaleVehicles(vehicles);

            var snapshots = new Dictionary<string, TelemetrySnapshot>(vehicles.Count);

            foreach (var vehicle in vehicles)
            {
                try
                {
                    var state = GetOrCreateState(vehicle.Id);
                    state.PreviousSnapshot = state.CurrentSnapshot;
                    state.CurrentSnapshot = VehicleTelemetry.CaptureSnapshot(vehicle, simTime);
                    snapshots[vehicle.Id] = state.CurrentSnapshot;

                    if (state.PreviousSnapshot != null)
                    {
                        var events = _detector.DetectEvents(state);
                        foreach (var evt in events)
                            _eventBus.Publish(evt);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MonitoringLoop] Error sampling vehicle {vehicle.Id}: {ex.Message}");
                }
            }

            CurrentSnapshots = snapshots;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MonitoringLoop] Error in SampleAllVehicles: {ex.Message}");
        }
    }

    private void PruneStaleVehicles(List<Vehicle> currentVehicles)
    {
        var currentIds = new HashSet<string>();
        foreach (var v in currentVehicles)
            currentIds.Add(v.Id);

        var toRemove = new List<string>();
        foreach (var id in _vehicleStates.Keys)
        {
            if (!currentIds.Contains(id))
                toRemove.Add(id);
        }
        foreach (var id in toRemove)
            _vehicleStates.Remove(id);
    }

    private VehicleMonitorState GetOrCreateState(string vehicleId)
    {
        if (!_vehicleStates.TryGetValue(vehicleId, out var state))
        {
            state = new VehicleMonitorState(vehicleId);
            _vehicleStates[vehicleId] = state;
        }
        return state;
    }
}
