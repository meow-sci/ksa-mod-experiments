using System;
using System.IO;
using System.Reflection;
using System.Text;
using Brutal.VulkanApi;
using KSA;
using KSA.AssetReloader;

namespace MeowSci.HumbleArteestLib.Experiments;

/// <summary>
/// Experiment 0.5: Runtime Shader Hot-Reload Test
///
/// Validates that KSA's shader infrastructure can be used from a mod to compile
/// modified shaders at runtime and swap them in without a game restart. This is the
/// key enabler for the painting system — if this works, the mod can inject custom
/// shaders on load and restore originals on unload, all in-memory.
///
/// Strategy: read the original shader GLSL from disk, modify in memory (add paint
/// tint support), write to a temp file in the same directory (for #include path
/// resolution), compile via ShaderModuleUtils.FromFile, swap the VkShaderModule on
/// the ShaderReference, delete the temp file, and rebuild the rendering pipeline.
/// The original game shader files are never modified.
/// </summary>
public static class ShaderHotReloadTest
{
    // ---- State ----

    private static bool _infrastructureReady;
    private static bool _shadersSwapped;
    private static string? _lastError;
    private static string? _statusMessage;

    // Cached infrastructure
    private static object? _programInstance;
    private static ShaderReloader? _shaderReloader;

    public static string? LastError => _lastError;
    public static string? StatusMessage => _statusMessage;
    public static bool InfrastructureReady => _infrastructureReady;
    public static bool ShadersSwapped => _shadersSwapped;

    // ---- Phase A: Verify Access to Hot-Reload Infrastructure ----

    /// <summary>
    /// Probes the game's shader infrastructure via reflection to confirm all
    /// required pieces are accessible from mod context.
    /// </summary>
    public static bool ProbeInfrastructure()
    {
        _lastError = null;
        _statusMessage = null;
        var sb = new StringBuilder();

        try
        {
            // 1. Access Program.Instance
            var programType = typeof(Part).Assembly.GetType("KSA.Program");
            if (programType == null) { _lastError = "KSA.Program type not found."; return false; }

            var instanceProp = programType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) { _lastError = "Program.Instance property not found."; return false; }

            _programInstance = instanceProp.GetValue(null);
            if (_programInstance == null) { _lastError = "Program.Instance is null (game not fully loaded?)."; return false; }
            sb.AppendLine("Program.Instance: OK");

            // 2. Access ShaderReloader
            var reloaderField = programType.GetField("_shaderReloader",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (reloaderField != null)
            {
                _shaderReloader = reloaderField.GetValue(_programInstance) as ShaderReloader;
                sb.AppendLine($"ShaderReloader: {(_shaderReloader != null ? "OK" : "null")}");
                if (_shaderReloader != null)
                    sb.AppendLine($"  HotReloadingEnabled: {_shaderReloader.HotReloadingEnabled}");
            }
            else
            {
                sb.AppendLine("ShaderReloader: field not found (non-critical)");
            }

            // 3. Access Renderer + Device
            var getRenderer = programType.GetMethod("GetRenderer", BindingFlags.Public | BindingFlags.Static);
            if (getRenderer == null) { _lastError = "Program.GetRenderer() not found."; return false; }
            var renderer = getRenderer.Invoke(null, null);
            if (renderer == null) { _lastError = "GetRenderer() returned null."; return false; }
            sb.AppendLine("Renderer: OK");

            var deviceProp = renderer.GetType().GetProperty("Device", BindingFlags.Public | BindingFlags.Instance);
            var device = deviceProp?.GetValue(renderer);
            sb.AppendLine($"Device: {(device != null ? "OK" : "null")}");

            // 4. Access ShaderReference for MeshIndirectVert/Frag
            var shaderIds = new[] {
                "MeshIndirectVert", "MeshIndirectFrag",
                "DynamicMeshIndirectVert", "DynamicMeshIndirectFrag"
            };

            foreach (var id in shaderIds)
            {
                try
                {
                    var shaderRef = ModLibrary.Get<ShaderReference>(id);
                    var modPath = GetShaderModPath(shaderRef);
                    var hasModule = shaderRef.Shader.HasValue;
                    sb.AppendLine($"  {id}: ModPath={modPath ?? "?"}, HasModule={hasModule}, Stage={shaderRef.Stage}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  {id}: ERROR — {ex.Message}");
                }
            }

            // 5. Verify PartModelRenderer.ColorData.Rebuild() is callable
            var colorDataType = typeof(PartModelRenderer).GetNestedType("ColorData",
                BindingFlags.Public | BindingFlags.Static);
            if (colorDataType != null)
            {
                var rebuildMethod = colorDataType.GetMethod("Rebuild",
                    BindingFlags.Public | BindingFlags.Static);
                sb.AppendLine($"PartModelRenderer.ColorData.Rebuild: {(rebuildMethod != null ? "OK" : "NOT FOUND")}");
            }
            else
            {
                sb.AppendLine("PartModelRenderer.ColorData: type not found");
            }

            // 6. Verify ShaderModuleUtils.FromFile is accessible
            var fromFileAvailable = FindFromFileMethod() != null;
            sb.AppendLine($"ShaderModuleUtils.FromFile: {(fromFileAvailable ? "OK" : "NOT FOUND")}");

            _infrastructureReady = true;
            _statusMessage = sb.ToString();
            Console.WriteLine($"humble-arteest: ShaderHotReloadTest probe results:\n{_statusMessage}");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Probe error: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            if (ex.InnerException != null)
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            return false;
        }
    }

