using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class CameraStopEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Delete(async () =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = CameraControllerOverrideSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Camera controller override mod is not loaded.");

                        var player = submod.SequencePlayer;
                        var previousState = player.State.ToString();

                        if (player.State != PlaybackState.Stopped)
                            player.Stop();

                        return new CameraStopResult(previousState);
                    });
                    return (object)new ApiResponse<CameraStopResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error stopping camera animation.", ex);
                }
            })
            .Build();
    }
}
