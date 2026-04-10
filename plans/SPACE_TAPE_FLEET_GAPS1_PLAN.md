# Space-Tape Fleet Gaps #1 — Interaction & UX Plan

## Problem Statement

The space-tape Part editor has five interaction gaps compared to the game's built-in vehicle editor:

1. **Gizmo drag breaks when mouse leaves gizmo lines** — dragging translate/rotate/scale gizmos only works while cursor stays over the rendered mesh
2. **No hover highlight** — hovering over a SubPart shows no visual feedback (no highlight shader)
3. **Click-to-select broken** — clicking a SubPart doesn't select it (though clicking empty space deselects correctly)
4. **No quick-flip rotation hotkeys** — need D (Y-axis) and F (X-axis) for 45° cumulative rotation snaps
5. **No click-and-drag plane-locked movement** — need P key to toggle through pan modes (Normal → X → Y → Z → Normal) constraining SubPart drag to a single plane

---

## Todo 1: Fix Gizmo Drag — Skip Raycast While Grabbed

### File to edit

`space-tape.lib/PartEditorInteraction.cs`

### Root cause

On line 55, `_gizmos.UpdateRaycast(ray, viewport)` runs **every frame unconditionally**. When the cursor moves off the gizmo mesh during a drag, `UpdateRaycast` (in `PartEditorGizmos.cs` lines 72-99) sets `HighlightedGizmo = null` and `HighlightedSegmentIndex = -1`. This causes the drag conditions on lines 104, 135, 166 to fail because they all check `_gizmos.HighlightedGizmo == _gizmos.TranslateGizmo` (or RotationGizmo/ScaleGizmo).

The game's VehicleEditor (`decomp/ksa/KSA/VehicleEditor.cs` line 488-569) solves this by using a `flag` variable: `bool flag = GizmoGrabbed;` then `if (!flag && flag2)` to skip the entire raycast block when grabbed. This preserves the locked `HighlightedGizmo` + `HighlightedGizmoSegmentIndex` for the duration of the drag.

### Exact change

In `PartEditorInteraction.cs`, replace line 54-55:

```csharp
        // Raycast gizmos first
        _gizmos.UpdateRaycast(ray, viewport);
```

With:

```csharp
        // Only raycast gizmos when NOT dragging — preserves locked axis during drag
        // (matches game's VehicleEditor pattern: decomp/ksa/KSA/VehicleEditor.cs:488-569)
        if (!_gizmos.GizmoGrabbed)
        {
            _gizmos.UpdateRaycast(ray, viewport);
        }
```

Also wrap the part-raycast block (lines 58-76) so it's skipped during gizmo drag (no point highlighting parts while dragging a gizmo axis). Replace:

```csharp
        // Raycast parts when no gizmo is hit
        Part? highlighted = null;
        if (_gizmos.HighlightedGizmo == null)
        {
```

With:

```csharp
        // Raycast parts when no gizmo is hit and not dragging a gizmo
        Part? highlighted = null;
        if (_gizmos.HighlightedGizmo == null && !_gizmos.GizmoGrabbed)
        {
```

### Verification

After this change, clicking a gizmo axis (e.g. the red X arrow) and dragging the mouse anywhere on screen should continue moving the SubPart along that axis. Previously, moving the mouse off the arrow would stop the drag.

### Build

```bash
dotnet build space-tape/space-tape.csproj
```

---

## Todo 2: Fix Click-to-Select — Use RayCastEgoSubPart for Leaf Parts

### File to edit

`space-tape.lib/PartEditorInteraction.cs`

### Root cause

The current raycast loop (lines 62-75) calls `part.RayCastEgo()` on each editor Part. Looking at the decompiled KSA source (`decomp/ksa/KSA/Part.cs` lines 1153-1185), `RayCastEgo()` iterates the Part's `SubParts` array (children) and calls `RayCastEgoSubPart()` on each child:

```csharp
// From decomp/ksa/KSA/Part.cs:1163-1166
int length = SubParts.Length;
while (length-- > 0)
{
    if (SubParts[length].RayCastEgoSubPart(...)
```

But editor Parts are created from SubPart templates in `PartEditorScene.CreatePartFromPlacement()` (line 231-241). These are **leaf-level Parts with no children** — their `SubParts` array is empty. So `RayCastEgo` iterates zero children and returns `false` every time.

