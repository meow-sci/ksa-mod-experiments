// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Registry bookkeeping for <see cref="RuntimeModLoader" />: taking the "before" marks, reading back
/// the deltas at the two moments they actually become available, and proving the loader step really
/// loaded what it was asked to.
/// </summary>
public static partial class RuntimeModLoader
{
    /// <summary>
    /// Snapshots the size of every registry this job can grow, plus the shared mesh buffer cursors.
    /// MUST be called immediately before <c>AssetBundle.OnDataLoad</c> runs for the first bundle.
    /// </summary>
    /// <remarks>
    /// ALL marks are taken here, even the ones whose deltas are not readable until after the loader
    /// step — see <see cref="CaptureRegistrationDeltas" /> for why the two capture points differ.
    /// </remarks>
    private static void TakeMarks(LoadJob job)
    {
        job.PartsMark = GameRegistry.AllParts.GetList().Count;
        job.GameDataMark = GameRegistry.AllPartGameData.GetList().Count;
        job.MaterialsMark = GameRegistry.AllMaterials.GetList().Count;
        job.LoaderMark = ModLibrary.Loaders.Count;
        job.FilesMark = GameRegistry.AllFiles.GetList().Count;
        job.MeshesMark = GameRegistry.AllMeshes.GetList().Count;
        job.BinderMark = ModLibrary.Binders.Count;

        job.Record.CursorsBefore = MeshBudget.SnapshotCursors();
    }

    /// <summary>
    /// Reads back everything <c>AssetBundle.OnDataLoad</c> registered: parts, part game data,
    /// materials and loaders. Call immediately after the <c>RegisterBundles</c> state, including when
    /// it threw part-way through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This split is the single subtlest thing in the loader, and getting it wrong makes unload
    /// and rollback silently incomplete.</b> It is tempting to read every delta once, right after
    /// <c>OnDataLoad</c>. Three of the seven registries are still empty at that point:
    /// </para>
    /// <para>
    /// Gained during <c>RegisterBundles</c> — <c>AllParts</c> (<c>PartTemplate.OnDataLoad</c> ends in
    /// <c>ModLibrary.Register(this)</c>), <c>AllPartGameDataReferences</c>
    /// (<c>PartGameDataReference.OnDataLoad</c>), <c>AllMaterials</c>
    /// (<c>PbrMaterialReference.OnDataLoad</c>) and <c>ModLibrary.Loaders</c>
    /// (<c>FileReference.OnDataLoad</c> calls <c>ModLibrary.RegisterLoader</c> — but only while
    /// <c>!mod.Preload</c>, which is why <c>CreateMod</c> forces that false).
    /// </para>
    /// <para>
    /// Gained during <c>RunLoaders</c> — <c>AllFiles</c> (<c>FileReference.Load()</c> starts with
    /// <c>ModLibrary.Register(this)</c>), <c>AllMeshes</c> (<c>MeshAtlasFileReference.DoLoad</c>
    /// registers one <c>MeshReference</c> per GLB node; <c>MeshFileReference.DoLoad</c> registers one)
    /// and <c>ModLibrary.Binders</c> (<c>MeshReference.Load</c> and <c>TextureReference.DoLoad</c>
    /// both call <c>ModLibrary.RegisterBinder</c>).
    /// </para>
    /// <para>
    /// So the deltas are read at two different moments, from marks all taken up front. Counts are
    /// re-read here rather than assumed: nothing else appends to these lists at runtime, but a purge
    /// racing a load would shrink them and <c>GetRange</c> would throw.
    /// </para>
    /// </remarks>
    private static void CaptureRegistrationDeltas(LoadJob job)
    {
        LoadedModRecord record = job.Record;

        record.NewParts.AddRange(Delta(GameRegistry.AllParts.GetList(), job.PartsMark));
        record.NewGameData.AddRange(Delta(GameRegistry.AllPartGameData.GetList(), job.GameDataMark));
        record.NewMaterials.AddRange(Delta(GameRegistry.AllMaterials.GetList(), job.MaterialsMark));
        record.NewLoaders.AddRange(Delta(ModLibrary.Loaders, job.LoaderMark));
    }

