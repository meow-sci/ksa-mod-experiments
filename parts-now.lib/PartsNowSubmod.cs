// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.PartsNowLib;

/// <summary>
/// parts-now entry point: runtime Part / SubPart loading from pasted XML or an existing mod folder.
/// Hosted by the unscience supermod and by the standalone <c>parts-now</c> mod.
/// </summary>
public sealed class PartsNowSubmod : ISubmod
{
    private readonly List<string> _selfTestProblems = new List<string>();

    private bool _firstFrameDone;

    /// <inheritdoc />
    public string Name => "Parts Now";

    /// <inheritdoc />
    public string Tooltip =>
        "Load Parts and SubParts into a running game — paste XML into a new mod folder, "
        + "or load / reload / unload an existing mod folder without restarting.";

    /// <summary>
    /// Problems found by <see cref="GameRegistry.SelfTest" /> during <see cref="Initialize" />.
    /// Non-empty means a KSA update moved something and loading must stay disabled.
    /// </summary>
    public IReadOnlyList<string> SelfTestProblems => _selfTestProblems;

    /// <summary>True when parts-now is able to load mods at all.</summary>
    public bool CanLoad => _selfTestProblems.Count == 0 && MeshBudget.Reserved;

    /// <summary>
    /// Called from <c>[StarMapAllModsLoaded]</c>, which StarMap fires as a Harmony postfix on
    /// <c>ModLibrary.LoadAll()</c> — i.e. BEFORE <c>ModLibrary.Bind()</c> allocates the shared
    /// interleaved mesh buffers. That ordering is what makes <see cref="MeshBudget.Reserve" /> work,
    /// so this must never be moved to a later hook.
    /// </summary>
    public void Initialize()
    {
        _selfTestProblems.Clear();
        _selfTestProblems.AddRange(GameRegistry.SelfTest());

        MeshBudget.Reserve();
    }

    /// <inheritdoc />
    public void Update(double dt)
    {
        if (!_firstFrameDone)
        {
            // By the first real UI frame ModLibrary.Bind() has run and the enlarged shared buffers
            // are allocated, so the bump cursors can be rewound to the startup watermark.
            MeshBudget.OnFirstFrame();
            _firstFrameDone = true;
        }
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
