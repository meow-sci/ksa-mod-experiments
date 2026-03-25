using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.UnladenSwallowLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallow;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;
    private bool _serverEnabled = false;
    private SwallowServer? _server;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _server = new SwallowServer();
            Patcher.Patch();
            _isInitialized = true;
            Console.WriteLine("unladen-swallow: initialized.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        GameThread.DrainOnGameThread();
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            if (_server is not null && _server.IsRunning)
                _server.StopAsync().GetAwaiter().GetResult();

            Patcher.Unload();
            _isDisposed = true;
            Console.WriteLine("unladen-swallow: unloaded.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(400, 120), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Unladen Swallow"))
        {
            ImGui.TextColored(new float4(1.0f, 0.84f, 0.0f, 1.0f), "Unladen Swallow");
            ImGui.SeparatorText("HTTP RPC Server");

            if (ImGui.Checkbox("Enable HTTP Server", ref _serverEnabled))
            {
                if (_serverEnabled)
                {
                    try
                    {
                        _server!.StartAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"unladen-swallow: Failed to start server: {ex.Message}");
                        _serverEnabled = false;
                    }
                }
                else
                {
                    try
                    {
                        _server!.StopAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"unladen-swallow: Failed to stop server: {ex.Message}");
                    }
                }
            }

            if (_server is not null && _server.IsRunning)
                ImGui.TextColored(new float4(0.0f, 1.0f, 0.4f, 1.0f), "Server: Running on http://0.0.0.0:7887");
            else
                ImGui.TextDisabled("Server: Stopped");
        }
        ImGui.End();
    }
}

