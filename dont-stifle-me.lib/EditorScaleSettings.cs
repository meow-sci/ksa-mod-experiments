namespace MeowSci.DontStifleMeLib;

/// <summary>
/// Runtime toggles read by <see cref="EditorScalePatches"/> every frame. All patches are
/// installed once; flipping these flags changes editor behavior immediately with no re-patching.
/// </summary>
public static class EditorScaleSettings
{
    /// <summary>Master switch. When false every patch is a no-op and the stock editor behaves normally.</summary>
    public static bool Enabled = true;

    /// <summary>Replace the stock 0.5x–2.0x top-level part scale clamp with (1e-6, +inf).</summary>
    public static bool RemoveClamp = true;

    /// <summary>Scale gizmo drags affect only the dragged axis (X/Y/Z) instead of all three uniformly.</summary>
    public static bool PerAxisScaling = true;

    public static bool ClampRemovalActive => Enabled && RemoveClamp;
    public static bool PerAxisScalingActive => Enabled && PerAxisScaling;
}
