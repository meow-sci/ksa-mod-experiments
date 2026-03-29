using System.Collections.Generic;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Static cache of generated subpart thumbnails, keyed by PartTemplate.Id.
/// Populated by SubpartThumbnailGenerator.GenerateAll().
/// </summary>
public static class SubpartThumbnailCache
{
    private static readonly Dictionary<string, ThumbnailReference> _thumbnails = new();

    /// <summary>All generated thumbnails. Do not mutate.</summary>
    public static IReadOnlyDictionary<string, ThumbnailReference> All => _thumbnails;

    /// <summary>Returns the thumbnail for a subpart ID, or null if not yet generated.</summary>
    public static ThumbnailReference? Get(string subpartId)
        => _thumbnails.GetValueOrDefault(subpartId);

    /// <summary>Returns true if any thumbnails have been generated.</summary>
    public static bool HasAny => _thumbnails.Count > 0;

    internal static void Store(string id, ThumbnailReference thumbnail)
        => _thumbnails[id] = thumbnail;

    internal static void Clear()
        => _thumbnails.Clear();
}
