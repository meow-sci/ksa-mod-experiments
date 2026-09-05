using System.Collections.Generic;
using KSA;
using MeowSci.KsaLights;
namespace MeowSci.ZippoLib;
public sealed partial class ZippoSubmod
{
    private readonly Dictionary<string, LightStateLease> _lightLeases = new();
    private void ManageLight(Part part)
    {
        string id = Key(part);
        if (!_lightLeases.ContainsKey(id)) _lightLeases.Add(id, new LightStateLease(part));
        _managedLights[id] = part;
    }
    private void ReleaseLight(string id)
    {
        _animationManager.CancelAll(id);
        if (_lightLeases.TryGetValue(id, out var lease)) { lease.Dispose(); _lightLeases.Remove(id); }
        _managedLights.Remove(id);
    }
}
