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

/// <summary>POST/DELETE /blinky/grids — build and destroy registered blinky grids.</summary>
public static class BlinkyGridsEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (BlinkyBuildGridRequest body) =>
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
                        if (BlinkyGridManager.Get(body.VehicleId, body.GridName) != null)
                        {
                            throw new ProviderException(ResponseStatus.Conflict,
                                $"Grid '{body.GridName}' already registered for vehicle '{body.VehicleId}'.");
                        }

                        var vehicle = FindVehicleById(body.VehicleId);
                        if (vehicle == null)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"Vehicle not found: {body.VehicleId}.");

                        var config = new LcdGridConfig
                        {
                            Width = body.Width ?? 16,
                            Height = body.Height ?? 8,
                            Layout = ParseLayout(body.Layout),
                            Spacing = body.Spacing ?? 5.0f,
                            OffsetX = body.OffsetX ?? 0f,
                            OffsetY = body.OffsetY ?? 5f,
                            OffsetZ = body.OffsetZ ?? 2f,
                            EnginePartId = body.EnginePartId ?? "CorePropulsionA_Prefab_EngineA3",
                            PartScale = body.PartScale ?? 0.010,
                        };

                        var blinkyGrid = LcdGridBuilder.BuildGrid(vehicle, body.GridName, config);
                        if (blinkyGrid == null)
                            throw new ProviderException(ResponseStatus.InternalServerError,
                                "Grid build failed. Check server logs for details.");

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
                        "Unexpected error building blinky grid.", ex);
                }
            })
            .Delete(async (string vehicleId, string gridName) =>
            {
                if (string.IsNullOrWhiteSpace(vehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId query parameter.");
                if (string.IsNullOrWhiteSpace(gridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName query parameter.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var state = BlinkyGridManager.Get(vehicleId, gridName);
                        if (state == null)
                        {
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No grid '{gridName}' registered for vehicle '{vehicleId}'.");
                        }

                        var wasOwned = state.BlinkyGrid.IsOwned;
                        if (wasOwned)
                            LcdGridBuilder.DestroyGrid(state.Vehicle, state.BlinkyGrid);

                        BlinkyGridManager.Unregister(vehicleId, gridName);

                        return new BlinkyResult(
                            vehicleId,
                            gridName,
                            wasOwned ? "grid_destroyed" : "grid_unregistered");
                    });

                    return (object)new ApiResponse<BlinkyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error deleting blinky grid.", ex);
                }
            })
            .Build();
    }

    private static GridLayout ParseLayout(string? layout)
    {
        if (string.IsNullOrWhiteSpace(layout) ||
            layout.Equals("flat", StringComparison.OrdinalIgnoreCase))
            return GridLayout.Flat;

        if (layout.Equals("cylinder", StringComparison.OrdinalIgnoreCase))
            return GridLayout.Cylinder;

        throw new ProviderException(ResponseStatus.BadRequest,
            $"Invalid layout '{layout}'. Must be 'flat' or 'cylinder'.");
    }

    private static Vehicle? FindVehicleById(string vehicleId)
    {
        return VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == vehicleId);
    }
}
