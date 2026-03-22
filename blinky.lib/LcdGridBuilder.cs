using System;
using System.Collections.Generic;
using System.Diagnostics;
using Brutal.Numerics;
using KSA;
using MeowSci.BlinkenLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Builds an LCD pixel grid of engine parts on a vehicle at runtime.
/// Each pixel position gets two engine parts (a/b pair) following blinken's naming convention
/// <c>pixel_{row}_{col}_{a|b}</c>, so that <see cref="PixelGrid.ScanFromVehicle"/> can be reused directly.
///
/// All pixel parts are attached as children of the vehicle's root part via <c>PartTree.Merge()</c>.
/// Resource graph recomputation is suppressed during batch creation and called once at the end.
/// </summary>
public static class LcdGridBuilder
{
    /// <summary>
    /// Builds a grid of engine parts on the vehicle and returns a fully initialised
    /// <see cref="BlinkyPixelGrid"/>. Returns null on failure (e.g. unknown part template,
    /// no root part, or all parts failed to merge).
    /// </summary>
    public static BlinkyPixelGrid? BuildGrid(Vehicle vehicle, LcdGridConfig config)
    {
        if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
        if (config == null) throw new ArgumentNullException(nameof(config));

        // ── Find attachment root ─────────────────────────────────────────────────
        var root = vehicle.Parts.Root;
        if (root == null)
        {
            Console.WriteLine("blinky: vehicle has no root part — cannot build grid");
            return null;
        }

        // ── Lookup PartTemplate ──────────────────────────────────────────────────
        if (!ModLibrary.TryGet<PartTemplate>(config.EnginePartId, out var template) || template == null)
        {
            Console.WriteLine($"blinky: PartTemplate '{config.EnginePartId}' not found in ModLibrary");
            return null;
        }

        Console.WriteLine($"blinky: building {config.Width}x{config.Height} grid using '{config.EnginePartId}' — {config.TotalParts} total parts");

        var createdParts = new List<Part>(config.TotalParts);

        // ── Batch creation with suppressed recomputes ────────────────────────────
        var swCreate = Stopwatch.StartNew();
        ResourceGraphSuppressor.Suppress();
        try
        {
            for (int row = 0; row < config.Height; row++)
            {
                for (int col = 0; col < config.Width; col++)
                {
                    var partA = CreateAndMergePixelPart(vehicle, root, template, row, col, "a", config);
                    var partB = CreateAndMergePixelPart(vehicle, root, template, row, col, "b", config);

                    if (partA != null) createdParts.Add(partA);
                    if (partB != null) createdParts.Add(partB);
                }
            }
        }
        finally
        {
            ResourceGraphSuppressor.Unsuppress();
        }
        swCreate.Stop();
        Console.WriteLine($"blinky: created {createdParts.Count} parts in {swCreate.ElapsedMilliseconds}ms");

        if (createdParts.Count == 0)
        {
            Console.WriteLine("blinky: no parts were successfully created");
            return null;
        }

        // ── Set MinimumThrottle before final recompute ───────────────────────────
        // This ensures engines can fire even at very low vehicle throttle settings.
        SetMinimumThrottle(createdParts, 0.0001f);

        // ── Single final recompute ───────────────────────────────────────────────
        var swRecompute = Stopwatch.StartNew();
        vehicle.Parts.RecomputeAllDerivedData();
        swRecompute.Stop();
        Console.WriteLine($"blinky: final RecomputeAllDerivedData took {swRecompute.ElapsedMilliseconds}ms");

        // ── Scan vehicle to build PixelGrid ──────────────────────────────────────
        var pixelGrid = PixelGrid.ScanFromVehicle(vehicle);
        if (pixelGrid.Count == 0)
            Console.WriteLine("blinky: WARNING — PixelGrid scan found 0 pixel pairs after creation");

        return new BlinkyPixelGrid(pixelGrid, createdParts);
    }

    /// <summary>
    /// Removes all dynamically created pixel parts from the vehicle, then recomputes once.
    /// Only operates on owned (blinky-built) grids.
    /// </summary>
    public static void DestroyGrid(Vehicle vehicle, BlinkyPixelGrid grid)
    {
        if (vehicle == null || grid == null || !grid.IsOwned) return;

        Console.WriteLine($"blinky: destroying grid — removing {grid.OwnedParts.Count} parts");

        ResourceGraphSuppressor.Suppress();
        try
        {
            foreach (var part in grid.OwnedParts)
            {
                try
                {
                    vehicle.Parts.Split(part);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"blinky: error splitting part '{part.Id}': {ex.Message}");
                }
            }
        }
        finally
        {
            ResourceGraphSuppressor.Unsuppress();
        }

        vehicle.Parts.RecomputeAllDerivedData();
        Console.WriteLine("blinky: grid destroyed and recomputed");
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a single pixel engine part at the correct grid position and merges it into the vehicle.
    /// </summary>
    private static Part? CreateAndMergePixelPart(
        Vehicle vehicle, Part attachTo, PartTemplate template,
        int row, int col, string slot, LcdGridConfig config)
    {
        try
        {
            string partId = $"pixel_{row}_{col}_{slot}";
            var part = new Part(partId, template);

            // Columns along +X, rows down along -Y, forward along +Z.
            // 'b' is offset 0.05 m in Z to prevent exact overlap with 'a'.
            double px = config.OffsetX + col * config.Spacing;
            double py = config.OffsetY - row * config.Spacing;
            double pz = config.OffsetZ + (slot == "b" ? 0.05 : 0.0);

            part.PositionParentAsmb = new double3(px, py, pz);
            part.Asmb2ParentAsmb = new doubleQuat(0, 0, 0, 1); // identity

            bool merged = vehicle.Parts.Merge(attachTo, part);
            if (!merged)
            {
                Console.WriteLine($"blinky: Merge returned false for part '{partId}'");
                return null;
            }

            return part;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: error creating pixel_{row}_{col}_{slot}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Sets MinimumThrottle on all EngineControllers of the given parts.</summary>
    private static void SetMinimumThrottle(List<Part> parts, float minThrottle)
    {
        int count = 0;
        foreach (var part in parts)
        {
            var controllers = part.SubtreeModules.Get<EngineController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i].MinimumThrottle = minThrottle;
                count++;
            }
        }
        Console.WriteLine($"blinky: set MinimumThrottle={minThrottle} on {count} engine controllers");
    }
}
