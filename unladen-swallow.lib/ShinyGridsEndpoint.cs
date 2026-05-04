using System;
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

/// <summary>POST/DELETE /shiny/grids — build and destroy registered its-so-shiny light grids.</summary>
public static class ShinyGridsEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ShinyBuildGridRequest body) =>
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
                        if (ShinyGridManager.Get(body.VehicleId, body.GridName) != null)
                        {
                            throw new ProviderException(ResponseStatus.Conflict,
                                $"Grid '{body.GridName}' already registered for vehicle '{body.VehicleId}'.");
                        }

                        var vehicle = FindVehicleById(body.VehicleId);
                        if (vehicle == null)
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"Vehicle not found: {body.VehicleId}.");

                        var color = new float3(body.ColorR ?? 1f, body.ColorG ?? 1f, body.ColorB ?? 1f);
                        var intensity = body.Intensity ?? 1f;

                        var config = new ShinyGridConfig
                        {
                            Width = body.Width ?? 8,
                            Height = body.Height ?? 8,
                            Layout = ParseLayout(body.Layout),
                            Spacing = body.Spacing ?? 0.75f,
                            OffsetX = body.OffsetX ?? 0f,
                            OffsetY = body.OffsetY ?? 3f,
                            OffsetZ = body.OffsetZ ?? 2f,
                            LightPartId = body.LightPartId ?? ShinyGridConfig.DefaultLightPartId,
                            PartScale = body.PartScale ?? 0.5,
                        };

                        var shinyGrid = ShinyGridBuilder.BuildGrid(vehicle, body.GridName, config, color, intensity);
                        if (shinyGrid == null)
                            throw new ProviderException(ResponseStatus.InternalServerError,
                                "Grid build failed. Check server logs for details.");

                        var state = ShinyGridManager.Register(vehicle, body.GridName, shinyGrid, color, intensity);

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
                        "Unexpected error building its-so-shiny grid.", ex);
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
                        var state = ShinyGridManager.Get(vehicleId, gridName);
                        if (state == null)
                        {
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No grid '{gridName}' registered for vehicle '{vehicleId}'.");
                        }

                        var wasOwned = state.ShinyGrid.IsOwned;
                        if (wasOwned)
                            ShinyGridBuilder.DestroyGrid(state.Vehicle, state.ShinyGrid);

                        ShinyGridManager.Unregister(vehicleId, gridName);

                        return new ShinyResult(
                            vehicleId,
                            gridName,
                            wasOwned ? "grid_destroyed" : "grid_unregistered");
                    });

                    return (object)new ApiResponse<ShinyResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error deleting its-so-shiny grid.", ex);
                }
            })
            .Build();
    }

    private static ShinyGridLayout ParseLayout(string? layout)
    {
        if (string.IsNullOrWhiteSpace(layout) ||
            layout.Equals("flat", StringComparison.OrdinalIgnoreCase))
            return ShinyGridLayout.Flat;

        if (layout.Equals("cylinder", StringComparison.OrdinalIgnoreCase))
            return ShinyGridLayout.Cylinder;

        throw new ProviderException(ResponseStatus.BadRequest,
            $"Invalid layout '{layout}'. Must be 'flat' or 'cylinder'.");
    }

    private static Vehicle? FindVehicleById(string vehicleId)
    {
        return VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == vehicleId);
    }
}
