# Space Tape Camera Controls — Implementation Plan

## Overview

Add camera snap-to-view functionality and an optional grid plane overlay to the Space Tape part editor. Six orthographic-style snap views (Front, Back, Left, Right, Top, Bottom) let the user instantly orient the camera to a standard vantage point. A translucent grid plane drawn in 3D provides a visual reference for the plane the camera is facing.

### Axis Convention (confirmed by user)

The Space Tape 3D editor uses the standard origin gizmo with:
- **X axis (red)** = right
- **Y axis (green)** = up
- **Z axis (blue)** = forward (into screen by convention)

**"Front" = the camera looks along −Z** (the blue axis points at you, you see the XY plane face-on).

### Snap View Definitions

| View   | Camera looks along | Camera "up" | Grid plane |
|--------|-------------------|-------------|------------|
| Front  | −Z                | +Y          | XY plane   |
| Back   | +Z                | +Y          | XY plane   |
| Left   | +X                | +Y          | YZ plane   |
| Right  | −X                | +Y          | YZ plane   |
| Top    | −Y                | −Z          | XZ plane   |
| Bottom | +Y                | +Z          | XZ plane   |

---

## Architecture

### New Files

| File | Purpose |
|------|---------|
| `space-tape.lib/CameraSnapController.cs` | State machine tracking active snap mode, computes camera orientation, draws grid lines |
| (UI additions in `PartEditorUi.cs`) | Camera snap button row in the toolbar section |
| (Render hook in `PartRenderHelper.cs`) | Grid line drawing call per-frame |

### Modified Files

| File | Change |
|------|--------|
| `space-tape.lib/PartEditorUi.cs` | Add camera snap button row to `RenderToolbar()`, add grid config controls |
| `space-tape.lib/SpaceTapeSubmod.cs` | Own `CameraSnapController` instance, pass it to UI and scene update |
| `space-tape.lib/PartRenderHelper.cs` | Call grid drawing in the render patch |
| `space-tape.lib/PartEditorScene.cs` | Expose helper to get target ECL position for camera positioning |

---

## Task 1: CameraSnapController State & Camera Snapping

### File: `space-tape.lib/CameraSnapController.cs`

Create a new class `CameraSnapController` in namespace `MeowSci.SpaceTapeLib`.

#### Snap Mode Enum

```csharp
public enum CameraSnapMode
{
    None,
    Front,   // look along -Z, up = +Y
    Back,    // look along +Z, up = +Y
    Left,    // look along +X, up = +Y
    Right,   // look along -X, up = +Y
    Top,     // look along -Y, up = -Z
    Bottom   // look along +Y, up = +Z
}
```

#### State

```csharp
public CameraSnapMode ActiveMode { get; private set; } = CameraSnapMode.None;
public bool GridVisible { get; set; } = true;      // grid auto-shows when snap is activated
public float GridWidth { get; set; } = 5.0f;        // meters, X extent
public float GridHeight { get; set; } = 5.0f;       // meters, Y extent
public float GridSpacing { get; set; } = 0.25f;     // meters between lines
public float4 GridColor { get; set; } = new float4(0.5f, 0.5f, 0.5f, 0.4f);  // translucent gray
public float4 GridAxisColor { get; set; } = new float4(0.8f, 0.8f, 0.2f, 0.6f); // brighter for axis lines
```

#### Camera Snap Method

```csharp
public void SnapTo(CameraSnapMode mode, PartEditorScene scene)
```

**How camera snapping works — the KSA OrbitController approach:**

The space-tape editor camera uses `CameraMode.Orbit` (set in `PartEditorScene.Enter()` at line 74). The KSA `OrbitController` (file: `decomp/ksa/KSA/OrbitController.cs`) computes camera position each frame from:

1. `Camera.Following` → the `VehicleEditingSpace` (the IFollowable target)
2. `followable.OrbitView` → contains `Azimuth`, `Elevation`, `DistancePower`
3. `GetFrame2Ecl()` → reference frame rotation
4. Spherical coordinate math: rotates `UnitX` by Azimuth around the frame's Z axis, then by Elevation around the resulting right vector

**The critical insight**: When `Program.Editor` is **not** set (which is the case for space-tape since it doesn't use the game's VehicleEditor), the reference frame comes from `orbitView.ReferenceFrame` (line 324 of `decomp/ksa/KSA/OrbitController.cs`).