The fix is to call `RayCastEgoSubPart()` directly on each editor Part. This method (`decomp/ksa/KSA/Part.cs` lines 1187-1222) tests the Part's own mesh via its `MeshViewModule`, performing a bounding-sphere test then a watertight mesh raycast.

### Exact change

In `PartEditorInteraction.cs`, replace the entire part-raycast block (lines 57-76):

```csharp
        // Raycast parts when no gizmo is hit
        Part? highlighted = null;
        if (_gizmos.HighlightedGizmo == null)
        {
            double closest = double.MaxValue;
            foreach (Part part in scene.EditorParts)
            {
                if (part.RayCastEgo(in matrixAsmb2Ego, ray,
                    out double nearT, out double _,
                    out double3 _, out double3 _,
                    out double3 _, out double3 _,
                    out Part? closestSub, out Part? _)
                    && nearT < closest)
                {
                    closest = nearT;
                    // closestSub is a sub-part; its PartParent is the top-level editor part
                    highlighted = closestSub?.PartParent ?? closestSub;
                }
            }
        }
```

With:

```csharp
        // Raycast parts when no gizmo is hit and not dragging a gizmo
        Part? highlighted = null;
        if (_gizmos.HighlightedGizmo == null && !_gizmos.GizmoGrabbed)
        {
            double closest = double.MaxValue;
            foreach (Part part in scene.EditorParts)
            {
                // Try RayCastEgoSubPart first — editor Parts are leaf-level (no children)
                // so RayCastEgo (which iterates SubParts children) returns false.
                // RayCastEgoSubPart tests THIS Part's own mesh via MeshViewModule.
                // See decomp/ksa/KSA/Part.cs:1187-1222 for implementation.
                if (part.RayCastEgoSubPart(in matrixAsmb2Ego, ray,
                    out double nearT, out double _,
                    out double3 _, out double3 _,
                    out double3 _, out double3 _)
                    && nearT < closest)
                {
                    closest = nearT;
                    highlighted = part;
                }

                // Also try RayCastEgo for imported Parts that may have SubParts children
                if (part.RayCastEgo(in matrixAsmb2Ego, ray,
                    out double nearT2, out double _2,
                    out double3 _3, out double3 _4,
                    out double3 _5, out double3 _6,
                    out Part? closestSub, out Part? _7)
                    && nearT2 < closest)
                {
                    closest = nearT2;
                    highlighted = closestSub?.PartParent ?? closestSub ?? part;
                }
            }
        }
```

### Note on `_gizmos.GizmoGrabbed` guard

This change also incorporates the gizmo-drag guard from Todo 1. If Todo 1 has already been applied, the `&& !_gizmos.GizmoGrabbed` will already be there — just make sure it's not duplicated.

### Verification

After this change, hovering over a SubPart should cause `highlighted` to be non-null. Clicking a SubPart should select it (index updates on line 88-89). The existing click-to-deselect (lines 93-96) already works because `highlighted == null` when clicking empty space.

### Build

```bash
dotnet build space-tape/space-tape.csproj
```

---

## Todo 3: Add Hover Highlight & Selection Visual Feedback

**Depends on:** Todo 2 (raycast must be working for highlight to trigger)

### Files to edit

`space-tape.lib/PartEditorInteraction.cs`

### How the game does it

The game's `Part` class (`decomp/ksa/KSA/Part.cs`) has two relevant boolean properties:
- **`Part.Highlighted`** (line 429-445): Setting to `true`/`false` auto-propagates to all `SubParts` children. The getter ORs the backing field with `HighlightedForStage | HighlightedForResources | HighlightedForSequence`. The setter sets the backing field.
- **`Part.Selected`** (line 542-557): Setting to `true`/`false` auto-propagates to all `SubParts` children.

These are encoded in `PartModelModule.UpdateRenderData()` (`decomp/ksa/KSA/PartModelModule.cs` lines 76-96):
```csharp
int num = 0;
num |= (Parent.Highlighted ? 1 : 0);     // bit 0 → highlight shader
num |= (Parent.Selected ? 1 : 0) << 3;   // bit 3 → selection shader
PartModel.PerInstanceData instanceData = new PartModel.PerInstanceData {
    ModelMatrix = ..., StateBitFlag = num
};
```

