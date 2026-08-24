# dont-stifle-me

Removes two restrictions the KSA vehicle editor added in build `2026.8.22.5348`:

1. **Scale clamp** — top-level parts can no longer be scaled outside **0.5x–2.0x**.
2. **Uniform scaling** — dragging any scale-gizmo arrow now scales all three axes together; per-axis
   (non-uniform) scaling is gone.

With this mod enabled, both go away: any positive scale is accepted and each gizmo arrow (X / Y / Z)
scales only its own axis, like the pre-5348 editor.

## Toggle window

Press **F11** to open / close the standalone window. Inside the **unscience** supermod the same
controls appear as the **"Don't Stifle Me - Editor Scale Limits"** section.

## Controls

| Control | Effect |
|---|---|
| **Don't stifle me** (master) | Off = stock editor behavior. Flip at any time; no restart needed. |
| Remove 0.5x-2x scale clamp | Widens the editor's scale bounds to `(1e-6, +inf)` for top-level parts. |
| Per-axis (non-uniform) scaling | Gizmo drags change only the dragged axis. |

All three default to **on**.

## Notes / limitations

- The game's **0.25 m diameter snapping** still applies (it is what makes drags feel deliberate);
  the mod only widens the bounds it snaps within.
- Non-uniform scale is a *visual/mesh* scale. The game now derives connector positions, mass and
  inertia from a single `ScaleFactors` value = the **largest axis**, so connectors on a part stretched
  along one axis may not sit on the mesh surface. This is a game limitation, not something the mod
  can fix without replacing `Part.RefreshScale`.
- Sub-parts already had unbounded scale in the stock editor; the mod does not change them.

## How it works

Core logic lives in [`dont-stifle-me.lib`](../dont-stifle-me.lib/README.md). This project is the
standalone StarMap entry: `Patcher.cs` applies `HotkeyGuard` + `EditorScalePatches`, `Mod.cs` hosts
the `DontStifleMeSubmod` UI in an F11 window.
