using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.ItsSoShinyLib;
public sealed partial class ItsSoShinySubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("visibility", () => ShinyGridManager.Grids.Count > 0 && !ShinyPatchState.RenderShinyParts, ShinyPatches.Apply, ShinyPatches.Remove);
    }
}
