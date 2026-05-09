using System.Collections.Generic;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.RedAlertLib;

/// <summary>Scans a vehicle's part tree to find parts that support red-alert actions.</summary>
public static class ActionScanner
{
    /// <summary>Returns all parts on the vehicle that have at least one supported capability.</summary>
    public static List<ActionablePart> Scan(Vehicle vehicle)
    {
        var result = new List<ActionablePart>();
        var seen = new HashSet<string>();

        foreach (var part in PartHelpers.GetAllParts(vehicle))
        {
            if (part.Template == null) continue;
            if (seen.Contains(part.Id)) continue;

            var caps = DetectCapabilities(part);
            if (caps == PartCapability.None) continue;

            seen.Add(part.Id);
            result.Add(new ActionablePart
            {
                VehicleId = vehicle.Id,
                PartId = part.Id,
                DisplayName = part.DisplayName ?? part.Id,
                TemplateId = part.Template.Id ?? "",
                Capabilities = caps,
            });
        }
        return result;
    }

    /// <summary>Inspects a single part for supported red-alert capabilities.</summary>
    public static PartCapability DetectCapabilities(Part part)
    {
        var caps = PartCapability.None;
        if (part.Template == null) return caps;

        bool hasLights = HasLightModule(part.Template);
        if (hasLights)
        {
            caps |= PartCapability.LightColor;
            var ls = part.LightSwitch ?? part.FullPart?.LightSwitch;
            if (ls != null) caps |= PartCapability.LightOnOff;
        }

        var owner = part.FullPart ?? part;
        var animSpan = owner.SubtreeModules.Get<KeyframeAnimationModule>();
        bool hasAnim = animSpan.Length > 0;
        bool showsDeployRetract = hasAnim && animSpan[0].ShowDeployRetract;

        bool isSolarPanel = owner.SubtreeModules.Get<SolarPanel>().Length > 0;

        if (isSolarPanel && hasAnim)
        {
            if (showsDeployRetract) caps |= PartCapability.SolarDeployRetract;
            else caps |= PartCapability.SolarActuate;
        }

        if (hasLights && hasAnim && !showsDeployRetract)
            caps |= PartCapability.LightActuate;

        return caps;
    }

    private static bool HasLightModule(PartTemplate template)
    {
        var comps = ReflectionHelpers.GetFieldValue(template, "Components") as System.Collections.IList;
        if (comps == null) return false;
        for (int i = 0; i < comps.Count; i++)
        {
            var c = comps[i];
            if (c?.GetType().FullName == "KSA.LightModule+TemplateData") return true;
        }
        return false;
    }
}
