using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.GlassLib;
public sealed partial class GlassSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("camera", () => FovController.IsOverrideActive, GlassPatches.Apply, GlassPatches.Remove);
    }
}
