namespace MeowSci.ThugLifeLib;

/// <summary>
/// Static pixel pattern for the thug-life sunglasses meme, encoded as a 26x5 grid.
///
/// Character legend:
///   '.' = transparent (no quad emitted, no geometry → background shows through)
///   '#' = black opaque (RGBA = 0, 0, 0, 255)
///   'W' = white opaque (RGBA = 255, 255, 255, 255) — sunflare/glare highlight
///
/// Layout (26 cols × 5 rows):
///   - cols 0-11  = left lens (12 wide)
///   - cols 12-13 = transparent bridge
///   - cols 14-25 = right lens (12 wide)
///
/// Each lens is a square with stair-stepped corners cut off across rows 0..4, and
/// carries a 3-row diagonal glare pattern in its upper-left.
/// </summary>
public static class ThugLifeTexturePattern
{
    public const int Width = 26;
    public const int Height = 5;

    public static readonly string[] Rows = new[]
    {
        "##########################",  // row 0 — solid lens top
        "##W#W#######..##W#W#######",  // row 1 — glare row 1
        ".##W#W######..###W#W#####.",  // row 2 — glare row 2 (stepped)
        "..##W#W####....###W#W###..",  // row 3 — glare row 3 (more stepped)
        "...#######......#######...",  // row 4 — stepped bottom
    };
}
