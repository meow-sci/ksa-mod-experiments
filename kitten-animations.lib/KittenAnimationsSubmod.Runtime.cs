using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.KittenAnimationsLib;
public sealed partial class KittenAnimationsSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("pose", () => _driver.HasOverrides, h => { KittenAnimationPatches.Driver = _driver; KittenAnimationPatches.Apply(h); }, h => { _driver.RestoreDisabledProcessors(); KittenAnimationPatches.Remove(h); });
    }
}
