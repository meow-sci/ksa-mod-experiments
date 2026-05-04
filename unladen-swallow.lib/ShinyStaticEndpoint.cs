using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /shiny/static — displays a static set of pixels on a vehicle's light grid.</summary>
public static class ShinyStaticEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ShinyStaticRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (body.Pixels == null)
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing pixels array.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var pixels = new (int x, int y)[body.Pixels.Length];
                        for (int i = 0; i < body.Pixels.Length; i++)
                            pixels[i] = (body.Pixels[i].X, body.Pixels[i].Y);

                        if (!ShinyGridManager.DisplayStatic(body.VehicleId, body.GridName, pixels, body.Reset))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No shiny grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new ShinyResult(body.VehicleId, body.GridName, body.Reset ? "static_reset" : "static_additive");
                    });

                    return (object)new ApiResponse<ShinyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error displaying static pixels.", ex);
                }
            })
            .Build();
    }
}
