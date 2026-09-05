// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// GPU load/purge operations use RuntimeModLoader.Step at the host BeforeGui boundary,
// before this frame emits any ImGui texture draw commands.

using System;
using System.Collections.Generic;
using System.Text;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// T13.4 — the results panel: how every Part of the last load fared, the job log, and the
/// validation findings.
/// </summary>
/// <remarks>
/// <para>
/// The thumbnail column is drawn exactly the way <c>VehicleEditor.PartWindow.DrawPartImageButton</c>
/// draws it, with one extra guard: a <c>&lt;Thumbnail&gt;</c> declared in XML produces a
/// <c>ThumbnailReference</c> that has never had <c>CreateImageView</c> called on it, so its
/// <c>ImageViewEx</c> is default. Handing that to <c>GetOrCreateImGuiTexture</c> would register a
/// null <c>VkImageView</c> with the ImGui backend, so those parts fall back to a plain button.
/// </para>
/// <para>
/// The debug readback toggle described in the plan is deliberately absent: the readback belongs to
/// <c>PartThumbnailGenerator</c>, which the loader owns and disposes at the end of every job, so
/// there is nothing here to toggle.
/// </para>
/// </remarks>
public sealed class ResultsPanel
{
    private const float ThumbnailSize = 64f;
    private const float LogHeight = 240f;

    /// <summary>Draws the results, log and issue sections for the current (or last) job.</summary>
    public void Render()
    {
        bool open = MeowSci.KsaAbstractions.WorkspaceUi.Header("Results (?)##pn_results", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Per-part outcome of the last load, the job log, and the validation findings.");

        if (!open)
        {
            return;
        }

        RenderParts();
        RenderIssues();
        RenderLog();
    }

    private static void RenderParts()
    {
        LoadedModRecord? record = RuntimeModLoader.CurrentRecord;
        IReadOnlyList<PartLoadResult> results =
            record is null ? Array.Empty<PartLoadResult>() : record.Results;

        if (results.Count == 0)
        {
            ImGui.TextDisabled("No parts have been loaded yet this session.");
            return;
        }

        ImGui.SeparatorText($"Parts ( {results.Count} )");

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));

        ImGuiTableFlags flags = ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##pn_results", 3, flags))
        {
            ImGui.TableSetupColumn("Part", ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn("Thumbnail", ImGuiTableColumnFlags.WidthFixed, ThumbnailSize + 16f);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch, 5f);
            ImGui.TableHeadersRow();

            for (int i = 0; i < results.Count; i++)
            {
                PartLoadResult result = results[i];

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(result.PartId);

                ImGui.TableNextColumn();
                DrawThumbnail(result.PartId);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                RenderStatus(result);
            }

            ImGui.EndTable();
        }

        ImGui.PopStyleVar();
    }

    /// <summary>
    /// Mirrors <c>VehicleEditor.PartWindow.DrawPartImageButton</c>: the rendered thumbnail when the
    /// template has one, otherwise a same-sized blank button so the table stays aligned.
    /// </summary>
    private static void DrawThumbnail(string partId)
    {
        float2 size = new float2(ThumbnailSize);

        PartTemplate? template = null;
        try
        {
            template = GameRegistry.FindPart(partId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: could not look up part '{partId}' for its thumbnail: {ex.Message}");
        }

        // ImageView.IsNull() is the load-bearing guard: an XML-declared <Thumbnail> never had
        // CreateImageView called, so GetOrCreateImGuiTexture would hand ImGui a null image view.
        if (template?.Thumbnail is not { } thumbnail || thumbnail.ImageView.IsNull())
        {
            ImGui.Button($"##pn_thumb_{partId}", new float2?(size));
            return;
        }

        try
        {
            ImTextureRef texture = thumbnail.GetOrCreateImGuiTexture(Program.LinearClampedSampler);
            ImGui.ImageButton($"##pn_thumb_{partId}", texture, in size, null, null, null, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: could not draw the thumbnail for '{partId}': {ex.Message}");
            ImGui.Button($"##pn_thumb_{partId}", new float2?(size));
        }
    }

    private static void RenderStatus(PartLoadResult result)
    {
        switch (result.Status)
        {
            case PartLoadStatus.Ok:
                ImGui.TextColored(PanelStyle.Success, "OK");
                break;

            case PartLoadStatus.Degraded:
                // Amber, not red: the part did load and is usable, just not fully.
                ImGui.TextColored(PanelStyle.Warning, $"Degraded — {Reason(result)}");
                break;

            default:
                ImGui.TextDisabled($"Skipped — {Reason(result)}");
                break;
        }
    }

    private static string Reason(PartLoadResult result) =>
        string.IsNullOrEmpty(result.Reason) ? "no reason recorded" : result.Reason;

    private static void RenderIssues()
    {
        IReadOnlyList<ValidationIssue> issues = RuntimeModLoader.Issues;
        if (issues.Count == 0)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.SeparatorText($"Validation issues ( {issues.Count} )");
        ValidationIssueView.Render("pn_result_issues", issues);
    }

    private static void RenderLog()
    {
        IReadOnlyList<string> log = RuntimeModLoader.Log;

        ImGui.Spacing();
        ImGui.SeparatorText($"Log ( {log.Count} )");

        bool hasLog = log.Count > 0;
        if (!hasLog)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(" Copy log ##pn_copylog") && hasLog)
        {
            PanelStyle.CopyToClipboard(Flatten(log));
        }

        if (!hasLog)
        {
            ImGui.EndDisabled();
        }

        PanelStyle.HoverTooltip(hasLog
            ? "Copies every line of the job log to the clipboard."
            : "Nothing has been logged yet.");

        ImGui.BeginChild("##pn_log", new float2(0f, LogHeight), ImGuiChildFlags.Borders);

        for (int i = 0; i < log.Count; i++)
        {
            ImGui.TextWrapped(log[i]);
        }

        ImGui.EndChild();
    }

    private static string Flatten(IReadOnlyList<string> log)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < log.Count; i++)
        {
            builder.AppendLine(log[i]);
        }

        return builder.ToString();
    }
}
