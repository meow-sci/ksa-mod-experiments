# Blinky Engine Ignition Regression Research

Date: 2026-05-03

## Summary

`blinky` still builds and registers the LCD engine grid, and the render skip patch is unrelated to the failure: meshes can render even when KSA's rocket solver refuses to command thrust. The current code toggles `EngineController.SetIsActive(null, on)` per pixel, but the current decompiled KSA engine path has several extra gates before an engine actually produces plume/thrust:

1. The vehicle's main engine input must be on (`ManualControlInputs.EngineOn`).
2. The per-engine controller must be active (`EngineController.IsActive`).
3. The engine's `ResourceManager.ResourceAvailable()` must find the required reactants.
4. `FlightComputer.CommandEngineThrottles()` must write nonzero `CommandThrottle` and `CommandBurnTime`.
5. `RocketCore.UpdateState()` and `RocketNozzle.UpdateState()` must turn that into nonzero nozzle performance.

The most suspicious regression is the resource/stage gate, not rendering. Blinky creates pixel engine parts manually and connects them to fuel parts, but it does not set the new pixel parts' stage to match their fuel anchor. Current KSA engines use `FlowRule.NearestToFurtherestSameStage`, and `ResourceManager.CreateOrders()` filters tank nodes with `tank.Parent.FullPart.Stage == enginePart.FullPart.Stage`. If a recent KSA update changed default stage handling or the source vehicle's fuel stage numbers, Blinky can have a valid part tree and valid connection graph while every pixel engine reports no propellant.

The second likely issue is that Blinky does not explicitly keep the vehicle's main engine input ignited. The intended model should be: vehicle main engines stay ignited/armed, and Blinky flips the per-pixel active/enabled state. Current Blinky only flips `EngineController.IsActive` and assumes the user or RPC has successfully set `VehicleEngine.MainIgnite`.

## Current Blinky Behavior

Relevant code:

- `blinky.lib/LcdGridBuilder.cs`
	- Creates two `Part` instances per pixel.
	- Sets position, rotation, scale.
	- Manually assigns `part.TreeParent = root` and `root.TreeChildren.Add(part)`.
	- Finds fuel parts with `SubtreeModules.Get<Tank>()`.
	- Calls `Part.Connection.Connect(pixelPart, fuelPart)` round-robin across fuel anchors.
	- Rebuilds once with `vehicle.Parts = PartTree.CreateFromNewPartTree(root)`.
	- Calls `vehicle.UpdateAfterPartTreeModification()`.
	- Sets `EngineController.MinimumThrottle = 0.0001f`.

- `blinky.lib/BlinkyGridManager.cs` and `blinky.lib/ScrollAnimation.cs`
	- Pixel on/off is implemented as `controller.SetIsActive(null, on)`.
	- There is no call to `vehicle.SetEnum(VehicleEngine.MainIgnite)`.
	- There is no stage alignment between pixel engines and the connected fuel parts.

## KSA Engine Path From Decompiled Sources

Relevant KSA files under `decomp/ksa/KSA`:

- `EngineController.cs`
	- `SetIsActive` only queues `InputEvents.IActivateInputData`.
	- Applying that input sets `EngineController.IsActive = ActivationState`.
	- Save data calls this `ActiveInStage`.

- `InputEvents.cs`
	- `IActivateInputBuffer.ApplyAll()` is run from `InputEvents.ApplyInputEvents()`.
	- The buffer resizes, so a hard 500 event cap is not the immediate issue.

- `Program.cs`
	- Per frame: apply orbit/vehicle solver results, apply input events, then start the next vehicle solvers.

- `Vehicle.cs`
	- `Vehicle.SetEnum(VehicleEngine.MainIgnite)` sets private `_manualControlInputs.EngineOn = true`.
	- `Vehicle.PrepareWorker()` passes `_manualControlInputs` into the worker state.

- `FlightComputer.cs`
	- In manual mode, `outputs.EngineBurnDuration = inputs.EngineOn ? double.PositiveInfinity : 0.0`.
	- `CommandEngineThrottles()` only commands an engine when `current.Module.IsActive && current.State.IsPropellantAvailable`.

- `PartTree.cs`
	- `RecreateResourceManagers()` gives engine `RocketCore.ResourceManager.FlowRule = FlowRule.NearestToFurtherestSameStage`.

- `ResourceManager.cs`
	- `ResourceAvailable()` searches the precomputed same-stage tank order for every reactant.

- `ResourceManagerBase.cs` and `ResourceManager.cs`
	- Graph traversal uses `Part.Connections`, so Blinky's explicit `Part.Connection.Connect()` is correct.
	- Same-stage filtering is still applied after the graph is found.

