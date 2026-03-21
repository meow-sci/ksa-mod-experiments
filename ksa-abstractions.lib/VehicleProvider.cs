using System.Collections.Generic;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>Static helpers to get vehicles — wraps Program.ControlledVehicle and Universe.CurrentSystem.Vehicles.</summary>
public static class VehicleProvider
{
    /// <summary>Returns the currently player-controlled vehicle, or null if none.</summary>
    public static Vehicle? GetControlledVehicle() => Program.ControlledVehicle;

    /// <summary>Returns all vehicles in the current system, or an empty list if unavailable.</summary>
    public static List<Vehicle> GetAllVehicles() =>
        Universe.CurrentSystem?.Vehicles?.GetList() ?? new List<Vehicle>();
}
