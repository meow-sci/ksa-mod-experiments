using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.KittenAnimationsLib.Ui;

/// <summary>
/// Per-processor animation strength. Each row is a checkbox that takes the value off the game plus the
/// value the mod then holds; the driver re-applies them every frame from the pose prefix, because the
/// game rewrites several of these itself.
/// </summary>
public static class StrengthSection
{
    public static void Render(AnimationUiContext ctx)
    {
        bool open = ImGui.CollapsingHeader("Animation Strength (?)##ka_strength", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Blend weights of the kitten's additive animation processors: ears, eyes,\n"
                           + "personality mood face and the acceleration-driven reactive face.");
        if (!open) return;

        var driver = ctx.Driver;
        var processors = ctx.Processors;

        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ka_strength_tbl", 3, flags))
        {
            ImGui.TableSetupColumn("##chk", ImGuiTableColumnFlags.WidthFixed, 28f);
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 2f);

            Row("##ka_ears", "Ear Motion",
                processors.Ear != null,
                driver.OverrideEarWeight, v => driver.OverrideEarWeight = v,
                driver.EarWeight, 0f, 1f, "%.2f", v => driver.EarWeight = v,
                "CatEarAnim blend weight. 1 = full ear/helmet mask pose, 0 = ears follow the body clip only.\n"
                + "The game sets this to 0 for seated kittens and 1 otherwise.");

            Row("##ka_eyeangle", "Eye Look Angle",
                processors.Eye != null,
                driver.OverrideEyeLookAngle, v => driver.OverrideEyeLookAngle = v,
                driver.EyeLookAngleDeg, 0f, 90f, "%.0f deg", v => driver.EyeLookAngleDeg = v,
                "CatEyeAnim.MaxLookAtAngle — how far the eyes may swing off forward when tracking\n"
                + "the camera. Game default is 30 degrees.");

            Row("##ka_eyepitch", "Eye Pitch Offset",
                processors.Eye != null,
                driver.OverrideEyePitch, v => driver.OverrideEyePitch = v,
                driver.EyePitchDeg, -90f, 90f, "%.0f deg", v => driver.EyePitchDeg = v,
                "CatEyeAnim.LookPitchOffsetDeg — tilts the forward basis the look angle is measured from.\n"
                + "The game rewrites this every frame (45 on a ladder, speed-scaled while swimming, else 0).");

            Row("##ka_personality", $"Personality Face ({ctx.Avatar.Personality})",
                processors.Personality != null,
                driver.OverridePersonalityWeight, v => driver.OverridePersonalityWeight = v,
                driver.PersonalityWeight, 0f, 1f, "%.2f", v => driver.PersonalityWeight = v,
                "Weight of the permanent mood face the game picks from the character's personality.\n"
                + "Neutral kittens have no personality processor, so this row is unavailable for them.");

            Row("##ka_reactive", "Reactive Face Cap",
                processors.Reactive != null,
                driver.LimitReactiveExpression, v => driver.LimitReactiveExpression = v,
                driver.ReactiveExpressionMax, 0f, 1f, "%.2f", v => driver.ReactiveExpressionMax = v,
                "The game drives a scared face from linear + angular acceleration and rewrites its weight\n"
                + "every frame, so it can only be capped. 0 = never pull a face under acceleration.");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        RenderLiveWeights(ctx);
    }

    private static void Row(string id, string label, bool available,
                            bool enabled, Action<bool> applyEnabled,
                            float value, float min, float max, string format, Action<float> applyValue,
                            string tooltip)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        if (!available) ImGui.BeginDisabled();
        bool working = enabled;
        if (ImGui.Checkbox($"{id}_chk", ref working)) applyEnabled(working);
        if (!available) ImGui.EndDisabled();
        ImGui.SetItemTooltip(available
            ? "Take this value off the game and hold the mod's value."
            : "Not present on this character.");

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.SetItemTooltip(tooltip);

        ImGui.TableNextColumn();
        bool interactive = available && enabled;
        if (!interactive) ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(-1);
        float slider = value;
        if (ImGui.SliderFloat($"{id}_val", ref slider, min, max, format)) applyValue(slider);
        if (!interactive) ImGui.EndDisabled();
        ImGui.SetItemTooltip(tooltip);
    }

    private static void RenderLiveWeights(AnimationUiContext ctx)
    {
        var processors = ctx.Processors;

        ImGui.Spacing();
        ImGui.TextDisabled(
            $"Live: ears {Weight(processors.Ear?.ExpressionWeight)}   "
            + $"personality {Weight(processors.Personality?.ExpressionWeight)}   "
            + $"reactive {Weight(processors.Reactive?.ExpressionWeight)}   "
            + $"mod expression {ctx.Expressions.CurrentWeight:F2}");
    }

    private static string Weight(float? value) => value.HasValue ? value.Value.ToString("F2") : "n/a";
}
