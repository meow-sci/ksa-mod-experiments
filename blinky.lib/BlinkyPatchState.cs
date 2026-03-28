namespace MeowSci.BlinkyLib;

/// <summary>Shared state for blinky Harmony patches (render-skip toggle).</summary>
public static class BlinkyPatchState
{
    /// <summary>When true, pixel-engine meshes are rendered. When false, they are hidden for better performance.</summary>
    public static bool RenderPixelParts = false;
}
