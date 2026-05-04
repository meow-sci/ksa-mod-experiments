using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /shiny/off — turns off all pixels on a vehicle's light grid.</summary>
public static class ShinyOffEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ShinyOffRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        if (!ShinyGridManager.TurnOff(body.VehicleId, body.GridName))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No shiny grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new ShinyResult(body.VehicleId, body.GridName, "off");
                    });

                    return (object)new ApiResponse<ShinyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error turning off pixels.", ex);
                }
            })
            .Build();
    }
}
