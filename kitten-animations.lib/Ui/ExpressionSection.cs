using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.KittenAnimationsLib.Ui;

/// <summary>Facial expression triggers and the envelope that shapes how hard they land.</summary>
public static class ExpressionSection
{
    private const int ButtonColumns = 3;

    public static void Render(AnimationUiContext ctx)
    {
        ImGui.SeparatorText("Expressions");

        RenderVariantSelector(ctx);
        ImGui.Spacing();
        RenderTriggerButtons(ctx);
        ImGui.Spacing();
        RenderEnvelope(ctx);
        ImGui.Spacing();
        RenderStatus(ctx);
    }

    private static void RenderVariantSelector(AnimationUiContext ctx)
    {
        int maxVariants = MaxVariantCount(ctx);
        var items = new string[maxVariants + 1];
        items[0] = "Random";
        for (int i = 0; i < maxVariants; i++)
            items[i + 1] = $"Variant {i + 1}";

        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ka_expr_variant", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Variant");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            int index = ctx.ExpressionVariant + 1;
            if (ImGui.Combo("##ka_expr_variant_val", ref index, items, items.Length))
                ctx.ExpressionVariant = index - 1;
            ImGui.SetItemTooltip("Characters author several clips per expression. Pick one, or let each\n"
                               + "trigger roll a random variant. Out-of-range picks clamp to the last clip.");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    private static void RenderTriggerButtons(AnimationUiContext ctx)
    {
        var expressions = KittenExpressionController.AllExpressions;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable("##ka_expr_btns", ButtonColumns, ImGuiTableFlags.NoPadOuterX))
        {
            for (int c = 0; c < ButtonColumns; c++)
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            for (int i = 0; i < expressions.Length; i++)
            {
                if (i % ButtonColumns == 0) ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(i % ButtonColumns);
                RenderTriggerButton(ctx, expressions[i]);
            }

            int clearSlot = expressions.Length;
            if (clearSlot % ButtonColumns == 0) ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(clearSlot % ButtonColumns);
            if (ImGui.Button(" Clear ##ka_expr_clear", new float2(-1, 0)))
                ctx.Expressions.Clear();
            ImGui.SetItemTooltip("Drop the expression back to zero weight immediately.");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    private static void RenderTriggerButton(AnimationUiContext ctx, KittenExpressionController.ExpressionType type)
    {
        var variants = KittenExpressionController.GetVariants(ctx.Avatar, type);
        int count = variants?.Count ?? 0;

        if (count == 0) ImGui.BeginDisabled();

        if (ImGui.Button($" {type} ##ka_expr_{type}", new float2(-1, 0)))
            ctx.Expressions.Trigger(ctx.Avatar, type, ctx.ExpressionVariant, ctx.Random);

        if (count == 0) ImGui.EndDisabled();

        ImGui.SetItemTooltip(count == 0
            ? $"This character has no {type} clips."
            : $"{count} authored clip(s).");
    }

    private static void RenderEnvelope(AnimationUiContext ctx)
    {
        var expressions = ctx.Expressions;

        var flags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ka_expr_env", 4, flags))
        {
            ImGui.TableNextRow();
            Field("Strength", "##ka_expr_strength", expressions.PeakWeight, 0.01f, 0f, 1f,
                "How strongly the expression pose is mixed over the body animation.\n1 = the full authored pose.",
                v => expressions.PeakWeight = v);
            Field("Hold (s)", "##ka_expr_hold", expressions.HoldDuration, 0.05f, 0f, 30f,
                "Seconds held at full strength before easing out.",
                v => expressions.HoldDuration = v);

            ImGui.TableNextRow();
            Field("Ease In (s)", "##ka_expr_in", expressions.EaseInDuration, 0.01f, 0f, 3f,
                "Quadratic ramp-up time from zero weight.",
                v => expressions.EaseInDuration = v);
            Field("Ease Out (s)", "##ka_expr_out", expressions.EaseOutDuration, 0.01f, 0f, 3f,
                "Linear ramp-down time back to zero weight.",
                v => expressions.EaseOutDuration = v);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        bool latch = expressions.Latch;
        if (ImGui.Checkbox("Latch (hold until cleared)##ka_expr_latch", ref latch))
            expressions.Latch = latch;
        ImGui.SetItemTooltip("Ignore Hold/Ease Out and keep the expression at full strength until Clear is pressed.");
    }

    /// <summary>
    /// One label + value cell of the envelope table.
    ///
    /// Deliberately a <c>DragFloat</c>, not a <c>SliderFloat</c>: min/max still bound the
    /// mouse drag, but ImGui does not clamp typed input on a drag widget unless
    /// <c>ImGuiSliderFlags.ClampOnInput</c> is set — so ctrl+click / double-click can push a
    /// hold or an ease past the range the mouse can reach. A <c>SliderFloat</c> clamps typed
    /// input unconditionally and cannot offer that. Do not add
    /// <c>ClampOnInput</c>/<c>AlwaysClamp</c> here.
    /// </summary>
    private static void Field(string label, string id, float value, float speed,
                              float min, float max, string tooltip, Action<float> apply)
    {
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        float working = value;
        if (ImGui.DragFloat(id, ref working, speed, min, max, "%.2f"))
            apply(working);
        ImGui.SetItemTooltip(tooltip + TypeToExceedHint);
    }

    /// <summary>Appended to every envelope tooltip so the out-of-range path is discoverable.</summary>
    private const string TypeToExceedHint =
        "\n\nDrag to adjust within range; double-click or ctrl+click to type a value beyond it.";

    private static void RenderStatus(AnimationUiContext ctx)
    {
        var expressions = ctx.Expressions;

        if (expressions.Current == KittenExpressionController.ExpressionType.None)
        {
            ImGui.TextDisabled("No expression playing.");
            return;
        }

        string remaining = expressions.Latch ? "latched" : $"{expressions.Remaining:F2}s left";
        ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f),
            $"{expressions.CurrentVariant}   weight {expressions.CurrentWeight:F2}   {remaining}");
    }

    private static int MaxVariantCount(AnimationUiContext ctx)
    {
        int max = 1;
        foreach (var type in KittenExpressionController.AllExpressions)
        {
            var variants = KittenExpressionController.GetVariants(ctx.Avatar, type);
            if (variants != null) max = Math.Max(max, variants.Count);
        }
        return max;
    }
}
