using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using StarMap.API;
using MeowSci.ItsSoShinyLib;

namespace MeowSci.ItsSoShiny;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private ItsSoShinySubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new ItsSoShinySubmod();
            Patcher.Patch();
            _submod.Initialize();
            _isInitialized = true;
            Console.WriteLine("its-so-shiny: initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"its-so-shiny: Error during initialization: {ex.Message}");
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
            Console.WriteLine($"its-so-shiny: Error in OnAfterUi: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"its-so-shiny: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(500, 640), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("its-so-shiny", ref _windowVisible, ImGuiWindowFlags.MenuBar))
            _submod.RenderContent();
        ImGui.End();
    }
}