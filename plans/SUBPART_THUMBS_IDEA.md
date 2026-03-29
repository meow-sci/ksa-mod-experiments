# Subpart Thumbnails — Runtime Generation Plan

## Problem Statement

The KSA vehicle editor displays 128×128 icon thumbnails for every selectable part. These thumbnails are
**generated at runtime via Vulkan rendering** during game startup — there are no pre-built image files.

Subparts (`IsSubPart == true`) are explicitly skipped during thumbnail generation. We want to generate
thumbnails for subparts at runtime using the same infrastructure, so they can be displayed in mod UIs
(e.g., a subpart picker panel in the **grant** supermod).

---

## Research Findings

### How the Game Generates Part Thumbnails

**Entry point:** `Program.cs → Load() → ThumbnailCreator.PreparePartThumbnails(Loading, Viewport)`

**Class:** `ThumbnailCreator` (`KSA.Rendering` namespace) — `public static class`

**Full rendering loop:**

```
PreparePartThumbnails(Loading inLoader, Viewport inRenderedViewport)
  Setup:
    - Grab Camera from inRenderedViewport
    - Save & resize Camera to ThumbnailRenderer.SIZE × ThumbnailRenderer.SIZE
    - Create ThumbnailPart inRoot (no model, just a camera anchor)
    - Create ThumbnailRenderer (Vulkan framebuffer: color R16G16B16A16SFloat + depth D16UNorm)
    - PartModelRenderer.ColorData.BeginThumbnailPass(renderPass, sampleCount)

  Per-part loop (ModLibrary.AllParts.GetList()):
    if (!item.IsSubPart):                      ← SUBPARTS SKIPPED HERE
      CreateThumbnailImage(item)               ← private: creates VkImage, sets item.Thumbnail
      AddPart(thumbnailPart, item)             ← private: adds SubPartInstances as ThumbnailPart children
      MoveRootPart(thumbnailPart, item.Thumbnail, camera)  ← private: positions via bounding sphere
      camera.OnFrame(dt)
      instance.UpdateShaderData(...)
      thumbnailPart.UpdateRenderData(viewport, frame)
      thumbnailRenderer.RenderThumbnail(PrePass, Pass, PostPass, item.Id, out fence)
      WaitForFence + ResetFences + DestroyFence
      PartModelRenderer.ClearFrameData(frame)
      frame = (frame + 1) % 2                 ← 2-frame buffering
      ResetRootPart(thumbnailPart)            ← private: clears children
      lightSystem.ClearLights()

  Teardown:
    - PartModelRenderer.ColorData.EndThumbnailPass()
    - thumbnailPart.Dispose()
    - Restore Camera size and following
```

### Key Data Structures

| Class | Namespace | Access | Role |
|---|---|---|---|
| `ThumbnailCreator` | `KSA.Rendering` | `public static` | Orchestrates rendering |
| `PreparePartThumbnails` | — | `public static` | Main entry point (Harmony-patchable) |
| `CreateThumbnailImage` | — | `private static` | Creates GPU VkImage, sets `PartTemplate.Thumbnail` |
| `AddPart` | — | `private static` | Builds ThumbnailPart child hierarchy from SubPartInstances |
| `MoveRootPart` | — | `private static` | Positions part using bounding sphere FOV calculation |
| `ResetRootPart` | — | `private static` | Clears ThumbnailPart children between renders |
| `_renderer` | — | `private static Renderer` | Vulkan renderer reference |
| `ThumbnailRenderer` | `KSA.Rendering.Thumbnails` | `public sealed` | Vulkan framebuffer (disposable) |
| `ThumbnailReference` | `KSA.Rendering.Thumbnails` | `public` | Holds `ImageViewEx` + `ImTextureRef` |
| `ThumbnailPart` | `KSA.Rendering.Thumbnails` | `public sealed` | Lightweight render node |
| `PartTemplate.Thumbnail` | `KSA` | `public` field | Null until generated |
| `PartTemplate.IsSubPart` | `KSA` | `public` field | `[XmlIgnore]` bool flag |
| `PartTemplate.SubPartInstances` | `KSA` | `public` field | The children this part renders |

