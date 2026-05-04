using System.Collections.Generic;
using GenHTTP.Api.Content;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>GET /shiny/grids — lists all registered its-so-shiny light grids.</summary>
public static class ShinyListEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async (string? vehicleId) =>
            {
                var result = await GameThread.Scheduler.Schedule(() =>
                {
                    var grids = new List<ShinyGridInfo>();
                    foreach (var state in ShinyGridManager.Grids.Values)
                    {
                        if (vehicleId != null && state.VehicleId != vehicleId) continue;
                        grids.Add(new ShinyGridInfo(
                            state.VehicleId,
                            state.GridName,
                            state.ShinyGrid.Grid.Rows,
                            state.ShinyGrid.Grid.Cols,
                            state.ShinyGrid.Grid.Count,
                            state.ShinyGrid.IsOwned,
                            state.Scroll.IsActive,
                            state.Scroll.IsActive ? state.Scroll.ScrollSpeed : 0f,
                            state.Color.X,
                            state.Color.Y,
                            state.Color.Z,
                            state.Intensity));
                    }
                    return new ShinyGridListResult(grids.ToArray());
                });
                return (object)new ApiResponse<ShinyGridListResult>("ok", result);
            })
            .Build();
    }
}
