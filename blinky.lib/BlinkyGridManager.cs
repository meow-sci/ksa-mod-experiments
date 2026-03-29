using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Per-vehicle grid state managed by <see cref="BlinkyGridManager"/>.
/// </summary>
public class GridState
{
    public string VehicleId { get; }
    public string GridName { get; }
    public Vehicle Vehicle { get; }
    public BlinkyPixelGrid BlinkyGrid { get; }
    public ScrollAnimation Scroll { get; } = new();

    /// <summary>
    /// Tracks which pixels are currently "on" for intelligent static display diffing.
    /// </summary>
    internal HashSet<(int row, int col)> ActivePixels { get; } = new();

    public GridState(string vehicleId, string gridName, Vehicle vehicle, BlinkyPixelGrid grid)
    {
        VehicleId = vehicleId;
        GridName = gridName;
        Vehicle = vehicle;
        BlinkyGrid = grid;
    }
}

/// <summary>
/// Static singleton that manages per-vehicle LCD grids and exposes scroll, static display, and off operations.
/// Shared between the blinky mod UI and the unladen-swallow RPC endpoints.
/// </summary>
public static class BlinkyGridManager
{
    private static readonly Dictionary<string, GridState> _grids = new();

    /// <summary>All currently registered grid states, keyed by vehicle ID.</summary>
    public static IReadOnlyDictionary<string, GridState> Grids => _grids;

    // ── Registration ─────────────────────────────────────────────────────────

    /// <summary>Registers a grid for the given vehicle. Replaces any existing grid for that vehicle.</summary>
    public static GridState Register(Vehicle vehicle, BlinkyPixelGrid grid)
    {
        var id = vehicle.Id;
        if (_grids.ContainsKey(id))
            Console.WriteLine($"blinky: replacing existing grid for vehicle '{id}'");

        var state = new GridState(id, vehicle, grid);
        _grids[id] = state;
        Console.WriteLine($"blinky: registered grid for vehicle '{id}' ({grid.Grid.Cols}x{grid.Grid.Rows})");
        return state;
    }

    /// <summary>Unregisters the grid for the given vehicle ID. Stops any running scroll.</summary>
    public static void Unregister(string vehicleId)
    {
        if (_grids.TryGetValue(vehicleId, out var state))
        {
            state.Scroll.Stop();
            _grids.Remove(vehicleId);
            Console.WriteLine($"blinky: unregistered grid for vehicle '{vehicleId}'");
        }
    }

    /// <summary>Gets the grid state for a vehicle, or null if not registered.</summary>
    public static GridState? Get(string vehicleId)
    {
        _grids.TryGetValue(vehicleId, out var state);
        return state;
    }

    /// <summary>Clears all registered grids.</summary>
    public static void Clear()
    {
        foreach (var state in _grids.Values)
            state.Scroll.Stop();
        _grids.Clear();
    }

    // ── Scroll ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a scrolling animation on the vehicle's grid with the supplied pixel data.
    /// Stops any existing scroll first and turns off all pixels before starting.
    /// </summary>
    /// <param name="vehicleId">Vehicle to apply scroll to.</param>
    /// <param name="pixels">Sparse pixel data: (x,y) positions that are "on" in the source image.</param>
    /// <param name="speed">Scroll speed in pixels/second.</param>
    /// <returns>True if scroll was started, false if vehicle not found.</returns>
    public static bool StartScroll(string vehicleId, (int x, int y)[] pixels, float speed)
    {
        var state = Get(vehicleId);
        if (state == null) return false;

        // Stop existing scroll and clear pixels
        state.Scroll.Stop();
        TurnOffAllPixels(state);

        state.Scroll.Start(state.BlinkyGrid.Grid, pixels, speed);
        return true;
    }

    /// <summary>
    /// Starts a scrolling animation using the built-in default pixel data.
    /// </summary>
    public static bool StartBuiltInScroll(string vehicleId, float speed)
    {
        var state = Get(vehicleId);
        if (state == null) return false;

        state.Scroll.Stop();
        TurnOffAllPixels(state);

        state.Scroll.StartBuiltIn(state.BlinkyGrid.Grid, speed);
        return true;
    }

