using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

/// <summary>Provides access to the controlled KittenEva, its renderable and its CharacterAvatar.</summary>
public static class KittenAvatarAccessor
{
    /// <summary>Returns the controlled vehicle as KittenEva, or null if not a KittenEva.</summary>
    public static KittenEva? GetKitten()
    {
        var vehicle = VehicleProvider.GetControlledVehicle();
        if (vehicle is KittenEva kitten)
            return kitten;
        return null;
    }

    /// <summary>Returns the controlled kitten's KittenRenderable, or null. Typed — no reflection needed.</summary>
    public static KittenRenderable? GetKittenRenderable()
    {
        return GetKitten()?.Renderable;
    }

    /// <summary>Returns the CharacterAvatar for the controlled KittenEva via private field reflection, or null.</summary>
    public static CharacterAvatar? GetKittenAvatar()
    {
        return GetAvatar(GetKittenRenderable());
    }

    /// <summary>Returns the CharacterAvatar owned by a KittenRenderable via private field reflection, or null.</summary>
    public static CharacterAvatar? GetAvatar(KittenRenderable? renderable)
    {
        if (renderable == null) return null;

        return ReflectionHelpers.GetFieldValue(renderable, "_characterAvatar") as CharacterAvatar;
    }
}
