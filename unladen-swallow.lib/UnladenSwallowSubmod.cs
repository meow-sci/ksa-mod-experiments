using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public sealed class UnladenSwallowSubmod : ISubmod
{
    public string Name => "Unladen Swallow \u2014 RPC Server";

    private SwallowServer? _server;
    private bool _serverEnabled;

    public void Initialize()
    {
        _server = new SwallowServer();
    }

    public void Update(double dt)
    {
        GameThread.DrainOnGameThread();
    }

    public void RenderContent()
    {
        ImGui.TextColored(new float4(1.0f, 0.84f, 0.0f, 1.0f), "Unladen Swallow");
        ImGui.SeparatorText("HTTP RPC Server");

        if (ImGui.Checkbox("Enable HTTP Server##us", ref _serverEnabled))
        {
            if (_serverEnabled)
            {
                try
                {
                    _server!.StartAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"grant/unladen-swallow: Failed to start server: {ex.Message}");
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
                    Console.WriteLine($"grant/unladen-swallow: Failed to stop server: {ex.Message}");
                }
            }
        }

        if (_server is not null && _server.IsRunning)
            ImGui.TextColored(new float4(0.0f, 1.0f, 0.4f, 1.0f), "Server: Running on http://0.0.0.0:7887");
        else
            ImGui.TextDisabled("Server: Stopped");
    }

    public void Dispose()
    {
        if (_server is not null && _server.IsRunning)
        {
            try { _server.StopAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.WriteLine($"grant/unladen-swallow: Error stopping server: {ex.Message}"); }
        }
    }
}