The render pipeline (`PartRenderHelper.cs` line 22) already calls `part.Tree.UpdateRenderData()` each frame, which calls `PartModelModule.UpdateRenderData()` which reads these properties. So we just need to SET them — the visual effect is automatic.

### Exact changes

**Step 1: Add tracking fields** — At the top of the `PartEditorInteraction` class (after line 16), add:

```csharp
    private Part? _highlightedPart;
    private Part? _selectedPart;
```

**Step 2: Add hover highlight logic** — After the part-raycast block (after the closing `}` of the `if (_gizmos.HighlightedGizmo == null ...)` block), insert:

```csharp
        // Update hover highlight — set Part.Highlighted for GPU shader feedback
        // (decomp/ksa/KSA/Part.cs:429-445 — auto-propagates to SubParts)
        if (highlighted != _highlightedPart)
        {
            if (_highlightedPart != null) _highlightedPart.Highlighted = false;
            if (highlighted != null) highlighted.Highlighted = true;
            _highlightedPart = highlighted;
        }
```

**Step 3: Add selection visual feedback** — In the click-to-select block (lines 78-96), after `controller.SelectedPlacementIndex` is changed, update `Part.Selected`. Replace lines 78-96:

```csharp
        // Click to select / grab gizmo
        bool leftClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        if (leftClicked)
        {
            if (_gizmos.HighlightedGizmo != null)
            {
                _gizmos.GizmoGrabbed = true;
            }
            else if (highlighted != null)
            {
                int idx = IndexOf(scene, highlighted);
                if (idx >= 0) controller.SelectedPlacementIndex = idx;
                _gizmos.GizmoGrabbed = false;
            }
            else
            {
                controller.SelectedPlacementIndex = -1;
                _gizmos.GizmoGrabbed = false;
            }
        }
```

With:

```csharp
        // Click to select / grab gizmo
        bool leftClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        if (leftClicked)
        {
            if (_gizmos.HighlightedGizmo != null)
            {
                _gizmos.GizmoGrabbed = true;
            }
            else if (highlighted != null)
            {
                int idx = IndexOf(scene, highlighted);
                if (idx >= 0)
                {
                    UpdateSelection(scene, controller, idx);
                }
                _gizmos.GizmoGrabbed = false;
            }
            else
            {
                UpdateSelection(scene, controller, -1);
                _gizmos.GizmoGrabbed = false;
            }
        }
```

**Step 4: Add `UpdateSelection` helper** — Add this new private method to the class:

```csharp
    private void UpdateSelection(PartEditorScene scene, PartEditorController controller, int newIndex)
    {
        // Clear old selection visual
        if (_selectedPart != null) _selectedPart.Selected = false;

        controller.SelectedPlacementIndex = newIndex;

        // Set new selection visual (Part.Selected propagates to SubParts — decomp/ksa/KSA/Part.cs:542-557)
        if (newIndex >= 0 && newIndex < scene.EditorParts.Count)
            _selectedPart = scene.EditorParts[newIndex];
        else
            _selectedPart = null;

        if (_selectedPart != null) _selectedPart.Selected = true;
    }
```

**Step 5: Add cleanup method** — Add a public method for clearing visual state when the editor closes:

```csharp
    /// <summary>Clears hover/selection visual state on all tracked parts. Call when editor scene exits.</summary>
    public void ClearVisualState()
    {
        if (_highlightedPart != null) { _highlightedPart.Highlighted = false; _highlightedPart = null; }
        if (_selectedPart != null) { _selectedPart.Selected = false; _selectedPart = null; }
    }
```

**Step 6: Call `ClearVisualState` from scene exit** — In `SpaceTapeSubmod.cs`, the `Dispose()` method (line 95-102) should call `_interaction.ClearVisualState()` before `_scene.Dispose()`. Add a line before `_gizmos.Dispose()`:

```csharp
        _interaction.ClearVisualState();
```

Also, when selection changes from external sources (e.g. UI hierarchy list click at `PartEditorUi.cs` line 510-511), the `_selectedPart` tracking needs to stay in sync. To handle this, at the **beginning** of `Update()`, after computing `selectedPart` (line 46-48), check if it diverged:

```csharp
        // Sync selection visual if external code changed SelectedPlacementIndex
        if (selectedPart != _selectedPart)
        {
            if (_selectedPart != null) _selectedPart.Selected = false;
            _selectedPart = selectedPart;
            if (_selectedPart != null) _selectedPart.Selected = true;
        }
```

