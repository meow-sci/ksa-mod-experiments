using System;
using System.Linq;
namespace MeowSci.GlassLib;
public sealed partial class GlassSubmod
{
    public void ReleaseLiveState()
    {
        FovController.DisableOverride();
    }
}
