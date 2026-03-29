# Inanimate Carbon Rod — Subpart Thumbnail Generator: Implementation Plan

## Overview

`inanimate-carbon-rod` is a KSA mod + grant supermod submod that generates runtime GPU thumbnails
for subpart `PartTemplate` objects (those with `IsSubPart == true`) which the game skips by default.
Thumbnails are stored in a static cache and displayable via ImGui anywhere in a mod UI.

Generation is **on-demand** — triggered by a button in the mod's ImGui window, NOT at game startup.
This is confirmed feasible: the Vulkan thumbnail pipeline can be invoked safely from the game thread
during normal gameplay.

---

## Architecture Decision: On-Demand (Not At Startup)

The game generates part thumbnails during `Program.Load()` via `ThumbnailCreator.PreparePartThumbnails()`.
We do NOT patch or hook that call. Instead:

- Our mod's ImGui UI has a **"Generate Thumbnails"** button
- On click, `SubpartThumbnailGenerator.GenerateAll()` runs synchronously on the main thread
- It instantiates its own `ThumbnailRenderer`, drives the same Vulkan render loop, and populates
  `SubpartThumbnailCache`
- The game freezes briefly while GPU work completes (expected — show a warning in the UI)
- No Harmony patches are required

---

## Confirmed API Surface (All Public — No Reflection Needed)

| Symbol | Type | Notes |
|---|---|---|
| `Program.GetRenderer()` | `public static Renderer` | Returns the Vulkan renderer |
| `Program.Instance` | `public static Program` | For instance methods |
| `Program.Instance.UpdateShaderData(double, Viewport)` | `public void` | Call per thumbnail |
| `Program.Instance.UpdateRenderingResources(int)` | `public void` | Call per thumbnail |
| `Program.RenderedViewport` | `public static Viewport` | `Viewports[_renderedViewportIndex]` — use this, NOT `Program.Instance.RenderedViewport` |
| `Program.LightSystem` | `public static LightSystem` | Pass to PrePass command |
| `Program.GetCSMSystem()` | `public static CascadedShadowSystem` | Pass to PrePass command |
| `Program.PlanetAtmosphereRenderer` | `public static AtmosphereRenderer` | Pass to Pre/PostPass |
| `Program.LinearClampedSampler` | `public static VkSampler` | For `CreateImGuiThumbnail` |
| `Program.DeviceHostSharedMemoryDebug` | `public static` | Set PostMemoryWrite/PostDescriptorSet = false after each render |
| `Universe.CurrentSystem` | `public static CelestialSystem?` | Guard: must not be null |
| `ThumbnailRenderer(Renderer)` | `public sealed` constructor | Creates own Vulkan framebuffer |
| `ThumbnailRenderer.SIZE` | `public static int` | From `GameSettings.Current.Graphics.PartThumbnailSize` |
| `ThumbnailRenderer.MipLevels` | `public static int` | `floor(log2(SIZE)) + 1` |
| `ThumbnailRenderer.ColorFormat` | `public static VkFormat` | `R16G16B16A16SFloat` |
| `ThumbnailRenderer.RenderPass` | `public VkRenderPass` | Passed to BeginThumbnailPass |
| `ThumbnailRenderer.SampleCount` | `public VkSampleCountFlags` | `_1Bit` |
| `ThumbnailRenderer.RenderThumbnail(PrePass, Pass, PostPass, string, out VkFence)` | `public void` | Core render call |
| `PartModelRenderer.ColorData.BeginThumbnailPass(VkRenderPass, VkSampleCountFlags)` | `public static void` | Sets up thumbnail pipelines |
| `PartModelRenderer.ColorData.EndThumbnailPass()` | `public static void` | Destroys thumbnail pipelines |
| `PartModelRenderer.ClearFrameData(int)` | `public static void` | Call after each thumbnail |
| `ThumbnailReference` | `public class` | GPU image + ImGui handle |
| `ThumbnailReference.CreateImageView(DeviceEx, ImageEx.CreateInfo, VkImageViewType, VkImageSubresourceRange)` | `public void` | Allocates GPU VkImage |
| `ThumbnailReference.CreateImGuiThumbnail(VkSampler)` | `public void` | Registers with ImGui backend |
| `ThumbnailReference.ImGuiImageRef` | `public ImTextureRef` | Pass to `ImGui.Image()` |
| `ThumbnailPart(Camera)` | `public` constructor | Root node (camera anchor, no mesh) |
| `ThumbnailPart(ThumbnailPart, PartInstance?)` | `public` constructor | Child node; reads template Components for mesh |
| `ThumbnailPart.AddChild(ThumbnailPart)` | `public void` | |
| `ThumbnailPart.ClearAndDisposeChildren()` | `public void` | Reset between thumbnails |
| `ThumbnailPart.UpdateRenderData(Viewport, int)` | `public void` | Submits instance data |
| `ThumbnailPart.ComputeBoundingSphereRadius()` | `public float` | For camera auto-positioning |
| `PrePassThumbnailCommand(Viewport, int, CascadedShadowSystem, LightSystem, AtmosphereRenderer)` | `public readonly struct` | |
| `PassThumbnailCommand(Viewport, int)` | `public readonly struct` | |
| `PostPassThumbnailCommand(ThumbnailRenderer, PartTemplate, AtmosphereRenderer)` | `public readonly struct` | |
| `PartInstance.InstanceOf` | `public string` field `[XmlAttribute]` | Set to subpart template ID |
| `PartInstance.GetTemplate()` | `public PartTemplate` | Calls `ModLibrary.Get<PartTemplate>(InstanceOf)` |
| `ModLibrary.AllParts.GetList()` | `public List<PartTemplate>` | All parts including subparts |
| `PartTemplate.IsSubPart` | `public bool` field `[XmlIgnore]` | Our filter target |
| `PartTemplate.IsHidden` | `public bool` property | Skip these |
| `PartTemplate.Thumbnail` | `public ThumbnailReference?` field | Set by our code |
| `PartTemplate.Components` | `public List<ModuleBase.TemplateDataBase>` | ThumbnailPart reads for mesh |