### How Subparts Relate to Models

A top-level part's visual geometry is stored in **subpart templates** referenced via `SubPartInstances`.
Each `PartInstance` in `SubPartInstances` has `InstanceOf` (a string ID) pointing to a subpart's
`PartTemplate`. That subpart template has `PartModelModule.Template` or `PartModelDynamicModule.Template`
in its `Components` list — this is where the mesh lives.

`ThumbnailCreator.AddPart()` walks `inTemplate.SubPartInstances`, creates a `ThumbnailPart` per
`PartInstance` (which loads the mesh via `GetTemplate().Components`), and adds them as children of the
root `ThumbnailPart`.

**For a standalone subpart thumbnail**, the subpart IS the leaf node — its own `Components` contains
the mesh. We need to render the subpart itself, not its children. This requires creating a `ThumbnailPart`
directly from the subpart's template+components, rather than using `AddPart()` (which would early-return
if `SubPartInstances.Count == 0`).

### How Thumbnails Are Displayed in ImGui

```csharp
// Before drawing (call once per frame to register):
partTemplate.Thumbnail?.CreateImGuiThumbnail(Program.LinearClampedSampler);

// In ImGui render code:
ImGui.Image(partTemplate.Thumbnail.ImGuiImageRef, new float2(128f));
// or as a button:
ImGui.ImageButton("##id", partTemplate.Thumbnail.ImGuiImageRef, new float2(128f));
```

`CreateImGuiThumbnail` registers the Vulkan image view with the ImGui Vulkan backend:
```csharp
ImGuiImageRef = ImGuiBackend.Vulkan.AddTexture(inSampler, ImageView.VkImageView);
```

---

## Implementation Approach

### Recommended: Harmony Postfix on `PreparePartThumbnails`

Patch `ThumbnailCreator.PreparePartThumbnails` with a **postfix** that fires after the game finishes
normal thumbnail generation. The postfix receives the original `Loading` and `Viewport` parameters and
runs our own subpart rendering pass.

