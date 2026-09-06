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
/// HTTP handler for /torch/welds/modify — POST modifies an existing weld's properties.
/// </summary>
public static class TorchWeldModifyEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (TorchModifyWeldRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.SourceVehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "SourceVehicleId is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GarrysTorchSubmod.Instance
                            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Garry's Torch mod is not loaded.");

                        float3? pos = body.Position != null
                            ? new float3(body.Position.X, body.Position.Y, body.Position.Z)
                            : null;
                        float3? rot = body.Rotation != null
                            ? new float3(body.Rotation.X, body.Rotation.Y, body.Rotation.Z)
                            : null;
                        float3? scale = body.Scale != null
                            ? new float3(body.Scale.X, body.Scale.Y, body.Scale.Z)
                            : null;

                        var (weld, error) = submod.ModifyWeld(
                            body.SourceVehicleId, pos, rot, scale, body.LockRotation);

                        if (weld == null)
                            throw new ProviderException(ResponseStatus.NotFound, error!);

                        return new TorchWeldResult(new WeldInfo(
                            weld.Source.Id, weld.Target.Id,
                            new Vec3(weld.Position.X, weld.Position.Y, weld.Position.Z),
                            new Vec3(weld.Rotation.X, weld.Rotation.Y, weld.Rotation.Z),
                            new Vec3(weld.Scale.X, weld.Scale.Y, weld.Scale.Z), weld.LockRotation));
                    });
                    return (object)new ApiResponse<TorchWeldResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error modifying weld.", ex);
                }
            })
            .Build();
    }
}
