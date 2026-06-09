using System.Collections.Generic;
using KSA;

namespace MeowSci.ItsSoShinyLib;

public sealed class ShinyBuiltGrid
{
    public ShinyPixelGrid Grid { get; }
    public IReadOnlyList<Part> OwnedParts { get; }

    public bool IsOwned => OwnedParts.Count > 0;

    public ShinyBuiltGrid(ShinyPixelGrid grid, List<Part> ownedParts)
    {
        Grid = grid;
        OwnedParts = ownedParts;
    }
}