**Viewport access**: `Program.RenderedViewport` is `public static Viewport` defined as
`Viewports[_renderedViewportIndex]`. Use `Program.RenderedViewport` directly — no instance access needed.

**Required DLL references**: All KSA types (`ThumbnailRenderer`, `PartModelRenderer`,
`ThumbnailReference`, `ThumbnailPart`, `PartTemplate`, `PartInstance`, Vulkan types, etc.) are all
in `KSA.dll`. No additional game DLL references are needed beyond the standard set used by other mods.

---

## Project Structure

```
ksa-mod-experiments/
├── inanimate-carbon-rod.lib/
│   ├── inanimate-carbon-rod.lib.csproj
│   ├── SubpartThumbnailCache.cs          ← Static cache of generated thumbnails
│   ├── SubpartThumbnailGenerator.cs      ← On-demand Vulkan rendering logic
│   └── InanimeCarbonicRodSubmod.cs       ← ISubmod: ImGui UI + orchestration
│
└── inanimate-carbon-rod/
    ├── inanimate-carbon-rod.csproj
    ├── Mod.cs                             ← StarMap entry point (no Harmony patches)
    └── mod.toml
```

No `Patcher.cs` is needed — there are no Harmony patches in this mod.

---

## Rendering Flow (On-Demand, Triggered by Button Click)

