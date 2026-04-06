using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/animate — starts a scrolling animation on a vehicle's LCD grid.</summary>
public static class BlinkyAnimateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyScrollRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (body.Pixels == null || body.Pixels.Length == 0)
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing or empty pixels array.");
                if (body.Speed <= 0)
                    throw new ProviderException(ResponseStatus.BadRequest, "Speed must be greater than 0.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var pixels = new (int x, int y)[body.Pixels.Length];
                        for (int i = 0; i < body.Pixels.Length; i++)
                            pixels[i] = (body.Pixels[i].X, body.Pixels[i].Y);

                        if (!BlinkyGridManager.StartScroll(body.VehicleId, body.GridName, pixels, body.Speed))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new BlinkyResult(body.VehicleId, body.GridName, "scroll_started");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error starting scroll.", ex);
                }
            })
            .Delete(async (string vehicleId, string gridName) =>
            {
                if (string.IsNullOrWhiteSpace(vehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId query parameter.");
                if (string.IsNullOrWhiteSpace(gridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName query parameter.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        if (!BlinkyGridManager.StopScroll(vehicleId, gridName))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No blinky grid '{gridName}' registered for vehicle: {vehicleId}.");

                        return new BlinkyResult(vehicleId, gridName, "scroll_stopped");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error stopping scroll.", ex);
                }
            })
            .Build();
    }
}
