using System.Collections.Generic;
using KSA;
using MeowSci.BlinkenLib;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Wraps a <see cref="PixelGrid"/> with ownership semantics for dynamically created engine parts.
/// When <see cref="IsOwned"/> is true, call <see cref="LcdGridBuilder.DestroyGrid"/> to remove
/// the parts from the vehicle when done.
/// </summary>
public class BlinkyPixelGrid
{
    /// <summary>The underlying pixel grid scanned from the vehicle after grid creation.</summary>
    public PixelGrid Grid { get; }

    /// <summary>
    /// All Part objects that were dynamically created and added. Empty for externally-scanned grids.
    /// </summary>
    public IReadOnlyList<Part> OwnedParts { get; }

    /// <summary>True when this grid owns dynamically created parts that can be destroyed.</summary>
    public bool IsOwned => OwnedParts.Count > 0;

    /// <summary>
    /// Creates a BlinkyPixelGrid.
    /// </summary>
    /// <param name="grid">The scanned pixel grid.</param>
    /// <param name="ownedParts">Parts created by blinky (pass empty list for borrowed/scanned grids).</param>
    public BlinkyPixelGrid(PixelGrid grid, List<Part> ownedParts)
    {
        Grid = grid;
        OwnedParts = ownedParts;
    }
}
