using System.Collections.Generic;
namespace MeowSci.Unscience.Contracts;

/// <summary>Reclaims only contiguous released tails; never moves offsets still held by a consumer.</summary>
public sealed class ReleasedRanges
{
    private readonly Dictionary<uint, uint> _byEnd = new();
    public void Add(uint start, uint size) { if (size != 0) _byEnd[checked(start + size)] = start; }
    public uint Trim(uint cursor, uint floor)
    {
        while (_byEnd.TryGetValue(cursor, out uint start) && start >= floor)
        { _byEnd.Remove(cursor); cursor = start; }
        return cursor;
    }
    public ulong Bytes { get { ulong bytes = 0; foreach (var range in _byEnd) bytes += range.Key - range.Value; return bytes; } }
}
