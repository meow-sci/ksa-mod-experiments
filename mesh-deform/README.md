# Mesh Deform

Per-part GPU vertex-shader deformation for KSA vehicles. Applies radial dents/bulges
by displacing vertices at render time using data injected into `PerInstanceData` padding.

## How it works

1. A Harmony prefix on `PartModelModule.UpdateRenderData` captures the current `Part`.
2. A Harmony prefix on `PartModel.AddInstance` reinterprets `PerInstanceData` padding
   as `float DeformMagnitude` + `float DeformRadius` and writes the active deformation.
3. At mod load the vertex shader (`MeshIndirectVert`) is recompiled at runtime with
   extra fields in the `InstanceData` struct and displacement logic in `main()`.
4. Pipelines are rebuilt via `PartModelRenderer.ColorData.Rebuild()`.

## Controls

- Press **F11** to open the Mesh Deform window.
- Select a vehicle from the dropdown.
- Set **Magnitude** (negative = dent, positive = bulge) and **Radius**.
- Click **Scan Parts** to list parts.
- Check individual parts and click **Apply**, or enable **Apply to all parts**.
- **Clear All** removes every deformation in the session.

## Architecture

| Layer | Responsibility |
|-------|-------------|
| `MeshDeformManager` | CPU-side dictionary keyed by `Part`; holds `Magnitude` + `Radius` |
| `MeshDeformPatches` | Harmony injection into `PartModel.AddInstance` via `ThreadLocal<Part>` capture |
| `MeshDeformShaders` | Runtime GLSL compile, `VkShaderModule` swap, pipeline rebuild |
| `MeshDeformSubmod` | ImGui UI — vehicle/part selectors, sliders, apply/clear |

## Session-only

Deformations are stored in an in-memory dictionary. They disappear when the mod is
unloaded or the game exits. No save/load persistence is implemented.

## Limitations

- Only **static parts** (`PartModel` / `MeshIndirectVert`) are deformed.
  Dynamic animated parts (`PartModelDynamic`) use a separate shader path and are
  not yet patched.
- Raycasting (`Part.RayCastEgoSubPart`) uses the **original undeformed** CPU mesh
  (`MeshReference.PositionCompare`). Mouse picking may be slightly misaligned with
  the visually deformed surface.
- Deformation is **visual-only** — mass, aerodynamics, and physics bounds are unchanged.
- The 8 bytes of padding limit us to radial deformation from the part's local origin.
  Arbitrary dent centers/directions would require more bytes or a different encoding.

## Build

```bash
dotnet build mesh-deform/mesh-deform.csproj
dotnet build mesh-deform.lib/mesh-deform.lib.csproj
```

Output is copied to `$(SelectedDistModDir)mesh-deform\` (by default the KSA user mods
folder under `Documents/My Games/Kitten Space Agency/mods/`).

## Files

```
mesh-deform/
  mod.toml
  Mod.cs
  Patcher.cs
  mesh-deform.csproj

mesh-deform.lib/
  MeshDeformLib.cs
  MeshDeformManager.cs
  MeshDeformPatches.cs
  MeshDeformShaders.cs
  MeshDeformSubmod.cs
  mesh-deform.lib.csproj
```

## Dependencies

- `ksa-abstractions.lib` — `VehicleProvider`, `ISubmod`, `SubmodUI`, `HotkeyGuard`
- `StarMap.API` — mod lifecycle attributes
- `Lib.Harmony` — runtime patching
- KSA game assemblies (referenced but not copied)
