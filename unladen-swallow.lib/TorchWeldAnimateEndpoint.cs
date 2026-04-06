using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using Brutal.Numerics;
using MeowSci.GarrysTorchLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// HTTP handler for /torch/welds/animate — POST starts an animated weld transition.
/// </summary>
public static class TorchWeldAnimateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (TorchAnimateWeldRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.SourceVehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "SourceVehicleId is required.");
                    if (body.DurationSeconds <= 0)
                        throw new ProviderException(ResponseStatus.BadRequest, "DurationSeconds must be greater than 0.");
                    if (body.Data != null && body.PresetName != null)
                        throw new ProviderException(ResponseStatus.BadRequest, "Specify Data or PresetName, not both.");
                    if (body.Data == null && body.PresetName == null)
                        throw new ProviderException(ResponseStatus.BadRequest, "Either Data or PresetName is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GarrysTorchSubmod.Instance
                            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Garry's Torch mod is not loaded.");

                        var data = ResolveWeldData(submod, body.Data, body.PresetName);
                        var targetPos = new float3(data.Position.X, data.Position.Y, data.Position.Z);
                        var targetRot = new float3(data.Rotation.X, data.Rotation.Y, data.Rotation.Z);

                        var easing = body.Easing ?? new TorchEasingConfig();
                        var weldEasing = (WeldEasingType)(int)easing.Easing;

                        var error = submod.AnimateWeld(
                            body.SourceVehicleId,
                            targetPos, targetRot, data.Scale,
                            body.DurationSeconds, weldEasing,
                            easing.EasingPowerStart, easing.EasingPowerEnd);

                        if (error != null)
                            throw new ProviderException(ResponseStatus.BadRequest, error);

                        return new TorchAnimateResult(body.SourceVehicleId, "animation_queued");
                    });
                    return (object)new ApiResponse<TorchAnimateResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error animating weld.", ex);
                }
            })
            .Build();
    }

    private static WeldData ResolveWeldData(GarrysTorchSubmod submod, WeldData? data, string? presetName)
    {
        if (data != null) return data;

        var preset = submod.GetPreset(presetName!)
            ?? throw new ProviderException(ResponseStatus.NotFound,
                $"Preset '{presetName}' not found.");

        return new WeldData(
            new Vec3(preset.Position.X, preset.Position.Y, preset.Position.Z),
            new Vec3(preset.Rotation.X, preset.Rotation.Y, preset.Rotation.Z),
            preset.Scale,
            preset.LockRotation);
    }
}
