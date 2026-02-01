namespace mod.Animation;

/// <summary>
/// Wrapper class for a keyframe animation with optional transition-in settings.
/// A keyframe represents one animation step in a sequence.
/// </summary>
public class Keyframe
{
    /// <summary>
    /// Unique identifier for this keyframe in the sequence.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// The animation to execute for this keyframe.
    /// </summary>
    public IKeyframeAnimation Animation { get; set; }
    
    /// <summary>
    /// Whether to include a smooth transition from the previous keyframe.
    /// If true, a transition animation will be inserted before this keyframe plays.
    /// </summary>
    public bool IncludeTransitionIn { get; set; } = false;
    
    /// <summary>
    /// Duration of the transition-in animation in seconds.
    /// Only used if IncludeTransitionIn is true.
    /// </summary>
    public double TransitionInDurationSeconds { get; set; } = 1.0;
    
    /// <summary>
    /// Easing function for the transition-in animation.
    /// Only used if IncludeTransitionIn is true.
    /// </summary>
    public EasingType TransitionInEasing { get; set; } = EasingType.EaseInOut;
    
    /// <summary>
    /// Create a keyframe with the specified animation.
    /// </summary>
    /// <param name="animation">The animation to wrap.</param>
    public Keyframe(IKeyframeAnimation animation)
    {
        Animation = animation;
    }
    
    /// <summary>
    /// Create an empty keyframe (for deserialization or manual initialization).
    /// </summary>
    public Keyframe()
    {
        Animation = null!;
    }
}
