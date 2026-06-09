using System;
using Brutal.Numerics;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /shiny/grids/scan-all — scans all active vehicles for its-so-shiny light grids.</summary>
public static class ShinyGridScanAllEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ShinyScanAllRequest body) =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var color = new float3(body.ColorR ?? 1f, body.ColorG ?? 1f, body.ColorB ?? 1f);
                        var intensity = body.Intensity ?? 1f;
                        var (discovered, names) = ShinyGridManager.ScanAllVehicles(color, intensity);
                        return new ShinyScanAllResult(discovered, names.ToArray());
                    });

                    return (object)new ApiResponse<ShinyScanAllResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error scanning its-so-shiny grids across all vehicles.", ex);
                }
            })
            .Build();
    }
}
