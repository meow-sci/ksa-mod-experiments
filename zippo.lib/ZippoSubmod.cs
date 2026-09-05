using MeowSci.KsaLights;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ZippoLib;

public sealed partial class ZippoSubmod : IWorkspaceFeature
{
    public static ZippoSubmod? Instance { get; private set; }

    public string Name => "Zippo - Lights!";
    public string Tooltip => "Light appearance, queued transitions, and Disco party-light cycles.";

    private float _intensity = 1.0f;
    private bool _lightEnabled = true;
    // 0 = Default, 1..4 = named presets (offset -1 into LightController.ColorPresetNames), 5 = (Custom)
    private float4 _currentColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private readonly LightAnimationManager _animationManager = new();

    // ── Animation UI state ────────────────────────────────────────────────────
    private float4 _animStartColor4 = new(1f, 1f, 1f, 1f);
    private float4 _animEndColor4 = new(1f, 1f, 1f, 1f);
    private float _animStartIntensity = 1.0f;
    private float _animEndIntensity = 1.0f;
    private float _animDuration = 2.0f;
    private int _animEasingIdx = 3; // EaseInOut
    private float _animPowerStart = 3.0f;
    private float _animPowerEnd = 3.0f;
    private readonly ImInputString _animStartColorFilter = new(128);
    private string? _animQueueError;

    public void Initialize() { Instance = this; }
    public void Update(double dt) { _animationManager.Update(dt, ResolveManagedPart); UpdateDisco(dt); }

    /// <summary>Builds the color preset combo items, appending "(Custom)" only when a custom color is active.</summary>

    // Programmatic light operations use the same runtime registry as the UI.

    /// <summary>Returns light part info for all light parts on a vehicle, or null if vehicle not found.</summary>
    public List<LightPartInfo>? GetLightPartInfos(string vehicleId)
    {
        var vehicle = VehicleProvider.GetAllVehicles().Find(v => v.Id == vehicleId);
        if (vehicle == null) return null;

        var parts = LightController.GetLightParts(vehicle);
        var result = new List<LightPartInfo>(parts.Count);
        foreach (var part in parts)
        {
            var ls = part.LightSwitch ?? part.FullPart?.LightSwitch;
            bool isEnabled = ls == null || ls.LightIsActive;
            result.Add(new LightPartInfo(
                part.Id,
                part.DisplayName ?? part.Id,
                LightController.ReadIntensity(part.Template),
                LightController.ReadColor(part.Template),
                isEnabled,
                _animationManager.IsAnimating(Key(part)),
                _animationManager.GetQueueCount(Key(part))));
        }
        return result;
    }

    /// <summary>Sets color and/or intensity on a specific light part. Returns error message or null on success.</summary>
    public string? SetLightState(string vehicleId, string partId, float3? color, float? intensity, bool? enabled)
    {
        var part = ResolvePartInVehicle(vehicleId, partId);
        if (part == null) return $"Part '{partId}' not found on vehicle '{vehicleId}'.";

        StopDisco(part);
        ManageLight(part);
        if (color.HasValue) LightController.ApplyColor(part, color.Value);
        if (intensity.HasValue) LightController.ApplyIntensity(part, intensity.Value);
        if (enabled.HasValue)
        {
            var ls = part.LightSwitch ?? part.FullPart?.LightSwitch;
            if (ls != null)
                ls.LightIsActive = enabled.Value;
            else if (!enabled.Value)
                LightController.ApplyIntensity(part, 0f);
        }
        return null;
    }

    /// <summary>Queues a light animation on a specific part. Returns error message or null on success.</summary>
    public string? QueueAnimation(string vehicleId, string partId, LightAnimation animation)
    {
        var part = ResolvePartInVehicle(vehicleId, partId);
        if (part == null) return $"Part '{partId}' not found on vehicle '{vehicleId}'.";

        StopDisco(part);
        ManageLight(part);
        if (!_animationManager.Enqueue(Key(part), animation))
            return $"Animation queue is full for part '{partId}' (max {LightAnimationManager.MaxQueueDepth}).";
        return null;
    }

    /// <summary>Clears the animation queue for a specific part. Returns error message or null on success.</summary>
    public string? ClearAnimationQueue(string vehicleId, string partId)
    {
        // No error if part doesn't exist — clear is idempotent
        var part = ResolvePartInVehicle(vehicleId, partId);
        if (part != null) _animationManager.CancelAll(Key(part));
        return null;
    }

    /// <summary>Returns true if a part has an active animation.</summary>
    public bool IsAnimating(string partId) => _managedLights.Any(pair =>
        (pair.Key == partId || pair.Value.Id == partId) && _animationManager.IsAnimating(pair.Key));

    private Part? ResolvePartInVehicle(string vehicleId, string partId)
    {
        var vehicle = VehicleProvider.GetAllVehicles().Find(v => v.Id == vehicleId);
        if (vehicle == null) return null;
        var matches = LightController.GetLightParts(vehicle).Where(p => p.Id == partId || Key(p) == partId).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public void Dispose()
    {
        ReleaseLiveState();
        _animationManager.Clear();
        Instance = null;
    }

    private static bool MatchesFilter(ImInputString filter, string value)
    {
        var filterText = filter.ToString().Trim();
        return filterText.Length == 0 || value.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }
}
