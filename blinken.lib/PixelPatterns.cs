using System;

namespace MeowSci.BlinkenLib;

public static class PixelPatterns
{
    public static bool AllOn((int row, int col) pos) => true;
    public static bool Checkerboard((int row, int col) pos) => (pos.row + pos.col) % 2 == 0;
    public static bool AlternatingRows((int row, int col) pos) => pos.row % 2 == 0;
    public static bool AlternatingCols((int row, int col) pos) => pos.col % 2 == 0;
}