- `Part.cs`
	- `new Part(...)` defaults `Stage = inInstance?.Stage ?? 0`.
	- `SetStage(int)` exists and propagates to subparts.
	- Blinky currently never calls `SetStage` or `SetSequence` for generated pixel parts.

## Ranked Root Cause Hypotheses

### 1. Pixel engines are connected to fuel but fail same-stage propellant filtering

Why this fits:

- Blinky's grid renders because render data only needs the parts/modules to exist.
- Engines can still be activated but never produce plume/thrust if `ResourceAvailable()` is false.
- Blinky already connects pixel parts to fuel parts, so the remaining fuel-flow failure mode is stage filtering.
- KSA explicitly sets engine resource flow to `NearestToFurtherestSameStage`.
- Generated parts default to stage `0`; fuel tanks on existing vehicles may be in another stage.

Fix idea:

- When assigning each pixel part to a fuel anchor, set the pixel part's stage to the anchor's stage before the `CreateFromNewPartTree()` rebuild:

```csharp
var fuelPart = fuelParts[i % fuelParts.Count];
createdParts[i].SetStage(fuelPart.Stage);
ConnectToFuel(createdParts[i], fuelPart);
```

Potential caveat: `SetStage()` resets the part's current `Tree.StageList`; before the rebuild this is the part's temporary single-part tree, so it should be okay. If this behaves oddly, add an internal helper that sets stage through `SetStage` immediately after `vehicle.Parts = PartTree.CreateFromNewPartTree(root)` and before resource managers are recreated, or fall back to resource flow override below.

Alternative for this same cause:

- After rebuilding, set each pixel `RocketCore.ResourceManager.FlowRule = FlowRule.NearestToFurtherest` to ignore stage. This is simpler but less game-native, and it may let pixel engines draw from fuel that the game would normally exclude by stage.

### 2. Blinky no longer guarantees the vehicle main engine input is ignited

Why this fits:

- KSA now clearly separates vehicle-level `EngineOn` from per-engine `IsActive`.
- Blinky toggles only `EngineController.IsActive`.
- If `EngineOn` is false, active pixel engines still receive `CommandBurnTime = 0` and will not fire.
- The user's intended model maps cleanly to: keep `EngineOn = true`, use per-engine active as the pixel enable toggle.

Fix idea:

- Add `EnsureVehicleIgnited(GridState state)` and call it before any operation that turns at least one pixel on: `StartScroll`, `StartBuiltInScroll`, `DisplayStatic`, and `ApplyPattern`.
- Implement it with public KSA API:

```csharp
private static void EnsureVehicleIgnited(Vehicle vehicle)
{
		vehicle.SetEnum(VehicleEngine.MainIgnite);
}
```

- Do not call `MainShutdown` from Blinky's `TurnOff`; leave the vehicle armed so the next pixel-on command works immediately.
- Optionally add a UI checkbox like `Keep vehicle engines ignited for grids`, default on.

Potential caveat: this does not force throttle. If the player lowered main throttle near the minimum, pixels may be dim/weak. If that is observed, either document that throttle should be up, or use the Harmony command override option below.

### 3. Activation through `SetIsActive` is too delayed or stale for the new frame ordering

Why this is less likely but plausible:

- `SetIsActive` queues input events, and Blinky currently changes pixels from StarMap UI/update hooks.
- `InputEvents.ApplyInputEvents()` happens before `Universe.ExecuteNextVehicleSolvers()` in `Program.PrepareFrame()`.
- If StarMap's `BeforeGui` hook runs after solver preparation, Blinky's pixel changes are always one frame late. That used to be harmless, but a game update may have made solver state caching less forgiving.

Fix ideas:

- Keep using `SetIsActive`, but drive animation by diffs only, not by rewriting every controller every scroll column. This reduces event volume and stale transitions.
- More robust: patch `Universe.ExecuteNextVehicleSolvers` with a Harmony prefix and apply Blinky's pending pixel states immediately before KSA prepares workers. If using `SetIsActive` there, it still waits for the next input-apply pass, so the prefix should either directly set the non-public `IsActive` setter via reflection or update a separate command override state.

### 4. Flight computer cached config is stale after Blinky changes active engines

Why this is possible:

- `FlightComputer.ReadUpdatedVehicleConfiguration()` caches active engines for total thrust/mass-flow and active thrusters.
- Part context menu activation calls it after toggling engine active state.
- Blinky does not call it after pixel active changes.

Why this is probably not the primary blocker:

- `CommandEngineThrottles()` iterates all engine modules from `FlightComputerOutput`, not only `VehicleConfig` engines.
- The cached config matters more for burn planning/TVC/summary values than for the direct active+propellant command gate.