```
[ImGui button clicked in RenderContent()]
  → _generator.GenerateAll(viewport)
      │
      ├─ Guard: Universe.CurrentSystem != null
      ├─ Set _state = Generating
      │
      ├─ Renderer renderer = Program.GetRenderer()
      ├─ Camera camera = viewport.GetCamera()
      ├─ Save: camera.FramebufferSize, viewport.Size, camera.Following
      │
      ├─ camera.Unfollow()
      ├─ camera.Resize(new int2(ThumbnailRenderer.SIZE))
      ├─ viewport.Size = new int2(ThumbnailRenderer.SIZE)
      ├─ camera.LocalPosition = double3.Zero
      ├─ camera.LocalRotation = doubleQuat.Identity
      ├─ camera.LocalScale = double3.One
      ├─ camera.OnFrame(1.0/60.0)
      │
      ├─ ThumbnailPart root = new ThumbnailPart(camera)
      ├─ using ThumbnailRenderer thumbRenderer = new ThumbnailRenderer(renderer)
      ├─ PartModelRenderer.ColorData.BeginThumbnailPass(thumbRenderer.RenderPass, thumbRenderer.SampleCount)
      │
      ├─ List<PartTemplate> subparts = ModLibrary.AllParts.GetList()
      │       .Where(p => p.IsSubPart && !p.IsHidden && p.Thumbnail == null).ToList()
      │
      ├─ For each subpart (index i of total):
      │   │
      │   ├─ [Create GPU image — mirrors ThumbnailCreator.CreateThumbnailImage()]
      │   │   subpart.Thumbnail = new ThumbnailReference()
      │   │   subpart.Thumbnail.CreateImageView(renderer.Device, createInfo, ...)
      │   │     where createInfo.ImageExtent = ThumbnailRenderer.SIZE × SIZE × 1
      │   │           createInfo.ImageFormat = ThumbnailRenderer.ColorFormat
      │   │           createInfo.ImageMipLevels = ThumbnailRenderer.MipLevels
      │   │           createInfo.ImageUsage = TransferSrc|TransferDst|Sampled|ColorAttachment
      │   │           createInfo.AllocPreference = MemoryPreference.PreferGpu
      │   │
      │   ├─ [Build ThumbnailPart — mirrors ThumbnailCreator.AddPart() but for the subpart itself]
      │   │   var synth = new PartInstance { InstanceOf = subpart.Id }
      │   │   var child = new ThumbnailPart(root, synth)
      │   │   if (child.Model == null && child.ModelDynamic == null):
      │   │       subpart.Thumbnail.Dispose(); subpart.Thumbnail = null; continue  ← no mesh, skip
      │   │   root.AddChild(child)
      │   │
      │   ├─ [Position camera — mirrors ThumbnailCreator.MoveRootPart()]
      │   │   if subpart.Thumbnail.ModelTransform != null:
      │   │       root.Transform = subpart.Thumbnail.ModelTransform.Create()
      │   │   else:
      │   │       float radius = root.ComputeBoundingSphereRadius()
      │   │       float dist = radius / (float)Math.Sin(camera.GetFieldOfView() * 0.5)
      │   │       root.LocalPosition = Double3Ex.Forward * (camera.NearPlane + dist)
      │   │       root.LocalRotation = doubleQuat.CreateFromYawPitchRoll(Math.PI, Math.PI/4, 0)
      │   │       root.LocalScale = Double3Ex.One
      │   │
      │   ├─ camera.OnFrame(1.0/60.0)
      │   ├─ Program.Instance.UpdateShaderData(1.0/60.0, viewport)
      │   ├─ root.UpdateRenderData(viewport, frameIndex)
      │   ├─ Program.Instance.UpdateRenderingResources(frameIndex)
      │   │
      │   ├─ thumbRenderer.RenderThumbnail(
      │   │       new PrePassThumbnailCommand(viewport, frameIndex,
      │   │           Program.GetCSMSystem(), Program.LightSystem, Program.PlanetAtmosphereRenderer),
      │   │       new PassThumbnailCommand(viewport, frameIndex),
      │   │       new PostPassThumbnailCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer),
      │   │       subpart.Id,
      │   │       out VkFence fence)
      │   │
      │   ├─ renderer.Device.WaitForFence(fence, IntPtr.MaxValue)
      │   ├─ renderer.Device.ResetFences(MemoryMarshal.CreateReadOnlySpan(ref fence, 1))
      │   ├─ renderer.Device.DestroyFence(fence, null)
      │   ├─ PartModelRenderer.ClearFrameData(frameIndex)
      │   ├─ Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = false
      │   ├─ Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = false
      │   ├─ frameIndex = (frameIndex + 1) % 2
      │   │
      │   ├─ root.ClearAndDisposeChildren()
      │   ├─ root.LocalPosition = double3.Zero
      │   ├─ root.LocalRotation = doubleQuat.Identity
      │   ├─ root.LocalScale = double3.One
      │   ├─ Program.LightSystem.ClearLights()
      │   │
      │   ├─ SubpartThumbnailCache.Store(subpart.Id, subpart.Thumbnail!)
      │   └─ _progress = (i + 1, subparts.Count)   ← for UI display
      │
      ├─ PartModelRenderer.ColorData.EndThumbnailPass()
      ├─ root.Dispose()
      │
      ├─ Restore: camera.Resize(savedFramebufferSize)
      ├─ Restore: viewport.Size = savedViewportSize
      ├─ if savedFollowing != null: camera.SetFollow(savedFollowing, tidalLocking: false)
      ├─ camera.OnFrame(1.0/60.0)
      │
      └─ _state = Done
```

---

## Task Breakdown for Coding Agents

Each task is self-contained. Complete tasks in order (1 → 2 → 3 → 4 → 5 → 6 → 7).
Dependencies are noted. Build after completing Task 5.

---

### TASK 1 — Scaffold Projects and Solution

**Goal**: Create the `inanimate-carbon-rod.lib` and `inanimate-carbon-rod` project directories,
register them in the solution, and verify the `.csproj` files have correct references.

**Steps**:

1. From the repo root, run:
   ```
   bun run mkmod.ts inanimate-carbon-rod InanimateCarbonRod
   ```
   This creates `inanimate-carbon-rod/` and `inanimate-carbon-rod.lib/` from the `fixme-mod-name`
   template, replacing all placeholder strings. It also adds both to `ksa-mod-experiments.slnx`.

2. Verify `ksa-mod-experiments.slnx` now contains:
   ```xml
   <Project Path="inanimate-carbon-rod/inanimate-carbon-rod.csproj" />
   <Project Path="inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj" />
   ```

