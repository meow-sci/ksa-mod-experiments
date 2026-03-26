using System;
using System.Linq;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class ActionIgnite
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (VehicleActionRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing or invalid vehicleId.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var vehicles = Universe.CurrentSystem?.Vehicles.GetList() ?? Enumerable.Empty<Vehicle>();
                        var vehicle = vehicles.FirstOrDefault(v => v.Id == body.VehicleId);

                        if (vehicle is null)
                            throw new ProviderException(ResponseStatus.NotFound, $"Vehicle not found: {body.VehicleId}.");

                        vehicle.SetEnum(VehicleEngine.MainIgnite);
                        return new VehicleActionResult(body.VehicleId, "ignited");
                    });

                    return (object)new ApiResponse<VehicleActionResult>("ok", result);
                }
                catch (ProviderException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error igniting engine.", ex);
                }
            })
            .Build();
    }
}
