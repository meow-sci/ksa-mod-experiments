using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.FreeFallinLib;
public sealed partial class FreeFallinSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("canopy", () => CanopyMaterialController.Enabled, FreeFallinPatches.Apply, FreeFallinPatches.Remove);
    }
}
