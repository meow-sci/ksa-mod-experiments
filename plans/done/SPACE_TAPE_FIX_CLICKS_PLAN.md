# Space-Tape Part Editor — Fix SubPart Click Detection

## Problem Statement

The custom part editor cannot detect mouse hover or clicks on rendered SubPart meshes. The meshes render correctly (GPU rendering via `PartModelModule` works fine), and the selection highlight shader works when a Part is selected via the ImGui list. But:

- **Mouse hover**: Mousing over a rendered SubPart mesh never triggers the highlight
- **Mouse click**: Clicking on a SubPart never selects it — instead, any previously selected SubPart is deselected
- **Gizmo interaction** is unaffected — once a Part is selected via the list, gizmos respond normally
- **Pan mode** works because it bypasses raycast altogether (hijacks all clicks when active)

The root issue is that `highlighted` is **always null** in the raycast loop of `PartEditorInteraction.Update()`. This means the entire raycast pipeline from screen coordinates through to mesh intersection is failing silently.

## Architecture Overview

### How the Game's Vehicle Editor Does It

The game's `VehicleEditor.cs` (lines 569-682):
1. Gets ray: `camera.ScreenToEgoRay(CursorPositionScreen)`
2. Gets matrix: `VehicleEditingSpace.GetMatrixAsmb2Ego(camera)`
3. For each top-level Part, calls `part.RayCastEgo(in matrix, ray, ...)`
4. `RayCastEgo()` iterates `part.SubParts[]` and calls `RayCastEgoSubPart()` on each child
5. `RayCastEgoSubPart()` tests the Part's own mesh via `Modules.Get<MeshViewModule>()`
6. Returns `closestSubPart.PartParent` as the highlighted Part

Key: The game always raycasts against SubPart children. `RayCastEgo()` does NOT test the root Part itself — it iterates SubParts only.

### How Space-Tape Does It

`PartEditorInteraction.cs` (lines 96-127):
1. Gets ray: `camera.ScreenToEgoRay(ImGui.GetMousePos())`
2. Gets matrix: `scene.GetMatrixAsmb2Ego(viewport)` — same VehicleEditingSpace approach
3. For each editor Part, tries BOTH:
   - `part.RayCastEgoSubPart(...)` — tests the Part's own mesh
   - `part.RayCastEgo(...)` — iterates SubPart children
4. If either hits, sets `highlighted`

### Why Both Calls Fail

Each editor Part is created from a SubPart template ID:
```csharp
PartTemplate template = ModLibrary.Get<PartTemplate>(placement.SubPartTemplateId);
var part = new Part(placement.InstanceId, template);
```

- **`RayCastEgo()` fails**: This iterates `part.SubParts[]`. Since SubPart templates define leaf-level parts (no children of their own), `SubParts` is empty → the loop body never executes → returns false.

- **`RayCastEgoSubPart()` fails**: This tests the Part's own mesh via `Modules.Get<MeshViewModule>()`. It returns false if either:
  1. `MeshViewModule` was never created (span is empty — `return false` at line 1198)
  2. `MeshViewModule.MeshView.BoundingSphereRadius` is 0 (bounding sphere test fails)
  3. `MeshViewModule.MeshView.PositionCompare` is empty (watertight raycast has no triangles)

## Root Cause Analysis

### Hypothesis A: MeshViewModule Not Created (HIGH likelihood)

The `PartTemplate.Components` field holds module template data (PartModel, MeshView, Light, etc.). Both `PartModelModule.CreateComponents` and `MeshViewModule.CreateComponents` iterate this list looking for their respective Template types.

**Critical finding from decompiled code**: `PartTemplate.Components` has no `[XmlElement]` attributes in the decompiled source. The decompiler likely stripped them. The actual binary must have:
```csharp
[XmlElement("PartModel", typeof(PartModelModule.Template))]
[XmlElement("MeshView", typeof(MeshViewModule.Template))]
// ... other module types ...
public List<ModuleBase.TemplateDataBase> Components = new List<ModuleBase.TemplateDataBase>();
```

Rendering works, proving PartModel IS in Components. But there's a risk that MeshView is handled differently for SubPart templates — perhaps through a different code path that the decompiler obscures, or that only some SubPart templates have MeshView components populated in their Components list.

### Hypothesis B: MeshView MeshReference Not Loaded (MEDIUM likelihood)

Even if MeshViewModule is created, `MeshViewModule.MeshView` (a `MeshReference`) might not have been fully loaded. An unloaded MeshReference has:
- `BoundingSphereRadius = 0.0` → bounding sphere test always fails
- `PositionCompare = Array.Empty<double3>()` → no triangles to raycast

