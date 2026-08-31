using System;
using System.Collections.Generic;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.RockyMcRockFaceLib;

/// <summary>A celestial body that declares planetary rings in its template.</summary>
public sealed class RingedBody
{
    public RingedBody(Celestial celestial, PlanetaryRingsReference rings)
    {
        Celestial = celestial;
        Rings = rings;
    }

    public Celestial Celestial { get; }
    public PlanetaryRingsReference Rings { get; }
    public string Id => Celestial.Id;
    public int LodCount => Math.Min(Rings.RingObjects.NumLods, RingSelection.MaxLods);
}

/// <summary>
/// Applies ring overrides by mutating the public XML-backed PlanetaryRingsReference
/// tree that PlanetaryRingsRenderData reads at construction, then forcing the game's
/// own renderer rebuild path (the same one its graphics settings use) so the per-planet
/// ring render data is rebuilt from the mutated references. Original values are
/// snapshotted per rings-reference so everything can be restored.
/// </summary>
public sealed class RingSwapController : IDisposable
{
    private sealed class Snapshot
    {
        public MeshReference?[] LodMeshes = Array.Empty<MeshReference?>();
        public TextureReference? Diffuse;
        public TexturePowerReference? Normal;
        public TextureReference? Pbr;
        public TextureReference? BandTexture;
        public DistanceReference Size = null!;
        public DoubleReference Density = null!;
        public DistanceReference RenderDistance = null!;
        public DistanceReference Thickness = null!;
    }

    private readonly Dictionary<PlanetaryRingsReference, Snapshot> _snapshots = new();
    private bool _anyApplied;

    public RingAssetCatalog Catalog { get; } = new();
    public RingMeshFactory MeshFactory { get; } = new();
    public List<RingedBody> Bodies { get; } = new();

    /// <summary>Rescans the current system for celestials with ring definitions.</summary>
    public void RefreshBodies()
    {
        Bodies.Clear();
        var system = Universe.CurrentSystem;
        if (system == null) return;

        foreach (var celestial in system.All.OfType<Celestial>())
        {
            var rings = celestial.BodyTemplate?.RingsReference;
            if (rings?.RingObjects == null || rings.RingObjects.NumLods == 0) continue;
            Bodies.Add(new RingedBody(celestial, rings));
            if (!_snapshots.ContainsKey(rings))
                _snapshots[rings] = TakeSnapshot(rings);
        }
    }

    /// <summary>
    /// Resolves and applies the selection onto the body's ring references. Does not
    /// rebuild the renderer — call <see cref="RebuildRenderer"/> afterwards.
    /// Returns false (with nothing mutated) if any referenced asset cannot be resolved.
    /// </summary>
    public bool Apply(RingedBody body, RingSelection selection, out string message)
    {
        var rings = body.Rings;
        if (!_snapshots.TryGetValue(rings, out var defaults))
        {
            message = "no default snapshot for this body (rescan first)";
            return false;
        }

        var objects = rings.RingObjects;
        int lodCount = body.LodCount;

        // Resolve everything up-front so a failed lookup leaves the game untouched.
        var meshes = new MeshReference?[lodCount];
        for (int i = 0; i < lodCount; i++)
        {
            string id = selection.LodMeshIds[i];
            if (id.Length == 0)
            {
                meshes[i] = defaults.LodMeshes[i];
                continue;
            }
            if (!Catalog.TryGetMesh(id, out var source))
            {
                message = $"unknown mesh '{id}'";
                return false;
            }
            meshes[i] = MeshFactory.GetRingUsable(source, out var error);
            if (meshes[i] == null)
            {
                message = error ?? $"mesh '{id}' is not usable";
                return false;
            }
        }

        if (!ResolveTexture(selection.DiffuseId, defaults.Diffuse, out var diffuse, out message)) return false;
        if (!ResolveTexture(selection.PbrId, defaults.Pbr, out var pbr, out message)) return false;
        if (!ResolveTexture(selection.BandTextureId, defaults.BandTexture, out var band, out message)) return false;

        TexturePowerReference? normal = defaults.Normal;
        if (selection.NormalId.Length > 0 && !Catalog.TryGetNormalTexture(selection.NormalId, out normal!))
        {
            message = $"unknown normal map '{selection.NormalId}'";
            return false;
        }

        for (int i = 0; i < lodCount; i++)
        {
            var fileReference = objects.Lods[i].MeshFileReference?.Get();
            if (fileReference != null) fileReference.Mesh = meshes[i];
        }
        objects.MaterialReference.DiffuseReference = diffuse;
        objects.MaterialReference.NormalReference = normal;
        objects.MaterialReference.PBRMap = pbr;
        if (band != null) rings.Texture = band;

        if (selection.OverrideFieldSettings)
        {
            objects.Size = new DistanceReference(selection.SizeM);
            objects.Density = DoubleReference.FromValue(selection.DensityPerKm3);
            objects.RenderDistance = new DistanceReference(selection.RenderDistanceKm, DistanceUnit.Kilometers);
            objects.Thickness = new DistanceReference(selection.ThicknessKm, DistanceUnit.Kilometers);
        }
        else
        {
            objects.Size = defaults.Size;
            objects.Density = defaults.Density;
            objects.RenderDistance = defaults.RenderDistance;
            objects.Thickness = defaults.Thickness;
        }

        _anyApplied = true;
        message = $"applied ring overrides to {body.Id}";
        return true;
    }

