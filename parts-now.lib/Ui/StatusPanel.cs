// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// T13.1 — the header strip: whether loading is possible at all, how much of the reserved GPU
/// budget is left, what the current job is doing, and the persisted settings.
/// </summary>
/// <remarks>
/// <para>
/// This panel is the single source of truth for <see cref="LoadingEnabled" />. <c>PastePanel</c> and
/// <c>ModFolderPanel</c> take that value as their <c>Render(bool canLoad)</c> argument rather than
/// recomputing it, so the banner shown here and the buttons disabled there can never disagree.
/// </para>
/// <para>
/// The self-test is deliberately NOT re-run here: <c>GameRegistry.SelfTest()</c> runs once in
/// <c>PartsNowSubmod.Initialize()</c> and its result is passed in.
/// </para>
/// </remarks>
public sealed partial class StatusPanel
{
    /// <summary>
    /// True when parts-now may load anything at all: the reflection self-test passed and the shared
    /// mesh headroom really was reserved during startup. Recomputed at the top of every
    /// <see cref="Render" />, so read it after calling that.
    /// </summary>
    public bool LoadingEnabled { get; private set; }

    /// <summary>Draws the status strip.</summary>
    /// <param name="selfTestProblems">
    /// The problems <c>GameRegistry.SelfTest()</c> reported during initialization, as held by
    /// <c>PartsNowSubmod.SelfTestProblems</c>. Empty means healthy.
    /// </param>
    public void Render(IReadOnlyList<string> selfTestProblems)
    {
        ArgumentNullException.ThrowIfNull(selfTestProblems);

        LoadingEnabled = selfTestProblems.Count == 0
            && MeshBudget.Reserved
            && MeshBudget.FailureReason is null;

        RenderBlockedBanner(selfTestProblems);
        RenderMeshBudget();
        RenderBindlessTextures();
        RenderJob();
        RenderSettings();
        RenderLimitations();
    }

    private void RenderBlockedBanner(IReadOnlyList<string> selfTestProblems)
    {
        if (LoadingEnabled)
        {
            return;
        }

        ImGui.TextColored(PanelStyle.Error, "Loading is DISABLED — parts-now cannot safely register anything.");

        for (int i = 0; i < selfTestProblems.Count; i++)
        {
            ImGui.TextColored(PanelStyle.Error, $"  - {selfTestProblems[i]}");
        }

        if (!MeshBudget.Reserved)
        {
            ImGui.TextColored(
                PanelStyle.Error,
                "  - no mesh headroom was reserved at startup, so a runtime mesh would be written past "
                + "the end of KSA's shared vertex buffer.");
        }

        if (MeshBudget.FailureReason is { } reason)
        {
            ImGui.TextColored(PanelStyle.Error, $"  - {reason}");
        }

        ImGui.Spacing();
    }

    private static void RenderMeshBudget()
    {
        ImGui.SeparatorText("Shared mesh buffer");

        if (!TryReadBudget(out uint usedVertex, out uint allocVertex,
                out uint usedIndex, out uint allocIndex, out string? error))
        {
            ImGui.TextColored(
                PanelStyle.Error,
                $"The shared mesh buffer could not be read: {error ?? "unknown error"}");
            return;
        }

        if (!PanelStyle.BeginLabelTable("##pn_budget"))
        {
            return;
        }

        PanelStyle.LabelRow("Vertex");
        Bar(usedVertex, allocVertex);

        PanelStyle.LabelRow("Index");
        Bar(usedIndex, allocIndex);

        PanelStyle.EndLabelTable();

        ulong leakedVertex = MeshBudget.LeakedVertexBytes;
        ulong leakedIndex = MeshBudget.LeakedIndexBytes;

        ImGui.TextDisabled(
            $"Orphaned by unload / reload: {PanelStyle.Mib(leakedVertex)} MiB vtx / "
            + $"{PanelStyle.Mib(leakedIndex)} MiB idx — KSA's shared allocator is a bump pointer, so "
            + "these bytes stay spent until the game restarts.");

        if (MeshBudget.LeakWarningTripped)
        {
            ImGui.TextColored(
                PanelStyle.Warning,
                "More than half of the reserved headroom has been orphaned by reloads. Restart the "
                + "game before loading much more.");
        }

        ImGui.Spacing();
    }

