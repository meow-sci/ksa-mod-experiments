using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.KittenAnimationsLib.Ui;

/// <summary>
/// The animation-facing slice of KittenLocomotionTuning.Current — clip blend time, playback-rate
/// clamps, the nominal clip speeds the game divides ground speed by, and the moonwalk/swim blend
/// ramps. These are global game tuning, not mod state; the game's own Debug menu has the full set.
/// </summary>
public static class TuningSection
{
    public static void Render(AnimationUiContext ctx)
    {
        bool open = ImGui.CollapsingHeader("Locomotion Anim Tuning (?)##ka_tuning");
        ImGui.SetItemTooltip("Live edits to KittenLocomotionTuning.Current. These change the game's own\n"
                           + "animation behaviour for every kitten, not just this one. The game exposes the\n"
                           + "complete set (including physics) under its menu bar: Debug > Kitten Tuning.");
        if (!open) return;

        if (ImGui.Button(" Reset Animation Tuning ##ka_tuning_reset"))
            ResetAnimationTuning();
        ImGui.SetItemTooltip("Restores only the animation fields below to KittenLocomotionTuning.Default.\n"
                           + "Physics tuning is left alone.");

        ImGui.Spacing();
        RenderBlending();
        RenderClipSpeeds();
        RenderLowGravity();
        RenderSwim();
        RenderJumpAndLadder();

        ImGui.Spacing();
        RenderLiveBlendWeights(ctx);
    }

    private static void RenderBlending()
    {
        ImGui.SeparatorText("Blending & Rate");

        var grid = new FieldGrid("##ka_tune_blend");
        grid.Field("Blend Time", "##ka_t_blend", ref KittenLocomotionTuning.Current.AnimBlendTime, 0.005f, 0f, 2f,
            "Cross-fade (s) the game uses whenever it switches clip.");
        grid.Field("Idle Threshold", "##ka_t_idle", ref KittenLocomotionTuning.Current.IdleSpeedThreshold, 0.005f, 0f, 2f,
            "Ground speed (m/s) below which the idle clip plays instead of walk/run.");
        grid.Field("Rate Min", "##ka_t_ratemin", ref KittenLocomotionTuning.Current.PlaybackRateMin, 0.01f, 0f, 4f,
            "Lower clamp on the speed-matched playback rate.");
        grid.Field("Rate Max", "##ka_t_ratemax", ref KittenLocomotionTuning.Current.PlaybackRateMax, 0.01f, 0f, 8f,
            "Upper clamp on the speed-matched playback rate.");
        grid.End();
    }

    private static void RenderClipSpeeds()
    {
        ImGui.SeparatorText("Nominal Clip Speeds");

        var grid = new FieldGrid("##ka_tune_clip");
        grid.Field("Walk Clip", "##ka_t_walkclip", ref KittenLocomotionTuning.Current.WalkClipNominalSpeed, 0.01f, 0f, 10f,
            "Ground speed (m/s) the walk clip was authored at. Actual speed divided by this is the playback rate.");
        grid.Field("Run Clip", "##ka_t_runclip", ref KittenLocomotionTuning.Current.RunClipNominalSpeed, 0.01f, 0f, 15f,
            "Ground speed (m/s) the run clip was authored at.");
        grid.Field("Ladder Clip", "##ka_t_ladderclip", ref KittenLocomotionTuning.Current.LadderNominalSpeed, 0.01f, 0f, 5f,
            "Climb speed the ladder clip was authored at.");
        grid.Field("Tumble Clip", "##ka_t_tumbleclip", ref KittenLocomotionTuning.Current.TumbleNominalSpeed, 0.01f, 0f, 5f,
            "Fixed playback rate used for the tumble / flail clip.");
        grid.End();
    }

    private static void RenderLowGravity()
    {
        ImGui.SeparatorText("Low Gravity");

        var grid = new FieldGrid("##ka_tune_moon");
        grid.Field("Moon Walk Clip", "##ka_t_mwalk", ref KittenLocomotionTuning.Current.MoonwalkWalkNominalSpeed, 0.01f, 0f, 10f,
            "Nominal speed of the low-gravity walk clip.");
        grid.Field("Moon Run Clip", "##ka_t_mrun", ref KittenLocomotionTuning.Current.MoonwalkRunNominalSpeed, 0.01f, 0f, 15f,
            "Nominal speed of the low-gravity run clip.");
        grid.Field("Blend Start g", "##ka_t_mstart", ref KittenLocomotionTuning.Current.MoonwalkStartGravity, 0.01f, 0f, 30f,
            "Gravity (m/s^2) at which the moonwalk blend starts coming in.");
        grid.Field("Blend Full g", "##ka_t_mfull", ref KittenLocomotionTuning.Current.MoonwalkFullGravity, 0.01f, 0f, 30f,
            "Gravity (m/s^2) at which the moonwalk blend is fully applied.");
        grid.Field("Playback Scale", "##ka_t_mscale", ref KittenLocomotionTuning.Current.MoonwalkPlaybackScale, 0.01f, 0f, 3f,
            "Playback rate multiplier at full moonwalk weight — the floaty slow-motion look.");
        grid.End();
    }

