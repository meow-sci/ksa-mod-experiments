using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.KittenAnimationsLib.Ui;

/// <summary>Live locomotion readout plus the controls that take the body animation off the game.</summary>
public static class PlaybackSection
{
    public static void Render(AnimationUiContext ctx)
    {
        bool open = ImGui.CollapsingHeader("Playback (?)##ka_playback", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("What the game is animating right now, and whether the mod has taken over.");
        if (!open) return;

        RenderStatus(ctx);
        ImGui.Spacing();
        RenderOverrideControls(ctx);
        ImGui.Spacing();
        RenderTimingControls(ctx);
    }

    private static void RenderStatus(AnimationUiContext ctx)
    {
        var state = ctx.Kitten.LocomotionState;

        ImGui.TextDisabled($"Mode: {state.Mode}    Control: {ctx.Kitten.ControlMode}    "
                         + $"Speed: {state.GroundSpeed:F2} m/s    Gravity: {state.GravityMagnitude:F2} m/s^2");
        ImGui.TextDisabled($"Jump chain: {ctx.Kitten.AnimJumpChainStage} ({ctx.Kitten.AnimJumpChainCountdown:F2}s)    "
                         + $"Game playback rate: {ctx.Kitten.AnimPlaybackRate:F2}x    "
                         + $"Personality: {ctx.Avatar.Personality}");
    }

    private static void RenderOverrideControls(AnimationUiContext ctx)
    {
        var driver = ctx.Driver;
        bool hasClip = driver.ForcedAnimation != null;

        if (!hasClip) ImGui.BeginDisabled();

        bool active = driver.OverrideActive;
        if (ImGui.Checkbox("Mod drives the body animation##ka_override", ref active))
        {
            if (active) driver.OverrideActive = true;
            else driver.Release();
        }
        ImGui.SetItemTooltip("While on, the mod forces the selected clip every frame instead of letting\n"
                           + "the game pick one from the locomotion state. Turn off to hand control back.");

        ImGui.SameLine(0, 12);
        if (ImGui.Button(" Restart ##ka_restart")) driver.Restart();
        ImGui.SetItemTooltip("Replay the forced clip from its first frame.");

        ImGui.SameLine(0, 8);
        bool paused = driver.Paused;
        if (ImGui.Checkbox("Freeze##ka_pause", ref paused)) driver.Paused = paused;
        ImGui.SetItemTooltip("Hold the forced clip on its current frame.");

        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Clear ##ka_clear")) driver.ClearClip();
        ImGui.SetItemTooltip("Drop the selection and give the animation back to the game.");

        if (!hasClip) ImGui.EndDisabled();

        ImGui.Spacing();
        if (hasClip)
        {
            var colour = driver.OverrideActive
                ? new float4(0.4f, 1f, 0.4f, 1f)
                : new float4(0.7f, 0.7f, 0.7f, 1f);
            string verb = driver.OverrideActive ? "Forcing" : "Selected";
            ImGui.TextColored(colour, $"{verb}: {driver.ForcedLabel}");
        }
        else
        {
            ImGui.TextDisabled("No clip selected — the game owns the animation.");
        }
    }

    private static void RenderTimingControls(AnimationUiContext ctx)
    {
        var driver = ctx.Driver;

        var flags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ka_playback_params", 4, flags))
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Blend Time");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            float blend = driver.BlendTime;
            if (ImGui.DragFloat("##ka_blend", ref blend, 0.01f, 0f, 2f))
                driver.BlendTime = blend;
            ImGui.SetItemTooltip("Cross-fade (s) into the forced clip.");

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Playback Rate");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            float rate = driver.PlaybackRateScale;
            if (ImGui.DragFloat("##ka_rate", ref rate, 0.01f, 0f, 5f))
                driver.PlaybackRateScale = rate;
            ImGui.SetItemTooltip("Multiplies the animation delta time on top of the game's own rate.\n"
                               + "0 = frozen, 1 = normal, 2 = double speed.");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }
}