    /// <summary>
    /// Reads back everything the loader worker registered: file references, meshes and binders. Call
    /// immediately after the loader task completes, whether it succeeded or not.
    /// </summary>
    private static void CaptureLoadDeltas(LoadJob job)
    {
        LoadedModRecord record = job.Record;

        record.NewFiles.AddRange(Delta(GameRegistry.AllFiles.GetList(), job.FilesMark));
        record.NewMeshes.AddRange(Delta(GameRegistry.AllMeshes.GetList(), job.MeshesMark));
        record.NewBinders.AddRange(Delta(ModLibrary.Binders, job.BinderMark));
    }

    /// <summary>
    /// The tail of a list from <paramref name="mark" /> onwards. Clamped rather than trusted, so a
    /// registry that somehow shrank produces an empty delta instead of an exception during a load.
    /// </summary>
    private static List<T> Delta<T>(List<T> list, int mark)
    {
        int count = list.Count;
        int start = Math.Clamp(mark, 0, count);
        return list.GetRange(start, count - start);
    }

    /// <summary>
    /// Fills in the record's part id set, its model-template id set and one initial
    /// <see cref="PartLoadStatus.Ok" /> result per new part. Call once the parts delta is known.
    /// </summary>
    /// <remarks>
    /// The model-template ids are what a purge uses to prune <c>PartModel.Instances</c>,
    /// <c>PartModelGlass.Instances</c>, <c>PartModelDynamic.Instances</c> and
    /// <c>PartModelModule.Template.RayTracers</c> — static lists KSA appends to and never prunes.
    /// </remarks>
    private static void CollectPartMetadata(LoadJob job)
    {
        LoadedModRecord record = job.Record;

        foreach (PartTemplate part in record.NewParts)
        {
            if (string.IsNullOrEmpty(part.Id))
            {
                continue;
            }

            record.PartIds.Add(part.Id);
            record.SetResult(part.Id, PartLoadStatus.Ok);

            foreach (ModuleBase.TemplateDataBase component in part.Components)
            {
                // Collect the template OBJECTS, not their ids: TemplateDataBase.Id is an optional
                // XML attribute, so an id-less template would otherwise never be purged and an
                // id collision would evict another mod's model instances.
                if (component is not (PartModelModule.Template
                    or PartModelGlassModule.Template
                    or PartModelDynamicModule.Template))
                {
                    continue;
                }

                record.ModelTemplates.Add(component);

                if (!string.IsNullOrEmpty(component.Id))
                {
                    record.ModelTemplateIds.Add(component.Id);
                }
            }
        }
    }

    /// <summary>
    /// True when the record holds at least one thing that reached a KSA registry, i.e. when a failure
    /// actually has something to undo. A load that dies in validation or while writing its folder has
    /// nothing to roll back, and must not pay for the purge's <c>Device.WaitIdle()</c>.
    /// </summary>
    private static bool HasRegisteredAnything(LoadedModRecord record) =>
        record.NewParts.Count > 0
        || record.NewGameData.Count > 0
        || record.NewMaterials.Count > 0
        || record.NewLoaders.Count > 0
        || record.NewFiles.Count > 0
        || record.NewMeshes.Count > 0
        || record.NewBinders.Count > 0;

