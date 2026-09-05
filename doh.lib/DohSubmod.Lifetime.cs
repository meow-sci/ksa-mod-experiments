using System;
using System.Linq;
namespace MeowSci.DohLib;
public sealed partial class DohSubmod
{
    public void ReleaseLiveState()
    {
        _spawner?.DespawnAll(); if (_registry?.Count > 0) throw new InvalidOperationException("Some kittens could not be despawned; their material ownership is retained."); _materialFactory?.Cleanup();
    }
}
