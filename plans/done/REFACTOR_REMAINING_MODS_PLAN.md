# mods remaining for refactor

- camera-controller-override
- geeforce
- kitten-animations

# task description

these mods have not yet been refactored to

- have all their functionality refactored into an ISubmod (from ksa-abstractions.lib), where all functionality, including ImGui content (not the window, but the window contents) is contained in the mod.lib cspproj and then reused from the mod project to put it into a standalone ImGui window for just that mod and inside unscience for being one of many submods living in collapsible headers in one ImGui window

make a detailed plan of tasks with deep detail so that future ai coding subagents will have enough unabiguous information as to perform these refactors correctly and accurately and efficiently

# reference: ISubmod interface (ksa-abstractions.lib/ISubmod.cs)

```csharp
public interface ISubmod
{
    string Name { get; }
    void Initialize();
    void Update(double dt);
    void RenderContent(); // renders ImGui content WITHOUT Begin/End window framing
    void Dispose();
}
```

# reference: already-refactored standalone mod pattern (e.g. glass/Mod.cs)

A refactored standalone mod is a thin shell that:
1. Creates one `XxxSubmod` instance (from the .lib project)
2. Calls `Patcher.Patch()` and `submod.Initialize()` in `OnFullyLoaded()`
3. Calls `submod.Update(dt)` in `OnBeforeUi()`
4. Toggles window visibility on a key press in `OnAfterUi()`, calls `RenderWindow()` when visible
5. `RenderWindow()` does `ImGui.Begin(...)` → `submod.RenderContent()` → `ImGui.End()`
6. Calls `submod.Dispose()` and `Patcher.Unload()` in `Unload()`

# reference: unscience integration pattern (unscience/Mod.cs)

Unscience creates all submod instances in `OnFullyLoaded()`, adds them to `_submods` list, calls `Initialize()` on each, then `Patcher.Patch()`. In `OnBeforeUi()` it calls `Update(dt)` on all submods. In `OnAfterUi()` it renders each visible submod's `RenderContent()` inside a `CollapsingHeader`. Unscience's `Patcher.cs` calls each lib's `XxxPatches.Apply(_harmony)` / `Remove(_harmony)` for any lib that has Harmony patches.

# reference: lib Harmony patches pattern (e.g. glass.lib/GlassPatches.cs, blinky.lib/BlinkyPatches.cs)

Libs that need Harmony patches expose a static class with:
- A static `Apply(Harmony harmony)` method that manually patches specific methods
- A static `Remove(Harmony harmony)` method that unpatches them
- Static state properties/fields that the patch methods reference (set by the caller before Apply)

This allows both the standalone mod's Patcher and unscience's Patcher to share the same patch logic.

---

# plan

Each mod refactor follows 3 phases: (A) create ISubmod in the .lib, (B) refactor the standalone mod to use it, (C) wire into unscience. All 3 mods can be done independently. After all 3 are done, do phase (D) to update docs.

Verify compilation with `dotnet build` after completing each mod's refactor.

---

## mod 1: geeforce

### current state

- `geeforce.lib/` contains `GForceRecorder.cs` (sampling/recording logic) and `GForceUI.cs` (static class whose `Render()` method calls `ImGui.Begin/End` internally to create its own window and render all content)
- `geeforce/Mod.cs` owns the sampling loop (accumulator, interval, recorder creation), calls `GForceUI.Render(ref visible, recorder, sampleInterval)` which manages its own window
- `geeforce/Patcher.cs` has a Harmony patcher with `PatchAll` but NO actual `[HarmonyPatch]` decorated methods — it's effectively a no-op

### A) create GeeForceSubmod in geeforce.lib

#### A1) refactor GForceUI.cs — split window from content

The existing `GForceUI.Render(ref bool visible, GForceRecorder recorder, double sampleIntervalSec)` method currently does:
1. `ImGui.SetNextWindowSize(...)` + `ImGui.Begin(title, ref visible, ...)` — **window framing**
2. Stats row, per-axis readout, graph, scrub slider, controls — **content**
3. `ImGui.End()` — **window framing**

