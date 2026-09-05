using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.EternalFlameLib;
public sealed partial class EternalFlameSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("solver", () => _fuelManager.MonitoredVehicles.Any(v => v.RefillElectricity), EternalFlamePatches.Apply, EternalFlamePatches.Remove);
    }
}
