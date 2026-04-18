# Inanimate Carbon Rod — Subpart Thumbnail Generator

Generates runtime GPU thumbnails for KSA subpart `PartTemplate` objects (those with `IsSubPart == true`) which the game skips by default during thumbnail generation. Thumbnails are displayed in a scrollable grid and stored in a static cache accessible by other mods.

## Overview

The KSA vehicle editor uses 128x128 thumbnails for every part, rendered at startup via Vulkan. Subparts are explicitly skipped during this process. This mod provides on-demand thumbnail generation for subparts using the same Vulkan rendering pipeline the game uses.

## Features

- **On-demand generation** — triggered by a button click, not at startup
- **Mirrors game rendering** — uses the same `ThumbnailRenderer`, `ThumbnailPart`, `ThumbnailRenderResources`, and camera positioning as `ThumbnailCreator`
- **Scrollable thumbnail grid** — 64x64 thumbnails with subpart ID tooltips
- **Progress display** — progress bar and status during generation
- **Static cache** — `SubpartThumbnailCache` allows other mods to access generated thumbnails
- **No Harmony patches** — uses only public game APIs (plus reflection for `ModLibrary.AllParts`)
- **Direct LDR rendering** — thumbnails rendered directly as R8G8B8A8UNorm using the game's updated thumbnail pipeline
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
| `SubpartThumbnailCache.cs` | Static `Dictionary<string, ThumbnailReference>` cache |
| `SubpartThumbnailGenerator.cs` | On-demand Vulkan rendering loop mirroring `ThumbnailCreator` |
| `SingleSubpartGenerator.cs` | Hi-res single-subpart multi-view generator |
| `SubpartViewerWindow.cs` | Single-subpart detail viewer with animation |
| `InanimateCarbonRodSubmod.cs` | `ISubmod` implementation with full ImGui UI |

## Technical Details

### Rendering Flow

1. Collects all `PartTemplate` where `IsSubPart && !IsHidden && Thumbnail == null`
2. Saves camera/viewport state
3. Configures camera for thumbnail-size rendering
4. Creates `ThumbnailRenderer` and `VkCommandPool`
5. For each subpart (one per frame):
   - For each rotation view: allocates R8G8B8A8UNorm GPU image, creates `ThumbnailRenderResources`, collects draw commands
   - Records all views into a single `CommandBuffer` via `RecordPartRender`
   - Submits command buffer, waits on fence, cleans up resources
6. Restores camera/viewport state

### API Notes

- `ModLibrary.AllParts` is `internal` — accessed via reflection
- All Vulkan types (`ThumbnailRenderer`, `ThumbnailPart`, etc.) are in `KSA.dll`
- Additional DLL references: `Brutal.Vulkan.dll`, `Brutal.Vulkan.Abstractions.dll`, `Planet.Render.Core.dll`
