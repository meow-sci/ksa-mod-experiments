using Brutal.Numerics;
using KSA;

namespace MeowSci.GarrysTorchLib;

/// <summary>Represents a single active weld between two vehicles.</summary>
public class WeldEntry
{
    public Vehicle Source = null!;
    public Vehicle Target = null!;
    /// <summary>Offset in target's body frame (metres).</summary>
    public float3 Position;
    /// <summary>Euler pitch/yaw/roll relative to target orientation (degrees).</summary>
    public float3 Rotation;
    /// <summary>Uniform scale factor applied to all source parts.</summary>
    public float Scale = 1f;
    /// <summary>When false, only position is locked; source can rotate freely.</summary>
    public bool LockRotation = true;
}
