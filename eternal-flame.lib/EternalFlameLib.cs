using System;
using System.Collections.Generic;
using System.Linq;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.EternalFlameLib;

public sealed class MonitoredVehicle
{
    public string VehicleId { get; }
    public string DisplayName { get; }
    public bool RefillFuel { get; set; }
    public bool RefillElectricity { get; set; }

    public MonitoredVehicle(string vehicleId, string displayName)
    {
        VehicleId = vehicleId;
        DisplayName = displayName;
        RefillFuel = true;
        RefillElectricity = true;
    }
}

public sealed class FuelManager
{
    private readonly List<MonitoredVehicle> _monitored = new();
    private double _accumulatedMs;
    private long _lastElectricRefillTickMs;
    public int RefillIntervalMs { get; set; } = 100;

    public IReadOnlyList<MonitoredVehicle> MonitoredVehicles => _monitored;

    public void AddVehicle(string vehicleId, string displayName)
    {
        if (_monitored.Any(m => m.VehicleId == vehicleId))
            return;

        _monitored.Add(new MonitoredVehicle(vehicleId, displayName));
        Console.WriteLine($"eternal-flame: AddVehicle - vehicleId={vehicleId}, monitored={_monitored.Count}");
    }

    public void RemoveVehicle(string vehicleId)
    {
        int removed = _monitored.RemoveAll(m => m.VehicleId == vehicleId);
        Console.WriteLine($"eternal-flame: RemoveVehicle - vehicleId={vehicleId}, removed={removed}, monitored={_monitored.Count}");
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

        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
            return;

        foreach (var entry in _monitored)
        {
            if (!entry.RefillFuel)
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
                Console.WriteLine($"eternal-flame: Error refilling fuel {entry.DisplayName}: {ex.Message}");
            }
        }
    }

    public void UpdateElectricityBeforeVehicleSolvers()
    {
        if (_monitored.Count == 0)
            return;

        long now = Environment.TickCount64;
        int interval = Math.Max(RefillIntervalMs, 1);
        long elapsedMs = _lastElectricRefillTickMs == 0 ? interval : now - _lastElectricRefillTickMs;
        if (elapsedMs < interval)
            return;

        _lastElectricRefillTickMs = now;

        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
            return;

        foreach (var entry in _monitored)
        {
            if (!entry.RefillElectricity)
                continue;

            var vehicle = vehicles.FirstOrDefault(v => v.Id == entry.VehicleId);
            if (vehicle == null)
                continue;

            try
            {
                RefillBatteries(vehicle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"eternal-flame: Error refilling electricity {entry.DisplayName}: {ex.Message}\n{ex}");
            }
        }
    }

    private static void RefillBatteries(Vehicle vehicle)
    {
        var batteryStates = vehicle.Parts.Batteries;
        if (batteryStates.NumModules == 0)
            return;

        var modules = batteryStates.Modules;
        for (int i = 0; i < modules.Length; i++)
        {
            var battery = modules[i];
            var mutableRef = batteryStates.GetModuleAndAllMutableStatesForInitialization(battery);
            mutableRef.Module.Refill(ref mutableRef.State);
        }
    }
}
