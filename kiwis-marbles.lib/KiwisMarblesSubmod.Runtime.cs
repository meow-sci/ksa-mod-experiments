using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.KiwisMarblesLib;
public sealed partial class KiwisMarblesSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("solver", () => _welds.Count > 0 || _pendingRestores.Count > 0, KiwisMarblesPatches.Apply, KiwisMarblesPatches.Remove);
    }
}
