// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Globalization;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The handful of colours, formatters and widget idioms every parts-now panel shares, kept in one
/// place so the four panels stay small and look identical.
/// </summary>
/// <remarks>
/// Colours follow the repository's <c>imgui-design</c> conventions: red for errors, green for
/// success, <see cref="ImGui.TextDisabled" /> for neutral text. <see cref="Warning" /> is the one
/// addition — "loaded but degraded" is neither an error nor a success, and painting it red would
/// make a non-fatal result look like a failed load.
/// </remarks>
public static class PanelStyle
{
    /// <summary>Colour for anything that blocks an action or reports a failure.</summary>
    public static readonly float4 Error = new float4(1f, 0.3f, 0.3f, 1f);

    /// <summary>Colour for a completed action or a healthy state.</summary>
    public static readonly float4 Success = new float4(0.4f, 1f, 0.4f, 1f);

    /// <summary>Colour for a non-fatal problem the user should still see (degraded, leaking, ...).</summary>
    public static readonly float4 Warning = new float4(1f, 0.75f, 0.3f, 1f);

    private const double BytesPerMiB = 1024.0 * 1024.0;

    /// <summary>Formats a byte count as MiB with one decimal place.</summary>
    /// <param name="bytes">The byte count.</param>
    /// <returns>For example <c>"12.5"</c>.</returns>
    public static string Mib(ulong bytes) =>
        (bytes / BytesPerMiB).ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>Formats a byte count as MiB with one decimal place.</summary>
    /// <param name="bytes">The byte count.</param>
    /// <returns>For example <c>"12.5"</c>.</returns>
    public static string Mib(uint bytes) => Mib((ulong)bytes);

    /// <summary>
    /// Shows a tooltip for the item just submitted, including while that item is disabled — which is
    /// exactly when the user most needs to know why they cannot click it.
    /// </summary>
    /// <param name="text">The tooltip text.</param>
    public static void HoverTooltip(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(text);
        }
    }

    /// <summary>
    /// Draws a small button that copies <paramref name="value" /> to the system clipboard, and the
    /// value itself as disabled text next to it.
    /// </summary>
    /// <param name="id">Unique ImGui id suffix, without the leading <c>##</c>.</param>
    /// <param name="value">The text to copy; the button is disabled when it is empty.</param>
    public static void CopyableText(string id, string value)
    {
        bool has = !string.IsNullOrEmpty(value);

        if (!has)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button($" Copy path ##pn_copy_{id}") && has)
        {
            ImGui.SetClipboardText(value);
        }

        if (!has)
        {
            ImGui.EndDisabled();
        }

        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(has ? value : "(unavailable)");
    }

    /// <summary>
    /// Pushes the destructive-action button colours. Always pair with <see cref="PopDanger" />.
    /// </summary>
    public static void PushDanger()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
    }

    /// <summary>Pops the colours pushed by <see cref="PushDanger" />.</summary>
    public static void PopDanger()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
    }

    /// <summary>
    /// Begins a two-column label/widget layout table (1:3 stretch) with the repository's standard
    /// cell padding. Returns false when the table could not be opened, in which case
    /// <see cref="EndLabelTable" /> must NOT be called.
    /// </summary>
    /// <param name="id">Unique ImGui table id, including the leading <c>##</c>.</param>
    /// <returns>True when the table is open.</returns>
    public static bool BeginLabelTable(string id)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));

        if (ImGui.BeginTable(id, 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn($"{id}_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn($"{id}_val", ImGuiTableColumnFlags.WidthStretch, 3f);
            return true;
        }

        ImGui.PopStyleVar();
        return false;
    }

    /// <summary>Closes the table opened by <see cref="BeginLabelTable" />.</summary>
    public static void EndLabelTable()
    {
        ImGui.EndTable();
        ImGui.PopStyleVar();
    }

    /// <summary>Starts a label/widget row and writes the label into the first column.</summary>
    /// <param name="label">The row's label.</param>
    public static void LabelRow(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.TableNextColumn();
    }

    /// <summary>
    /// Renders a bordered child window that sits flush under a collapsing header, matching the
    /// repository's list-item idiom. Always pair with <see cref="ImGui.EndChild" />.
    /// </summary>
    /// <param name="id">Unique ImGui child id.</param>
    /// <param name="height">Fixed height, or 0 to auto-size vertically.</param>
    public static void BeginBorderedChild(string id, float height)
    {
        float padX = ImGui.GetStyle().WindowPadding.X;
        float width = ImGui.GetContentRegionAvail().X + padX * 2f;

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - padX);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(20f, 10f));

        ImGuiChildFlags flags = ImGuiChildFlags.Borders | ImGuiChildFlags.AlwaysUseWindowPadding;
        if (height <= 0f)
        {
            flags |= ImGuiChildFlags.AutoResizeY;
        }

        ImGui.BeginChild(id, new float2(width, height), flags);
        ImGui.PopStyleVar();
    }

    /// <summary>
    /// Copies text to the clipboard, swallowing (and logging) any failure — a clipboard error must
    /// never take down a render pass.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    public static void CopyToClipboard(string text)
    {
        try
        {
            ImGui.SetClipboardText(text);
        }
        catch (Exception ex)
        {
            Console.WriteLine("parts-now: could not write to the clipboard: " + ex.Message);
        }
    }
}