**Refactor:** Extract steps (2) into a new public static method `RenderContent(GForceRecorder recorder, double sampleIntervalSec)` that renders ONLY the inner content (no Begin/End). Keep the existing `Render()` method as a convenience wrapper that calls Begin → RenderContent → End, so the standalone mod can still use it if desired.

Specifically:
- Add new method: `public static void RenderContent(GForceRecorder recorder, double sampleIntervalSec)` containing the body of Render between Begin and End (stats row through DrawControls)
- Modify existing `Render()` to call `RenderContent(recorder, sampleIntervalSec)` between its Begin/End

#### A2) create geeforce.lib/GeeForceSubmod.cs

Create a new file `geeforce.lib/GeeForceSubmod.cs` implementing `ISubmod`:

```csharp
namespace MeowSci.GeeForceLib;

public sealed class GeeForceSubmod : ISubmod
{
    public string Name => "G-Force Monitor";

    private const double SampleIntervalSec = 0.025; // 25ms → 40 Hz
    private double _accumulator;
    private GForceRecorder _recorder = null!;

    public void Initialize()
    {
        int capacity = GForceUI.GetRequiredCapacity(SampleIntervalSec);
        _recorder = new GForceRecorder(capacity, SampleIntervalSec);
    }

    public void Update(double dt)
    {
        _accumulator += dt;
        while (_accumulator >= SampleIntervalSec)
        {
            _accumulator -= SampleIntervalSec;
            var vehicle = VehicleProvider.GetControlledVehicle();
            if (vehicle != null)
            {
                double simTime = SimTimeProvider.GetElapsedTime().Seconds();
                _recorder.RecordSample(vehicle, simTime);
            }
        }
    }

    public void RenderContent()
    {
        GForceUI.RenderContent(_recorder, SampleIntervalSec);
    }

    public void Dispose() { }
}
```

Key points:
- Moves sampling logic (accumulator, interval, recorder creation) OUT of `geeforce/Mod.cs` into the submod
- Uses `GForceUI.RenderContent()` (the new split method) so no window framing in the submod
- Uses `VehicleProvider` and `SimTimeProvider` from `ksa-abstractions.lib` (already referenced by geeforce.lib)
- Needs `using MeowSci.KsaAbstractions;` for VehicleProvider/SimTimeProvider
- Needs `using Brutal.ImGuiApi;` is NOT needed (GForceUI handles ImGui calls)

### B) refactor geeforce/Mod.cs

Replace the current `Mod.cs` with the thin-shell pattern:

```csharp
using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.GeeForceLib;

namespace MeowSci.GeeForce;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private GeeForceSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new GeeForceSubmod();
            Patcher.Patch();
            _submod.Initialize();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"geeforce: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        _submod.Update(dt);
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            if (ImGui.IsKeyPressed(ImGuiKey.F11)) _windowVisible = !_windowVisible;
            if (_windowVisible) RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"geeforce: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod.Dispose();
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"geeforce: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(520, 440), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("G-Force Monitor", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
```

Key changes from current:
- Remove all sampling fields (`_accumulator`, `SampleIntervalSec`, `_recorder`) — now in submod
- Remove `using KSA;` and `using MeowSci.KsaAbstractions;` — no longer needed in Mod.cs
- Move sampling from `OnAfterUi` to `OnBeforeUi` via `_submod.Update(dt)` (matches the standard pattern; the original had sampling in OnAfterUi which is non-standard)
- `RenderWindow()` uses `ImGui.Begin/End` and delegates to `_submod.RenderContent()`

### C) wire into unscience

**unscience/Mod.cs changes:**
1. Add `using MeowSci.GeeForceLib;` to the imports
2. In `OnFullyLoaded()`, add `_submods.Add(new GeeForceSubmod());` in the desired display order (after existing submods, or alphabetically — place between `GlassSubmod` and `IFeelSeenSubmod` to maintain alphabetical order by mod name)

**unscience/Patcher.cs changes:** None needed — geeforce has no Harmony patches that need to be shared.

---

## mod 2: kitten-animations

### current state

- `kitten-animations.lib/` contains `KittenAnimationController.cs` (expression state, timers, animation playback) and `KittenAvatarAccessor.cs` (reflection-based avatar access)
- `kitten-animations/Mod.cs` owns a `KittenAnimationController` instance, calls `_animController.Update(dt, avatar)` in `OnBeforeUi`, and has ALL ImGui UI code (~80 lines of buttons for MMU animations, expressions, walking) directly in `RenderWindow()`
- `kitten-animations/Patcher.cs` has a Harmony patcher with `PatchAll` but NO actual `[HarmonyPatch]` decorated methods — it's effectively a no-op

