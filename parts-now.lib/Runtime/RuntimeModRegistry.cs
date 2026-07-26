// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Session-scoped record of every mod parts-now loaded this run, keyed by mod id.
/// </summary>
/// <remarks>
/// Only mods in here may be reloaded or unloaded. A mod KSA loaded at boot is deliberately absent:
/// purging it would remove parts parts-now never registered and cannot account for.
/// Game-thread only; there is no locking.
/// </remarks>
public static class RuntimeModRegistry
{
    private static readonly Dictionary<string, LoadedModRecord> Records =
        new Dictionary<string, LoadedModRecord>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of mods parts-now currently has loaded.</summary>
    public static int Count => Records.Count;

    /// <summary>Every loaded mod record, in no particular order.</summary>
    /// <returns>A snapshot list, safe to iterate while loading or unloading.</returns>
    public static List<LoadedModRecord> All() => new List<LoadedModRecord>(Records.Values);

    /// <summary>True when parts-now loaded this mod id during this session.</summary>
    /// <param name="modId">The mod id to test.</param>
    public static bool IsLoadedByPartsNow(string modId) =>
        !string.IsNullOrEmpty(modId) && Records.ContainsKey(modId);

    /// <summary>Looks up the record for a mod id, or null.</summary>
    /// <param name="modId">The mod id to look up.</param>
    public static LoadedModRecord? Find(string modId) =>
        string.IsNullOrEmpty(modId) ? null : Records.GetValueOrDefault(modId);

    /// <summary>Adds or replaces the record for a completed load.</summary>
    /// <param name="record">The record to store; its <c>ModId</c> is the key.</param>
    public static void Add(LoadedModRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ModId))
        {
            throw new ArgumentException("parts-now: a LoadedModRecord must have a ModId.", nameof(record));
        }

        Records[record.ModId] = record;
    }

    /// <summary>Drops the record for a mod id after its purge completed.</summary>
    /// <param name="modId">The mod id to forget.</param>
    /// <returns>True when a record was removed.</returns>
    public static bool Remove(string modId) =>
        !string.IsNullOrEmpty(modId) && Records.Remove(modId);

    /// <summary>
    /// Finds the record that owns a part id, or null. Used by the reload/unload safety gate to name
    /// the mod a live part belongs to.
    /// </summary>
    /// <param name="partId">The part id to search for.</param>
    public static LoadedModRecord? FindOwnerOfPart(string partId)
    {
        if (string.IsNullOrEmpty(partId))
        {
            return null;
        }

        foreach (LoadedModRecord record in Records.Values)
        {
            if (record.PartIds.Contains(partId))
            {
                return record;
            }
        }

        return null;
    }
}
