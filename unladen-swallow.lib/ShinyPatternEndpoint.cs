using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /shiny/pattern — applies a named built-in pattern to a vehicle's light grid.</summary>
public static class ShinyPatternEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ShinyPatternRequest body) =>
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

                        if (!ShinyGridManager.ApplyPattern(body.VehicleId, body.GridName, selector))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No shiny grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        return new ShinyResult(body.VehicleId, body.GridName, $"pattern_{body.Pattern}");
                    });

                    return (object)new ApiResponse<ShinyResult>("ok", result);
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
            "allon"        => ShinyPixelPatterns.AllOn,
            "alloff"       => _ => false,
            "checkerboard" => ShinyPixelPatterns.Checkerboard,
            "altrows"      => ShinyPixelPatterns.AlternatingRows,
            "altcols"      => ShinyPixelPatterns.AlternatingCols,
            _ => throw new ProviderException(
                ResponseStatus.BadRequest,
                "Unknown pattern. Valid values: allOn, allOff, checkerboard, altRows, altCols.")
        };
    }
}