3. **Edit `inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj`**. It must reference ALL of these
   (add any missing compared to the template):
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <OutputType>Library</OutputType>
       <AssemblyName>MeowSci.InanimateCarbonRodLib</AssemblyName>
       <RootNamespace>MeowSci.InanimateCarbonRodLib</RootNamespace>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
     </ItemGroup>
     <ItemGroup>
       <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
     </ItemGroup>
     <ItemGroup>
       <!-- Standard KSA mod DLLs -->
       <Reference Include="Brutal.Core.Common" Condition="Exists('$(KSAFolder)Brutal.Core.Common.dll')">
         <HintPath>$(KSAFolder)Brutal.Core.Common.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.Core.Numerics" Condition="Exists('$(KSAFolder)Brutal.Core.Numerics.dll')">
         <HintPath>$(KSAFolder)Brutal.Core.Numerics.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.ImGui" Condition="Exists('$(KSAFolder)Brutal.ImGui.dll')">
         <HintPath>$(KSAFolder)Brutal.ImGui.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.ImGui.Abstractions" Condition="Exists('$(KSAFolder)Brutal.ImGui.Abstractions.dll')">
         <HintPath>$(KSAFolder)Brutal.ImGui.Abstractions.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.Core.Strings" Condition="Exists('$(KSAFolder)Brutal.Core.Strings.dll')">
         <HintPath>$(KSAFolder)Brutal.Core.Strings.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
         <HintPath>$(KSAFolder)KSA.dll</HintPath><Private>false</Private>
       </Reference>
       <!-- All KSA types (ThumbnailRenderer, PartModelRenderer, ThumbnailPart,
            ThumbnailReference, Vulkan types, etc.) are all in KSA.dll. -->
     </ItemGroup>
   </Project>
   ```

4. **Edit `inanimate-carbon-rod/inanimate-carbon-rod.csproj`**. The standalone mod does NOT need
   Harmony (no patches). Ensure it references the lib:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <OutputType>Library</OutputType>
       <AssemblyName>MeowSci.InanimateCarbonRod</AssemblyName>
       <RootNamespace>MeowSci.InanimateCarbonRod</RootNamespace>
       <DistDir>$(SelectedDistModDir)inanimate-carbon-rod\</DistDir>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
       <ProjectReference Include="..\inanimate-carbon-rod.lib\inanimate-carbon-rod.lib.csproj" />
     </ItemGroup>
     <ItemGroup>
       <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
     </ItemGroup>
     <ItemGroup>
       <Reference Include="Brutal.Core.Common" Condition="Exists('$(KSAFolder)Brutal.Core.Common.dll')">
         <HintPath>$(KSAFolder)Brutal.Core.Common.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.Core.Numerics" Condition="Exists('$(KSAFolder)Brutal.Core.Numerics.dll')">
         <HintPath>$(KSAFolder)Brutal.Core.Numerics.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.ImGui" Condition="Exists('$(KSAFolder)Brutal.ImGui.dll')">
         <HintPath>$(KSAFolder)Brutal.ImGui.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.ImGui.Abstractions" Condition="Exists('$(KSAFolder)Brutal.ImGui.Abstractions.dll')">
         <HintPath>$(KSAFolder)Brutal.ImGui.Abstractions.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="Brutal.Core.Strings" Condition="Exists('$(KSAFolder)Brutal.Core.Strings.dll')">
         <HintPath>$(KSAFolder)Brutal.Core.Strings.dll</HintPath><Private>false</Private>
       </Reference>
       <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
         <HintPath>$(KSAFolder)KSA.dll</HintPath><Private>false</Private>
       </Reference>
     </ItemGroup>
     <ItemGroup>
       <None Update="mod.toml">
         <CopyToOutputDirectory>Always</CopyToOutputDirectory>
       </None>
     </ItemGroup>
     <Target Name="CopyCustomContent" AfterTargets="AfterBuild">
       <MakeDir Directories="$(DistDir)" />
       <ItemGroup>
         <FilesToCopy Include="$(OutputPath)mod.toml" />
         <FilesToCopy Include="$(OutputPath)$(AssemblyName).dll" />
         <FilesToCopy Include="$(OutputPath)$(AssemblyName).pdb" />
         <FilesToCopy Include="$(OutputPath)$(AssemblyName).deps.json" />
       </ItemGroup>
       <Copy SourceFiles="@(FilesToCopy)" DestinationFolder="$(DistDir)" />
       <ItemGroup>
         <MeowSciAssemblies Include="$(TargetDir)MeowSci.*.dll;$(TargetDir)MeowSci.*.pdb" />
       </ItemGroup>
       <Copy SourceFiles="@(MeowSciAssemblies)" DestinationFolder="$(DistDir)"
             Condition="'@(MeowSciAssemblies)' != ''" />
     </Target>
   </Project>
   ```

5. Delete the template-generated `Patcher.cs` in `inanimate-carbon-rod/` if it was created
   (this mod has no Harmony patches).

6. Write `inanimate-carbon-rod/mod.toml`:
   ```toml
   name = "inanimate-carbon-rod"
   description = "Generates thumbnails for KSA subparts on demand."
   version = "0.1.0"
   author = "meow sci"

   [StarMap]
   EntryAssembly = "MeowSci.InanimateCarbonRod"
   ```

**Verify**: `dotnet build inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj` compiles
(even with empty source files). Fix any missing DLL references before proceeding.

**Depends on**: nothing

---

### TASK 2 — SubpartThumbnailCache

**File**: `inanimate-carbon-rod.lib/SubpartThumbnailCache.cs`

**Goal**: A static thread-safe read-accessible cache mapping subpart template IDs to their generated
`ThumbnailReference` objects.

