using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Z-axis rotation views for a single subpart (count varies by generation settings).
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

    /// <summary>
    /// Disposes all GPU resources, clears subpart.Thumbnail references, and empties the cache.
    /// </summary>
    internal static void DestroyAll()
    {
        if (_thumbnails.Count == 0) return;

        // Wait for all GPU work to finish before destroying Vulkan resources
        Program.GetRenderer().Device.WaitIdle();

        // Clear the subpart.Thumbnail references we set during generation
        List<PartTemplate> allParts = GetAllParts();
        foreach (var kvp in _thumbnails)
        {
            var subpart = allParts.FirstOrDefault(p => p.Id == kvp.Key);
            if (subpart != null && kvp.Value.Views.Length > 0 && subpart.Thumbnail == kvp.Value.Views[0])
                subpart.Thumbnail = null;

            foreach (var view in kvp.Value.Views)
                view?.Dispose();
        }
        _thumbnails.Clear();
    }

    private static List<PartTemplate> GetAllParts()
    {
        FieldInfo? field = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) return new List<PartTemplate>();

        object? collection = field.GetValue(null);
        if (collection == null) return new List<PartTemplate>();

        MethodInfo? getList = collection.GetType().GetMethod("GetList");
        if (getList == null) return new List<PartTemplate>();

        return (List<PartTemplate>)getList.Invoke(collection, null)!;
    }
}
