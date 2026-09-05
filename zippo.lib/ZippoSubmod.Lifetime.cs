using System;
using System.Linq;
namespace MeowSci.ZippoLib;
public sealed partial class ZippoSubmod
{
    public void ReleaseLiveState()
    {
        foreach (var part in _discoLights.Keys.ToArray()) StopDisco(part); foreach (var id in _managedLights.Keys.ToArray()) ReleaseLight(id);
    }
}
