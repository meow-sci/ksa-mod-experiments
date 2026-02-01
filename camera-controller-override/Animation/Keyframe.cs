namespace mod.Animation;

/// <summary>
/// Wrapper class for a keyframe animation.
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
