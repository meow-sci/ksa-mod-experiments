using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brutal.Numerics;
using Brutal.VulkanApi;
using KSA;
using KSA.AssetReloader;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// Core paint state and runtime shader management for the vehicle paint system.
///
/// Manages per-PartModel paint colors and shader swapping. Modified shaders read
/// RGB paint values from the PerInstanceData padding bytes (slots 68–79) that normally
/// go unused. A Harmony prefix on PartModel.AddInstance writes the paint color into
/// those bytes before the data is sent to the GPU.
///
/// Shader lifecycle:
///   ActivateShaders() — compile modified GLSL at runtime, swap VkShaderModule, rebuild pipelines
///   DeactivateShaders() — restore originals via ShaderReference.DoLoad(), rebuild pipelines
/// </summary>
public static class VehiclePaint
{
    // ---- Paint state ----

    private static readonly Dictionary<PartModel, float3> _partModelColors = new();
    private static bool _paintAllEnabled;
    private static float3 _defaultColor;

    // ---- Shader state ----

    private static bool _shadersActive;
    private static string? _lastError;

    // ---- Public properties ----

    public static bool ShadersActive => _shadersActive;
    public static string? LastError => _lastError;

    /// <summary>When true, all parts receive <see cref="DefaultColor"/> unless overridden per-PartModel.</summary>
    public static bool PaintAllEnabled
    {
        get => _paintAllEnabled;
        set => _paintAllEnabled = value;
    }

    /// <summary>Default paint color applied to all parts when <see cref="PaintAllEnabled"/> is true.</summary>
    public static float3 DefaultColor
    {
        get => _defaultColor;
        set => _defaultColor = value;
    }

    // ---- Per-PartModel API ----

    /// <summary>Sets a paint color for a specific PartModel instance.</summary>
    public static void SetPaintColor(PartModel partModel, float3 color)
    {
        _partModelColors[partModel] = color;
    }

    /// <summary>Removes paint from a specific PartModel instance.</summary>
    public static void ClearPaint(PartModel partModel)
    {
        _partModelColors.Remove(partModel);
    }

    /// <summary>Clears all per-PartModel paint entries and disables global paint.</summary>
    public static void ClearAllPaint()
    {
        _partModelColors.Clear();
        _paintAllEnabled = false;
    }

    /// <summary>Returns the paint color for a PartModel, or null if not painted.</summary>
    public static float3? GetPaintColor(PartModel partModel)
    {
        if (_partModelColors.TryGetValue(partModel, out var color))
            return color;
        if (_paintAllEnabled)
            return _defaultColor;
        return null;
    }

    /// <summary>
    /// Tries to get the effective paint color for a PartModel (used by the Harmony prefix).
    /// Returns true if paint should be applied and sets <paramref name="color"/>.
    /// </summary>
    internal static bool TryGetEffectiveColor(PartModel partModel, out float3 color)
    {
        if (_partModelColors.TryGetValue(partModel, out color))
            return true;
        if (_paintAllEnabled)
        {
            color = _defaultColor;
            return true;
        }
        color = default;
        return false;
    }

    // ---- Shader management ----

