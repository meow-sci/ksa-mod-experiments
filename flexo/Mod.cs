using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.FlexoLib;

namespace MeowSci.Flexo;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    private FlexoSubmod? _submod;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new FlexoSubmod();
            _submod.Initialize();
            Patcher.Patch();
            _isInitialized = true;
            Console.WriteLine("flexo: Mod loaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;

        try
        {
            _submod?.Update(dt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error in OnBeforeUi: {ex.Message}");
        }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F11))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();

            _submod?.RenderFloatingWindows();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error in OnAfterUi: {ex.Message}");
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
            Console.WriteLine("flexo: Mod unloaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Flexo — Robotics", ref _windowVisible))
        {
            _submod?.RenderContent();
        }
        ImGui.End();
    }
}
