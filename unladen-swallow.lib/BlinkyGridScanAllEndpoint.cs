using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/grids/scan-all — scans all active vehicles for blinky grids.</summary>
public static class BlinkyGridScanAllEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async () =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var (discovered, names) = BlinkyGridManager.ScanAllVehicles();
                        return new BlinkyScanAllResult(discovered, names.ToArray());
                    });

                    return (object)new ApiResponse<BlinkyScanAllResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error scanning blinky grids across all vehicles.", ex);
                }
            })
            .Build();
    }
}
