using MeowSci.KsaLights;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Brutal.Numerics;
using KSA;

namespace MeowSci.ItsSoShinyLib;

public static class ShinyGridBuilder
{
    public static ShinyBuiltGrid? BuildGrid(Vehicle vehicle, string gridName, ShinyGridConfig config)
    {
        if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
        if (config == null) throw new ArgumentNullException(nameof(config));

        var root = vehicle.Parts.Root;
        if (root == null)
        {
            Console.WriteLine("its-so-shiny: vehicle has no root part; cannot build grid");
            return null;
        }

        PartTemplate template;
        try
        {
            template = ModLibrary.Get<PartTemplate>(config.LightPartId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"its-so-shiny: PartTemplate '{config.LightPartId}' not found: {ex.Message}");
            return null;
        }

        Console.WriteLine($"its-so-shiny: building {config.Width}x{config.Height} {config.Layout} grid using '{config.LightPartId}' — {config.TotalParts} total parts");
        var swTotal = Stopwatch.StartNew();
        var timings = new List<(string Label, long Ms)>();
        var sw = Stopwatch.StartNew();

        // Find every battery anchor on the vehicle BEFORE creating new parts so that
        // round-robin partitioning across all available batteries is maximised. With K
        // distinct battery anchors the per-PowerConsumer DFS in PowerManager.PopulateGraph
        // and CreateOrders only sees ~N/K consumers, taking total cost from O(N^3) to
        // ~O(N^3/K^2). Enumerating Modules.Get<Battery>() picks up batteries that live
        // on sub-parts too, not just top-level parts.
        var batteryParts = FindBatteryParts(vehicle);
        timings.Add(($"FindBatteryParts ({batteryParts.Count} found)", sw.ElapsedMilliseconds));
        if (batteryParts.Count > 0)
            Console.WriteLine($"its-so-shiny: found {batteryParts.Count} battery anchor(s) for partitioned connections: {string.Join(", ", batteryParts.Select(p => p.Id))}");
        else
            Console.WriteLine("its-so-shiny: WARNING - no battery parts found; light switches may not receive power");

        var createdParts = new List<Part>(config.TotalParts);

        sw.Restart();
        for (int row = 0; row < config.Height; row++)
        {
            for (int col = 0; col < config.Width; col++)
            {
                var part = CreateLightPartInstance(template, gridName, row, col, config);
                if (part != null)
                    createdParts.Add(part);
            }
        }
        timings.Add(($"Part instantiation ({createdParts.Count} parts)", sw.ElapsedMilliseconds));

        if (createdParts.Count == 0)
        {
            Console.WriteLine("its-so-shiny: no light parts were created");
            return null;
        }

        sw.Restart();
        foreach (var part in createdParts)
        {
            part.TreeParent = root;
            root.TreeChildren.Add(part);
        }
        timings.Add(($"Tree wiring ({createdParts.Count} parts)", sw.ElapsedMilliseconds));

        if (batteryParts.Count > 0)
        {
            sw.Restart();
            for (int i = 0; i < createdParts.Count; i++)
            {
                var batteryPart = batteryParts[i % batteryParts.Count];
                createdParts[i].SetStage(batteryPart.Stage);
                ConnectToPower(createdParts[i], batteryPart);
            }
            timings.Add(($"Power connections + stage align ({createdParts.Count} parts, {batteryParts.Count} anchors)", sw.ElapsedMilliseconds));
        }

        sw.Restart();
        vehicle.Parts = PartTree.CreateFromNewPartTree(root);
        timings.Add(("PartTree.CreateFromNewPartTree", sw.ElapsedMilliseconds));

        sw.Restart();
        vehicle.UpdateVehicleConfiguration();
        timings.Add(("UpdateVehicleConfiguration", sw.ElapsedMilliseconds));

        sw.Restart();
        var grid = ShinyPixelGrid.CreateFromParts(createdParts, gridName);
        TurnOffLights(grid);
        timings.Add(($"CreateFromParts + TurnOffLights ({grid.Count} pixels)", sw.ElapsedMilliseconds));

        swTotal.Stop();
        Console.WriteLine($"its-so-shiny: BuildGrid timing ({config.Width}x{config.Height}, {createdParts.Count} parts, total={swTotal.ElapsedMilliseconds}ms):");
        foreach (var (label, ms) in timings)
            Console.WriteLine($"its-so-shiny:   {label,-55} {ms,6}ms");

        return new ShinyBuiltGrid(grid, createdParts);
    }

