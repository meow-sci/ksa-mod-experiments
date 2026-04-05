using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.InanimateCarbonRodLib;

namespace MeowSci.InanimateCarbonRod;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private InanimateCarbonRodSubmod _submod = null!;
    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new InanimateCarbonRodSubmod();
            _submod.Initialize();
            _isInitialized = true;
            Console.WriteLine("inanimate-carbon-rod: Initialized (standalone)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"inanimate-carbon-rod: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        try { _submod.Update(dt); }
        catch (Exception ex) { Console.WriteLine($"inanimate-carbon-rod: Update error: {ex.Message}"); }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F10))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"inanimate-carbon-rod: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod?.Dispose();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"inanimate-carbon-rod: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(520f, 420f), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Inanimate Carbon Rod", ref _windowVisible))
        {
            _submod.RenderContent();
        }
        ImGui.End();
    }
}

