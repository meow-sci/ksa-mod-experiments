using System;
using System.IO;
using System.Reflection;
using System.Text;
using Brutal.VulkanApi;
using KSA;

namespace MeowSci.MeshDeformLib;

/// <summary>
/// Runtime shader modification for mesh deformation.
///
/// Compiles modified <c>MeshIndirectVert.glsl</c> at runtime, swaps the
/// <see cref="VkShaderModule"/> on the <see cref="ShaderReference"/>, and
/// rebuilds the rendering pipeline (<see cref="PartModelRenderer.ColorData.Rebuild"/>).
///
/// On unload the original shader is restored via <see cref="ShaderReference.DoLoad()"/>.
///
/// Pattern copied from <c>humble-arteest.lib/VehiclePaint.cs</c> and
/// <c>humble-arteest.lib/Experiments/ShaderHotReloadTest.cs</c>.
/// </summary>
public static class MeshDeformShaders
{
    private static bool _shadersActive;
    private static string? _lastError;
    private static bool? _isSupported;
    private static string? _unsupportedReason;

    public static bool ShadersActive => _shadersActive;
    public static string? LastError => _lastError;

    /// <summary>
    /// Whether this build of KSA still supports the runtime shader-swap deformation mechanism.
    /// Computed once by probing the on-disk part shader; see <see cref="DetectSupport"/>.
    /// </summary>
    public static bool IsSupported
    {
        get
        {
            _isSupported ??= DetectSupport(out _unsupportedReason);
            return _isSupported.Value;
        }
    }

    /// <summary>Human-readable reason deformation is unavailable, when <see cref="IsSupported"/> is false.</summary>
    public static string? UnsupportedReason
    {
        get
        {
            _ = IsSupported; // ensure the probe has run
            return _unsupportedReason;
        }
    }

    /// <summary>
    /// Activates deformation by compiling a modified vertex shader and rebuilding pipelines.
    /// </summary>
    public static bool Activate()
    {
        _lastError = null;

        if (!IsSupported)
        {
            _lastError = UnsupportedReason;
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }

        try
        {
            var device = Program.GetRenderer().Device;

            if (!CompileAndSwapShader("MeshIndirectVert", ModifyVertexShader, device))
                return false;

            PartModelRenderer.ColorData.Rebuild();

            _shadersActive = true;
            Console.WriteLine("mesh-deform: Deformation shaders activated.");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Shader activation failed: {Unwrap(ex)}";
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }
    }

    /// <summary>
    /// Restores the original game shaders and rebuilds pipelines.
    /// </summary>
    public static bool Deactivate()
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
            // Note: fragment shader is untouched in this mod, but if we ever modify it,
            // restore it here as well.

            PartModelRenderer.ColorData.Rebuild();

