using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class CameraStatusEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                try
                {
                    var status = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = CameraControllerOverrideSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Camera controller override mod is not loaded.");

                        var player = submod.SequencePlayer;
                        return new CameraPlaybackStatus(
                            State: player.State.ToString(),
                            IsReturningToStart: player.IsReturningToStart,
                            CurrentKeyframeIndex: player.CurrentKeyframeIndex,
                            TotalKeyframes: player.Keyframes.Count,
                            TotalElapsedTime: player.TotalElapsedTime,
                            TotalDurationSeconds: player.TotalDuration);
                    });
                    return (object)new ApiResponse<CameraPlaybackStatus>("ok", status);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error reading camera status.", ex);
                }
            })
            .Build();
    }
}
