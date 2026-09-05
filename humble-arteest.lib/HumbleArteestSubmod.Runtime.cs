using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.HumbleArteestLib;
public sealed partial class HumbleArteestSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("paint", () => VehiclePaint.Active, VehiclePaintPatches.Apply, VehiclePaintPatches.Remove);
        runtime.Patches("engines", () => EngineEmissive.GlobalEnabled || EngineEmissive.Overrides.Count > 0, EngineEmissivePatches.Apply, EngineEmissivePatches.Remove);
    }
}