**Snap implementation strategy — set Azimuth, Elevation, and ReferenceFrame on the OrbitView:**

```csharp
public void SnapTo(CameraSnapMode mode, PartEditorScene scene)
{
    if (!scene.IsActive || scene.EditingSpace == null) return;
    
    ActiveMode = mode;
    if (mode == CameraSnapMode.None)
    {
        GridVisible = false;
        return;
    }
    
    GridVisible = true;
    
    // Get the camera and its following target's OrbitView
    Camera camera = Program.GetCamera();
    IFollowable? following = camera?.Following;
    OrbitView? orbitView = following?.OrbitView;
    if (orbitView == null) return;
    
    // The OrbitController spherical coordinate system (from decomp/ksa/KSA/OrbitController.cs lines 366-373):
    //   Starting direction = UnitX transformed by frame2Ecl
    //   Up axis = UnitZ transformed by frame2Ecl
    //   Azimuth rotates the starting direction around the up axis
    //   Elevation rotates the result around the right vector (cross of rotated direction and up)
    //
    // For the VehicleEditingSpace at identity rotation, frame axes should align with ECL axes.
    // UnitX = starting look-from direction, UnitZ = up axis.
    //
    // To snap to a specific view, we need to find the Azimuth and Elevation that place
    // the camera at the correct position looking at the origin.
    //
    // IMPORTANT: The exact Azimuth/Elevation values depend on the reference frame rotation.
    // Since VehicleEditingSpace uses identity rotation, and Chase frame just uses the
    // following target's body rotation... we need to determine empirically if needed.
    //
    // The approach: compute the desired camera offset direction (from target to camera)
    // in the frame's coordinate system, then derive azimuth/elevation from it.
    
    (double azimuth, double elevation) = GetAzimuthElevation(mode);
    orbitView.Azimuth = azimuth;
    orbitView.Elevation = elevation;
    // Don't change DistancePower — keep user's current zoom level
}
```

**Azimuth/Elevation calculation:**

The camera offset direction (from origin to camera position) for each snap view, and the corresponding spherical coordinates assuming the frame's X axis is the base direction and Z is up:

```csharp
private static (double azimuth, double elevation) GetAzimuthElevation(CameraSnapMode mode)
{
    // These define where the CAMERA is positioned relative to the target.
    // Front view = camera at +Z looking along -Z → offset direction is +Z
    // The OrbitController starts from UnitX and rotates by azimuth around Z, then by elevation.
    //
    // From UnitX: Azimuth rotates around Z (counterclockwise from +X toward +Y).
    //   Azimuth = 0    → camera along +X
    //   Azimuth = π/2  → camera along +Y
    //   Azimuth = π    → camera along -X
    //   Azimuth = -π/2 → camera along -Y
    // Elevation tilts up/down from the XY plane:
    //   Elevation = π/2  → camera above (along +Z)
    //   Elevation = -π/2 → camera below (along -Z)
    //
    // HOWEVER: the exact frame orientation for VehicleEditingSpace may differ.
    // The values below assume a standard right-hand coordinate system where:
    //   Frame X = editor +X (right), Frame Z = editor +Y (up)... 
    //   OR the frame might map differently.
    //
    // PRACTICAL APPROACH: Since the exact frame mapping is uncertain, these initial values 
    // should be tuned at runtime. Use a debug mode that prints the current azimuth/elevation 
    // when the user manually positions the camera, then record the correct values.
    //
    // INITIAL ESTIMATES (will need runtime verification):
    return mode switch
    {
        CameraSnapMode.Front  => (Math.PI / 2, 0),        // +Z offset
        CameraSnapMode.Back   => (-Math.PI / 2, 0),       // -Z offset  
        CameraSnapMode.Left   => (Math.PI, 0),             // -X offset
        CameraSnapMode.Right  => (0, 0),                   // +X offset
        CameraSnapMode.Top    => (0, Math.PI / 2),         // above
        CameraSnapMode.Bottom => (0, -Math.PI / 2),        // below
        _ => (0, 0)
    };
}
```

> **⚠️ CRITICAL NOTE FOR IMPLEMENTERS**: The Azimuth/Elevation values above are *educated guesses*. The VehicleEditingSpace's reference frame rotation determines how these map to actual camera positions. **You MUST add a temporary debug readout** (like `ImGui.Text($"Az: {orbitView.Azimuth:F3} El: {orbitView.Elevation:F3}")` in the toolbar) to verify the mapping. Manually orbit the camera to each canonical view, read the Az/El values, then hardcode the correct ones. Remove the debug readout when done.

