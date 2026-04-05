# Subparts Go Brrrr — VRAM Optimization Plan

## Problem Statement

The `inanimate-carbon-rod` mod generates Vulkan thumbnail images for every subpart's `PartTemplate`. Each image is allocated in GPU device-local VRAM (`VkMemoryPropertyFlags.DeviceLocalBit`). At 32 views × 512px resolution, a single subpart costs ~85 MB of VRAM. With 50+ subparts this can exceed available VRAM, especially on systems running KSA at high graphics settings that already consume most of the Vulkan memory budget.

## Goals

Three independent, incremental optimizations — each reduces VRAM per thumbnail and can be implemented/tested in isolation:

| Task | Technique | VRAM Savings | Complexity |
|------|-----------|-------------|------------|
| **Task 1** | Eliminate mip chains (`ImageMipLevels = 1`) | ~25% | Trivial |
| **Task 2** | R8G8B8A8UNorm post-blit (LDR conversion) | ~50% | Medium |
| **Task 3** | Render-to-CPU + LRU GPU cache | ~95%+ (bounded) | High |

Each task stands alone. Implement any one, any combination, or all three in any order.

---

## Repository & Code Layout

### Key Files (all paths relative to repo root)

| File | Purpose |
|------|---------|
| `inanimate-carbon-rod.lib/SubpartThumbnailGenerator.cs` | Bulk renderer — renders all subpart thumbnails, one batch per frame |
| `inanimate-carbon-rod.lib/SingleSubpartGenerator.cs` | Single-subpart renderer — generates hi-res views for viewing a single subpart |
| `inanimate-carbon-rod.lib/SubpartThumbnailCache.cs` | Static dictionary holding `SubpartThumbnailEntry` keyed by `PartTemplate.Id` |
| `inanimate-carbon-rod.lib/InanimateCarbonRodSubmod.cs` | ImGui UI — grid display, filter, virtual scrolling, descriptor management |
| `inanimate-carbon-rod.lib/SubpartViewerWindow.cs` | Single-subpart detail viewer with animation and hi-res regeneration |
| `inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj` | Library csproj with Vulkan DLL references |

### Key Decompiled Game Sources (read-only reference)

| File | Purpose |
|------|---------|
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailReference.cs` | Game type — wraps `ImageViewEx` + ImGui descriptor |
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailRenderer.cs` | Game type — Vulkan render pass, framebuffer, mip generation |
| `decomp/ksa/KSA.Rendering.Thumbnails/PostPassThumbnailCommand.cs` | Game type — post-render image copy with mip chain |
| `decomp/ksa/KSA.Rendering/ThumbnailCreator.cs` | Game's own thumbnail generation flow |
| `decomp/ksa/KSA.Rendering.Water.Rendering/OceanFFT.cs` | **GPU readback pattern** — `CopyImageToBuffer` + host-visible `BufferEx` + `Map()` |

### DLL Dependencies (already referenced in `.lib.csproj`)

- `Brutal.Vulkan.dll` — Vulkan wrapper (`DeviceEx`, `CommandBuffer`, `BufferEx`, etc.)
- `Brutal.Vulkan.Abstractions.dll` — Types (`ImageEx`, `ImageViewEx`, `VkFormat`, etc.)
- `KSA.dll` — Game types (`ThumbnailRenderer`, `ThumbnailReference`, etc.)

---

## Shared Context: Current Thumbnail VRAM Allocation

Every thumbnail image is created with this exact pattern (found in both `SubpartThumbnailGenerator.RenderViewToImage` and `SingleSubpartGenerator`):

```csharp
int size = ThumbnailRenderer.SIZE;               // = GameSettings.Current.Graphics.PartThumbnailSize (default 512)
int mipLevels = (int)Math.Floor(Math.Log2(size)) + 1; // 10 levels for 512px

var thumb = new ThumbnailReference();
thumb.CreateImageView(
    renderer.Device,
    new ImageEx.CreateInfo
    {
        Name = imageName,
        AllocPreference = MemoryPreference.PreferGpu,        // → DeviceLocalBit (pure VRAM)
        ImageArrayLayers = 1,
        ImageInitialLayout = VkImageLayout.Undefined,
        ImageType = VkImageType._2D,
        ImageExtent = new VkExtent3D { Width = size, Height = size, Depth = 1 },
        ImageUsage = VkImageUsageFlags.TransferSrcBit
                   | VkImageUsageFlags.TransferDstBit
                   | VkImageUsageFlags.SampledBit
                   | VkImageUsageFlags.ColorAttachmentBit,
        ImageFormat = ThumbnailRenderer.ColorFormat,          // VkFormat.R16G16B16A16SFloat (8 bytes/pixel)
        ImageMipLevels = mipLevels,                           // 10 mip levels for 512px
        ImageSamples = VkSampleCountFlags._1Bit,
        ImageSharingMode = VkSharingMode.Exclusive,
        ImageTiling = VkImageTiling.Optimal
    },
    VkImageViewType._2D,
    new VkImageSubresourceRange
    {
        AspectMask = VkImageAspectFlags.ColorBit,
        BaseMipLevel = 0,
        LevelCount = mipLevels,
        BaseArrayLayer = 0,
        LayerCount = 1
    });
```

### VRAM cost formula

```
bytes_per_pixel = 8 (R16G16B16A16SFloat)
mip_multiplier  = Σ(1/4^i for i in 0..mipLevels-1)  ≈  1.333 for full chain
vram_per_image  = size² × bytes_per_pixel × mip_multiplier

Example: 512² × 8 × 1.333 = ~2.79 MB per image
With 32 views × 50 subparts: ~4.5 GB VRAM
```

---

# Task 1: Eliminate Mip Chains (`ImageMipLevels = 1`)

## Rationale

Thumbnails are displayed in ImGui at small display sizes (32–256 px), sampled with `Program.LinearClampedSampler`. Mipmaps exist to avoid aliasing when textures are rendered at sizes much smaller than their native resolution — but for thumbnails that are already small or displayed near their native size, the visual benefit is negligible. Setting `ImageMipLevels = 1` saves ~25% VRAM immediately and simplifies the render pipeline slightly.

## VRAM Impact

