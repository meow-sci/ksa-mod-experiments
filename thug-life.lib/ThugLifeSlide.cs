using Brutal.Numerics;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// A one-shot position animation for a <see cref="ThugLifeEntry"/> — the "drop onto the
/// face" slide. Advanced once per frame on the game thread from
/// <see cref="ThugLifeRenderManager.Update"/>; the entry keeps its final position when the
/// slide completes and the slide is then discarded.
/// </summary>
public sealed class ThugLifeSlide
{
    private readonly float3 _from;
    private readonly float3 _to;
    private readonly float _duration;
    private float _elapsed;

    /// <param name="duration">Seconds. Zero or less snaps straight to <paramref name="to"/>.</param>
    public ThugLifeSlide(float3 from, float3 to, float duration)
    {
        _from = from;
        _to = to;
        _duration = duration;
    }

    public bool IsDone => _elapsed >= _duration;

    /// <summary>Advances by <paramref name="dt"/> seconds and returns the position for this frame.</summary>
    public float3 Advance(double dt)
    {
        _elapsed += (float)dt;
        if (_duration <= 0f || _elapsed >= _duration)
        {
            _elapsed = _duration;
            return _to;
        }

        float t = _elapsed / _duration;
        float eased = 1f - (1f - t) * (1f - t); // ease-out, matching the gatOS `thug` default
        return _from + (_to - _from) * eased;
    }
}
