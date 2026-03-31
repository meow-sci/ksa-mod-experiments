using Brutal.Numerics;

namespace MeowSci.GarysTorchLib;

/// <summary>Preset weld configuration (position/rotation/scale/lockRotation).</summary>
public struct WeldPreset
{
    public float3 Position;
    public float3 Rotation;
    public float Scale;
    public bool LockRotation;
}