| Setting | With mips | Without mips | Savings |
|---------|-----------|-------------|---------|
| 512px | ~2.79 MB/image | ~2.10 MB/image | 25% |
| 128px | ~174 KB/image | ~131 KB/image | 25% |

## Changes Required

### 1.1 — `SubpartThumbnailGenerator.cs` — `RenderViewToImage` method

**Location:** The `RenderViewToImage` static method, approximately lines 280–350.

**What to change:**

Replace the mip level calculation with a constant `1`:

```csharp
// BEFORE:
int mipLevels = (int)Math.Floor(Math.Log2(size)) + 1;

// AFTER:
int mipLevels = 1;
```

This single-line change affects:
- The `ImageEx.CreateInfo.ImageMipLevels` field (passed to `CreateImageView`)
- The `VkImageSubresourceRange.LevelCount` field (passed to `CreateImageView`)

Both already use the local `mipLevels` variable, so changing the one declaration propagates to both.

**Verification:** The existing `PostPassThumbnailCommand.CopyImageWithMips` already handles `mipLevels == 1` correctly — see `decomp/ksa/KSA.Rendering.Thumbnails/PostPassThumbnailCommand.cs` lines 30–56 where it has an explicit `if (mipLevels == 1)` fast path that does a single `VkImageCopy` instead of iterating. Likewise, `ThumbnailRenderer.RenderThumbnail` (lines 310–320 of `ThumbnailRenderer.cs`) has an `if (MipLevels == 1)` branch that skips `GenerateMipmaps` and does a direct `ColorAttachmentWrite → SampledReadFragment` transition.

**HOWEVER**: `ThumbnailRenderer.MipLevels` and `ThumbnailRenderer.SIZE` are static properties derived from `GameSettings.Current.Graphics.PartThumbnailSize`. The mod already temporarily overrides `PartThumbnailSize` during generation (saved/restored in `BeginGeneration`/`CleanupGenerationResources`). This means `ThumbnailRenderer.MipLevels` is computed from the overridden size and the `ThumbnailRenderer`'s internal render target images are created with those mip levels.

**The issue**: `ThumbnailRenderer` creates its own internal color image with `MipLevels` mip levels in its constructor (see `ThumbnailRenderer.cs` line 98). The internal `GenerateMipmaps` call and `PostPassThumbnailCommand` both use `ThumbnailRenderer.MipLevels` (the static property). So the render pass itself generates mips on the _renderer's internal image_ before copying to the final destination image.

**The fix is therefore focused on the DESTINATION image only**: Change only the `mipLevels` variable used for the `ImageEx.CreateInfo` and `VkImageSubresourceRange` when creating the `ThumbnailReference` that holds the final thumbnail. The `PostPassThumbnailCommand` copies from the renderer's internal mipped color image into the destination — if the destination has `MipLevels = 1`, only mip 0 gets copied (confirmed by `CopyImageWithMips` checking `ThumbnailRenderer.MipLevels`).

**Wait — problem**: `PostPassThumbnailCommand.CopyImageWithMips` reads `ThumbnailRenderer.MipLevels` (the static property), NOT the destination image's mip level count. It will try to copy all mip levels from the source into the destination. If the destination only has 1 mip level, this generates a Vulkan validation error when writing mip levels that don't exist.

**Correct approach**: We need to write a custom post-pass copy that copies only mip 0. The mod already uses a custom post-pass struct pattern:

```csharp
thumbRenderer.RenderThumbnail(
    new PrePassThumbnailCommand(viewport, frameIndex, ...),
    new PassThumbnailCommand(viewport, frameIndex),
    new PostPassThumbnailCommand(thumbRenderer, subpart, ...),  // ← this is the game's struct
    subpart.Id,
    out VkFence fence);
```

**Solution**: Create a new `SingleMipPostPassCommand` struct implementing `IRenderCommandRecord` that copies only mip level 0, and use it instead of `PostPassThumbnailCommand` when generating single-mip thumbnails.

### 1.2 — New file: `inanimate-carbon-rod.lib/SingleMipPostPassCommand.cs`

Create a new `readonly struct` implementing `IRenderCommandRecord`:

```csharp
using System;
using Brutal.VulkanApi;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Post-pass command that copies only mip level 0 from the ThumbnailRenderer's
/// internal color image into the destination ThumbnailReference image.
/// Used when destination images are allocated with ImageMipLevels = 1.
/// </summary>
public readonly struct SingleMipPostPassCommand : IRenderCommandRecord
{
    private readonly ThumbnailRenderer _renderer;
    private readonly PartTemplate _template;
    private readonly AtmosphereRenderer _atmosphereRenderer;

    public SingleMipPostPassCommand(
        ThumbnailRenderer inRenderer,
        PartTemplate inTemplate,
        AtmosphereRenderer inAtmosphereRenderer)
    {
        _renderer = inRenderer;
        _template = inTemplate;
        _atmosphereRenderer = inAtmosphereRenderer;
    }

    public unsafe void ProcessCommands(CommandBuffer inCommandBuffer)
    {
        VkImage src = _renderer.ColorImage;
        VkImage dst = _template.Thumbnail.ImageView.Image.VkImage;
        int size = ThumbnailRenderer.SIZE;

        // Transition: src → TransferSrc (all mips, since renderer generated them)
        //             dst → TransferDst (mip 0 only)
        // Use Span-based TransitionImages2 with 2 transitions
        Span<ImageTransition> transitions = stackalloc ImageTransition[2];
        transitions[0] = new ImageTransition(
            src,
            ImageBarrierInfo.Presets.SampledReadFragment,
            ImageBarrierInfo.Presets.TransferSrc,
            ImageTransition.Subresource(VkImageAspectFlags.ColorBit, 0, ThumbnailRenderer.MipLevels));
        transitions[1] = new ImageTransition(
            dst,
            ImageBarrierInfo.Presets.Undefined,
            ImageBarrierInfo.Presets.TransferDst);  // default subresource = mip 0, count 1
        inCommandBuffer.TransitionImages2(transitions);

        // Copy only mip 0
        VkImageCopy region = new VkImageCopy
        {
            SrcSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1
            },
            DstSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1
            },
            SrcOffset = new VkOffset3D(0, 0, 0),
            DstOffset = new VkOffset3D(0, 0, 0),
            Extent = new VkExtent3D(size, size, 1)
        };
        inCommandBuffer.CopyImage(
            src, VkImageLayout.TransferSrcOptimal,
            dst, VkImageLayout.TransferDstOptimal,
            new Span<VkImageCopy>(&region, 1));

        // Transition both to shader-readable
        Span<ImageTransition> finals = stackalloc ImageTransition[2];
        finals[0] = new ImageTransition(dst, ImageBarrierInfo.Presets.TransferDst, ImageBarrierInfo.Presets.SampledReadFragment);
        finals[1] = new ImageTransition(src, ImageBarrierInfo.Presets.TransferSrc, ImageBarrierInfo.Presets.SampledReadFragment,
            ImageTransition.Subresource(VkImageAspectFlags.ColorBit, 0, ThumbnailRenderer.MipLevels));
        inCommandBuffer.TransitionImages2(finals);

        _atmosphereRenderer.TransitionLuts(inCommandBuffer, ImageBarrierInfo.Presets.SampledReadFragment, ImageBarrierInfo.Presets.StorageWriteCompute);
    }
}
```