### Verification

Hovering over a SubPart should now show the game's built-in highlight shader. Clicking selects it (selection shader). Moving hover off clears the highlight. Clicking empty space deselects (clears selection shader).

### Build

```bash
dotnet build space-tape/space-tape.csproj
```

---

## Todo 4: Add Quick-Flip Hotkeys (D/F for 45° Rotation)

### File to edit

`space-tape.lib/PartEditorInteraction.cs`

### Feature spec

- **D key:** Rotate selected SubPart +45° around the Y-axis (cumulative — each press adds 45°)
- **F key:** Rotate selected SubPart +45° around the X-axis (cumulative — each press adds 45°)
- Only fires when a SubPart is selected AND ImGui is not capturing keyboard input
- Pushes undo before applying the rotation
- Updates both the runtime `Part` and the `SubPartPlacement` data

### Exact change

In `PartEditorInteraction.cs`, inside the `Update()` method, insert the following block **after** the mouse-release handling (after line 101 `_gizmos.GizmoGrabbed = false;`) and **before** the drag blocks (before line 103 `// Drag: translate`):

```csharp
        // Quick-flip hotkeys: D = +45° around Y-axis, F = +45° around X-axis
        if (selectedPart != null && !ImGui.GetIO().WantCaptureKeyboard)
        {
            bool flipD = ImGui.IsKeyPressed(ImGuiKey.D);
            bool flipF = ImGui.IsKeyPressed(ImGuiKey.F);
            if (flipD || flipF)
            {
                controller.PushUndo();
                double3 axis = flipD ? new double3(0, 1, 0) : new double3(1, 0, 0);
                doubleQuat rot = doubleQuat.CreateFromAxisAngle(axis, Math.PI / 4.0);
                selectedPart.Asmb2ParentAsmb = doubleQuat.Multiply(rot, selectedPart.Asmb2ParentAsmb);
                InvalidatePartMatrixCache(selectedPart);
                if (controller.SelectedPlacement != null)
                    controller.SelectedPlacement.Rotation = selectedPart.Asmb2ParentAsmb;
            }
        }
```

### API references

- `ImGui.IsKeyPressed(ImGuiKey.D)` — from `Brutal.ImGuiApi.ImGui`, returns true on the frame the key was first pressed (not held). Same pattern used in `space-tape/Mod.cs` line 46 with `ImGuiKey.F11`.
- `ImGui.GetIO().WantCaptureKeyboard` — returns true when an ImGui text input is focused (HotkeyGuard handles game hotkeys but we still need this for our own mod hotkeys).
- `controller.PushUndo()` — saves a deep clone of `CurrentPart` to the undo stack (`PartEditorState.cs` lines 142-155). Must be called before mutation.
- `doubleQuat.CreateFromAxisAngle(axis, angle)` — creates rotation quaternion. `Math.PI / 4.0` = 45°.
- `doubleQuat.Multiply(rot, existing)` — pre-multiplies to apply rotation in parent assembly space.
- `InvalidatePartMatrixCache(selectedPart)` — already exists in this file (line 208-211), sets `_matrixAsmb` field to Identity via reflection.

### Verification

Select a SubPart. Press D — it should visually rotate 45° around the Y-axis. Press D again — another 45° (cumulative). Press F — 45° around X-axis. Press Ctrl+Z (if undo is wired up) — should revert each flip. Type in a text input — D/F should not trigger.

### Build

```bash
dotnet build space-tape/space-tape.csproj
```

---

## Todo 5: Add Plane-Locked Click-and-Drag Movement (P Key Toggle)

**Depends on:** Todo 2 (raycast must work so SubParts can be clicked for dragging)

### Files to edit

1. `space-tape.lib/PartEditorInteraction.cs` — main drag logic + P key handler
2. `space-tape.lib/PartEditorUi.cs` — toolbar UI indicator for current pan mode

### Feature spec

- **P key** cycles: Normal → PlaneX → PlaneY → PlaneZ → Normal
- When in a plane mode, **left-click + drag on a SubPart** moves it constrained to that plane
- Movement uses ray-plane intersection in assembly space (NOT screen-space projection)
- The plane passes through the SubPart's position at drag start, with normal along the constrained axis
- A toolbar indicator shows the current mode
- Only active when editor scene is open and ImGui is not capturing keyboard

### Step 1: Add PanMode enum and state

