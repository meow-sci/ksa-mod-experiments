using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using MeowSci.CameraControllerOverrideLib.Animation;
namespace MeowSci.CameraControllerOverrideLib;

public sealed partial class CameraControllerOverrideSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
if (_liveSequencePlayer.Keyframes.Count > 0)
            yield return new LiveStateItem<KeyframeSequencePlayer>("sequence", "Camera sequence", "Active camera", _liveSequencePlayer.State.ToString(), _liveSequencePlayer, player =>
            {
                _livePanel.Render(player);
                if (ImGui.Button("Copy sequence to workspace", new float2(-1, 0))) CopySequence(player, _sequencePlayer);
            });
    }
    private static void ValidateRecipes(List<AnimationRecipe> recipes)
    { if (recipes == null || recipes.Count > 512) throw new InvalidOperationException("Invalid sequence."); foreach (var recipe in recipes) recipe.Create(); }
    private void ApplySequence() { CopySequence(_sequencePlayer, _liveSequencePlayer); _liveSequencePlayer.Play(); }
    private static void CopySequence(KeyframeSequencePlayer source, KeyframeSequencePlayer target)
    {
        var recipes = source.Keyframes.Select(k => AnimationRecipe.Capture(k.Animation)).ToList();
        ValidateRecipes(recipes);
        target.Clear();
        foreach (var recipe in recipes) target.AddKeyframe(recipe.Create());
        target.ReturnToStartEnabled = source.ReturnToStartEnabled;
        target.ReturnToStartDuration = source.ReturnToStartDuration;
        target.ReturnToStartEasing = source.ReturnToStartEasing;
        target.ReturnToStartEasingPowerStart = source.ReturnToStartEasingPowerStart;
        target.ReturnToStartEasingPowerEnd = source.ReturnToStartEasingPowerEnd;
    }

}
