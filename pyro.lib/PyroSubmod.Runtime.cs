using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.PyroLib;
public sealed partial class PyroSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("plumes", () => _plumes.Count > 0, PyroPatches.Apply, PyroPatches.Remove);
    }
}
