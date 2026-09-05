using System;

namespace MeowSci.GraffitiLib;

/// <summary>Wall-clock spray gating. Never catches up missed ticks or continues a UI-originated drag.</summary>
internal sealed class SprayCadence
{
    private bool _stroke;
    private double _next;
    public void Reset() => _stroke = false;

    public bool Tick(double now, bool pressed, bool held, bool captured, int intervalMs)
    {
        if (captured || !held) { Reset(); return false; }
        if (pressed) { _stroke = true; _next = now; }
        if (!_stroke || now < _next) return false;
        _next = now + Math.Clamp(intervalMs, 10, 60_000) / 1000.0;
        return true;
    }
}