#### Deactivation

The snap mode should deactivate when the user interacts with the camera (mouse drag, WASD). This happens automatically because the KSA `OrbitController` updates `orbitView.Azimuth` and `orbitView.Elevation` each frame based on user input. Once the user moves the camera, those values change from the snapped values, and the camera moves freely.

However, `ActiveMode` should track whether the grid should still be visible. The simplest approach:
- Clicking a snap button sets `ActiveMode` and `GridVisible = true`
- Clicking the same button again (or a "Clear" button) sets `ActiveMode = None` and `GridVisible = false`
- The grid stays visible even if the user orbits after snapping (per user request)
- Only explicitly toggling off hides the grid

---

## Task 2: Grid Plane Drawing via GizmosRenderer

### Approach: `Program.GizmosRenderer.DrawLine()`

The KSA `GizmosRenderer` (file: `decomp/ksa/KSA/GizmosRenderer.cs`) provides a public `DrawLine()` method that renders colored lines in 3D space using ego-space coordinates. This is the same system the game uses for debug visualization (used in `Celestial.cs`, `CubeCellGrid.cs`, `BoundingVolumeHierarchy.cs`).

**Key API** (from `decomp/ksa/KSA/GizmosRenderer.cs` lines 183-221):
```csharp
// float3 version
public void DrawLine(float3 startEgo, float3 endEgo, float4 color)

// double3 version 
public void DrawLine(double3 startEgo, double3 endEgo, float4 color)
```

**Access via**: `Program.GizmosRenderer` (public static field, `decomp/ksa/KSA/Program.cs` line 157)

**Coordinate space**: All positions must be in **ego space** (camera-relative). Convert from assembly space using the `matrixAsmb2Ego` from `PartEditorScene.GetMatrixAsmb2Ego(viewport)`.

**Capacity**: 131,072 line vertices per frame (65,536 line segments). A 5×5m grid with 0.25m spacing = ~40 lines per axis × 2 axes = ~80 line segments. Well within limits.

**When to draw**: Each frame during the render patch, *before* the GizmosRenderer.Render() call. The `PartModelRendererPatch.Prefix()` in `PartRenderHelper.cs` is called during `PartModelRenderer.UpdateRenderData`, which runs before the gizmo render pass. So drawing lines here should work. If not, the lines can be drawn in `SpaceTapeSubmod.UpdateScene()` which is already called from the render patch.

### Grid Drawing Method

Add to `CameraSnapController`:

```csharp
/// <summary>
/// Draws the grid plane in 3D using GizmosRenderer.DrawLine().
/// Must be called once per frame from the render patch when the grid is visible.
/// </summary>
public void DrawGrid(Viewport viewport, PartEditorScene scene)
{
    if (!GridVisible || ActiveMode == CameraSnapMode.None || !scene.IsActive) return;
    
    double4x4 matrixAsmb2Ego = scene.GetMatrixAsmb2Ego(viewport);
    
    // Determine which plane to draw based on snap mode
    // Front/Back → XY plane (grid in X and Y, at Z=0)
    // Left/Right → YZ plane (grid in Y and Z, at X=0)
    // Top/Bottom → XZ plane (grid in X and Z, at Y=0)
    
    DrawGridForMode(ActiveMode, matrixAsmb2Ego);
}

private void DrawGridForMode(CameraSnapMode mode, double4x4 matrixAsmb2Ego)
{
    // Determine the two axes of the grid plane and the grid extents
    // axisU and axisV are unit vectors in assembly space defining the grid plane
    double3 axisU, axisV;
    float extentU, extentV;
    
    switch (mode)
    {
        case CameraSnapMode.Front:
        case CameraSnapMode.Back:
            axisU = double3.UnitX;  // horizontal
            axisV = double3.UnitY;  // vertical
            extentU = GridWidth;
            extentV = GridHeight;
            break;
        case CameraSnapMode.Left:
        case CameraSnapMode.Right:
            axisU = double3.UnitZ;  // horizontal (depth becomes horizontal)
            axisV = double3.UnitY;  // vertical
            extentU = GridWidth;
            extentV = GridHeight;
            break;
        case CameraSnapMode.Top:
        case CameraSnapMode.Bottom:
            axisU = double3.UnitX;  // horizontal
            axisV = double3.UnitZ;  // vertical (depth)
            extentU = GridWidth;
            extentV = GridHeight;
            break;
        default:
            return;
    }
    
    float halfU = extentU / 2f;
    float halfV = extentV / 2f;
    float spacing = GridSpacing;
    
    // Draw lines along U axis (varying V)
    int linesV = (int)(extentV / spacing) + 1;
    for (int i = 0; i <= linesV; i++)
    {
        double v = -halfV + i * spacing;
        double3 startAsmb = axisU * (-halfU) + axisV * v;
        double3 endAsmb = axisU * halfU + axisV * v;
        
        double3 startEgo = startAsmb.Transform(matrixAsmb2Ego);
        double3 endEgo = endAsmb.Transform(matrixAsmb2Ego);
        
        // Use brighter color for the axis line (v ≈ 0)
        float4 color = Math.Abs(v) < spacing * 0.5f ? GridAxisColor : GridColor;
        Program.GizmosRenderer.DrawLine(startEgo, endEgo, color);
    }
    
    // Draw lines along V axis (varying U)
    int linesU = (int)(extentU / spacing) + 1;
    for (int i = 0; i <= linesU; i++)
    {
        double u = -halfU + i * spacing;
        double3 startAsmb = axisU * u + axisV * (-halfV);
        double3 endAsmb = axisU * u + axisV * halfV;
        
        double3 startEgo = startAsmb.Transform(matrixAsmb2Ego);
        double3 endEgo = endAsmb.Transform(matrixAsmb2Ego);
        
        float4 color = Math.Abs(u) < spacing * 0.5f ? GridAxisColor : GridColor;
        Program.GizmosRenderer.DrawLine(startEgo, endEgo, color);
    }
}
```

