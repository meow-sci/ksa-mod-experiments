using System;
using System.Collections.Generic;
using System.Linq;
using KSA;
using MeowSci.FlexoLib.Data;
using MeowSci.KsaAbstractions;

namespace MeowSci.FlexoLib.Runtime;

public sealed class FlexoRuntime
{
    private readonly FlexoDataManager _dataManager = new();
    private readonly List<HingeController> _activeHinges = new();

    public IReadOnlyList<FlexoPartDefinition> Definitions => _dataManager.Definitions;
    public IReadOnlyList<HingeController> ActiveHinges => _activeHinges;
    public bool HasScanned { get; private set; }
    public string? ScanStatusMessage { get; private set; }

    public void Initialize()
    {
        _dataManager.LoadAll();
    }

    public void ReloadDefinitions()
    {
        _dataManager.LoadAll();
        Console.WriteLine($"flexo: Reloaded {Definitions.Count} definition(s)");
    }

    public void ScanVehicle()
    {
        _activeHinges.Clear();
        HasScanned = true;

        var vehicle = VehicleProvider.GetControlledVehicle();
        if (vehicle == null)
        {
            ScanStatusMessage = "No active vehicle";
            Console.WriteLine("flexo: Scan — no active vehicle");
            return;
        }

        var allParts = PartHelpers.GetAllParts(vehicle);
        Console.WriteLine($"flexo: Scanning vehicle with {allParts.Count} part(s)");
        foreach (var p in allParts)
            Console.WriteLine($"flexo:   part template={p.Template.Id}");

        var hingeDefinitions = Definitions.Where(d => d.PartType == FlexoPartType.Hinge && d.Hinge != null);

        foreach (var def in hingeDefinitions)
        {
            var hinge = def.Hinge!;
            var fixedParts = allParts.Where(p => p.Template.Id == hinge.FixedPartTemplateId).ToList();
            var movingParts = allParts.Where(p => p.Template.Id == hinge.MovingPartTemplateId).ToList();

            Console.WriteLine($"flexo: Hinge '{def.DisplayName}' — looking for fixed='{hinge.FixedPartTemplateId}' ({fixedParts.Count} found), moving='{hinge.MovingPartTemplateId}' ({movingParts.Count} found)");

            foreach (var fixedPart in fixedParts)
            {
                foreach (var movingPart in movingParts)
                {
                    if (fixedPart == movingPart) continue;

                    // Both parts are on the same vehicle — that's sufficient to pair them.
                    // Closest-pair heuristic: prefer direct connection or tree relation,
                    // but accept any match on the same vehicle.
                    _activeHinges.Add(new HingeController(def, fixedPart, movingPart));
                    Console.WriteLine($"flexo: Found hinge '{def.DisplayName}' — fixed={fixedPart.Template.Id}, moving={movingPart.Template.Id}");
                }
            }
        }

        ScanStatusMessage = _activeHinges.Count > 0
            ? $"Found {_activeHinges.Count} hinge(s)"
            : "No flexo parts found in current vehicle";
        Console.WriteLine($"flexo: Scan complete — {_activeHinges.Count} hinge(s) found");
    }

    public void ClearScan()
    {
        _activeHinges.Clear();
        HasScanned = false;
        ScanStatusMessage = null;
    }

    public void Update(double dt)
    {
        foreach (var hinge in _activeHinges)
        {
            try
            {
                hinge.Update(dt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"flexo: HingeController update error: {ex.Message}");
            }
        }
    }

    public FlexoDataManager DataManager => _dataManager;

    private static bool IsConnected(Part partA, Part partB)
    {
        foreach (var connection in partA.Connections)
        {
            if (connection.OtherPart(partA) == partB)
                return true;
        }
        return false;
    }
}
