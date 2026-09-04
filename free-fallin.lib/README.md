# free-fallin.lib

Reusable core for [`../free-fallin`](../free-fallin) and the unscience umbrella mod.

| File | Responsibility |
|---|---|
| `FreeFallinSubmod.cs` | `ISubmod` lifecycle and ImGui appearance/PBR editor |
| `FreeFallinPatches.cs` | Prefixes `ChuteRenderable.Draw`, substitutes material handle 0, and restores observed canopies |
| `CanopyProjectionShaders.cs` | Injects the material-gated Full Canopy varying/albedo projection into KSA's model PBR shaders in memory |
| `CanopyMaterialController.cs` | Transcodes the stock BC7 KTX2 to RGBA8 when compositing a decal, then builds GPU albedo/PBR textures and `MaterialData` objects |
| `CanopyMaterialSettings.cs` / `CanopyTextureMode.cs` | Public settings model and Stock/Replace/FullCanopy/CenterDecal modes |
| `ParachuteTextureLibrary.cs` | Persistent imported-PNG library under `.unscience/parachutes` |
| `PngFileBrowser.cs` | ImGui filesystem picker modeled after Graffiti's importer |

The lib deliberately retains generated KSA texture/material asset registrations until renderer
shutdown. This avoids invalidating handles referenced by frames in flight; one pair is allocated per
press of Apply, not per UI change or frame.

Full Canopy stores projection scale, rotation, and a mode marker in `MaterialData.ExtraData`. The
patched skinned vertex shader derives a second UV from the canopy's bind-pose X/Z coordinates. The
patched PBR fragment shader uses that UV only for marked materials' albedo; the authored UV remains
in use for the stock normal and AO/roughness/metallic maps. Shader files on disk are never modified.