### Transform Math Reference

The `double3.Transform(double4x4)` extension method applies the 4×4 matrix to a point (with implicit w=1). This converts assembly-space coordinates to ego-space (camera-relative) coordinates, which is what `GizmosRenderer.DrawLine()` expects.

The matrix `matrixAsmb2Ego` is already computed by `PartEditorScene.GetMatrixAsmb2Ego(viewport)` and used extensively in `PartEditorGizmos.cs` and `PartEditorInteraction.cs` for the same purpose. See:
- `PartEditorScene.cs` line 216: `_editingSpace?.GetMatrixAsmb2Ego(viewport.GetCamera())`
- `PartEditorGizmos.cs` line 111: `selectedPart.PositionEgo(in matrixAsmb2Ego)`
- `ConnectorGizmo.cs` line 47: `c.Position.Transform(matrixAsmb2Ego)`

---

## Task 3: ImGui UI — Snap Buttons and Grid Controls

### Location: `space-tape.lib/PartEditorUi.cs`, inside `RenderToolbar()`

Add a new row to the existing toolbar table (the 3-column table with checkbox | label | widget layout). Insert **after** the Rotation Snap row (line ~214) and **before** `ImGui.EndTable()`.

### UI Layout

**Row 6: Camera Snap**

A row of 6 small buttons for snap views + a clear button:

```
[✓] Camera Snap   [ F ] [ Bk ] [ L ] [ R ] [ T ] [ Bt ] [ ✕ ]
```

- Checkbox enables/disables the camera snap feature
- When disabled, all buttons are grayed out and grid is hidden
- Clicking a button snaps the camera and shows the grid
- Clicking the active button again (or ✕) clears the snap and hides the grid
- Active button should be visually highlighted (different color)

**Row 7: Grid Size** (only visible when grid is active)

```
[✓] Grid Size      [  5.00 ] × [  5.00 ]
```

Two DragFloat inputs for width and height.

**Row 8: Grid Spacing** (only visible when grid is active)

```
[✓] Grid Spacing    [  0.250 ]
```

Single DragFloat for line spacing.

### Implementation Pattern

Follow the existing toolbar table pattern from `PartEditorUi.cs` lines 132-218. Key conventions:
- `ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f))` is already pushed
- Column setup: `##cb` (checkbox width), `##lbl` (110px), `##widget` (stretch)
- Use `ImGui.AlignTextToFramePadding()` before label text
- Use `ImGui.BeginDisabled()`/`EndDisabled()` for grayed-out controls
- Button highlighting: `ImGui.PushStyleColor(ImGuiCol.Button, highlightColor)` around the active button

### Button Highlight Pattern