```csharp
using System.Collections.Generic;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Static cache of generated subpart thumbnails, keyed by PartTemplate.Id.
/// Populated by SubpartThumbnailGenerator.GenerateAll().
/// </summary>
public static class SubpartThumbnailCache
{
    private static readonly Dictionary<string, ThumbnailReference> _thumbnails = new();

    /// <summary>All generated thumbnails. Do not mutate.</summary>
    public static IReadOnlyDictionary<string, ThumbnailReference> All => _thumbnails;

    /// <summary>Returns the thumbnail for a subpart ID, or null if not yet generated.</summary>
    public static ThumbnailReference? Get(string subpartId)
        => _thumbnails.GetValueOrDefault(subpartId);

    /// <summary>Returns true if any thumbnails have been generated.</summary>
    public static bool HasAny => _thumbnails.Count > 0;

    internal static void Store(string id, ThumbnailReference thumbnail)
        => _thumbnails[id] = thumbnail;

    internal static void Clear()
        => _thumbnails.Clear();
}
```

**Depends on**: Task 1 (project exists)

---

### TASK 3 — SubpartThumbnailGenerator

**File**: `inanimate-carbon-rod.lib/SubpartThumbnailGenerator.cs`

**Goal**: Performs the on-demand Vulkan thumbnail rendering for all subparts. Called from the
submod's UI when the user clicks the generate button.

**Critical notes for the implementer**:
- Mirror `ThumbnailCreator.PreparePartThumbnails()` in `decomp/ksa/KSA.Rendering/ThumbnailCreator.cs`
  exactly for the per-part GPU work. Deviating from the fence/frame-index/light reset pattern will
  likely cause GPU errors or corruption.
- The subpart mesh problem: `ThumbnailCreator.AddPart()` adds a part's `SubPartInstances` as
  children. For a subpart being rendered in isolation, its own mesh lives in its own `Components`
  list. We create a synthetic `PartInstance { InstanceOf = subpart.Id }` so that
  `new ThumbnailPart(root, syntheticInstance)` finds the mesh by calling
  `syntheticInstance.GetTemplate().Components`. If the resulting child has no `Model` and no
  `ModelDynamic`, the subpart has no visual mesh — skip it.
- `Program.RenderedViewport` is `public static Viewport` — use it directly.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

public enum GenerationState { Idle, Generating, Done, Failed }

