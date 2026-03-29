Deep Dive: VRAM Control for Inanimate Carbon Rod Thumbnails
The Core Problem
Every thumbnail your mod generates is a Vulkan image allocated in device-local VRAM via ThumbnailReference.CreateImageView(). The allocation path is:

All game image allocations are hardwired to GPU-only VRAM. The Brutal.VulkanApi layer (undecompiled) only ever receives MemoryPreference.PreferGpu for images — there's no PreferHost variant used anywhere in the game, and VkImageTiling.Linear (which would allow host-mappable images) is never used either.

VRAM Cost Per Thumbnail
Setting	Format	Size	Mip chain	Per image	50 subparts × 32 views
512px	R16G16B16A16SFloat (8 B/px)	512²	~1.33×	~2.67 MB	~4.2 GB
128px	R16G16B16A16SFloat (8 B/px)	128²	~1.33×	~170 KB	~272 MB
64px	R16G16B16A16SFloat (8 B/px)	64²	~1.33×	~43 KB	~69 MB
The format is R16G16B16A16SFloat — 16-bit float per channel, 8 bytes/pixel. With full mip chains, the multiplier is ~1.33×. At your default 32 views × 512px, a single subpart costs ~85 MB of VRAM.

What's NOT Possible (Dead Ends)
Allocating in system RAM instead of VRAM — The game's ImageEx.CreateInfo always produces DeviceLocalBit images. No host-visible image allocation has ever been used in KSA. You'd need to bypass the Brutal.VulkanApi wrapper entirely with raw Vulkan P/Invoke, which is extremely fragile.

Using VkImageTiling.Linear — Never used by the game. Even if it worked, linear-tiled images can't be used as color attachments (needed by ThumbnailRenderer.RenderThumbnail).

Using a compressed format (BC7/ASTC) — The game only uses R16G16B16A16SFloat for thumbnails. The render pass and PostPassThumbnailCommand write this format directly. Compressed formats can't be render targets.

Custom Vulkan memory pools — No VMA integration; the game does direct vkAllocateMemory. No pool API exposed.

What IS Possible (Actionable Solutions)
1. Render-to-CPU + Lazy Re-upload (Best Long-term Option)
The strongest solution: render each thumbnail to VRAM, immediately read it back to a CPU byte array, dispose the GPU image, and only re-upload to VRAM the handful of thumbnails currently visible on screen.

Flow:

Render thumbnail to GPU image (as now)
Use a staging buffer (BufferEx.CreateInfo with HostVisibleBit | HostCoherentBit) to copy the image data to CPU — the game already has this pattern for buffers
Store the byte[] in your cache (system RAM, unlimited)
Dispose the GPU ThumbnailReference
When displaying, only upload visible thumbnails back to GPU images
VRAM at rest: Only the ~5-10 visible thumbnails + the staging buffer — probably <50 MB regardless of total subpart count.

Challenges: You'd need to use vkCmdCopyImageToBuffer and then vkCmdCopyBufferToImage which are available through the game's Vulkan command infrastructure (the ThumbnailRenderer already records command buffers). The PostPassThumbnailCommand shows the pattern for image blit/copy operations.

2. Drop to R8G8B8A8UNorm After Rendering (50% VRAM Savings)
Render at full R16G16B16A16SFloat quality, then blit the result into a second R8G8B8A8UNorm image (4 bytes/pixel instead of 8) and dispose the HDR original. The VkImageBlit command supports format conversion during blit.

Savings: 50% immediate VRAM reduction. A 128px thumbnail drops from ~170 KB to ~85 KB. This is the single easiest win.

Implementation: After RenderViewToImage, create a second ThumbnailReference with ImageFormat = VkFormat.R8G8B8A8UNorm, blit from HDR to LDR, dispose the HDR one. The blit hardware handles the conversion automatically.

3. Reduce View Count and Resolution (Already Exposed, Easiest)
Your UI already exposes view count (2-32) and image size (64-1024). But the defaults are aggressive — 32 views at 128px. Dropping to something like 8 views at 64px would reduce VRAM by ~32× (from ~272 MB to ~8.5 MB for 50 subparts). The thumbnails are displayed at _thumbDisplaySize which defaults to 128px — displaying 64px images at 128px is perfectly acceptable for a grid overview.

4. LRU Eviction of GPU Images
Keep all thumbnail data in CPU memory (byte[]), but maintain a fixed-size pool of GPU images (say 64-128). Use LRU eviction: when you need to display a thumbnail that isn't in the GPU pool, evict the least-recently-displayed one, reuse its GPU image, and upload the new data. This is essentially a texture cache.

5. Eliminate Mip Chains
You currently request full mip chains ((int)Math.Floor(Math.Log2(size)) + 1). For thumbnails displayed in ImGui at small display sizes, mipmaps add ~33% overhead for no visible benefit. Setting ImageMipLevels = 1 would save ~25% of allocated VRAM per image immediately.

Recommended Priority
Priority	Strategy	Effort	VRAM Impact
1	ImageMipLevels = 1	Trivial (1 line)	-25%
2	Lower default views/resolution	Trivial (defaults)	-90%+
3	R8G8B8A8UNorm post-blit	Medium	-50%
4	Render-to-CPU + LRU GPU cache	High	-95%+ (bounded VRAM)
Do you want me to implement any of these? The mip chain elimination and lower defaults are essentially free wins. The R8G8B8A8UNorm conversion would require adding a blit step after rendering. The full CPU-cache approach is the most architectural work but gives you total control over VRAM budget.

