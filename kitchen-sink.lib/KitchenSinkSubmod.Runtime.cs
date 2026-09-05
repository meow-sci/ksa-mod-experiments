using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.KitchenSinkLib;
public sealed partial class KitchenSinkSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("iva", () => IvaForceRender.Enabled, IvaForceRender.Patch, IvaForceRender.Unpatch);
    }
}
