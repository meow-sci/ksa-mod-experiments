using System.Collections.Generic;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// 24 Z-axis rotation views (every 15 degrees) for a single subpart.
/// </summary>
public sealed class SubpartThumbnailEntry
{
    public ThumbnailReference[] Views { get; }

    public SubpartThumbnailEntry(ThumbnailReference[] views)
    {
        Views = views;
    }
}

/// <summary>
/// Static cache of generated subpart thumbnail pairs, keyed by PartTemplate.Id.
/// Populated by SubpartThumbnailGenerator.GenerateAll().
/// </summary>
public static class SubpartThumbnailCache
{
    private static readonly Dictionary<string, SubpartThumbnailEntry> _thumbnails = new();

    /// <summary>All generated thumbnail entries. Do not mutate.</summary>
    public static IReadOnlyDictionary<string, SubpartThumbnailEntry> All => _thumbnails;

    /// <summary>Returns the thumbnail entry for a subpart ID, or null if not yet generated.</summary>
    public static SubpartThumbnailEntry? Get(string subpartId)
        => _thumbnails.GetValueOrDefault(subpartId);

    /// <summary>Returns true if any thumbnails have been generated.</summary>
    public static bool HasAny => _thumbnails.Count > 0;

    internal static void Store(string id, SubpartThumbnailEntry entry)
        => _thumbnails[id] = entry;

    internal static void Clear()
        => _thumbnails.Clear();
}
