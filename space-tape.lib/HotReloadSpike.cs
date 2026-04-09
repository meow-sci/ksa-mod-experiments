using System;
using System.Reflection;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Experimental spike: attempts to register a newly created PartTemplate into the
/// game's ModLibrary at runtime, without a game restart.
///
/// This is a HUMAN-IN-THE-LOOP feature. Use the "Test Hot-Reload" button in the editor,
/// then check whether the new part appears in the vehicle editor's part catalog.
/// </summary>
public static class HotReloadSpike
{
    // ModLibrary.AllParts is internal; access via reflection for read-back verification.
    private static readonly FieldInfo? AllPartsField =
        typeof(ModLibrary).GetField("AllParts",
            BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>
    /// Tries to register the given <paramref name="editingPart"/> as a live
    /// <see cref="PartTemplate"/> in <see cref="ModLibrary"/>.
    /// </summary>
    public static (bool success, string message) TryRegisterPart(EditingPart editingPart)
    {
        if (string.IsNullOrWhiteSpace(editingPart.PartId))
            return (false, "Part ID is empty. Set a Part ID before testing hot-reload.");

        if (editingPart.Placements.Count == 0)
            return (false, "Part has no SubParts. Add at least one SubPart before testing hot-reload.");

        try
        {
            var template = BuildTemplate(editingPart);

            // OnDataLoad populates EditorTags, resolves refs, and calls ModLibrary.Register
            // when IsReferenceable && !_isGameData.
            template.OnDataLoad(Mod.Empty);

            bool inLibrary = VerifyRegistration(editingPart.PartId);

            if (inLibrary)
            {
                Console.WriteLine($"space-tape: Hot-reload: '{editingPart.PartId}' registered and verified.");
                return (true, $"'{editingPart.PartId}' registered in ModLibrary. Check the vehicle editor part catalog!");
            }
            else
            {
                // OnDataLoad may have failed silently — try direct registration as fallback.
                bool ok = ModLibrary.Register(template);
                Console.WriteLine($"space-tape: Hot-reload fallback register: {ok} for '{editingPart.PartId}'");
                return ok
                    ? (true, $"'{editingPart.PartId}' registered via fallback. Check the vehicle editor part catalog!")
                    : (false, $"Registration returned false — ID '{editingPart.PartId}' may already exist or the game rejected it.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"space-tape: Hot-reload exception for '{editingPart.PartId}': {ex}");
            return (false, $"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks whether the given Part ID is present in ModLibrary's AllParts collection.
    /// Uses reflection since AllParts is internal.
    /// </summary>
    public static bool VerifyRegistration(string partId)
    {
        try
        {
            if (AllPartsField?.GetValue(null) is not SerializedCollection<PartTemplate> allParts)
                return false;
            return allParts.Find(KeyHash.Make(partId.AsSpan())) != null;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------

    private static PartTemplate BuildTemplate(EditingPart editingPart)
    {
        var gd = editingPart.GameData;

        var template = new PartTemplate
        {
            Id = editingPart.PartId,
            DisplayName = string.IsNullOrWhiteSpace(gd.DisplayName)
                ? editingPart.PartId
                : gd.DisplayName
        };

        // Add SubPart instances
        foreach (var placement in editingPart.Placements)
        {
            var instance = new PartInstance
            {
                Id = placement.InstanceId,
                InstanceOf = placement.SubPartTemplateId,
                Transform = new TransformReference
                {
                    PositionValue = placement.Position,
                    RotationValue = placement.Rotation,
                    ScaleValue = placement.Scale
                }
            };
            template.SubPartInstances.Add(instance);
        }

        // Add editor tags as StringReferences
        foreach (var tag in gd.EditorTags)
            template.EditorTagsStrings.Add(new StringReference { Value = tag });

        // Custom mass (kg)
        if (gd.CustomMass.HasValue && gd.CustomMass.Value > 0)
        {
            template.InertMasses.Add(new CustomMassTemplate
            {
                Mass = new MassReference(gd.CustomMass.Value)
            });
        }

        // Tank
        if (gd.Tank != null)
        {
            if (gd.Tank.Shape == TankShape.Cylindrical)
            {
                template.Tank = new CylindricalTankTemplate
                {
                    Length = new DistanceReference(gd.Tank.LengthM),
                    OuterRadius = new DistanceReference(gd.Tank.OuterRadiusM),
                    WallThickness = new DistanceReference(gd.Tank.WallThicknessMm / 1000.0),
                };
            }
            else
            {
                template.Tank = new SphericalTankTemplate
                {
                    OuterRadius = new DistanceReference(gd.Tank.OuterRadiusM),
                    WallThickness = new DistanceReference(gd.Tank.WallThicknessMm / 1000.0),
                };
            }
            if (!string.IsNullOrWhiteSpace(gd.Tank.WallMaterialId))
                template.Tank.Material = new SerializedReference(gd.Tank.WallMaterialId);
        }

        // Connectors
        foreach (var c in gd.Connectors)
        {
            var flags = (Part.Connector.Flag)0;
            if (c.FlagInternal) flags |= Part.Connector.Flag.Internal;
            if (c.FlagToSurface) flags |= Part.Connector.Flag.ToSurface;
            if (c.FlagFromSurface) flags |= Part.Connector.Flag.FromSurface;

            template.Connectors.Add(new Part.Connector.TemplateBase
            {
                Id = c.Id,
                Transform = new TransformReference
                {
                    PositionValue = c.Position,
                    RotationValue = c.Rotation,
                    ScaleValue = c.Scale,
                },
                Flags = flags,
            });
        }

        // Batteries
        foreach (var b in gd.Batteries)
        {
            var jr = new JoulesReference { KWh = (float)b.CapacityKWh };
            jr.OnDataLoad(Mod.Empty);
            template.Batteries.Add(new BatteryTemplate { MaximumCapacity = jr });
        }

        // Generators
        foreach (var g in gd.Generators)
        {
            var jr = new JoulesReference { W = (float)g.OutputWatts };
            jr.OnDataLoad(Mod.Empty);
            template.Generators.Add(new GeneratorTemplate { Produced = jr });
        }

        // PowerConsumers
        foreach (var pc in gd.PowerConsumers)
        {
            var jr = new JoulesReference { W = (float)pc.ConsumedWatts };
            jr.OnDataLoad(Mod.Empty);
            template.PowerConsumers.Add(new PowerConsumerTemplate { Consumed = jr });
        }

        // Decoupler
        if (gd.Decoupler != null)
        {
            template.Decoupler = new DecouplerTemplate
            {
                ConnectorId = gd.Decoupler.ConnectorId,
                Force = (float)gd.Decoupler.Force,
            };
        }

        // DockingPort
        if (gd.DockingPort != null)
        {
            template.DockingPort = new DockingPortTemplate
            {
                ConnectorId = gd.DockingPort.ConnectorId,
                Force = (float)gd.DockingPort.Force,
            };
        }

        // EVADoor (marker only — no settable properties)
        if (gd.EVADoor != null)
            template.EVADoor = new EVADoorTemplate();

        return template;
    }
}
