namespace MeowSci.DontStifleMeLib;

/// <summary>
/// Runtime toggles read by <see cref="EditorScalePatches"/> every frame. All patches are
/// installed once; flipping these flags changes editor behavior immediately with no re-patching.
/// </summary>
public static class EditorScaleSettings
{
    /// <summary>
    /// Master switch. When true the 0.5x–2.0x top-level part scale clamp is lifted and scale-gizmo
    /// drags act on the dragged axis only. When false every patch is a no-op (stock editor).
    /// </summary>
    public static bool Enabled = true;

    /// <summary>
    /// Keep the game's scale snapping (0.25 m diameter increments). True is the stock behavior.
    /// Only consulted while <see cref="Enabled"/> is true.
    /// </summary>
    public static bool Snap = true;

    public static bool ClampRemovalActive => Enabled;
    public static bool PerAxisScalingActive => Enabled;
    public static bool SnapDisabledActive => Enabled && !Snap;
}
