# Glass Mod — Implementation Plan

Camera FOV override mod that simulates looking through different camera lenses (telephoto, standard, wide-angle, etc.) by changing the game camera's field of view at runtime.

## Architecture Overview

The mod intercepts FOV at two points using Harmony patches:
1. **`Camera.ChangeFieldOfView`** — blocks the game's built-in Page Up/Page Down FOV input so it doesn't fight us
2. **`Camera.UpdateProjection`** — postfix that re-applies our desired FOV and rebuilds the projection matrix when the game tries to reset it

The mod UI presents lens presets (named after real camera focal lengths) and a manual slider, all mapped to FOV degrees. A hotkey toggles the mod window. When the mod is active (not in "Game Default" mode), it continuously enforces the chosen FOV.

---

## Key Game API Details

| Item | Detail |
|------|--------|
| Camera class | `KSA.Camera` (extends `Transform3D`) |
| FOV field | `private float _fovRadians` — default `0.87266463f` (~50°) |
| Set FOV | `Camera.SetFieldOfView(float fovDegrees)` — converts to radians, calls `UpdateProjection()` |
| Get FOV | `Camera.GetFieldOfView()` — returns **radians** (not degrees) |
| Change FOV | `Camera.ChangeFieldOfView(float change)` — adds degrees, clamps 15–120, calls `UpdateProjection()` |
| Update projection | `Camera.UpdateProjection()` — builds reverse-Z perspective matrix from `_fovRadians`, `AspectRatio`, near/far planes |
| FOV range | Constants: `MINIMUM_FOV = 15`, `MAXIMUM_FOV = 120` (degrees) |
| Camera access | `Program.GetCamera()` returns the active `Camera` instance |
| Settings FOV | `GameSettings.FieldOfView` (int, default 50°), applied via `GameSettings.ApplyTo(Camera?)` |

---

## Lens Presets

Map real photography focal lengths to game FOV values. The FOV range is 15°–120°.

| Preset Name | Approx Focal Length | FOV (degrees) | Purpose |
|-------------|-------------------|---------------|---------|
| Super Telephoto | 200mm+ | 15 | Maximum zoom, tight framing |
| Telephoto | 135mm | 20 | Strong zoom |
| Portrait | 85mm | 30 | Moderate zoom, good for focused views |
| Standard | 50mm | 50 | Game default, human-eye equivalent |
| Wide Angle | 28mm | 75 | Wide view |
| Ultra Wide | 16mm | 100 | Very wide, mild distortion |
| Fisheye | 10mm | 120 | Maximum FOV, extreme distortion |

---

## Implementation Tasks

### Task 1: Create project scaffolding

Create the `glass/` directory with these files:

**`glass/glass.csproj`**
- `OutputType`: Library
- `AssemblyName`: `MeowSci.Glass`
- `RootNamespace`: `MeowSci.Glass`
- `DistDir`: `$(SelectedDistModDir)glass\`
- No lib project references needed (self-contained mod)
- Package references: `StarMap.API` 0.3.6, `Lib.Harmony` 2.4.2 (both `PrivateAssets="all"`)
- DLL references: `Brutal.Core.Common`, `Brutal.Core.Numerics`, `Brutal.ImGui`, `Brutal.ImGui.Abstractions`, `Brutal.Core.Strings`, `KSA` (all from `$(KSAFolder)`, `Private=false`)
- Copy mod.toml + assembly to `$(DistDir)` in `CopyCustomContent` target
- Follow same patterns as existing csproj files (e.g., geeforce.csproj)

**`glass/mod.toml`**
```toml
name = "glass"
description = "Camera lens/FOV control"
version = "0.1.0"
author = "meow sci"

