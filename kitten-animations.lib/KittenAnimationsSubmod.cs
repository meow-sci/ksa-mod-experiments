using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

public sealed class KittenAnimationsSubmod : ISubmod
{
    public string Name => "Kitten Animations";

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

        if (ImGui.CollapsingHeader("MMU Animations"))
        {
            var tableFlags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
            if (ImGui.BeginTable("##ka_mmu", 2, tableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Idle Default ##ka"))
                    KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuIdleDefaultAnim);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Move Left ##ka"))
                    KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveLeftLoopAnim);

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Move Right ##ka"))
                    KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveRightLoopAnim);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Move Forward ##ka"))
                    KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveForwardLoopAnim);

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Move Backward ##ka"))
                    KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveBackwardLoopAnim);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Move Up ##ka"))
                    KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveUpLoopAnim);

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Move Down ##ka"))
                    KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveDownLoopAnim);

                ImGui.EndTable();
            }
            ImGui.PopStyleVar(); // CellPadding
        }

        if (ImGui.CollapsingHeader("Expressions"))
        {
            // Duration slider in a 2-column label/widget table
            var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
            if (ImGui.BeginTable("##ka_expr_dur", 2, tableFlags))
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

            ImGui.SeparatorText("Expressions");

            // Expression buttons in a 3-column equal-width grid
            var btnFlags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
            if (ImGui.BeginTable("##ka_expr_btns", 3, btnFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Angry ##ka"))
                {
                    var anim = avatar.Expressions.Angry?[_animController.Random.Next(avatar.Expressions.Angry.Count)];
                    _animController.TriggerExpression(KittenAnimationController.ExpressionType.Angry, anim, avatar);
                }
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Awe ##ka"))
                {
                    var anim = avatar.Expressions.Awe?[_animController.Random.Next(avatar.Expressions.Awe.Count)];
                    _animController.TriggerExpression(KittenAnimationController.ExpressionType.Awe, anim, avatar);
                }
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Happy ##ka"))
                {
                    var anim = avatar.Expressions.Happy?[_animController.Random.Next(avatar.Expressions.Happy.Count)];
                    _animController.TriggerExpression(KittenAnimationController.ExpressionType.Happy, anim, avatar);
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Sad ##ka"))
                {
                    var anim = avatar.Expressions.Sad?[_animController.Random.Next(avatar.Expressions.Sad.Count)];
                    _animController.TriggerExpression(KittenAnimationController.ExpressionType.Sad, anim, avatar);
                }
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                if (ImGui.Button(" Scared ##ka"))
                {
                    var anim = avatar.Expressions.Scared?[_animController.Random.Next(avatar.Expressions.Scared.Count)];
                    _animController.TriggerExpression(KittenAnimationController.ExpressionType.Scared, anim, avatar);
                }

                ImGui.EndTable();
            }
            ImGui.PopStyleVar(); // CellPadding
        }

        if (ImGui.CollapsingHeader("Walking Animations"))
        {
            if (ImGui.Button(" Running ##ka"))
            {
                var anim = avatar.Animations.WalkingAnimations.RunningAnim;
                KittenAnimationController.PlayAvatarAnimation(avatar, anim);
            }
            ImGui.SameLine(0, 8);
            if (ImGui.Button(" Walking ##ka"))
            {
                var anim = avatar.Animations.WalkingAnimations.WalkingAnim;
                KittenAnimationController.PlayAvatarAnimation(avatar, anim);
            }
        }

        SubmodUI.EndContentArea();
    }

    public void Dispose() { }
}