```csharp
void SnapButton(string label, CameraSnapMode mode, CameraSnapController snap, PartEditorScene scene)
{
    bool isActive = snap.ActiveMode == mode;
    if (isActive)
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32((float4)KSAColor.Xkcd.BrightLightBlue));
    
    if (ImGui.Button(label))
    {
        if (isActive)
            snap.SnapTo(CameraSnapMode.None, scene);  // toggle off
        else
            snap.SnapTo(mode, scene);
    }
    
    if (isActive)
        ImGui.PopStyleColor();
    
    ImGui.SameLine(0, 4);
}
```

### Method Signature Changes

`RenderToolbar` needs access to the `CameraSnapController`. Update the call chain:

1. `PartEditorUi.RenderEditorWindow()` — add `CameraSnapController` parameter
2. `RenderToolbar()` — add `CameraSnapController` parameter
3. `SpaceTapeSubmod.RenderFloatingWindows()` — pass the snap controller to the UI

---

## Task 4: Integration into SpaceTapeSubmod

### File: `space-tape.lib/SpaceTapeSubmod.cs`

**Add field:**
```csharp
private readonly CameraSnapController _cameraSnap = new CameraSnapController();
```

**Modify `UpdateScene()`** to draw the grid:
```csharp
public void UpdateScene(Viewport viewport)
{
    _scene.UpdateGizmo(viewport, _controller.CurrentPart);
    if (_scene.IsActive)
    {
        // ... existing gizmo/interaction code ...
        
        // Draw grid overlay
        _cameraSnap.DrawGrid(viewport, _scene);
    }
}
```

**Modify `RenderFloatingWindows()`** to pass snap controller:
```csharp
public void RenderFloatingWindows()
{
    _ui.RenderEditorWindow(_controller, _scene, _gizmos, _catalog, _writer, _cameraSnap);
}
```

**Modify `Dispose()`** — `CameraSnapController` has no disposable resources, but if ActiveMode is set, clear it:
```csharp
public void Dispose()
{
    _cameraSnap.SnapTo(CameraSnapMode.None, _scene);
    // ... existing dispose code ...
}
```

---

## Task 5: Render Patch Integration

### File: `space-tape.lib/PartRenderHelper.cs`

The grid lines must be drawn during the render patch so they appear in 3D. The `PartModelRendererPatch.Prefix()` method already calls `SpaceTapeSubmod.Current?.UpdateScene(viewport)` which is where the grid drawing will happen (via the `_cameraSnap.DrawGrid()` call added in Task 4).

**No changes needed to PartRenderHelper.cs** — the grid drawing is triggered through the existing `UpdateScene()` call chain.

However, there is a **timing concern**: `GizmosRenderer.ResetInstances()` is called at the start of each frame (`Program.cs` line 1817), and `GizmosRenderer.Render()` is called later (`Program.cs` line 3665). The `PartModelRendererPatch.Prefix` runs during `PartModelRenderer.UpdateRenderData`, which should be between these two calls. **Verify this timing by checking that grid lines actually appear.** If they don't, the DrawLine calls may need to move to a different hook point.

**Fallback if timing is wrong**: Use a Harmony postfix on `GizmosRenderer.ResetInstances()` or a prefix on `GizmosRenderer.Render()` to inject the line drawing at the right point in the frame.

---

## Task 6: Verify and Tune Azimuth/Elevation Values

### Runtime Calibration Procedure

After initial implementation, the snap views may point in wrong directions because the Azimuth/Elevation → camera position mapping depends on the VehicleEditingSpace's reference frame orientation.

**Steps:**
1. Add a temporary debug line to the toolbar: `ImGui.Text($"Az: {orbitView?.Azimuth:F3} El: {orbitView?.Elevation:F3} Ref: {orbitView?.ReferenceFrame}")`
2. Open the space-tape editor, enter the editing scene
3. Manually orbit the camera to face the front of a part (looking along −Z, blue axis pointing at you)
4. Record the Azimuth and Elevation values
5. Repeat for all 6 views
6. Update the `GetAzimuthElevation()` switch with the correct values
7. Remove the debug line

**Alternative approach if OrbitView manipulation doesn't work:**

If the VehicleEditingSpace's OrbitView is null or the reference frame doesn't behave as expected, use a **Harmony prefix on `KSA.OrbitController.OnFrame`** to directly set the camera transform for one frame:

```csharp
[HarmonyPatch(typeof(KSA.OrbitController), "OnFrame")]
[HarmonyPrefix]
static bool OrbitController_OnFrame_Prefix(KSA.OrbitController __instance, Viewport inViewport, double inDeltaTime)
{
    var snap = CameraSnapController.Current;
    if (snap == null || snap.ActiveMode == CameraSnapMode.None) return true; // pass through
    if (!snap.ShouldApplySnap) return true; // already snapped, let user orbit freely
    
    // Get the camera following target position
    Camera camera = __instance.Camera;
    IFollowable? target = camera?.Following;
    if (target == null) return true;
    
    double3 targetPosEcl = target.GetPositionEcl();
    (double3 lookDir, double3 upDir) = snap.GetSnapVectors();
    
    // Position camera at current distance from target, along the offset direction
    // The "offset direction" is opposite to the look direction
    double distance = (camera.PositionEcl - targetPosEcl).Length();
    if (distance < 0.1) distance = 10.0; // safety minimum
    
    double3 offsetDir = -lookDir;
    camera.PositionEcl = targetPosEcl + offsetDir * distance;
    camera.LocalRotation = Camera.LookAtRotation(lookDir, upDir);
    
    snap.ShouldApplySnap = false; // only apply once, then let orbit controller resume
    return false; // skip original OnFrame this one time
}
```

This prefix approach:
1. Fires once when a snap button is clicked (`ShouldApplySnap = true`)
2. Positions the camera at the correct orientation
3. Sets `ShouldApplySnap = false` so subsequent frames pass through to normal orbit control
4. The orbit controller picks up from the new position/rotation seamlessly

**Choose** between the OrbitView approach and the Harmony prefix approach based on which one produces correct results during testing.

If using the Harmony prefix approach, the patch must be registered in `PartRenderHelper.Patch()` alongside the existing render patch, or in a new `CameraSnapPatches` class registered the same way.

### Snap Vector Definitions (for the Harmony approach)

```csharp
public static (double3 lookDir, double3 upDir) GetSnapVectors(CameraSnapMode mode)
{
    return mode switch
    {
        CameraSnapMode.Front  => (new double3(0, 0, -1), new double3(0, 1, 0)),   // look along -Z, up = +Y
        CameraSnapMode.Back   => (new double3(0, 0, 1),  new double3(0, 1, 0)),   // look along +Z, up = +Y
        CameraSnapMode.Left   => (new double3(1, 0, 0),  new double3(0, 1, 0)),   // look along +X, up = +Y
        CameraSnapMode.Right  => (new double3(-1, 0, 0), new double3(0, 1, 0)),   // look along -X, up = +Y
        CameraSnapMode.Top    => (new double3(0, -1, 0), new double3(0, 0, -1)),  // look along -Y, up = -Z
        CameraSnapMode.Bottom => (new double3(0, 1, 0),  new double3(0, 0, 1)),   // look along +Y, up = +Z
        _ => (new double3(0, 0, -1), new double3(0, 1, 0))
    };
}
```

**IMPORTANT**: These directions are in the editor's **assembly space**, but `Camera.LookAtRotation` expects **ECL (ecliptic) space** vectors. The VehicleEditingSpace at identity rotation means assembly and ECL axes align, BUT the `VehicleEditingSpace.Asmb2Ecl` quaternion might introduce a rotation. If the snap views are rotated, multiply the snap vectors by the editing space's `Asmb2Ecl` quaternion:

```csharp
doubleQuat asmb2Ecl = scene.EditingSpace?.Asmb2Ecl ?? doubleQuat.Identity;
double3 lookDirEcl = lookDirAsmb.Transform(asmb2Ecl);
double3 upDirEcl = upDirAsmb.Transform(asmb2Ecl);
```

The `Asmb2Ecl` property is used in `SpaceTapeSubmod.UpdateScene()` (line 48) and `PartEditorGizmos.cs` — follow the same pattern.

---

## Key References for Implementers

### Decompiled Source Files

