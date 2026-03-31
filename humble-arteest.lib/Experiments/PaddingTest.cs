using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.HumbleArteestLib.Experiments;

/// <summary>
/// Experiment 0.2: PerInstanceData Padding Passthrough Test
///
/// Verifies that the 3 int padding fields in PerInstanceData (packing1/2/3) are passed
/// through to the GPU shader unchanged. Modified shaders read paint color from those
/// padding slots. A Harmony patch on PartModel.AddInstance writes known float values
/// (R=1.0, G=0.0, B=0.0) into the padding. If all static parts turn red, the
/// passthrough works and per-part RGB coloring via Approach A is feasible.
///
/// C# PerInstanceData layout (80 bytes):
///   float4x4 ModelMatrix  [0..63]   64 bytes
///   int      StateBitFlag  [64..67]  4 bytes
///   int      packing1      [68..71]  4 bytes ← PaintR (float bits)
///   int      packing2      [72..75]  4 bytes ← PaintG (float bits)
///   int      packing3      [76..79]  4 bytes ← PaintB (float bits)
///
/// GLSL std430 InstanceData layout (80 bytes, aligned to 16):
///   mat4  WorldMatrix  [0..63]   — same
///   int   Highlighted  [64..67]  — same
///   float PaintR       [68..71]  — reads the packing1 slot
///   float PaintG       [72..75]  — reads the packing2 slot
///   float PaintB       [76..79]  — reads the packing3 slot
/// </summary>
public static class PaddingTest
{
    private const string BackupSuffix = ".padding-test-backup";
    private const string VertRelPath = @"Mesh\MeshIndirect.vert";
    private const string FragRelPath = @"Mesh\MeshIndirect.frag";

    private static bool _enabled;
    private static float _paintR = 1.0f;
    private static float _paintG = 0.0f;
    private static float _paintB = 0.0f;
    private static string? _lastError;
    private static MethodInfo? _addInstanceOriginal;
    private static MethodInfo? _addInstancePrefix;

