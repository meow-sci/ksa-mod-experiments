using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using Brutal.Numerics;
using MeowSci.ZippoLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// HTTP handler for /zippo/animate — POST queues a light animation, DELETE clears the queue.
/// </summary>
public static class ZippoAnimateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (ZippoAnimateRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.VehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "VehicleId is required.");
                    if (string.IsNullOrWhiteSpace(body.PartId))
                        throw new ProviderException(ResponseStatus.BadRequest, "PartId is required.");
                    if (body.DurationSeconds <= 0)
                        throw new ProviderException(ResponseStatus.BadRequest, "DurationSeconds must be greater than 0.");
                    ValidateAnimColor(body.StartColor, "StartColor");
                    ValidateAnimColor(body.EndColor, "EndColor");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = ZippoSubmod.Instance
                            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Zippo mod is not loaded.");

                        // Find part to read current state for defaults
                        var parts = submod.GetLightPartInfos(body.VehicleId)
                            ?? throw new ProviderException(ResponseStatus.NotFound,
                                $"Vehicle '{body.VehicleId}' not found.");
                        var partInfo = parts.Find(p => p.PartId == body.PartId)
                            ?? throw new ProviderException(ResponseStatus.NotFound,
                                $"Part '{body.PartId}' not found on vehicle '{body.VehicleId}'.");

                        // Resolve start and end colors (default to current part color)
                        float3 startColor = ResolveColor(body.StartColor, partInfo.Color);
                        float3 endColor = ResolveColor(body.EndColor, partInfo.Color);

                        float startIntensity = body.StartIntensity ?? partInfo.Intensity;
                        float endIntensity = body.EndIntensity ?? partInfo.Intensity;

                        var easingConfig = body.Easing ?? new ZippoEasingConfig();
                        var easing = (MeowSci.KsaAbstractions.EasingType)(int)easingConfig.Easing;

                        var animation = new LightAnimation(
                            startColor, endColor,
                            startIntensity, endIntensity,
                            body.DurationSeconds, easing,
                            easingConfig.EasingPowerStart, easingConfig.EasingPowerEnd);

                        var error = submod.QueueAnimation(body.VehicleId, body.PartId, animation);
                        if (error != null)
                            throw new ProviderException(ResponseStatus.Conflict, error);

                        // Re-read part state to get accurate queue position
                        var refreshedParts = submod.GetLightPartInfos(body.VehicleId);
                        int queuePos = refreshedParts?.Find(p => p.PartId == body.PartId)?.QueuedAnimations ?? 0;

                        return new ZippoAnimateResult(body.PartId, "queued", queuePos);
                    });

                    return (object)new ApiResponse<ZippoAnimateResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error queuing animation.", ex);
                }
            })
            .Delete(async (ZippoClearAnimationRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.VehicleId))
                        throw new ProviderException(ResponseStatus.BadRequest, "VehicleId is required.");
                    if (string.IsNullOrWhiteSpace(body.PartId))
                        throw new ProviderException(ResponseStatus.BadRequest, "PartId is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = ZippoSubmod.Instance
                            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Zippo mod is not loaded.");

                        submod.ClearAnimationQueue(body.VehicleId, body.PartId);
                        return new ZippoClearAnimationResult(body.PartId, "cleared");
                    });

                    return (object)new ApiResponse<ZippoClearAnimationResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error clearing animation queue.", ex);
                }
            })
            .Build();
    }

    private static void ValidateAnimColor(ZippoAnimColor? spec, string fieldName)
    {
        if (spec != null && spec.Rgb != null && spec.ColorName != null)
            throw new ProviderException(ResponseStatus.BadRequest,
                $"{fieldName}: specify Rgb or ColorName, not both.");
    }

    private static float3 ResolveColor(ZippoAnimColor? spec, float3 currentColor)
    {
        if (spec == null) return currentColor;
        if (spec.Rgb != null) return new float3(spec.Rgb.R, spec.Rgb.G, spec.Rgb.B);
        if (spec.ColorName != null)
        {
            var resolved = XkcdColorHelper.FindByName(spec.ColorName);
            if (resolved == null)
                throw new ProviderException(ResponseStatus.BadRequest,
                    $"Unknown XKCD color name: '{spec.ColorName}'.");
            return new float3(resolved.Value.X, resolved.Value.Y, resolved.Value.Z);
        }
        return currentColor;
    }
}
