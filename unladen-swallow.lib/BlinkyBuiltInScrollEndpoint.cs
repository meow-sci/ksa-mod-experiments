using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/animate/builtin - starts built-in scrolling on a vehicle's LCD grid.</summary>
public static class BlinkyBuiltInScrollEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyBuiltInScrollRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (body.Speed <= 0)
                    throw new ProviderException(ResponseStatus.BadRequest, "Speed must be greater than 0.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        if (!BlinkyGridManager.StartBuiltInScroll(body.VehicleId, body.GridName, body.Speed))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new BlinkyResult(body.VehicleId, body.GridName, "builtin_scroll_started");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error starting built-in scroll.", ex);
                }
            })
            .Build();
    }
}