[StarMap]
EntryAssembly = "MeowSci.Glass"
```

Add the project to the solution file (`ksa-mod-experiments.slnx`).

---

### Task 2: Implement `Patcher.cs` — Harmony patches

File: `glass/Patcher.cs`

Namespace: `MeowSci.Glass`

**Static state:**
- `private static Harmony? _harmony = new Harmony("glass");`
- `internal static bool IsOverrideActive = false;` — true when mod is controlling FOV (not "Game Default")
- `internal static float OverrideFovDegrees = 50f;` — the desired FOV in degrees

**Methods:**
- `Patch()` — call `_harmony?.PatchAll(typeof(Patcher).Assembly);`
- `Unload()` — call `_harmony?.UnpatchAll("glass"); _harmony = null;`

**Harmony Patch 1: Block built-in FOV changes when override is active**

```csharp
[HarmonyPatch(typeof(Camera), "ChangeFieldOfView")]
[HarmonyPrefix]
private static bool ChangeFieldOfView_Prefix(Camera __instance)
{
    if (!IsOverrideActive) return true; // let game handle it
    // Block game's FOV input — we control FOV
    return false;
}
```

This prevents Page Up/Page Down from changing FOV while our override is active.

**Harmony Patch 2: Enforce our FOV after projection updates**

```csharp
[HarmonyPatch(typeof(Camera), "UpdateProjection")]
[HarmonyPrefix]
private static void UpdateProjection_Prefix(Camera __instance)
{
    if (!IsOverrideActive) return;
    // Overwrite _fovRadians before the projection matrix is built
    // Use Harmony's AccessTools to set private field
    var field = AccessTools.Field(typeof(Camera), "_fovRadians");
    float targetRadians = (float)(OverrideFovDegrees * (Math.PI / 180.0));
    field.SetValue(__instance, targetRadians);
}
```

This prefix runs before `UpdateProjection` builds the projection matrix, ensuring our desired FOV is baked into the matrix. Using a prefix on `UpdateProjection` rather than patching `SetFieldOfView` means we catch all paths that rebuild the projection (settings changes, window resizes, etc.).

**Alternative approach (cache FieldInfo for performance):**
Cache `AccessTools.Field(typeof(Camera), "_fovRadians")` in a static `FieldInfo` variable initialized in `Patch()` to avoid repeated reflection per frame.

---

### Task 3: Implement `Mod.cs` — Lifecycle and ImGui UI

File: `glass/Mod.cs`

Namespace: `MeowSci.Glass`

```csharp
using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
```

**Class structure:**

```csharp
[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;
    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    // Lens preset index (0 = Game Default, then each preset)
    private int _selectedPreset = 0;
    // Manual FOV slider value
    private float _manualFov = 50f;
    // Whether manual mode is active (vs preset)
    private bool _manualMode = false;
}
```

**Preset data — define as a static array:**

```csharp
private static readonly (string Name, float Fov)[] Presets = new[]
{
    ("Game Default", 0f),        // 0f = sentinel, means use game's own FOV
    ("Super Telephoto (200mm)", 15f),
    ("Telephoto (135mm)", 20f),
    ("Portrait (85mm)", 30f),
    ("Standard (50mm)", 50f),
    ("Wide Angle (28mm)", 75f),
    ("Ultra Wide (16mm)", 100f),
    ("Fisheye (10mm)", 120f),
};
```

**Lifecycle methods:**

- `OnImmediateLoad()` — empty
- `OnFullyLoaded()` — call `Patcher.Patch()`, set `_isInitialized = true`, wrap in try/catch
- `OnBeforeUi(double dt)` — empty
- `OnAfterUi(double dt)`:
  - Guard: if not initialized or disposed, return
  - Check `ImGui.IsKeyPressed(ImGuiKey.F9)` → toggle `_windowVisible`
  - If `_windowVisible`, call `RenderWindow()`
  - Apply FOV override every frame: if override is active, call `Program.GetCamera().SetFieldOfView(Patcher.OverrideFovDegrees)` to enforce it continuously
- `Unload()` — call `Patcher.Unload()`, set `_isDisposed = true`, wrap in try/catch

**RenderWindow() method — ImGui UI:**

```
Window title: "Glass — Camera Lens"
Initial size: 350 x 400
```

UI layout:
1. Header: colored text "Glass" in cyan
2. Separator
3. Current FOV display: read `Program.GetCamera().GetFieldOfView()`, convert radians to degrees, show as text
4. Separator
5. Section: "Lens Presets"
   - Radio buttons for each preset in `Presets` array
   - When a preset is selected (not "Game Default"):
     - Set `_manualMode = false`
     - Set `Patcher.OverrideFovDegrees = preset.Fov`
     - Set `Patcher.IsOverrideActive = true`
   - When "Game Default" selected:
     - Set `Patcher.IsOverrideActive = false`
     - (Game resumes its own FOV management)
6. Separator
7. Section: "Manual FOV"
   - Checkbox to enable manual mode (unchecks preset selection)
   - `ImGui.SliderFloat("FOV°", ref _manualFov, 15f, 120f)`
   - When manual mode is active:
     - Set `Patcher.OverrideFovDegrees = _manualFov`
     - Set `Patcher.IsOverrideActive = true`
8. Separator
9. "Reset" button — resets to Game Default (set `_selectedPreset = 0`, `_manualMode = false`, `Patcher.IsOverrideActive = false`)

**FOV enforcement approach in `OnAfterUi`:**

Each frame when override is active, call:
```csharp
try
{
    if (Patcher.IsOverrideActive)
    {
        var camera = Program.GetCamera();
        camera.SetFieldOfView(Patcher.OverrideFovDegrees);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"glass: Error applying FOV: {ex.Message}");
}
```

This is the primary enforcement mechanism. The Harmony `UpdateProjection` prefix is a secondary safety net for when other code paths trigger projection rebuilds between our frames.

---

### Task 4: Add project to solution

Add the new `glass/glass.csproj` to `ksa-mod-experiments.slnx`. Look at the existing slnx format and add a matching entry.

---

### Task 5: Build and verify compilation

Run `dotnet build` on the solution (or at minimum on the `glass` project) and fix any compilation errors.

---

## File Summary

| File | Purpose |
|------|---------|
| `glass/glass.csproj` | Project file with dependencies |
| `glass/mod.toml` | StarMap mod metadata |
| `glass/Mod.cs` | StarMap lifecycle + ImGui window |
| `glass/Patcher.cs` | Harmony patches for Camera FOV |

## Design Notes

- **Why prefix on `UpdateProjection` instead of postfix?** A prefix lets us set `_fovRadians` *before* the projection matrix is built, meaning the matrix is constructed correctly with our FOV. A postfix would need to rebuild the matrix a second time, which is wasteful.
- **Why also call `SetFieldOfView` in `OnAfterUi`?** The Harmony patch alone isn't enough because `UpdateProjection` only fires when something triggers it. We need to proactively apply our FOV each frame to handle cases where the game resets FOV (e.g., settings reloaded, camera mode changed).
- **Why block `ChangeFieldOfView`?** Without this, the player's Page Up/Page Down inputs would temporarily change FOV until our next frame enforcement overwrites it, causing visual flicker.
- **Hotkey choice:** F9 is used since F11 is already taken by camera-controller-override.
