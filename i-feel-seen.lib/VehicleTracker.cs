using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.IFeelSeenLib;

public class TrackedVehicle
{
    public Vehicle Vehicle = null!;
    public bool SeeMe = true;
}

public class VehicleTracker
{
    public List<TrackedVehicle> Tracked { get; } = new();

    public bool IsTracked(Vehicle vehicle)
    {
        foreach (var entry in Tracked)
            if (entry.Vehicle == vehicle && entry.SeeMe)
                return true;
        return false;
    }

    public bool AddVehicle(Vehicle vehicle)
    {
        foreach (var entry in Tracked)
            if (entry.Vehicle == vehicle)
                return false;

        Tracked.Add(new TrackedVehicle { Vehicle = vehicle, SeeMe = true });
        Console.WriteLine($"i-feel-seen: Tracking {vehicle.Id}");
        return true;
    }

    public bool RemoveVehicle(Vehicle vehicle)
    {
        for (int i = 0; i < Tracked.Count; i++)
        {
            if (Tracked[i].Vehicle == vehicle)
            {
                Console.WriteLine($"i-feel-seen: Untracked {vehicle.Id}");
                Tracked.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public void Clear()
    {
        Tracked.Clear();
    }
}
