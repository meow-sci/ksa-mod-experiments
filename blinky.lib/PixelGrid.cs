using System;
using System.Collections.Generic;
using System.Linq;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Scans a vehicle for pixel engine part pairs and caches engine controllers for efficient per-frame access.
/// Part naming convention: pixel_{row}_{col}_a / pixel_{row}_{col}_b
/// </summary>
public class PixelGrid
{
    private readonly Dictionary<(int row, int col), (Part a, Part b)> _grid = new();
    private readonly Dictionary<(int row, int col), EngineController[]> _engines = new();

    private PixelGrid() { }

    public int Count => _grid.Count;
    public int Rows { get; private set; }
    public int Cols { get; private set; }

    public IReadOnlyDictionary<(int row, int col), (Part a, Part b)> Grid => _grid;
    public IReadOnlyDictionary<(int row, int col), EngineController[]> Engines => _engines;

    /// <summary>Returns the first cached EngineController for the given grid cell, or null if none found.</summary>
    public EngineController? GetFirstController(int row, int col)
    {
        if (_engines.TryGetValue((row, col), out var engines) && engines.Length > 0)
            return engines[0];
        return null;
    }

    /// <summary>
    /// Re-queries engine controllers from the Part objects already cached in this grid.
    /// Call this after the vehicle has finished merging and recomputing derived data,
    /// if the initial scan captured the parts before their modules were fully initialized.
    /// </summary>
    public void RefreshEngineControllers()
    {
        foreach (var (key, (a, b)) in _grid)
        {
            var list = new List<EngineController>();
            foreach (var part in new[] { a, b })
            {
                var controllers = part.SubtreeModules.Get<EngineController>();
                for (int i = 0; i < controllers.Length; i++)
                    list.Add(controllers[i]);
            }
            _engines[key] = list.ToArray();
        }
        int total = _engines.Values.Sum(e => e.Length);
        Console.WriteLine($"blinky: RefreshEngineControllers — {total} controllers across {_grid.Count} cells");
    }

    /// <summary>Scans all vehicle parts for pixel engine pairs and returns a populated PixelGrid.</summary>
    public static PixelGrid ScanFromVehicle(Vehicle vehicle)
    {
        var result = new PixelGrid();

        var partA = new Dictionary<(int row, int col), Part>();
        var partB = new Dictionary<(int row, int col), Part>();

        foreach (var part in PartHelpers.GetAllParts(vehicle))
        {
            if (!part.Id.StartsWith("pixel_")) continue;

            var segments = part.Id.Split('_');
            if (segments.Length != 4) continue;
            if (!int.TryParse(segments[1], out int row)) continue;
            if (!int.TryParse(segments[2], out int col)) continue;

            var key = (row, col);
            if (segments[3] == "a") partA[key] = part;
            else if (segments[3] == "b") partB[key] = part;
        }

        foreach (var key in partA.Keys)
            if (partB.TryGetValue(key, out var pb))
                result._grid[key] = (partA[key], pb);

        // Cache EngineControllers per pixel — resolved once at scan time, used every frame
        foreach (var (key, (a, b)) in result._grid)
        {
            var list = new List<EngineController>();
            foreach (var part in new[] { a, b })
            {
                var controllers = part.SubtreeModules.Get<EngineController>();
                for (int i = 0; i < controllers.Length; i++)
                    list.Add(controllers[i]);
            }
            result._engines[key] = list.ToArray();
        }

        if (result._grid.Count > 0)
        {
            result.Rows = result._grid.Keys.Max(k => k.row) + 1;
            result.Cols = result._grid.Keys.Max(k => k.col) + 1;
        }

        Console.WriteLine($"blinky: found {result._grid.Count} pixel pairs, cached {result._engines.Values.Sum(e => e.Length)} engine controllers");
        return result;
    }

    /// <summary>
    /// Creates a PixelGrid from pre-built part groups (e.g., recovered from save-loaded parts
    /// that lost their pixel_* IDs but were identified by template and spatial analysis).
    /// </summary>
    public static PixelGrid BuildFromPartGroups(Dictionary<(int row, int col), (Part a, Part b)> partGroups)
    {
        var result = new PixelGrid();

        foreach (var (key, parts) in partGroups)
            result._grid[key] = parts;

        foreach (var (key, (a, b)) in result._grid)
        {
            var list = new List<EngineController>();
            foreach (var part in new[] { a, b })
            {
                var controllers = part.SubtreeModules.Get<EngineController>();
                for (int i = 0; i < controllers.Length; i++)
                    list.Add(controllers[i]);
            }
            result._engines[key] = list.ToArray();
        }

        if (result._grid.Count > 0)
        {
            result.Rows = result._grid.Keys.Max(k => k.row) + 1;
            result.Cols = result._grid.Keys.Max(k => k.col) + 1;
        }

        int total = result._engines.Values.Sum(e => e.Length);
        Console.WriteLine($"blinky: BuildFromPartGroups — {result._grid.Count} cells, {total} controllers, {result.Cols}x{result.Rows} grid");
        return result;
    }
}
