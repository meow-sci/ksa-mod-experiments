using System;
using Brutal.Numerics;

namespace MeowSci.RedAlertLib;

/// <summary>Discrete action that can be performed on a part.</summary>
public enum ActionType
{
    LightOff,
    LightOn,
    LightToggle,
    LightColor,
    LightActuate,
    SolarPanelDeploy,
    SolarPanelRetract,
    SolarPanelToggle,
    SolarPanelActuate,
}

/// <summary>Capabilities discovered for a part on a vehicle.</summary>
[Flags]
public enum PartCapability
{
    None = 0,
    LightOnOff = 1 << 0,
    LightColor = 1 << 1,
    LightActuate = 1 << 2,
    SolarDeployRetract = 1 << 3,
    SolarActuate = 1 << 4,
}

/// <summary>A part on a vehicle that supports one or more red-alert actions.</summary>
public sealed class ActionablePart
{
    public required string VehicleId;
    /// <summary>Runtime-unique identifier for this Part instance (Part.InstanceId).
    /// Use this — not <see cref="PartId"/> — to address a specific instance.</summary>
    public required uint PartInstanceId;
    /// <summary>Persisted Part.Id string. May collide between instances of the same template.
    /// Display only; never use as a key.</summary>
    public required string PartId;
    public required string DisplayName;
    public required string TemplateId;
    public required PartCapability Capabilities;

    public bool Supports(ActionType type) => type switch
    {
        ActionType.LightOff or ActionType.LightOn or ActionType.LightToggle => Capabilities.HasFlag(PartCapability.LightOnOff),
        ActionType.LightColor => Capabilities.HasFlag(PartCapability.LightColor),
        ActionType.LightActuate => Capabilities.HasFlag(PartCapability.LightActuate),
        ActionType.SolarPanelDeploy or ActionType.SolarPanelRetract or ActionType.SolarPanelToggle => Capabilities.HasFlag(PartCapability.SolarDeployRetract),
        ActionType.SolarPanelActuate => Capabilities.HasFlag(PartCapability.SolarActuate),
        _ => false,
    };
}

/// <summary>One action queued in an action plan.</summary>
public sealed class PlannedAction
{
    public required string VehicleId;
    /// <summary>Runtime-unique Part.InstanceId — addresses a specific Part instance.</summary>
    public required uint PartInstanceId;
    /// <summary>Persisted Part.Id (may collide across instances). Display only.</summary>
    public required string PartId;
    public required string PartDisplayName;
    public required ActionType Type;

    /// <summary>Color for LightColor actions (0..1 RGB). Ignored otherwise.</summary>
    public float3 Color = new(1f, 1f, 1f);

    /// <summary>Actuate value 0..1 for LightActuate / SolarPanelActuate. Ignored otherwise.</summary>
    public float Actuate = 0.5f;
}

/// <summary>A named collection of actions that are executed together when engaged.</summary>
public sealed class ActionPlan
{
    public string Name = "";
    public readonly System.Collections.Generic.List<PlannedAction> Actions = new();
}
