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
            // Create all submods in display order
            var iFeelSeen = new IFeelSeenSubmod();
            var skittles = new SkittlesSubmod();
            var cameraOverride = new CameraControllerOverrideSubmod();

            _features.Add(new AverageTwrSubmod());
            _features.Add(new BloominOnionSubmod());
            _features.Add(cameraOverride);
            _features.Add(new ConManSubmod());
            _features.Add(new DohSubmod());
            _features.Add(new DontStifleMeSubmod());
            _features.Add(new EternalFlameSubmod());
            _features.Add(new FreeFallinSubmod());
            _features.Add(new GarrysTorchSubmod());
            _features.Add(new GeeForceSubmod());
            _features.Add(new GlassSubmod());
            _features.Add(new GraffitiSubmod());
            _features.Add(new HotPursuitSubmod());
            _features.Add(new HumbleArteestSubmod());
            _features.Add(iFeelSeen);
            _features.Add(new ItsSoShinySubmod());
            _features.Add(new KitchenSinkSubmod());
            _features.Add(new KittenAnimationsSubmod());
            _features.Add(new KiwisMarblesSubmod());
            _features.Add(new PartsNowSubmod());
            _features.Add(new PyroSubmod());
            _features.Add(new RockyMcRockFaceSubmod());
            _features.Add(skittles);
            _features.Add(new ThugLifeSubmod());
            _features.Add(new ZippoSubmod());


            foreach (var feature in _features) feature.Initialize();
            _workspace = new WorkspaceWindow(_features);
            Patcher.IFeelSeenTracker = iFeelSeen.Tracker;
            Patcher.CameraSequencePlayer = cameraOverride.SequencePlayer;
            Patcher.MenuBarToggle = () => _workspace.Toggle();
            HiddenUiFrameHook.BeforeGui = UpdateSubmods;
            HiddenUiFrameHook.AfterGui = UpdateWelds;
            Patcher.Patch();
            _initialized = true;
            Console.WriteLine($"unscience: initialized {_features.Count} workspace features");
        }
        catch (Exception ex) { Console.WriteLine($"unscience: initialization failed: {ex}"); }
    }

    [StarMapBeforeGui] public void OnBeforeUi(double dt) => UpdateSubmods(dt);
    private void UpdateSubmods(double dt)
    {
        if (!_initialized || _disposed) return;
        GameThread.DrainOnGameThread();
        foreach (var feature in _features)
            try { feature.Update(dt); }
            catch (Exception ex) { Console.WriteLine($"unscience/{feature.FeatureId}: update failed: {ex.Message}"); }
    }
    private void UpdateWelds(double dt)
    {
        if (!_initialized || _disposed) return;
        try { GarrysTorchSubmod.Instance?.UpdateWelds(dt); }
        catch (Exception ex) { Console.WriteLine($"unscience: weld update failed: {ex.Message}"); }
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
                try { feature.RenderFloatingWindows(); }
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
            try { _features[i].Dispose(); }
            catch (Exception ex) { Console.WriteLine($"unscience/{_features[i].FeatureId}: unload failed: {ex.Message}"); }
        Patcher.Unload();
    }
}
