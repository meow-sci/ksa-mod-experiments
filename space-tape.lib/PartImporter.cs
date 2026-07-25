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
        var template = TryGetPartTemplate(partId, "part");
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
        int skippedSubParts = 0;
        foreach (var subPartInstance in template.SubPartInstances)
        {
            try
            {
                string instanceId = subPartInstance.Id ?? "";
                string subPartTemplateId = subPartInstance.InstanceOf ?? "";
                if (string.IsNullOrWhiteSpace(subPartTemplateId))
                {
                    Console.WriteLine($"space-tape: skipped imported SubPart '{instanceId}' from '{partId}' because InstanceOf is empty");
                    skippedSubParts++;
                    continue;
                }

                if (TryGetPartTemplate(subPartTemplateId, $"SubPart '{instanceId}'") == null)
                {
                    skippedSubParts++;
                    continue;
                }

                var placement = new SubPartPlacement
                {
                    InstanceId = string.IsNullOrWhiteSpace(instanceId) ? CreateFallbackInstanceId(subPartTemplateId, part.Placements.Count) : instanceId,
                    SubPartTemplateId = subPartTemplateId,
                };
                if (subPartInstance.Transform != null)
                {
                    placement.Position = subPartInstance.Transform.PositionValue;
                    placement.Rotation = subPartInstance.Transform.RotationValue;
                    placement.Scale = subPartInstance.Transform.ScaleValue;
                }
                part.Placements.Add(placement);
            }
            catch (Exception ex)
            {
                skippedSubParts++;
                Console.WriteLine($"space-tape: skipped imported SubPart '{subPartInstance.Id}' from '{partId}': {ex.Message}");
            }
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

        // Tanks — as of KSA 2026.7.9.5018 tanks are no longer a single PartTemplate.Tank
        // field; they are Tank.TemplateData entries in the generic Components list.
        foreach (var component in template.Components)
        {
            if (component is Tank.TemplateData tankData && tankData.Tank != null)
                gd.Tanks.Add(ImportTank(tankData.Tank));
        }

        // Connectors
        foreach (var c in template.Connectors)
            gd.Connectors.Add(ImportConnector(c));

        // Batteries — JoulesReference.KWh is a float field; if NaN, fallback to _value / 3600000
        foreach (var b in template.Batteries)
        {
            double kwh = !double.IsNaN(b.MaximumCapacity.KWh)
                ? (double)b.MaximumCapacity.KWh
                : (double)(float)b.MaximumCapacity / 3600000.0;
            gd.Batteries.Add(new BatteryState { CapacityKWh = kwh });
        }

        // Generators — JoulesReference.W is a float field; if NaN, fallback to _value
        foreach (var g in template.Generators)
        {
            double watts = !double.IsNaN(g.Produced.W)
                ? (double)g.Produced.W
                : (double)(float)g.Produced;
            gd.Generators.Add(new GeneratorState { OutputWatts = watts });
        }

        // PowerConsumers
        foreach (var pc in template.PowerConsumers)
        {
            double watts = !double.IsNaN(pc.Consumed.W)
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
                PushoffImpulseNs = template.DockingPort.PushoffImpulse.GetNewtonSeconds(),
            };
        }

        // EVADoor — marker only, no ConnectorId in template
        if (template.EVADoor != null)
            gd.EVADoor = new EVADoorState();

        Console.WriteLine($"space-tape: Imported '{partId}' — {part.Placements.Count} SubParts, {gd.Connectors.Count} Connectors, {skippedSubParts} skipped");
        return part;
    }

    private static PartTemplate? TryGetPartTemplate(string templateId, string context)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        try
        {
            return ModLibrary.Get<PartTemplate>(templateId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"space-tape: template lookup failed for {context} '{templateId}': {ex.Message}");
            return null;
        }
    }

    private static string CreateFallbackInstanceId(string templateId, int placementCount)
    {
        int lastDot = templateId.LastIndexOf('.');
        string baseName = lastDot >= 0 && lastDot < templateId.Length - 1
            ? templateId[(lastDot + 1)..]
            : templateId;
        return $"{baseName}_{placementCount + 1}";
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
