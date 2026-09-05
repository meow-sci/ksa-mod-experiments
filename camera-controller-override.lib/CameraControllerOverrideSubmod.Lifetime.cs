using System;
using System.Linq;
namespace MeowSci.CameraControllerOverrideLib;
public sealed partial class CameraControllerOverrideSubmod
{
    public void ReleaseLiveState()
    {
        SequencePlayer.Stop();
    }
}
