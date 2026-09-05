using System;
using System.Linq;
namespace MeowSci.GarrysTorchLib;
public sealed partial class GarrysTorchSubmod
{
    public void ReleaseLiveState()
    {
        foreach (var weld in _welds.ToArray()) RemoveWeld(weld);
    }
}
