using System;
using Brutal.Numerics;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>GET/POST /shiny/appearance — gets or sets the light color and intensity for a registered grid.</summary>
public static class ShinyAppearanceEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async (string vehicleId, string gridName) =>
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
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No shiny grid '{gridName}' registered for vehicle: {vehicleId}.");

                        return new ShinyAppearance(vehicleId, gridName, state.Color.X, state.Color.Y, state.Color.Z, state.Intensity);
                    });

                    return (object)new ApiResponse<ShinyAppearance>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error reading shiny appearance.", ex);
                }
            })
            .Post(async (ShinyAppearanceRequest body) =>
            {
                if (string.IsNullOrWhiteSpace(body.VehicleId))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing vehicleId.");
                if (string.IsNullOrWhiteSpace(body.GridName))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing gridName.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var color = new float3(body.ColorR, body.ColorG, body.ColorB);
                        if (!ShinyGridManager.SetAppearance(body.VehicleId, body.GridName, color, body.Intensity))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"No shiny grid '{body.GridName}' registered for vehicle: {body.VehicleId}.");

                        var state = ShinyGridManager.Get(body.VehicleId, body.GridName)!;
                        return new ShinyAppearance(body.VehicleId, body.GridName, state.Color.X, state.Color.Y, state.Color.Z, state.Intensity);
                    });

                    return (object)new ApiResponse<ShinyAppearance>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error updating shiny appearance.", ex);
                }
            })
            .Build();
    }
}