public sealed class SubpartThumbnailGenerator
{
    public GenerationState State { get; private set; } = GenerationState.Idle;
    public int ProgressCurrent { get; private set; }
    public int ProgressTotal { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>
    /// Synchronously generates thumbnails for all subparts that don't have one yet.
    /// Must be called from the main game thread (e.g., from [StarMapAfterGui] or ImGui callback).
    /// Briefly stalls the frame while GPU work completes — expected behaviour.
    /// </summary>
    public void GenerateAll()
    {
        if (State == GenerationState.Generating || State == GenerationState.Done)
            return;

        if (Universe.CurrentSystem == null)
        {
            LastError = "No celestial system loaded. Load a system first.";
            State = GenerationState.Failed;
            Console.WriteLine("inanimate-carbon-rod: GenerateAll skipped — Universe.CurrentSystem is null");
            return;
        }

        State = GenerationState.Generating;
        LastError = null;

        try
        {
            RunGenerationPass();
            State = GenerationState.Done;
            Console.WriteLine($"inanimate-carbon-rod: Generated {SubpartThumbnailCache.All.Count} subpart thumbnails.");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = GenerationState.Failed;
            Console.WriteLine($"inanimate-carbon-rod: GenerateAll failed — {ex}");
        }
    }

    private void RunGenerationPass()
    {
        // --- Collect candidates ---
        List<PartTemplate> subparts = ModLibrary.AllParts.GetList()
            .Where(p => p.IsSubPart && !p.IsHidden && p.Thumbnail == null)
            .ToList();

        ProgressCurrent = 0;
        ProgressTotal = subparts.Count;

        if (subparts.Count == 0)
        {
            Console.WriteLine("inanimate-carbon-rod: No subparts to generate thumbnails for.");
            return;
        }

        Console.WriteLine($"inanimate-carbon-rod: Generating thumbnails for {subparts.Count} subparts...");

        // --- Get rendering infrastructure ---
        Renderer renderer = Program.GetRenderer();
        Viewport viewport = Program.RenderedViewport; // public static: Viewports[_renderedViewportIndex]
        Camera camera = viewport.GetCamera();

        // --- Save camera state ---
        int2 savedFramebufferSize = camera.FramebufferSize;
        int2 savedViewportSize = viewport.Size;
        IFollowable? savedFollowing = camera.Following;

        // --- Set up thumbnail camera ---
        camera.Unfollow();
        int2 thumbSize = new int2(ThumbnailRenderer.SIZE);
        camera.Resize(thumbSize);
        viewport.Size = thumbSize;
        camera.LocalPosition = double3.Zero;
        camera.LocalRotation = doubleQuat.Identity;
        camera.LocalScale = double3.One;
        camera.OnFrame(1.0 / 60.0);

        // --- Create render infrastructure ---
        ThumbnailPart root = new ThumbnailPart(camera);
        using ThumbnailRenderer thumbRenderer = new ThumbnailRenderer(renderer);
        PartModelRenderer.ColorData.BeginThumbnailPass(thumbRenderer.RenderPass, thumbRenderer.SampleCount);

        int frameIndex = 0;

        try
        {
            for (int i = 0; i < subparts.Count; i++)
            {
                PartTemplate subpart = subparts[i];
                RenderOneSubpart(subpart, root, thumbRenderer, renderer, viewport, camera, ref frameIndex);
                ProgressCurrent = i + 1;
            }
        }
        finally
        {
            // Always clean up even if a subpart fails
            PartModelRenderer.ColorData.EndThumbnailPass();
            root.Dispose();

            // --- Restore camera state ---
            camera.Resize(savedFramebufferSize);
            viewport.Size = savedViewportSize;
            if (savedFollowing != null)
                camera.SetFollow(savedFollowing, tidalLocking: false);
            camera.OnFrame(1.0 / 60.0);
        }
    }

    private static void RenderOneSubpart(
        PartTemplate subpart,
        ThumbnailPart root,
        ThumbnailRenderer thumbRenderer,
        Renderer renderer,
        Viewport viewport,
        Camera camera,
        ref int frameIndex)
    {
        // 1. Allocate GPU image (mirrors ThumbnailCreator.CreateThumbnailImage)
        ImageEx.CreateInfo createInfo = new ImageEx.CreateInfo
        {
            Name = "Thumbnail_" + subpart.Id,
            AllocPreference = MemoryPreference.PreferGpu,
            ImageArrayLayers = 1,
            ImageInitialLayout = VkImageLayout.Undefined,
            ImageType = VkImageType._2D,
            ImageExtent = new VkExtent3D
            {
                Width = ThumbnailRenderer.SIZE,
                Height = ThumbnailRenderer.SIZE,
                Depth = 1
            },
            ImageUsage = VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.TransferDstBit
                       | VkImageUsageFlags.SampledBit | VkImageUsageFlags.ColorAttachmentBit,
            ImageFormat = ThumbnailRenderer.ColorFormat,
            ImageMipLevels = ThumbnailRenderer.MipLevels,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageSharingMode = VkSharingMode.Exclusive,
            ImageTiling = VkImageTiling.Optimal
        };
        subpart.Thumbnail = new ThumbnailReference();
        subpart.Thumbnail.CreateImageView(renderer.Device, createInfo, VkImageViewType._2D,
            new VkImageSubresourceRange
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = ThumbnailRenderer.MipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            });

        // 2. Build ThumbnailPart child for this subpart's own mesh
        //    (mirrors AddPart, but for the subpart itself rather than its children)
        var syntheticInstance = new PartInstance { InstanceOf = subpart.Id };
        var child = new ThumbnailPart(root, syntheticInstance);

        if (child.Model == null && child.ModelDynamic == null)
        {
            // Subpart has no renderable mesh — skip cleanly
            subpart.Thumbnail.Dispose();
            subpart.Thumbnail = null;
            Console.WriteLine($"inanimate-carbon-rod: Skipped {subpart.Id} (no mesh)");
            return;
        }

        root.AddChild(child);

        // 3. Position camera (mirrors ThumbnailCreator.MoveRootPart)
        if (subpart.Thumbnail.ModelTransform != null)
        {
            root.Transform = subpart.Thumbnail.ModelTransform.Create();
        }
        else
        {
            float radius = root.ComputeBoundingSphereRadius();
            float dist = radius / (float)Math.Sin(camera.GetFieldOfView() * 0.5f);
            root.LocalPosition = Double3Ex.Forward * (camera.NearPlane + dist);
            root.LocalRotation = doubleQuat.CreateFromYawPitchRoll(Math.PI, Math.PI / 4.0, 0.0);
            root.LocalScale = Double3Ex.One;
        }

        // 4. Drive render (mirrors ThumbnailCreator.PreparePartThumbnails inner loop)
        camera.OnFrame(1.0 / 60.0);
        Program.Instance.UpdateShaderData(1.0 / 60.0, viewport);
        root.UpdateRenderData(viewport, frameIndex);
        Program.Instance.UpdateRenderingResources(frameIndex);

        thumbRenderer.RenderThumbnail(
            new PrePassThumbnailCommand(viewport, frameIndex,
                Program.GetCSMSystem(), Program.LightSystem, Program.PlanetAtmosphereRenderer),
            new PassThumbnailCommand(viewport, frameIndex),
            new PostPassThumbnailCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer),
            subpart.Id,
            out VkFence fence);

        // 5. GPU synchronization (exact mirror of game code)
        renderer.Device.WaitForFence(fence, IntPtr.MaxValue);
        renderer.Device.ResetFences(MemoryMarshal.CreateReadOnlySpan(ref fence, 1));
        renderer.Device.DestroyFence(fence, null);
        PartModelRenderer.ClearFrameData(frameIndex);
        Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = false;
        Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = false;

        // 6. Advance frame index (2-frame buffering)
        frameIndex = (frameIndex + 1) % 2;

