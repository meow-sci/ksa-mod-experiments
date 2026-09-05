using System;
using System.Linq;
namespace MeowSci.IFeelSeenLib;
public sealed partial class IFeelSeenSubmod
{
    public void ReleaseLiveState()
    {
        _tracker.Clear();
    }
}