**Implementation note regarding `Span<ImageTransition>` vs game's inline array pattern**: The decompiled game code uses compiler-generated `InlineArray` types for fixed-size spans. In our mod code we can use `stackalloc` with `Span<T>` or manually create arrays. The `TransitionImages2` method takes `ReadOnlySpan<ImageTransition>`. A `Span<T>` implicitly converts to `ReadOnlySpan<T>`, so `stackalloc` works. **If `stackalloc` does not compile for `ImageTransition`** (it requires the type to be unmanaged), fall back to allocating a small array: `var transitions = new ImageTransition[2];` and pass `new ReadOnlySpan<ImageTransition>(transitions)`.

### 1.3 — `SubpartThumbnailGenerator.cs` — Use `SingleMipPostPassCommand`

In `RenderViewToImage`, replace the `PostPassThumbnailCommand` usage:

```csharp
// BEFORE:
thumbRenderer.RenderThumbnail(
    new PrePassThumbnailCommand(/* ... */),
    new PassThumbnailCommand(viewport, frameIndex),
    new PostPassThumbnailCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer),
    subpart.Id,
    out VkFence fence);

// AFTER:
thumbRenderer.RenderThumbnail(
    new PrePassThumbnailCommand(/* ... */),
    new PassThumbnailCommand(viewport, frameIndex),
    new SingleMipPostPassCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer),
    subpart.Id,
    out VkFence fence);
```

### 1.4 — `SingleSubpartGenerator.cs` — Same changes

Apply the identical `mipLevels = 1` and `SingleMipPostPassCommand` changes to `SingleSubpartGenerator`'s rendering method. The flow is identical, just in a different file.

