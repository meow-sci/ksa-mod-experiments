using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.EternalFlameLib;

namespace MeowSci.EternalFlame;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private EternalFlameSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new EternalFlameSubmod();
            _submod.Initialize();
            Patcher.Patch();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error during initialization: {ex.Message}\n{ex}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            _submod.Update(dt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error in OnBeforeUi: {ex.Message}\n{ex}");
        }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            if (ImGui.IsKeyPressed(ImGuiKey.F11))
            {
                _windowVisible = !_windowVisible;
                Console.WriteLine($"eternal-flame: window visibility toggled - visible={_windowVisible}");
            }
            if (_windowVisible) RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            Console.WriteLine("eternal-flame: Unload - begin");
            if (_submod != null)
                _submod.Dispose();
            Patcher.Unload();
            _isDisposed = true;
            Console.WriteLine("eternal-flame: Unload - complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error during unload: {ex.Message}\n{ex}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(500, 450), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Eternal Flame - Infinite Fuel", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}