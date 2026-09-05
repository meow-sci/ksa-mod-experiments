using System;
using System.Linq;
namespace MeowSci.GraffitiLib;
public sealed partial class GraffitiSubmod
{
    public void ReleaseLiveState()
    {
        CancelAuthoringGesture(); _renderActive = false; _published = Array.Empty<DecalEntry>(); FreeGpu(); WaitIdle(); _textures.DisposeAll(); _decals.Clear();
    }
}
