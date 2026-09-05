// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// GPU load/purge operations use RuntimeModLoader.Step at the host BeforeGui boundary,
// before this frame emits any ImGui texture draw commands.

using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The bodies of the numbered purge steps that <see cref="RuntimeModUnloader.Purge" /> drives.
/// </summary>
/// <remarks>
/// Each method is one step of the strict purge order and returns a short detail string for the log.
/// The order itself lives in <see cref="RuntimeModUnloader.Purge" /> and is deliberately not
/// re-implemented here. Game-thread only, after the device has been waited idle (step 1).
/// </remarks>
internal static class RuntimeModPurgeSteps
{
    /// <summary>
    /// Step 2 — <c>PartTemplate.Dispose()</c> then unregister. <c>Dispose()</c> only disposes
    /// <c>Thumbnail</c>, and <c>ThumbnailReference.Dispose()</c> calls
    /// <c>ImGuiBackend.Vulkan.RemoveTexture</c> and then destroys the image view and image — which is
    /// why step 1 waits for the device to go idle, and why this must never run twice for a record.
    /// </summary>
    /// <param name="record">The record being purged.</param>
    internal static string PurgeParts(LoadedModRecord record)
    {
        int disposed = 0;
        int unregistered = 0;

        foreach (PartTemplate template in record.NewParts)
        {
            try
            {
                // A <Thumbnail> that came straight from XML carries only a ModelTransform:
                // CreateImageView was never called, so its ImageViewEx holds a null Device and
                // Dispose() would NRE. Detach it — the template is being discarded anyway, and a
                // reload re-parses the XML.
                if (template.Thumbnail is { } thumbnail && thumbnail.ImageView.IsNull())
                {
                    template.Thumbnail = null;
                }

                template.Dispose();
                disposed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("parts-now: could not dispose part '" + template.Id + "' — " + ex.Message);
            }

            try
            {
                if (GameRegistry.Unregister(GameRegistry.AllParts, template))
                {
                    unregistered++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("parts-now: could not unregister part '" + template.Id + "' — " + ex.Message);
            }
        }

        return disposed + " disposed, " + unregistered + " unregistered";
    }

    /// <summary>Step 3 — drop the game-data references this load registered.</summary>
    /// <param name="record">The record being purged.</param>
    internal static string PurgeGameData(LoadedModRecord record)
    {
        int removed = 0;

        foreach (PartGameDataReference gameData in record.NewGameData)
        {
            if (GameRegistry.Unregister(GameRegistry.AllPartGameData, gameData))
            {
                removed++;
            }
        }

        return removed + " of " + record.NewGameData.Count + " removed";
    }

    /// <summary>
    /// Step 4 — prune the static model caches. KSA never removes from these: <c>PartModel.Get</c>
    /// resolves by scanning <c>Instances</c> for a matching <c>Template.Id</c>, so a stale entry
    /// would hand a freed template back to the next part that asks for the same id.
    /// </summary>
    /// <param name="record">The record being purged.</param>
    internal static string PurgeModelInstances(LoadedModRecord record)
    {
        HashSet<object> templates = record.ModelTemplates;
        if (templates.Count == 0)
        {
            return "no model templates to purge";
        }

        // Matched by reference, never by Template.Id: TemplateDataBase.Id is optional and not
        // required to be unique, so an id match would miss every id-less template (leaving a stale
        // PartModel that PartModel.Get would hand to the reloaded part, still pointing at the purged
        // mesh's old shared-buffer offsets) and would evict another mod's instances on a collision.
        int removed = 0;

        removed += PartModel.Instances.RemoveAll(m => templates.Contains(m.Template));
        removed += PartModel.InstancesRayTrace.RemoveAll(m => templates.Contains(m.Template));

        removed += PartModelGlass.Instances.RemoveAll(m => templates.Contains(m.Template));
        removed += PartModelGlass.InstancesRayTrace.RemoveAll(m => templates.Contains(m.Template));

        // PartModelDynamic has no InstancesRayTrace list — dynamic models are never ray traced.
        removed += PartModelDynamic.Instances.RemoveAll(m => templates.Contains(m.Template));

        // Both module families keep their own static ray-tracer registry.
        removed += PartModelModule.Template.RayTracers.RemoveAll(templates.Contains);
        removed += PartModelGlassModule.Template.RayTracers.RemoveAll(templates.Contains);

        return removed + " instance/ray-tracer entries for " + templates.Count + " model template(s)";
    }

    /// <summary>
    /// Step 5 — free the bindless slot and the GPU texture, then unregister.
    /// </summary>
    /// <param name="textures">The record's <c>TextureReference</c>s, split out of <c>NewFiles</c>.</param>
    /// <param name="renderer">The renderer captured by step 1, or null if step 1 failed.</param>
    /// <remarks>
    /// <c>TextureReference.Dispose(Device)</c> calls
    /// <c>BindlessTextures.FreeTexture(BindlessHandle)</c> and then <c>Texture.Dispose()</c> and
    /// <c>TextureAsset.Dispose()</c> with no null checks, so it NREs on a texture that never got
    /// bound. Handle <c>0</c> is the bindless library's shared <i>empty</i> texture and must never be
    /// freed. Hence the double guard below. Note the type does not implement <c>IDisposable</c>, and
    /// the <c>Device</c> argument its signature demands is ignored by the game.
    /// </remarks>
    internal static string PurgeTextures(List<TextureReference> textures, Core.Renderer? renderer)
    {
        int disposed = 0;
        int unregistered = 0;
        int skipped = 0;

        foreach (TextureReference texture in textures)
        {
            bool bound = texture.BindlessHandle > 0
                && texture.Texture is not null
                && texture.TextureAsset is not null;

            if (bound && renderer is not null)
            {
                try
                {
                    texture.Dispose(renderer.Device);
                    disposed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("parts-now: could not dispose texture '" + texture.Id + "' — " + ex.Message);
                }
            }
            else
            {
                skipped++;
            }

            try
            {
                if (GameRegistry.Unregister(GameRegistry.AllFiles, texture))
                {
                    unregistered++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("parts-now: could not unregister texture '" + texture.Id + "' — " + ex.Message);
            }
        }

        return disposed + " disposed, " + skipped + " never bound, " + unregistered + " unregistered";
    }

    /// <summary>
    /// Step 6 — record the leak, then dispose and unregister.
    /// </summary>
    /// <param name="record">The record being purged.</param>
    /// <param name="recordLeak">
    /// True for a real unload, where the bytes really are orphaned. False for a rollback, which
    /// rewinds the allocation cursors instead — counting those bytes as leaked as well would
    /// double-charge them against the headroom.
    /// </param>
    /// <remarks>
    /// A mesh's slice of the shared interleaved vertex/index buffer is <b>never reclaimed</b>: the
    /// allocator is a monotonic bump pointer with no free list, so those bytes are gone until the
    /// game restarts. The measurement has to happen <i>before</i> <c>Dispose()</c>, while
    /// <c>DeviceMeshesInterleaved</c> still holds the sizes.
    /// </remarks>
    internal static string PurgeMeshes(LoadedModRecord record, bool recordLeak)
    {
        ulong leakedVertexBytes = 0uL;
        ulong leakedIndexBytes = 0uL;
        int unregistered = 0;

        foreach (MeshReference mesh in record.NewMeshes)
        {
            MeshBudget.MeasureMesh(mesh, out ulong vertexBytes, out ulong indexBytes);
            if (recordLeak)
            {
                MeshBudget.RecordLeak(vertexBytes, indexBytes);
            }

            leakedVertexBytes += vertexBytes;
            leakedIndexBytes += indexBytes;

            try
            {
                mesh.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("parts-now: could not dispose mesh '" + mesh.Id + "' — " + ex.Message);
            }

            try
            {
                if (GameRegistry.Unregister(GameRegistry.AllMeshes, mesh))
                {
                    unregistered++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("parts-now: could not unregister mesh '" + mesh.Id + "' — " + ex.Message);
            }
        }

        string fate = recordLeak
            ? " KiB idx into the shared buffer (not reclaimable)"
            : " KiB idx reclaimed by rewinding the allocation cursors";
        return unregistered + " unregistered; " + (recordLeak ? "leaked " : "released ")
            + (leakedVertexBytes / 1024uL) + " KiB vtx / " + (leakedIndexBytes / 1024uL) + fate;
    }

    /// <summary>Step 7 — unregister the non-texture file references (mesh atlases, mesh files).</summary>
    /// <param name="files">The record's non-texture <c>FileReference</c>s.</param>
    internal static string PurgeFiles(List<FileReference> files)
    {
        int removed = 0;

        foreach (FileReference file in files)
        {
            if (GameRegistry.Unregister(GameRegistry.AllFiles, file))
            {
                removed++;
            }
        }

        return removed + " of " + files.Count + " removed";
    }

    /// <summary>Step 8 — unregister the PBR materials.</summary>
    /// <param name="record">The record being purged.</param>
    internal static string PurgeMaterials(LoadedModRecord record)
    {
        int removed = 0;

        foreach (PbrMaterialReference material in record.NewMaterials)
        {
            if (GameRegistry.Unregister(GameRegistry.AllMaterials, material))
            {
                removed++;
            }
        }

        return removed + " of " + record.NewMaterials.Count + " removed";
    }

    /// <summary>
    /// Step 9 — drop this load's entries from <c>ModLibrary.Loaders</c> / <c>Binders</c>. KSA never
    /// clears either list, so leaving them behind would make a later full re-run re-load freed objects.
    /// </summary>
    /// <param name="record">The record being purged.</param>
    internal static string PurgeLoadersAndBinders(LoadedModRecord record)
    {
        int loaders = ModLibrary.Loaders.RemoveAll(record.NewLoaders.Contains);
        int binders = ModLibrary.Binders.RemoveAll(record.NewBinders.Contains);
        return loaders + " loader(s), " + binders + " binder(s) removed";
    }
}
