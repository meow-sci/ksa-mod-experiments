using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using Brutal.Numerics;
using MeowSci.ZippoLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// HTTP handler for POST /zippo/lights/state — sets color/intensity/enabled on a light part.
/// </summary>
public static class ZippoLightStateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ZippoSetStateRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.VehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "VehicleId is required.");
                    if (string.IsNullOrWhiteSpace(body.PartId))
                        throw new ProviderException(ResponseStatus.BadRequest, "PartId is required.");
                    if (body.Color != null && body.ColorName != null)
                        throw new ProviderException(ResponseStatus.BadRequest, "Specify Color or ColorName, not both.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = ZippoSubmod.Instance
                            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Zippo mod is not loaded.");

                        // Resolve color
                        float3? color = null;
                        if (body.Color != null)
                            color = new float3(body.Color.R, body.Color.G, body.Color.B);
                        else if (body.ColorName != null)
                        {
                            var resolved = XkcdColorHelper.FindByName(body.ColorName);
                            if (resolved == null)
                                throw new ProviderException(ResponseStatus.BadRequest,
                                    $"Unknown XKCD color name: '{body.ColorName}'.");
                            color = new float3(resolved.Value.X, resolved.Value.Y, resolved.Value.Z);
                        }

                        var error = submod.SetLightState(body.VehicleId, body.PartId, color, body.Intensity, body.Enabled);
                        if (error != null)
                            throw new ProviderException(ResponseStatus.NotFound, error);

                        // Read back state for response
                        var parts = submod.GetLightPartInfos(body.VehicleId);
                        var part = parts?.Find(p => p.PartId == body.PartId)
                            ?? throw new ProviderException(ResponseStatus.NotFound,
                                $"Part '{body.PartId}' not found after update.");

                        return new ZippoSetStateResult(
                            part.PartId,
                            new ZippoColor(part.Color.X, part.Color.Y, part.Color.Z),
                            part.Intensity,
                            part.IsEnabled);
                    });

                    return (object)new ApiResponse<ZippoSetStateResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error setting light state.", ex);
                }
            })
            .Build();
    }
}
