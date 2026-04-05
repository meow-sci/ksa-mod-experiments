using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/pattern - applies a named built-in pattern to a vehicle's LCD grid.</summary>
public static class BlinkyPatternEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyPatternRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (string.IsNullOrWhiteSpace(body.Pattern))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing pattern.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var selector = ResolvePattern(body.Pattern);

                        if (!BlinkyGridManager.ApplyPattern(body.VehicleId, body.GridName, selector))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No blinky grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new BlinkyResult(body.VehicleId, body.GridName, $"pattern_{body.Pattern}");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error applying pattern.", ex);
                }
            })
            .Build();
    }

    private static Func<(int row, int col), bool> ResolvePattern(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "allon" => PixelPatterns.AllOn,
            "alloff" => _ => false,
            "checkerboard" => PixelPatterns.Checkerboard,
            "altrows" => PixelPatterns.AlternatingRows,
            "altcols" => PixelPatterns.AlternatingCols,
            _ => throw new ProviderException(
                ResponseStatus.BadRequest,
                "Unknown pattern. Valid values: allOn, allOff, checkerboard, altRows, altCols.")
        };
    }
}