        // 7. Reset for next subpart
        root.ClearAndDisposeChildren();
        root.LocalPosition = double3.Zero;
        root.LocalRotation = doubleQuat.Identity;
        root.LocalScale = Double3Ex.One;
        Program.LightSystem.ClearLights();

        // 8. Store in cache
        SubpartThumbnailCache.Store(subpart.Id, subpart.Thumbnail!);
        Console.WriteLine($"inanimate-carbon-rod: Generated thumbnail for {subpart.Id}");
    }
}
```

**Depends on**: Task 1 (project), Task 2 (cache)

---

### TASK 4 — InanimeCarbonicRodSubmod (ISubmod Implementation)

**File**: `inanimate-carbon-rod.lib/InanimeCarbonicRodSubmod.cs`

**Goal**: The `ISubmod` that wires the generator and cache to an ImGui UI. This class is used by
both the standalone mod and the grant supermod.

**UI design**:
- Status line (Idle / Generating / Done / Failed)
- Warning text about frame freeze
- "Generate Subpart Thumbnails" button (disabled if already generating or done)
- Progress bar while generating (shown during the synchronous call — updates per subpart)
- After generation: scrollable grid of 64×64 thumbnail images with subpart ID tooltips
- If no thumbnails: show explanatory text

```csharp
using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.InanimateCarbonRodLib;

public sealed class InanimeCarbonicRodSubmod : ISubmod
{
    public string Name => "Inanimate Carbon Rod";

    private readonly SubpartThumbnailGenerator _generator = new();

    public void Initialize() { }

    public void Update(double dt) { }

    public void RenderContent()
    {
        ImGui.TextColored(new float4(1f, 0.85f, 0.1f, 1f), "Subpart Thumbnail Generator");
        ImGui.Separator();
        ImGui.Spacing();

        // Status
        switch (_generator.State)
        {
            case GenerationState.Idle:
                ImGui.TextDisabled("Status: Idle");
                ImGui.Spacing();
                ImGui.TextColored(new float4(1f, 0.7f, 0.2f, 1f),
                    "⚠ Generating thumbnails will briefly freeze the game.");
                ImGui.TextWrapped(
                    "Click the button below to generate thumbnails for all subparts. " +
                    "This only needs to be done once per session.");
                ImGui.Spacing();
                if (ImGui.Button("Generate Subpart Thumbnails"))
                    TriggerGeneration();
                break;

            case GenerationState.Generating:
                ImGui.Text("Status: Generating...");
                float progress = _generator.ProgressTotal > 0
                    ? (float)_generator.ProgressCurrent / _generator.ProgressTotal
                    : 0f;
                ImGui.ProgressBar(progress, new float2(-1f, 0f),
                    $"{_generator.ProgressCurrent} / {_generator.ProgressTotal}");
                break;

            case GenerationState.Done:
                ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f),
                    $"Status: Done — {SubpartThumbnailCache.All.Count} thumbnails generated.");
                ImGui.Spacing();
                RenderThumbnailGrid();
                break;

