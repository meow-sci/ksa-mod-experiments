using System;
using System.Linq;
namespace MeowSci.KiwisMarblesLib;
public sealed partial class KiwisMarblesSubmod
{
    public void ReleaseLiveState()
    {
        foreach (var weld in _welds.ToArray()) RemoveWeld(weld);
    }
}
