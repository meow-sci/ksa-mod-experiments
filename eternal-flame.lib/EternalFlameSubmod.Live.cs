using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.EternalFlameLib;

public sealed partial class EternalFlameSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in _fuelManager.MonitoredVehicles.ToArray())
            yield return new LiveStateItem<MonitoredVehicle>(entry.VehicleId, "Fuel / electricity refill", entry.DisplayName, entry, item =>
            {
                bool fuel = item.RefillFuel, electricity = item.RefillElectricity;
                if (ImGui.Checkbox("Refill fuel", ref fuel)) item.RefillFuel = fuel;
                if (ImGui.Checkbox("Refill electricity", ref electricity)) item.RefillElectricity = electricity;
                int interval = _fuelManager.RefillIntervalMs;
                if (ImGui.DragInt(MeowSci.KsaAbstractions.FormField.Label("Shared interval (ms)"), ref interval, 1f, 0, 5000)) _fuelManager.RefillIntervalMs = interval;
                if (ImGui.Button(" Remove monitoring ")) _fuelManager.RemoveVehicle(item.VehicleId);
            });
    }

}