    /// <summary>Puts the game's original meshes, textures and field settings back.</summary>
    public void Restore(RingedBody body)
    {
        if (!_snapshots.TryGetValue(body.Rings, out var defaults)) return;
        var objects = body.Rings.RingObjects;
        int lodCount = Math.Min(body.LodCount, defaults.LodMeshes.Length);
        for (int i = 0; i < lodCount; i++)
        {
            var fileReference = objects.Lods[i].MeshFileReference?.Get();
            if (fileReference != null) fileReference.Mesh = defaults.LodMeshes[i];
        }
        objects.MaterialReference.DiffuseReference = defaults.Diffuse;
        objects.MaterialReference.NormalReference = defaults.Normal;
        objects.MaterialReference.PBRMap = defaults.Pbr;
        if (defaults.BandTexture != null) body.Rings.Texture = defaults.BandTexture;
        objects.Size = defaults.Size;
        objects.Density = defaults.Density;
        objects.RenderDistance = defaults.RenderDistance;
        objects.Thickness = defaults.Thickness;
    }

    /// <summary>
    /// Forces the game's full renderer rebuild (Program.RebuildRenderer) so the ring
    /// render data — meshes, index counts, descriptor sets, instance buffers — is
    /// reconstructed from the current reference state. Same path the game's own
    /// graphics settings use; it waits for the device, so it is safe but causes a hitch.
    /// </summary>
    public bool RebuildRenderer(out string message)
    {
        try
        {
            var program = Program.Instance;
            if (program == null)
            {
                message = "game renderer not ready";
                return false;
            }
            program.RebuildRenderer();
            message = "renderer rebuilt";
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: renderer rebuild failed: {ex}");
            message = $"renderer rebuild failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>True once the game has constructed its planetary rings renderer.</summary>
    public bool IsRingsRendererCreated()
    {
        var transparencies = ReflectionHelpers.GetFieldValue(Program.Instance, "_planetTransparenciesRenderer");
        return ReflectionHelpers.GetFieldValue(transparencies, "_ringsRenderer") != null;
    }

    public void Dispose()
    {
        try
        {
            if (_anyApplied)
            {
                foreach (var body in Bodies) Restore(body);
                if (IsRingsRendererCreated()) RebuildRenderer(out _);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: restore on dispose failed: {ex.Message}");
        }
        // Only after the rebuild above: nothing references the converted meshes any more.
        MeshFactory.Dispose();
    }

    private bool ResolveTexture(string id, TextureReference? fallback, out TextureReference? result, out string message)
    {
        message = "";
        if (id.Length == 0)
        {
            result = fallback;
            return true;
        }
        if (Catalog.TryGetTexture(id, out var texture))
        {
            result = texture;
            return true;
        }
        result = null;
        message = $"unknown texture '{id}'";
        return false;
    }

    private static Snapshot TakeSnapshot(PlanetaryRingsReference rings)
    {
        var objects = rings.RingObjects;
        var lodMeshes = new MeshReference?[objects.NumLods];
        for (int i = 0; i < objects.NumLods; i++)
            lodMeshes[i] = objects.Lods[i].MeshFileReference?.Get().Mesh;

        return new Snapshot
        {
            LodMeshes = lodMeshes,
            Diffuse = objects.MaterialReference.DiffuseReference,
            Normal = objects.MaterialReference.NormalReference,
            Pbr = objects.MaterialReference.PBRMap,
            BandTexture = rings.Texture,
            Size = objects.Size,
            Density = objects.Density,
            RenderDistance = objects.RenderDistance,
            Thickness = objects.Thickness,
        };
    }
}
