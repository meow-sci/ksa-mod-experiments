using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/off — turns off all pixels on a vehicle's LCD grid.</summary>
public static class BlinkyOffEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyOffRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        if (!BlinkyGridManager.TurnOff(body.VehicleId, body.GridName))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new BlinkyResult(body.VehicleId, body.GridName, "off");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
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