    private static void RenderSwim()
    {
        ImGui.SeparatorText("Swimming");

        var grid = new FieldGrid("##ka_tune_swim");
        grid.Field("Swim Clip Speed", "##ka_t_swimclip", ref KittenLocomotionTuning.Current.NominalSwimAnimSpeed, 0.01f, 0f, 10f,
            "Speed the swim clip was authored at.");
        grid.Field("Blend Full Speed", "##ka_t_swimfull", ref KittenLocomotionTuning.Current.SwimBlendFullSpeed, 0.01f, 0f, 10f,
            "Swim speed at which the idle-to-stroke blend reaches 1.");
        grid.Field("Blend Half Life", "##ka_t_swimhl", ref KittenLocomotionTuning.Current.SwimBlendHalfLife, 0.01f, 0f, 3f,
            "Damping half-life (s) of that blend — higher is lazier.");
        grid.Field("Eye Pitch Factor", "##ka_t_swimeye", ref KittenLocomotionTuning.Current.SwimEyePitchFactor, 0.01f, 0f, 3f,
            "Scales the eye pitch offset taken from body tilt while swimming.");
        grid.End();
    }

    private static void RenderJumpAndLadder()
    {
        ImGui.SeparatorText("Jump & Ladder");

        var grid = new FieldGrid("##ka_tune_jump");
        grid.Field("Land Duration", "##ka_t_land", ref KittenLocomotionTuning.Current.JumpLandDuration, 0.01f, 0f, 3f,
            "How long (s) the landing clip plays before it freezes on its last frame.");
        grid.Field("Bounce Ignore", "##ka_t_bounce", ref KittenLocomotionTuning.Current.JumpLandBounceIgnoreTime, 0.01f, 0f, 3f,
            "Grace (s) after landing during which going airborne again does not restart the flail.");
        grid.Field("Ladder Eye Pitch", "##ka_t_ladeye", ref KittenLocomotionTuning.Current.LadderEyePitchDeg, 0.5f, -90f, 90f,
            "Eye pitch offset (deg) forced while on a ladder.");
        grid.End();
    }

    private static void RenderLiveBlendWeights(AnimationUiContext ctx)
    {
        var catalog = ctx.Catalog;
        var state = ctx.Kitten.LocomotionState;
        var tuning = KittenLocomotionTuning.Current;

        ImGui.TextDisabled(
            $"Live blends: walk {Weight(catalog.WalkPairSampler?.Weight)}   "
            + $"run {Weight(catalog.RunPairSampler?.Weight)}   "
            + $"swim {Weight(catalog.SwimPairSampler?.Weight)}");
        ImGui.TextDisabled(
            $"Derived: moonwalk {KittenLocomotion.ComputeMoonwalkWeight(state.GravityMagnitude, in tuning):F2}   "
            + $"swim target {KittenLocomotion.ResolveSwimBlend(state.GroundSpeed, in tuning):F2}");
    }

    private static string Weight(float? value) => value.HasValue ? value.Value.ToString("F2") : "n/a";

    private static void ResetAnimationTuning()
    {
        var defaults = KittenLocomotionTuning.Default;
        ref var current = ref KittenLocomotionTuning.Current;

        current.AnimBlendTime = defaults.AnimBlendTime;
        current.IdleSpeedThreshold = defaults.IdleSpeedThreshold;
        current.PlaybackRateMin = defaults.PlaybackRateMin;
        current.PlaybackRateMax = defaults.PlaybackRateMax;
        current.WalkClipNominalSpeed = defaults.WalkClipNominalSpeed;
        current.RunClipNominalSpeed = defaults.RunClipNominalSpeed;
        current.LadderNominalSpeed = defaults.LadderNominalSpeed;
        current.TumbleNominalSpeed = defaults.TumbleNominalSpeed;
        current.MoonwalkWalkNominalSpeed = defaults.MoonwalkWalkNominalSpeed;
        current.MoonwalkRunNominalSpeed = defaults.MoonwalkRunNominalSpeed;
        current.MoonwalkStartGravity = defaults.MoonwalkStartGravity;
        current.MoonwalkFullGravity = defaults.MoonwalkFullGravity;
        current.MoonwalkPlaybackScale = defaults.MoonwalkPlaybackScale;
        current.NominalSwimAnimSpeed = defaults.NominalSwimAnimSpeed;
        current.SwimBlendFullSpeed = defaults.SwimBlendFullSpeed;
        current.SwimBlendHalfLife = defaults.SwimBlendHalfLife;
        current.SwimEyePitchFactor = defaults.SwimEyePitchFactor;
        current.JumpLandDuration = defaults.JumpLandDuration;
        current.JumpLandBounceIgnoreTime = defaults.JumpLandBounceIgnoreTime;
        current.LadderEyePitchDeg = defaults.LadderEyePitchDeg;
    }

    /// <summary>Two label + DragFloat pairs per row across a 4-column stretch table.</summary>
    private sealed class FieldGrid
    {
        private const int PairsPerRow = 2;

        private readonly bool _open;
        private int _count;

        public FieldGrid(string id)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
            _open = ImGui.BeginTable(id, 4, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX);
        }

        public void Field(string label, string id, ref float value, float speed, float min, float max, string tooltip)
        {
            if (!_open) return;

            if (_count % PairsPerRow == 0) ImGui.TableNextRow();
            _count++;

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(label);
            ImGui.SetItemTooltip(tooltip);

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat(id, ref value, speed, min, max);
            ImGui.SetItemTooltip(tooltip);
        }

        public void End()
        {
            if (_open) ImGui.EndTable();
            ImGui.PopStyleVar(); // CellPadding
        }
    }
}
