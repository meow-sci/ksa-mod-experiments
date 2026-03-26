using System;
using KSA;

namespace MeowSci.GlassLib;

/// <summary>
/// Controls camera field-of-view overrides for the glass mod.
/// State is set from any thread; <see cref="ApplyFov"/> must be called on the game thread.
/// </summary>
public static class FovController
{
    public const float MinFov = 1f;
    public const float MaxFov = 179f;
    public const float DefaultFov = 50f;

    public static bool IsOverrideActive { get; set; } = false;
    public static float OverrideFovDegrees { get; set; } = DefaultFov;

    /// <summary>Sets the FOV override to <paramref name="degrees"/>, clamping to [MinFov, MaxFov].</summary>
    public static void SetFov(float degrees)
    {
        OverrideFovDegrees = MathF.Max(MinFov, MathF.Min(MaxFov, degrees));
        IsOverrideActive = true;
    }

    /// <summary>Resets the override FOV to the default (50°) and keeps override active.</summary>
    public static void ResetToDefault()
    {
        OverrideFovDegrees = DefaultFov;
        IsOverrideActive = true;
    }

    /// <summary>Disables the FOV override, returning control to the game.</summary>
    public static void DisableOverride()
    {
        IsOverrideActive = false;
    }

    /// <summary>Returns the current camera FOV in degrees (reads the live camera state; must be called on the game thread).</summary>
    public static float GetCurrentFovDegrees()
    {
        float currentFovRad = Program.GetCamera().GetFieldOfView();
        return currentFovRad * (180f / MathF.PI);
    }

    /// <summary>
    /// Applies the current override FOV to the camera.
    /// Must be called on the game thread (e.g. in OnAfterUi).
    /// Does nothing if override is not active.
    /// </summary>
    public static void ApplyFov()
    {
        if (!IsOverrideActive) return;
        float clampedFov = MathF.Max(MinFov, MathF.Min(MaxFov, OverrideFovDegrees));
        Program.GetCamera().SetFieldOfView(clampedFov);
    }
}
