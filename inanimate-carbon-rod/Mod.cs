using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;

namespace MeowSci.InanimateCarbonRod;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"inanimate-carbon-rod: Error during initialization: {ex.Message}");
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
            ImGui.Text("Placeholder — submod UI coming soon.");
        }
        ImGui.End();
    }
}