    /// <summary>Stops any running scroll on the vehicle.</summary>
    public static bool StopScroll(string vehicleId)
    {
        var state = Get(vehicleId);
        if (state == null) return false;

        state.Scroll.Stop();
        return true;
    }

    // ── Static Display ───────────────────────────────────────────────────────

    /// <summary>
    /// Displays a static set of pixels on the vehicle's grid.
    /// </summary>
    /// <param name="vehicleId">Vehicle to apply to.</param>
    /// <param name="pixels">List of (x, y) pixel coordinates to turn on (0-based col, row).</param>
    /// <param name="reset">
    /// If true, intelligently diffs: turns off pixels that were on but aren't in the new set,
    /// and turns on pixels in the new set that weren't already on.
    /// If false, additively turns on the specified pixels without clearing others.
    /// </param>
    /// <returns>True if applied, false if vehicle not found.</returns>
    public static bool DisplayStatic(string vehicleId, (int x, int y)[] pixels, bool reset)
    {
        var state = Get(vehicleId);
        if (state == null) return false;

        // Stop any running scroll
        state.Scroll.Stop();

        var grid = state.BlinkyGrid.Grid;
        var newPixels = new HashSet<(int row, int col)>();
        foreach (var (x, y) in pixels)
            newPixels.Add((y, x)); // (x=col, y=row) → (row, col) for grid lookup

        if (reset)
        {
            // Intelligent diff: only change what needs changing
            // Turn off pixels that were on but aren't in the new set
            foreach (var pos in state.ActivePixels)
            {
                if (!newPixels.Contains(pos))
                    SetPixel(grid, pos.row, pos.col, false);
            }

            // Turn on pixels in the new set that weren't already on
            foreach (var pos in newPixels)
            {
                if (!state.ActivePixels.Contains(pos))
                    SetPixel(grid, pos.row, pos.col, true);
            }

            state.ActivePixels.Clear();
            foreach (var pos in newPixels)
                state.ActivePixels.Add(pos);
        }
        else
        {
            // Additive: just turn on the specified pixels
            foreach (var pos in newPixels)
            {
                SetPixel(grid, pos.row, pos.col, true);
                state.ActivePixels.Add(pos);
            }
        }

        return true;
    }

    // ── Off ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Turns off all pixels on the vehicle's grid and stops any running scroll.
    /// </summary>
    /// <returns>True if turned off, false if vehicle not found.</returns>
    public static bool TurnOff(string vehicleId)
    {
        var state = Get(vehicleId);
        if (state == null) return false;

        state.Scroll.Stop();
        TurnOffAllPixels(state);
        return true;
    }

    // ── Pattern Application ──────────────────────────────────────────────────

    /// <summary>Applies a pattern function to all pixels on the vehicle's grid.</summary>
    public static bool ApplyPattern(string vehicleId, Func<(int row, int col), bool> selector)
    {
        var state = Get(vehicleId);
        if (state == null) return false;

        state.Scroll.Stop();
        state.ActivePixels.Clear();

        foreach (var (key, controllers) in state.BlinkyGrid.Grid.Engines)
        {
            bool on = selector(key);
            for (int i = 0; i < controllers.Length; i++)
                controllers[i].SetIsActive(null, on);
            if (on)
                state.ActivePixels.Add(key);
        }

        return true;
    }

    // ── Tick ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calls Update on all active scroll animations. Call this every frame from the mod.
    /// </summary>
    public static void TickAll(double dt)
    {
        foreach (var state in _grids.Values)
        {
            if (state.Scroll.IsActive && state.BlinkyGrid.Grid.Cols > 0)
                state.Scroll.Update(dt);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void SetPixel(PixelGrid grid, int row, int col, bool on)
    {
        if (grid.Engines.TryGetValue((row, col), out var controllers))
        {
            for (int i = 0; i < controllers.Length; i++)
                controllers[i].SetIsActive(null, on);
        }
    }

    private static void TurnOffAllPixels(GridState state)
    {
        foreach (var (_, controllers) in state.BlinkyGrid.Grid.Engines)
        {
            for (int i = 0; i < controllers.Length; i++)
                controllers[i].SetIsActive(null, false);
        }
        state.ActivePixels.Clear();
    }
}
