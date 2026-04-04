using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

public sealed class KittenAnimationsSubmod : ISubmod
{
    public string Name => "Kitten Animations";
    public string Tooltip => "Controls kitten character animations and facial expressions.";

    private readonly KittenAnimationController _animController = new();

    public void Initialize() { }

    public void Update(double dt)
    {
        var avatar = KittenAvatarAccessor.GetKittenAvatar();
        _animController.Update(dt, avatar);
    }

    public void RenderContent()
    {
        var avatar = KittenAvatarAccessor.GetKittenAvatar();

        SubmodUI.BeginContentArea("##ka_content");

        if (avatar == null)
        {
            ImGui.TextDisabled("No avatar detected in scene.");
            SubmodUI.EndContentArea();
            return;
        }

        // === Expressions ===
        ImGui.SeparatorText("Expressions");

        // Duration slider in a 2-column label/widget table
        var durTableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ka_expr_dur", 2, durTableFlags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Duration (s)");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var duration = _animController.ExpressionDuration;
            if (ImGui.DragFloat("##ka_expr_dur_val", ref duration, 0.1f, 1.0f, 5.0f))
                _animController.ExpressionDuration = duration;

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // Expression buttons in a 3-column equal-width grid
        var exprBtnFlags = ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable("##ka_expr_btns", 3, exprBtnFlags))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Button(" Angry ##ka", new float2(-1, 0)))
            {
                var anim = avatar.Expressions.Angry?[_animController.Random.Next(avatar.Expressions.Angry.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Angry, anim, avatar);
            }
            ImGui.TableSetColumnIndex(1);
            if (ImGui.Button(" Awe ##ka", new float2(-1, 0)))
            {
                var anim = avatar.Expressions.Awe?[_animController.Random.Next(avatar.Expressions.Awe.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Awe, anim, avatar);
            }
            ImGui.TableSetColumnIndex(2);
            if (ImGui.Button(" Happy ##ka", new float2(-1, 0)))
            {
                var anim = avatar.Expressions.Happy?[_animController.Random.Next(avatar.Expressions.Happy.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Happy, anim, avatar);
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Button(" Sad ##ka", new float2(-1, 0)))
            {
                var anim = avatar.Expressions.Sad?[_animController.Random.Next(avatar.Expressions.Sad.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Sad, anim, avatar);
            }
            ImGui.TableSetColumnIndex(1);
            if (ImGui.Button(" Scared ##ka", new float2(-1, 0)))
            {
                var anim = avatar.Expressions.Scared?[_animController.Random.Next(avatar.Expressions.Scared.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Scared, anim, avatar);
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // === MMU Animations ===
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.SeparatorText("MMU Animations");

        var mmuBtnFlags = ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable("##ka_mmu", 3, mmuBtnFlags))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Button(" Idle Default ##ka", new float2(-1, 0)))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuIdleDefaultAnim);
            ImGui.TableSetColumnIndex(1);
            if (ImGui.Button(" Move Left ##ka", new float2(-1, 0)))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveLeftLoopAnim);
            ImGui.TableSetColumnIndex(2);
            if (ImGui.Button(" Move Right ##ka", new float2(-1, 0)))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveRightLoopAnim);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Button(" Move Forward ##ka", new float2(-1, 0)))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveForwardLoopAnim);
            ImGui.TableSetColumnIndex(1);
            if (ImGui.Button(" Move Backward ##ka", new float2(-1, 0)))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveBackwardLoopAnim);
            ImGui.TableSetColumnIndex(2);
            if (ImGui.Button(" Move Up ##ka", new float2(-1, 0)))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveUpLoopAnim);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Button(" Move Down ##ka", new float2(-1, 0)))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveDownLoopAnim);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // === Walking Animations ===
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.SeparatorText("Walking Animations");

        var walkBtnFlags = ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable("##ka_walk", 3, walkBtnFlags))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Button(" Running ##ka", new float2(-1, 0)))
            {
                var anim = avatar.Animations.WalkingAnimations.RunningAnim;
                KittenAnimationController.PlayAvatarAnimation(avatar, anim);
            }
            ImGui.TableSetColumnIndex(1);
            if (ImGui.Button(" Walking ##ka", new float2(-1, 0)))
            {
                var anim = avatar.Animations.WalkingAnimations.WalkingAnim;
                KittenAnimationController.PlayAvatarAnimation(avatar, anim);
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        SubmodUI.EndContentArea();
    }

    public void Dispose() { }
}