The `_VM` mesh variant must be loaded from the mesh atlas GLB. If the atlas loading skips _VM meshes or they're in a separate file, the MeshReference remains in its default unloaded state.

### Hypothesis C: Transform/Coordinate Mismatch (LOW likelihood)

Both rendering and raycasting use the same matrix (`scene.GetMatrixAsmb2Ego(viewport)`) and the same `Part.MatrixAsmb2Ego()` method. Since rendering puts meshes in the correct visual position, the matrix should also place the bounding sphere and raycast geometry correctly.

One subtle issue: `Part.MatrixAsmb2Ego()` has two code paths (line 593-604):
- `Program.Editor != null` → always recomputes (no cache)
- `Program.Editor == null` → uses cached `_matrixAsmb`

Space-tape runs without the game editor open, so it uses the cached path. After `part.Scale = ...` etc. are set, the cache is invalidated (set to Identity). On next access it recomputes correctly. This should not cause issues.

### Hypothesis D: Cursor Coordinate Mismatch (LOW likelihood)

Space-tape uses `ImGui.GetMousePos()` while the game editor uses raw GLFW `CursorPositionScreen`. These should be identical in most cases, but DPI scaling or viewport offsets could cause divergence. However, if this were the issue, gizmo raycasting would also fail (gizmos use the same ray).

## Diagnostic Plan

### Step 1: Verify MeshViewModule Existence

Add one-time diagnostic logging in `CreatePartFromPlacement()`:

```csharp
private static Part CreatePartFromPlacement(SubPartPlacement placement)
{
    PartTemplate template = ModLibrary.Get<PartTemplate>(placement.SubPartTemplateId);
    var part = new Part(placement.InstanceId, template);
    // ...
    PartTree.CreateFromNewPartTree(part);

    // DIAGNOSTIC — remove after fixing
    var meshViews = part.Modules.Get<MeshViewModule>();
    var partModels = part.Modules.Get<PartModelModule>();
    Console.WriteLine($"space-tape DIAG: Part '{placement.SubPartTemplateId}' — "
        + $"PartModelModules={partModels.Length}, MeshViewModules={meshViews.Length}, "
        + $"Components={template.Components.Count}");
    if (!meshViews.IsEmpty)
    {
        var mv = meshViews[0].MeshView;
        Console.WriteLine($"  MeshView: BoundingSphereRadius={mv.BoundingSphereRadius}, "
            + $"PositionCompare.Length={mv.PositionCompare.Length}, "
            + $"HostMesh={(mv.HostMesh != null ? "present" : "NULL")}");
    }
    return part;
}
```

### Step 2: Verify Raycast Parameters

Add per-frame diagnostic logging (throttled) in `PartEditorInteraction.Update()`:

```csharp
// DIAGNOSTIC — one-shot per frame, remove after fixing
if (_diagFrameCount++ % 300 == 0 && scene.EditorParts.Count > 0)
{
    Part p = scene.EditorParts[0];
    var mvs = p.Modules.Get<MeshViewModule>();
    double3 posEgo = p.PositionEgo(in matrixAsmb2Ego);
    Console.WriteLine($"space-tape RAYDIAG: ray.Origin={ray.Origin}, ray.Dir={ray.Direction}");
    Console.WriteLine($"  Part posEgo={posEgo}, MeshViewCount={mvs.Length}");
    if (!mvs.IsEmpty)
    {
        var mv = mvs[0].MeshView;
        double scale = Double3Ex.GetAbsoluteLargestElement(p.ScaleTotal);
        double r = mv.BoundingSphereRadius * scale;
        Console.WriteLine($"  BoundingSphere: center={posEgo}, radius={r}");
        double3 diff = posEgo - ray.Origin;
        Console.WriteLine($"  distToSphere={diff.Length()}, ray len needed={diff.Length() - r}");
    }
}
```

### Step 3: Interpret Results

| Diagnostic Result | Root Cause | Fix |
|---|---|---|
| `MeshViewModules=0` | MeshViewModule not created | Fix A below |
| `MeshViewModules>0, BoundingSphereRadius=0` | _VM mesh not loaded | Fix B below |
| `MeshViewModules>0, PositionCompare.Length=0` | _VM mesh data missing | Fix B below |
| `MeshViewModules>0, data looks valid` | Transform or ray issue | Fix C below |

## Fix Strategies

### Fix A: Manually Create MeshViewModule

If `MeshViewModule.CreateComponents` doesn't find MeshView templates in `template.Components` for SubPart templates, we need to create the module manually.

**Approach**: After Part construction, check if MeshViewModule exists. If not, try to find the _VM MeshReference from ModLibrary and create one manually.

