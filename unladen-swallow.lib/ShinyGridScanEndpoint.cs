using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using KSA;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /shiny/grids/scan — scans a vehicle for a named its-so-shiny light grid and registers it.</summary>
public static class ShinyGridScanEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ShinyScanGridRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (!ShinyPixelGrid.IsValidGridName(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest,
                        $"Invalid gridName '{body.GridName}'. Allowed: a-z, A-Z, 0-9, hyphen.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var vehicle = FindVehicleById(body.VehicleId);
                        if (vehicle == null)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"Vehicle not found: {body.VehicleId}.");

                        var pixelGrid = ShinyPixelGrid.ScanFromVehicle(vehicle, body.GridName);
                        if (pixelGrid.Count == 0)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No grid named '{body.GridName}' found on vehicle '{body.VehicleId}'.");

                        var defaultColor = new float3(1f, 1f, 1f);
                        const float defaultIntensity = 1f;
                        var shinyGrid = new ShinyBuiltGrid(pixelGrid, new List<Part>());
                        var state = ShinyGridManager.Register(vehicle, body.GridName, shinyGrid, defaultColor, defaultIntensity);

                        return new ShinyGridInfo(
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
                            state.Intensity);
                    });

                    return (object)new ApiResponse<ShinyGridInfo>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error scanning its-so-shiny grid.", ex);
                }
            })
            .Build();
    }

    private static Vehicle? FindVehicleById(string vehicleId)
    {
        return VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == vehicleId);
    }
}
