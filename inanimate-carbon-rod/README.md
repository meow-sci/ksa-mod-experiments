# Inanimate Carbon Rod — Subpart Thumbnail Generator

Generates runtime GPU thumbnails for KSA subpart `PartTemplate` objects (those with `IsSubPart == true`) which the game skips by default during thumbnail generation. Thumbnails are rendered to CPU-backed pixel arrays and uploaded to GPU on demand via a fixed-capacity LRU pool, keeping VRAM usage bounded regardless of total subpart count.

## Overview

The KSA vehicle editor uses 128x128 thumbnails for every part, rendered at startup via Vulkan. Subparts are explicitly skipped during this process. This mod provides on-demand thumbnail generation for subparts using the same Vulkan rendering pipeline the game uses.

## Features

- **On-demand generation** — triggered by a button click, not at startup
- **Mirrors game rendering** — uses the same `ThumbnailRenderer`, `ThumbnailPart`, camera positioning, and fence synchronization as `ThumbnailCreator`
- **CPU-backed storage** — thumbnails rendered to GPU, read back to CPU byte arrays via staging buffers, stored in `CpuThumbnailCache`
- **LRU GPU pool** — `GpuThumbnailPool` uploads CPU pixel data to a fixed number of reusable GPU images on demand, evicting least-recently-used entries at capacity
- **Scrollable thumbnail grid** — 64x64 thumbnails with subpart ID tooltips
- **Progress display** — progress bar and status during generation
- **No Harmony patches** — uses only public game APIs (plus reflection for `ModLibrary.AllParts`)
- **VRAM optimized** — bounded GPU memory via LRU pool (256 grid slots + 64 viewer slots); all pixel data stored as R8G8B8A8UNorm (4 bytes/pixel) with no mip chain
- **Grant supermod integration** — appears as a collapsible section in the grant window

## Usage

### Standalone
- Press **F10** to toggle the mod window
- Click **"Generate Subpart Thumbnails"** to start generation
- The game will briefly freeze while GPU work completes
- Browse generated thumbnails in the scrollable grid

### Via Grant Supermod
- The mod appears as "Inanimate Carbon Rod" in the grant toolbox (F11)
- Same UI and functionality as standalone mode

## Architecture

| File | Purpose |
|------|---------|
| `Mod.cs` | StarMap entry point, F10 toggle, hosts submod |
| `mod.toml` | Mod metadata |

### Library (`inanimate-carbon-rod.lib/`)

| File | Purpose |
|------|---------|
| `CpuThumbnailData.cs` | CPU-side pixel data holder (byte[][] views + size) for a single subpart |
| `CpuThumbnailCache.cs` | Static dictionary of `CpuThumbnailData` keyed by `PartTemplate.Id` |
| `GpuThumbnailPool.cs` | Fixed-capacity LRU pool of reusable GPU images; uploads CPU pixels on demand |
| `SubpartThumbnailGenerator.cs` | Bulk renderer: generates CPU-backed thumbnails for all subparts |
| `SingleSubpartGenerator.cs` | Hi-res single-subpart multi-view generator (CPU-backed) |
| `SubpartViewerWindow.cs` | Single-subpart detail viewer with animation and own GPU pool |
| `ReadbackPostPassCommand.cs` | Post-pass command: HDR→LDR blit + CopyImageToBuffer for CPU readback |
| `LdrPostPassCommand.cs` | Legacy post-pass blit command (HDR→LDR only, no readback) |
| `SubpartThumbnailCache.cs` | Legacy GPU-only cache (kept for backward compatibility) |
| `InanimeCarbonicRodSubmod.cs` | `ISubmod` implementation with full ImGui UI |

## Technical Details

### Rendering Flow (CPU-Backed with LRU GPU Pool)

1. **Generation phase** (runs in background, one batch per frame):
   - Collects all `PartTemplate` where `IsSubPart && !IsHidden && Thumbnail == null`
   - Creates a host-visible staging buffer for GPU→CPU readback
   - For each subpart, renders N rotation views:
     - Allocates temporary GPU image (TransferDst + TransferSrc + Sampled)
     - Drives render via `ThumbnailRenderer.RenderThumbnail`
     - `ReadbackPostPassCommand` blits HDR→LDR and copies result to staging buffer
     - After fence wait, maps staging buffer and copies pixels to a `byte[]`
     - Disposes temporary GPU image immediately (frees VRAM)
   - Stores `CpuThumbnailData` (array of byte[] views) in `CpuThumbnailCache`

2. **Display phase** (every frame, in ImGui):
   - Iterates visible thumbnails in the scrollable grid
   - For each visible thumbnail, checks `GpuThumbnailPool.TryGet(key)`
   - On cache miss, calls `GpuThumbnailPool.Upload(key, pixels)` which:
     - Acquires a pool slot (free list → new allocation → LRU eviction)
     - `Marshal.Copy` pixels to persistent staging buffer
     - Records and submits Vulkan commands: transition → CopyBufferToImage → transition
     - Waits on fence for synchronous upload
     - Registers ImGui texture handle
   - Renders the thumbnail via `ImGui.Image()`

### API Notes

- `ModLibrary.AllParts` is `internal` — accessed via reflection
- All Vulkan types (`ThumbnailRenderer`, `ThumbnailPart`, etc.) are in `KSA.dll`
- Additional DLL references: `Brutal.Vulkan.dll`, `Brutal.Vulkan.Abstractions.dll`, `Planet.Render.Core.dll`
