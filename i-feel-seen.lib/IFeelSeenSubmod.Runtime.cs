using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.IFeelSeenLib;
public sealed partial class IFeelSeenSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("visibility", () => Tracker.Tracked.Any(v => v.SeeMe), h => IFeelSeenPatches.Apply(h, Tracker), IFeelSeenPatches.Remove);
    }
}
