using MeowSci.KsaLights;
namespace MeowSci.ItsSoShinyLib;

/// <summary>Shared state for its-so-shiny Harmony patches (render-skip toggle).</summary>
public static class ShinyPatchState
{
    /// <summary>
    /// When true, shiny light-part meshes are always rendered.
    /// When false (default), meshes are only rendered while the light is active.
    /// </summary>
    public static bool RenderShinyParts = false;
}