### A) create KittenAnimationsSubmod in kitten-animations.lib

#### A1) create kitten-animations.lib/KittenAnimationsSubmod.cs

Create a new file implementing `ISubmod`. This class absorbs:
- The `KittenAnimationController` instance (currently in Mod.cs)
- The `Update(dt)` logic that calls `_animController.Update(dt, avatar)`
- ALL the ImGui UI code from `Mod.cs.RenderWindow()` (the content between Begin/End)

```csharp
using System;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

public sealed class KittenAnimationsSubmod : ISubmod
{
    public string Name => "Kitten Animations";

    private KittenAnimationController _animController = new();

    public void Initialize() { }

    public void Update(double dt)
    {
        var avatar = KittenAvatarAccessor.GetKittenAvatar();
        _animController.Update(dt, avatar);
    }

    public void RenderContent()
    {
        var avatar = KittenAvatarAccessor.GetKittenAvatar();
        if (avatar == null) return;

        // --- MMU Animations section ---
        if (ImGui.CollapsingHeader("MMU Animations"))
        {
            // Buttons: Idle Default, Move Left/Right/Forward/Backward/Up/Down
            // Each calls KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.XxxAnim)
        }

        // --- Expressions section ---
        if (ImGui.CollapsingHeader("Expressions"))
        {
            // Duration slider bound to _animController.ExpressionDuration
            // Buttons: Angry, Awe, Happy, Sad, Scared
            // Each picks random anim from avatar.Expressions.Xxx list and calls _animController.TriggerExpression(...)
        }

        // --- Walking Animations section ---
        if (ImGui.CollapsingHeader("Walking Animations"))
        {
            // Buttons: Running, Walking
            // Each calls KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.WalkingAnimations.XxxAnim)
        }
    }

    public void Dispose() { }
}
```

**Critical detail:** Copy the EXACT ImGui code from `kitten-animations/Mod.cs` lines inside `RenderWindow()` between `ImGui.Begin(...)` and the final Close button / `ImGui.End()`. Specifically:
- The `var avatar = KittenAvatarAccessor.GetKittenAvatar();` and `if (null != avatar)` guard
- The MMU Animations CollapsingHeader with 7 buttons
- The Expressions CollapsingHeader with duration slider + 5 expression buttons
- The Walking Animations CollapsingHeader with 2 buttons
- Do NOT include the header text (`ImGui.TextColored(...)` and first `ImGui.Separator()`) — those are window-level decoration
- Do NOT include the Close button — that's window-level

**Required usings:**
- `using Brutal.ImGuiApi;` (for ImGui calls)
- `using KSA;` (for CharacterAvatar, AnimationAssetRef)
- `using MeowSci.KsaAbstractions;` (already available via project reference — for ISubmod)

**Note:** kitten-animations.lib.csproj already has the Brutal.ImGui reference, so no csproj changes needed.

### B) refactor kitten-animations/Mod.cs

Replace with the thin-shell pattern (same structure as glass/Mod.cs):

```csharp
using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.KittenAnimationsLib;

namespace MeowSci.KittenAnimations;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private KittenAnimationsSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new KittenAnimationsSubmod();
            Patcher.Patch();
            _submod.Initialize();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        _submod.Update(dt);
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            if (ImGui.IsKeyPressed(ImGuiKey.F11)) _windowVisible = !_windowVisible;
            if (_windowVisible) RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod.Dispose();
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Kitten Animations", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
```

Key changes from current:
- Remove `KittenAnimationController _animController` field — now in submod
- Remove `using KSA;` — no longer needed
- Remove the inline `OnBeforeUi` logic — replaced by `_submod.Update(dt)`
- Remove ~80 lines of inline ImGui UI code — replaced by `_submod.RenderContent()`

### C) wire into unscience

**unscience/Mod.cs changes:**
1. Add `using MeowSci.KittenAnimationsLib;` to imports
2. In `OnFullyLoaded()`, add `_submods.Add(new KittenAnimationsSubmod());` (place after KiwisMarblesSubmod alphabetically)

