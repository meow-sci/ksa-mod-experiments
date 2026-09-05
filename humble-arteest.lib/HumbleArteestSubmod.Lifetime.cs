using System;
using System.Linq;
namespace MeowSci.HumbleArteestLib;
public sealed partial class HumbleArteestSubmod
{
    public void ReleaseLiveState()
    {
        CancelAuthoringGesture(); VehiclePaint.Cleanup(); EngineEmissive.Cleanup(); if (KittenColor.IsInitialized) KittenColor.ResetAll();
    }
}
