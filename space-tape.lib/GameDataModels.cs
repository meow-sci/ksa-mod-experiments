using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

/// <summary>Tank shape type.</summary>
public enum TankShape { Cylindrical, Spherical }

/// <summary>Fuel tank definition state.</summary>
public sealed class TankState
{
    public TankShape Shape { get; set; } = TankShape.Cylindrical;
    public string WallMaterialId { get; set; } = "Aluminum.2014";
    public double LengthM { get; set; } = 2.0;
    public double OuterRadiusM { get; set; } = 0.5;
    public double WallThicknessMm { get; set; } = 2.0;

    public TankState Clone() => new()
    {
        Shape = Shape,
        WallMaterialId = WallMaterialId,
        LengthM = LengthM,
        OuterRadiusM = OuterRadiusM,
        WallThicknessMm = WallThicknessMm,
    };
}

/// <summary>Connector attachment point state.</summary>
public sealed class ConnectorState
{
    public string Id { get; set; } = "";
    public double3 Position { get; set; } = double3.Zero;
    public doubleQuat Rotation { get; set; } = doubleQuat.Identity;
    public double3 Scale { get; set; } = double3.One;
    public bool FlagInternal { get; set; }
    public bool FlagToSurface { get; set; }
    public bool FlagFromSurface { get; set; }

    public ConnectorState Clone() => new()
    {
        Id = Id,
        Position = Position,
        Rotation = Rotation,
        Scale = Scale,
        FlagInternal = FlagInternal,
        FlagToSurface = FlagToSurface,
        FlagFromSurface = FlagFromSurface
    };
}

/// <summary>Decoupler state.</summary>
public sealed class DecouplerState
{
    public string ConnectorId { get; set; } = "";
    public double Force { get; set; } = 500.0;
    public DecouplerState Clone() => new() { ConnectorId = ConnectorId, Force = Force };
}

/// <summary>Docking port state.</summary>
public sealed class DockingPortState
{
    public string ConnectorId { get; set; } = "";
    public double Force { get; set; } = 500.0;
    public DockingPortState Clone() => new() { ConnectorId = ConnectorId, Force = Force };
}

/// <summary>EVA door state.</summary>
public sealed class EVADoorState
{
    public string ConnectorId { get; set; } = "";
    public EVADoorState Clone() => new() { ConnectorId = ConnectorId };
}

/// <summary>Battery state (multiple allowed per part).</summary>
public sealed class BatteryState
{
    public double CapacityKWh { get; set; } = 0.01;
    public BatteryState Clone() => new() { CapacityKWh = CapacityKWh };
}

/// <summary>Generator state (multiple allowed per part).</summary>
public sealed class GeneratorState
{
    public double OutputWatts { get; set; } = 5.0;
    public GeneratorState Clone() => new() { OutputWatts = OutputWatts };
}

/// <summary>Power consumer state (multiple allowed per part).</summary>
public sealed class PowerConsumerState
{
    public double ConsumedWatts { get; set; } = 2.0;
    public PowerConsumerState Clone() => new() { ConsumedWatts = ConsumedWatts };
}
