using System;
using System.Linq;
namespace MeowSci.ItsSoShinyLib;
public sealed partial class ItsSoShinySubmod
{
    public void ReleaseLiveState()
    {
        ShinyGridManager.Clear(); _pendingDestroy.Clear(); _deferredActions.Clear(); ShinyPatchState.RenderShinyParts = false;
    }
}