In `PartEditorInteraction.cs`, add the enum at namespace level (before the class declaration):

```csharp
/// <summary>Pan constraint mode for click-and-drag SubPart movement.</summary>
public enum PanMode { Normal, PlaneX, PlaneY, PlaneZ }
```

Add fields to the `PartEditorInteraction` class:

```csharp
    /// <summary>Current plane-lock mode, toggled by P key.</summary>
    public PanMode CurrentPanMode { get; private set; } = PanMode.Normal;

    // Plane-drag state
    private bool _planeDragging;
    private double3 _planeDragOrigin;  // assembly-space position at drag start
    private double3 _planeDragNormal;  // assembly-space normal of the constraint plane
```

### Step 2: Add P key cycling

In `Update()`, near the quick-flip hotkey block (from Todo 4), add:

```csharp
        // P key cycles pan mode: Normal → PlaneX → PlaneY → PlaneZ → Normal
        if (!ImGui.GetIO().WantCaptureKeyboard && ImGui.IsKeyPressed(ImGuiKey.P))
        {
            CurrentPanMode = CurrentPanMode switch
            {
                PanMode.Normal => PanMode.PlaneX,
                PanMode.PlaneX => PanMode.PlaneY,
                PanMode.PlaneY => PanMode.PlaneZ,
                PanMode.PlaneZ => PanMode.Normal,
                _ => PanMode.Normal
            };
            Console.WriteLine($"space-tape: Pan mode → {CurrentPanMode}");
        }
```

### Step 3: Add plane-drag initiation

In the click-to-select block, when a part is clicked AND a plane mode is active, start a plane drag. Modify the `else if (highlighted != null)` branch:

```csharp
            else if (highlighted != null)
            {
                int idx = IndexOf(scene, highlighted);
                if (idx >= 0)
                {
                    UpdateSelection(scene, controller, idx);

                    // Start plane drag if a plane mode is active
                    if (CurrentPanMode != PanMode.Normal)
                    {
                        controller.PushUndo();
                        _planeDragging = true;
                        _planeDragOrigin = highlighted.PositionEgo(in matrixAsmb2Ego);
                        _planeDragNormal = CurrentPanMode switch
                        {
                            PanMode.PlaneX => new double3(1, 0, 0),  // YZ plane (normal = X)
                            PanMode.PlaneY => new double3(0, 1, 0),  // XZ plane (normal = Y)
                            PanMode.PlaneZ => new double3(0, 0, 1),  // XY plane (normal = Z)
                            _ => new double3(0, 1, 0)
                        };
                    }
                }
                _gizmos.GizmoGrabbed = false;
            }
```

### Step 4: Add plane-drag update logic

Add a new drag block **after** the existing gizmo drag blocks (after the scale drag block ending around line 199), **before** `_prevCursorPos = cursorPos;`:

```csharp
        // Plane-constrained drag: move SubPart on locked plane
        if (_planeDragging && selectedPart != null)
        {
            // Ray-plane intersection in ego space:
            // Plane passes through _planeDragOrigin with normal _planeDragNormal
            double denom = double3.Dot(ray.Direction, _planeDragNormal);
            if (Math.Abs(denom) > 1e-10)
            {
                double t = double3.Dot(_planeDragOrigin - ray.Origin, _planeDragNormal) / denom;
                if (t > 0)
                {
                    double3 hitPointEgo = ray.Origin + ray.Direction * t;

                    // Convert ego-space hit to parent-assembly-space
                    double4x4.Invert(selectedPart.MatrixParentAsmb2Ego(in matrixAsmb2Ego), out double4x4 invParent);
                    double3 newPosInParent = hitPointEgo.Transform(invParent);
                    selectedPart.PositionParentAsmb = newPosInParent;

                    InvalidatePartMatrixCache(selectedPart);
                    if (controller.SelectedPlacement != null)
                        controller.SelectedPlacement.Position = newPosInParent;
                }
            }
        }
```

### Step 5: End plane drag on mouse release

In the mouse-release block (lines 99-101), add `_planeDragging = false`:

```csharp
        bool leftReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left);
        if (leftReleased)
        {
            _gizmos.GizmoGrabbed = false;
            _planeDragging = false;
        }
```

### Step 6: Add UI indicator in toolbar

