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
        SubmodUI.BeginContentArea("##us_content");

        bool isRunning = _server is not null && _server.IsRunning;

        var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##us_info", 2, tableFlags))
        {
            ImGui.TableSetupColumn("##us_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##us_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Server enable row
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Server");
            ImGui.TableNextColumn();
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

            // Status row
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Status");
            ImGui.TableNextColumn();
            if (isRunning)
                ImGui.TextColored(new float4(0.0f, 1.0f, 0.4f, 1.0f), "● Running");
            else
                ImGui.TextColored(new float4(0.5f, 0.5f, 0.5f, 1.0f), "○ Stopped");

            // Endpoint row (only when running)
            if (isRunning)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Endpoint");
                ImGui.TableNextColumn();
                ImGui.TextDisabled("http://0.0.0.0:7887");
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        ImGui.Spacing();

        if (ImGui.CollapsingHeader("Available Endpoints"))
        {
            ImGui.TextDisabled("GET  /health                    — server health check");
            ImGui.TextDisabled("GET  /fov                       — read current FOV");
            ImGui.TextDisabled("POST /fov                       — set FOV");
            ImGui.TextDisabled("POST /vehicle/actions/ignite    — ignite engines");
            ImGui.TextDisabled("POST /vehicle/actions/shutdown  — shut down engines");
            ImGui.TextDisabled("GET  /blinky/grids              — list Blinky grids");
            ImGui.TextDisabled("POST /blinky/animate            — set animated scroll");
            ImGui.TextDisabled("POST /blinky/static             — set static pixel data");
            ImGui.TextDisabled("POST /blinky/off                — clear grid");
        }

        SubmodUI.EndContentArea();
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