    public static void DestroyGrid(Vehicle vehicle, ShinyBuiltGrid grid)
    {
        if (vehicle == null || grid == null) return;

        var partsToRemove = new List<Part>();
        if (grid.IsOwned)
            partsToRemove.AddRange(grid.OwnedParts);
        else
            return;

        if (partsToRemove.Count == 0) return;
        JobSystems.VehicleSolver?.Wait();

        foreach (var cell in grid.Grid.Cells.Values)
            cell.SetEnabled(false, 0f);

        foreach (var part in partsToRemove)
        {
            foreach (var connection in part.Connections.ToArray())
            {
                try { connection.Disconnect(); }
                catch { }
            }
        }

        foreach (var part in partsToRemove)
        {
            var parent = part.TreeParent;
            if (parent == null) continue;
            parent.TreeChildren.Remove(part);
            part.TreeParent = null;
        }

        var root = vehicle.Parts.Root;
        vehicle.Parts = PartTree.CreateFromNewPartTree(root);
        vehicle.UpdateVehicleConfiguration();
        foreach (var part in partsToRemove) part.Dispose();
        Console.WriteLine($"its-so-shiny: destroyed grid parts ({partsToRemove.Count} removed)");
    }

    private static Part? CreateLightPartInstance(PartTemplate template, string gridName, int row, int col, ShinyGridConfig config)
    {
        try
        {
            string partId = ShinyPixelGrid.MakeCellId(gridName, row, col);
            var part = new Part(partId, template);

            double px;
            double py;
            double pz;
            double rotY;

            if (config.Layout == ShinyGridLayout.Cylinder)
            {
                double radius = (config.Width * config.Spacing) / (2.0 * Math.PI);
                double theta = col * 2.0 * Math.PI / config.Width;
                px = config.OffsetX + radius * Math.Sin(theta);
                py = config.OffsetY - row * config.Spacing;
                pz = config.OffsetZ + radius * Math.Cos(theta);
                rotY = theta;
            }
            else
            {
                px = config.OffsetX + col * config.Spacing;
                py = config.OffsetY - row * config.Spacing;
                pz = config.OffsetZ;
                rotY = 0.0;
            }

            part.PositionParentAsmb = new double3(px, py, pz);
            double halfAngle = rotY / 2.0;
            var qY    = new doubleQuat(0, Math.Sin(halfAngle), 0, Math.Cos(halfAngle));
            var qXNeg = new doubleQuat(-Math.Sin(Math.PI / 4.0), 0, 0, Math.Cos(Math.PI / 4.0));
            part.Asmb2ParentAsmb = doubleQuat.Concatenate(qY, qXNeg);
            part.Scale = new double3(config.PartScale, config.PartScale, config.PartScale);
            return part;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"its-so-shiny: error creating shiny_{gridName}_{row}_{col}: {ex.Message}");
            return null;
        }
    }

    // Collects every distinct top-level Part on the vehicle that contains at least one
    // Battery module (directly or via a sub-part). Enumerating Modules.Get<Battery>() and
    // resolving each module's owner to its FullPart is the most thorough way to discover
    // battery anchors — it picks up batteries on sub-parts that a top-level-only walk
    // would miss, and it deduplicates parts that contain multiple battery modules.
    private static List<Part> FindBatteryParts(Vehicle vehicle)
    {
        var result = new List<Part>();
        var seen = new HashSet<Part>();
        var batteries = vehicle.Parts.Modules.Get<Battery>();
        for (int i = 0; i < batteries.Length; i++)
        {
            var battery = batteries[i];
            if (battery?.Parent == null) continue;
            var anchor = battery.Parent.FullPart;
            if (anchor != null && seen.Add(anchor))
                result.Add(anchor);
        }
        return result;
    }

    private static void ConnectToPower(Part lightPart, Part batteryPart)
    {
        try
        {
            if (!Part.Connection.Connect(lightPart, batteryPart))
                Console.WriteLine($"its-so-shiny: Connection.Connect returned false for '{lightPart.Id}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"its-so-shiny: error connecting '{lightPart.Id}' to power: {ex.Message}");
        }
    }

    private static void TurnOffLights(ShinyPixelGrid grid)
    {
        foreach (var cell in grid.Cells.Values)
        {
            var lightSwitch = cell.LightPart.LightSwitch ?? cell.LightPart.FullPart.LightSwitch;
            if (lightSwitch != null)
                lightSwitch.LightIsActive = false;
        }
    }
}