using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.FreeFallinLib;
using StarMap.API;

namespace MeowSci.FreeFallin;

[StarMapMod]
public sealed class Mod
{
    public bool ImmediateUnload => false;

    private FreeFallinSubmod _submod = null!;
    private bool _initialized;
    private bool _disposed;
    private bool _windowVisible;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new FreeFallinSubmod();
            _submod.Initialize();
            Patcher.Patch();
            _initialized = true;
        }
        catch (Exception ex) { Console.WriteLine($"free-fallin: initialization failed: {ex.Message}"); }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_initialized || _disposed) return;
        try { _submod.Update(dt); }
        catch (Exception ex) { Console.WriteLine($"free-fallin: update failed: {ex.Message}"); }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_initialized || _disposed) return;
        try
        {
            if (ImGui.IsKeyPressed(ImGuiKey.F11)) _windowVisible = !_windowVisible;
            if (_windowVisible)
            {
                ImGui.SetNextWindowSize(new float2(560f, 540f), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("Free Fallin###free_fallin", ref _windowVisible)) _submod.RenderContent();
                ImGui.End();
            }
            _submod.RenderFloatingWindows();
        }
        catch (Exception ex) { Console.WriteLine($"free-fallin: UI failed: {ex.Message}"); }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            Patcher.Unload();
            _submod.Dispose();
            _disposed = true;
        }
        catch (Exception ex) { Console.WriteLine($"free-fallin: unload failed: {ex.Message}"); }
    }
}
