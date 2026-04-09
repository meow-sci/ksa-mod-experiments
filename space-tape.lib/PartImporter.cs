using System;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Deep-reads a PartTemplate from ModLibrary to create an EditingPart
/// pre-populated with all SubParts, Connectors, Tank, Batteries, etc.
/// </summary>
public static class PartImporter
{
    public static EditingPart? ImportFromTemplate(string partId)
    {
        var template = ModLibrary.Get<PartTemplate>(partId);
        if (template == null)
        {
            Console.WriteLine($"space-tape: PartImporter — template not found: {partId}");
            return null;
        }

        var part = new EditingPart
        {
            PartId = partId + ".Custom",
        };

        // SubParts
        foreach (var sp in template.SubPartInstances)
        {
            var placement = new SubPartPlacement
            {
                InstanceId = sp.Id ?? "",
                SubPartTemplateId = sp.InstanceOf ?? "",
            };
            if (sp.Transform != null)
            {
                placement.Position = sp.Transform.PositionValue;
                placement.Rotation = sp.Transform.RotationValue;
                placement.Scale = sp.Transform.ScaleValue;
            }
            part.Placements.Add(placement);
        }

        var gd = part.GameData;
        gd.DisplayName = template.DisplayName ?? partId;

        // EditorTags — EditorTag is a record struct with a Tag field (string)
        foreach (var tag in template.EditorTags)
            gd.EditorTags.Add(tag.Tag);

        // Custom mass — take the first CustomMassTemplate from InertMasses
        foreach (var mass in template.InertMasses)
        {
            if (mass is CustomMassTemplate cm)
            {
                gd.CustomMass = (double)cm.Mass;
                break;
            }
        }

        // Tank
        if (template.Tank != null)
            gd.Tank = ImportTank(template.Tank);

        // Connectors
        foreach (var c in template.Connectors)
            gd.Connectors.Add(ImportConnector(c));

        // Batteries — JoulesReference.KWh is a float field; if NaN, fallback to _value / 3600000
        foreach (var b in template.Batteries)
        {
            double kwh = !float.IsNaN(b.MaximumCapacity.KWh)
                ? (double)b.MaximumCapacity.KWh
                : (double)(float)b.MaximumCapacity / 3600000.0;
            gd.Batteries.Add(new BatteryState { CapacityKWh = kwh });
        }

        // Generators — JoulesReference.W is a float field; if NaN, fallback to _value
        foreach (var g in template.Generators)
        {
            double watts = !float.IsNaN(g.Produced.W)
                ? (double)g.Produced.W
                : (double)(float)g.Produced;
            gd.Generators.Add(new GeneratorState { OutputWatts = watts });
        }

        // PowerConsumers
        foreach (var pc in template.PowerConsumers)
        {
            double watts = !float.IsNaN(pc.Consumed.W)
                ? (double)pc.Consumed.W
                : (double)(float)pc.Consumed;
            gd.PowerConsumers.Add(new PowerConsumerState { ConsumedWatts = watts });
        }

        // Decoupler
        if (template.Decoupler != null)
        {
            gd.Decoupler = new DecouplerState
            {
                ConnectorId = template.Decoupler.ConnectorId,
                Force = template.Decoupler.Force,
            };
        }

        // DockingPort
        if (template.DockingPort != null)
        {
            gd.DockingPort = new DockingPortState
            {
                ConnectorId = template.DockingPort.ConnectorId,
                Force = template.DockingPort.Force,
            };
        }

        // EVADoor — marker only, no ConnectorId in template
        if (template.EVADoor != null)
            gd.EVADoor = new EVADoorState();

        Console.WriteLine($"space-tape: Imported '{partId}' — {part.Placements.Count} SubParts, {gd.Connectors.Count} Connectors");
        return part;
    }

    private static TankState ImportTank(AsmbTankTemplate tank)
    {
        var state = new TankState
        {
            WallMaterialId = tank.Material?.Id ?? "Aluminum.2014(s)",
        };

        if (tank is CylindricalTankTemplate cyl)
        {
            state.Shape = TankShape.Cylindrical;
            state.LengthM = (double)cyl.Length;
            state.OuterRadiusM = (double)cyl.OuterRadius;
            state.WallThicknessMm = (double)cyl.WallThickness * 1000.0;
        }
        else if (tank is SphericalTankTemplate sph)
        {
            state.Shape = TankShape.Spherical;
            state.OuterRadiusM = (double)sph.OuterRadius;
            state.WallThicknessMm = (double)sph.WallThickness * 1000.0;
        }

        return state;
    }

    private static ConnectorState ImportConnector(Part.Connector.TemplateBase c)
    {
        return new ConnectorState
        {
            Id = c.Id,
            Position = c.Transform.PositionValue,
            Rotation = c.Transform.RotationValue,
            Scale = c.Transform.ScaleValue,
            FlagInternal = c.Flags.HasFlag(Part.Connector.Flag.Internal),
            FlagToSurface = c.Flags.HasFlag(Part.Connector.Flag.ToSurface),
            FlagFromSurface = c.Flags.HasFlag(Part.Connector.Flag.FromSurface),
        };
    }
}
