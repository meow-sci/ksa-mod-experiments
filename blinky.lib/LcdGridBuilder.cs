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
/// All pixel parts are attached as children of the vehicle's root part via manual
/// <c>TreeParent</c>/<c>TreeChildren</c> assignment.  The <c>PartTree</c> is rebuilt once
/// at the end with <c>PartTree.CreateFromNewPartTree()</c>, avoiding the per-part
/// <c>RecomputeAllDerivedData()</c> cost that <c>PartTree.Merge()</c> would trigger.
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

        var swTotal = Stopwatch.StartNew();
        var timings = new List<(string Label, long Ms)>();

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
        var sw = Stopwatch.StartNew();
        try
        {
            template = ModLibrary.Get<PartTemplate>(config.EnginePartId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: PartTemplate '{config.EnginePartId}' not found in ModLibrary: {ex.Message}");
            return null;
        }
        timings.Add(("ModLibrary.Get<PartTemplate>", sw.ElapsedMilliseconds));

        // ── Find all fuel-carrying parts to use as connection anchors ──────────────
        // PERFORMANCE: With N pixel engines all connected to 1 fuel part, each
        // ResourceManager's PopulateGraph DFS traverses all N+1 nodes using
        // O(N) List.Contains visited checks → O(N²) per engine → O(N³) total.
        // By distributing engines across K fuel parts (round-robin), each DFS
        // only sees N/K nodes → O(N/K)² per engine → O(N³/K²) total.
        // With K=4 fuel parts on a 1444-engine grid: 3B → ~188M ops (~16× faster).
        sw.Restart();
        var fuelParts = FindAllFuelParts(vehicle);
        timings.Add(($"FindAllFuelParts ({fuelParts.Count} found)", sw.ElapsedMilliseconds));
        if (fuelParts.Count > 0)
            Console.WriteLine($"blinky: found {fuelParts.Count} fuel part(s) for partitioned connections: {string.Join(", ", fuelParts.Select(p => p.Id))}");
        else
            Console.WriteLine("blinky: WARNING — no fuel parts found; engines may not fire");

        Console.WriteLine($"blinky: building {config.Width}x{config.Height} grid using '{config.EnginePartId}' — {config.TotalParts} total parts");

        var createdParts = new List<Part>(config.TotalParts);

        // ── Part instantiation — new Part() + position/rotation/scale ───────────
        sw.Restart();
        for (int row = 0; row < config.Height; row++)
        {
            for (int col = 0; col < config.Width; col++)
            {
                var partA = CreatePixelPartInstance(template, row, col, "a", config);
                var partB = CreatePixelPartInstance(template, row, col, "b", config);
                if (partA != null) createdParts.Add(partA);
                if (partB != null) createdParts.Add(partB);
            }
        }
        timings.Add(($"Part instantiation ({createdParts.Count} parts)", sw.ElapsedMilliseconds));

        if (createdParts.Count == 0)
        {
            Console.WriteLine("blinky: no parts were successfully created");
            return null;
        }

        // ── Tree wiring — TreeParent / TreeChildren ──────────────────────────────
        sw.Restart();
        foreach (var part in createdParts)
        {
            part.TreeParent = root;
            root.TreeChildren.Add(part);
        }
        timings.Add(($"Tree wiring ({createdParts.Count} parts)", sw.ElapsedMilliseconds));

        // ── Fuel connections — round-robin across all fuel parts ─────────────────
        // Each engine is assigned to exactly one fuel part (by index mod K).
        // This creates K isolated subgraphs of ~N/K engines each, reducing the
        // O(N³) PopulateGraph cost to O(N³/K²).
        if (fuelParts.Count > 0)
        {
            sw.Restart();
            for (int i = 0; i < createdParts.Count; i++)
                ConnectToFuel(createdParts[i], fuelParts[i % fuelParts.Count]);
            timings.Add(($"Fuel connections ({createdParts.Count} parts, {fuelParts.Count} anchors)", sw.ElapsedMilliseconds));
        }

        // ── PartTree rebuild — CreateFromNewPartTree ─────────────────────────────
        // Walks full TreeChildren hierarchy from root, registers all modules/states,
        // and calls RecomputeAllDerivedData exactly once.
        sw.Restart();
        vehicle.Parts = PartTree.CreateFromNewPartTree(root);
        timings.Add(("PartTree.CreateFromNewPartTree", sw.ElapsedMilliseconds));

        // ── UpdateAfterPartTreeModification — resync FlightComputer ─────────────
        // Rebuilds FlightComputer.VehicleConfig (Gimbals, Engines, etc.) from the
        // new part tree.  Without this the flight computer holds stale GimbalController
        // references and crashes with an index-out-of-range in UpdateTvcParams.
        sw.Restart();
        vehicle.UpdateAfterPartTreeModification();
        timings.Add(("UpdateAfterPartTreeModification", sw.ElapsedMilliseconds));

        // ── SetMinimumThrottle — iterate EngineControllers ──────────────────────
        sw.Restart();
        SetMinimumThrottle(createdParts, 0.0001f);
        timings.Add(("SetMinimumThrottle", sw.ElapsedMilliseconds));

        // ── PixelGrid.ScanFromVehicle ────────────────────────────────────────────
        sw.Restart();
        var pixelGrid = PixelGrid.ScanFromVehicle(vehicle);
        timings.Add(($"PixelGrid.ScanFromVehicle ({pixelGrid.Count} pairs)", sw.ElapsedMilliseconds));
        if (pixelGrid.Count == 0)
            Console.WriteLine("blinky: WARNING — PixelGrid scan found 0 pixel pairs after creation");

        // ── RefreshEngineControllers ─────────────────────────────────────────────
        sw.Restart();
        pixelGrid.RefreshEngineControllers();
        timings.Add(("RefreshEngineControllers", sw.ElapsedMilliseconds));

        // ── Print timing summary ─────────────────────────────────────────────────
        swTotal.Stop();
        Console.WriteLine($"blinky: BuildGrid timing ({config.Width}x{config.Height}, {createdParts.Count} parts, total={swTotal.ElapsedMilliseconds}ms):");
        foreach (var (label, ms) in timings)
            Console.WriteLine($"blinky:   {label,-45} {ms,6}ms");

        return new BlinkyPixelGrid(pixelGrid, createdParts);
    }

    /// <summary>
    /// Removes all dynamically created pixel parts from the vehicle.
    /// Uses manual tree unlinking + single <c>CreateFromNewPartTree</c> rebuild,
    /// mirroring the BuildGrid approach to avoid N per-part <c>RecomputeAllDerivedData</c> calls.
    /// Only operates on owned (blinky-built) grids.
    /// </summary>
    public static void DestroyGrid(Vehicle vehicle, BlinkyPixelGrid grid)
    {
        if (vehicle == null || grid == null || !grid.IsOwned) return;

        int partCount = grid.OwnedParts.Count;
        Console.WriteLine($"blinky: destroying grid — removing {partCount} parts");
        var swTotal = Stopwatch.StartNew();
        var timings = new List<(string Label, long Ms)>();
        var sw = Stopwatch.StartNew();

        // ── Disconnect fuel connections ──────────────────────────────────────────
        foreach (var part in grid.OwnedParts)
        {
            foreach (var conn in part.Connections.ToArray())
            {
                try { conn.Disconnect(); } catch { }
            }
        }
        timings.Add(($"Disconnect fuel connections ({partCount} parts)", sw.ElapsedMilliseconds));

        // ── Manually unlink all pixel parts from the tree ────────────────────────
        // Mirrors BuildGrid: set TreeParent=null and remove from parent's TreeChildren
        // without calling Split (which triggers RecomputeAllDerivedData per part).
        sw.Restart();
        foreach (var part in grid.OwnedParts)
        {
            var parent = part.TreeParent;
            if (parent != null)
            {
                parent.TreeChildren.Remove(part);
                part.TreeParent = null;
            }
        }
        timings.Add(($"Tree unlink ({partCount} parts)", sw.ElapsedMilliseconds));

        // ── Rebuild vehicle PartTree once from the now-clean root ────────────────
        // CreateFromNewPartTree walks only the remaining (non-pixel) tree children,
        // so the pixel parts are simply gone, and recompute runs exactly once.
        var root = vehicle.Parts.Root;
        sw.Restart();
        vehicle.Parts = PartTree.CreateFromNewPartTree(root);
        timings.Add(("PartTree.CreateFromNewPartTree", sw.ElapsedMilliseconds));

        // ── UpdateAfterPartTreeModification — resync FlightComputer ─────────────
        // Same reason as BuildGrid: rebuilds FlightComputer.VehicleConfig so it no
        // longer references the now-removed pixel engine GimbalControllers.
        sw.Restart();
        vehicle.UpdateAfterPartTreeModification();
        timings.Add(("UpdateAfterPartTreeModification", sw.ElapsedMilliseconds));

        swTotal.Stop();
        Console.WriteLine($"blinky: DestroyGrid timing ({partCount} parts, total={swTotal.ElapsedMilliseconds}ms):");
        foreach (var (label, ms) in timings)
            Console.WriteLine($"blinky:   {label,-45} {ms,6}ms");
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and configures a single pixel engine part (position, rotation, scale).
    /// Does NOT wire it into any tree — caller handles <c>TreeParent</c>/<c>TreeChildren</c>.
    /// </summary>
    private static Part? CreatePixelPartInstance(
        PartTemplate template, int row, int col, string slot, LcdGridConfig config)
    {
        try
        {
            string partId = $"pixel_{row}_{col}_{slot}";
            var part = new Part(partId, template);

            // Columns along +X, rows down along -Y.
            // Both 'a' and 'b' occupy the same position — they oppose each other via rotation.
            double px = config.OffsetX + col * config.Spacing;
            double py = config.OffsetY - row * config.Spacing;
            double pz = config.OffsetZ;

            part.PositionParentAsmb = new double3(px, py, pz);

            // Mirror blinken's XML convention: pixel_*_a rotates Y=-90°, pixel_*_b rotates Y=+90°.
            // This places the two nozzles firing in exactly opposite horizontal directions,
            // cancelling all net thrust while both engine glows remain visible.
            // Quaternion for rotation around Y by θ: (0, sin(θ/2), 0, cos(θ/2))
            const double s = 0.7071067811865476; // sin/cos of ±45° = 1/√2
            part.Asmb2ParentAsmb = slot == "b"
                ? new doubleQuat(0,  s, 0, s)   // Y = +90°
                : new doubleQuat(0, -s, 0, s);  // Y = -90°

            // Scale down to match blinken's convention (blinken XML uses Scale=0.1).
            part.Scale = new double3(config.PartScale, config.PartScale, config.PartScale);

            return part;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: error creating pixel_{row}_{col}_{slot}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Collects ALL parts on the vehicle that carry Tank modules (fuel/oxidizer),
    /// preferring non-sub-parts. Used for round-robin partitioned fuel connections
    /// to reduce ResourceManager.PopulateGraph cost from O(N³) to O(N³/K²).
    /// </summary>
    private static List<Part> FindAllFuelParts(Vehicle vehicle)
    {
        var result = new List<Part>();
        foreach (var part in vehicle.Parts.Parts)
        {
            if (part.IsSubPart) continue;
            var tanks = part.SubtreeModules.Get<Tank>();
            if (tanks.Length > 0)
                result.Add(part);
        }
        // Fallback: accept sub-parts if nothing found at top level
        if (result.Count == 0)
        {
            foreach (var part in vehicle.Parts.Parts)
            {
                var tanks = part.SubtreeModules.Get<Tank>();
                if (tanks.Length > 0)
                    result.Add(part);
            }
        }
        return result;
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
