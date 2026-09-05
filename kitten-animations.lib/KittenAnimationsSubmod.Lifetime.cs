using System;
using System.Linq;
namespace MeowSci.KittenAnimationsLib;
public sealed partial class KittenAnimationsSubmod
{
    public void ReleaseLiveState()
    {
        Unbind(); _driver.Reset(); _liveKittenTarget = null; RestoreTuning();
    }
}
