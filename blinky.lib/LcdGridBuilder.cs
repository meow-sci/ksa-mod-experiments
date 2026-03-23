using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        // NOTE: TryGet<PartTemplate> is NOT supported by ModLibrary — only Get<PartTemplate> works.
        // Get throws NullReferenceException if the id is unknown, so we catch that.
        PartTemplate? template;
        try
        {
            template = ModLibrary.Get<PartTemplate>(config.EnginePartId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: PartTemplate '{config.EnginePartId}' not found in ModLibrary: {ex.Message}");
            return null;
        }

        // ── Find a fuel-carrying part to connect pixel engines to ─────────────
        // The ResourceManager walks Part.Connections (not the tree hierarchy) to
        // find tanks.  Without a connection from each pixel engine to a part that
        // has Tank modules the resource graph is empty and ResourceAvailable()
        // returns false → engine never fires.
        var fuelPart = FindFuelPart(vehicle);
        if (fuelPart != null)
            Console.WriteLine($"blinky: will connect pixel engines to fuel part '{fuelPart.Id}'");
        else
            Console.WriteLine("blinky: WARNING — no fuel part found; engines may not fire");

        Console.WriteLine($"blinky: building {config.Width}x{config.Height} grid using '{config.EnginePartId}' — {config.TotalParts} total parts");

        var createdParts = new List<Part>(config.TotalParts);

        // ── Batch creation (each Merge triggers its own recompute) ───────────────
        var swCreate = Stopwatch.StartNew();
        for (int row = 0; row < config.Height; row++)
        {
            for (int col = 0; col < config.Width; col++)
            {
                var partA = CreateAndMergePixelPart(vehicle, root, template, row, col, "a", config);
                var partB = CreateAndMergePixelPart(vehicle, root, template, row, col, "b", config);

                if (partA != null)
                {
                    createdParts.Add(partA);
                    if (fuelPart != null) ConnectToFuel(partA, fuelPart);
                }
                if (partB != null)
                {
                    createdParts.Add(partB);
                    if (fuelPart != null) ConnectToFuel(partB, fuelPart);
                }
            }
        }
        swCreate.Stop();
        Console.WriteLine($"blinky: created {createdParts.Count} parts in {swCreate.ElapsedMilliseconds}ms");

        if (createdParts.Count == 0)
        {
            Console.WriteLine("blinky: no parts were successfully created");
            return null;
        }

        // ── Set MinimumThrottle after recompute so EngineControllers are initialized ──
        // Engines can fire even at very low vehicle throttle settings.
        SetMinimumThrottle(createdParts, 0.0001f);

        // ── Recompute after connections established ──────────────────────────────
        // Merge() already calls RecomputeAllDerivedData, but we also need it after
        // Part.Connection.Connect() so the ResourceManager graph picks up the new
        // connections to fuel tanks.
        vehicle.Parts.RecomputeAllDerivedData();

        // ── Scan vehicle to build PixelGrid ──────────────────────────────────────
        var pixelGrid = PixelGrid.ScanFromVehicle(vehicle);
        if (pixelGrid.Count == 0)
            Console.WriteLine("blinky: WARNING — PixelGrid scan found 0 pixel pairs after creation");
        // The initial scan may run before engine modules are fully initialized in the vehicle's
        // state lists (Modules.Get<EngineController>() on individual parts can return empty
        // immediately after Merge). RefreshEngineControllers re-queries the already-located
        // parts so the cached Engines dictionary is populated for animation use.
        pixelGrid.RefreshEngineControllers();
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

        foreach (var part in grid.OwnedParts)
        {
            try
            {
                // Disconnect resource connections before splitting
                foreach (var conn in part.Connections.ToArray())
                {
                    try { conn.Disconnect(); } catch { }
                }
                vehicle.Parts.Split(part);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"blinky: error splitting part '{part.Id}': {ex.Message}");
            }
        }
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

    /// <summary>
    /// Finds the first part on the vehicle that has Tank modules (fuel/oxidizer).
    /// The pixel engines need a Part.Connection to a tank-carrying part so the
    /// ResourceManager graph can discover propellant.
    /// </summary>
    private static Part? FindFuelPart(Vehicle vehicle)
    {
        foreach (var part in vehicle.Parts.Parts)
        {
            var tanks = part.SubtreeModules.Get<Tank>();
            if (tanks.Length > 0 && !part.IsSubPart)
                return part;
        }
        // Fallback: accept sub-parts too
        foreach (var part in vehicle.Parts.Parts)
        {
            var tanks = part.SubtreeModules.Get<Tank>();
            if (tanks.Length > 0)
                return part;
        }
        return null;
    }

    /// <summary>
    /// Creates a Part.Connection between a pixel engine part and the fuel part.
    /// This lets ResourceManager.PopulateGraph() discover the fuel tanks.
    /// </summary>
    private static void ConnectToFuel(Part pixelPart, Part fuelPart)
    {
        try
        {
            bool connected = Part.Connection.Connect(pixelPart, fuelPart);
            if (!connected)
                Console.WriteLine($"blinky: Connection.Connect returned false for '{pixelPart.Id}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: error connecting '{pixelPart.Id}' to fuel: {ex.Message}");
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