Fix idea:

- After batches of direct active-state changes, call `vehicle.FlightComputer.ReadUpdatedVehicleConfiguration(vehicle)`. If continuing to use queued `SetIsActive`, defer the config refresh until after the input buffer has applied, or move to direct state changes in a solver-prefix hook.

## Recommended Fix Path

### Minimal first pass

1. In `LcdGridBuilder.BuildGrid`, stage-align every pixel engine with its selected fuel anchor before the `CreateFromNewPartTree()` rebuild.
2. Add a debug log after rebuild that samples a few pixel engines and prints:
	 - pixel part id
	 - pixel stage
	 - connected fuel part id/stage
	 - `ResourceManager.ResourceAvailable(vehicle.Parts.Moles.States)` for the engine's first core
3. In `BlinkyGridManager`, call `EnsureVehicleIgnited(vehicle)` before any operation that turns pixels on.
4. Do not shut down the vehicle main engine from Blinky when pixels are off.
5. Keep the existing `SetIsActive(null, on)` pixel control for now.

This path matches the user's intended design while keeping the implementation small and close to existing KSA mechanics.

### If the minimal pass still fails

Add a Harmony-driven pixel command override:

- Keep all pixel `EngineController.IsActive = true` once a grid is armed.
- Store desired pixel on/off in Blinky state instead of using `IsActive` as the desired state.
- Patch `FlightComputer.CommandEngineThrottles(ref FlightComputerOutput outputs)` with a postfix.
- For controllers whose `Parent.FullPart.Id.StartsWith("pixel_")`:
	- if desired on and propellant available: set `CommandThrottle = 1f`, `CommandBurnTime = double.PositiveInfinity`;
	- if desired off: set both command fields to zero.

Pros:

- Independent of vehicle main engine input and player throttle.
- Avoids input-buffer timing entirely.
- Pixel display becomes deterministic.

Cons:

- Patches a private KSA method.
- If all pixel engines remain `IsActive = true`, KSA's `VehicleConfig.TotalEngineVacuumThrust` may include every pixel engine and distort burn planning/TWR displays. We may need to patch `FlightComputer.ReadUpdatedVehicleConfiguration()` or accept that Blinky grids are special-effect engines.

### More invasive fallback

Stop relying on physical engine thrust for the display and drive visual exhaust/light state directly:

- Patch/update `RocketNozzleFxState`, plume data, or exhaust light for pixel nozzles based on Blinky's desired pixel state.
- Keep physics engine commands off so no fuel, thrust, staging, or vehicle throttle is involved.

This is the most stable display architecture long term, but it requires more reverse engineering of KSA's render/fx state path.

## Runtime Debug Checklist

Add a temporary Blinky debug button or log block for one grid cell after building and after pressing All On:

1. Confirm `PixelGrid.RefreshEngineControllers` finds controllers.
2. For a sampled pixel controller:
	 - `controller.IsActive`
	 - `controller.Parent.FullPart.Stage`
	 - `controller.Cores[0].ResourceManager.FlowRule`
	 - `controller.Cores[0].ResourceManager.ResourceAvailable(vehicle.Parts.Moles.States)`
	 - `vehicle.Parts.States` engine global state: `IsAnyActive`, `IsAnyPropellantAvailable`
3. For the connected fuel anchor:
	 - fuel part id and `Stage`
	 - tank module count
	 - relevant mole masses are nonzero
4. Confirm vehicle main engine input:
	 - call `vehicle.SetEnum(VehicleEngine.MainIgnite)` and then check whether nozzles produce nonzero `RocketNozzleState.Performance.TotalThrust` after the next solver result.
5. If stage differs between pixel and fuel, test `pixelPart.SetStage(fuelPart.Stage)` plus rebuild/recompute.
6. If propellant is available but command throttle remains zero, test explicit `vehicle.SetEnum(VehicleEngine.MainIgnite)` and then the Harmony command override.

## Implementation Notes

- Keep resource connection cleanup in `DestroyGrid`; Blinky already disconnects `part.Connections` before unlinking parts.
- If changing stage or flow rule, do it before or immediately after the `PartTree.CreateFromNewPartTree()` rebuild and before relying on `ResourceManager` orders.
- Preserve the render-skip patch; it only hides meshes and should not affect physics.
- Avoid making non-LCD engines active. `NonLcdEngineCache` correctly excludes `pixel_` parts, and its warning still makes sense.
- After any code change, update `blinky/README.md`, `REPOSITORY_INDEX.md` if behavior changes, and run `dotnet build` per repo instructions.
