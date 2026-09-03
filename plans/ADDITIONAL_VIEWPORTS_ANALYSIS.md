# problem

i want to have additional viewports render video inside imgui windows OR on quads or something like that. 

Preferably an imgui window.

There is already an existing docking port camera that kind of works but i think that it may be broken'ish at the moment as the video feed doesnt update live.

I know the developers working on KSA have been making recent fixes to support multiple camera / viewports.

What I really want to do is to be able to have an ImGui window be an additional camera viewport that I can point at the KittenEva models (which are the astronauts of the game) and have them as small crew avatars for their current expressions

Do a deep dive analysis of the codebase to see how this may be possible and document it in the `# analysis` section of ADDITIONAL_VIEWPORTS_ANALYSIS.md file with fine detail sufficient that we could use the information from the analysis to create a game mod to accomplish it without having to re-analyze the whole game again.

# analysis

> ⛔ **Superseded by KSA `2026.9.7.5402` — do not build against this analysis as written.**
> Everything below was researched against the pre-5402 viewport API, which the game has since
> replaced wholesale: the `KSA.Viewport` **class is gone** (now `IViewport` / `IGameViewport` /
> `ViewportBase` / `GameViewport` / `PartThumbnailViewport`), `Program.Viewports` and
> `Program.ViewportCount` no longer exist (viewports live in `ViewportRegistry`, `Views` /
> `GameViews`, capped at `MAX_VIEWPORTS = 8` with shader slots handed out by a pool),
> `Viewport.Index` is now `IViewport.ShaderSlot`, and `EViewportLightMode` is now
> `ViewportLightMode`. The **mechanism this document wanted now exists first-class**:
> `ViewportRegistry.TryOpenSecondaryViewport` / `TryClaimSecondaryViewport(IViewportOwner, …)` /
> `ReleaseSecondaryViewport`, with per-viewport behaviour selected by `ViewportOptionFlags`
> (`HasUi`/`HasInput`/`RenderGizmos`/`RenderPartModels`/…) and `ViewportType`
> (`Main`/`Secondary`/`PartThumbnail`/`CharacterPortrait`) — and the game already ships
> crew-portrait viewports (`Program.CrewPortraitViewports`), which is close to the feature this
> document set out to build. **Re-do the analysis against `ViewportRegistry.cs` before writing any
> code.** The problem statement and the general shape of the findings are still useful history.
> See [`KSA_5402_UPGRADE.md`](KSA_5402_UPGRADE.md) §2.1.


> All file:line references are into `decomp/ksa/` and reflect the decompiled sources, which **may lag the running binary**. Anything marked **VERIFY** must be confirmed at runtime with a reflection/Dbg probe before relying on it.

## 1. Verdict / feasibility

**This is very achievable, and the engine already does almost exactly what we want.** KSA ships a complete multi-viewport system: each `Viewport` owns a `Camera`, controllers, an offscreen render target, *and* registers its final rendered image with ImGui as an `ImTextureRef`. The game already renders multiple viewports per frame and already special-cases `KittenEva` so its avatar (including facial expression) renders live into every visible secondary viewport.

There are two layers to the solution:

1. **Use an existing secondary viewport** (zero engine risk). KSA builds **3** viewports at startup (`Program.ViewportCount = 3`): index 0 is the fullscreen main view; indexes **1 and 2** are 500×500 offscreen targets already wired for ImGui display. The shipping **"Docking Port Camera"** (`DockingPort.cs:223-251`) is a working precedent that grabs a spare viewport, points it via `FixedController`, and shows it. We can do the same but point at a `KittenEva` and draw the texture in our own panel.

2. **Scale to N viewports** (what you asked for — "infinite"). The entire render system is **generalized over `Program.ViewportCount`** — every consumer reads that field dynamically (see §6). The per-viewport shader uniforms use a **dynamic uniform-buffer offset** keyed by viewport index (`GlobalShaderBindings.DynamicOffset(viewportIndex)`), *not* a fixed-size shader array, so **there is no GLSL-level cap on viewport count**. The only thing pinning it at 3 is the literal initializer `public static int ViewportCount = 3;` (`Program.cs:221`). We need to set that value *before* the engine allocates its per-viewport arrays — and the StarMap sources (§6.3) **confirm we can**: a `[StarMapBeforeMain]` mod method runs before KSA's entry point is even invoked. The remaining cost is GPU/VRAM (each viewport pre-allocates render targets; each *visible* one is a full scene render), which bounds "infinite" to a sane max — see §6.3.

**Recommended build:** a `crew-cam` mod (lib + `ISubmod`, per repo conventions) that (a) ensures enough viewports exist, (b) per crew member assigns a viewport in `Fixed` mode following that `KittenEva` with a recomputed head-framing offset, (c) suppresses the game's default window for those viewports, and (d) draws each viewport's image as a tile in a custom ImGui "crew" panel.

---

## 2. The engine's multi-viewport architecture