```csharp
private static void EnsureMeshViewModule(Part part, string subPartTemplateId)
{
    if (!part.Modules.Get<MeshViewModule>().IsEmpty)
        return; // already exists

    // Convention: _VM mesh Id = subPartTemplateId + "_VM"
    MeshReference? vmMesh = null;
    try { vmMesh = ModLibrary.Get<MeshReference>(subPartTemplateId + "_VM"); }
    catch { /* not found */ }

    if (vmMesh == null)
    {
        // Try the template's own mesh (less ideal but better than nothing)
        var partModels = part.Modules.Get<PartModelModule>();
        if (!partModels.IsEmpty)
        {
            vmMesh = partModels[0].PartModel.MeshReference; // if accessible
        }
    }

    if (vmMesh != null && vmMesh.PositionCompare.Length > 0)
    {
        var module = new MeshViewModule(subPartTemplateId, vmMesh) { Parent = part };
        part.Modules.Add(module);
        Console.WriteLine($"space-tape: manually created MeshViewModule for '{subPartTemplateId}'");
    }
}
```

Call this in `CreatePartFromPlacement()` after `PartTree.CreateFromNewPartTree(part)`.

**Risk**: `MeshViewModule` constructor or `part.Modules.Add()` may have side effects we don't anticipate. The diagnostic step will tell us if this path is needed.

### Fix B: Handle Missing or Empty Mesh Data

If the MeshViewModule exists but mesh data is empty/zero, the _VM mesh wasn't loaded. Options:

1. **Find the _VM mesh by convention**: Try `ModLibrary.Get<MeshReference>(templateId + "_VM")` and replace the MeshViewModule's MeshView reference.

2. **Fall back to the rendering mesh**: Use the PartModelModule's mesh for raycasting. The rendering mesh has vertex data (it's what's on screen). Extract positions from `PartModel.MeshReference.HostMesh`:
   ```csharp
   var hostMesh = partModels[0].PartModel.MeshReference.HostMesh;
   var positions = hostMesh.GetVertexSpan<float3>(MeshAttribute.Position);
   var indices = hostMesh.IndexBuffer.ToSpan<int>();
   // Build PositionCompare array from these
   ```
   This is a heavier mesh (more triangles) but works.

3. **Use bounding-box raycast as fallback**: If no mesh data is available at all, compute a bounding box from the rendered vertices and do AABB raycast. Least precise but always works.

### Fix C: Fix Transform/Coordinate Issues

If the diagnostic shows valid MeshViewModule data but raycasts still miss:

1. **Check ray direction normalization**: Ensure `ray.Direction` is normalized (already done in code, but verify the value).

2. **Check for NaN in matrices**: Log and guard against NaN in `matrixAsmb2Ego` or `Part.MatrixAsmb2Ego()`.

3. **Verify `PositionEgo` vs `MatrixAsmb2Ego` consistency**: The bounding sphere center uses `PositionEgo` (based on `PositionVehicleAsmb`), while the triangle raycast uses `MatrixAsmb2Ego`. For non-SubPart Parts, both go through `PositionParentAsmb` — should be consistent. But verify with logging.

## Implementation Steps

### Phase 1: Diagnostic (do this first)

1. Add diagnostic logging to `PartEditorScene.CreatePartFromPlacement()` (Step 1 above)
2. Add throttled raycast logging to `PartEditorInteraction.Update()` (Step 2 above)
3. Run the game, open the part editor, add a SubPart, and check console output
4. Interpret results per Step 3 table

### Phase 2: Apply Fix Based on Diagnostics

Based on the diagnostic output, apply the appropriate fix (A, B, or C above).

### Phase 3: Clean Up

1. Remove all diagnostic logging
2. Verify hover highlighting works on all tested SubPart types
3. Verify click-to-select works
4. Verify gizmo interaction still works after the fix
5. Verify that moving/rotating/scaling a Part doesn't break subsequent hover detection

## Files to Modify

- `space-tape.lib/PartEditorScene.cs` — `CreatePartFromPlacement()` (diagnostic + fix)
- `space-tape.lib/PartEditorInteraction.cs` — raycast loop (diagnostic + potential fix)
- Possibly `space-tape.lib/PartEditorGizmos.cs` — if gizmo interaction order needs adjustment

## Open Questions

1. Does every SubPart template ID in the game have a corresponding `_VM` mesh? If not, some SubParts may never be clickable (only selectable via the UI list).
2. Are there SubPart templates without `<MeshView>` elements in their XML definition? (Some IVA props or purely decorative parts might lack them.)
3. If we must fall back to the rendering mesh for raycasting, what's the performance impact with high-poly meshes?
