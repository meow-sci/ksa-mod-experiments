# Space Tape Grid Line Improvements Plan

## Goal

Make Space Tape's grid color and opacity controls behave predictably using code-only mod changes. The UI already exposes RGBA values through `ColorEdit4`; the problem is the rendering path that consumes those values.

## Current Findings

The original Space Tape grid used `Program.GizmosRenderer.DrawLine(startEgo, endEgo, color)` from `CameraSnapController.DrawGridForMode()`.

That path preserves RGBA in C# and in `Content/Core/Shaders/Gizmos/LineGizmo.vert`, but the shared `DebugGizmoFrag` shader then applies sphere-oriented Fresnel RGB changes and writes alpha as `1`. A first implementation attempted to patch only the line shader stage at runtime, but in-game testing showed the alpha slider still had no useful effect.

The second-pass investigation found a better code-only path already available in KSA: `OrbitLinePass`.

- `OrbitLinePass` uses the existing `LineVert` and `LineFrag` shaders.
- `LineFrag` outputs `outColor = vec4(inColor)`, including alpha.
- `OrbitLinePass` renders directly to the offscreen target with `BlendColorAlpha`.
- This avoids modifying core game shader files and avoids runtime shader-module swapping.

## Chosen Implementation

Use `OrbitLinePass` for Space Tape grid lines instead of `GizmosRenderer.DrawLine()`.

Implementation details:

- Convert Space Tape's `float4` RGBA grid colors to normalized `byte4` values with `byte4.Pack(..., Pack.Float.Normalize)`.
- For each grid segment, call:
  - `OrbitLinePass.AddLineVertex(viewport, start, color)`
  - `OrbitLinePass.AddLineVertex(viewport, end, color)`
  - `OrbitLinePass.AddLineEnd(viewport)`
- Keep existing grid plane math and snap-mode behavior unchanged.
- Remove the failed runtime shader override and its `Patcher.cs` wiring.

## Files Changed

- `space-tape.lib/CameraSnapController.cs`
  - Grid lines now submit through `OrbitLinePass`.
  - Added color clamping/packing helper for `float4` to `byte4`.

- `space-tape/Patcher.cs`
  - Removed `GridLineShaderOverride` patch/unpatch calls.

- `space-tape.lib/GridLineShaderOverride.cs`
  - Removed because Space Tape no longer uses runtime shader replacement for the grid.

- `space-tape/README.md`
  - Updated grid rendering notes and project structure.

- `REPOSITORY_INDEX.md`
  - Updated Space Tape feature and component descriptions.

## Tradeoffs

- This keeps rendering in KSA's 3D pipeline with depth testing and alpha blending.
- It relies on KSA's existing orbit line renderer, so line thickness remains the game's default line width.
- KSA's `Line.vert` also darkens line color based on alpha, so very low opacity may look dimmer as well as more transparent. This is still a better approximation than the gizmo path, where alpha was forced opaque.

## Fallback If Needed

If `OrbitLinePass` does not render in the Space Tape editor mode on a target build, the next code-only fallback is an ImGui overlay grid:

- Project grid endpoints with `viewport.GetCamera().EgoToScreen(...)`.
- Draw with `ImGui.GetForegroundDrawList().AddLine(...)` using `ImGui.GetColorU32(float4)`.
- This would provide exact RGBA behavior but would be a screen-space overlay without true 3D depth testing.

## Verification Plan

1. Run `dotnet build` from the repository root.
2. Open Space Tape's part editor and enable the grid.
3. Set grid alpha to `1.0`; expected result is fully visible grid lines.
4. Set grid alpha to `0.2`; expected result is visibly translucent/dim grid lines.
5. Set grid alpha to `0.0`; expected result is no visible grid lines.
6. Check that camera snap modes still place the grid on the expected plane.
