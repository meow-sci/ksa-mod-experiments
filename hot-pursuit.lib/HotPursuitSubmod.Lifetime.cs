using System;
using System.Linq;
namespace MeowSci.HotPursuitLib;
public sealed partial class HotPursuitSubmod
{
    public void ReleaseLiveState()
    {
        CancelAuthoringGesture(); foreach (var entry in _cameras.ToArray()) { ReleaseLease(entry); _cameras.Remove(entry); }
    }
}
