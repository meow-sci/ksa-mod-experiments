using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>GET/POST /blinky/render - gets or sets blinky pixel-part render settings.</summary>
public static class BlinkyRenderEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                        new BlinkyRenderSettings(BlinkyPatchState.RenderPixelParts));

                    return (object)new ApiResponse<BlinkyRenderSettings>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error reading blinky render settings.", ex);
                }
            })
            .Post(async (BlinkyRenderSettingsRequest body) =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        BlinkyPatchState.RenderPixelParts = body.RenderPixelParts;
                        return new BlinkyRenderSettings(BlinkyPatchState.RenderPixelParts);
                    });

                    return (object)new ApiResponse<BlinkyRenderSettings>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error updating blinky render settings.", ex);
                }
            })
            .Build();
    }
}
