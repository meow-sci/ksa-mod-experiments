using Brutal.Numerics;
using KSA;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// The one-click "animate thug" preset: the tuned pose that drops a pair of sunglasses onto
/// an EVA kitten's face. Values are in the anchor part's local frame, the same frame the
/// manual create form uses, so the preset and hand-tuning stay interchangeable.
/// </summary>
public static class KittenGlassesPreset
{
    /// <summary>Where the slide begins — off the face, along the anchor part's -Z.</summary>
    public static readonly float3 StartPosition = new(0.251f, 0f, -2f);

    /// <summary>Where the glasses come to rest.</summary>
    public static readonly float3 EndPosition = new(0.251f, 0f, -0.761f);

    /// <summary>Pitch/yaw/roll, degrees. Fixed — the preset does not tune rotation.</summary>
    public static readonly float3 Rotation = new(-90f, 0f, 90f);

    /// <summary>Quad width in meters.</summary>
    public const float Width = 0.975f;

    /// <summary>Quad height in meters.</summary>
    public const float Height = 0.2f;

    /// <summary>Slide duration in seconds.</summary>
    public const float SlideSeconds = 1.2f;

    /// <summary>
    /// True when <paramref name="vehicle"/> is a kitten on EVA. Seated kittens are not
    /// vehicles and so never appear in the target list.
    /// </summary>
    public static bool IsKitten(Vehicle? vehicle) => vehicle is KittenEva;
}
