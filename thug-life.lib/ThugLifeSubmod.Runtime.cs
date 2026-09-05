using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.ThugLifeLib;
public sealed partial class ThugLifeSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("render", () => ThugLifeRenderManager.Active, ThugLifeRenderPatches.Apply, ThugLifeRenderPatches.Remove);
    }
}
