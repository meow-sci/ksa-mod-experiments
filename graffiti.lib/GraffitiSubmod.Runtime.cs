using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.GraffitiLib;
public sealed partial class GraffitiSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("decals", () => _decals.Count > 0, GraffitiPatches.Apply, GraffitiPatches.Remove);
    }
}