    // ---- Phase B: Runtime Shader Swap (Temp-File Compile) ----

    /// <summary>
    /// Compiles modified shaders at runtime and swaps them into the rendering pipeline.
    /// The original game shader files are NOT modified. A temporary file is created
    /// in the same directory for #include path resolution, then immediately deleted.
    /// </summary>
    public static bool SwapToModifiedShaders()
    {
        _lastError = null;

        try
        {
            var device = Program.GetRenderer().Device;

            // Swap vertex shader (static)
            if (!CompileAndSwapShader("MeshIndirectVert", ModifyVertexShader, device))
                return false;

            // Swap fragment shader (static)
            if (!CompileAndSwapShader("MeshIndirectFrag", ModifyFragmentShader, device))
                return false;

            // Rebuild pipelines to pick up new shader modules
            PartModelRenderer.ColorData.Rebuild();

            _shadersSwapped = true;
            _statusMessage = "Shaders swapped and pipelines rebuilt. Paint tint support is ACTIVE.";
            Console.WriteLine($"humble-arteest: {_statusMessage}");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Swap error: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            if (ex.InnerException != null)
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores the original shaders by recompiling from the untouched game files
    /// on disk, then rebuilding the pipelines.
    /// </summary>
    public static bool RestoreOriginalShaders()
    {
        _lastError = null;

        try
        {
            // DoLoad() is internal — invoke via reflection
            var doLoadMethod = typeof(ShaderReference).GetMethod("DoLoad",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (doLoadMethod == null)
            {
                _lastError = "ShaderReference.DoLoad() method not found via reflection.";
                return false;
            }

            var vertRef = ModLibrary.Get<ShaderReference>("MeshIndirectVert");
            doLoadMethod.Invoke(vertRef, null);

            var fragRef = ModLibrary.Get<ShaderReference>("MeshIndirectFrag");
            doLoadMethod.Invoke(fragRef, null);

            // Rebuild pipelines with restored shader modules
            PartModelRenderer.ColorData.Rebuild();

            _shadersSwapped = false;
            _statusMessage = "Original shaders restored and pipelines rebuilt.";
            Console.WriteLine($"humble-arteest: {_statusMessage}");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Restore error: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    // ---- Shader Modification Logic ----

    private static bool CompileAndSwapShader(string shaderId, Func<string, string> modifier, Device device)
    {
        var shaderRef = ModLibrary.Get<ShaderReference>(shaderId);
        var modPath = GetShaderModPath(shaderRef);

        if (modPath == null || !File.Exists(modPath))
        {
            _lastError = $"Shader file not found for {shaderId}: {modPath}";
            return false;
        }

        // Read original source and apply modifications in memory
        var originalSource = File.ReadAllText(modPath);
        var modifiedSource = modifier(originalSource);

        if (modifiedSource == originalSource)
        {
            _lastError = $"Modification had no effect on {shaderId} — expected strings not found in shader.";
            return false;
        }

        // Write to a temp file in the same directory so #include paths resolve correctly
        var dir = Path.GetDirectoryName(modPath)!;
        var ext = Path.GetExtension(modPath);
        var tempFileName = $"_humble_arteest_tmp_{shaderId}{ext}";
        var tempPath = Path.Combine(dir, tempFileName);

        try
        {
            File.WriteAllText(tempPath, modifiedSource, new UTF8Encoding(false));

            // Compile from temp file via reflection to avoid Brutal.ShaderCompiler dependency.
            // ShaderModuleUtils.FromFile(Device, string, out VkShaderStageFlags, CompileOptions?)
            var fromFileMethod = FindFromFileMethod();
            if (fromFileMethod == null)
            {
                _lastError = $"ShaderModuleUtils.FromFile method not found for {shaderId}.";
                return false;
            }

            var args = new object?[] { device, tempPath, default(VkShaderStageFlags), null };
            var newModule = (VkShaderModule)fromFileMethod.Invoke(null, args)!;
            var stage = (VkShaderStageFlags)args[2]!;

            // Swap: destroy old module, set new one via reflection (Shader is a private setter)
            var oldModule = shaderRef.Shader;

            var shaderProp = typeof(ShaderReference).GetProperty("Shader",
                BindingFlags.Public | BindingFlags.Instance);
            var setter = shaderProp?.GetSetMethod(nonPublic: true);
            if (setter == null)
            {
                // Fallback: set via backing field
                var backingField = typeof(ShaderReference).GetField("<Shader>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField != null)
                    backingField.SetValue(shaderRef, (VkShaderModule?)newModule);
                else
                {
                    _lastError = $"Cannot set Shader property on {shaderId} — no setter or backing field found.";
                    device.DestroyShaderModule(newModule, null);
                    return false;
                }
            }
            else
            {
                setter.Invoke(shaderRef, new object[] { (VkShaderModule?)newModule });
            }

            // Destroy old module after swap
            if (oldModule.HasValue)
                device.DestroyShaderModule(oldModule.Value, null);

            Console.WriteLine($"humble-arteest: {shaderId} compiled and swapped successfully.");
            return true;
        }
        finally
        {
            // Always clean up the temp file
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// Modifies the vertex shader to pass paint color from PerInstanceData padding to fragment.
    /// </summary>
    private static string ModifyVertexShader(string source)
    {
        // Normalize line endings for reliable matching
        source = source.Replace("\r\n", "\n");

        // 1. Expand InstanceData struct with paint fields in the padding slots
        source = source.Replace(
            "    int Highlighted;\n};",
            "    int Highlighted;\n    float PaintR;\n    float PaintG;\n    float PaintB;\n};");

        // 2. Add output variables for paint color
        source = source.Replace(
            "layout(location = 5) out flat int outHighlighted;",
            "layout(location = 5) out flat int outHighlighted;\nlayout(location = 6) out float outPaintR;\nlayout(location = 7) out float outPaintG;\nlayout(location = 8) out float outPaintB;");

        // 3. Pass paint values through to fragment shader
        source = source.Replace(
            "    outHighlighted = instanceData.Highlighted;",
            "    outHighlighted = instanceData.Highlighted;\n\n    outPaintR = instanceData.PaintR;\n    outPaintG = instanceData.PaintG;\n    outPaintB = instanceData.PaintB;");

        return source;
    }

    /// <summary>
    /// Modifies the fragment shader to read paint color and apply as multiplicative tint.
    /// </summary>
    private static string ModifyFragmentShader(string source)
    {
        // Normalize line endings for reliable matching
        source = source.Replace("\r\n", "\n");

        // 1. Add input variables for paint color
        source = source.Replace(
            "layout (location = 5) in flat int inHighlighted;",
            "layout (location = 5) in flat int inHighlighted;\nlayout (location = 6) in float inPaintR;\nlayout (location = 7) in float inPaintG;\nlayout (location = 8) in float inPaintB;");

        // 2. Apply paint tint after albedo texture sampling
        source = source.Replace(
            "    vec3 sampledColor = gammaToLinear(texture(sampler2D(globalTextures[drawData.diffuseTextureIndex], textureSampler), inUv).xyz);",
            "    vec3 sampledColor = gammaToLinear(texture(sampler2D(globalTextures[drawData.diffuseTextureIndex], textureSampler), inUv).xyz);\n\n    // Paint tint from per-instance data padding\n    vec3 paintTint = vec3(inPaintR, inPaintG, inPaintB);\n    if (dot(paintTint, paintTint) > 0.001) {\n        sampledColor *= paintTint;\n    }");

        return source;
    }

    // ---- Helpers ----

    /// <summary>
    /// Finds the ShaderModuleUtils.FromFile(Device, string, out VkShaderStageFlags, CompileOptions?)
    /// method via reflection to avoid direct dependency on Brutal.ShaderCompiler assembly.
    /// </summary>
    private static MethodInfo? FindFromFileMethod()
    {
        var shaderModuleUtilsType = typeof(Part).Assembly.GetType("RenderCore.ShaderModuleUtils");
        if (shaderModuleUtilsType == null)
        {
            // Try Planet.Render.Core assembly
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                shaderModuleUtilsType = asm.GetType("RenderCore.ShaderModuleUtils");
                if (shaderModuleUtilsType != null) break;
            }
        }

        if (shaderModuleUtilsType == null) return null;

        // Find the 4-param overload: FromFile(Device, string, out VkShaderStageFlags, CompileOptions?)
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

    /// <summary>
    /// Gets the full file path for a ShaderReference via its ModPath property.
    /// </summary>
    private static string? GetShaderModPath(ShaderReference shaderRef)
    {
        try
        {
            // ModPath is a computed property on FileReference
            var modPathProp = typeof(ShaderReference).GetProperty("ModPath",
                BindingFlags.Public | BindingFlags.Instance);
            if (modPathProp != null)
                return modPathProp.GetValue(shaderRef) as string;

            // Fallback: try LocalPath + manual resolution
            var localPath = shaderRef.LocalPath;
            if (!string.IsNullOrEmpty(localPath))
                return GamePaths.GetShaderPath(localPath);

            return null;
        }
        catch
        {
            return null;
        }
    }
}
