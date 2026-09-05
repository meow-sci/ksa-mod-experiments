// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// GPU load/purge operations use RuntimeModLoader.Step at the host BeforeGui boundary,
// before this frame emits any ImGui texture draw commands.

using System;
using System.Collections.Generic;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The T11.1 safety gate: proves that nothing alive still references a mod's parts before
/// <see cref="RuntimeModUnloader.Purge" /> is allowed to free them.
/// </summary>
/// <remarks>
/// <para>
/// Purging a <see cref="PartTemplate" /> that a live <see cref="Part" /> still points at leaves that
/// part holding a template that is no longer in any registry, with a disposed thumbnail image — the
/// game crashes the next time the editor or the part browser touches it. So the gate <b>fails
/// closed</b>: any exception while checking becomes a refusal, never a silent pass.
/// </para>
/// <para>Game-thread only, and only meaningful while no load job is in flight.</para>
/// </remarks>
internal static class RuntimeModUnloadGate
{
    /// <summary>
    /// Checks every condition that must hold before a record may be purged.
    /// </summary>
    /// <param name="record">The record whose parts are about to be freed.</param>
    /// <param name="loadJobInFlight">True when <c>RuntimeModLoader</c> still has a job running.</param>
    /// <returns><c>null</c> when purging is safe, otherwise a user-facing refusal reason.</returns>
    internal static string? Check(LoadedModRecord record, bool loadJobInFlight)
    {
        if (record is null)
        {
            return "there is no loaded-mod record to unload.";
        }

        try
        {
            if (loadJobInFlight)
            {
                return "a parts-now load job is still running — wait for it to finish, then unload '"
                    + record.ModId + "'.";
            }

            if (record.PartIds.Count == 0)
            {
                // Nothing part-shaped was registered, so no live object can be referencing it.
                return null;
            }

            return CheckLiveVehicles(record) ?? CheckVehicleEditor(record);
        }
        catch (Exception ex)
        {
            // Fail closed. A gate that cannot see the game state must never conclude "safe".
            return "could not verify that '" + record.ModId + "' is unused (" + ex.Message
                + ") — refusing to unload.";
        }
    }

    /// <summary>
    /// Rule 1 — no <see cref="Part" /> in any live vehicle may be an instance of one of the record's
    /// part templates.
    /// </summary>
    private static string? CheckLiveVehicles(LoadedModRecord record)
    {
        // VehicleProvider resolves Universe.CurrentSystem and returns an empty list when there is no
        // system loaded; PartHelpers walks vehicle.Parts.Parts and recurses Part.SubParts.
        // Debris counts: a fragment shed by KSA 5402's part failure is a live vehicle holding real
        // parts, so a runtime template it still references must keep the mod pinned. This gate fails
        // closed, and a list that hides debris could wrongly conclude "safe".
        foreach (Vehicle vehicle in VehicleProvider.GetAllVehicles(includeDebris: true))
        {
            foreach (Part part in PartHelpers.GetAllParts(vehicle))
            {
                PartTemplate template = part.Template;
                if (template is not null && record.PartIds.Contains(template.Id))
                {
                    return "vehicle '" + vehicle.Id + "' is flying part '" + template.Id
                        + "' from '" + record.ModId + "' — unloading it would leave that vehicle "
                        + "holding a freed part template.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Rule 2 — the vehicle editor, if open, must not contain any of the record's parts, either in
    /// the vehicle under construction (<c>EditingSpace.Parts</c>) or floating loose
    /// (<c>UnattachedPartTrees</c>).
    /// </summary>
    private static string? CheckVehicleEditor(LoadedModRecord record)
    {
        VehicleEditor? editor = Program.Editor;
        if (editor is null)
        {
            // Editor closed — Program.Editor is disposed and nulled in Program.PrepareFrame.
            return null;
        }

        VehicleEditingSpace space = editor.EditingSpace;
        if (space is not null)
        {
            // VehicleEditingSpace.AllParts is `Parts?.Parts ?? default`, so it is an empty span for
            // an empty editor rather than a null dereference.
            string? attached = FindBlockingPart(space.AllParts, record.PartIds);
            if (attached is not null)
            {
                return "the vehicle editor is open on '" + space.Id + "', which uses part '"
                    + attached + "' from '" + record.ModId
                    + "' — remove it or close the editor first.";
            }
        }

        List<PartTree> unattached = editor.UnattachedPartTrees;
        if (unattached is not null)
        {
            foreach (PartTree tree in unattached)
            {
                string? loose = FindBlockingPart(tree.Parts, record.PartIds);
                if (loose is not null)
                {
                    return "the vehicle editor has a detached part tree containing '" + loose
                        + "' from '" + record.ModId
                        + "' — delete it or close the editor first.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Recursively searches a span of parts (and their <c>SubParts</c>) for the first part whose
    /// template id is in <paramref name="partIds" />.
    /// </summary>
    /// <param name="parts">Top-level parts to search.</param>
    /// <param name="partIds">The record's part ids.</param>
    /// <returns>The blocking template id, or <c>null</c>.</returns>
    private static string? FindBlockingPart(ReadOnlySpan<Part> parts, HashSet<string> partIds)
    {
        foreach (Part part in parts)
        {
            PartTemplate template = part.Template;
            if (template is not null && partIds.Contains(template.Id))
            {
                return template.Id;
            }

            string? nested = FindBlockingPart(part.SubParts, partIds);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
