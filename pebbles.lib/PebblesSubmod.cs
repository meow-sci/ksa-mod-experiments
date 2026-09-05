using System;
using System.Linq;
using MeowSci.KsaAbstractions;

namespace MeowSci.PebblesLib;

public sealed partial class PebblesSubmod : IWorkspaceFeature
{
    public string Name => "Pebbles — Ground Clutter Workshop";
    public string Tooltip => "Replace ground clutter per celestial, tune placement, and build collision shapes in a private mesh Workshop.";
    public string FeatureId => "pebbles";
    private readonly ClutterAssets _assets = new();
    private readonly ClutterController _controller;
    private readonly WorkshopEditor _workshop = new();
    private PebblesRecipe _recipe = new();
    private string _bodyId = "", _ecotypeName = "", _objectId = "";
    private string _workshopBody = "", _workshopEcotype = "", _workshopObject = "";
    private string _bulkMesh = "", _bulkTexture = "", _message = "";
    private int _lod, _bulkScope;
    private MaterialRecipe _bulkMaterial = new();
    private double _refreshTime;
    public PebblesSubmod() { _controller = new ClutterController(_assets); }
    public void Initialize() => Console.WriteLine("pebbles: initialized");
    public void ConfigureRuntime(FeatureRuntime runtime) => runtime.Patches("clutter", () => _controller.NeedsHooks, _controller.ApplyPatches, _controller.RemovePatches);
    public void Update(double dt)
    {
        _controller.Update();
        _workshop.Update();
        if (_releaseImports && !_controller.NeedsHooks && _controller.Faults.Count == 0)
        {
            _releaseImports = false; // A failed native release is reported, never retried every frame.
            _workshop.Release(); _workshop.Update();
            _assets.ReleaseGlbImports(); _glbOptions = [];
        }
        _refreshTime -= dt;
        if (_refreshTime > 0) return;
        _refreshTime = 5;
        Try(() => { _controller.Refresh(); if (_assets.MeshIds.Length == 0) _assets.Refresh(); });
    }
    public void RenderFloatingWindows()
    {
        _workshop.SetCompletion(CompleteWorkshop);
        _workshop.Draw(_assets);
        _glbBrowser.Draw(ImportGlb);
    }
    private void CompleteWorkshop(ObjectRecipe value)
    {
        if (_bodyId != _workshopBody) throw new InvalidOperationException("Select the Workshop's original celestial before keeping its recipe.");
        var ecotype = _recipe.Ecotypes.Find(e => e.Name == _workshopEcotype);
        var index = ecotype?.Objects.FindIndex(o => o.SourceId == _workshopObject) ?? -1;
        if (ecotype == null || index < 0) throw new InvalidOperationException("The Workshop destination is unresolved. Capture or select its original recipe before Done.");
        ecotype.Objects[index] = RecipeCopy.Clone(value);
    }
    public void CancelAuthoringGesture() => _workshop.CancelGesture();
    public void ReleaseLiveState() { _controller.Release(); _workshop.Release(); _releaseImports = true; }
    public void Dispose()
    {
        _workshop.Dispose(); _controller.Dispose();
        if (_assets.ImportedGlbCount > 0 && _controller.Faults.Count != 0)
            throw new InvalidOperationException("GLB imports retained because native clutter retirement failed; restart the game to reclaim them.");
        _assets.Dispose();
    }
    private void Try(Action action)
    {
        try { _message = ""; action(); }
        catch (Exception ex) { _message = ex.Message; Console.WriteLine($"pebbles: {ex}"); }
    }
    private EcotypeRecipe? SelectedEcotype => _recipe.Ecotypes.Find(e => e.Name == _ecotypeName);
    private ObjectRecipe? SelectedObject => SelectedEcotype?.Objects.Find(o => o.SourceId == _objectId);
}
