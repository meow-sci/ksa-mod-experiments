using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.KsaAbstractions;
using MeowSci.AverageTwrLib;
using MeowSci.EternalFlameLib;
using MeowSci.GarrysTorchLib;
using MeowSci.GeeForceLib;
using MeowSci.GlassLib;
using MeowSci.IFeelSeenLib;
using MeowSci.ItsSoShinyLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.ConManLib;
using MeowSci.KittenAnimationsLib;
using MeowSci.KiwisMarblesLib;
using MeowSci.SkittlesLib;
using MeowSci.ZippoLib;
using MeowSci.HumbleArteestLib;
using MeowSci.DohLib;
using MeowSci.KitchenSinkLib;
using MeowSci.PartsNowLib;
using MeowSci.ThugLifeLib;
using MeowSci.DontStifleMeLib;
using MeowSci.GraffitiLib;
using MeowSci.FreeFallinLib;
using MeowSci.HotPursuitLib;
using MeowSci.PyroLib;
using MeowSci.RockyMcRockFaceLib;
using MeowSci.BloominOnionLib;

namespace MeowSci.Unscience;

[StarMapMod]
public sealed class Mod
{
    public bool ImmediateUnload => false;
    private readonly List<IWorkspaceFeature> _features = new();
    private WorkspaceWindow? _workspace;
    private bool _initialized;
    private bool _disposed;

    [StarMapImmediateLoad] public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            AddFeature("average-twr", () => new AverageTwrSubmod());
            AddFeature("bloomin-onion", () => new BloominOnionSubmod());
            AddFeature("camera-controller-override", () => new CameraControllerOverrideSubmod());
            AddFeature("con-man", () => new ConManSubmod());
            AddFeature("doh", () => new DohSubmod());
            AddFeature("dont-stifle-me", () => new DontStifleMeSubmod());
            AddFeature("eternal-flame", () => new EternalFlameSubmod());
            AddFeature("free-fallin", () => new FreeFallinSubmod());
            AddFeature("garrys-torch", () => new GarrysTorchSubmod());
            AddFeature("geeforce", () => new GeeForceSubmod());
            AddFeature("glass", () => new GlassSubmod());
            AddFeature("graffiti", () => new GraffitiSubmod());
            AddFeature("hot-pursuit", () => new HotPursuitSubmod());
            AddFeature("humble-arteest", () => new HumbleArteestSubmod());
            AddFeature("i-feel-seen", () => new IFeelSeenSubmod());
            AddFeature("its-so-shiny", () => new ItsSoShinySubmod());
            AddFeature("kitchen-sink", () => new KitchenSinkSubmod());
            AddFeature("kitten-animations", () => new KittenAnimationsSubmod());
            AddFeature("kiwis-marbles", () => new KiwisMarblesSubmod());
            AddFeature("parts-now", () => new PartsNowSubmod());
            AddFeature("pyro", () => new PyroSubmod());
            AddFeature("rocky-mcrock-face", () => new RockyMcRockFaceSubmod());
            AddFeature("skittles", () => new SkittlesSubmod());
            AddFeature("thug-life", () => new ThugLifeSubmod());
            AddFeature("zippo", () => new ZippoSubmod());
            _workspace = new WorkspaceWindow(_features);
            Patcher.MenuBarToggle = () => _workspace.Toggle();
            HiddenUiFrameHook.BeforeGui = UpdateSubmods;
            HiddenUiFrameHook.AfterGui = UpdateWelds;
            Patcher.Patch();
            _initialized = true;
            Console.WriteLine($"unscience: initialized {_features.Count} workspace features");
        }
        catch (Exception ex) { Console.WriteLine($"unscience: initialization failed: {ex}"); }
    }

    private void AddFeature(string id, Func<IWorkspaceFeature> create)
    {
        IWorkspaceFeature? feature = null;
        try
        {
            feature = create();
            feature.Initialize();
            feature.CaptureDraft();
            feature.ConfigureRuntime(FeatureRuntime.For(feature));
            _features.Add(feature);
        }
        catch (Exception ex)
        {
            string error = ex.Message;
            if (feature != null)
            {
                try { feature.Dispose(); FeatureRuntime.For(feature).ReleasePatches(); }
                catch (Exception cleanup) { error += " Partial initialization cleanup failed: " + cleanup.Message; }
            }
            Console.WriteLine($"unscience/{id}: initialization failed: {ex}");
            _features.Add(new UnavailableFeature(id, error));
        }
    }

    [StarMapBeforeGui] public void OnBeforeUi(double dt) => UpdateSubmods(dt);
    private void UpdateSubmods(double dt)
    {
        if (!_initialized || _disposed) return;
        GameThread.DrainOnGameThread();
        foreach (var feature in _features)
            try { FeatureRuntime.For(feature).Sync(); feature.Update(dt); FeatureRuntime.For(feature).Sync(); }
            catch (Exception ex) { Console.WriteLine($"unscience/{feature.FeatureId}: update failed: {ex.Message}"); }
    }
    private void UpdateWelds(double dt)
    {
        if (!_initialized || _disposed) return;
        foreach (var feature in _features)
            try { feature.UpdateAfterGui(dt); FeatureRuntime.For(feature).Sync(); }
            catch (Exception ex) { Console.WriteLine($"unscience/{feature.FeatureId}: after-GUI failed: {ex.Message}"); }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_initialized || _disposed) return;
        try
        {
            if (ImGui.IsKeyPressed(ImGuiKey.F11)) _workspace?.Toggle();
            _workspace?.Render(dt);
            foreach (var feature in _features)
                try { FeatureUi.Render(feature.RenderFloatingWindows); }
                catch (Exception ex) { Console.WriteLine($"unscience/{feature.FeatureId}: floating UI failed: {ex.Message}"); }
        }
        catch (Exception ex) { Console.WriteLine($"unscience: UI failed: {ex}"); }
        UpdateWelds(dt);
    }

    [StarMapUnload]
    public void Unload()
    {
        if (_disposed) return;
        _workspace?.SaveSession();
        _disposed = true;
        for (int i = _features.Count - 1; i >= 0; --i)
            try { _features[i].Dispose(); FeatureRuntime.For(_features[i]).ReleasePatches(); }
            catch (Exception ex) { Console.WriteLine($"unscience/{_features[i].FeatureId}: unload failed: {ex.Message}"); }
        Patcher.Unload();
    }
}
