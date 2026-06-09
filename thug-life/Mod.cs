using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.ThugLifeLib;

namespace MeowSci.ThugLife;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private ThugLifeSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new ThugLifeSubmod();
            Patcher.Patch();
            _submod.Initialize();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        _submod.Update(dt);
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            if (ImGui.IsKeyPressed(ImGuiKey.F12)) _windowVisible = !_windowVisible;
            if (_windowVisible) RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod?.Dispose();
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(500, 600), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Thug Life###thug-life", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
