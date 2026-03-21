using System;
using System.Linq;
using KSA;

namespace MeowSci.KittenAnimationsLib;

/// <summary>Manages kitten avatar animation state, including expression timers and animation playback.</summary>
public class KittenAnimationController
{
    public enum ExpressionType { None, Angry, Awe, Happy, Sad, Scared }

    private const float ExpressionEaseInDuration = 0.25f;

    private ExpressionType _currentExpression = ExpressionType.None;
    private AnimationAssetRef? _currentExpressionAnim = null;
    private float _expressionTimer = 0f;
    private float _expressionDuration = 1.5f;
    private float _expressionEaseInTimer = 0f;

    public Random Random { get; } = new Random();

    public float ExpressionDuration
    {
        get => _expressionDuration;
        set => _expressionDuration = value;
    }

    /// <summary>Updates expression timer and eases expression weight. Call once per frame from BeforeGui.</summary>
    public void Update(double dt, CharacterAvatar? avatar)
    {
        if (_expressionTimer > 0f)
        {
            _expressionTimer -= (float)dt;
            _expressionEaseInTimer += (float)dt;

            float easeInProgress = Math.Min(_expressionEaseInTimer / ExpressionEaseInDuration, 1.0f);
            float easedWeight = easeInProgress * easeInProgress; // Quadratic ease-in: t^2

            if (avatar != null && _currentExpressionAnim != null)
            {
                var expressionProcessor = avatar.Core.CharacterModel.AnimProcessors
                    .OfType<CatExpressionAnim>()
                    .FirstOrDefault();

                if (expressionProcessor != null)
                    expressionProcessor.ExpressionWeight = easedWeight;
            }
        }
        else if (_currentExpression != ExpressionType.None)
        {
            _currentExpression = ExpressionType.None;
            _currentExpressionAnim = null;
            _expressionEaseInTimer = 0f;
        }
    }

    /// <summary>Triggers an expression animation on the avatar for the configured duration.</summary>
    public void TriggerExpression(ExpressionType expressionType, AnimationAssetRef? animation, CharacterAvatar avatar)
    {
        if (animation == null) return;

        SetExpressionAnimation(avatar, animation);

        _currentExpression = expressionType;
        _currentExpressionAnim = animation;
        _expressionTimer = _expressionDuration;
        _expressionEaseInTimer = 0f;
    }

    /// <summary>Plays a body/MMU animation on the avatar.</summary>
    public static void PlayAvatarAnimation(CharacterAvatar avatar, IAnimation? animation)
    {
        if (avatar == null || animation == null) return;

        try
        {
            avatar.Core.CharacterModel.SetAnimation(animation);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error playing animation: {ex.Message}");
        }
    }

    /// <summary>Sets the expression animation on the avatar's CatExpressionAnim processor, starting weight at 0.</summary>
    public static void SetExpressionAnimation(CharacterAvatar avatar, AnimationAssetRef? animation)
    {
        if (avatar == null || animation == null) return;

        try
        {
            var expressionProcessor = avatar.Core.CharacterModel.AnimProcessors
                .OfType<CatExpressionAnim>()
                .FirstOrDefault();

            if (expressionProcessor != null)
            {
                expressionProcessor.ExpressionAnim = animation;
                expressionProcessor.ExpressionWeight = 0f; // Will be eased in by Update
            }
            else
            {
                Console.WriteLine("[EXPR] Warning: CatExpressionAnim processor not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXPR] Error setting expression animation: {ex.Message}");
        }
    }
}
