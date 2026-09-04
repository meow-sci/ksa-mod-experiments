using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using HarmonyLib;
using KSA;
using KSA.Rendering;
using RenderCore;
using RenderCore.Pipelines;

namespace MeowSci.FreeFallinLib;

/// <summary>
/// Adds one optional bind-pose projection varying to KSA's model PBR shaders. The path is selected
/// by a marker in MaterialData.ExtraData, so every non-Free-Fallin material remains stock.
/// </summary>
internal static class CanopyProjectionShaders
{
    internal const float MaterialMarker = 31415f;

    private const string StaticVertex = "Model.vert";
    private const string SkinnedVertex = "Model_Skinned.vert";
    private const string PbrFragment = "ModelPbr.frag";

    private static readonly string[] ShaderIds = { "ModelVert", "ModelSkinnedVert", "ModelPbrFrag" };
    private static readonly Dictionary<string, byte[]> SourceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly MethodInfo FromFile = AccessTools.Method(typeof(ShaderModuleUtils), nameof(ShaderModuleUtils.FromFile),
        new[] { typeof(Device), typeof(string), typeof(VkShaderStageFlags).MakeByRefType(), typeof(CompileOptions?) })
        ?? throw new MissingMethodException(typeof(ShaderModuleUtils).FullName, nameof(ShaderModuleUtils.FromFile));
    private static readonly MethodInfo SetShaderFromMod = AccessTools.Method(typeof(Utils), nameof(Utils.SetShaderFromMod),
        new[] { typeof(SimpleShaderStages), typeof(Device), typeof(string), typeof(bool) })
        ?? throw new MissingMethodException(typeof(Utils).FullName, nameof(Utils.SetShaderFromMod));
    private static readonly MethodInfo FromFilePatch = AccessTools.Method(typeof(CanopyProjectionShaders), nameof(FromFilePrefix))!;
    private static readonly MethodInfo SetShaderFromModPatch = AccessTools.Method(typeof(CanopyProjectionShaders), nameof(SetShaderFromModPrefix))!;

    private static bool _skinnedVertexCompiled;
    private static bool _pbrFragmentCompiled;

    internal static bool Available { get; private set; }
    internal static string? LastError { get; private set; }

    internal static void Apply(Harmony harmony)
    {
        SourceCache.Clear();
        LastError = null;
        _skinnedVertexCompiled = false;
        _pbrFragmentCompiled = false;
        Available = ValidateSources();
        if (!Available)
        {
            Console.WriteLine($"free-fallin: full-canopy projection unavailable: {LastError}");
            return;
        }

        harmony.Patch(FromFile, prefix: new HarmonyMethod(FromFilePatch));
        harmony.Patch(SetShaderFromMod, prefix: new HarmonyMethod(SetShaderFromModPatch));
        Program.RendererRebuildNeeded = true;
        Console.WriteLine("free-fallin: full-canopy projection shaders armed; renderer rebuild requested");
    }

    internal static void Remove(Harmony harmony)
    {
        if (!Available) return;
        Available = false;
        SourceCache.Clear();
        _skinnedVertexCompiled = false;
        _pbrFragmentCompiled = false;
        harmony.Unpatch(FromFile, FromFilePatch);
        harmony.Unpatch(SetShaderFromMod, SetShaderFromModPatch);
        Program.RendererRebuildNeeded = true;
        Console.WriteLine("free-fallin: full-canopy projection shaders removed; renderer rebuild requested");
    }

    internal static void RequireAvailable()
    {
        if (!Available)
            throw new InvalidOperationException(LastError ?? "Full-canopy projection shaders are unavailable on this KSA build.");
        if (!_skinnedVertexCompiled || !_pbrFragmentCompiled)
            throw new InvalidOperationException("Full-canopy projection shaders have not reached the active flight renderer. Check the console for shader rebuild errors.");
    }

    /// <summary>
    /// Stock model pipelines normally reuse cached ShaderReference modules when rebuilt. Force only
    /// our three targets through CompileVariantWithCustomOptions so FromFilePrefix can transform them.
    /// </summary>
    private static void SetShaderFromModPrefix(string modId, ref bool useCustomOptions)
    {
        if (!Available) return;
        foreach (string shaderId in ShaderIds)
        {
            if (!shaderId.Equals(modId, StringComparison.OrdinalIgnoreCase)) continue;
            useCustomOptions = true;
            return;
        }
    }

