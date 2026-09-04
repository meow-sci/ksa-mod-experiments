# free-fallin.lib

Reusable core for [`../free-fallin`](../free-fallin) and the unscience umbrella mod.

| File | Responsibility |
|---|---|
| `FreeFallinSubmod.cs` | `ISubmod` lifecycle and ImGui appearance/PBR editor |
| `FreeFallinPatches.cs` | Prefixes `ChuteRenderable.Draw`, substitutes material handle 0, and restores observed canopies |
| `CanopyMaterialController.cs` | Builds GPU albedo/PBR textures and `MaterialData` objects from stock assets and user settings |
| `CanopyMaterialSettings.cs` / `CanopyTextureMode.cs` | Public settings model and Stock/Replace/CenterDecal modes |
| `ParachuteTextureLibrary.cs` | Persistent imported-PNG library under `.unscience/parachutes` |
| `PngFileBrowser.cs` | ImGui filesystem picker modeled after Graffiti's importer |

The lib deliberately retains generated KSA texture/material asset registrations until renderer
shutdown. This avoids invalidating handles referenced by frames in flight; one pair is allocated per
press of Apply, not per UI change or frame.
