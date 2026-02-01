This project is a testbed for KSA (Kitten Space Agency) game mods.

# camera-controller-override mod

## overview

This mod is meant to host test and experiment code to learn how to manipulate the game camera system.

The game camera system has a `Controller` base class where the current active instance is 
accessible via `KSA.Program.OnFrameViewport.GetActiveController()`

The active `Controller` is responsible for controlling the camera.

References for `Controller` are found at:
- `docs\ksa\decompiled\Controller.cs` - decompiled base class
- `docs\ksa\decompiled\OrbitController.cs` - decompiled orbit follow mode controller class
- `docs\ksa\decompiled\FlyController.cs` - decompiled free camera controller class
- `docs\ksa\decompiled\Viewport.cs` - decompiled Viewport class which controls the active camera controller
- `docs\analysis\KSA_CAMERA_ANALYSIS.md` - an analysis of how the controller system works

## features

### common info

- `camera-controller-override\Mod.cs` contains general mod code
- `Mod.OnAfterUi` is where ImGui UI code should be placed
- use ImGui UI code to place text, buttons, and other widgets to trigger mod testing code

### simple movement (via Controller patching)

- requires an ImGui window collapsing section "simple movement"
- requires an ImGui button "patch controller" in the mod window to trigger
    - the button should toggle the runtime patching on/off
- use Harmony to runtime patch the current `Controller` 
    - statically accessible at `KSA.Program.OnFrameViewport.GetActiveController()`
    - patch the `OnFrame` instance method to our custom code
- the patch method `PatchedOnFrame` should determine the current camera conditions and then perform a very simple 
  5 second animation where it moves the camera away from the target linearlly at a configurable speed (default to 1 meter per second)
- when the animation is done the runtime patching should be disabled
- the animation completion and toggle button should share the same enablement boolean so the toggle button can cancel the patching at any time
- the animation length can be changed via an ImGui slider in seconds with min = 1s and max = 30s
- the camera move speed should be changed by a ImGui slider in m/s with min = 1 and max = 250
- animation progress bar always visible so the UI doesnt change heght when starting
- add a boolean UI toggle/checkbox (enabled by default) for "lerp back to start"
    - remember the starting camera position before changing any animation
    - after animation is done, lerp back to the original position from the current
    - use an ease-in-out to make the lerp smooth
    - lerp for 3 seconds

### simple movement (via custom Controller)

TBD: DONT USE THIS YET

- requires an ImGui button in the mod window to trigger
- create a custom Controller class which will implement 
- use Harmony to runtime patch the current `Viewport` 
    - statically accessible at `KSA.Program.Viewports[KSA.Program._onFrameViewportIndex]`
    - patch the `Viewport.GetActiveController` instance method 
