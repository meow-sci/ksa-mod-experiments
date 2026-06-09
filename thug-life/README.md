# thug-life

Apply the classic "thug life" sunglasses meme as a 2D textured quad anchored to any
part or subpart of any vehicle in 3D space. Multiple sunglasses can be placed at once,
each with its own offset, rotation, and size.

## Toggle window

Press **F12** to open / close the standalone control panel. When loaded inside the
**unscience** supermod, the same UI appears as a collapsible section in the unscience
window (F11) — no separate hotkey needed there.

## Usage

1. Press F12 to open the **Thug Life** window.
2. Under **Anchor New Sunglasses**:
   - Pick a **Vehicle** from the dropdown
   - Pick a **Part** on that vehicle
   - Optionally pick a **SubPart** of that part — leave at `(use this part)` to
     anchor to the part itself
3. Tune the **Position** (meters), **Rotation** (degrees pitch/yaw/roll), and **Width / Height** (meters) in the anchor part's local frame.
4. Click **Add Sunglasses**.

The new sunglasses entry appears under **Active Sunglasses** and can be re-tuned in
place. Toggle the **Visible** checkbox to hide an entry without removing it, or click
the red **Remove** button to delete it.

## How it works

### Texture
Generated programmatically in [ThugLifeTexturePattern.cs](../thug-life.lib/ThugLifeTexturePattern.cs) as a 15x4 R8G8B8A8UNorm bitmap matching the iconic
blocky sunglasses look — two lenses with a transparent bridge, stepped top/bottom
edges, and white "glare" highlights in the upper-left of each lens.

The texture is uploaded once at mod load via [ThugLifeTextureFactory](../thug-life.lib/ThugLifeTextureFactory.cs) using
`SimpleVkTexture` + `VkUtils.UploadBufferToImage`. A nearest-neighbour sampler preserves the blocky pixel-art look at any size.

### Rendering
Each frame, a Harmony **postfix** on `SuperMeshRenderSystem.RenderMainPass` injects
draw commands for every entry into the active offscreen render pass:

- Pipeline uses KSA's stock `UnlitMeshVert` / `UnlitMeshFrag` shaders.
- Targets `Program.OffScreenPass` (NOT `Program.MainPass`) with matching MSAA sample
  count, reverse-Z depth test/write, and no face culling so the quad is visible from
  both sides.
- Per-entry model matrix is composed in ego-space using `part.PositionEgo` +
  `part.Asmb2Ego` so the quad rides along with whatever vehicle / subpart it is
  anchored to.

Detailed approach lives in the project's [ksa/quad.md skill doc](../.claude/skills/ksa/quad.md).

## Files

### thug-life (mod entry assembly)

| File | Purpose |
|---|---|
| `Mod.cs` | StarMap lifecycle, F12 toggle, top-level ImGui window framing. |
| `Patcher.cs` | Applies `HotkeyGuard` plus calls `ThugLifeRenderPatches.Apply` to install the render postfix. |

### thug-life.lib (reusable core)

| File | Purpose |
|---|---|
| `ThugLifeSubmod.cs` | `ISubmod` implementation owning the UI and the render manager. Hosted by both the standalone Mod.cs and unscience. |
| `ThugLifeEntry.cs` | Per-anchor state: vehicle, part, position/rotation/size, visibility. |
| `ThugLifeRenderManager.cs` | Holds entries + GPU resources; static `Active`/`Instance` for the Harmony postfix; iterates entries and submits draws. |
| `ThugLifeQuadRenderer.cs` | Owns the pipeline, descriptor set, vertex/index buffers; computes the per-frame MVP and records one draw per entry. |
| `ThugLifeTextureFactory.cs` | Creates the `SimpleVkTexture` + `VkSampler` for the sunglasses pattern. |
| `ThugLifeRenderPatches.cs` | Shared `Apply` / `Remove` Harmony postfix on `SuperMeshRenderSystem.RenderMainPass` — used by both the standalone Patcher and the unscience Patcher. |
| `ThugLifeTexturePattern.cs` | The 15x4 ASCII pixel grid that defines the meme image. |

## Notes

- Width / Height are in meters and apply directly to the unit-square quad — the
  default 0.6 m × 0.16 m matches the texture's 15:4 aspect ratio. Tweak both to
  re-stretch.
- Rotation is applied in the **anchor part's local frame** (not the camera or
  vehicle), so a fixed rotation stays "stuck" to the part even as the vehicle
  rotates relative to the camera.
- The quad's `+Z` normal initially points along the anchor part's +Z; use the
  rotation fields to face it where you want.
- If rendering ever throws (e.g. shaders missing), the manager disables itself and
  the error appears in the UI; it never spams the render loop.