            case GenerationState.Failed:
                ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f),
                    $"Status: Failed — {_generator.LastError}");
                ImGui.Spacing();
                if (ImGui.Button("Retry"))
                    TriggerGeneration();
                break;
        }
    }

    private void TriggerGeneration()
    {
        // Called from ImGui callback — we are on the main thread. Safe to call GPU work directly.
        _generator.GenerateAll();
    }

    private void RenderThumbnailGrid()
    {
        if (!SubpartThumbnailCache.HasAny)
        {
            ImGui.TextDisabled("No thumbnails available (all subparts may lack meshes).");
            return;
        }

        const float thumbSize = 64f;
        const float cellPadding = 6f;
        float contentWidth = ImGui.GetContentRegionAvail().X;
        int cols = (int)Math.Max(1, Math.Floor(contentWidth / (thumbSize + cellPadding)));

        ImGui.BeginChild("subpart_thumb_scroll", new float2(0, 0), ImGuiChildFlags.None,
            ImGuiWindowFlags.HorizontalScrollbar);

        if (ImGui.BeginTable("subpart_thumb_grid", cols,
            ImGuiTableFlags.None, new float2(0, 0), 0))
        {
            foreach (var (id, thumb) in SubpartThumbnailCache.All)
            {
                // Register with ImGui backend on first display
                thumb.CreateImGuiThumbnail(Program.LinearClampedSampler);

                ImGui.TableNextColumn();
                ImGui.Image(thumb.ImGuiImageRef, new float2(thumbSize));
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
                    ImGui.SetTooltip(id);
            }
            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    public void Dispose()
    {
        // ThumbnailReference GPU resources are owned by PartTemplate.Thumbnail — not disposed here.
        // SubpartThumbnailCache is static and lives for the process lifetime.
    }
}
```

**Depends on**: Task 1, Task 2, Task 3

---

### TASK 5 — Standalone Mod Entry Point

**File**: `inanimate-carbon-rod/Mod.cs`

**Goal**: StarMap entry point for the standalone mod. Hosts the submod in its own toggleable
ImGui window. Window toggled with **F10** (check other mods to confirm no collision; adjust if needed).

```csharp
using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.InanimateCarbonRodLib;
using StarMap.API;

namespace MeowSci.InanimateCarbonRod;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private InanimeCarbonicRodSubmod _submod = null!;
    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new InanimeCarbonicRodSubmod();
            _submod.Initialize();
            _isInitialized = true;
            Console.WriteLine("inanimate-carbon-rod: Loaded.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"inanimate-carbon-rod: Error during init: {ex}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        try { _submod.Update(dt); }
        catch (Exception ex) { Console.WriteLine($"inanimate-carbon-rod: OnBeforeUi error: {ex}"); }
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        try
        {
            // Toggle with F10 — verify no collision with other mods
            if (ImGui.IsKeyPressed(ImGuiKey.F10))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"inanimate-carbon-rod: OnAfterUi error: {ex}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod?.Dispose();
            _isDisposed = true;
            Console.WriteLine("inanimate-carbon-rod: Unloaded.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"inanimate-carbon-rod: Unload error: {ex}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(520f, 420f), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Inanimate Carbon Rod", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
```

**Depends on**: Task 1, Task 4

---

### TASK 6 — Grant Supermod Integration

**Goal**: Register `inanimate-carbon-rod.lib` with the grant supermod so `InanimeCarbonicRodSubmod`
appears as a collapsible section in the grant window.

**Files to modify**:

#### `grant/grant.csproj`
Add a `ProjectReference` to the lib (alongside existing .lib references):
```xml
<ProjectReference Include="..\inanimate-carbon-rod.lib\inanimate-carbon-rod.lib.csproj" />
```

#### `grant/Mod.cs`
1. Add using at the top:
   ```csharp
   using MeowSci.InanimateCarbonRodLib;
   ```
2. In `OnFullyLoaded()`, instantiate and add the submod to `_submods` in the desired display order
   (alphabetically or logically near related submods):
   ```csharp
   var inanimeCarbonicRod = new InanimeCarbonicRodSubmod();
   _submods.Add(inanimeCarbonicRod);
   ```
   The rest of the grant initialization loop (`Initialize()`, `_submodVisibility` registration)
   already handles all submods uniformly — no further changes needed.

**⚠ Read `grant/Mod.cs` fully before editing** to understand where `_submods` is populated and
what the exact pattern looks like — it has changed over time. Insert the new submod in the correct
location.

**Depends on**: Task 4

---

### TASK 7 — Build Verification

**Goal**: Confirm the solution compiles cleanly.

**Steps**:
1. Run `dotnet build` from the repo root.
2. Fix any missing type references (add DLLs to `inanimate-carbon-rod.lib.csproj` as needed).
3. Fix any namespace/using errors.
4. Run `dotnet build` again until it passes with zero errors.

**Common issues to expect**:
- `ThumbnailRenderer`, `ThumbnailPart`, `ThumbnailReference`, `PartModelRenderer`, Vulkan types, etc.
  are all in `KSA.dll` — if any are not found, double-check the using directives and namespaces
  (`KSA.Rendering`, `KSA.Rendering.Thumbnails`) rather than adding new DLL references.
- `Double3Ex` not found → verify the namespace/assembly for this helper (check decomp usage)
- `IFollowable` not found → check which namespace contains camera-following interfaces

**Depends on**: Tasks 1–6

---

## Key References for Implementers

| File | What to read |
|---|---|
| `decomp/ksa/KSA.Rendering/ThumbnailCreator.cs` | The exact rendering loop to mirror (159 lines total) |
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailRenderer.cs` | ThumbnailRenderer constructor + RenderThumbnail signature |
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailPart.cs` | ThumbnailPart constructors and helpers |
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailReference.cs` | CreateImageView, CreateImGuiThumbnail |
| `decomp/ksa/KSA/PartModelRenderer.cs` | ColorData.BeginThumbnailPass/EndThumbnailPass, ClearFrameData |
| `decomp/ksa/KSA/Program.cs` | GetRenderer(), Instance, LightSystem, GetCSMSystem(), PlanetAtmosphereRenderer, LinearClampedSampler, RenderedViewport (~line 324), DeviceHostSharedMemoryDebug |
| `decomp/ksa/KSA/PartTemplate.cs` | IsSubPart, IsHidden, Thumbnail, Components |
| `decomp/ksa/KSA/PartInstance.cs` | InstanceOf field, GetTemplate() |
| `decomp/ksa/KSA/Universe.cs` | CurrentSystem null check |
| `decomp/ksa/KSA/VehicleEditor.cs` | Reference for how ImGui.ImageButton is used with ThumbnailReference (lines ~1412-1415) |
| `grant/Mod.cs` | How submods are registered in the grant supermod |
| `ksa-abstractions.lib/ISubmod.cs` | Interface definition |
| `zippo.lib/` | Simplest example of a mod.lib to compare against |
