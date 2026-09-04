# Hot Pursuit Library

Reusable implementation for the Hot Pursuit KSA mod. `HotPursuitSubmod` implements
`MeowSci.KsaAbstractions.ISubmod`, so the same camera manager and ImGui panel can run in the
standalone `hot-pursuit` StarMap host or inside the `unscience` umbrella mod.

The library owns camera entries and `IViewportOwner` lease tokens, performs mesh-precise vehicle
part picking, re-resolves targets by vehicle ID and `Part.InstanceId`, and recomposes each camera's
part-relative pose from `FixedController.OnFrame`. It uses KSA's stock secondary viewport renderer
and window; it does not allocate Vulkan resources.

See [`../hot-pursuit/README.md`](../hot-pursuit/README.md) for features, user controls, limitations,
and the four-slot shared-pool constraint. Game integration details and update risks are cataloged in
[`../scope/camera.md`](../scope/camera.md).