**Why postfix (not prefix)?** The game's setup (`BeginThumbnailPass`, camera resize, etc.) has
already run, and we need similar setup. However, by the time our postfix runs, the game's
`ThumbnailRenderer` has been disposed (it's a `using` statement). We create our **own**
`ThumbnailRenderer` instance.

**Why not transpiler?** Removing the `!item.IsSubPart` guard via IL transpiler would be the most
elegant solution but is brittle and hard to maintain. The postfix approach is more mod-friendly.

### Postfix Logic (Pseudocode)

```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(ThumbnailCreator), nameof(ThumbnailCreator.PreparePartThumbnails))]
static void AfterPreparePartThumbnails(Loading inLoader, Viewport inRenderedViewport)
{
    // 1. Reflect out the private _renderer field
    Renderer renderer = GetPrivateField<Renderer>(typeof(ThumbnailCreator), "_renderer");

    // 2. Grab camera and save state
    Camera camera = inRenderedViewport.GetCamera();
    int2 savedSize = camera.FramebufferSize;
    int2 savedViewportSize = inRenderedViewport.Size;
    IFollowable savedFollowing = camera.Following;

    // 3. Resize camera to thumbnail dimensions
    int2 thumbSize = new int2(ThumbnailRenderer.SIZE);
    camera.Resize(thumbSize);
    inRenderedViewport.Size = thumbSize;
    camera.Unfollow();
    camera.LocalPosition = double3.Zero;
    camera.LocalRotation = doubleQuat.Identity;
    camera.LocalScale = double3.One;
    camera.OnFrame(1f / 60f);

    // 4. Create render infrastructure
    ThumbnailPart root = new ThumbnailPart(camera);
    using ThumbnailRenderer thumbRenderer = new ThumbnailRenderer(renderer);
    BeginThumbnailPassReflected(thumbRenderer);  // reflects PartModelRenderer.ColorData.BeginThumbnailPass

    int frameIndex = 0;
    Program instance = Program.Instance;
    CascadedShadowSystem csm = Program.GetCSMSystem();
    LightSystem lights = Program.LightSystem;
    AtmosphereRenderer atmo = Program.PlanetAtmosphereRenderer;

    foreach (PartTemplate subpart in ModLibrary.AllParts.GetList())
    {
        if (!subpart.IsSubPart || subpart.IsHidden || subpart.Thumbnail != null)
            continue;

        // 5. Create GPU image for this subpart (mirror CreateThumbnailImage)
        CreateSubpartThumbnailImage(renderer, subpart);

        // 6. Build ThumbnailPart for this subpart's own mesh (NOT via AddPart!)
        BuildSubpartThumbnailPart(root, subpart);

        // 7. Position camera (mirror MoveRootPart)
        MoveRootPartForSubpart(root, subpart.Thumbnail, camera);

        // 8. Update & render (mirror the game's per-part loop body)
        camera.OnFrame(1f / 60f);
        instance.UpdateShaderData(1f / 60f, inRenderedViewport);
        root.UpdateRenderData(inRenderedViewport, frameIndex);
        instance.UpdateRenderingResources(frameIndex);

        thumbRenderer.RenderThumbnail(
            new PrePassThumbnailCommand(inRenderedViewport, frameIndex, csm, lights, atmo),
            new PassThumbnailCommand(inRenderedViewport, frameIndex),
            new PostPassThumbnailCommand(thumbRenderer, subpart, atmo),
            subpart.Id,
            out VkFence fence);

        renderer.Device.WaitForFence(fence, IntPtr.MaxValue);
        renderer.Device.ResetFences(MemoryMarshal.CreateReadOnlySpan(ref fence, 1));
        renderer.Device.DestroyFence(fence, null);
        PartModelRenderer.ClearFrameData(frameIndex);

        frameIndex = (frameIndex + 1) % 2;
        root.ClearAndDisposeChildren();
        root.LocalPosition = double3.Zero;
        root.LocalRotation = doubleQuat.Identity;
        root.LocalScale = double3.One;
        lights.ClearLights();

        SubpartThumbnails[subpart.Id] = subpart.Thumbnail!;
        inLoader.OnFrame();
    }

    EndThumbnailPassReflected();  // reflects PartModelRenderer.ColorData.EndThumbnailPass
    root.Dispose();

    // 9. Restore camera state
    camera.Resize(savedSize);
    inRenderedViewport.Size = savedViewportSize;
    if (savedFollowing != null)
        camera.SetFollow(savedFollowing, tidalLocking: false);
    camera.OnFrame(1f / 60f);
}
```

### The Subpart Mesh Problem

`AddPart()` adds `SubPartInstances` (children) of a part to the `ThumbnailPart` tree. For a subpart
being rendered in isolation, this returns immediately (subparts typically have no further sub-parts of
their own). We need `BuildSubpartThumbnailPart` to directly build a ThumbnailPart from the subpart's
own mesh components:

```csharp
static void BuildSubpartThumbnailPart(ThumbnailPart root, PartTemplate subpart)
{
    // Synthesise a PartInstance pointing at this subpart template
    // ThumbnailPart constructor reads GetTemplate().Components to find the mesh
    var syntheticInstance = new PartInstance { InstanceOf = subpart.Id };
    var child = new ThumbnailPart(root, syntheticInstance);
    root.AddChild(child);
}
```

`PartInstance.GetTemplate()` simply calls `ModLibrary.Get<PartTemplate>(InstanceOf)`, so a synthetic
`PartInstance` with the correct `InstanceOf` is sufficient. The `ThumbnailPart` constructor then finds
`PartModelModule.Template` or `PartModelDynamicModule.Template` in `GetTemplate().Components` and
loads the mesh.

---

## Project Structure

This belongs in a new library/mod pair following established conventions:

```
ksa-mod-experiments/
├── subpart-thumbs.lib/          ← Core library: Harmony patch + thumbnail cache
│   ├── subpart-thumbs.lib.csproj
│   ├── SubpartThumbnailPatch.cs ← Harmony postfix on PreparePartThumbnails
│   ├── SubpartThumbnailBuilder.cs ← BuildSubpartThumbnailPart, CreateSubpartThumbnailImage, etc.
│   └── SubpartThumbnailCache.cs ← Dictionary<string, ThumbnailReference> + access API
│
└── subpart-thumbs/              ← Thin mod wrapper (or integrate into grant)
    ├── subpart-thumbs.csproj
    └── SubpartThumbsMod.cs      ← StarMap mod entry point, loads the lib
```

Alternatively, integrate `subpart-thumbs.lib` directly into the **grant** supermod — any submod
needing subpart thumbnails references `subpart-thumbs.lib` as a project dependency.

---

## Step-by-Step Implementation Tasks

### Phase 1: Investigation (Before Writing Code)

1. **Check access levels of `PartModelRenderer.ColorData`** — is `BeginThumbnailPass` / `EndThumbnailPass` public?
   - File: `decomp/ksa/KSA.Rendering/PartModelRenderer.cs` (look for `ColorData` inner class)
   - If not public, we need reflection for these too.

2. **Verify `PostPassThumbnailCommand` constructor signature** — it takes `(ThumbnailRenderer, PartTemplate, AtmosphereRenderer)`.
   Confirm all are accessible.

3. **Verify `Program.GetCSMSystem()` and `Program.LightSystem`** — confirm they are public static.

4. **Check `Program.LinearClampedSampler`** — confirm it is public static (used for `CreateImGuiThumbnail`).

5. **Test synthetic `PartInstance`** — create a `PartInstance { InstanceOf = subpart.Id }` and verify
   `ThumbnailPart` can load the mesh without throwing. Some subpart templates may have no mesh (purely
   logical subparts) — handle gracefully.

6. **Count subparts** — `ModLibrary.AllParts.GetList().Count(p => p.IsSubPart)` to estimate memory cost.

### Phase 2: Library Implementation

7. **Create `subpart-thumbs.lib` project** (copy `.csproj` structure from another `.lib` project).

8. **`SubpartThumbnailCache.cs`**:
   ```csharp
   public static class SubpartThumbnailCache
   {
       private static readonly Dictionary<string, ThumbnailReference> _thumbnails = new();
       public static IReadOnlyDictionary<string, ThumbnailReference> All => _thumbnails;
       public static ThumbnailReference? Get(string subpartId) => _thumbnails.GetValueOrDefault(subpartId);
       internal static void Store(string id, ThumbnailReference t) => _thumbnails[id] = t;
   }
   ```

9. **`SubpartThumbnailBuilder.cs`** — implement `CreateSubpartThumbnailImage` and
   `BuildSubpartThumbnailPart` as described above. Handle:
   - Subparts with no mesh components → skip silently
   - Subparts with `ModelTransform` on Thumbnail → respect it (same as `MoveRootPart`)

10. **`SubpartThumbnailPatch.cs`** — implement the Harmony postfix. Wrap in try/catch with logging
    so failures don't crash game startup.

### Phase 3: Mod Wrapper

11. **Create `subpart-thumbs` mod project** or add to `grant`.

12. Add `[HarmonyPatch]` setup in the mod's `Initialize()`.

13. Verify patch fires during game load (add `Console.WriteLine` log).

### Phase 4: UI Usage

14. In any mod that wants subpart thumbnails, access via:
    ```csharp
    var thumb = SubpartThumbnailCache.Get(subpartId);
    thumb?.CreateImGuiThumbnail(Program.LinearClampedSampler);
    // in ImGui render:
    if (thumb != null)
        ImGui.Image(thumb.ImGuiImageRef, new float2(128f));
    ```

---

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| `PartModelRenderer.ColorData.BeginThumbnailPass` is private | Medium | Reflect it; method signature is stable |
| Synthetic `PartInstance` breaks if constructor does validation | Medium | Test early in Phase 1; may need a different PartInstance construction path |
| Subpart templates with no mesh (logical-only) | Low | Skip if `child.Model == null && child.ModelDynamic == null` after construction |
| `ThumbnailRenderer` creation requires a valid Vulkan device | Low | We have `_renderer` via reflection; same path as the game uses |
| Frame-fence lifetime issues | Medium | Mirror the game's exact fence create/wait/reset/destroy sequence |
| Memory: many subparts create many VkImages | Medium | Each 512×512 R16G16B16A16 = ~2MB + mips ≈ ~2.7MB per subpart; count subparts first |
| Startup time regression | Low | Subpart generation adds to load time; acceptable for a mod, add progress reporting |
| Private API breaks on game update | Medium | All reflection targets are documented here; update on game update |
| `PostPassThumbnailCommand` requires `PartTemplate` for mip copy | Low | We pass the subpart's `PartTemplate` directly |

---

## Open Questions

1. **How many subparts are there?** Run `ModLibrary.AllParts.GetList().Count(p => p.IsSubPart)` in a
   debug session to get an accurate count. This determines memory and startup time impact.

2. **Are all subparts visual?** Some subparts may be purely logical (no mesh). The code must not crash
   when `ThumbnailPart` has neither `Model` nor `ModelDynamic`.

3. **Is `PartModelRenderer.ColorData.BeginThumbnailPass` public?** Check
   `decomp/ksa/KSA.Rendering/PartModelRenderer.cs`. If not, determine if we can call our own
   `BeginThumbnailPass` equivalent or must reflect.

4. **Is there an existing mod hook post-thumbnail-load?** Check `Program.cs` around line 1344 for
   any event/callback after `PreparePartThumbnails`. If yes, prefer that over Harmony.

5. **Does `ThumbnailRenderer` depend on the game being in a specific render state?** It resets and
   creates its own framebuffer, but confirm there are no global render state prerequisites.

6. **What does `PrePassThumbnailCommand` require from `Universe.CurrentSystem`?** The game's own
   guard (`if (Universe.CurrentSystem == null) return;`) applies — if this is null for some reason
   post-startup, the postfix should bail gracefully.

---

## Example: Displaying Subpart Thumbnails in a Mod UI

```csharp
// In ISubmod.RenderContent() (called inside an existing ImGui Begin/End window):
void RenderContent()
{
    ImGui.Text("Subpart Browser");

    float cellSize = 128f + 8f;
    int cols = (int)Math.Max(1, Math.Floor(ImGui.GetContentRegionAvail().X / cellSize));

    if (ImGui.BeginTable("subpart_grid", cols))
    {
        foreach (var (id, thumb) in SubpartThumbnailCache.All)
        {
            thumb.CreateImGuiThumbnail(Program.LinearClampedSampler);
            ImGui.TableNextColumn();
            ImGui.ImageButton($"##sub_{id}", thumb.ImGuiImageRef, new float2(128f));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(id);
        }
        ImGui.EndTable();
    }
}
```

---

## Related Files

| Path | Purpose |
|---|---|
| `decomp/ksa/KSA.Rendering/ThumbnailCreator.cs` | Full game implementation to mirror |
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailRenderer.cs` | Vulkan framebuffer we instantiate ourselves |
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailReference.cs` | GPU image + ImGui handle (public) |
| `decomp/ksa/KSA.Rendering.Thumbnails/ThumbnailPart.cs` | Render node (public, reused directly) |
| `decomp/ksa/KSA/PartTemplate.cs` | Part definition; `IsSubPart`, `SubPartInstances`, `Thumbnail` |
| `decomp/ksa/KSA/PartInstance.cs` | Instance of a subpart; `InstanceOf` string, `GetTemplate()` |
| `decomp/ksa/KSA/VehicleEditor.cs` | Reference for how thumbnails are displayed in ImGui |