In `PartEditorUi.cs`, the toolbar method `RenderToolbar()` currently ends at line 302 with `ImGui.PopStyleVar()`. The `RenderToolbar` method takes `PartEditorGizmos gizmos` — we need to also pass the `PartEditorInteraction` to read `CurrentPanMode`.

**Option A (simpler):** Add the pan mode indicator directly in `SpaceTapeSubmod.RenderFloatingWindows()` or pass the interaction to `RenderEditorWindow`.

**Recommended approach:** Add `PartEditorInteraction` as an additional parameter to `RenderEditorWindow` and `RenderToolbar`.

In `PartEditorUi.cs`, change the `RenderEditorWindow` signature (line 70-77):

```csharp
    public void RenderEditorWindow(
        PartEditorController controller,
        PartEditorScene scene,
        PartEditorGizmos gizmos,
        PartEditorInteraction interaction,
        SubPartCatalog catalog,
        PartModWriter writer,
        CameraSnapController cameraSnap)
```

Change the `RenderToolbar` call (line 84):

```csharp
            RenderToolbar(controller, gizmos, interaction, scene, cameraSnap);
```

Change the `RenderToolbar` signature (line 104):

```csharp
    private void RenderToolbar(PartEditorController controller, PartEditorGizmos gizmos, PartEditorInteraction interaction, PartEditorScene scene, CameraSnapController cameraSnap)
```

At the **end** of `RenderToolbar`, just before `ImGui.PopStyleVar()` on line 302, but after the `ImGui.EndTable()` on line 300, add the pan mode indicator:

```csharp
        // Pan mode indicator (below the settings table)
        ImGui.Spacing();
        PanMode panMode = interaction.CurrentPanMode;
        float4 panColor = panMode switch
        {
            PanMode.PlaneX => new float4(1f, 0.3f, 0.3f, 1f),  // red
            PanMode.PlaneY => new float4(0.3f, 1f, 0.3f, 1f),  // green
            PanMode.PlaneZ => new float4(0.3f, 0.3f, 1f, 1f),  // blue
            _ => new float4(0.5f, 0.5f, 0.5f, 1f)              // gray
        };
        string panLabel = panMode switch
        {
            PanMode.PlaneX => "Pan: YZ Plane (lock X)",
            PanMode.PlaneY => "Pan: XZ Plane (lock Y)",
            PanMode.PlaneZ => "Pan: XY Plane (lock Z)",
            _ => "Pan: Normal"
        };
        ImGui.TextColored(panColor, panLabel);
        ImGui.SameLine();
        ImGui.TextDisabled("(P to cycle)");
```

Update the call site in `SpaceTapeSubmod.cs` line 77:

```csharp
        _ui.RenderEditorWindow(_controller, _scene, _gizmos, _interaction, _catalog, _writer, _cameraSnap);
```

### API references

- `ray` is already computed in `Update()` on line 51-52: `Ray ray = camera.ScreenToEgoRay(cursorPos)`
- `matrixAsmb2Ego` is already computed on line 42: `scene.GetMatrixAsmb2Ego(viewport)`
- `selectedPart.MatrixParentAsmb2Ego(in matrixAsmb2Ego)` — used in the existing translate drag (line 123)
- `double3.Dot(a, b)` — dot product, from `Brutal.Numerics`
- `PanMode` enum needs `using MeowSci.SpaceTapeLib;` in `PartEditorUi.cs` (already imported)

### Verification

1. Press P — toolbar shows "Pan: YZ Plane (lock X)" in red
2. Press P again — "Pan: XZ Plane (lock Y)" in green
3. Press P again — "Pan: XY Plane (lock Z)" in blue
4. Press P again — "Pan: Normal" in gray
5. In a plane mode, click and drag a SubPart — it should move freely within the constrained plane but not along the locked axis
6. Release mouse — drag ends
7. Ctrl+Z should undo the movement

### Build

```bash
dotnet build space-tape/space-tape.csproj
```

---

## Todo 6: Update README & Documentation

**Depends on:** All other todos

### Files to edit

1. `space-tape/README.md`
2. `REPOSITORY_INDEX.md`

### Changes to `space-tape/README.md`

**In the Features section** (around line 3-21), add these bullet points:

```markdown
- **Hover highlight** — SubParts highlight when hovered using the game's native highlight shader
- **Click-to-select** — click any SubPart in the 3D viewport to select it for editing
- **Selection visual feedback** — selected SubPart shows the game's native selection shader
- **Quick-flip rotation** — D key rotates +45° around Y-axis, F key rotates +45° around X-axis (cumulative)
- **Plane-locked drag** — P key cycles through pan modes (Normal / YZ / XZ / XY plane), click-and-drag to move SubParts constrained to a plane
```

**Add a new Hotkeys section** after the Features section:

```markdown
## Hotkeys

| Key | Action | Context |
|-----|--------|---------|
| F11 | Toggle editor window | Global |
| D | Rotate +45° around Y-axis | SubPart selected |
| F | Rotate +45° around X-axis | SubPart selected |
| P | Cycle pan mode (Normal → YZ → XZ → XY → Normal) | Editor active |
```

**In the Architecture section** (around line 59-79), update the `PartEditorScene (3D viewport)` block to mention the new interaction features:

```markdown
PartEditorScene (3D viewport)
├── GenericGizmo             → translate/rotate/scale for SubParts
├── ConnectorGizmo           → color-coded connector cubes
├── CameraSnapController     → snap views + grid plane overlay
├── PartEditorInteraction    → hover highlight, click-select, gizmo drag, quick-flip, plane drag
└── Origin marker            → axis lines at part origin
```

### Changes to `REPOSITORY_INDEX.md`

Find the space-tape entry and update its description to mention the new interaction features. Add mention of hover highlight, click-to-select, quick-flip hotkeys, and plane-locked drag.

### Build

No build needed for documentation changes.

---

## Dependency Graph

```
Todo 1 (gizmo drag fix)       ← no dependencies        → READY
Todo 2 (raycast fix)           ← no dependencies        → READY
Todo 4 (quick-flip hotkeys)    ← no dependencies        → READY
Todo 3 (hover highlight)       ← depends on Todo 2      → BLOCKED until Todo 2 done
Todo 5 (plane drag)            ← depends on Todo 2      → BLOCKED until Todo 2 done
Todo 6 (docs)                  ← depends on ALL others  → BLOCKED until all done
```

**Parallelization:** Todos 1, 2, 4 in parallel → then Todos 3, 5 in parallel → then Todo 6.

## File Edit Summary

| File | Todos | Changes |
|------|-------|---------|
| `space-tape.lib/PartEditorInteraction.cs` | 1, 2, 3, 4, 5 | Skip raycast while grabbed; fix raycast to use RayCastEgoSubPart; add highlight/selection tracking; add D/F/P hotkeys; add plane-drag logic; add PanMode enum |
| `space-tape.lib/PartEditorUi.cs` | 5 | Add `PartEditorInteraction` parameter to RenderEditorWindow/RenderToolbar; add pan mode indicator |
| `space-tape.lib/SpaceTapeSubmod.cs` | 3, 5 | Call ClearVisualState on dispose; pass `_interaction` to RenderEditorWindow |
| `space-tape/README.md` | 6 | Add new features, hotkeys table, update architecture |
| `REPOSITORY_INDEX.md` | 6 | Update space-tape entry description |

## Risks & Considerations

- **Part.Highlighted / Part.Selected side effects:** These are safe to set on editor Parts because they're in an isolated `VehicleEditingSpace` far from the main scene. No game systems enumerate parts in this space.
- **RayCastEgoSubPart needs MeshViewModule:** The `PartTree.CreateFromNewPartTree(part)` call in `PartEditorScene.CreatePartFromPlacement()` (line 239) populates the module tree including `MeshViewModule`. If a SubPart template has no mesh, the bounding sphere test will fail gracefully (returns false).
- **HotkeyGuard vs WantCaptureKeyboard:** HotkeyGuard patches the game's global hotkey system. Our mod hotkeys (D/F/P) use `ImGui.IsKeyPressed()` which is separate — we must check `!ImGui.GetIO().WantCaptureKeyboard` ourselves to avoid firing during text input.
- **Undo for plane drag:** Push undo once at drag start (not per-frame). The existing code pattern is: push undo before mutation, then mutate freely until drag ends.
- **Plane-drag normal is in ego space:** The constraint plane normal is fixed world-axis (1,0,0 / 0,1,0 / 0,0,1). Since the editing space uses `doubleQuat.Identity` for `Asmb2Ecl`, these align with assembly axes. The ray is already in ego space, and the plane origin is the part's ego-space position, so the intersection math works directly in ego space.
