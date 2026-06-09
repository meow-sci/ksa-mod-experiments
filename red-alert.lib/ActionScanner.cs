using System.Collections.Generic;
using KSA;

namespace MeowSci.RedAlertLib;

/// <summary>
/// Scans a vehicle's top-level parts to find those that support red-alert actions.
/// Capabilities are aggregated across each top-level part's full subpart subtree —
/// e.g. a `LightSmallA` is one user-facing entry even though its `LightModule` lives on
/// an inner `Subpart_SpotlightA`. This keeps the part picker at the right granularity.
/// </summary>
public static class ActionScanner
{
    public static List<ActionablePart> Scan(Vehicle vehicle)
    {
        var result = new List<ActionablePart>();
        foreach (var part in vehicle.Parts.Parts)
        {
            if (part.Template == null) continue;
            var caps = DetectCapabilities(part);
            if (caps == PartCapability.None) continue;
            result.Add(new ActionablePart
            {
                VehicleId = vehicle.Id,
                PartInstanceId = part.InstanceId,
                PartId = part.Id,
                DisplayName = part.DisplayName ?? part.Id,
                TemplateId = part.Template.Id ?? "",
                Capabilities = caps,
            });
        }
        return result;
    }

    /// <summary>Inspects a top-level part (and its subpart subtree) for supported capabilities.</summary>
    public static PartCapability DetectCapabilities(Part part)
    {
        var caps = PartCapability.None;
        if (part.Template == null) return caps;

        bool hasLights = SubtreeHasLightModule(part);
        if (hasLights)
        {
            caps |= PartCapability.LightColor;
            if (part.LightSwitch != null) caps |= PartCapability.LightOnOff;
        }

        var animSpan = part.SubtreeModules.Get<KeyframeAnimationModule>();
        bool hasAnim = animSpan.Length > 0;
        bool showsDeployRetract = hasAnim && animSpan[0].ShowDeployRetract;
        bool isSolarPanel = part.SubtreeModules.Get<SolarPanel>().Length > 0;

        if (isSolarPanel && hasAnim)
        {
            if (showsDeployRetract) caps |= PartCapability.SolarDeployRetract;
            else caps |= PartCapability.SolarActuate;
        }

        if (hasLights && hasAnim && !showsDeployRetract)
            caps |= PartCapability.LightActuate;

        return caps;
    }

    /// <summary>True if `part` or any subpart in its subtree carries a runtime `LightModule`.</summary>
    public static bool SubtreeHasLightModule(Part part)
    {
        if (part.Modules.Get<LightModule>().Length > 0) return true;
        foreach (var sub in part.SubParts)
            if (SubtreeHasLightModule(sub)) return true;
        return false;
    }
}
