using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

/// <summary>Provides access to the KittenEva vehicle and its CharacterAvatar via reflection.</summary>
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

    /// <summary>Returns the CharacterAvatar for the controlled KittenEva via private field reflection, or null.</summary>
    public static CharacterAvatar? GetKittenAvatar()
    {
        var kitten = GetKitten();
        if (kitten == null) return null;

        var renderable = ReflectionHelpers.GetFieldValue(kitten, "_renderable");
        return ReflectionHelpers.GetFieldValue(renderable, "_characterAvatar") as CharacterAvatar;
    }
}
