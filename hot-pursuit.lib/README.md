# Hot Pursuit Library

Reusable implementation for the Hot Pursuit KSA mod. `HotPursuitSubmod` implements
`MeowSci.KsaAbstractions.ISubmod`, so the same camera manager and ImGui panel can run in the
standalone `hot-pursuit` StarMap host or inside the `unscience` umbrella mod.

The library owns camera entries and `IViewportOwner` lease tokens, performs mesh-precise vehicle
part picking, re-resolves targets by vehicle ID and `Part.InstanceId`, and recomposes each camera's
part-relative pose from `FixedController.OnFrame`. It uses KSA's stock secondary viewport renderer
and window; it does not allocate Vulkan resources.

After writing each mounted ECL position, `HotPursuitCelestialState` mirrors KSA 5402's
main-camera nearby-celestial selection and public distance/terrain/altitude fields. This
prevents the nearby body from also being emitted by the distant-sphere pass (the source of
the dark-grey secondary-view artifact) while retaining KSA's 80,000 km surface-distance
cutoff.

The KSA 5402 secondary `Program.RenderViewport` path omits the `ParticleSystem`,
`VolumetricExhaustRenderer`, main planet/ocean/cloud, part-glass, and overall-bloom passes.
Engine plumes and generic particles are therefore not available in these feeds; the game-owned
passes bind main-camera targets/resources and are intentionally not re-injected by this library.

See [`../hot-pursuit/README.md`](../hot-pursuit/README.md) for features, user controls, limitations,
and the four-slot shared-pool constraint. Game integration details and update risks are cataloged in
[`../scope/camera.md`](../scope/camera.md).
