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

/// <summary>
/// POST /blinky/grids/repair — re-wires a registered grid's engines into the vehicle's
/// propellant network. Grids discovered by scanning (or built before the feed-wiring fix)
/// have no declared feed connection, so their engines can never light.
/// </summary>
public static class BlinkyGridRepairEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyRepairGridRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var state = BlinkyGridManager.Get(body.VehicleId, body.GridName);
                        if (state == null)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No registered grid '{body.GridName}' on vehicle '{body.VehicleId}'.");

                        int total = state.BlinkyGrid.Grid.Count * 2;
                        int fed = LcdGridBuilder.RepairFuelFeeds(state.Vehicle, state.BlinkyGrid);
                        return new BlinkyRepairResult(state.VehicleId, state.GridName, fed, total);
                    });

                    return (object)new ApiResponse<BlinkyRepairResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error repairing blinky grid fuel feed.", ex);
                }
            })
            .Build();
    }
}
