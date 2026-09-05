using System;

namespace MeowSci.ZippoLib;

/// <summary>A repeating hold followed by a transition to the next value; independent per channel.</summary>
public sealed class DiscoTiming
{
    public float Transition = 2;
    public float Hold = 1;
    public int Easing = 3;

    public void Validate()
    {
        if (!float.IsFinite(Transition) || Transition < .01f || Transition > 3600
            || !float.IsFinite(Hold) || Hold < 0 || Hold > 3600 || Easing < 0 || Easing > 3)
            throw new InvalidOperationException("Disco timing requires transition 0.01–3600 s, hold 0–3600 s, and a valid easing.");
    }

    public (long Step, float Mix) Sample(double elapsed)
    {
        double period = (double)Transition + Hold;
        elapsed = double.IsFinite(elapsed) ? Math.Max(0, elapsed) : 0;
        long step = (long)Math.Floor(elapsed / period);
        float t = (float)Math.Clamp((elapsed % period - Hold) / Transition, 0, 1);
        t = Easing switch { 1 => t * t * t, 2 => 1 - MathF.Pow(1 - t, 3), 3 => t * t * (3 - 2 * t), _ => t };
        return (step, t);
    }
}
