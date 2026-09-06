using System;

namespace MeowSci.ZippoLib;

/// <summary>A repeating hold followed by a transition to the next value.</summary>
public sealed class DiscoTiming
{
    public float Transition = 2f;
    public float Hold = 1f;
    public int Easing = 3;

    public DiscoTiming Clone() => new()
    {
        Transition = Transition,
        Hold = Hold,
        Easing = Easing,
    };

    public void Validate()
    {
        if (!float.IsFinite(Transition) || Transition < 0.01f || Transition > 3600f
            || !float.IsFinite(Hold) || Hold < 0f || Hold > 3600f
            || Easing < 0 || Easing > 3)
        {
            throw new InvalidOperationException(
                "Disco timing requires a 0.01-3600 second transition, a 0-3600 second hold, and a valid easing mode.");
        }
    }

    public (long Step, float Mix) Sample(double elapsed)
    {
        double period = (double)Transition + Hold;
        elapsed = double.IsFinite(elapsed) ? Math.Max(0d, elapsed) : 0d;
        long step = (long)Math.Floor(elapsed / period);
        float t = (float)Math.Clamp((elapsed % period - Hold) / Transition, 0d, 1d);
        t = Easing switch
        {
            1 => t * t * t,
            2 => 1f - MathF.Pow(1f - t, 3f),
            3 => t * t * (3f - 2f * t),
            _ => t,
        };
        return (step, t);
    }
}