**unscience/Patcher.cs changes:** None needed — kitten-animations has no Harmony patches that need to be shared.

---

## mod 3: camera-controller-override

This is the most complex refactor because it has:
- A large amount of UI state (30+ config fields for 8 animation types)
- Active Harmony patches that intercept camera controller OnFrame methods
- The patches need a reference to the `KeyframeSequencePlayer` instance

### current state

- `camera-controller-override.lib/` contains the animation framework: `IKeyframeAnimation`, `KeyframeSequencePlayer`, `AnimationHelpers`, 8 animation classes, and `KeyframeSequencePanel` (UI for the sequence timeline)
- `camera-controller-override/Mod.cs` has ALL the ImGui UI code (~700 lines) with 30+ float/int config fields for each animation type, sliders, "Add to Sequence" buttons, plus the window wrapper
- `camera-controller-override/Patcher.cs` has active Harmony patches on `OrbitController.OnFrame` and `FlyController.OnFrame` that check `_sequencePlayer.State` and call `_sequencePlayer.Update(controller, transform, deltaTime)`. The `SequencePlayer` is a static field in Patcher.

### A) create submod and patches in camera-controller-override.lib

#### A1) create camera-controller-override.lib/CameraControllerOverridePatches.cs

Create a Harmony patches class following the Apply/Remove pattern used by other libs (e.g., `glass.lib/GlassPatches.cs`, `blinky.lib/BlinkyPatches.cs`).

```csharp
using System;
using System.Reflection;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.CameraControllerOverrideLib.Animation;

namespace MeowSci.CameraControllerOverrideLib;

public static class CameraControllerOverridePatches
{
    private static KeyframeSequencePlayer? _sequencePlayer;

    /// <summary>Must be set before calling Apply(). The submod's SequencePlayer instance.</summary>
    public static KeyframeSequencePlayer? SequencePlayer
    {
        get => _sequencePlayer;
        set => _sequencePlayer = value;
    }

    public static void Apply(Harmony harmony)
    {
        // Patch OrbitController.OnFrame prefix
        var orbitOnFrame = typeof(OrbitController).GetMethod("OnFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var flyOnFrame = typeof(FlyController).GetMethod("OnFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var prefix = typeof(CameraControllerOverridePatches).GetMethod(nameof(OnFramePrefix), BindingFlags.Static | BindingFlags.NonPublic);

        if (orbitOnFrame != null)
            harmony.Patch(orbitOnFrame, prefix: new HarmonyMethod(prefix));
        if (flyOnFrame != null)
            harmony.Patch(flyOnFrame, prefix: new HarmonyMethod(prefix));
    }

    public static void Remove(Harmony harmony)
    {
        harmony.UnpatchAll(harmony.Id);
    }

    private static bool OnFramePrefix(Controller __instance, double inDeltaTime, Transform3D ___Transform)
    {
        try
        {
            if (_sequencePlayer != null && _sequencePlayer.State == PlaybackState.Playing)
            {
                bool shouldSkip = _sequencePlayer.Update(__instance, ___Transform, inDeltaTime);
                return !shouldSkip;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error in prefix: {ex.Message}");
            return true;
        }
    }
}
```

