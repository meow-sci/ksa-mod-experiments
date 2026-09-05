using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.DontStifleMeLib;
public sealed partial class DontStifleMeSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("scale", () => EditorScaleSettings.Enabled, EditorScalePatches.Apply, EditorScalePatches.Remove);
        runtime.Patches("limits", () => EditorLimitSettings.JplSaidNoClamps, EditorValueLimitPatches.Apply, EditorValueLimitPatches.Remove);
    }
}