### 2.1 `Viewport` (`KSA/Viewport.cs`)
The central abstraction. Key members:
- `public bool Visible` — gates **both** whether the viewport's scene is rendered each frame **and** whether the game draws its ImGui window. (This dual role matters — see §5.3.)
- `public CameraMode Mode` and one controller each: `OrbitController`, `FlyController`, `MapController`, `IVAController`, `FixedController` (lines 38–46). `GetActiveController()` (349) dispatches on `Mode`; `SetCameraMode(mode)` (369) switches with `OnSwitchOff`/`OnSwitchOn`.
- `public Camera BaseCamera` / `MapCamera`; `GetCamera()` returns `MapCamera` only in `Map` mode, else `BaseCamera` (360).
- `public RenderTarget? OffscreenTarget` (MSAA scene target) and `public RenderTarget? MainTarget` (1-sample resolved/composited target). `MainTarget.ColorImage.ImageView` is the **final sampleable image** shown in ImGui.
- `private ImTextureRef _imguiViewportTextureId` (line 66) — the ImGui handle for `MainTarget.ColorImage.ImageView`. **Private**; see §5.2 for access.
- `BuildRenderTarget()` (221) builds both targets, the render passes, framebuffers, and registers the texture:
  ```csharp
  _imguiViewportTextureId = ImGuiBackend.Vulkan.AddTexture(_sampler, MainTarget.ColorImage.ImageView);
  ```
- `DrawImGui()` (268) — the game's own window for the viewport. When `Visible`, it `ImGui.Begin(_viewportName,...)`, then:
  ```csharp
  ImGui.ImageWithBg(_imguiViewportTextureId, in imageSize, null, null, null, null);   // line 299
  ```
  followed by an invisible input-blocker button, hover/menu handling, and `Program.Instance.DrawMenuBar(this, Size.X)`.
- `Resize(int2)` (329) disposes & rebuilds the targets and **re-registers** the ImGui texture (so the `ImTextureRef` changes on resize — important for our texture handle lifecycle).
- `OnFrame(dt)` (165) runs the active controller, the camera, and viewport audio.

### 2.2 Viewport list & accessors (`KSA/Program.cs`)
- `public static int ViewportCount = 3;` (221)
- `public static readonly List<Viewport> Viewports = new();` (223)
- `_mainViewportIndex = 0` (225); helpers: `MainViewport`, `HoveredViewport`, `RenderedViewport`, `FrameViewport`, `VisibleViewportCount` (392–400).
- `AddViewport(int2 size, bool buildRenderTarget, bool centerViewport=false)` (523) — constructs a `Camera`, a `Viewport`, optionally calls `BuildRenderTarget()`, and appends to `Viewports`. **This is the factory we can reuse.**
- Startup build (876–887):
  ```csharp
  _viewportSampler = device.CreateSampler(Presets.Sampler.SamplerPointClamped, null);
  _mainViewportIndex = 0;
  AddViewport(new int2(extent.Width, extent.Height), buildRenderTarget: false); // viewport 0
  for (int i = 1; i < ViewportCount; i++)
      AddViewport(new int2(500, 500), buildRenderTarget: true);                 // viewports 1,2
  MainViewport.Visible = true;
  ```

### 2.3 The per-frame render loop
`Program.OnFrame` (1927) → `OnFrameViewports` (2125) → … → `RenderGame` (3820).

- `OnFrameViewports` (2125): for each viewport, rebuilds it if `NewSize` changed, then `viewport.OnFrame(dt)` (drives the controller/camera), and tracks hover. **Runs for every viewport regardless of `Visible`** for `OnFrame`, but UI/burn-node work is gated on `Visible`.
- `UpdateRenderingResources(frameIndex)` (3578): for each **visible** viewport, populates `GlobalShaderBindings.CameraData(i)/LightingData(i)/...` and calls `vehicle.UpdateRenderData(viewport, frameIndex)` for **non-`KittenEva`** vehicles (3608–3616), then `PartModelRenderer.UpdateRenderData`, gizmos, etc., then descriptor-set updates (3636–3648).
- `RenderGame` (3820): for `i = 1 .. ViewportCount-1`, if `viewport.Visible`, calls `RenderViewport(cmd, viewport, frameIndex)` (3844–3851); then renders the main viewport (index 0).
- `RenderViewport(cmd, viewport, frameIndex)` (3716): the per-secondary-viewport scene render. Notably, **before** the render pass it does (3722–3733):
  ```csharp
  if (Editor == null)
    foreach (Vehicle v in VehiclesInFrame)
        if (v is KittenEva)
            v.UpdateRenderData(viewport, frameIndex);   // KittenEva updated HERE, per visible viewport
  ```
  then begins the offscreen pass, renders stars/celestials/parts (`SuperMeshRenderSystem.RenderMainPass`), sun bloom, MSAA resolve, translucency, **orbit lines, gizmos**, resolves to `MainTarget`, and composites (`_compositeRenderer[viewport.Index].Render(...)`). The result lands in `MainTarget.ColorImage`, which is what the ImGui texture samples.

**Takeaway:** secondary viewports get a *complete* scene render (stars, planets, atmosphere, bloom, the works) every frame they are `Visible`. That is the cost driver for "many viewports."

---

## 3. Render-data flow & why `KittenEva` is the well-supported path (and the likely docking-port bug)

The render-data update is split between two phases, and `KittenEva` is deliberately handled in the *render* phase, not the *resource* phase:

- **Regular vehicles**: `UpdateRenderData` called in `UpdateRenderingResources` for each visible viewport (`!(v is KittenEva)`, `Program.cs:3612`).
- **`KittenEva`**: `UpdateRenderData` called inside `RenderViewport` for each visible secondary viewport (`v is KittenEva`, `Program.cs:3728`), and again for the main viewport in `RenderGame`.

`KittenEva.UpdateRenderData` (`KittenEva.cs`) calls `base.UpdateRenderData` then `_renderable.UpdateRenderData(viewport, frameIndex, dt, worldMatrix, accel, angularAccel)`. `GetWorldMatrix(camera)` (`Vehicle.cs:2252`) returns `null` if the apparent diameter < 1px (a culling gate — the camera must be close enough). `KittenRenderable.UpdateRenderData` (`KittenRenderable.cs:143`) sets the `CharacterModel.Transform`, calls `UpdateAnimation(dt * simSpeed)` and `.Draw()` for the model, fur, helmet, MMU, cosmetics — queuing into the **current** viewport's bucket. The avatar/skeleton/expression state is a **single shared instance per kitten** (`_characterAvatar`, `_catExpressionAnim`), so the **same facial expression renders identically in every viewport** in a frame — exactly what we want for a crew-expression panel.

**Conclusion for our use case:** pointing a visible secondary viewport's camera at a `KittenEva` will produce a **live, expression-correct render**, provided (a) the kitten is in `VehiclesInFrame` (auto — it's any vehicle in `Universe.CurrentSystem`, `Program.cs:RefreshVehiclesInFrame`), (b) the viewport is `Visible`, and (c) the camera is close enough to clear the 1px cull.

**Likely cause of the "docking port camera doesn't update live" symptom (VERIFY):** the docking-port path uses `FixedController` and sets `CameraOffset` **once** at toggle-time, computed in the vehicle's body-fixed frame, but `FixedController.OnFrame` (`FixedController.cs:31`) applies it as a **static ECL-space offset** added to `following.GetPositionEcl()`. It only re-reads the followed object's *position* each frame, not its rotation, so as the docked vehicle rotates/orbits the camera no longer tracks the port — the image looks frozen/wrong even though it is technically re-rendering. Whatever the exact in-binary cause, **our mod sidesteps it by recomputing the framing offset every frame** (§4.2). (Other possibilities to rule out at runtime: the running build may not yet call `KittenEva.UpdateRenderData` in `RenderViewport` for secondary viewports, or the spare viewport may not be getting `UpdateRenderingResources` — both are checkable with a Dbg probe.)

---

## 4. Pointing a viewport's camera at a `KittenEva`

### 4.1 The follow API (`KSA/Camera.cs`)
- `Camera.Following` is `IFollowable?` (read-only, line ~139). `Vehicle` (and thus `KittenEva`) implements `IFollowable`.
- `camera.SetFollow(IFollowable target, bool tidalLocking, bool changeControl = true, bool alert = true)` (~550) — sets `_following`, snaps camera to `2.5 * target.MeanRadius` away, and (if `changeControl`) sets `Program.ControlledVehicle = target as Vehicle`. **Call with `changeControl: false, alert: false`** so we don't hijack the player's control or spam "Following …" alerts.
- `camera.Unfollow(changeControl=false)` to release.
- One-shot aim helpers exist: `LookAt(double3 target, double3 up)`, static `LookAtRotation(double3 forwardEcl, double3 upEcl)`. Camera world space is **ECL**.

### 4.2 Auto-headshot framing via `FixedController` (`KSA/FixedController.cs`)
This is the cleanest mechanism and matches the docking-port precedent. `FixedController` exposes two tunables and a simple `OnFrame`:
```csharp
public double3 CameraOffset;     // added to following.GetPositionEcl() => camera position (ECL)
public double3 CameraRotation;   // forward look direction (ECL); up is derived
// OnFrame: pos = following.GetPositionEcl() + CameraOffset;
//          rot = LookAtRotation(CameraRotation, up-from-cross(CameraRotation, frameZ));
```
The shipping docking-port setup (`DockingPort.cs:233-242`) is the template:
```csharp
viewport.Visible = true;
viewport.NewSize = new int2(500, 500);
viewport.SetCameraMode(CameraMode.Fixed);
viewport.FixedController.Camera.SetFollow(vehicle, tidalLocking: true);
viewport.FixedController.CameraOffset   = vehicle.PosAsmbToBody(connectorPos).Transform(vehicle.Body2Cce);
viewport.FixedController.CameraRotation = new double3(1,0,0).Transform(connectorRot);
viewport.AllowResize = false;
viewport.SetViewPortName("Docking Port Camera");
```

