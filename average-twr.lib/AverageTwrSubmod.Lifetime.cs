using System;
using System.Linq;
namespace MeowSci.AverageTwrLib;
public sealed partial class AverageTwrSubmod
{
    public void ReleaseLiveState()
    {
        _isCollecting = false; _accumulator?.Reset();
    }
}