    /// <summary>
    /// Proves the loader step actually read every file it was given.
    /// </summary>
    /// <returns>A description of everything that silently failed, or null when all is well.</returns>
    /// <remarks>
    /// <c>FileReference.Load()</c> catches and logs its own exceptions instead of throwing, so a
    /// missing GLB or KTX2 produces a perfectly quiet partial load whose parts render as nothing.
    /// Every check below is a post-condition of a successful <c>DoLoad()</c>:
    /// <c>_isReference</c> stays true when the id collided with an already-registered file,
    /// a mesh atlas ends with a non-empty <c>Meshes</c> list, a mesh file ends with a non-null
    /// <c>Mesh</c>, a texture ends by calling <c>ModLibrary.RegisterBinder(this)</c>, and
    /// <c>MeshReference.Load</c> ends by clearing its own <c>_isReference</c>.
    /// </remarks>
    private static string? VerifyLoadersProduced(LoadJob job)
    {
        HashSet<IBinder> newBinders = new HashSet<IBinder>(
            job.Record.NewBinders, ReferenceEqualityComparer.Instance);
        HashSet<FileReference> newFiles = new HashSet<FileReference>(
            job.Record.NewFiles, ReferenceEqualityComparer.Instance);
        List<string> failures = new List<string>();

        foreach (ILoader loader in job.PendingLoaders)
        {
            if (loader is not FileReference file)
            {
                continue;
            }

            string id = string.IsNullOrEmpty(file.Id) ? file.LocalPath : file.Id;

            if (file.IsReference())
            {
                // A FileReference whose id collided is demoted by FileReference.Load() and never
                // read. That is only a failure when the winner belongs to somebody else: naming the
                // same texture from two material channels is an authoring pattern KSA itself uses
                // (Content/Core/CharacterAssets.xml points at EmptyAoRoughMetallic.png seven times),
                // and there the first entry of this same job already loaded the file.
                if (ResolvesToOwnFile(file, newFiles))
                {
                    // Not fatal — the file WAS read, by the first declaration. But this second
                    // object is not the one that got read: KSA has no way to repoint a material
                    // channel at the winner, so this one keeps BindlessHandle 0 (the bindless
                    // library's empty texture) and its material renders white. Record it so the
                    // parts that use it are marked degraded rather than silently wrong.
                    job.UnreadDuplicateFiles.Add(file);
                    LogLine("'" + id + "' is declared more than once in this mod. Only the first "
                        + "declaration is read; the others fall back to the empty texture. Declare "
                        + "the file once and share it through a single <PbrMaterial Id>.");
                    continue;
                }

                failures.Add("'" + id + "' was demoted to a reference — something with the same id was "
                    + "already registered, so '" + file.LocalPath + "' was never read.");
                continue;
            }

            switch (file)
            {
                case MeshAtlasFileReference atlas when atlas.Meshes.Count == 0:
                    failures.Add("mesh atlas '" + id + "' produced no meshes from '" + file.LocalPath + "'.");
                    break;
                case MeshFileReference meshFile when meshFile.Mesh is null:
                    failures.Add("mesh file '" + id + "' produced no mesh from '" + file.LocalPath + "'.");
                    break;
                case TextureReference texture when !newBinders.Contains(texture):
                    failures.Add("texture '" + id + "' never registered for GPU upload, so '"
                        + file.LocalPath + "' was not decoded.");
                    break;
            }
        }

        foreach (MeshReference mesh in job.Record.NewMeshes)
        {
            if (mesh.IsReference())
            {
                failures.Add("mesh '" + mesh.Id + "' was registered but its geometry never loaded.");
            }
        }

        if (failures.Count == 0)
        {
            return null;
        }

        return failures.Count + " asset(s) did not load: " + string.Join(" ", failures)
            + " FileReference.Load() logs its own exceptions rather than throwing, so the underlying "
            + "error is in the game log.";
    }

    /// <summary>
    /// True when a demoted <see cref="FileReference" />'s id resolves to a file THIS load registered,
    /// i.e. the duplicate is an intra-bundle share rather than a collision with something that was
    /// already in the game.
    /// </summary>
    private static bool ResolvesToOwnFile(FileReference file, HashSet<FileReference> newFiles)
    {
        if (string.IsNullOrEmpty(file.Id))
        {
            return false;
        }

        try
        {
            FileReference? winner = GameRegistry.AllFiles.Find(KeyHash.Make(file.Id.AsSpan()));
            return winner is not null && newFiles.Contains(winner);
        }
        catch (Exception ex)
        {
            Console.WriteLine("parts-now: could not resolve '" + file.Id + "' while verifying the "
                + "loader step — " + ex.Message);
            return false;
        }
    }
}