    private static bool FromFilePrefix(Device device, string filePath, ref VkShaderStageFlags shaderStage,
        CompileOptions? options, ref VkShaderModule __result)
    {
        if (!Available || !IsTarget(filePath)) return true;
        try
        {
            byte[] source = GetPatchedSource(filePath);
            VkShaderStageFlags stage = ShaderModuleUtils.ShaderStageFromFileExtension(filePath);
            __result = ShaderModuleUtils.FromString(device, source, stage, options, NullTerminated(filePath));
            shaderStage = stage;
            string name = Path.GetFileName(filePath);
            if (name.Equals(SkinnedVertex, StringComparison.OrdinalIgnoreCase)) _skinnedVertexCompiled = true;
            if (name.Equals(PbrFragment, StringComparison.OrdinalIgnoreCase)) _pbrFragmentCompiled = true;
            Console.WriteLine($"free-fallin: compiled full-canopy shader {name}");
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"Patched {Path.GetFileName(filePath)} failed to compile: {ex.Message}";
            Console.WriteLine($"free-fallin: {LastError}; using the stock shader");
            return true;
        }
    }

    private static bool ValidateSources()
    {
        foreach (string shaderId in ShaderIds)
        {
            try
            {
                string path = ModLibrary.Get<ShaderReference>(shaderId).ModPath;
                if (!File.Exists(path)) throw new FileNotFoundException($"Could not locate shader '{shaderId}'.", path);
                SourceCache[path] = PatchSource(path, File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                SourceCache.Clear();
                return false;
            }
        }
        return true;
    }

    private static bool IsTarget(string path)
    {
        string name = Path.GetFileName(path);
        return name.Equals(StaticVertex, StringComparison.OrdinalIgnoreCase)
               || name.Equals(SkinnedVertex, StringComparison.OrdinalIgnoreCase)
               || name.Equals(PbrFragment, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] GetPatchedSource(string path)
    {
        if (SourceCache.TryGetValue(path, out byte[]? source)) return source;
        source = PatchSource(path, File.ReadAllText(path));
        SourceCache[path] = source;
        return source;
    }

    private static byte[] PatchSource(string path, string source)
    {
        string name = Path.GetFileName(path);
        string patched = name switch
        {
            StaticVertex => PatchStaticVertex(source),
            SkinnedVertex => PatchSkinnedVertex(source),
            PbrFragment => PatchFragment(source),
            _ => throw new InvalidOperationException($"Unsupported shader target '{name}'.")
        };
        return new UTF8Encoding(false).GetBytes(patched);
    }

    private static string PatchStaticVertex(string source)
    {
        source = InsertAfter(source,
            "layout (location = 2) out flat ivec4 outInstanceID;",
            "layout (location = 3) out vec2 outFreeFallinUv;");
        return InsertAfter(source,
            "outWorldPosV = vec4(worldPos.xyz, inUv.y);",
            "    outFreeFallinUv = inUv;");
    }

    private static string PatchSkinnedVertex(string source)
    {
        source = InsertAfter(source, "#version 450",
            "\n#define SET_TEXTURE 1\n#include \"../Common/TextureSet.glsl\"\n\n#define SET_MATERIAL 2\n#include \"../Common/MaterialSet.glsl\"");
        source = InsertAfter(source,
            "layout (location = 2) out flat ivec4 outInstanceID;",
            "layout (location = 3) out vec2 outFreeFallinUv;");
        return InsertAfter(source,
            "outWorldPosV = vec4(worldPos.xyz, inUv.y);",
            "    Material ffMaterial = materialData[outInstanceID.x];\n" +
            "    vec2 ffSource = vec2(inPos.x, -inPos.z) * ffMaterial.extraData.x;\n" +
            "    outFreeFallinUv = vec2(\n" +
            "        ffMaterial.extraData.y * ffSource.x - ffMaterial.extraData.z * ffSource.y,\n" +
            "        ffMaterial.extraData.z * ffSource.x + ffMaterial.extraData.y * ffSource.y) + vec2(0.5);");
    }

    private static string PatchFragment(string source)
    {
        source = InsertAfter(source,
            "layout (location = 2) in flat ivec4 inInstanceID;",
            "layout (location = 3) in vec2 inFreeFallinUv;");
        return InsertAfter(source,
            "GetMaterialData(materialID, uv, albedo, normalMap, roughness, metallic, ao, emissive);",
            "    if (abs(materialData[materialID].extraData.w - 31415.0) < 0.25)\n" +
            "    {\n" +
            "        Material ffMaterial = materialData[materialID];\n" +
            "        albedo = ffMaterial.albedoColor * texture(\n" +
            "            sampler2D(globalTextures[ffMaterial.albedoIndex], samplers[ffMaterial.samplerIndex]),\n" +
            "            inFreeFallinUv);\n" +
            "    }");
    }

    private static string InsertAfter(string source, string anchor, string insertion)
    {
        int index = source.IndexOf(anchor, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException($"Shader changed shape; anchor not found: {anchor}");
        index += anchor.Length;
        return source.Insert(index, "\n" + insertion);
    }

    private static byte[] NullTerminated(string value)
    {
        byte[] result = new byte[Encoding.UTF8.GetByteCount(value) + 1];
        Encoding.UTF8.GetBytes(value, 0, value.Length, result, 0);
        return result;
    }
}
