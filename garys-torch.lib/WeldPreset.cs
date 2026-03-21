using Brutal.Numerics;

namespace MeowSci.GarysTorchLib;

/// <summary>Preset weld configuration (position/rotation/scale/lockRotation).</summary>
public struct WeldPreset
{
    public string Name;
    public float3 Position;
    public float3 Rotation;
    public float Scale;
    public bool LockRotation;

    /// <summary>Built-in presets.</summary>
    public static readonly WeldPreset[] Presets = new[]
    {
        new WeldPreset { Name = "Ridin' Dirty 1", Position = new float3(-0.375f, 0f, -1.894f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
        new WeldPreset { Name = "Ridin' Dirty 2", Position = new float3(-1.287f, 0f, -1.894f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
        new WeldPreset { Name = "Ridin' Dirty 3", Position = new float3(-2.215f, 0f, -1.894f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
        new WeldPreset { Name = "Shotgun",         Position = new float3(5.675f,  0.413f, -0.125f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
        new WeldPreset { Name = "Not Shotgun",     Position = new float3(5.675f, -0.413f, -0.125f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
    };
}
