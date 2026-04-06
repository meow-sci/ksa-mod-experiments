using System;
using System.Linq;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using Brutal.Numerics;
using MeowSci.GarrysTorchLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// HTTP handler for /torch/presets — GET lists all presets, POST saves a preset, DELETE removes a preset.
/// </summary>
public static class TorchPresetsEndpoint
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
                    {
                        var submod = GetSubmod();
                        var presets = submod.GetPresetNames()
                            .Select(name =>
                            {
                                var p = submod.GetPreset(name)!.Value;
                                return new TorchPresetInfo(name,
                                    new Vec3(p.Position.X, p.Position.Y, p.Position.Z),
                                    new Vec3(p.Rotation.X, p.Rotation.Y, p.Rotation.Z),
                                    p.Scale, p.LockRotation);
                            })
                            .ToArray();
                        return new TorchPresetListResult(presets);
                    });
                    return (object)new ApiResponse<TorchPresetListResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error listing presets.", ex);
                }
            })
            .Post(async (TorchSavePresetRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.Name))
                        throw new ProviderException(ResponseStatus.BadRequest, "Name is required.");
                    if (body.Data == null)
                        throw new ProviderException(ResponseStatus.BadRequest, "Data is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GetSubmod();
                        var preset = new WeldPreset
                        {
                            Position = new float3(body.Data.Position.X, body.Data.Position.Y, body.Data.Position.Z),
                            Rotation = new float3(body.Data.Rotation.X, body.Data.Rotation.Y, body.Data.Rotation.Z),
                            Scale = body.Data.Scale,
                            LockRotation = body.Data.LockRotation,
                        };

                        if (!submod.SavePreset(body.Name, preset))
                            throw new ProviderException(ResponseStatus.InternalServerError,
                                "Failed to save preset.");

                        return new TorchPresetResult(new TorchPresetInfo(
                            body.Name,
                            body.Data.Position, body.Data.Rotation,
                            body.Data.Scale, body.Data.LockRotation));
                    });
                    return (object)new ApiResponse<TorchPresetResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error saving preset.", ex);
                }
            })
            .Delete(async (TorchDeletePresetRequest body) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body.Name))
                        throw new ProviderException(ResponseStatus.BadRequest, "Name is required.");

                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = GetSubmod();
                        if (!submod.DeletePreset(body.Name))
                            throw new ProviderException(ResponseStatus.NotFound,
                                $"Preset '{body.Name}' not found.");

                        return new TorchDeleteResult($"Preset '{body.Name}' deleted.");
                    });
                    return (object)new ApiResponse<TorchDeleteResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error deleting preset.", ex);
                }
            })
            .Build();
    }

    private static GarrysTorchSubmod GetSubmod()
    {
        return GarrysTorchSubmod.Instance
            ?? throw new ProviderException(ResponseStatus.ServiceUnavailable,
                "Garry's Torch mod is not loaded.");
    }
}
