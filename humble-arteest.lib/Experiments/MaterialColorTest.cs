using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using KSA;
using RenderCore;

namespace MeowSci.HumbleArteestLib.Experiments;

/// <summary>
/// Experiment 0.3: Material AlbedoColor Path Test
///
/// Tests whether modifying MaterialData.AlbedoColor in the GPU material buffer
/// affects the indirect rendering path (MeshIndirect.frag).
///
/// Expected: NO visible change — the indirect path reads texture indices from PerDrawData
/// directly and does NOT use MaterialSet.glsl/AlbedoColor. This would confirm Approach B
/// (material cloning) is NOT viable for the indirect path.
///
/// If AlbedoColor DOES affect parts: Approach B is viable as a simpler alternative.
/// </summary>
public static class MaterialColorTest
{
    private static string? _lastError;
    private static string? _statusMessage;
    private static bool _initialized;

    // Cached reflection handles
    private static object? _materialSystem;
    private static IDictionary? _assetMap;
    private static PropertyInfo? _bigBufferProp;
    private static FieldInfo? _deviceCtxField;

    public static string? LastError => _lastError;
    public static string? StatusMessage => _statusMessage;
    public static bool IsInitialized => _initialized && _materialSystem != null;

    /// <summary>
    /// Discovers the GpuMaterialSystem and its AssetMap via reflection.
    /// </summary>
    public static bool Initialize()
    {
        if (_initialized) return _materialSystem != null;
        _initialized = true;
        _lastError = null;

        try
        {
            // Access Program.Instance
            var programType = typeof(Part).Assembly.GetType("KSA.Program");
            if (programType == null) { _lastError = "KSA.Program type not found."; return false; }

            var instanceProp = programType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) { _lastError = "Program.Instance not found."; return false; }

            var programInstance = instanceProp.GetValue(null);
            if (programInstance == null) { _lastError = "Program.Instance is null (game not fully loaded?)."; return false; }

            // Access MaterialSystem field or property
            _materialSystem = GetFieldOrProp(programType, programInstance, "MaterialSystem");
            if (_materialSystem == null) { _lastError = "MaterialSystem not found on Program."; return false; }

            Console.WriteLine($"humble-arteest: MaterialSystem type: {_materialSystem.GetType().FullName}");

            // Walk up to find AssetMap (ConcurrentDictionary in AssetManager<T>)
            _assetMap = FindFieldInHierarchy(_materialSystem, "AssetMap") as IDictionary;
            if (_assetMap == null) { _lastError = "AssetMap not found in MaterialSystem hierarchy."; return false; }

            // Cache BigBuffer property
            _bigBufferProp = _materialSystem.GetType().GetProperty("BigBuffer",
                BindingFlags.Public | BindingFlags.Instance);

            // Cache DeviceCtx field (protected, declared on GpuObjectSystem<T>)
            _deviceCtxField = FindFieldInfoInHierarchy(_materialSystem.GetType(), "DeviceCtx");

            Console.WriteLine($"humble-arteest: Found {_assetMap.Count} materials");
            Console.WriteLine($"humble-arteest: BigBuffer accessible: {_bigBufferProp != null}");
            Console.WriteLine($"humble-arteest: DeviceCtx accessible: {_deviceCtxField != null}");

            _statusMessage = $"Initialized: {_assetMap.Count} materials found";
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Init error: {ex.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    /// <summary>Returns material names and their GPU buffer handles.</summary>
    public static (string Name, int Handle)[] GetMaterialList()
    {
        if (_assetMap == null) return Array.Empty<(string, int)>();

        try
        {
            var results = new System.Collections.Generic.List<(string, int)>();
            foreach (DictionaryEntry entry in _assetMap)
            {
                string name = entry.Key?.ToString() ?? "unknown";
                int handle = -1;

                if (entry.Value != null)
                {
                    var handleField = entry.Value.GetType().GetField("Handle",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (handleField != null)
                        handle = (int)handleField.GetValue(entry.Value)!;
                }

                results.Add((name, handle));
            }

            results.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.Ordinal));
            return results.ToArray();
        }
        catch (Exception ex)
        {
            _lastError = $"Error listing materials: {ex.Message}";
            return Array.Empty<(string, int)>();
        }
    }

    /// <summary>
    /// Modifies the AlbedoColor of a material at the given buffer handle.
    /// Replicates the GpuObjectSystem.SendToBuffer pattern but writes only
    /// the float4 AlbedoColor at the exact offset within the material slot.
    /// </summary>
    public static bool ModifyAlbedoColor(int handle, float4 newColor)
    {
        _lastError = null;

        if (_materialSystem == null || _bigBufferProp == null || _deviceCtxField == null)
        {
            _lastError = "MaterialSystem not fully initialized.";
            return false;
        }

        try
        {
            // Get BigBuffer (BufferEx) → VkBuffer
            var bigBufferObj = _bigBufferProp.GetValue(_materialSystem);
            if (bigBufferObj == null) { _lastError = "BigBuffer is null."; return false; }
            var bigBuffer = (BufferEx)bigBufferObj;

            // Get DeviceCtx (IVulkanContext) for staging pool creation
            var deviceCtxObj = _deviceCtxField.GetValue(_materialSystem);
            if (deviceCtxObj == null) { _lastError = "DeviceCtx is null."; return false; }
            var deviceCtx = (IVulkanContext)deviceCtxObj;

            // Calculate buffer offset for the AlbedoColor field within the material slot
            int materialSize = Marshal.SizeOf<MaterialData>();
            int albedoColorOffset = (int)Marshal.OffsetOf<MaterialData>(nameof(MaterialData.AlbedoColor));
            ByteSize targetOffset = handle * ByteSize.Of<MaterialData>() + albedoColorOffset;

            Console.WriteLine($"humble-arteest: MaterialData size={materialSize}, AlbedoColor offset={albedoColorOffset}");
            Console.WriteLine($"humble-arteest: Writing to handle {handle}, buffer offset={targetOffset}");

            // Stage and upload using the same pattern as GpuObjectSystem.SendToBuffer
            using var stagingPool = deviceCtx.Device.CreateStagingPool(deviceCtx.MainQueue, 1);
            var commandBuffer = stagingPool.NextCommandBuffer();

            float4 colorCopy = newColor;
            var span = new Span<float4>(ref colorCopy);

            commandBuffer.Begin();
            VkUtils.StageAndUploadToBuffer(stagingPool, bigBuffer.VkBuffer, targetOffset, MemoryMarshal.AsBytes(span), commandBuffer);
            commandBuffer.End();

            _statusMessage = $"Uploaded AlbedoColor ({newColor.X:F2}, {newColor.Y:F2}, {newColor.Z:F2}, {newColor.W:F2}) " +
                $"to material handle {handle}";
            Console.WriteLine($"humble-arteest: {_statusMessage}");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"Error modifying material: {ex.Message}";
            if (ex.InnerException != null)
                _lastError += $"\nInner: {ex.InnerException.Message}";
            Console.WriteLine($"humble-arteest: {_lastError}");
            return false;
        }
    }

    // ---- Reflection helpers ----

    private static object? GetFieldOrProp(Type type, object instance, string name)
    {
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) return field.GetValue(instance);

        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null) return prop.GetValue(instance);

        return null;
    }

    private static object? FindFieldInHierarchy(object instance, string fieldName)
    {
        var type = instance.GetType();
        while (type != null)
        {
            var field = type.GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(instance);
            type = type.BaseType;
        }
        return null;
    }

    private static FieldInfo? FindFieldInfoInHierarchy(Type? type, string fieldName)
    {
        while (type != null)
        {
            var field = type.GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        return null;
    }
}
