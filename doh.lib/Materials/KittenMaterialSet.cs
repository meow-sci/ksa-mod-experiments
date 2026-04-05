using Brutal.Numerics;

namespace MeowSci.DohLib.Materials;

/// <summary>
/// Holds per-kitten GPU material handles created by MaterialFactory.
/// Each handle points to a unique slot in the GpuMaterialSystem buffer
/// with a custom AlbedoColor tint.
/// </summary>
public sealed class KittenMaterialSet
{
    /// <summary>Unique identifier for this material set (e.g., "doh_0042").</summary>
    public string Id { get; }

    /// <summary>The AlbedoColor tint applied to body/head materials.</summary>
    public float4 TintColor { get; private set; }

    /// <summary>GPU material handle for the body mesh.</summary>
    public int BodyMaterialHandle { get; init; }

    /// <summary>GPU material handle for the head mesh.</summary>
    public int HeadMaterialHandle { get; init; }

    /// <summary>GPU material handle for the eye mesh (usually untinted).</summary>
    public int EyeMaterialHandle { get; init; }

    /// <summary>GPU material handle for the fur mesh.</summary>
    public int FurMaterialHandle { get; init; }

    /// <summary>Whether this material set was successfully created and all handles are valid.</summary>
    public bool IsValid => BodyMaterialHandle >= 0 && HeadMaterialHandle >= 0 && EyeMaterialHandle >= 0;

    public KittenMaterialSet(string id, float4 tintColor)
    {
        Id = id;
        TintColor = tintColor;
    }

    /// <summary>
    /// Updates the AlbedoColor tint on body and head materials.
    /// Writes directly to the GPU buffer for immediate visual update.
    /// </summary>
    public bool UpdateTint(float4 newColor)
    {
        TintColor = newColor;
        bool ok = MaterialSystemAccessor.WriteAlbedoColor(BodyMaterialHandle, newColor);
        ok &= MaterialSystemAccessor.WriteAlbedoColor(HeadMaterialHandle, newColor);
        return ok;
    }
}
