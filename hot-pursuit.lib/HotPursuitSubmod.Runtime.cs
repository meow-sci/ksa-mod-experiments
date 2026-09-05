using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.HotPursuitLib;
public sealed partial class HotPursuitSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("cameras", () => _cameras.Count > 0, HotPursuitPatches.Apply, HotPursuitPatches.Remove);
    }
}
