using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.GraffitiLib;

namespace MeowSci.Graffiti;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private GraffitiSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new GraffitiSubmod();
            _submod.Initialize();
            Patcher.Patch();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"graffiti: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        try { _submod.Update(dt); }
        catch (Exception ex) { Console.WriteLine($"graffiti: Update error: {ex.Message}"); }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            if (ImGui.IsKeyPressed(ImGuiKey.F11)) _windowVisible = !_windowVisible;
            if (_windowVisible) RenderWindow();

            // Always rendered: the click-placement mode and the file browser must keep working
            // while the main window is hidden or its section collapsed.
            _submod.RenderFloatingWindows();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"graffiti: Error in OnAfterUi: {ex.Message}");
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
            Console.WriteLine($"graffiti: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(560, 640), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Graffiti###graffiti", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
