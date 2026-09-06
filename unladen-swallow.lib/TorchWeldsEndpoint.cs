using System;
using System.Linq;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using Brutal.Numerics;
using MeowSci.GarrysTorchLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// HTTP handler for /torch/welds — GET lists all welds, POST creates a weld, DELETE removes a weld.
/// </summary>
public static class TorchWeldsEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GetSubmod();
                        var welds = submod.Welds.Select(ToWeldInfo).ToArray();
                        return new TorchWeldListResult(welds);
                    });
                    return (object)new ApiResponse<TorchWeldListResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error listing welds.", ex);
                }
            })
            .Post(async (TorchCreateWeldRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.SourceVehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "SourceVehicleId is required.");
                    if (string.IsNullOrWhiteSpace(body.TargetVehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "TargetVehicleId is required.");
                    if (body.Data != null && body.PresetName != null)
                        throw new ProviderException(ResponseStatus.BadRequest, "Specify Data or PresetName, not both.");
                    if (body.Data == null && body.PresetName == null)
                        throw new ProviderException(ResponseStatus.BadRequest, "Either Data or PresetName is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GetSubmod();
                        var data = ResolveWeldData(submod, body.Data, body.PresetName);
                        var pos = ToFloat3(data.Position);
                        var rot = ToFloat3(data.Rotation);
                        var scale = ToScale(data.Scale);

                        var (weld, error) = submod.CreateWeld(
                            body.SourceVehicleId, body.TargetVehicleId,
                            pos, rot, scale, data.LockRotation);

                        if (weld == null)
                            throw new ProviderException(ResponseStatus.BadRequest, error!);

                        return new TorchWeldResult(ToWeldInfo(weld));
                    });
                    return (object)new ApiResponse<TorchWeldResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error creating weld.", ex);
                }
            })
            .Delete(async (TorchDeleteWeldRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.SourceVehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "SourceVehicleId is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GetSubmod();
                        if (!submod.RemoveWeld(body.SourceVehicleId))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No weld found with source vehicle '{body.SourceVehicleId}'.");

                        return new TorchDeleteResult($"Weld for source '{body.SourceVehicleId}' removed.");
                    });
                    return (object)new ApiResponse<TorchDeleteResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error deleting weld.", ex);
                }
            })
            .Build();
    }

    private static GarrysTorchSubmod GetSubmod()
    {
        return GarrysTorchSubmod.Instance
            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                "Garry's Torch mod is not loaded.");
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
            ToVec3(preset.Scale),
            preset.LockRotation);
    }

    private static float3 ToFloat3(Vec3 v) => new float3(v.X, v.Y, v.Z);
    private static float3 ToScale(Vec3? v) =>
        v == null ? WeldScale.Identity : new float3(v.X, v.Y, v.Z);
    private static Vec3 ToVec3(float3 v) => new(v.X, v.Y, v.Z);

    private static WeldInfo ToWeldInfo(WeldEntry w) =>
        new WeldInfo(
            w.Source.Id, w.Target.Id,
            new Vec3(w.Position.X, w.Position.Y, w.Position.Z),
            new Vec3(w.Rotation.X, w.Rotation.Y, w.Rotation.Z),
            ToVec3(w.Scale), w.LockRotation);
}
