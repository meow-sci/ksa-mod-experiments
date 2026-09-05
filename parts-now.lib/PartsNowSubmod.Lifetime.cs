using System;
using System.Linq;
namespace MeowSci.PartsNowLib;
public sealed partial class PartsNowSubmod
{
    public void ReleaseLiveState()
    {
        _releaseRequested = true;
    }
}
