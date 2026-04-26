# Load SubParts Follow Alert Analysis

## Problem

When Space Tape generates SubPart thumbnails from the `Load SubParts` modal, KSA repeatedly displays cyan timed alerts such as `Following Rocket`. The alerts stack through the center of the screen while thumbnail batches are generated, creating a bad user experience even though the thumbnail output itself succeeds.

## Relevant Space Tape Code

- `space-tape.lib/Thumbnails/SubpartThumbnailGenerator.cs`
  - `GenerateAll()` starts catalog thumbnail generation.
  - `StepGeneration()` renders one batch per frame.
  - Before the fix, each frame saved `camera.Following`, called `camera.Unfollow()`, rendered thumbnails, then restored with `camera.SetFollow(savedFollowing, tidalLocking: false)`.
- `space-tape.lib/Thumbnails/SingleSubpartGenerator.cs`
  - Generates the large detailed SubPart viewer images.
  - It used the same save/unfollow/render/restore pattern.
- `space-tape.lib/PartEditorScene.cs`
  - Already uses `SetFollow(..., alert: false)` when intentionally moving the camera into and out of the editor scene.

## Relevant KSA Decompiled Code

- `decomp/ksa/KSA/Camera.cs`
  - `Camera.AlertFollowing()` creates the on-screen alert via `TimedAlert.Create("Following " + _following.Id, Color.Cyan, 3.0)`.
  - `Camera.SetFollow(IFollowable target, bool tidalLocking, bool changeControl = true, bool alert = true)` calls `AlertFollowing()` when `_following != target && alert`.
  - `Camera.Unfollow(bool changeControl = true)` clears `_following`, clears tidal locking, and sets `Program.ControlledVehicle = null` when `changeControl` is true.
- `decomp/ksa/KSA/TimedAlert.cs`
  - `TimedAlert.Create(...)` registers messages.
  - `TimedAlert.Draw(...)` renders them centered on screen.
- `decomp/ksa/KSA.Rendering/ThumbnailCreator.cs`
  - Stock game thumbnail generation also mutates a render camera and restores follow with the default alert behavior. Space Tape's own generators copied the same risky pattern, but use the live rendered viewport camera during gameplay.

## Root Cause

The root cause is the default `alert: true` argument on `Camera.SetFollow`.

Space Tape's thumbnail generation is frame-based. On every frame used for a thumbnail batch, it did this sequence:

1. Save the current follow target, usually the active rocket.
2. Call `camera.Unfollow()` so the camera can be placed at the thumbnail origin.
3. Render one SubPart batch.
4. Restore with `camera.SetFollow(savedFollowing, tidalLocking: false)`.

Because `Unfollow()` clears `_following`, the restore always looks like a changed follow target to KSA. Since the restore omitted `alert: false`, KSA posted `Following <target.Id>` on every batch. If the target's id is `Rocket`, the visible spam is `Following Rocket`.

The old calls also had two secondary state issues:

- `camera.Unfollow()` used its default `changeControl: true`, briefly clearing `Program.ControlledVehicle` during thumbnail work.
- The restore forced `tidalLocking: false`, even if the user had tidal locking enabled before thumbnail generation.

## Applied Fix

Implemented `space-tape.lib/Thumbnails/ThumbnailCameraState.cs` and used it from both thumbnail generators.

The helper captures:

- Camera framebuffer size
- Viewport size
- Follow target
- Tidal locking state
- Absolute and local camera transform
- `Program.ControlledVehicle`
- `Program.DeviceHostSharedMemoryDebug` flags that thumbnail rendering temporarily disables

During thumbnail rendering it calls:

```csharp
camera.Unfollow(changeControl: false);
```

During restore it calls:

```csharp
camera.SetFollow(savedFollowing, savedTidalLocking, changeControl: false, alert: false);
```

Then it restores local transform, controlled vehicle, debug flags, camera size, viewport size, and runs `camera.OnFrame(...)` to refresh camera-derived state.

This keeps the same thumbnail rendering behavior while preventing KSA from posting follow-change alerts. It also avoids the temporary controlled-vehicle clear and preserves tidal locking.

## Option Details For Future Agents

### Option A: Shared Quiet Camera State Helper

Status: applied.

Implementation:

- Add a helper that captures and restores all camera/viewport/control state touched by thumbnail rendering.
- Use `Unfollow(changeControl: false)` for the temporary thumbnail camera setup.
- Use `SetFollow(..., changeControl: false, alert: false)` for restore.
- Restore `Program.ControlledVehicle` explicitly.

Pros:

- Solves the alert spam at the direct cause.
- Covers both catalog thumbnails and large single-subpart previews.
- Reduces duplicated camera-state code.
- Preserves tidal locking and controlled vehicle state better than the old code.
- Avoids Harmony and global alert filtering.

Cons/Risks:

- Still mutates the live rendered viewport camera for thumbnail rendering during the frame. If future KSA rendering code becomes more sensitive to mid-frame camera changes, a deeper off-screen approach may be needed.
- Runtime smoke testing is still needed in-game because this path depends on KSA rendering internals.

### Option B: Minimal Two-Call Fix

Implementation:

```csharp
camera.Unfollow(changeControl: false);
...
camera.SetFollow(savedFollowing, camera.TidalLocking, changeControl: false, alert: false);
```

Pros:

- Very small change.
- Directly suppresses the alert.
- Avoids clearing `Program.ControlledVehicle`.

Cons/Risks:

- Easy to regress because the save/restore logic remains duplicated.
- Needs careful capture of tidal locking before calling `Unfollow()`, because `Unfollow()` clears it.
- Does not restore the full camera local transform unless added separately.

### Option C: Smallest Hotfix

Implementation:

```csharp
camera.SetFollow(savedFollowing, tidalLocking: false, alert: false);
```

Pros:

- Stops the visible spam with the least code churn.
- Lowest chance of compile-time surprises.

Cons/Risks:

- Still lets `camera.Unfollow()` clear `Program.ControlledVehicle` during thumbnail work.
- Still forces tidal locking off.
- Keeps duplicated restore code.
- Fixes the symptom more than the state hygiene problem.

### Option D: Scoped Harmony Suppression

Implementation ideas:

- Patch `Camera.SetFollow` and rewrite `ref bool alert = false` only while a Space Tape `QuietFollowScope` flag is active.
- Or patch `Camera.AlertFollowing()` and return `false` while the same flag is active.
- Or patch `TimedAlert.Create(...)` and drop messages that start with `Following ` while the flag is active.

Pros:

- Can suppress alerts even if some KSA or third-party thumbnail path cannot be edited.
- A `Camera.SetFollow` prefix with a scoped flag is less string-based than filtering timed alert text.

Cons/Risks:

- Global patches are broader than necessary for Space Tape's own generators.
- If the scope leaks, legitimate user follow alerts can disappear.
- String filtering in `TimedAlert.Create` is brittle and can affect other mods or game features.
- Harmony is unnecessary for the current known call sites.

### Option E: Dedicated Off-Screen Thumbnail Camera/Viewport

Implementation idea:

- Build thumbnail resources around a scratch camera/viewport instead of `Program.RenderedViewport`.
- Feed model matrices and `ThumbnailRenderResources` directly to `ThumbnailRenderer.RecordPartRender(...)`.

Pros:

- Architecturally cleanest long-term approach.
- Avoids mutating the active gameplay camera at all.

Cons/Risks:

- Highest implementation risk.
- KSA's thumbnail renderer and shader-data path are coupled to viewport/render-resource indices.
- Would need deeper runtime validation across KSA versions and GPU states.

## Recommended Runtime Smoke Tests

1. Load a save with a controlled/followed rocket visible.
2. Open Space Tape's `Load SubParts` modal.
3. Generate a small thumbnail set.
4. Confirm no cyan `Following Rocket` messages appear during generation.
5. Confirm the controlled vehicle remains controllable during and after generation.
6. Toggle tidal locking before generation, then confirm it remains enabled afterward.
7. Open the SubParts window and launch the large viewer to cover `SingleSubpartGenerator`.
8. Re-generate thumbnails to ensure reset and repeated generation remain quiet.

## Build Verification

Because this repository can copy build outputs into live KSA mod folders, use a redirected output directory when KSA may be running:

```powershell
dotnet build .\ksa-mod-experiments.slnx /p:SelectedDistModDir="$PWD\.tmp-build-mods\"
```
