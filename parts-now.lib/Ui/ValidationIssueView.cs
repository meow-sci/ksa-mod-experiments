// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using System.Collections.Generic;
using System.Text;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Renders a list of <see cref="ValidationIssue" />s grouped by severity, with the rule number and
/// the offending element id on every line. Shared by the paste panel and the results panel.
/// </summary>
public static class ValidationIssueView
{
    private const float MaxHeight = 220f;

    /// <summary>Draws the grouped issue list, or nothing when there are no issues.</summary>
    /// <param name="id">Unique ImGui id suffix, without the leading <c>##</c>.</param>
    /// <param name="issues">The findings to show, in rule order.</param>
    public static void Render(string id, IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        if (issues.Count == 0)
        {
            return;
        }

        if (ImGui.Button($" Copy issues ##{id}_copy"))
        {
            PanelStyle.CopyToClipboard(Flatten(issues));
        }

        ImGui.BeginChild($"##{id}_child", new float2(0f, MaxHeight), ImGuiChildFlags.Borders);

        RenderGroup(issues, IssueSeverity.Error, "Errors (these block the load)", PanelStyle.Error);
        RenderGroup(issues, IssueSeverity.Warning, "Warnings", PanelStyle.Warning);

        ImGui.EndChild();
    }

    private static void RenderGroup(
        IReadOnlyList<ValidationIssue> issues,
        IssueSeverity severity,
        string heading,
        in float4 colour)
    {
        int count = 0;
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == severity)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        ImGui.SeparatorText($"{heading} ( {count} )");

        for (int i = 0; i < issues.Count; i++)
        {
            ValidationIssue issue = issues[i];
            if (issue.Severity != severity)
            {
                continue;
            }

            ImGui.TextColored(colour, Prefix(issue));
            ImGui.TextWrapped($"    {issue.Message}");
        }
    }

    private static string Prefix(ValidationIssue issue)
    {
        string element = string.IsNullOrEmpty(issue.ElementId) ? "(document)" : issue.ElementId;
        string source = string.IsNullOrEmpty(issue.SourceName) ? string.Empty : $" [{issue.SourceName}]";
        return $"{issue.Rule}  {element}{source}";
    }

    private static string Flatten(IReadOnlyList<ValidationIssue> issues)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < issues.Count; i++)
        {
            ValidationIssue issue = issues[i];
            builder.Append(issue.Severity == IssueSeverity.Error ? "ERROR " : "WARN  ")
                .Append(Prefix(issue))
                .Append(": ")
                .AppendLine(issue.Message);
        }

        return builder.ToString();
    }
}
