using MeowSci.KsaRings;
using System;
using System.Reflection;
using Brutal.VulkanApi;
using KSA;
using KSA.Rendering.Rings.Rendering;
using MeowSci.KsaAbstractions;

namespace MeowSci.BloominOnionLib;

/// <summary>
/// Makes the game pick up ring references that were added to or removed from body templates
/// after load.
///
/// Two things decide what the game renders: <c>PlanetTransparenciesRenderer</c>'s list of
/// bodies with rings/atmospheres (built by its public <c>PopulatePlanets</c>, whose result is
/// cached in the private <c>_anyRings</c>), and <c>PlanetaryRingsRenderer</c>'s per-body
/// render data (built only in its constructor). So: wait for the device, dispose the existing
/// rings renderer, re-populate the body list, then run the game's own
/// <c>Program.RebuildRenderer()</c> — its <c>CreateRingsRenderer</c> branch rebuilds
/// everything from the current references with proper GPU sync.
/// </summary>
public static class RingRendererRebuilder
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool Rebuild(out string message)
    {
        try
        {
            var program = Program.Instance;
            if (program == null)
            {
                message = "game renderer not ready";
                return false;
            }
            var transparencies = ReflectionHelpers.GetFieldValue<PlanetTransparenciesRenderer>(program, "_planetTransparenciesRenderer");
            if (transparencies == null)
            {
                message = "Program._planetTransparenciesRenderer not found (game update?)";
                return false;
            }

            // In-flight frames may still reference ring pipelines/buffers/textures.
            Program.GetRenderer().Device.WaitIdle();
            DisposeRingsRenderer(transparencies);

            bool anyRings = transparencies.PopulatePlanets();
            ReflectionHelpers.SetFieldValue(transparencies, "_anyRings", anyRings);

            program.RebuildRenderer();
            message = anyRings ? "renderer rebuilt with rings" : "renderer rebuilt (no rings in system)";
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"bloomin-onion: renderer rebuild failed: {ex}");
            message = $"renderer rebuild failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>True while the game's planetary rings renderer exists.</summary>
    public static bool IsRingsRendererCreated()
    {
        var transparencies = ReflectionHelpers.GetFieldValue(Program.Instance, "_planetTransparenciesRenderer");
        return ReflectionHelpers.GetFieldValue(transparencies, "_ringRendererCreated") is true;
    }

    private static void DisposeRingsRenderer(PlanetTransparenciesRenderer transparencies)
    {
        if (ReflectionHelpers.GetFieldValue(transparencies, "_ringRendererCreated") is not true) return;
        if (ReflectionHelpers.GetFieldValue(transparencies, "_ringsRenderer") is not PlanetaryRingsRenderer ringsRenderer) return;
        ringsRenderer.Dispose();
        ReflectionHelpers.SetFieldValue(transparencies, "_ringRendererCreated", false);
    }

    /// <summary>
    /// Best-effort: a <c>StaticCelestial</c>'s distant-sphere renderer bakes "has ring shadow",
    /// the radii and the band texture handle into its push-constant struct at construction.
    /// Refreshing those fields keeps the ring shadow correct on the far-away sphere too.
    /// Silent on any mismatch — this is cosmetic.
    /// </summary>
    public static void SyncDistantSphereShadow(Celestial celestial)
    {
        try
        {
            var distant = GetFieldFromHierarchy(celestial, "_distantRenderer");
            if (distant == null) return;
            var dataField = distant.GetType().GetField("_data", AnyInstance);
            if (dataField == null) return;
            object? data = dataField.GetValue(distant);
            if (data == null) return;

            var rings = celestial.BodyTemplate?.RingsReference;
            var type = data.GetType();
            type.GetField("UseRingShadows")?.SetValue(data, rings != null ? 1 : 0);
            if (rings != null)
            {
                type.GetField("RingInnerRadius")?.SetValue(data, (float)rings.InnerRadius.InMeters());
                type.GetField("RingOuterRadius")?.SetValue(data, (float)rings.OuterRadius.InMeters());
                type.GetField("RingTextureId")?.SetValue(data, rings.Texture.Get().BindlessHandle);
                type.GetField("SamplerClampId")?.SetValue(data, Program.Instance.TextureSystem.SamplerClampHandle);
            }
            dataField.SetValue(distant, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"bloomin-onion: distant sphere ring shadow sync skipped for {celestial.Id}: {ex.Message}");
        }
    }

    /// <summary>Private fields declared on a base class are invisible to <c>GetType().GetField</c>; walk up.</summary>
    private static object? GetFieldFromHierarchy(object instance, string fieldName)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(fieldName, AnyInstance | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(instance);
        }
        return null;
    }
}
