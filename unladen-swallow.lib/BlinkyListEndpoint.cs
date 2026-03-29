using System;
using System.Collections.Generic;
using GenHTTP.Api.Content;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>GET /blinky/grids — lists all registered blinky grids.</summary>
public static class BlinkyListEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async (string? vehicleId) =>
            {
                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    var grids = new List<BlinkyGridInfo>();
                    foreach (var state in BlinkyGridManager.Grids.Values)
                    {
                        if (vehicleId != null && state.VehicleId != vehicleId) continue;
                        grids.Add(new BlinkyGridInfo(
                            state.VehicleId,
                            state.GridName,
                            state.BlinkyGrid.Grid.Rows,
                            state.BlinkyGrid.Grid.Cols,
                            state.Scroll.IsActive));
                    }
                    return new BlinkyGridListResult(grids.ToArray());
                });
                return (object)new ApiResponse<BlinkyGridListResult>("ok", result);
            })
            .Build();
    }
}
