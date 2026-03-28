using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.ConManLib;

namespace MeowSci.ConMan;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private ConManSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new ConManSubmod();
            _submod.Initialize();
            _isInitialized = true;
            Console.WriteLine("con-man: Initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"con-man: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        try { _submod.Update(dt); }
        catch (Exception ex) { Console.WriteLine($"con-man: Update error: {ex.Message}"); }
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
            Console.WriteLine($"con-man: Error in OnAfterUi: {ex.Message}");
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
            Console.WriteLine($"con-man: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(500, 600), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Con-Man \u2014 Layout Manager", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}

