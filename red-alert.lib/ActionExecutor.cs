using System;
using System.Collections.Generic;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.RedAlertLib;

/// <summary>Executes planned actions against the live game state.</summary>
public static class ActionExecutor
{
    /// <summary>Runs every action in the plan in order, swallowing per-action errors.</summary>
    public static void Execute(ActionPlan plan)
    {
        foreach (var action in plan.Actions)
        {
            try { Execute(action); }
            catch (Exception ex)
            {
                Console.WriteLine($"red-alert: action error ({action.Type} on {action.PartId}): {ex.Message}");
            }
        }
    }

    public static void Execute(PlannedAction action)
    {
        var part = ResolvePart(action.VehicleId, action.PartId);
        if (part == null)
        {
            Console.WriteLine($"red-alert: skipped — part '{action.PartId}' not found on vehicle '{action.VehicleId}'");
            return;
        }

        switch (action.Type)
        {
            case ActionType.LightOn:
                LightActions.SetEnabled(part, true);
                break;
            case ActionType.LightOff:
                LightActions.SetEnabled(part, false);
                break;
            case ActionType.LightToggle:
                var current = LightActions.GetEnabled(part) ?? false;
                LightActions.SetEnabled(part, !current);
                break;
            case ActionType.LightColor:
                LightActions.ApplyColor(part, action.Color);
                break;
            case ActionType.LightActuate:
                SetActuate(part, action.Actuate);
                break;
            case ActionType.SolarPanelDeploy:
                SetActuate(part, 1f);
                break;
            case ActionType.SolarPanelRetract:
                SetActuate(part, 0f);
                break;
            case ActionType.SolarPanelToggle:
                ToggleDeploy(part);
                break;
            case ActionType.SolarPanelActuate:
                SetActuate(part, action.Actuate);
                break;
        }
    }

    private static Part? ResolvePart(string vehicleId, string partId)
    {
        foreach (var v in VehicleProvider.GetAllVehicles())
        {
            if (v.Id != vehicleId) continue;
            foreach (var p in PartHelpers.GetAllParts(v))
                if (p.Id == partId) return p;
        }
        return null;
    }

    private static KeyframeAnimationModule? FindAnimModule(Part part)
    {
        var owner = part.FullPart ?? part;
        var span = owner.SubtreeModules.Get<KeyframeAnimationModule>();
        return span.Length > 0 ? span[0] : null;
    }

    private static void SetActuate(Part part, float t)
    {
        var anim = FindAnimModule(part);
        if (anim == null) return;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        anim.TimeGoal = t * anim.Shared.Duration;
    }

    private static void ToggleDeploy(Part part)
    {
        var anim = FindAnimModule(part);
        if (anim == null) return;
        // If we're currently closer to retracted, deploy. Otherwise retract.
        bool isMostlyDeployed = anim.TimeGoal >= anim.Shared.Duration * 0.5f;
        anim.TimeGoal = isMostlyDeployed ? 0f : anim.Shared.Duration;
    }
}
