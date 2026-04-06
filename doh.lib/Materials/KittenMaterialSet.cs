using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;

namespace MeowSci.DohLib.Materials;

/// <summary>
/// Holds per-kitten GPU material handles created by MaterialFactory.
/// Every material the kitten uses is cloned to a unique GPU slot so
/// tinting one kitten doesn't affect others.
/// </summary>
public sealed class KittenMaterialSet
{
    /// <summary>Unique identifier for this material set (e.g., "doh_0042").</summary>
    public string Id { get; }

    /// <summary>The AlbedoColor tint applied to all materials.</summary>
    public float4 TintColor { get; private set; }

    /// <summary>GPU material handle for the body mesh.</summary>
    public int BodyMaterialHandle { get; init; }

    /// <summary>GPU material handle for the head mesh.</summary>
    public int HeadMaterialHandle { get; init; }

    /// <summary>GPU material handle for the eye mesh.</summary>
    public int EyeMaterialHandle { get; init; }

    /// <summary>GPU material handle for the fur mesh.</summary>
    public int FurMaterialHandle { get; init; }

    /// <summary>
    /// Maps old shared GPU handle → new unique per-kitten handle.
    /// Used by ApplyMaterialSetToKitten to replace every entry in MaterialIndices.
    /// </summary>
    public Dictionary<int, int> HandleMap { get; set; } = new();

    /// <summary>
    /// All unique per-kitten material handles created for this kitten.
    /// Used for recoloring — WriteAlbedoColor is called on each.
    /// </summary>
    public List<int> AllMaterialHandles { get; } = new();

    public KittenMaterialSet(string id, float4 tintColor)
    {
        Id = id;
        TintColor = tintColor;
    }

    /// <summary>
    /// Updates the AlbedoColor tint on ALL unique per-kitten materials.
    /// Writes directly to the GPU buffer for immediate visual update.
    /// </summary>
    public bool UpdateTint(float4 newColor)
    {
        TintColor = newColor;
        bool ok = true;
        foreach (int handle in AllMaterialHandles)
        {
            if (handle >= 0)
                ok &= MaterialSystemAccessor.WriteAlbedoColor(handle, newColor);
        }
        return ok;
    }
}