            _shadersActive = false;
            Console.WriteLine("mesh-deform: Original shaders restored.");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Shader deactivation failed: {Unwrap(ex)}";
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }
    }

    /// <summary>
    /// Deactivates shaders and resets state. Call on mod unload.
    /// </summary>
    public static void Cleanup()
    {
        if (_shadersActive)
            Deactivate();
    }

    // ---- Capability detection ----

    /// <summary>
    /// Probes the on-disk MeshIndirect vertex shader to decide whether the legacy runtime
    /// shader-swap can still drive deformation on this game build.
    ///
    /// KSA rev 4693 rebuilt the part color pipeline: <c>PartModelRenderer.ColorData</c> now
    /// compiles MeshIndirect per-pipeline through
    /// <c>ShaderReference.CompileVariantWithCustomOptions()</c> — which re-reads the GLSL from
    /// disk with <c>ENABLE_*</c> macro variants and destroys the compiled module immediately —
    /// so it no longer consults <c>ShaderReference.Shader</c>. Swapping that module (this mod's
    /// entire mechanism) therefore has no effect, and the feature-gated shader also wrapped the
    /// <c>uint EmissiveColor;</c> struct anchor this mod injects after in <c>#ifdef</c> guards.
    /// Both conditions are detected here so activation fails visibly instead of compiling a
    /// broken shader.
    /// </summary>
    private static bool DetectSupport(out string? reason)
    {
        reason = null;
        try
        {
            var shaderRef = ModLibrary.Get<ShaderReference>("MeshIndirectVert");
            var modPath = GetShaderModPath(shaderRef);
            if (modPath == null || !File.Exists(modPath))
            {
                reason = "Mesh Deform unavailable: MeshIndirect.vert could not be located on disk.";
                return false;
            }

            var src = File.ReadAllText(modPath).Replace("\r\n", "\n");

            bool featureGated = src.Contains("#ifdef ENABLE_EMISSIVE")
                             || src.Contains("#ifdef ENABLE_TEMPERATURE");
            bool anchorPresent = src.Contains("    uint EmissiveColor;\n};");

            if (featureGated || !anchorPresent)
            {
                reason = "Mesh Deform is unavailable on this KSA build. The part shader was rebuilt "
                       + "(KSA 4693+) into feature-gated variants compiled per-pipeline from disk, so the "
                       + "runtime shader-swap this feature depends on no longer affects rendering.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = $"Mesh Deform unavailable: could not verify shader compatibility ({Unwrap(ex).Message}).";
            return false;
        }
    }

    /// <summary>
    /// Unwraps reflection invocation exceptions to reveal the real error.
    /// </summary>
    private static Exception Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException tie && tie.InnerException != null)
            ex = tie.InnerException;
        return ex;
    }

    /// <summary>
    /// Validates that the GLSL struct was actually modified by checking for our injected fields.
    /// </summary>
    private static bool ValidateStructModified(string source, string shaderId)
    {
        if (!source.Contains("DeformMagnitude"))
        {
            _lastError = $"Struct injection failed for {shaderId} — 'DeformMagnitude' not found in modified source. " +
                         "The InstanceData struct in the original shader may use different field names or layout.";
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }
        if (!source.Contains("DeformRadius"))
        {
            _lastError = $"Struct injection failed for {shaderId} — 'DeformRadius' not found in modified source.";
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }
        return true;
    }

    // ---- Shader modification logic ----

    private static bool CompileAndSwapShader(string shaderId, Func<string, string> modifier, Device device)
    {
        ShaderReference shaderRef;
        try { shaderRef = ModLibrary.Get<ShaderReference>(shaderId); }
        catch (Exception ex)
        {
            _lastError = $"Failed to get ShaderReference '{shaderId}': {Unwrap(ex).Message}";
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }

        var modPath = GetShaderModPath(shaderRef);

        if (modPath == null || !File.Exists(modPath))
        {
            _lastError = $"Shader file not found for {shaderId}: {modPath}";
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }

        var originalSource = File.ReadAllText(modPath);
        var modifiedSource = modifier(originalSource);

        if (modifiedSource == originalSource)
        {
            _lastError = $"Modification had no effect on {shaderId} — expected strings not found in shader source.";
            Console.WriteLine($"mesh-deform: {_lastError}");
            return false;
        }

        if (!ValidateStructModified(modifiedSource, shaderId))
            return false;

        // Write modified source to a persistent debug file on failure so user can inspect
        var dir = Path.GetDirectoryName(modPath)!;
        var ext = Path.GetExtension(modPath);
        var tempPath = Path.Combine(dir, $"_mesh_deform_tmp_{shaderId}{ext}");
        var debugPath = Path.Combine(dir, $"_mesh_deform_debug_{shaderId}{ext}");

        try
        {
            File.WriteAllText(tempPath, modifiedSource, new UTF8Encoding(false));

            var fromFileMethod = FindFromFileMethod();
            if (fromFileMethod == null)
            {
                _lastError = $"ShaderModuleUtils.FromFile not found for {shaderId}.";
                Console.WriteLine($"mesh-deform: {_lastError}");
                return false;
            }

            var args = new object?[] { device, tempPath, default(VkShaderStageFlags), null };
            object? invokeResult;
            try { invokeResult = fromFileMethod.Invoke(null, args); }
            catch (Exception ex)
            {
                var unwrapped = Unwrap(ex);
                _lastError = $"Shader compilation failed for {shaderId}: {unwrapped.Message}";
                Console.WriteLine($"mesh-deform: {_lastError}");
                Console.WriteLine($"mesh-deform: Stack trace: {unwrapped.StackTrace}");
                // Preserve debug file for inspection
                try
                {
                    File.Copy(tempPath, debugPath, overwrite: true);
                    Console.WriteLine($"mesh-deform: Debug shader written to: {debugPath}");
                }
                catch { /* best effort */ }
                return false;
            }

            if (invokeResult is not VkShaderModule newModule)
            {
                _lastError = $"Shader compilation returned unexpected type for {shaderId}: {(invokeResult?.GetType().FullName ?? "null")}";
                Console.WriteLine($"mesh-deform: {_lastError}");
                return false;
            }

            var oldModule = shaderRef.Shader;
            SwapShaderModule(shaderRef, newModule);

            if (oldModule.HasValue)
            {
                try { device.DestroyShaderModule(oldModule.Value, null); }
                catch (Exception ex)
                {
                    Console.WriteLine($"mesh-deform: Non-fatal error destroying old shader module: {Unwrap(ex).Message}");
                }
            }

            Console.WriteLine($"mesh-deform: {shaderId} compiled and swapped.");
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

    /// <summary>
    /// Modifies the vertex shader to add deformation fields and apply displacement.
    ///
    /// The shader receives per-instance data in a storage buffer. We expand the
    /// <c>InstanceData</c> struct with <c>DeformMagnitude</c> and <c>DeformRadius</c>
    /// after the existing <c>EmissiveColor</c> field (which is the last field before
    /// the closing brace).  Then in <c>main()</c> we displace vertices radially from
    /// the local-space origin BEFORE the world matrix is applied.
    /// </summary>
    private static string ModifyVertexShader(string source)
    {
        source = source.Replace("\r\n", "\n");

        // 1. Inject deformation fields into the InstanceData struct.
        // The real shader's struct ends with "uint EmissiveColor;" followed by "};".
        source = source.Replace(
            "    uint EmissiveColor;\n};",
            "    uint EmissiveColor;\n    float DeformMagnitude;\n    float DeformRadius;\n};");

        // 2. Inject the deformation logic BEFORE the world-space transform.
        // The real shader has:   vec4 worldPosVec4 = worldMatrix * vec4(inPos, 1.0);
        // We replace it with displacement + the same line using deformedPos.
        string anchor = "    vec4 worldPosVec4 = worldMatrix * vec4(inPos, 1.0);";
        if (source.Contains(anchor))
        {
            string deformationCode =
                "    // --- MeshDeform radial displacement ---\n" +
                "    float deformDist = length(inPos);\n" +
                "    float deformAtten = smoothstep(instanceData.DeformRadius, 0.0, deformDist);\n" +
                "    vec3 deformOffset = deformDist > 0.001\n" +
                "        ? normalize(inPos) * instanceData.DeformMagnitude * deformAtten\n" +
                "        : vec3(0.0);\n" +
                "    vec3 deformedPos = inPos + deformOffset;\n" +
                "    // --- End MeshDeform ---\n\n" +
                "    vec4 worldPosVec4 = worldMatrix * vec4(deformedPos, 1.0);";

            source = source.Replace(anchor, deformationCode);
            return source;
        }

        // Fallback: try the older anchor patterns if the shader has a different layout
        string anchorA = "    vec4 worldPosition = instanceData.ModelMatrix * vec4(inPosition, 1.0);";
        if (source.Contains(anchorA))
        {
            source = source.Replace(anchorA,
                "    // --- MeshDeform fallback A ---\n" +
                "    float deformDist = length(inPosition);\n" +
                "    float deformAtten = smoothstep(instanceData.DeformRadius, 0.0, deformDist);\n" +
                "    vec3 deformOffset = deformDist > 0.001 ? normalize(inPosition) * instanceData.DeformMagnitude * deformAtten : vec3(0.0);\n" +
                "    vec3 deformedPos = inPosition + deformOffset;\n" +
                "    // --- End MeshDeform ---\n\n" +
                anchorA.Replace("inPosition", "deformedPos"));
            return source;
        }

        string anchorB = "    vec4 worldPos = instanceData.ModelMatrix * vec4(localPos, 1.0);";
        if (source.Contains(anchorB))
        {
            source = source.Replace(anchorB,
                "    // --- MeshDeform fallback B ---\n" +
                "    float deformDist = length(localPos);\n" +
                "    float deformAtten = smoothstep(instanceData.DeformRadius, 0.0, deformDist);\n" +
                "    vec3 deformOffset = deformDist > 0.001 ? normalize(localPos) * instanceData.DeformMagnitude * deformAtten : vec3(0.0);\n" +
                "    vec3 deformedPos = localPos + deformOffset;\n" +
                "    // --- End MeshDeform ---\n\n" +
                anchorB.Replace("localPos", "deformedPos"));
            return source;
        }

        // If we reach here, no anchor matched.  Return unmodified so the caller aborts.
        Console.WriteLine("mesh-deform: WARNING — Could not find world-position anchor in MeshIndirectVert.");
        Console.WriteLine("mesh-deform: The shader may have changed. Inspect the source and update anchors.");
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
                return GetShaderPath(localPath);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetShaderPath(string relPath)
    {
        // Resolve game directory from KSA.dll assembly location
        string? gameDir = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "KSA" && !string.IsNullOrEmpty(asm.Location))
            {
                var dir = Path.GetDirectoryName(asm.Location);
                if (dir != null && Directory.Exists(Path.Combine(dir, "Content", "Core", "Shaders")))
                {
                    gameDir = dir;
                    break;
                }
            }
        }
        gameDir ??= @"C:\Program Files\Kitten Space Agency";
        return Path.Combine(gameDir, "Content", "Core", "Shaders", relPath);
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
