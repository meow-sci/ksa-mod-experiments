using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/static — displays a static set of pixels on a vehicle's LCD grid.</summary>
public static class BlinkyStaticEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyStaticRequest body) =>
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

                        if (!BlinkyGridManager.DisplayStatic(body.VehicleId, body.GridName, pixels, body.Reset))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new BlinkyResult(body.VehicleId, body.GridName, body.Reset ? "static_reset" : "static_additive");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error displaying static.", ex);
                }
            })
            .Build();
    }
}
