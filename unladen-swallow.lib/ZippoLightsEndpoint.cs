using System;
using System.Linq;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.ZippoLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// HTTP handler for GET /zippo/lights — lists all light parts on a vehicle with current state.
/// Query param: vehicleId (required)
/// </summary>
public static class ZippoLightsEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async (IRequest request) =>
            {
                try
                {
                    // Read vehicleId directly from the query dictionary to avoid GenHTTP's
                    // parameter-injection quirk where named params aren't injected when the
                    // request path lacks a trailing slash (e.g. /zippo/lights vs /zippo/lights/).
                    request.Query.TryGetValue("vehicleId", out var vehicleId);
                    if (string.IsNullOrWhiteSpace(vehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "vehicleId query parameter is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = ZippoSubmod.Instance
                            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Zippo mod is not loaded.");

                        var parts = submod.GetLightPartInfos(vehicleId)
                            ?? throw new ProviderException(ResponseStatus.NotFound,
                                $"Vehicle '{vehicleId}' not found.");

                        var lights = parts.Select(p => new ZippoLightPartInfo(
                            p.PartId,
                            p.DisplayName,
                            p.Intensity,
                            new ZippoColor(p.Color.X, p.Color.Y, p.Color.Z),
                            p.IsEnabled,
                            p.IsAnimating,
                            p.QueuedAnimations)).ToArray();

                        return new ZippoLightsListResult(vehicleId, lights);
                    });

                    return (object)new ApiResponse<ZippoLightsListResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error listing lights.", ex);
                }
            })
            .Build();
    }
}
