using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;

namespace MeowSci.Doh;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

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
            Patcher.Patch();
            _isInitialized = true;
            Console.WriteLine("doh: Initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt) { }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F8))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            Patcher.Unload();
            _isDisposed = true;
            Console.WriteLine("doh: Unloaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(450, 500), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("DOH - Kitten Spawner###doh-window", ref _windowVisible))
        {
            ImGui.End();
            return;
        }

        ImGui.Text("DOH mod placeholder — implementation in progress");
        ImGui.End();
    }
}
