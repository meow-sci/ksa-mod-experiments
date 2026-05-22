namespace MeowSci.ThugLifeLib;

/// <summary>
/// Static pixel pattern for the thug-life sunglasses meme, encoded as a 15x4 grid.
///
/// Character legend:
///   '.' = fully transparent
///   '#' = black opaque (RGBA = 0, 0, 0, 255)
///   'W' = white opaque (RGBA = 255, 255, 255, 255) — sunflare/glare highlight
///
/// Layout: left lens (cols 0-6) + transparent bridge (col 7) + right lens (cols 8-14).
/// Stepped top/bottom edges and double white highlights mimic the iconic meme.
/// </summary>
public static class ThugLifeTexturePattern
{
    public const int Width = 15;
    public const int Height = 4;

    public static readonly string[] Rows = new[]
    {
        ".##.##...##.##.",  // row 0 — top notches
        "#######.#######",  // row 1 — solid lens top
        "#W.W###.#W.W###",  // row 2 — body with white glare highlights
        ".######.######.",  // row 3 — bottom rounded
    };
}
