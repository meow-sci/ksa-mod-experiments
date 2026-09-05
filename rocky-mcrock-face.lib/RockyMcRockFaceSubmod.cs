using MeowSci.KsaRings;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;

namespace MeowSci.RockyMcRockFaceLib;

/// <summary>
/// Rocky McRock Face — swap the meshes and textures KSA uses for planetary ring
/// objects (Saturn's rock field). Any built-in mesh with retained CPU geometry —
/// including part/subpart meshes — can be assigned per LOD, along with the rock
/// PBR material textures and the 2D ring band texture.
/// </summary>
public sealed partial class RockyMcRockFaceSubmod : IWorkspaceFeature
{
    public string Name => "Rocky McRock Face - Planetary Ring Swapper";

    public string Tooltip =>
        "Swap the meshes and textures of KSA's planetary ring objects (Saturn's rock field).\n" +
        "Pick any built-in mesh — including part subpart meshes — per LOD, change the rock\n" +
        "material textures, the ring band texture, and the rock field density/size.\n" +
        "Applying rebuilds the renderer (brief hitch). Overrides are session-only —\n" +
        "restarting the game brings the stock ring back.";

    private readonly RingSwapController _controller = new();
    private RingSelection _selection = new();

    private readonly Dictionary<string, RingSelection> _appliedSelections = new();
    private bool _catalogReady;
    private double _rescanTimer;

    public void Initialize()
    {
        RingOwnership.Replacing += BeforeRingReplacement;
        Console.WriteLine("rocky-mcrock-face: initialized");
    }

    public void Update(double dt)
    {
        _rescanTimer -= dt;
        if (_rescanTimer > 0) return;
        _rescanTimer = 2.0;
        try
        {
            if (!_catalogReady)
            {
                _controller.Catalog.Refresh();
                _catalogReady = _controller.Catalog.MeshIds.Length > 0 && _controller.Catalog.TextureIds.Length > 0;
            }
            _controller.RefreshBodies();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: update failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        ReleaseLiveState();
        RingOwnership.Replacing -= BeforeRingReplacement;
        _controller.Dispose();
    }

    /// <summary>
    /// Frees GPU buffers of converted meshes no longer selected anywhere. Safe only right
    /// after a successful rebuild: the fresh ring data references exactly the clones that
    /// were resolved for it, so everything outside the current selections is unreferenced.
    /// </summary>
    private void BeforeRingReplacement(KSA.Celestial celestial)
    {
        foreach (var body in _controller.Bodies)
            if (ReferenceEquals(body.Celestial, celestial) && _appliedSelections.Remove(body.Id)) _controller.Restore(body);
        // The replacement's renderer rebuild is responsible for releasing old frame references.
        // Retain converted meshes until our next successful rebuild or disposal.
    }

    private void PruneUnusedMeshClones()
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in _appliedSelections.Values)
        {
            foreach (var id in selection.LodMeshIds)
                if (id.Length > 0) keep.Add(id);
        }
        _controller.MeshFactory.PruneExcept(keep);
    }

    private RingSelection GetOrCreateSelection(string bodyId) => _selection;
}
