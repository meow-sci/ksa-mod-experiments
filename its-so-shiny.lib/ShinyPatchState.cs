namespace MeowSci.ItsSoShinyLib;

/// <summary>Shared state for its-so-shiny Harmony patches (render-skip toggle).</summary>
public static class ShinyPatchState
{
    /// <summary>When true, shiny light-part meshes are rendered. When false they are hidden for better performance.</summary>
    public static bool RenderShinyParts = true;
}
