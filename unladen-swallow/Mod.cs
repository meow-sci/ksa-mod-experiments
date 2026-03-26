using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.UnladenSwallowLib;

namespace MeowSci.UnladenSwallow;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private UnladenSwallowSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new UnladenSwallowSubmod();
            Patcher.Patch();
            _submod.Initialize();
            _isInitialized = true;
            Console.WriteLine("unladen-swallow: initialized.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error during initialization: {ex.Message}");
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
            if (ImGui.IsKeyPressed(ImGuiKey.F11)) _windowVisible = !_windowVisible;
            if (_windowVisible) RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod.Dispose();
            Patcher.Unload();
            _isDisposed = true;
            Console.WriteLine("unladen-swallow: unloaded.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(400, 120), ImGuiCond.FirstUseEver);
        // No close button on this window
        if (ImGui.Begin("Unladen Swallow"))
            _submod.RenderContent();
        ImGui.End();
    }
}