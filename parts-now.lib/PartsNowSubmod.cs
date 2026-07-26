// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.PartsNowLib;

/// <summary>
/// parts-now entry point: runtime Part / SubPart loading from pasted XML or an existing mod folder.
/// Hosted by the unscience supermod and by the standalone <c>parts-now</c> mod.
/// </summary>
public sealed class PartsNowSubmod : ISubmod
{
    /// <inheritdoc />
    public string Name => "Parts Now";

    /// <inheritdoc />
    public string Tooltip =>
        "Load Parts and SubParts into a running game — paste XML into a new mod folder, "
        + "or load / reload / unload an existing mod folder without restarting.";

    /// <inheritdoc />
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public void Update(double dt)
    {
    }

    /// <inheritdoc />
    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##pn_content");
        ImGui.TextDisabled("parts-now — not yet implemented.");
        SubmodUI.EndContentArea();
    }

    /// <inheritdoc />
    public void RenderFloatingWindows()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
