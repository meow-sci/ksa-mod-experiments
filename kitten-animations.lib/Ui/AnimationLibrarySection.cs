using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.KittenAnimationsLib.Ui;

/// <summary>Every animation the game loaded for this kitten, one button per clip.</summary>
public static class AnimationLibrarySection
{
    private const int ButtonColumns = 3;

    private static readonly float4 ActiveButtonColour = new(0.20f, 0.45f, 0.30f, 1f);

    public static void Render(AnimationUiContext ctx)
    {
        ImGui.SeparatorText("Animations");

        for (int i = 0; i < ctx.Catalog.Groups.Count; i++)
            RenderGroup(ctx, ctx.Catalog.Groups[i], i);

        if (ctx.Catalog.UnresolvedFields.Count > 0)
            RenderUnresolved(ctx);
    }

    private static void RenderGroup(AnimationUiContext ctx, AnimationGroup group, int groupIndex)
    {
        if (group.Entries.Count == 0) return;

        bool open = ImGui.CollapsingHeader($"{group.Name}  ({group.Entries.Count}) (?)##ka_grp_{groupIndex}",
            ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(group.Tooltip);
        if (!open) return;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable($"##ka_grp_tbl_{groupIndex}", ButtonColumns, ImGuiTableFlags.NoPadOuterX))
        {
            for (int c = 0; c < ButtonColumns; c++)
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (i % ButtonColumns == 0) ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(i % ButtonColumns);
                RenderEntryButton(ctx, group.Entries[i], groupIndex, i);
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        ImGui.Spacing();
    }

    private static void RenderEntryButton(AnimationUiContext ctx, AnimationEntry entry, int groupIndex, int index)
    {
        bool isActive = ReferenceEquals(ctx.Driver.ForcedAnimation, entry.Animation);

        if (isActive)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ActiveButtonColour));

        if (ImGui.Button($" {entry.Label} ##ka_anim_{groupIndex}_{index}", new float2(-1, 0)))
            ctx.Driver.Play(entry);

        if (isActive)
            ImGui.PopStyleColor();

        ImGui.SetItemTooltip($"{entry.Source}\nLength: {entry.Length:F2}s");
    }

    private static void RenderUnresolved(AnimationUiContext ctx)
    {
        ImGui.Spacing();
        ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f),
            $"{ctx.Catalog.UnresolvedFields.Count} game field(s) could not be resolved — the game build may have changed.");
        ImGui.SetItemTooltip(string.Join("\n", ctx.Catalog.UnresolvedFields));
    }
}
