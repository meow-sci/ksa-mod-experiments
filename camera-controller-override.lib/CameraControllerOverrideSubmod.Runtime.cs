using System.Linq;
using MeowSci.KsaAbstractions;
namespace MeowSci.CameraControllerOverrideLib;
public sealed partial class CameraControllerOverrideSubmod
{
    public void ConfigureRuntime(FeatureRuntime runtime)
    {
        runtime.Patches("camera", () => SequencePlayer.State == Animation.PlaybackState.Playing, h => { CameraControllerOverridePatches.SequencePlayer = SequencePlayer; CameraControllerOverridePatches.Apply(h); }, CameraControllerOverridePatches.Remove);
    }
}
