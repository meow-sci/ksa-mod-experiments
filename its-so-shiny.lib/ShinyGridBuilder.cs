using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ItsSoShinyLib;

public static class ShinyGridBuilder
{
    public static ShinyBuiltGrid? BuildGrid(Vehicle vehicle, string gridName, ShinyGridConfig config, float3 color, float intensity)
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

        Console.WriteLine($"its-so-shiny: building {config.Width}x{config.Height} {config.Layout} grid using '{config.LightPartId}'");
        var sw = Stopwatch.StartNew();
        var createdParts = new List<Part>(config.TotalParts);

        for (int row = 0; row < config.Height; row++)
        {
            for (int col = 0; col < config.Width; col++)
            {
                var part = CreateLightPartInstance(template, gridName, row, col, config);
                if (part != null)
                    createdParts.Add(part);
            }
        }

        if (createdParts.Count == 0)
        {
            Console.WriteLine("its-so-shiny: no light parts were created");
            return null;
        }

        foreach (var part in createdParts)
        {
            part.TreeParent = root;
            root.TreeChildren.Add(part);
        }

        var batteryParts = FindBatteryParts(vehicle);
        if (batteryParts.Count > 0)
        {
            for (int i = 0; i < createdParts.Count; i++)
            {
                var batteryPart = batteryParts[i % batteryParts.Count];
                createdParts[i].SetStage(batteryPart.Stage);
                ConnectToPower(createdParts[i], batteryPart);
            }
            Console.WriteLine($"its-so-shiny: connected {createdParts.Count} light parts to {batteryParts.Count} battery anchor(s)");
        }
        else
        {
            Console.WriteLine("its-so-shiny: WARNING - no battery parts found; light switches may not receive power");
        }

        vehicle.Parts = PartTree.CreateFromNewPartTree(root);
        vehicle.UpdateVehicleConfiguration();

        var grid = ShinyPixelGrid.ScanFromVehicle(vehicle, gridName);
        foreach (var cell in grid.Cells.Values)
        {
            cell.ApplyAppearance(color, intensity);
            cell.SetEnabled(false, intensity);
        }

        sw.Stop();
        Console.WriteLine($"its-so-shiny: built grid '{gridName}' with {grid.Count} pixels in {sw.ElapsedMilliseconds}ms");
        return new ShinyBuiltGrid(grid, createdParts);
    }

    public static void DestroyGrid(Vehicle vehicle, ShinyBuiltGrid grid)
    {
        if (vehicle == null || grid == null) return;

        var partsToRemove = new List<Part>();
        if (grid.IsOwned)
            partsToRemove.AddRange(grid.OwnedParts);
        else
            partsToRemove.AddRange(grid.Grid.Cells.Values.Select(c => c.HostPart));

        if (partsToRemove.Count == 0) return;

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

    private static List<Part> FindBatteryParts(Vehicle vehicle)
    {
        var result = new List<Part>();
        foreach (var part in vehicle.Parts.Parts)
        {
            if (part.IsSubPart) continue;
            if (part.SubtreeModules.Get<Battery>().Length > 0)
                result.Add(part);
        }

        if (result.Count == 0)
        {
            foreach (var part in PartHelpers.GetAllParts(vehicle))
                if (part.SubtreeModules.Get<Battery>().Length > 0)
                    result.Add(part);
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
}