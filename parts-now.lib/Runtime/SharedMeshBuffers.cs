using System;
using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering.Raytracing;
using RenderCore;

namespace MeowSci.PartsNowLib;

/// <summary>Relocates raster mesh storage at the pre-GUI boundary, retaining all mesh offsets.</summary>
internal static class SharedMeshBuffers
{
    public static void Resize(uint vertexBytes, uint indexBytes)
    {
        // RaytracingRenderer retains device addresses, BLAS and SubPartRefs beyond Rebuild().
        // Refuse relocation rather than leave those live structures pointing at freed buffers.
        if (Program.Instance?.RaytracingRenderer != null)
            throw new InvalidOperationException("Runtime mesh buffer resizing requires starting the game without ray tracing. Texture-only loads remain available.");
        var renderer = Program.GetRenderer();
        renderer.Device.WaitIdle();
        var oldVertex = DeviceMeshInterleaved.Shared.VertexAllocation;
        var oldIndex = DeviceMeshInterleaved.Shared.IndexAllocation;
        IBufferAllocator allocator = GameSettings.Current.Graphics.IVARayTracing ? new RaytraceAllocator(renderer.Device) : renderer.Allocator;
        BufferEx vertex = allocator.CreateBuffer(Info("Vertices", vertexBytes, VkBufferUsageFlags.VertexBufferBit));
        BufferEx index;
        try { index = allocator.CreateBuffer(Info("Indices", indexBytes, VkBufferUsageFlags.IndexBufferBit)); }
        catch { vertex.Dispose(); throw; }
        try
        {
            using var staging = renderer.Allocator.CreateStagingPool(renderer.Graphics, 1);
            var command = staging.NextCommandBuffer();
            command.Begin();
            command.CopyBuffer(oldVertex.VkBuffer, vertex.VkBuffer, new ByteSize(Math.Min((uint)oldVertex.BufferSize, vertexBytes)));
            command.CopyBuffer(oldIndex.VkBuffer, index.VkBuffer, new ByteSize(Math.Min((uint)oldIndex.BufferSize, indexBytes)));
            command.End();
            staging.Submit().Wait();
        }
        catch { vertex.Dispose(); index.Dispose(); throw; }
        DeviceMeshInterleaved.Shared.VertexAllocation = vertex;
        DeviceMeshInterleaved.Shared.IndexAllocation = index;
        oldVertex.Dispose(); oldIndex.Dispose();
    }
    private static BufferEx.CreateInfo Info(string name, uint size, VkBufferUsageFlags usage) => new()
    {
        Name = "DeviceMeshInterleaved " + name,
        AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        BufferSize = new ByteSize(size),
        BufferUsage = VkBufferUsageFlags.TransferSrcBit | VkBufferUsageFlags.TransferDstBit | usage,
        BufferSharingMode = VkSharingMode.Exclusive,
    };
}