**For a live head framing of a KittenEva**, recompute both fields **every frame** (in our `OnBeforeUi`, before the game's `OnFrameViewports`) so the camera tracks the kitten's current orientation:
```csharp
var kitten = (KittenEva)vehicle;
doubleQuat body2Cce = kitten.Body2Cce;                 // body -> body-fixed
// pick a body-frame point in front of & slightly above the head, and a back-off distance:
double3 headLocal   = new double3(0, headUpMeters, 0); // tune: head height in body frame
double3 backOffLocal= new double3(0, 0, -faceDistM);   // tune: how far in front of the face
// face-forward direction in body frame (which body axis the face points along is a VERIFY item):
double3 faceFwdLocal= new double3(0, 0, 1);

double3 headEcl   = headLocal.Transform(body2Cce);     // (ECL offset of head from kitten origin)
double3 camEcl    = (headLocal + (-faceFwdLocal * faceDistM)).Transform(body2Cce);
fc.CameraOffset   = camEcl;                             // camera sits in front of the face
fc.CameraRotation = (headEcl - camEcl);                // look from camera toward the head (ECL)
```
Notes:
- `Body2Cce` is a real `Vehicle` property (per the `ksa` skill). `CameraOffset`/`CameraRotation` are applied in **ECL**; for a body relative to a single celestial, `Body2Cce` is the right body→world rotation to use here, mirroring the docking-port code.
- The exact body axis the kitten's face points along, and a good head height, are **VERIFY** at runtime — start with the docking-port-style guess and adjust live with ImGui sliders, then bake the constants.
- Optional refinement: read the actual **head bone** transform from `avatar.Core.CharacterModel` skeleton `WorldTransforms[headBoneIndex]` for perfect framing. Heavier; the fixed-offset approach is enough for "crew avatar" tiles. Defer.
- Keep the FOV reasonable: viewport cameras inherit `GameSettings` FOV. We can narrow it for a portrait look via the `glass.lib` `FovController` precedent **per-camera** (the per-viewport camera object is independent), or leave default.

### 4.3 Which `KittenEva`s to target
`MeowSci.KsaAbstractions.VehicleProvider` already enumerates vehicles from `Universe.CurrentSystem`. Filter `vehicle is KittenEva` (or `vehicle.GetType().Name == "KittenEva"`). The `doh` mod spawns kittens and the `kitten-animations` mod already drives expressions — both are existing references for KittenEva handling in this repo.

---

## 5. Displaying the feed in a custom ImGui crew panel

### 5.1 Registering the texture (`ImGuiBackend.Vulkan` — `KSA/ImGuiBackendVulkanImpl.cs`)
```csharp
public ImTextureRef AddTexture(VkSampler sampler, VkImageView imageView,
                               VkImageLayout layout = VkImageLayout.ShaderReadOnlyOptimal);  // ~1052
public void RemoveTexture(ImTextureRef texture);                                            // ~1060
```
Accessed via the static `ImGuiBackend.Vulkan` (an `ImGuiBackendVulkanImpl`). We can register our **own** handle to a viewport's final image:
```csharp
var sampler = Program.LinearClampedSampler;            // or create our own / reuse the viewport sampler
var view    = viewport.MainTarget.ColorImage.ImageView; // VkImageView, final composited image
ImTextureRef tile = ImGuiBackend.Vulkan.AddTexture(sampler, view);
```
`MainTarget.ColorImage` is a `FramebufferAttachment` (`RenderTarget.ColorImage` → `Attachments[ColorAttachmentIndex]`); `.ImageView` is the `VkImageView`, `.Image` the `VkImage` (`Framebuffer.cs`). Its render pass `FinalLayout` is `ShaderReadOnlyOptimal`, so it is safe to sample in the ImGui pass with no manual barriers.

**Handle lifecycle:** the `ImTextureRef` is only valid while that `ImageView` lives. `Viewport.Resize()` recreates the view, so re-register after any size change (we control size and set `AllowResize=false`, so resizes are rare/controlled). On dispose, call `RemoveTexture`.

### 5.2 Reuse vs. self-register
The viewport already holds a valid `ImTextureRef` in the private field `_imguiViewportTextureId`. Two options:
- **Self-register (recommended):** call `AddTexture` ourselves as above. No reflection; we own the handle and its lifecycle. Re-register on viewport rebuild.
- **Reflect the existing field:** `typeof(Viewport).GetField("_imguiViewportTextureId", NonPublic|Instance)` each frame. Always current (survives the viewport's own resize re-registration) but reflection-per-frame and depends on the private name (**VERIFY**).

### 5.3 Drawing the tiles and suppressing the game's default window
Draw in our submod `RenderContent()`:
```csharp
ImGui.Image(tile, new float2(tileW, tileH), uv0: null, uv1: null);  // Brutal.ImGuiApi/ImGui.cs ~6331
ImGui.SameLine(); /* lay out a grid of crew tiles */
```
`ImGui.Image(ImTextureRef, in float2 size, in float2? uv0=null, in float2? uv1=null)` and `ImageWithBg(... , float4? bgCol, float4? tintCol)` are both available.

Because `Visible == true` is **required** for the viewport to render *and* causes the game to draw its own `Viewport.DrawImGui()` window, we must suppress that default window for the viewports we manage. Cleanest: a **Harmony prefix on `Viewport.DrawImGui`** that returns `false` (skips original) when the instance is one of ours:
```csharp
[HarmonyPatch(typeof(Viewport), nameof(Viewport.DrawImGui))]
static class Viewport_DrawImGui_Patch {
    static bool Prefix(Viewport __instance) =>
        !CrewCamManager.IsManaged(__instance);   // false => suppress the default window
}
```
We still keep `Visible = true` so `RenderGame`/`UpdateRenderingResources` render the scene; we just don't let the game paint the stock window. We then paint the image ourselves in the crew panel.

(Alternative considered & rejected: drawing the feed on an in-world quad via the `quad.md` pipeline. That samples a `VkImageView` too and works, but you asked for an ImGui window, and `ImGui.Image` of the viewport's `MainTarget` is strictly simpler — no custom pipeline/shader/MVP.)

---

## 6. Scaling to N viewports ("infinite cameras")

### 6.1 The system is fully generalized over `ViewportCount`
Every per-viewport resource is sized from `Program.ViewportCount` at init and every loop reads it dynamically. Confirmed consumers:
- `Program.cs`: `_cameraData/_lightingData/_celestialData/_vesselData = new [...][ViewportCount]` (1075–1078); `_compositeRenderer = new ScreenspaceRenderer[ViewportCount]` (1022); the `AddViewport` startup loop (881); all render loops (3583/3600/3636/3844).
- `GlobalShaderBindings.Initialize` (87, 198): uniform buffer sized `_frameStride * frameCount * viewportCount`.
- `InstancedRenderTechnique.cs:28-29`, `OrbitLinePass.cs:27/36/173`, `SingleToMultisamplePass.cs:41-51`, `StaticCelestial.cs:15`, `SunbloomMergeRenderer.cs:79-82`, `SunbloomRenderer.cs:129/143/209-275`, `DockingPort.cs:228` — all `Program.ViewportCount`.

### 6.2 No shader-side cap (the crucial finding)
`GlobalShaderBindings` binds the camera/lighting/celestial/vessel UBO as a **`UniformBufferDynamic`** at set-0 binding-0, and selects the per-viewport slice at draw time via a **dynamic offset**:
```csharp
public static ByteSize DynamicOffset(int viewportIndex) => FrameOffset(viewportIndex, _currentFrame);
// FrameOffset = viewportIndex*_frameStride*_frameCount + frameIndex*_frameStride
```
The shader sees **one** UBO instance at a time, not an array indexed by viewport. So **adding viewports does not require shader changes and has no hardcoded `[3]` limit** — only the host-side buffer must be big enough, which is governed by `viewportCount` at `Initialize` time. This is the strongest evidence that "many viewports" is a supported, intended capability (consistent with the recent dev work you mentioned).

### 6.3 The one real gate: set `ViewportCount` before engine init (TIMING — RESOLVED via StarMap sources)
The arrays/buffers above are allocated during the game's startup (the big init method spanning ~`Program.cs:790-1180`, which contains the "Build Viewports" task at 876 and `GlobalShaderBindings.Initialize` at 925). To get N viewports cleanly, `Program.ViewportCount` must be **N before that init runs**. The StarMap loader sources (`decomp/starmap`) confirm the boot order and give us a hook that is **structurally guaranteed to run before KSA's entry point**:

**StarMap boot/lifecycle order (definitive):**
1. `GameSurveyer.TryLoadCoreAndGame()` (`StarMap.Loader/GameSurveyer.cs:26-50`): loads the **KSA game assembly** (`_game = ...LoadFromAssemblyPath(_gameLocation)`, line 35), then calls `core.Init()` (line 48) — **but does not run the game yet**.
2. `StarMapCore.Init()` → `ModLoader.PrepareMods()` → for each mod, `RuntimeMod.InitializeMod()` instantiates the `[StarMapMod]` class, registers its attributed methods, and **immediately invokes the `[StarMapBeforeMain]` method** (`RuntimeMod.cs:153-156`; the mapping `BeforeMainAttribute → BeforeMainAction` is in `ModRegistry.cs:46-47`).
3. **Then** `GameSurveyer.RunGame()` (line 52-57) invokes KSA's entry point → constructs `Program` → runs the init that builds viewports/arrays.

So **`[StarMapBeforeMain]` runs before `Program` is ever constructed.** Because the KSA assembly is already loaded by then, `KSA.Program.ViewportCount` (a static field) is fully addressable. Setting it there is bulletproof:
```csharp
[StarMapBeforeMain]
public void BeforeMain()       // signature: void, no params (BaseAttributes.cs:38-45)
{
    KSA.Program.ViewportCount = desiredViewportCount;  // e.g. 1 main + N crew slots
}
```
(Writing the field triggers `Program`'s static init first — which sets the literal `3` — then our assignment overwrites it to N. The instance-side array allocations run later in `RunGame`, reading the final N.)

**Secondary confirmation:** even `[StarMapAllModsLoaded]` precedes the viewport build in the current decomp — it fires as a postfix on `ModLibrary.LoadAll()` (`StarMap.Core/Patches/ModLibraryPatches.cs`), and `LoadAll()` is at `Program.cs:873`, three lines *before* "Build Viewports" at `876` (and well before `GlobalShaderBindings.Initialize` @925 and the arrays @1022/1075). So setting `ViewportCount` in `[StarMapAllModsLoaded]` would also take effect today — but that ordering is incidental (same method, a few lines apart) and could drift, whereas `[StarMapBeforeMain]` is guaranteed by construction. **Prefer `[StarMapBeforeMain]`.**

**This removes the need for a round-robin fallback as the primary plan.** (Round-robin over the 2 stock spares remains a valid *degrade* path if, in some future build, `[StarMapBeforeMain]` were unavailable or the count-set were rejected — keep it in mind but don't build for it first.)

**Cost model (the real ceiling, now that timing is free):**
- **Upfront VRAM ∝ N, paid whether or not a viewport is visible.** `AddViewport(..., buildRenderTarget:true)` builds an offscreen + main render target per viewport (default 500×500), and the init loops allocate `_compositeRenderer[N]`, sunbloom targets ×N (`SunbloomRenderer.cs:129/209`), orbit-line buffers ×N, etc. So raising `ViewportCount` to N pre-allocates N full sets of per-viewport render resources at startup. **Implication: don't pick a literally "infinite" N — pick a sane cap (e.g. 8–16) and/or use small target sizes.** We control size via `viewport.NewSize` / `Resize` after grabbing the viewport, so set crew tiles to something small (e.g. 128–192 px) to keep VRAM modest.
- **Per-frame GPU cost ∝ number of *visible* viewports only.** `RenderGame` (`Program.cs:3844`) renders a secondary viewport only when `viewport.Visible`. Hidden pre-allocated viewports cost no render time. So you can pre-allocate a generous slot count and only pay render cost for the crew tiles actually shown.
- **Per-visible-viewport cost is a full scene render** (stars, planets, atmosphere, bloom, composite — §2.3). Mitigations if many tiles are visible at once: tiny tile resolution, and optionally a Harmony short-circuit in `RenderViewport` for our managed viewports to **skip the celestial/atmosphere/star passes** (we only need the kitten + a clear/space background). Treat the skip as a phase-2 optimization.

### 6.4 Recommended scaling stance
Set `Program.ViewportCount` in `[StarMapBeforeMain]` to `1 + maxCrewSlots` (a fixed, configurable cap — not unbounded, because of the upfront VRAM in §6.3). The engine then builds that many viewport slots for free. Keep all crew viewports **hidden** until a crew tile is shown, set each to a small resolution, and only make visible the ones currently displayed. This gives genuine simultaneous multi-camera rendering (your goal) with cost that scales with *shown* tiles, not slot count. Add the `RenderViewport` celestial-skip optimization only if profiling shows it's needed.

### 6.5 StarMap lifecycle hooks (corrected reference)
From `decomp/starmap` (`StarMap.API/BaseAttributes.cs`, `OnFrameAttributes.cs`, `OnGuiAttributes.cs`, and the patch/registry classes), the actual hook semantics — **note these differ from the abbreviated template in the `ksa` skill**:

| Attribute | When it fires | Signature | Invoked by |
|---|---|---|---|
| `[StarMapBeforeMain]` | **Before KSA entry point runs** (Program not yet constructed) | `void M()` | `RuntimeMod.InitializeMod` during `StarMapCore.Init` |
| `[StarMapAllModsLoaded]` | Postfix of `ModLibrary.LoadAll()` (`Program.cs:873`, **before** viewport build @876) | `void M()` | `ModLibraryPatches.AfterLoad` |
| `[StarMapImmediateLoad]` | Prefix of `KSA.Mod.PrepareSystems()` (per mod, on systems prepare) | `void M(KSA.Mod definingMod)` — **requires the `Mod` param** | `ModPatches.OnLoadMod` |
| `[StarMapBeforeGui]` | Prefix of `Program.OnDrawUiFrame(dt)` (per frame) | `void M(double dt)` | `ProgramPatcher.BeforeOnDrawUi` |
| `[StarMapAfterGui]` | Postfix of `Program.OnDrawUiViewports(dt)` (per frame) | `void M(double dt)` | `ProgramPatcher.AfterOnDrawUi` |
| `[StarMapAfterOnFrame]` | Postfix of `Program.OnFrame(t, dt)` (per frame) | `void M(double t, double dt)` | `ProgramPatcher.AfterOnFrame` |
| `[StarMapUnload]` | StarMap teardown | `void M()` | `ModLoader.Dispose` |

Consequences for our mod:
- **`[StarMapBeforeMain]`**: set `Program.ViewportCount`. Nothing else is alive yet — no renderer, no `Program.Instance`.
- **`[StarMapAllModsLoaded]`**: renderer (`Program.GetRenderer()`) is live (samplers/ImGui backend created ~`Program.cs:790-807`), but **`Program.Viewports` is still empty** (built at 876, after the LoadAll@873 postfix). Safe place to `Harmony.PatchAll` (the repo convention `OnFullyLoaded`), but **do not try to grab viewport objects here** — they don't exist yet.
- **`[StarMapBeforeGui]` (per frame)**: viewports exist and the renderer is fully live. **This is where to lazily one-time-init**: grab the extra viewport slots, size them small, point them at kittens, register their ImGui textures; and where to recompute per-frame head framing (it lands before `OnFrameViewports`/`RenderGame`).

---

## 7. Recommended mod architecture

Follow repo conventions (see `REPOSITORY_INDEX.md`, `mod-impl`/`mod-dev` rules): a `crew-cam` top-level mod + `crew-cam.lib`, integrated as an `ISubmod` into `unscience`, with a standalone window too.

- **`crew-cam.lib`**
  - `CrewCamManager` (static singleton, like `ThugLifeRenderManager`): owns the list of managed `(viewport, kitten, ImTextureRef)` bindings, the viewport-count strategy, per-frame offset recompute, and `IsManaged(Viewport)`.
  - `CrewCamSubmod : ISubmod` — `RenderContent()` draws the crew grid (one `ImGui.Image` tile per crew member, with name/expression label); `Update(dt)` keeps framing offsets fresh (or do that in the patcher's `OnBeforeUi`).
  - `KittenCamFraming` — the head-offset math (§4.2), with tunable constants and optional live sliders.
- **`crew-cam` mod** (StarMap lifecycle — see the corrected hook table in §6.5)
  - `[StarMapBeforeMain]`: set `Program.ViewportCount = 1 + maxCrewSlots` (fixed cap, §6.4). This is the *only* thing that can be done this early — no renderer/viewports exist yet.
  - `[StarMapAllModsLoaded]` (the repo's `OnFullyLoaded` convention): `Harmony.PatchAll` here (apply the `Viewport.DrawImGui` prefix and `HotkeyGuard`). **Do not grab viewports here — `Program.Viewports` is still empty at this point** (built right after `ModLibrary.LoadAll@873`).
  - `[StarMapBeforeGui] OnBeforeUi(double dt)`: **lazy one-time init on first call** — grab the pre-allocated extra viewport slots (`Program.Viewports[i]` for `i ≥ 1`), shrink them (`NewSize`/small `Resize`), `SetCameraMode(Fixed)`, `SetFollow(kitten, …, changeControl:false, alert:false)`, register each `MainTarget.ColorImage.ImageView` via `AddTexture`. On every call, recompute the head-framing `CameraOffset`/`CameraRotation` (§4.2) so it lands before `OnFrameViewports`/`RenderGame`.
  - `Patcher.cs`: Harmony prefix on `Viewport.DrawImGui` (§5.3) to suppress the stock window for managed viewports. **MUST** also apply `HotkeyGuard.Patch/Unpatch` per the project CLAUDE.md hotkey-guard rule (the panel may have text inputs/sliders). Reference `ksa-abstractions.lib`.
- **Lifecycle/threading:** all viewport/texture work happens on the main thread inside the per-frame `[StarMapBeforeGui]`/`[StarMapAfterGui]` hooks (which run inside KSA's `OnFrame`). On `[StarMapUnload]`: release follows (`Unfollow`), restore each managed viewport's `Visible`/`Mode`/`AllowResize`/name, `RemoveTexture` our handles (clear the managed-flag the `DrawImGui` prefix reads *before* removing), and unpatch Harmony. Note `Program.ViewportCount` is set once at `[StarMapBeforeMain]` and cannot be meaningfully lowered after the arrays are built — leaving the extra (hidden) slots allocated until process exit is the expected behavior. Use `IGameStateScheduler`/`GameThread` from `ksa-abstractions.lib` if anything is ever driven off-thread.

---

## 8. Key API reference (file:line — VERIFY against running binary)

| Need | Symbol | Location |
|---|---|---|
| Viewport list / count | `Program.Viewports`, `Program.ViewportCount` | `Program.cs:223, 221` |
| Add a viewport | `Program.AddViewport(int2, bool, bool)` *(private)* | `Program.cs:523` |
| Build a viewport's targets + register texture | `Viewport.BuildRenderTarget()` | `Viewport.cs:221` |
| Final sampleable image | `Viewport.MainTarget.ColorImage.ImageView` | `Viewport.cs:54`, `RenderTarget.cs`, `Framebuffer.cs` |
| Existing ImGui handle (private) | `Viewport._imguiViewportTextureId` | `Viewport.cs:66` |
| Game's per-viewport window | `Viewport.DrawImGui()` | `Viewport.cs:268` (patch target) |
| Mode switch | `Viewport.SetCameraMode(CameraMode)` | `Viewport.cs:369` |
| Follow a target | `Camera.SetFollow(IFollowable, bool, bool, bool)` | `Camera.cs:~550` |
| Release | `Camera.Unfollow(bool)` | `Camera.cs:~567` |
| Fixed-cam framing | `FixedController.CameraOffset`, `.CameraRotation` | `FixedController.cs:9, 11, 18` |
| Working precedent | docking-port "Toggle Camera" | `DockingPort.cs:223-251` |
| Register ImGui texture | `ImGuiBackend.Vulkan.AddTexture(VkSampler, VkImageView, layout?)` | `ImGuiBackendVulkanImpl.cs:~1052` |
| Remove ImGui texture | `ImGuiBackend.Vulkan.RemoveTexture(ImTextureRef)` | `ImGuiBackendVulkanImpl.cs:~1060` |
| Draw in ImGui | `ImGui.Image(ImTextureRef, in float2, …)` / `ImGui.ImageWithBg(…)` | `Brutal.ImGuiApi/ImGui.cs:~6331` |
| Per-viewport shader UBO (dynamic offset, no cap) | `GlobalShaderBindings.DynamicOffset/CameraData(int)` | `GlobalShaderBindings.cs:57-80, 198` |
| KittenEva render hook (per visible viewport) | `RenderViewport` KittenEva branch | `Program.cs:3722-3733` |
| KittenEva avatar update | `KittenEva.UpdateRenderData` → `KittenRenderable.UpdateRenderData` | `KittenEva.cs`, `KittenRenderable.cs:143` |
| Cull gate | `Vehicle.GetWorldMatrix(Camera)` returns null < 1px | `Vehicle.cs:2252` |
| KittenEva access / expressions | `_renderable` → `_characterAvatar` → `CatExpressionAnim` | `kitten-eva.md`, `kitten-animations.lib` |
| `[StarMapBeforeMain]` invoke (pre-Program) | `RuntimeMod.InitializeMod` → `BeforeMainAction` | `starmap/StarMap.Core/ModRepository/RuntimeMod.cs:153`, `ModRegistry.cs:46` |
| Boot order (load game → Init mods → RunGame) | `GameSurveyer.TryLoadCoreAndGame` / `RunGame` | `starmap/StarMap.Loader/GameSurveyer.cs:26,52` |
| `[StarMapAllModsLoaded]` invoke | postfix on `ModLibrary.LoadAll` (`Program.cs:873`) | `starmap/StarMap.Core/Patches/ModLibraryPatches.cs` |
| `[StarMapImmediateLoad]` (needs `Mod` param) | prefix on `KSA.Mod.PrepareSystems` | `starmap/StarMap.Core/Patches/ModPatches.cs`, `StarMap.API/BaseAttributes.cs:72` |
| Per-frame hooks (BeforeGui/AfterGui/AfterOnFrame) | patches on `OnDrawUiFrame`/`OnDrawUiViewports`/`OnFrame` | `starmap/StarMap.Core/Patches/ProgramPatcher.cs` |

---

## 9. Risks & runtime validation checklist (do these first)

> The mod-load-vs-init **timing question is now RESOLVED** via the StarMap sources (§6.3): `[StarMapBeforeMain]` runs before `Program` is constructed, so setting `Program.ViewportCount` there is guaranteed. The items below are the remaining things to confirm against the *running* binary.

1. **`ViewportCount` actually scales the build:** set it (e.g. to 5) in `[StarMapBeforeMain]`, then in a first-frame hook log `Program.Viewports.Count`. Confirm the engine built that many and that frames still render (no broken array bound elsewhere). **Highest-impact remaining check.**
2. **Decomp vs binary drift:** confirm `Viewport.MainTarget`, `.ColorImage.ImageView`, `Viewport.DrawImGui`, `FixedController.CameraOffset/Rotation`, `ImGuiBackend.Vulkan.AddTexture`, `Program.Viewports/ViewportCount`, and the StarMap hook signatures in §6.5 exist with these names/shapes (Dbg reflection dump per `debug.md`). In particular, verify `[StarMapImmediateLoad]` requires the `KSA.Mod` parameter in the deployed StarMap build.
3. **KittenEva renders in a secondary viewport at all:** simplest possible test — make viewport 1 `Visible`, `SetCameraMode(Fixed)`, `SetFollow(kitten, …)`, with a crude offset; confirm the stock "Camera 1" window shows the kitten *and updates live* (validates §3 against the running build before we build any custom UI).
4. **Kitten body axes / head height:** tune `CameraOffset`/`CameraRotation` constants live, then bake.
5. **Texture handle lifetime:** confirm re-registering after a (rare) resize, and that `RemoveTexture` on unload doesn't crash an in-flight frame (clear the managed flag before removing, like `quad.md`'s ordering rule).
6. **VRAM & performance:** confirm the upfront VRAM cost of N pre-allocated viewport slots (§6.3) is acceptable at your chosen cap and tile size; measure per-frame cost with 1, 2, then 4+ *visible* small viewports before committing to a large cap.

---

## 10. Implementation plan (incremental, each step independently testable)

1. **Spike (no UI):** mod that, on a hotkey, takes `Viewports[1]`, sets `Visible/Fixed/SetFollow(firstKitten)` with a guessed offset, and lets the stock window render. Validates §3 + §9.3 live.
2. **Live framing:** recompute `CameraOffset/CameraRotation` each frame in `OnBeforeUi` for a stable headshot; add temporary sliders to dial in the constants.
3. **Custom panel:** self-register `AddTexture` on the viewport's `MainTarget` image; Harmony-prefix `Viewport.DrawImGui` to suppress the stock window for managed viewports; draw the tile with `ImGui.Image` in `RenderContent`. Now it's a real crew tile.
4. **Two crew members:** use both spare viewports (1 & 2). Add the round-robin scheduler so a crew list longer than the available viewports cycles refresh.
5. **Scale-up:** set `Program.ViewportCount = 1 + maxCrewSlots` in `[StarMapBeforeMain]` (§6.3/§6.4 — timing is guaranteed), confirm via §9.1, then drive one viewport per crew member (all small, only the shown ones `Visible`). Add an expression label per tile (read current expression from the kitten via `kitten-animations`/`CatExpressionAnim`).
6. **Polish + perf:** small tile resolution, optional per-managed-viewport celestial/atmosphere skip in `RenderViewport` (Harmony) for big speedups; `unscience` `ISubmod` integration; `HotkeyGuard`; clean teardown; update `REPOSITORY_INDEX.md` + per-mod `README.md`.
