using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.RockyMcRockFaceLib;

namespace MeowSci.RockyMcRockFace;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private RockyMcRockFaceSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new RockyMcRockFaceSubmod();
            _submod.Initialize();
            Patcher.Patch();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        try { _submod.Update(dt); }
        catch (Exception ex) { Console.WriteLine($"rocky-mcrock-face: Update error: {ex.Message}"); }
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
            Console.WriteLine($"rocky-mcrock-face: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            Patcher.Unload();
            _submod.Dispose();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(560, 640), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Rocky McRock Face###rockymcrockface", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
