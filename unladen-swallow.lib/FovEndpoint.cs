using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.GlassLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// HTTP handler for /fov — GET returns current FOV state, POST sets the FOV override.
/// </summary>
public static class FovEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                var state = await GameThread.Scheduler.Schedule(() =>
                    new FovState(
                        FovController.GetCurrentFovDegrees(),
                        FovController.OverrideFovDegrees,
                        FovController.IsOverrideActive));

                return (object)new ApiResponse<FovState>("ok", state);
            })
            .Post(async (FovRequest body) =>
            {
                var state = await GameThread.Scheduler.Schedule(() =>
                {
                    if (body.Fov <= 0f)
                        FovController.DisableOverride();
                    else
                        FovController.SetFov(body.Fov);

                    return new FovState(
                        FovController.GetCurrentFovDegrees(),
                        FovController.OverrideFovDegrees,
                        FovController.IsOverrideActive);
                });

                return (object)new ApiResponse<FovState>("ok", state);
            })
            .Build();
    }
}
