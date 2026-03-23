using System;
using System.Collections.Generic;
using System.Linq;
using KSA;

namespace MeowSci.EternalFlameLib;

public sealed class MonitoredVehicle
{
    public string VehicleId { get; }
    public string DisplayName { get; }
    public bool Active { get; set; }

    public MonitoredVehicle(string vehicleId, string displayName)
    {
        VehicleId = vehicleId;
        DisplayName = displayName;
        Active = true;
    }
}

public sealed class FuelManager
{
    private readonly List<MonitoredVehicle> _monitored = new();
    private double _accumulatedMs;
    public int RefillIntervalMs { get; set; } = 500;

    public IReadOnlyList<MonitoredVehicle> MonitoredVehicles => _monitored;

    public void AddVehicle(string vehicleId, string displayName)
    {
        if (_monitored.Any(m => m.VehicleId == vehicleId))
            return;

        _monitored.Add(new MonitoredVehicle(vehicleId, displayName));
    }

    public void RemoveVehicle(string vehicleId)
    {
        _monitored.RemoveAll(m => m.VehicleId == vehicleId);
    }

    public void Update(double deltaTimeSeconds)
    {
        if (_monitored.Count == 0)
            return;

        _accumulatedMs += deltaTimeSeconds * 1000.0;

        int interval = Math.Max(RefillIntervalMs, 1);
        if (_accumulatedMs < interval)
            return;

        _accumulatedMs -= interval;
        // Clamp to avoid runaway accumulation
        if (_accumulatedMs > interval * 2)
            _accumulatedMs = 0;

        var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
        if (vehicles == null)
            return;

        foreach (var entry in _monitored)
        {
            if (!entry.Active)
                continue;

            var vehicle = vehicles.FirstOrDefault(v => v.Id == entry.VehicleId);
            if (vehicle == null)
                continue;

            try
            {
                vehicle.RefillConsumables();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"eternal-flame: Error refilling {entry.DisplayName}: {ex.Message}");
            }
        }
    }
}
