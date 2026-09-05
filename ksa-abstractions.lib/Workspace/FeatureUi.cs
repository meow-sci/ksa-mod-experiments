using System;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Internal;
namespace MeowSci.KsaAbstractions;

/// <summary>Restores native ImGui scopes before reporting a feature exception to its host.</summary>
public static class FeatureUi
{
    public static unsafe void Render(Action draw)
    {
        var state = new ImGuiErrorRecoveryState();
        ImGui.Internal.ErrorRecoveryStoreState(&state);
        try { draw(); }
        catch
        {
            var io = ImGui.GetIO();
            bool assert = io.ConfigErrorRecoveryEnableAssert;
            io.ConfigErrorRecoveryEnableAssert = false;
            try { ImGui.Internal.ErrorRecoveryTryToRecoverState(&state); }
            finally { io.ConfigErrorRecoveryEnableAssert = assert; }
            throw;
        }
    }
}
