using System;
using System.Linq;
namespace MeowSci.EternalFlameLib;
public sealed partial class EternalFlameSubmod
{
    public void ReleaseLiveState()
    {
        if (_fuelManager != null) foreach (var item in _fuelManager.MonitoredVehicles.ToArray()) _fuelManager.RemoveVehicle(item.VehicleId);
    }
}
