using System;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.Numerics;
using Brutal.Pointers.Extensions;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using RenderCore;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// Owns the per-draw GPU resources (pipeline, descriptor set, vertex/index buffers,
/// texture) needed to draw a textured unit-square quad in the scene. The model matrix
/// is rebuilt per-entry per-frame and pushed as a vertex push constant.
/// </summary>
public sealed unsafe class ThugLifeQuadRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct QuadVertex
    {
        public float3 Pos;
        public float2 Uv;
    }

    private readonly Renderer _renderer;
    private readonly ThugLifeTextureFactory _texture;

    private readonly DescriptorSetLayoutEx _descriptorSetLayout;
    private readonly DescriptorPoolEx _descriptorPool;
    private readonly VkDescriptorSet _descriptorSet;
    private readonly VkPipelineLayout _pipelineLayout;
    private readonly VkPipeline _pipeline;
    private readonly BufferEx _vb;
    private readonly BufferEx _ib;

    private bool _disposed;

    public bool IsValid => !_disposed;

    public ThugLifeQuadRenderer(Renderer renderer, ThugLifeTextureFactory texture)
    {
        _renderer = renderer;
        _texture = texture;

        var device = renderer.Device;

        var binding = new VkDescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = VkShaderStageFlags.FragmentBit,
        };
        _descriptorSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = new Span<VkDescriptorSetLayoutBinding>(ref binding),
        }, null);

        var poolSize = new VkDescriptorPoolSize
        {
            Type = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
        };
        _descriptorPool = device.CreateDescriptorPool(new DescriptorPoolEx.CreateInfo
        {
            MaxSets = 1,
            PoolSizes = new Span<VkDescriptorPoolSize>(ref poolSize),
        }, null);
        _descriptorSet = device.AllocateDescriptorSet(_descriptorPool, _descriptorSetLayout);

        var imageInfo = new VkDescriptorImageInfo
        {
            ImageView = texture.ImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
            Sampler = texture.Sampler,
        };
        VkWriteDescriptorSet write = new VkWriteDescriptorSet
        {
            DstSet = _descriptorSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            ImageInfo = &imageInfo,
        };
        device.UpdateDescriptorSets(
            new ReadOnlySpan<VkWriteDescriptorSet>(ref write),
            default(ReadOnlySpan<VkCopyDescriptorSet>));

        VkPushConstantRange pushRange = new VkPushConstantRange
        {
            StageFlags = VkShaderStageFlags.VertexBit,
            Offset = ByteSize.Zero,
            Size = ByteSize.Of<float4x4>(),
        };
        VkDescriptorSetLayout dslHandle = _descriptorSetLayout;
        _pipelineLayout = device.CreatePipelineLayout(
            new ReadOnlySpan<VkDescriptorSetLayout>(ref dslHandle),
            new ReadOnlySpan<VkPushConstantRange>(ref pushRange),
            null);

        _pipeline = BuildPipeline(device, renderer, _pipelineLayout);
        (_vb, _ib) = BuildGeometry(renderer);
    }

    private static VkPipeline BuildPipeline(DeviceEx device, Renderer renderer, VkPipelineLayout layout)
    {
        var shaderRefs = new ShaderReference[]
        {
            ModLibrary.Get<ShaderReference>("UnlitMeshVert"),
            ModLibrary.Get<ShaderReference>("UnlitMeshFrag"),
        };
        var stages = RenderTechnique.CreateShaderStages(device, shaderRefs.AsSpan());

        var vertexInput = new VertexInput(1, 2)
            .AddBinding(0, ByteSize.Of<QuadVertex>(), VkVertexInputRate.Vertex)
            .AddAttribute(0, 0, VkFormat.R32G32B32SFloat, ByteSize.Zero)
            .AddAttribute(1, 0, VkFormat.R32G32SFloat, ByteSize.Of<float3>())
            .Check();

        var multisample = new VkPipelineMultisampleStateCreateInfo
        {
            RasterizationSamples = Program.OffScreenPass.SampleCount,
        };

        var info = new VkGraphicsPipelineCreateInfo
        {
            Layout = layout,
            RenderPass = Program.OffScreenPass.Pass,
            Subpass = 0,
            StageCount = stages.Count,
            Stages = stages,
            DynamicState = renderer.DynamicStateInfo,
            ViewportState = renderer.ViewportState,
            VertexInputState = vertexInput,
            InputAssemblyState = Presets.InputAssembly.TriangleList,
            RasterizationState = Presets.Rasterization.Fill.CullNone,
            DepthStencilState = RenderingPresets.ReverseZDepthStencil.DepthTestWrite,
            ColorBlendState = Presets.BlendState.BlendColorAlpha,
            MultisampleState = &multisample,
        };
        return device.CreateGraphicsPipeline(default(VkPipelineCache), info, null);
    }

    private static (BufferEx vb, BufferEx ib) BuildGeometry(Renderer renderer)
    {
        Span<QuadVertex> verts = stackalloc QuadVertex[4]
        {
            new QuadVertex { Pos = new float3(-0.5f, -0.5f, 0f), Uv = new float2(0f, 1f) },
            new QuadVertex { Pos = new float3( 0.5f, -0.5f, 0f), Uv = new float2(1f, 1f) },
            new QuadVertex { Pos = new float3( 0.5f,  0.5f, 0f), Uv = new float2(1f, 0f) },
            new QuadVertex { Pos = new float3(-0.5f,  0.5f, 0f), Uv = new float2(0f, 0f) },
        };
        Span<ushort> indices = stackalloc ushort[6] { 0, 1, 2, 0, 2, 3 };

        var vb = renderer.Allocator.CreateBuffer(new BufferEx.CreateInfo
        {
            Name = "thug-life-vb",
            BufferUsage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit,
            BufferSize = ByteSize.Of<QuadVertex>(verts.Length),
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        });
        var ib = renderer.Allocator.CreateBuffer(new BufferEx.CreateInfo
        {
            Name = "thug-life-ib",
            BufferUsage = VkBufferUsageFlags.IndexBufferBit | VkBufferUsageFlags.TransferDstBit,
            BufferSize = ByteSize.Of<ushort>(indices.Length),
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        });

        using var staging = renderer.Allocator.CreateStagingPool(renderer.Graphics, 1);
        var cmd = staging.NextCommandBuffer();
        cmd.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);
        VkUtils.StageAndUploadToBuffer(staging, vb.VkBuffer, vb.BindOffset, verts, cmd);
        VkUtils.StageAndUploadToBuffer(staging, ib.VkBuffer, ib.BindOffset, indices, cmd);
        cmd.End();
        staging.Submit().Wait();

        return (vb, ib);
    }

    /// <summary>
    /// Records the draw for a single entry. Caller must already be inside the offscreen
    /// render pass (i.e. invoked from a postfix on <c>SuperMeshRenderSystem.RenderMainPass</c>).
    /// </summary>
    public void RecordDraw(CommandBuffer cmd, ThugLifeEntry entry)
    {
        if (_disposed || !entry.Visible) return;
        if (!TryComputeModelEgo(entry, out float4x4 modelEgo)) return;
        SubmitDraw(cmd, modelEgo);
    }

    /// <summary>
    /// Debug helper: draws the quad at a fixed offset in ego-space (camera-centered) with no
    /// rotation. Useful for proving the render pipeline actually works without depending on
    /// any vehicle/part anchor math. The +Z normal makes the quad visible from the camera
    /// side when the camera looks down -Z (so a positive Z offset puts the quad behind the
    /// camera; try a negative offset).
    /// </summary>
    public void RecordDebugDraw(CommandBuffer cmd, float3 egoOffset, float width, float height)
    {
        if (_disposed) return;
        float4x4 model = float4x4.CreateScale(width, height, 1f)
                       * float4x4.CreateTranslation(egoOffset);
        if (!TryProjectModel(model, out float4x4 mvp)) return;
        SubmitDraw(cmd, mvp);
    }

    private static bool TryProjectModel(float4x4 modelEgo, out float4x4 mvp)
    {
        mvp = float4x4.Identity;
        var camera = Program.GetMainCamera();
        if (camera == null) return false;
        mvp = modelEgo * camera.MVP.viewProjection;
        return true;
    }

    private void SubmitDraw(CommandBuffer cmd, float4x4 modelEgo)
    {
        if (!TryProjectModel(modelEgo, out float4x4 mvp)) return;

        cmd.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline);
        VkDescriptorSet setCopy = _descriptorSet;
        cmd.BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipelineLayout, 0,
            new ReadOnlySpan<VkDescriptorSet>(ref setCopy),
            default(Span<ByteSize32>));

        Program.SetViewport(cmd);
        cmd.PushConstants(_pipelineLayout, VkShaderStageFlags.VertexBit, ByteSize.Zero, mvp);

        VkBuffer vbHandle = _vb.VkBuffer;
        ByteSize64 vbOff = (ByteSize64)_vb.BindOffset;
        cmd.BindVertexBuffers(0,
            new ReadOnlySpan<VkBuffer>(ref vbHandle),
            new ReadOnlySpan<ByteSize64>(ref vbOff));
        cmd.BindIndexBuffer(_ib.VkBuffer, (ByteSize64)_ib.BindOffset, VkIndexType.Uint16);
        cmd.DrawIndexed(6, 1, 0, 0, 0);
    }

    private static bool TryComputeModelEgo(ThugLifeEntry entry, out float4x4 model)
    {
        model = float4x4.Identity;
        var camera = Program.GetMainCamera();
        if (camera == null) return false;
        if (entry.Vehicle == null || entry.Part == null) return false;

        double4x4 vehMat = entry.Vehicle.GetMatrixAsmb2Ego(camera);
        double3 partPos = entry.Part.PositionEgo(in vehMat);
        doubleQuat partRot = entry.Part.Asmb2Ego(entry.Vehicle.Asmb2Ego);

        float4x4 partRotMat = float4x4.CreateFromQuaternion(floatQuat.Pack(in partRot));
        float4x4 partTransMat = float4x4.CreateTranslation(float3.Pack(in partPos));
        float4x4 partEgo = partRotMat * partTransMat;

        const float deg2rad = MathF.PI / 180f;
        float4x4 userRot = float4x4.CreateRotationX(entry.Rotation.X * deg2rad)
                         * float4x4.CreateRotationY(entry.Rotation.Y * deg2rad)
                         * float4x4.CreateRotationZ(entry.Rotation.Z * deg2rad);
        float4x4 userTrans = float4x4.CreateTranslation(entry.Position);
        float4x4 scaleMat = float4x4.CreateScale(entry.Width, entry.Height, 1f);

        // v_local → scale → userRot → userTrans → partEgo → ego
        model = scaleMat * userRot * userTrans * partEgo;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var device = _renderer.Device;
        try { _vb.Dispose(); } catch { }
        try { _ib.Dispose(); } catch { }
        try { device.DestroyPipeline(_pipeline, null); } catch { }
        try { device.DestroyPipelineLayout(_pipelineLayout, null); } catch { }
        try { _descriptorPool.Dispose(); } catch { }
        try { _descriptorSetLayout.Dispose(); } catch { }
    }
}