| File | What to reference |
|------|-------------------|
| `decomp/ksa/KSA/OrbitController.cs` | Lines 294-400: `OnFrame()` — how azimuth/elevation/distance are used to compute camera position. Line 324: reference frame selection for editor vs non-editor |
| `decomp/ksa/KSA/Camera.cs` | Lines 136-142: `LookAtRotation()` — creates rotation quaternion from forward and up vectors. Lines 144-153: `LookAt()` overloads |
| `decomp/ksa/KSA/GizmosRenderer.cs` | Lines 183-221: `DrawLine()` overloads — draw colored lines in ego space. Line 157 of `Program.cs`: `public static GizmosRenderer GizmosRenderer` |
| `decomp/ksa/KSA/Program.cs` | Line 157: `GizmosRenderer` static field. Line 443: `GetCamera()`. Line 1817: `GizmosRenderer.ResetInstances()`. Line 3665: `GizmosRenderer.Render()` |
| `decomp/ksa/KSA/OrbitView.cs` | `Azimuth`, `Elevation`, `DistancePower`, `ReferenceFrame` properties |
| `decomp/ksa/KSA/Transform3D.cs` | `LocalPosition`, `LocalRotation`, `PositionEcl` |

### Existing Mod Code Patterns

| File | What to reference |
|------|-------------------|
| `space-tape.lib/PartEditorScene.cs` | Lines 62-77: How editing space is created and camera is configured. Line 216: `GetMatrixAsmb2Ego()` helper |
| `space-tape.lib/PartEditorGizmos.cs` | Lines 108-135: How gizmo positions are computed in ego space using `matrixAsmb2Ego` |
| `space-tape.lib/ConnectorGizmo.cs` | Line 47: `c.Position.Transform(matrixAsmb2Ego)` — assembly-to-ego transform pattern |
| `space-tape.lib/PartEditorUi.cs` | Lines 132-218: Toolbar table layout pattern with 3 columns |
| `space-tape.lib/SpaceTapeSubmod.cs` | Lines 41-55: `UpdateScene()` — per-frame update with viewport |
| `space-tape.lib/PartRenderHelper.cs` | Lines 8-39: Render patch that drives the scene update |
| `camera-controller-override.lib/CameraControllerOverridePatches.cs` | Harmony prefix pattern on `OrbitController.OnFrame` and `FlyController.OnFrame` |

### ImGui Patterns

| Pattern | Reference |
|---------|-----------|
| Toolbar table layout | `PartEditorUi.cs` lines 132-218 |
| Button highlighting | Use `ImGui.PushStyleColor(ImGuiCol.Button, ...)` / `PopStyleColor()` |
| Disabled controls | `ImGui.BeginDisabled()` / `EndDisabled()` |
| DragFloat | `ImGui.DragFloat("##id", ref value, speed, min, max, "%.3f")` |
| SameLine spacing | `ImGui.SameLine(0, 4)` for tight button spacing |
| KSA colors | `(float4)KSAColor.Xkcd.BrightLightBlue` for highlights |
| Full-width widget | `ImGui.SetNextItemWidth(-1)` before the widget |

---

## Implementation Order

1. **Task 1**: Create `CameraSnapController.cs` with enum, state, and snap logic
2. **Task 3**: Add UI buttons to `PartEditorUi.cs` toolbar
3. **Task 4**: Wire into `SpaceTapeSubmod.cs`
4. **Task 2**: Add grid drawing to `CameraSnapController.DrawGrid()`
5. **Task 5**: Verify render timing (grid lines appear correctly)
6. **Task 6**: Runtime calibration of azimuth/elevation values
7. **Final**: Build verification (`dotnet build space-tape/space-tape.csproj`), remove debug lines, update README

---

## Edge Cases and Considerations

- **OrbitView is null**: The VehicleEditingSpace might not provide an OrbitView. In this case, fall back to the Harmony prefix approach (Task 6 alternative).
- **Grid line count explosion**: Clamp maximum lines to ~200 per axis to prevent exceeding GizmosRenderer's 65,536 line segment capacity. `int maxLines = Math.Min((int)(extent / spacing) + 1, 200);`
- **Camera distance**: When snapping, preserve the user's current zoom (DistancePower). Don't reset it.
- **Editor not active**: All snap/grid functionality should no-op gracefully when `PartEditorScene.IsActive` is false.
- **Performance**: Grid drawing runs every frame. With 0.25m spacing on a 5×5m grid, that's ~40 lines × 2 = ~80 lines total. Negligible performance impact.
- **Multiple viewports**: The game supports multiple viewports. The grid should draw in whichever viewport the scene is rendering in (the `viewport` parameter handles this automatically).

---

## README Updates

After implementation, update `space-tape/README.md` to document:
- Camera snap feature in the Features list
- Grid plane overlay in the Features list
- Keyboard shortcuts (if any are added)
- Updated architecture diagram showing CameraSnapController
