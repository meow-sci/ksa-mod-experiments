using System;
using System.Collections.Generic;
using System.Linq;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using KSA;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>POST /blinky/grids/scan — scans a vehicle for a named blinky grid and registers it.</summary>
public static class BlinkyGridScanEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyScanGridRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");
                if (!PixelGrid.IsValidGridName(body.GridName))
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

                        BlinkyPixelGrid blinkyGrid;

                        if (!string.IsNullOrWhiteSpace(body.EnginePartId))
                        {
                            var scanned = LcdGridBuilder.ScanExistingGrid(vehicle, body.GridName, body.EnginePartId);
                            if (scanned == null)
                                throw new ProviderException(ResponseStatus.NotFound,
                                    $"No grid found on vehicle '{body.VehicleId}' matching template '{body.EnginePartId}'.");
                            blinkyGrid = scanned;
                        }
                        else
                        {
                            var pixelGrid = PixelGrid.ScanFromVehicle(vehicle, body.GridName);
                            if (pixelGrid.Count == 0)
                                throw new ProviderException(ResponseStatus.NotFound,
                                    $"No grid named '{body.GridName}' found on vehicle '{body.VehicleId}'.");

                            pixelGrid.RefreshEngineControllers();
                            blinkyGrid = new BlinkyPixelGrid(pixelGrid, new List<Part>());
                        }

                        var state = BlinkyGridManager.Register(vehicle, body.GridName, blinkyGrid);

                        return new BlinkyGridInfo(
                            state.VehicleId,
                            state.GridName,
                            state.BlinkyGrid.Grid.Rows,
                            state.BlinkyGrid.Grid.Cols,
                            state.BlinkyGrid.Grid.Count,
                            state.BlinkyGrid.IsOwned,
                            state.Scroll.IsActive,
                            state.Scroll.IsActive ? state.Scroll.ScrollSpeed : 0f);
                    });

                    return (object)new ApiResponse<BlinkyGridInfo>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error scanning blinky grid.", ex);
                }
            })
            .Build();
    }

    private static Vehicle? FindVehicleById(string vehicleId)
    {
        return VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == vehicleId);
    }
}
