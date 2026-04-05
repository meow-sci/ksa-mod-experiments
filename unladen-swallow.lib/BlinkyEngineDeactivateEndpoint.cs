using System;
using System.Linq;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using KSA;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/engines/deactivate - deactivates non-LCD engines for a vehicle.</summary>
public static class BlinkyEngineDeactivateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyEngineDeactivateRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var vehicle = FindVehicleById(body.VehicleId);
                        if (vehicle == null)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"Vehicle not found: {body.VehicleId}.");

                        NonLcdEngineCache.DeactivateAll(vehicle);
                        return new BlinkyResult(body.VehicleId, string.Empty, "engines_deactivated");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error deactivating non-LCD engines.", ex);
                }
            })
            .Build();
    }

    private static Vehicle? FindVehicleById(string vehicleId)
    {
        return VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == vehicleId);
    }
}