    private static void Bar(uint used, uint allocated)
    {
        float fraction = allocated == 0u ? 0f : Math.Clamp((float)used / allocated, 0f, 1f);
        ImGui.ProgressBar(
            fraction,
            new float2(-1f, 0f),
            $"{PanelStyle.Mib(used)} / {PanelStyle.Mib(allocated)} MiB used");
    }

    private static bool TryReadBudget(
        out uint usedVertex, out uint allocatedVertex,
        out uint usedIndex, out uint allocatedIndex,
        out string? error)
    {
        try
        {
            usedVertex = MeshBudget.UsedVertexBytes;
            allocatedVertex = MeshBudget.AllocatedVertexBytes;
            usedIndex = MeshBudget.UsedIndexBytes;
            allocatedIndex = MeshBudget.AllocatedIndexBytes;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            usedVertex = 0u;
            allocatedVertex = 0u;
            usedIndex = 0u;
            allocatedIndex = 0u;
            error = ex.Message;
            return false;
        }
    }

    private static void RenderBindlessTextures()
    {
        ImGui.SeparatorText("Bindless textures");

        int count;
        int max;
        try
        {
            Program program = Program.Instance;
            if (program is null || program.BindlessTextures is null)
            {
                ImGui.TextDisabled("The game's bindless texture library is not available yet.");
                ImGui.Spacing();
                return;
            }

            count = program.BindlessTextures.TextureCount;
            max = program.BindlessTextures.MaxTextures;
        }
        catch (Exception ex)
        {
            ImGui.TextColored(PanelStyle.Error, $"The bindless texture library could not be read: {ex.Message}");
            ImGui.Spacing();
            return;
        }

        float fraction = max <= 0 ? 0f : Math.Clamp((float)count / max, 0f, 1f);
        ImGui.ProgressBar(fraction, new float2(-1f, 0f), $"{count} / {max} texture slots used");
        ImGui.TextDisabled(
            "The slot pool cannot grow, so a load that would not fit is rejected by validation rule V15.");
        ImGui.Spacing();
    }

    private static void RenderJob()
    {
        ImGui.SeparatorText("Current job");

        bool busy = RuntimeModLoader.IsBusy;

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"State: {RuntimeModLoader.StatusText}");

        ImGui.ProgressBar(Math.Clamp(RuntimeModLoader.Progress, 0f, 1f), new float2(-1f, 0f));

        if (!busy)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(" Cancel ##pn_cancel") && busy)
        {
            RuntimeModLoader.RequestCancel();
        }

        if (!busy)
        {
            ImGui.EndDisabled();
        }

        PanelStyle.HoverTooltip(
            "Cancellation is only honoured BETWEEN states — never in the middle of a Vulkan submit "
            + "and never while the background loader thread is still reading files. A cancelled job "
            + "is rolled back exactly like a failed one.");

        bool finished = RuntimeModLoader.State is LoadJobState.Done or LoadJobState.Failed;
        ImGui.SameLine(0, 8);
        if (!finished)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(" Clear result ##pn_clear") && finished)
        {
            RuntimeModLoader.Reset();
        }

        if (!finished)
        {
            ImGui.EndDisabled();
        }

        PanelStyle.HoverTooltip("Clears the log, the validation issues and the per-part results below.");

        if (RuntimeModLoader.FailureMessage is { } failure)
        {
            ImGui.Spacing();
            ImGui.TextColored(PanelStyle.Error, failure);
        }

        ImGui.Spacing();
    }
}
