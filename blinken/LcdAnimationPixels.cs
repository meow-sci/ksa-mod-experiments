using System.Collections.Generic;
using System.Linq;

namespace mod;

/// <summary>
/// Contains the pixel data for the LCD scrolling animation.
/// Each entry is an (x, y) pair representing an "on" pixel in the source image.
/// Replace the sample data with real pixel coordinates as needed.
/// </summary>
public static class LcdAnimationPixels
{
  /// <summary>
  /// Sparse pixel data: array of (x, y) positions that are "on".
  /// x = horizontal position in the source image (column).
  /// y = vertical position in the source image (row).
  /// </summary>
  public static readonly (int x, int y)[] Pixels = GenerateHI();

  /// <summary>
  /// Generates a 53-pixel-tall "HI" with 9px stroke width.
  /// H = 27px wide (left bar + crossbar + right bar), 9px gap, I = 17px wide (serif).
  /// Total: 53px wide × 53px tall.
  /// </summary>
  private static (int x, int y)[] GenerateHI()
  {
    var pixels = new HashSet<(int x, int y)>();
    const int h = 53; // height
    const int s = 9;  // stroke width

    void Rect(int x0, int y0, int x1, int y1)
    {
      for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
          pixels.Add((x, y));
    }

    // ── H (width = 3×s = 27) ──
    Rect(0, 0, s - 1, h - 1);                                 // left vertical
    Rect(s, (h - s) / 2, 2 * s - 1, (h - s) / 2 + s - 1);   // crossbar
    Rect(2 * s, 0, 3 * s - 1, h - 1);                         // right vertical

    // ── Gap (s pixels) ──

    // ── I (serif style, width = 2×s - 1 = 17) ──
    int ix = 4 * s;          // x-start of I = 36
    int iw = 2 * s - 1;      // serif width  = 17

    Rect(ix, 0, ix + iw - 1, s - 1);                                          // top serif
    Rect(ix + (iw - s) / 2, 0, ix + (iw - s) / 2 + s - 1, h - 1);           // stem (centered)
    Rect(ix, h - s, ix + iw - 1, h - 1);                                      // bottom serif

    return pixels.ToArray();
  }
}