**Key points:**
- The `SequencePlayer` property is set externally before `Apply()` is called (by the submod or caller)
- `Apply(Harmony)` manually patches both controllers (matches pattern of other lib patches)
- `Remove(Harmony)` unpatches
- The prefix method signature uses `Controller __instance` (base class of both OrbitController and FlyController), `double inDeltaTime` (the parameter), and `Transform3D ___Transform` (private field injected by Harmony's triple-underscore convention)

**csproj change needed:** `camera-controller-override.lib.csproj` must add `Lib.Harmony` package reference:
```xml
<PackageReference Include="Lib.Harmony" Version="2.4.2" PrivateAssets="all" />
```

#### A2) create camera-controller-override.lib/CameraControllerOverrideSubmod.cs

Create the ISubmod implementation. This is the largest submod because it absorbs ~30 config fields and ~700 lines of ImGui rendering from `Mod.cs`.

```csharp
using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.CameraControllerOverrideLib.Animation.Animations;
using MeowSci.CameraControllerOverrideLib.UI;

namespace MeowSci.CameraControllerOverrideLib;

public sealed class CameraControllerOverrideSubmod : ISubmod
{
    public string Name => "Camera Controller Override";

    private KeyframeSequencePlayer _sequencePlayer = new();

    /// <summary>Exposes the SequencePlayer so patch wiring can reference it.</summary>
    public KeyframeSequencePlayer SequencePlayer => _sequencePlayer;

    // All 30+ config fields for 8 animation types — copy EXACTLY from camera-controller-override/Mod.cs fields:
    // Zoom Out: _zoomOutSpeed, _zoomOutDuration, _zoomOutEasing, _zoomOutEasingPowerStart, _zoomOutEasingPowerEnd
    // Zoom In: _zoomInSpeed, _zoomInDuration, _zoomInEasing, _zoomInEasingPowerStart, _zoomInEasingPowerEnd
    // Zoom In To Offset: _zoomInOffsetSpeed, _zoomInOffsetDuration, _zoomInOffsetEasing, _zoomInOffsetEasingPowerStart, _zoomInOffsetEasingPowerEnd, _zoomInOffsetX/Y/Z
    // Spiral Zoom In: _spiralZoomInSpeed, _spiralZoomInDuration, _spiralZoomInEasing, _spiralZoomInEasingPowerStart/End, _spiralZoomInDegrees
    // Orbit: _orbitDegrees, _orbitDuration, _orbitEasing, _orbitEasingPowerStart/End
    // Loopy Orbit: _loopyOrbitDegrees, _loopyLoopInterval, _loopyAmplitude, _loopyDuration, _loopyEasing, _loopyEasingPowerStart/End
    // Shake: _shakeDuration, _shakeCount, _shakeAmplitude, _shakeSpeed, _shakeEasing, _shakeEasingPowerStart/End
    // Spiral Zoom Out: _spiralZoomOutSpeed, _spiralZoomOutDuration, _spiralZoomOutEasing, _spiralZoomOutEasingPowerStart/End, _spiralZoomOutDegrees

    public void Initialize() { }

    public void Update(double dt) { }

    public void RenderContent()
    {
        // Copy the EXACT ImGui code from camera-controller-override/Mod.cs RenderWindow() between:
        //   if (ImGui.Begin(...)) { ... }
        // Specifically, copy everything AFTER the Begin block opening brace and BEFORE the final ImGui.End()
        //
        // Replace all references to `Patcher.SequencePlayer` with `_sequencePlayer`
        //
        // Include:
        //   - Header text + separator
        //   - Zoom Out Animation CollapsingHeader (slider, combo, easing power, add button)
        //   - Zoom In Animation CollapsingHeader
        //   - Zoom In To Offset CollapsingHeader
        //   - Spiral Zoom In CollapsingHeader
        //   - Shake Animation CollapsingHeader
        //   - Orbit Animation CollapsingHeader
        //   - Loopy Orbit Animation CollapsingHeader
        //   - Spiral Zoom Out Animation CollapsingHeader
        //   - Keyframe Sequence CollapsingHeader (calls KeyframeSequencePanel.Render(_sequencePlayer))
        //
        // Do NOT include:
        //   - ImGui.SetNextWindowSize
        //   - ImGui.Begin / ImGui.End
        //   - The Close button at the bottom
    }

    public void Dispose() { }
}
```

**Note on file size:** This submod file will be ~600-700 lines due to the extensive UI. This is acceptable — the 300-line soft limit can be exceeded when it makes sense, and this is all cohesive UI configuration for one animation system. If desired, the rendering could be split into a separate static `CameraControllerOverrideUI.RenderContent(submod)` helper class, but this is optional.

#### A3) alternative: split UI rendering into helper (optional, recommended if agent prefers)

If the submod file would exceed ~500 lines, create a helper:
- `camera-controller-override.lib/UI/CameraControllerOverrideUI.cs` — static class with `public static void RenderContent(CameraControllerOverrideSubmod submod)` containing all the ImGui rendering
- The submod's `RenderContent()` would simply call `CameraControllerOverrideUI.RenderContent(this)`
- The config fields on the submod would need to be `internal` or `public` for the UI helper to access them

### B) refactor camera-controller-override/Mod.cs

Replace with the thin-shell pattern:

```csharp
using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.CameraControllerOverrideLib;

namespace MeowSci.CameraControllerOverride;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    private CameraControllerOverrideSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            _submod = new CameraControllerOverrideSubmod();
            _submod.Initialize();
            Patcher.Patch(_submod.SequencePlayer);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        _submod.Update(dt);
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;
            if (ImGui.IsKeyPressed(ImGuiKey.F11)) _windowVisible = !_windowVisible;
            if (_windowVisible) RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _submod.Dispose();
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Camera Controller Override", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
```

Key changes:
- Remove ALL 30+ config fields — now in submod
- Remove all `using` statements for Animation/Animations namespaces — no longer needed
- `Patcher.Patch()` becomes `Patcher.Patch(_submod.SequencePlayer)` — pass the player to the patcher

### B2) refactor camera-controller-override/Patcher.cs

Simplify to delegate to the lib's patches:

```csharp
using System;
using HarmonyLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;

namespace MeowSci.CameraControllerOverride;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch(KeyframeSequencePlayer sequencePlayer)
    {
        try
        {
            _harmony = new Harmony("camera-controller-override");
            CameraControllerOverridePatches.SequencePlayer = sequencePlayer;
            CameraControllerOverridePatches.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
                CameraControllerOverridePatches.Remove(_harmony);
            _harmony = null;
            CameraControllerOverridePatches.SequencePlayer = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error removing patches: {ex.Message}");
        }
    }
}
```

Key changes:
- `Patch()` now takes `KeyframeSequencePlayer sequencePlayer` parameter
- Delegates to `CameraControllerOverridePatches.Apply/Remove` in the .lib
- No more `[HarmonyPatch]` attribute on the class, no more `PatchAll`, no more inline patch methods

### C) wire into unscience

**unscience/Mod.cs changes:**
1. Add `using MeowSci.CameraControllerOverrideLib;` to imports
2. In `OnFullyLoaded()`:
   - Create the submod: `var cameraOverride = new CameraControllerOverrideSubmod();`
   - Add to list: `_submods.Add(cameraOverride);` (place after BlinkySubmod to maintain roughly alphabetical order)
   - The submod exposes `SequencePlayer` which the unscience Patcher needs

**unscience/Patcher.cs changes:**
1. Add `using MeowSci.CameraControllerOverrideLib;` to imports
2. Add a `SequencePlayer` property: `public static KeyframeSequencePlayer? CameraSequencePlayer { private get; set; }`
3. In `Patch()`: set `CameraControllerOverridePatches.SequencePlayer = CameraSequencePlayer;` then call `CameraControllerOverridePatches.Apply(_harmony);`
4. In `Unload()`: call `CameraControllerOverridePatches.Remove(_harmony);` and null out the property

**unscience/Mod.cs OnFullyLoaded() wiring:**
After creating cameraOverride submod and before calling Patcher.Patch():
```csharp
Patcher.CameraSequencePlayer = cameraOverride.SequencePlayer;
```

---

## D) documentation updates (after all 3 mods are done)

### D1) update REPOSITORY_INDEX.md

Add entries for the 3 new submods under the appropriate sections:
- `camera-controller-override.lib` — note it now provides `CameraControllerOverrideSubmod` and `CameraControllerOverridePatches`
- `geeforce.lib` — note it now provides `GeeForceSubmod`
- `kitten-animations.lib` — note it now provides `KittenAnimationsSubmod`

Update the unscience mod entry to list 13 submods (was 10).

### D2) update unscience/README.md

Add the 3 new submods to unscience's README feature list.

### D3) update individual mod READMEs

Update each mod's README.md to reflect the new architecture (ISubmod pattern, shared .lib code).

---

## verification checklist

After all changes:
1. `dotnet build` succeeds for the entire solution
2. Each standalone mod (camera-controller-override, geeforce, kitten-animations) still works independently with its own ImGui window
3. Unscience includes all 3 new submods in collapsible headers
4. Camera-controller-override Harmony patches work from both standalone and unscience contexts
5. No code duplication — all ImGui content and business logic lives exclusively in the .lib projects
