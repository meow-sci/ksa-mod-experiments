using System;
using System.Linq;
namespace MeowSci.ThugLifeLib;
public sealed partial class ThugLifeSubmod
{
    public void ReleaseLiveState()
    {
        if (_manager != null) foreach (var entry in _manager.Entries.ToArray()) _manager.Remove(entry);
    }
}