    public static string? LastError => _lastError;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Console.WriteLine($"humble-arteest: Padding test {(value ? "ENABLED" : "DISABLED")}");
        }
    }

    public static float PaintR { get => _paintR; set => _paintR = value; }
    public static float PaintG { get => _paintG; set => _paintG = value; }
    public static float PaintB { get => _paintB; set => _paintB = value; }

    // ---- Mirror struct with same layout but public float fields at padding offsets ----

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintablePerInstanceData
    {
        public float4x4 ModelMatrix; // 64 bytes
        public int StateBitFlag;     //  4 bytes
        public float PaintR;         //  4 bytes (was packing1)
        public float PaintG;         //  4 bytes (was packing2)
        public float PaintB;         //  4 bytes (was packing3)
    }

    // ---- Shader state ----

    public enum ShaderState
    {
        Original,
        Modified,
        Error
    }

    public static ShaderState GetShaderState()
    {
        _lastError = null;

        try
        {
            var vertPath = GamePaths.GetShaderPath(VertRelPath);
            var fragPath = GamePaths.GetShaderPath(FragRelPath);

            if (!File.Exists(vertPath) || !File.Exists(fragPath))
            {
                _lastError = $"Shader files not found.";
                return ShaderState.Error;
            }

            var vertContent = File.ReadAllText(vertPath);
            var fragContent = File.ReadAllText(fragPath);

            bool vertModified = vertContent.Contains("float PaintR;");
            bool fragModified = fragContent.Contains("inPaintR");

            if (vertModified && fragModified) return ShaderState.Modified;
            if (!vertModified && !fragModified) return ShaderState.Original;

            _lastError = "Shaders in inconsistent state (one modified, one not).";
            return ShaderState.Error;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return ShaderState.Error;
        }
    }

    // ---- Shader modification ----

    public static bool ApplyShaderModifications()
    {
        _lastError = null;

        try
        {
            var vertPath = GamePaths.GetShaderPath(VertRelPath);
            var fragPath = GamePaths.GetShaderPath(FragRelPath);

            // Read and normalize line endings for reliable string matching
            var vertContent = File.ReadAllText(vertPath).Replace("\r\n", "\n");
            var fragContent = File.ReadAllText(fragPath).Replace("\r\n", "\n");

            // Validate the shader is in expected original state
            if (!vertContent.Contains("    int Highlighted;\n};"))
            {
                _lastError = "Vertex shader struct not in expected state. May already be modified — restore first.";
                return false;
            }
            if (!fragContent.Contains("layout (location = 5) in flat int inHighlighted;\n"))
            {
                _lastError = "Fragment shader inputs not in expected state. May already be modified — restore first.";
                return false;
            }

            // Backup originals
            BackupFile(vertPath);
            BackupFile(fragPath);

            // ---- Modify vertex shader ----
            vertContent = vertContent
                // Expand InstanceData struct with paint fields
                .Replace(
                    "    int Highlighted;\n};",
                    "    int Highlighted;\n    float PaintR;\n    float PaintG;\n    float PaintB;\n};")
                // Add output variables
                .Replace(
                    "layout(location = 5) out flat int outHighlighted;",
                    "layout(location = 5) out flat int outHighlighted;\nlayout(location = 6) out float outPaintR;\nlayout(location = 7) out float outPaintG;\nlayout(location = 8) out float outPaintB;")
                // Pass paint values through
                .Replace(
                    "    outHighlighted = instanceData.Highlighted;",
                    "    outHighlighted = instanceData.Highlighted;\n\n    outPaintR = instanceData.PaintR;\n    outPaintG = instanceData.PaintG;\n    outPaintB = instanceData.PaintB;");

            // ---- Modify fragment shader ----
            fragContent = fragContent
                // Add input variables
                .Replace(
                    "layout (location = 5) in flat int inHighlighted;",
                    "layout (location = 5) in flat int inHighlighted;\nlayout (location = 6) in float inPaintR;\nlayout (location = 7) in float inPaintG;\nlayout (location = 8) in float inPaintB;")
                // Apply paint tint after albedo sampling
                .Replace(
                    "    vec3 sampledColor = gammaToLinear(texture(sampler2D(globalTextures[drawData.diffuseTextureIndex], textureSampler), inUv).xyz);",
                    "    vec3 sampledColor = gammaToLinear(texture(sampler2D(globalTextures[drawData.diffuseTextureIndex], textureSampler), inUv).xyz);\n\n    // Paint tint from per-instance data padding\n    vec3 paintTint = vec3(inPaintR, inPaintG, inPaintB);\n    if (dot(paintTint, paintTint) > 0.001) {\n        sampledColor *= paintTint;\n    }");

            File.WriteAllText(vertPath, vertContent);
            File.WriteAllText(fragPath, fragContent);

            Console.WriteLine("humble-arteest: Padding test shaders applied to vert + frag. RESTART THE GAME.");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _lastError = "Permission denied. Game may need to run as administrator.";
            return false;
        }
        catch (Exception ex)
        {
            _lastError = $"Error: {ex.Message}";
            return false;
        }
    }

    public static bool RestoreOriginalShaders()
    {
        _lastError = null;

        try
        {
            RestoreFile(GamePaths.GetShaderPath(VertRelPath));
            RestoreFile(GamePaths.GetShaderPath(FragRelPath));

            Console.WriteLine("humble-arteest: Original shaders restored. RESTART THE GAME.");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Error: {ex.Message}";
            return false;
        }
    }

    private static void BackupFile(string path)
    {
        var backup = path + BackupSuffix;
        if (!File.Exists(backup))
            File.Copy(path, backup);
    }

    private static void RestoreFile(string path)
    {
        var backup = path + BackupSuffix;
        if (File.Exists(backup))
        {
            File.Copy(backup, path, overwrite: true);
            File.Delete(backup);
        }
    }

    // ---- Harmony patches ----

    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            _addInstanceOriginal = AccessTools.Method(typeof(PartModel), nameof(PartModel.AddInstance));
            _addInstancePrefix = typeof(PaddingTest).GetMethod(
                nameof(AddInstancePrefix), BindingFlags.NonPublic | BindingFlags.Static);

            if (_addInstanceOriginal == null)
            {
                Console.WriteLine("humble-arteest: WARNING — PartModel.AddInstance not found");
                return;
            }

            harmony.Patch(_addInstanceOriginal, prefix: new HarmonyMethod(_addInstancePrefix));
            Console.WriteLine("humble-arteest: Padding test Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error applying padding test patches: {ex.Message}");
        }
    }

    public static void RemovePatches(Harmony harmony)
    {
        try
        {
            if (_addInstanceOriginal != null && _addInstancePrefix != null)
                harmony.Unpatch(_addInstanceOriginal, _addInstancePrefix);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error removing padding test patches: {ex.Message}");
        }
    }

    /// <summary>
    /// Harmony prefix on PartModel.AddInstance. When enabled, writes test color (red)
    /// into the PerInstanceData padding fields before the original method sends it to GPU.
    /// </summary>
    private static void AddInstancePrefix(ref PartModel.PerInstanceData instanceData)
    {
        if (!_enabled) return;

        ref var paintable = ref Unsafe.As<PartModel.PerInstanceData, PaintablePerInstanceData>(ref instanceData);
        paintable.PaintR = _paintR;
        paintable.PaintG = _paintG;
        paintable.PaintB = _paintB;
    }
}