Find the method that creates the `ThumbnailReference` (look for the `ImageEx.CreateInfo` block — it's in a method called something like `RenderSubpartViews` or inline in `StepGeneration`). Apply both the `mipLevels = 1` change and the `SingleMipPostPassCommand` swap.

### 1.5 — Testing

1. Run `dotnet build` — must compile cleanly
2. Launch KSA with the mod, open the Inanimate Carbon Rod window (F10 or F11 via Grant)
3. Generate thumbnails at various resolutions (64, 128, 256, 512)
4. Verify thumbnails display correctly in the grid — no black images, no visual artifacts
5. Verify the SubpartViewerWindow works — open a subpart, verify all views render
6. Verify hi-res regeneration in the viewer still works
7. Check KSA console output for Vulkan validation errors (any line containing "validation" or "ERROR")

---

# Task 2: R8G8B8A8UNorm Post-Blit (LDR Conversion)

## Rationale

The game renders thumbnails in `VkFormat.R16G16B16A16SFloat` — 16-bit floating point per channel (8 bytes/pixel). This HDR format is needed for the render pass but wasteful for storage. Thumbnails are small UI previews that don't need HDR precision. By blitting the rendered HDR result into an `R8G8B8A8UNorm` image (4 bytes/pixel), we halve VRAM per thumbnail. The Vulkan `vkCmdBlitImage` command performs the format conversion in hardware.

## VRAM Impact

| With mips | R16G16B16A16SFloat | R8G8B8A8UNorm | Savings |
|-----------|-------------------|---------------|---------|
| Yes (from Task 1 = 1 mip) | size² × 8 bytes | size² × 4 bytes | 50% |
| Yes (full chain) | size² × 8 × 1.33 | size² × 4 × 1.33 | 50% |

Combined with Task 1: **~62.5% total savings** (25% from no mips + 50% of remainder from LDR).

## Design

**Current flow:**
1. `ThumbnailRenderer` renders into its internal R16G16B16A16SFloat color image
2. `PostPassThumbnailCommand` copies (same format) into the destination `ThumbnailReference` image
3. Destination stays in VRAM forever

**New flow:**
1. `ThumbnailRenderer` renders into its internal R16G16B16A16SFloat color image (unchanged)
2. Our custom post-pass copies mip 0 from the renderer into the destination `ThumbnailReference` image
3. **NEW**: The destination `ThumbnailReference` is created with `VkFormat.R8G8B8A8UNorm` instead of `R16G16B16A16SFloat`
4. **NEW**: The copy is done via `CommandBuffer.BlitImage()` (which supports format conversion) instead of `CommandBuffer.CopyImage()` (which requires matching formats)

## Important Constraint

`VkCmdCopyImage` requires source and destination to have the same format. Since we're changing the destination format, we MUST use `VkCmdBlitImage` for the data transfer. The game already uses `BlitImage` in `ThumbnailRenderer.GenerateMipmaps()` — see `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailRenderer.cs` line 363:

```csharp
inCommandBuffer.BlitImage(inImg, VkImageLayout.TransferSrcOptimal, inImg,
    VkImageLayout.TransferDstOptimal, new Span<VkImageBlit>(&vkImageBlit2, 1), VkFilter.Linear);
```

## Changes Required

### 2.1 — New file: `inanimate-carbon-rod.lib/LdrPostPassCommand.cs`

Create a new post-pass command struct that performs an HDR-to-LDR blit:

```csharp
using System;
using Brutal.VulkanApi;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Post-pass command that blits from the ThumbnailRenderer's R16G16B16A16SFloat
/// color image into the destination ThumbnailReference image which uses R8G8B8A8UNorm.
/// Performs HDR → LDR conversion via VkCmdBlitImage hardware format conversion.
/// Only copies mip level 0.
/// </summary>
public readonly struct LdrPostPassCommand : IRenderCommandRecord
{
    private readonly ThumbnailRenderer _renderer;
    private readonly PartTemplate _template;
    private readonly AtmosphereRenderer _atmosphereRenderer;

    public LdrPostPassCommand(
        ThumbnailRenderer inRenderer,
        PartTemplate inTemplate,
        AtmosphereRenderer inAtmosphereRenderer)
    {
        _renderer = inRenderer;
        _template = inTemplate;
        _atmosphereRenderer = inAtmosphereRenderer;
    }

    public unsafe void ProcessCommands(CommandBuffer inCommandBuffer)
    {
        VkImage src = _renderer.ColorImage;
        VkImage dst = _template.Thumbnail.ImageView.Image.VkImage;
        int size = ThumbnailRenderer.SIZE;

        // Transition source to TransferSrc, destination to TransferDst
        Span<ImageTransition> preps = stackalloc ImageTransition[2];
        preps[0] = new ImageTransition(
            src,
            ImageBarrierInfo.Presets.SampledReadFragment,
            ImageBarrierInfo.Presets.TransferSrc,
            ImageTransition.Subresource(VkImageAspectFlags.ColorBit, 0, ThumbnailRenderer.MipLevels));
        preps[1] = new ImageTransition(
            dst,
            ImageBarrierInfo.Presets.Undefined,
            ImageBarrierInfo.Presets.TransferDst);
        inCommandBuffer.TransitionImages2(preps);

        // Blit mip 0 with format conversion (R16G16B16A16SFloat → R8G8B8A8UNorm)
        VkImageBlit blit = new VkImageBlit
        {
            SrcSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1
            },
            DstSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1
            }
        };
        // SrcOffsets[0] = (0,0,0), SrcOffsets[1] = (size,size,1)
        // DstOffsets[0] = (0,0,0), DstOffsets[1] = (size,size,1)
        // VkImageBlit uses VkOffset3D_2 (inline array of 2 VkOffset3D) for offsets.
        // Set them via the game's inline array helpers or directly:
        //   blit.SrcOffsets = { {0,0,0}, {size,size,1} }
        //   blit.DstOffsets = { {0,0,0}, {size,size,1} }
        //
        // The decompiled pattern from ThumbnailRenderer.GenerateMipmaps:
        //   VkOffset3D_2 buffer2 = default(VkOffset3D_2);
        //   InlineArrayFirstElementRef<...>(ref buffer2) = new VkOffset3D(0, 0, 0);
        //   InlineArrayElementRef<...>(ref buffer2, 1) = new VkOffset3D(width, height, 1);
        //   blit.SrcOffsets = buffer2;
        //
        // Since VkOffset3D_2 is an InlineArray, the simplest approach may be
        // to use unsafe code with fixed-size buffers, or to try the following:
        //
        // If VkOffset3D_2 has a public indexer:
        //   blit.SrcOffsets[0] = new VkOffset3D(0, 0, 0);
        //   blit.SrcOffsets[1] = new VkOffset3D(size, size, 1);
        //
        // If not, create the struct manually following the InlineArray pattern.
        // The key insight: VkOffset3D_2 is just two VkOffset3D values packed together.
        // You can zero-initialize and set via element access or pointer manipulation.
        //
        // RECOMMENDED APPROACH: Create helper that mimics the game pattern:
        var srcOffsets = new VkOffset3D_2();
        // Try indexer access first. If it doesn't compile, use unsafe pointer cast:
        //   unsafe { VkOffset3D* p = (VkOffset3D*)&srcOffsets; p[0] = ...; p[1] = ...; }
        // Or try: srcOffsets = default; and set elements.
        //
        // At minimum the following should work since VkOffset3D_2 is [InlineArray(2)]:
        srcOffsets[0] = new VkOffset3D(0, 0, 0);
        srcOffsets[1] = new VkOffset3D(size, size, 1);
        blit.SrcOffsets = srcOffsets;

        var dstOffsets = new VkOffset3D_2();
        dstOffsets[0] = new VkOffset3D(0, 0, 0);
        dstOffsets[1] = new VkOffset3D(size, size, 1);
        blit.DstOffsets = dstOffsets;

        inCommandBuffer.BlitImage(
            src, VkImageLayout.TransferSrcOptimal,
            dst, VkImageLayout.TransferDstOptimal,
            new Span<VkImageBlit>(&blit, 1),
            VkFilter.Linear);

        // Transition both back to shader-readable
        Span<ImageTransition> finals = stackalloc ImageTransition[2];
        finals[0] = new ImageTransition(dst, ImageBarrierInfo.Presets.TransferDst, ImageBarrierInfo.Presets.SampledReadFragment);
        finals[1] = new ImageTransition(src, ImageBarrierInfo.Presets.TransferSrc, ImageBarrierInfo.Presets.SampledReadFragment,
            ImageTransition.Subresource(VkImageAspectFlags.ColorBit, 0, ThumbnailRenderer.MipLevels));
        inCommandBuffer.TransitionImages2(finals);

        _atmosphereRenderer.TransitionLuts(inCommandBuffer, ImageBarrierInfo.Presets.SampledReadFragment, ImageBarrierInfo.Presets.StorageWriteCompute);
    }
}
```

**Note on `stackalloc` vs array**: See Task 1 note. If `ImageTransition` is not unmanaged, use `new ImageTransition[2]` instead of `stackalloc`.

**Note on `VkOffset3D_2`**: This is an `[InlineArray(2)]` struct generated by the compiler. If the indexer doesn't compile, use `unsafe` pointer arithmetic: `VkOffset3D* p = (VkOffset3D*)&srcOffsets; p[0] = ...; p[1] = ...;`

### 2.2 — `SubpartThumbnailGenerator.cs` — Change image format and post-pass

**In `RenderViewToImage`**, make two changes:

**(a) Change image format:**

```csharp
// BEFORE:
ImageFormat = ThumbnailRenderer.ColorFormat,  // R16G16B16A16SFloat

// AFTER:
ImageFormat = VkFormat.R8G8B8A8UNorm,
```

**(b) Change mip levels (if not already done via Task 1):**

```csharp
// Set mipLevels = 1 (or keep the existing value if Task 1 was not done)
int mipLevels = 1;  // recommended
```

**(c) Change image usage flags** — the destination no longer needs `ColorAttachmentBit` (it's never a render target), but it DOES need `TransferDstBit` (it's a blit destination):

```csharp
// BEFORE:
ImageUsage = VkImageUsageFlags.TransferSrcBit
           | VkImageUsageFlags.TransferDstBit
           | VkImageUsageFlags.SampledBit
           | VkImageUsageFlags.ColorAttachmentBit,

// AFTER:
ImageUsage = VkImageUsageFlags.TransferDstBit
           | VkImageUsageFlags.SampledBit,
```

**(d) Swap the post-pass command:**

```csharp
// BEFORE (or after Task 1):
new SingleMipPostPassCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer)
// or:
new PostPassThumbnailCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer)

// AFTER:
new LdrPostPassCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer)
```

### 2.3 — `SingleSubpartGenerator.cs` — Same changes

Apply the identical format, mip level, usage flag, and post-pass command changes.

### 2.4 — Verify `ImGui.Image` works with `R8G8B8A8UNorm`

The existing `CreateImGuiThumbnail` on `ThumbnailReference` calls:
```csharp
ImGuiBackend.Vulkan.AddTexture(inSampler, ImageView.VkImageView);
```

This registers the `VkImageView` for use in `ImGui.Image()`. ImGui's Vulkan backend should handle `R8G8B8A8UNorm` correctly — it's the standard LDR format that ImGui typically expects. No changes should be needed in the UI code.

### 2.5 — Important: `VkFormat` support for BlitImage

Vulkan requires that both source and destination formats support `VK_FORMAT_FEATURE_BLIT_SRC_BIT` / `VK_FORMAT_FEATURE_BLIT_DST_BIT` respectively. `R16G16B16A16SFloat` (blit source) and `R8G8B8A8UNorm` (blit destination) are universally supported for blit operations on all Vulkan implementations. This is safe.

### 2.6 — Testing

1. `dotnet build` — must compile
2. Generate thumbnails — verify they appear correctly (colors should look identical, just 8-bit precision)
3. Verify no banding artifacts in the grid or viewer (LDR thumbnails of 3D parts should look fine)
4. Check console for Vulkan validation errors
5. To quantify VRAM savings: compare with and without the change using a GPU monitoring tool (e.g., `nvidia-smi` or Task Manager GPU memory), or add logging: `Console.WriteLine($"icr: Allocated {count} images @ R8G8B8A8UNorm ({size}px, no mips = {count * size * size * 4} bytes)");`

---

# Task 3: Render-to-CPU + LRU GPU Cache

## Rationale

Even with LDR format and no mips, persistent VRAM grows linearly with subpart count × view count. The only way to **bound** VRAM usage regardless of subpart count is to keep the pixel data in system RAM and maintain a fixed-size pool of GPU images that are uploaded on demand.

## VRAM Impact

| Component | VRAM Usage |
|-----------|-----------|
| GPU image pool (e.g. 128 slots × 128px R8G8B8A8UNorm) | ~8 MB fixed |
| Staging buffer (one, reusable) | ~65 KB (128×128×4) |
| **Total VRAM: bounded constant** | **~8 MB** |

System RAM: ~4 bytes/pixel × size² × views × subparts. E.g. 50 subparts × 32 views × 128² × 4 = ~105 MB system RAM — well within budget.

## Design Overview

### New Components

1. **`CpuThumbnailData`** — holds `byte[]` pixel data for all views of one subpart (lives in system RAM)
2. **`CpuThumbnailCache`** — replaces `SubpartThumbnailCache` as the main storage, keyed by subpart ID, stores `CpuThumbnailData`
3. **`GpuThumbnailPool`** — fixed-size pool of reusable `ThumbnailReference` GPU images with LRU eviction
4. **Readback pipeline** — after rendering a thumbnail to GPU, immediately copy pixel data to CPU via staging buffer, then release the GPU image back to the pool

### Rendering Pipeline (Generation Phase)

```
for each subpart view:
  1. Render to ThumbnailRenderer's internal framebuffer (existing)
  2. Blit/copy into a temporary GPU image from the pool (existing post-pass)
  3. NEW: CopyImageToBuffer → staging buffer (device-local or host-visible)
  4. NEW: WaitForFence (GPU sync — already done)
  5. NEW: Read staging buffer into byte[] (CPU memory)
  6. NEW: Return the temporary GPU image to the pool
  7. Store byte[] in CpuThumbnailCache
```

### Display Pipeline (UI Phase — per frame)

```
for each visible thumbnail in the scroll view:
  1. Check if a GPU image is already assigned in the pool (cache hit → use it)
  2. If not: evict LRU entry, upload byte[] from CpuThumbnailCache into the evicted GPU image
  3. CreateImGuiThumbnail if not already registered
  4. ImGui.Image(...)
```

### Game-Proven Readback Pattern

The game's `OceanFFT.cs` uses this exact approach for ocean displacement readback. Key code from `decomp/ksa/KSA.Rendering.Water.Rendering/OceanFFT.cs`:

**Creating host-visible readback buffer (line 241):**
```csharp
_displacementReadbackBufferArray[i] = _renderer.Device.CreateBuffer(new BufferEx.CreateInfo
{
    Name = "OceanRenderer._displacementReadbackBufferArray" + i,
    BufferUsage = VkBufferUsageFlags.TransferDstBit,
    BufferSize = _readBackBufferSize,
    AllocRequiredProperties = (VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit)
});
_displacementReadBackMemoryArray[i] = _displacementReadbackBufferArray[i].Map();
```

**Copying image to buffer (line 610):**
```csharp
VkBufferImageCopy copyRegion = new VkBufferImageCopy
{
    BufferOffset = ByteSize.Zero,
    BufferRowLength = 0,
    BufferImageHeight = 0,
    ImageSubresource = new VkImageSubresourceLayers
    {
        AspectMask = VkImageAspectFlags.ColorBit,
        MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1
    },
    ImageOffset = new VkOffset3D { X = 0, Y = 0, Z = 0 },
    ImageExtent = new VkExtent3D { Width = size, Height = size, Depth = 1 }
};
inCommandBuffer.CopyImageToBuffer(image, VkImageLayout.General, buffer, new ReadOnlySpan<VkBufferImageCopy>(in copyRegion));
```

**Note**: The `OceanFFT` pattern uses two buffers (device-local intermediate + host-visible readback) with an explicit CopyBuffer between them. For our case, we can likely use a **single host-visible buffer** and `CopyImageToBuffer` directly into it (the image → host-visible buffer transfer should work — it's just slightly slower than going through a device-local intermediate on some GPUs). If there are issues, fall back to the two-buffer pattern.

**Reading data from mapped buffer:**
The `BufferEx.Map()` returns a `Ptr` (which is a thin wrapper around `IntPtr`). You can copy from it using `Marshal.Copy` or `Span<byte>` from the pointer:

```csharp
Ptr mappedPtr = buffer.Map();
// BufferSize is a ByteSize — get its value in bytes
int byteCount = size * size * bytesPerPixel;
byte[] cpuData = new byte[byteCount];
unsafe { new Span<byte>((void*)mappedPtr.Value, byteCount).CopyTo(cpuData); }
buffer.Unmap(); // or keep mapped for reuse
```

**Note on `Ptr` type**: The `Ptr` type is from `Brutal.Pointers`. It wraps an `IntPtr`. Access the raw pointer via `.Value` or cast to `void*` in unsafe context. The `Map()` method on `BufferEx` maps the Vulkan device memory and returns the CPU-accessible pointer.

## Changes Required

### 3.1 — New file: `inanimate-carbon-rod.lib/CpuThumbnailData.cs`

```csharp
namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// CPU-side pixel data for all rotation views of a single subpart.
/// Pixel format: R8G8B8A8UNorm (4 bytes/pixel), no mip chain.
/// </summary>
public sealed class CpuThumbnailData
{
    /// <summary>Pixel data for each rotation view. Index = view number.</summary>
    public byte[][] Views { get; }

    /// <summary>Image width/height in pixels (square).</summary>
    public int Size { get; }

    public CpuThumbnailData(byte[][] views, int size)
    {
        Views = views;
        Size = size;
    }
}
```

### 3.2 — New file: `inanimate-carbon-rod.lib/CpuThumbnailCache.cs`

Replaces the role of `SubpartThumbnailCache` for the CPU-side data. `SubpartThumbnailCache` should be kept as-is for backward compatibility (other mods may reference it), but it will hold at most the currently-visible GPU images rather than all thumbnails.

```csharp
using System.Collections.Generic;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// CPU-side cache of thumbnail pixel data, keyed by PartTemplate.Id.
/// All data lives in managed byte arrays (system RAM, not VRAM).
/// </summary>
public static class CpuThumbnailCache
{
    private static readonly Dictionary<string, CpuThumbnailData> _data = new();

    public static IReadOnlyDictionary<string, CpuThumbnailData> All => _data;
    public static bool HasAny => _data.Count > 0;

    public static CpuThumbnailData? Get(string subpartId)
        => _data.GetValueOrDefault(subpartId);

    internal static void Store(string id, CpuThumbnailData data)
        => _data[id] = data;

    internal static void Clear()
        => _data.Clear();
}
```

### 3.3 — New file: `inanimate-carbon-rod.lib/GpuThumbnailPool.cs`

Fixed-size LRU pool of reusable GPU images. Each slot is a `ThumbnailReference` that can be re-uploaded with new pixel data.

**Design details:**

- Pool size is configurable (e.g. 128 slots)
- Each slot holds one `ThumbnailReference` (one GPU image, one ImGui descriptor)
- LRU tracking: maintain a linked list ordered by last-use time
- When a thumbnail needs to be displayed:
  - If it's already in a pool slot → return it (cache hit), move to front of LRU
  - If not → evict the LRU tail slot, upload new pixel data, return it

**Upload mechanism**: To upload `byte[]` data into an existing Vulkan image, we need a **staging buffer** + `CopyBufferToImage` command. The game's `CommandBuffer` type exposes `CopyBufferToImage` (complementary to `CopyImageToBuffer` found in OceanFFT).

**IMPORTANT**: `CopyBufferToImage` was NOT found by search in the decompiled code. However, `CopyImageToBuffer` IS present, which means the `CommandBuffer` wrapper likely exposes both (they're both standard Vulkan commands). The `CommandBuffer` type wraps raw Vulkan — `CopyBufferToImage` is a standard Vulkan command (`vkCmdCopyBufferToImage`) and the C# wrapper almost certainly exposes it symmetrically. **If `CopyBufferToImage` is not available on `CommandBuffer`**, you will need to find the right method name by inspecting the `CommandBuffer` type at runtime using reflection (dump all public methods), or check if it's named differently.

**Pool lifecycle:**
- Created once during mod initialization (or lazily on first generate)
- All GPU images in the pool are created at the same resolution (the generation resolution)
- On `Reset()` or `Dispose()`, all pool slots are disposed

```
Class GpuThumbnailPool:
  Fields:
    - _slots: ThumbnailReference[] (fixed size array of GPU images)
    - _slotKeys: string?[] (which subpart ID each slot holds, null if empty)
    - _slotViewIndex: int[] (which view index each slot holds)
    - _lruOrder: LinkedList<int> (slot indices, head = most recent, tail = LRU)
    - _keyToNode: Dictionary<string, LinkedListNode<int>> (for O(1) LRU lookup)
    - _stagingBuffer: BufferEx (host-visible, reusable)
    - _stagingMapped: Ptr (mapped pointer to staging buffer)
    - _imageSize: int (pixel dimension)
    - _device: DeviceEx

  Methods:
    - ThumbnailReference? TryGet(string subpartId, int viewIndex)
        → returns GPU image if already in pool, updates LRU
    - ThumbnailReference Upload(string subpartId, int viewIndex, byte[] pixels)
        → evicts LRU if needed, copies pixels to staging buffer,
          records CopyBufferToImage command, returns GPU image
    - void Dispose()
        → dispose all ThumbnailReferences, staging buffer
```

**The upload method must:**
1. Copy `byte[]` into the mapped staging buffer memory
2. Record and submit a command buffer that:
   - Transitions the destination image to `TransferDst`
   - Calls `CopyBufferToImage` from staging buffer to the image
   - Transitions the image to `SampledReadFragment`
3. Wait for the fence (sync)
4. Register/update the ImGui descriptor

**Command buffer for upload** — create a dedicated `VkCommandPool` + `CommandBuffer` for the pool, similar to how `ThumbnailRenderer` creates its own. Allocate once, reuse for each upload.

### 3.4 — Modify `SubpartThumbnailGenerator.cs` — Readback after render

**New generation flow:**

After rendering each view to a `ThumbnailReference`:

1. **Transition image** to `TransferSrcOptimal` (if not already)
2. **Record `CopyImageToBuffer`** from the GPU image into the staging buffer
3. **Submit + wait fence** (already done for the render)
4. **Read staging buffer** into `byte[]`
5. **Dispose** the `ThumbnailReference` (free VRAM immediately)
6. Store `byte[]` in `CpuThumbnailCache`

The staging buffer should be created once at the start of generation and reused across all views/subparts. Size = `imageSize × imageSize × 4` bytes (R8G8B8A8UNorm).

**Modified RenderViewToImage signature** — instead of returning `ThumbnailReference`, return `byte[]`:

```csharp
// BEFORE:
private static ThumbnailReference RenderViewToImage(...)

// AFTER:
private static byte[] RenderViewToImage(..., BufferEx stagingBuffer, Ptr stagingMapped)
```

**Modified RenderOneSubpart** — collect `byte[]` arrays instead of `ThumbnailReference[]`:

```csharp
// BEFORE:
var views = new ThumbnailReference[viewCount];
// ... render into views[v] ...
subpart.Thumbnail = views[0];
SubpartThumbnailCache.Store(subpart.Id, new SubpartThumbnailEntry(views));

// AFTER:
var views = new byte[viewCount][];
// ... render into views[v] as byte[] ...
CpuThumbnailCache.Store(subpart.Id, new CpuThumbnailData(views, ThumbnailRenderer.SIZE));
// Do NOT set subpart.Thumbnail (no persistent GPU image)
```

**Readback command recording** — after the existing `RenderThumbnail` call and fence wait, but BEFORE disposing the temp `ThumbnailReference`:

```csharp
// After RenderThumbnail + WaitForFence:
// The image is in SampledReadFragment layout. Transition to TransferSrc.
// Submit a new command buffer that copies image → staging buffer.

// Allocate a command buffer from the existing _cmdPool on ThumbnailRenderer
// (or create a dedicated pool on the generator)
// The simplest approach: extend of the post-pass command to also do the
// CopyImageToBuffer into the staging buffer, while we're already in the
// command buffer. This avoids a second submit.

// BETTER APPROACH: Integrate the readback into the post-pass command itself.
// After blitting into the destination image and transitioning to SampledReadFragment,
// add the image-to-buffer copy. This way everything happens in a single command
// buffer submit and we only wait for one fence.
```

**Recommended integrated approach** — create a `ReadbackPostPassCommand` struct:

```csharp
public readonly struct ReadbackPostPassCommand : IRenderCommandRecord
{
    private readonly ThumbnailRenderer _renderer;
    private readonly PartTemplate _template;
    private readonly AtmosphereRenderer _atmosphereRenderer;
    private readonly VkBuffer _stagingBuffer;
    private readonly int _imageSize;

    // constructor takes staging buffer handle + image size

    public unsafe void ProcessCommands(CommandBuffer inCommandBuffer)
    {
        VkImage src = _renderer.ColorImage;
        VkImage dst = _template.Thumbnail.ImageView.Image.VkImage;

        // 1. Transition src → TransferSrc, dst → TransferDst
        // 2. BlitImage src → dst (HDR → LDR, mip 0 only)
        // 3. Transition dst → TransferSrc (for readback)
        // 4. CopyImageToBuffer dst → staging buffer
        // 5. Transition dst → SampledReadFragment (cleanup — even though we'll dispose)
        // 6. Transition src → SampledReadFragment (cleanup)
        // 7. Atmosphere LUT transitions

        // Step 3-4 are the NEW additions vs LdrPostPassCommand
    }
}
```

After the fence wait:

```csharp
// Read pixels from staging
int byteCount = imageSize * imageSize * 4; // R8G8B8A8UNorm = 4 bytes/pixel
byte[] pixels = new byte[byteCount];
unsafe { new Span<byte>((void*)stagingMapped.Value, byteCount).CopyTo(pixels); }

// Dispose the temporary GPU image (free VRAM immediately!)
thumb.Dispose();

return pixels;
```

### 3.5 — Modify `SubpartThumbnailGenerator.cs` — Staging buffer lifecycle

Add fields:

```csharp
private BufferEx? _stagingBuffer;
private Ptr _stagingMapped;
```

In `BeginGeneration()`:

```csharp
int stagingSize = ThumbnailImageSize * ThumbnailImageSize * 4; // R8G8B8A8UNorm
_stagingBuffer = Program.GetRenderer().Device.CreateBuffer(new BufferEx.CreateInfo
{
    Name = "ICR_ThumbnailReadback",
    BufferUsage = VkBufferUsageFlags.TransferDstBit,
    BufferSize = (ByteSize)stagingSize,  // ByteSize might need explicit construction
    AllocRequiredProperties = (VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit)
});
_stagingMapped = _stagingBuffer.Map();
```

**Note on `ByteSize`**: Check how the game constructs `ByteSize` from an int. In `OceanFFT.cs` it uses `_fftSize * _fftSize * ByteSize.Of<Half>((ElementCount)4)`. You may need to adapt: `ByteSize.Of<byte>((ElementCount)(size*size*4))` or just `new ByteSize(size*size*4)` or a cast `(ByteSize)(size*size*4)`. Inspect the `ByteSize` type in decompiled sources if needed.

In `CleanupGenerationResources()`:

```csharp
if (_stagingBuffer != null)
{
    _stagingBuffer.Unmap(); // if Map/Unmap is a pair
    _stagingBuffer.Dispose();
    _stagingBuffer = null;
}
```

### 3.6 — Modify `InanimateCarbonRodSubmod.cs` — UI changes

**The grid rendering must change** from reading `SubpartThumbnailCache` (GPU images) to reading `CpuThumbnailCache` (CPU data) and using `GpuThumbnailPool` for display.

**Key changes to `RenderThumbnailGrid()`:**

```csharp
// BEFORE: Iterate SubpartThumbnailCache.All
// AFTER:  Iterate CpuThumbnailCache.All

// BEFORE: entry.Views[animIdx].CreateImGuiThumbnail(...)
//         ImGui.Image(entry.Views[animIdx].ImGuiImageRef, ...)
// AFTER:
//   var gpuRef = _gpuPool.TryGet(subpartId, viewIdx);
//   if (gpuRef == null)
//       gpuRef = _gpuPool.Upload(subpartId, viewIdx, cpuData.Views[viewIdx]);
//   gpuRef.CreateImGuiThumbnail(Program.LinearClampedSampler);
//   ImGui.Image(gpuRef.ImGuiImageRef, thumbSize);
```

**Pool sizing**: For the grid view, at most ~20-30 thumbnails are visible at once (depending on window size). Each row shows 5 images (1 animated + 4 cardinal). With view cycling, size the pool at ~128–256 slots to minimize upload churn.

**The descriptor management** (`_registeredEntries` tracking) can be simplified: the pool itself manages which GPU images exist and their ImGui descriptors. When a slot is evicted, the pool calls `DestroyImGuiThumbnail()` on the old reference before overwriting it.

### 3.7 — Modify `SingleSubpartGenerator.cs` — Same readback pattern

Apply the same readback pattern: render → blit to LDR → CopyImageToBuffer → read staging → return `byte[]`. The single-subpart viewer will need to use the same `GpuThumbnailPool` or its own small pool for display.

### 3.8 — Modify `SubpartThumbnailCache.cs` — Decide role

Two options:

**(A) Keep `SubpartThumbnailCache` for backward compatibility but empty**: Other mods (`grant`) may reference `SubpartThumbnailCache.All` or `SubpartThumbnailCache.Get()`. If so, keep the type but have it return empty/null. Document that consumers should migrate to `CpuThumbnailCache`.

**(B) Remove it entirely**: If no other mod currently uses it, simplify by removing.

**Recommended: Option A** — the cache type is public and in a shared `.lib`. Keep it but make it a thin adapter that materializes GPU images from the CPU cache on demand (effectively wrapping the pool).

### 3.9 — Modify `SubpartViewerWindow.cs` — Use pool for display

The viewer window currently holds direct `ThumbnailReference` pointers. Change it to request images from the pool:

```csharp
// For the animation frame:
var gpuRef = _gpuPool.TryGet(_subpartName, _frameIndex);
if (gpuRef == null)
    gpuRef = _gpuPool.Upload(_subpartName, _frameIndex, _cpuData.Views[_frameIndex]);
```

### 3.10 — Testing

1. `dotnet build` — must compile
2. Generate thumbnails — verify the grid displays correctly
3. **Scroll rapidly** through the thumbnail grid — verify images appear (may flash briefly on first display as data uploads from CPU)
4. Verify animation still works in the grid
5. Open SubpartViewerWindow — verify animation and all views display
6. Generate hi-res views in viewer — verify they work
7. **Check VRAM**: Before and after generation, the GPU memory delta should be bounded (pool size) regardless of subpart count
8. Check console for Vulkan validation errors
9. **Reset and regenerate** — verify cleanup works (no leaked GPU resources)
10. **Stress test**: Generate at max views (32) and max resolution (1024) for a system RAM check — should work without issue since all data goes to managed `byte[]` arrays

---

## Appendix: Vulkan Command Signatures (Reference)

All from decompiled `Brutal.VulkanApi.CommandBuffer`:

```csharp
// Image copy (same format required)
void CopyImage(VkImage src, VkImageLayout srcLayout, VkImage dst, VkImageLayout dstLayout, Span<VkImageCopy> regions);

// Image blit (format conversion + scaling)
void BlitImage(VkImage src, VkImageLayout srcLayout, VkImage dst, VkImageLayout dstLayout, Span<VkImageBlit> regions, VkFilter filter);

// Image → Buffer (readback)
void CopyImageToBuffer(VkImage src, VkImageLayout srcLayout, VkBuffer dst, ReadOnlySpan<VkBufferImageCopy> regions);

// Buffer → Image (upload) — expected to exist symmetrically
void CopyBufferToImage(VkBuffer src, VkImage dst, VkImageLayout dstLayout, ReadOnlySpan<VkBufferImageCopy> regions);

// Image layout transitions
void TransitionImages2(ReadOnlySpan<ImageTransition> transitions);
```

**Image transition presets** (from `ImageBarrierInfo.Presets`):
- `Undefined` — initial/don't-care layout
- `ColorAttachmentWrite` — render target
- `TransferSrc` — source for copy/blit
- `TransferDst` — destination for copy/blit
- `SampledReadFragment` — shader-readable (for ImGui sampling)

**Buffer creation** (host-visible):
```csharp
BufferEx buffer = device.CreateBuffer(new BufferEx.CreateInfo
{
    Name = "MyBuffer",
    BufferUsage = VkBufferUsageFlags.TransferDstBit,
    BufferSize = byteSize,
    AllocRequiredProperties = (VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit)
});
Ptr mapped = buffer.Map();
// ... read/write via mapped pointer ...
buffer.Unmap();
buffer.Dispose();
```

**ImGui texture integration:**
```csharp
// Register image for ImGui use
thumbnailRef.CreateImGuiThumbnail(Program.LinearClampedSampler);
// Display
ImGui.Image(thumbnailRef.ImGuiImageRef, new float2(displaySize));
// Unregister
thumbnailRef.DestroyImGuiThumbnail();
```