    /// <summary>
    /// Compiles modified shaders at runtime and swaps them into the pipeline.
    /// Original game shader files on disk are NOT modified — a temp file is used
    /// for compilation (for #include path resolution) and immediately deleted.
    /// </summary>
    public static bool ActivateShaders()
    {
        _lastError = null;

        try
        {
            var device = Program.GetRenderer().Device;

            if (!CompileAndSwapShader("MeshIndirectVert", ModifyVertexShader, device))
                return false;

            if (!CompileAndSwapShader("MeshIndirectFrag", ModifyFragmentShader, device))
                return false;

            PartModelRenderer.ColorData.Rebuild();

            _shadersActive = true;
            Console.WriteLine("humble-arteest: Paint shaders activated and pipelines rebuilt.");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Shader activation failed: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    /// <summary>
    /// Restores original shaders by recompiling from the untouched game files on disk
    /// and rebuilding pipelines.
    /// </summary>
    public static bool DeactivateShaders()
    {
        _lastError = null;

        try
        {
            var doLoadMethod = typeof(ShaderReference).GetMethod("DoLoad",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (doLoadMethod == null)
            {
                _lastError = "ShaderReference.DoLoad() not found via reflection.";
                return false;
            }

            doLoadMethod.Invoke(ModLibrary.Get<ShaderReference>("MeshIndirectVert"), null);
            doLoadMethod.Invoke(ModLibrary.Get<ShaderReference>("MeshIndirectFrag"), null);

            PartModelRenderer.ColorData.Rebuild();

            _shadersActive = false;
            Console.WriteLine("humble-arteest: Original shaders restored and pipelines rebuilt.");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Shader deactivation failed: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    /// <summary>Deactivates shaders and clears all paint state. Call on mod unload.</summary>
    public static void Cleanup()
    {
        if (_shadersActive)
            DeactivateShaders();
        ClearAllPaint();
    }

    // ---- Shader modification logic ----

    private static bool CompileAndSwapShader(string shaderId, Func<string, string> modifier, Device device)
    {
        var shaderRef = ModLibrary.Get<ShaderReference>(shaderId);
        var modPath = GetShaderModPath(shaderRef);

        if (modPath == null || !File.Exists(modPath))
        {
            _lastError = $"Shader file not found for {shaderId}: {modPath}";
            return false;
        }

        var originalSource = File.ReadAllText(modPath);
        var modifiedSource = modifier(originalSource);

        if (modifiedSource == originalSource)
        {
            _lastError = $"Modification had no effect on {shaderId} — expected strings not found.";
            return false;
        }

        // Write temp file in the same directory so #include paths resolve correctly
        var dir = Path.GetDirectoryName(modPath)!;
        var ext = Path.GetExtension(modPath);
        var tempPath = Path.Combine(dir, $"_humble_paint_tmp_{shaderId}{ext}");

        try
        {
            File.WriteAllText(tempPath, modifiedSource, new UTF8Encoding(false));

            var fromFileMethod = FindFromFileMethod();
            if (fromFileMethod == null)
            {
                _lastError = $"ShaderModuleUtils.FromFile not found for {shaderId}.";
                return false;
            }

            var args = new object?[] { device, tempPath, default(VkShaderStageFlags), null };
            var newModule = (VkShaderModule)fromFileMethod.Invoke(null, args)!;

            // Swap the VkShaderModule on the ShaderReference
            var oldModule = shaderRef.Shader;
            SwapShaderModule(shaderRef, newModule);

            if (oldModule.HasValue)
                device.DestroyShaderModule(oldModule.Value, null);

            Console.WriteLine($"humble-arteest: {shaderId} compiled and swapped.");
            return true;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best effort */ }
        }
    }

    private static void SwapShaderModule(ShaderReference shaderRef, VkShaderModule newModule)
    {
        var setter = typeof(ShaderReference)
            .GetProperty("Shader", BindingFlags.Public | BindingFlags.Instance)
            ?.GetSetMethod(nonPublic: true);

        if (setter != null)
        {
            setter.Invoke(shaderRef, new object[] { (VkShaderModule?)newModule });
            return;
        }

        var backingField = typeof(ShaderReference).GetField("<Shader>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (backingField != null)
        {
            backingField.SetValue(shaderRef, (VkShaderModule?)newModule);
            return;
        }

        throw new InvalidOperationException("Cannot set Shader property — no setter or backing field found.");
    }

    /// <summary>Adds PaintR/G/B float fields to the InstanceData struct and output variables.</summary>
    private static string ModifyVertexShader(string source)
    {
        source = source.Replace("\r\n", "\n");

        // Expand InstanceData struct with paint fields in padding slots
        source = source.Replace(
            "    int Highlighted;\n};",
            "    int Highlighted;\n    float PaintR;\n    float PaintG;\n    float PaintB;\n};");

        // Add output variables for paint color
        source = source.Replace(
            "layout(location = 5) out flat int outHighlighted;",
            "layout(location = 5) out flat int outHighlighted;\nlayout(location = 6) out float outPaintR;\nlayout(location = 7) out float outPaintG;\nlayout(location = 8) out float outPaintB;");

        // Pass paint values through to fragment shader
        source = source.Replace(
            "    outHighlighted = instanceData.Highlighted;",
            "    outHighlighted = instanceData.Highlighted;\n\n    outPaintR = instanceData.PaintR;\n    outPaintG = instanceData.PaintG;\n    outPaintB = instanceData.PaintB;");

        return source;
    }

    /// <summary>Adds paint inputs and applies multiplicative tint after albedo sampling.</summary>
    private static string ModifyFragmentShader(string source)
    {
        source = source.Replace("\r\n", "\n");

        // Add input variables for paint color
        source = source.Replace(
            "layout (location = 5) in flat int inHighlighted;",
            "layout (location = 5) in flat int inHighlighted;\nlayout (location = 6) in float inPaintR;\nlayout (location = 7) in float inPaintG;\nlayout (location = 8) in float inPaintB;");

        // Apply paint tint after albedo texture sampling
        source = source.Replace(
            "    vec3 sampledColor = gammaToLinear(texture(sampler2D(globalTextures[drawData.diffuseTextureIndex], textureSampler), inUv).xyz);",
            "    vec3 sampledColor = gammaToLinear(texture(sampler2D(globalTextures[drawData.diffuseTextureIndex], textureSampler), inUv).xyz);\n\n    // Paint tint from per-instance data padding\n    vec3 paintTint = vec3(inPaintR, inPaintG, inPaintB);\n    if (dot(paintTint, paintTint) > 0.001) {\n        sampledColor *= paintTint;\n    }");

        return source;
    }

    // ---- Helpers ----

    private static string? GetShaderModPath(ShaderReference shaderRef)
    {
        try
        {
            var modPathProp = typeof(ShaderReference).GetProperty("ModPath",
                BindingFlags.Public | BindingFlags.Instance);
            if (modPathProp != null)
                return modPathProp.GetValue(shaderRef) as string;

            var localPath = shaderRef.LocalPath;
            if (!string.IsNullOrEmpty(localPath))
                return Experiments.GamePaths.GetShaderPath(localPath);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static MethodInfo? FindFromFileMethod()
    {
        Type? shaderModuleUtilsType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            shaderModuleUtilsType = asm.GetType("RenderCore.ShaderModuleUtils");
            if (shaderModuleUtilsType != null) break;
        }

        if (shaderModuleUtilsType == null) return null;

        foreach (var method in shaderModuleUtilsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "FromFile") continue;
            var parameters = method.GetParameters();
            if (parameters.Length >= 3 &&
                parameters[0].ParameterType == typeof(Device) &&
                parameters[1].ParameterType == typeof(string) &&
                parameters[2].IsOut)
            {
                return method;
            }
        }

        return null;
    }
}
