using System;
using System.Collections.Generic;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.CameraControllerOverrideLib.Animation.Animations;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class CameraAnimateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (CameraAnimateRequest body) =>
            {
                if (body.Sequence == null || body.Sequence.Length == 0)
                    throw new ProviderException(ResponseStatus.BadRequest, "Sequence must not be empty.");

                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = CameraControllerOverrideSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Camera controller override mod is not loaded.");

                        var player = submod.SequencePlayer;

                        if (player.State != PlaybackState.Stopped)
                            player.Stop();
                        player.Clear();

                        foreach (var step in body.Sequence)
                            player.AddKeyframe(ConvertStep(step));

                        if (body.ReturnToStart != null)
                        {
                            player.ReturnToStartEnabled = true;
                            player.ReturnToStartDuration = body.ReturnToStart.DurationSeconds;
                            player.ReturnToStartEasing = (EasingType)body.ReturnToStart.Easing;
                            player.ReturnToStartEasingPowerStart = body.ReturnToStart.EasingPowerStart;
                            player.ReturnToStartEasingPowerEnd = body.ReturnToStart.EasingPowerEnd;
                        }
                        else
                        {
                            player.ReturnToStartEnabled = false;
                        }

                        player.Play();

                        return new CameraAnimateResult(
                            player.Keyframes.Count,
                            player.TotalDuration,
                            player.ReturnToStartEnabled);
                    });
                    return (object)new ApiResponse<CameraAnimateResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error starting camera animation.", ex);
                }
            })
            .Build();
    }

    private static IKeyframeAnimation ConvertStep(CameraSequenceStep step)
    {
        if (step.Group != null)
        {
            if (step.Group.Length == 0)
                throw new ProviderException(ResponseStatus.BadRequest,
                    "Group must contain at least one animation.");

            var group = new AnimationGroup();
            foreach (var subStep in step.Group)
            {
                if (subStep.Group != null)
                    throw new ProviderException(ResponseStatus.BadRequest,
                        "Nested groups are not allowed. Groups may only contain single animations.");
                group.Add(ConvertSingleAnimation(subStep));
            }
            return group;
        }

        return ConvertSingleAnimation(step);
    }

    private static IKeyframeAnimation ConvertSingleAnimation(CameraSequenceStep step)
    {
        var set = new List<string>();
        if (step.ZoomOut != null) set.Add("zoomOut");
        if (step.ZoomIn != null) set.Add("zoomIn");
        if (step.ZoomInToOffset != null) set.Add("zoomInToOffset");
        if (step.Orbit != null) set.Add("orbit");
        if (step.LoopyOrbit != null) set.Add("loopyOrbit");
        if (step.SpiralZoomIn != null) set.Add("spiralZoomIn");
        if (step.SpiralZoomOut != null) set.Add("spiralZoomOut");
        if (step.Shake != null) set.Add("shake");
        if (step.Pan != null) set.Add("pan");
        if (step.Rotate != null) set.Add("rotate");

        if (set.Count == 0)
            throw new ProviderException(ResponseStatus.BadRequest,
                "Each sequence step must have exactly one animation type set.");
        if (set.Count > 1)
            throw new ProviderException(ResponseStatus.BadRequest,
                $"Each sequence step must have exactly one animation type set, but got: {string.Join(", ", set)}.");

        if (step.ZoomOut != null)
            return new ZoomOutAnimation(
                step.ZoomOut.SpeedMetersPerSecond,
                step.ZoomOut.DurationSeconds,
                (EasingType)step.ZoomOut.Easing,
                step.ZoomOut.EasingPowerStart,
                step.ZoomOut.EasingPowerEnd);

        if (step.ZoomIn != null)
            return new ZoomInAnimation(
                step.ZoomIn.SpeedMetersPerSecond,
                step.ZoomIn.DurationSeconds,
                (EasingType)step.ZoomIn.Easing,
                step.ZoomIn.EasingPowerStart,
                step.ZoomIn.EasingPowerEnd);

        if (step.ZoomInToOffset != null)
            return new ZoomInToOffsetAnimation(
                step.ZoomInToOffset.SpeedMetersPerSecond,
                step.ZoomInToOffset.DurationSeconds,
                (EasingType)step.ZoomInToOffset.Easing,
                step.ZoomInToOffset.OffsetX,
                step.ZoomInToOffset.OffsetY,
                step.ZoomInToOffset.OffsetZ,
                step.ZoomInToOffset.EasingPowerStart,
                step.ZoomInToOffset.EasingPowerEnd);

        if (step.Orbit != null)
            return new OrbitAnimation(
                step.Orbit.Degrees,
                step.Orbit.DurationSeconds,
                (EasingType)step.Orbit.Easing,
                step.Orbit.EasingPowerStart,
                step.Orbit.EasingPowerEnd);

        if (step.LoopyOrbit != null)
            return new LoopyOrbitAnimation(
                step.LoopyOrbit.Degrees,
                step.LoopyOrbit.LoopIntervalDegrees,
                step.LoopyOrbit.AmplitudeMeters,
                step.LoopyOrbit.DurationSeconds,
                (EasingType)step.LoopyOrbit.Easing,
                step.LoopyOrbit.EasingPowerStart,
                step.LoopyOrbit.EasingPowerEnd);

        if (step.SpiralZoomIn != null)
            return new SpiralZoomInAnimation(
                step.SpiralZoomIn.SpeedMetersPerSecond,
                step.SpiralZoomIn.DurationSeconds,
                (EasingType)step.SpiralZoomIn.Easing,
                step.SpiralZoomIn.SpiralDegrees,
                step.SpiralZoomIn.EasingPowerStart,
                step.SpiralZoomIn.EasingPowerEnd);

        if (step.SpiralZoomOut != null)
            return new SpiralZoomOutAnimation(
                step.SpiralZoomOut.SpeedMetersPerSecond,
                step.SpiralZoomOut.DurationSeconds,
                (EasingType)step.SpiralZoomOut.Easing,
                step.SpiralZoomOut.SpiralDegrees,
                step.SpiralZoomOut.EasingPowerStart,
                step.SpiralZoomOut.EasingPowerEnd);

        if (step.Shake != null)
            return new ShakeAnimation(
                step.Shake.DurationSeconds,
                step.Shake.ShakeCount,
                step.Shake.AmplitudeDegrees,
                step.Shake.ShakeSpeed,
                (EasingType)step.Shake.Easing,
                step.Shake.EasingPowerStart,
                step.Shake.EasingPowerEnd);

        if (step.Pan != null)
            return new PanAnimation(
                step.Pan.OffsetX,
                step.Pan.OffsetY,
                step.Pan.OffsetZ,
                step.Pan.DurationSeconds,
                (EasingType)step.Pan.Easing,
                step.Pan.EasingPowerStart,
                step.Pan.EasingPowerEnd);

        if (step.Rotate != null)
            return new RotateAnimation(
                step.Rotate.YawDegrees,
                step.Rotate.PitchDegrees,
                step.Rotate.DurationSeconds,
                (EasingType)step.Rotate.Easing,
                step.Rotate.EasingPowerStart,
                step.Rotate.EasingPowerEnd);

        // Unreachable given the count check above, but satisfies the compiler
        throw new ProviderException(ResponseStatus.BadRequest,
            "Each sequence step must have exactly one animation type set.");
    }
}
