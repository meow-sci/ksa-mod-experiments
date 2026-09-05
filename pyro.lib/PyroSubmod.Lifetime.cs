using System;
using System.Linq;
namespace MeowSci.PyroLib;
public sealed partial class PyroSubmod
{
    public void ReleaseLiveState()
    {
        foreach (var id in _templateOverrides.Keys.ToArray()) RestoreTemplate(id); _plumes.Clear();
    }
}
