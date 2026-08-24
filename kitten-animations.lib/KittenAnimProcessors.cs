using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

/// <summary>
/// Typed handles on the four IAnimProcessor instances KittenRenderable installs on the kitten model.
/// They are private fields on KittenRenderable; resolving them by name is more precise than scanning
/// AnimProcessors by type, because two of them are the same CatExpressionAnim type with very
/// different roles.
/// </summary>
public sealed class KittenAnimProcessors
{
    /// <summary>Personality mood pose (Cheerful/Curious/Gruff/Woeful). Absent for Neutral kittens.</summary>
    public CatExpressionAnim? Personality { get; private init; }

    /// <summary>
    /// The reactive "scared" face. KittenRenderable.UpdateRenderData overwrites its ExpressionWeight
    /// every frame from linear + angular acceleration, so a mod cannot hold a value on it.
    /// </summary>
    public CatExpressionAnim? Reactive { get; private init; }

    public CatEyeAnim? Eye { get; private init; }

    public CatEarAnim? Ear { get; private init; }

    public static KittenAnimProcessors Read(KittenRenderable renderable)
    {
        return new KittenAnimProcessors
        {
            Personality = ReflectionHelpers.GetFieldValue(renderable, "_catPersonalityExpressionAnim") as CatExpressionAnim,
            Reactive = ReflectionHelpers.GetFieldValue(renderable, "_catExpressionAnim") as CatExpressionAnim,
            Eye = ReflectionHelpers.GetFieldValue(renderable, "_catEyeAnim") as CatEyeAnim,
            Ear = ReflectionHelpers.GetFieldValue(renderable, "_catEarAnim") as CatEarAnim,
        };
    }
}
