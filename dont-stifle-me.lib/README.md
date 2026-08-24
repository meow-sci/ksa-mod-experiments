# dont-stifle-me.lib

Core of the [dont-stifle-me](../dont-stifle-me/README.md) mod — three Harmony patches on
`KSA.VehicleEditor` gated by runtime toggles. Consumed by the standalone mod and by the unscience
supermod.

## Files

| File | Purpose |
|---|---|
| `EditorScaleSettings.cs` | Static toggles: `Enabled` (master), `RemoveClamp`, `PerAxisScaling`. Read every frame; no re-patching on change. |
| `EditorScalePatches.cs` | `Apply(Harmony)` / `Remove(Harmony)`. Resolves the private targets by name and installs the three patches below. |
| `PerAxisScaleDrag.cs` | The per-axis replacement for the stock drag routine; owns the raw (un-snapped) accumulator for the current drag session. |
| `DontStifleMeSubmod.cs` | `ISubmod` ImGui surface (checkboxes + patch-status warning). |

## Patches

| Game target (`KSA.VehicleEditor`) | Patch | What it does |
|---|---|---|
| `private static (double Min, double Max) ScaleBoundsFor(Part)` | postfix | When clamp removal is active, returns `(1e-6, +inf)` instead of `(0.5, 2.0)`. Both the drag accumulator and `QuantizeScale` read this, so one patch lifts the clamp everywhere. |
| `private void UpdateSelectedScale(ref readonly double4x4, Viewport)` | prefix | When per-axis scaling is active, runs `PerAxisScaleDrag.Step` and skips the original (which writes `new double3(s, s, s)`). Falls through to stock on any exception. |
| `public void UpdateScaleGizmo(ref readonly double4x4, doubleQuat, Viewport, double)` | postfix | Per-frame hook: when `GizmoGrabbed` is false, ends the drag session so the next grab re-seeds from the part's actual scale. Avoids a `Brutal.Glfw` dependency on `OnMouseButton`. |

`PerAxisScaleDrag.Step` mirrors the stock math (near-plane cursor delta → projected on the gizmo
axis → scaled by part depth), then calls the game's own private `QuantizeScale` and
`ForEachPartWithSymmetry` through `AccessTools.MethodDelegate` so snapping and symmetry behave
exactly as stock, and finishes with `Part.RefreshScaleAndReposition()` + `PartTree.RefreshStaticMass()`.

Gizmo segment index → axis: `0 = X`, `1 = Y`, `2 = Z`.

## Game-update watchlist

All five member names are resolved as **strings** (`AccessTools.Method`) — a rename fails at
`Apply()` (logged, patches skipped, UI shows a red notice) rather than at compile time. See
`scope/part-editor-and-robotics.md` → dont-stifle-me.
