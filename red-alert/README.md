# Red Alert — Vehicle Action Plans

Red Alert builds reusable **action plans** that bundle one-click vehicle actions across light parts and solar panels. Press one button and a whole list of "turn that on, deploy that, change that color" actions fires in order.

Available as both:

- a **standalone mod** (F11 toggle window)
- an **unscience submod** rendered inside the Unscience Toolbox window

## Concepts

- **Action plan** — a named container holding any number of actions. The plan has a single **Engage** button that runs every action it contains, in order.
- **Action** — `vehicle + part + action type` (+ optional color or actuate value, depending on type).

## Supported actions

Per-part actions are filtered by what the part actually supports.

| Part capability                   | Actions available                                                |
|-----------------------------------|------------------------------------------------------------------|
| Light on/off (has `LightSwitch`)  | Light on, Light off, Light toggle                                |
| Light color (has `LightModule`)   | Light color (RGBA)                                               |
| Light animation (`KeyframeAnimationModule`, no deploy/retract) | Light animate (actuate 0..1)         |
| Solar panel deploy/retract        | Solar deploy, Solar retract, Solar toggle                        |
| Solar panel actuate (continuous)  | Solar animate (actuate 0..1)                                     |

The scanner walks each vehicle's part tree and inspects each part's `Template.Components` (for lights), `LightSwitch` (for on/off), and the part subtree's `SolarPanel` and `KeyframeAnimationModule` (for solar deploy and continuous actuate). The action picker only offers actions the selected part supports, so you can't add a "Solar deploy" action to a light part.

## Example workflow

1. Open the panel (F11 in standalone, or Unscience → Red Alert).
2. Type `battle stations` into **Plan Name**, click **Create Plan**.
3. Inside the new plan, in the **Add Action** section:
   - pick a Vehicle, then a Part (the part list shows what each part can do).
   - pick an Action Type appropriate to that part.
   - if it's a Color or Actuate action, set the color / value.
   - click **Add Action**.
4. Repeat to add as many actions to the plan as you want.
5. Click **Engage** at the top of the plan to run the whole list.

## Architecture

| File                      | Purpose                                                                  |
|---------------------------|--------------------------------------------------------------------------|
| `Mod.cs`                  | Standalone StarMap entry — F11 toggle, hosts `RedAlertSubmod` UI         |
| `Patcher.cs`              | Harmony placeholder + `HotkeyGuard` patch                                |
| `red-alert.lib/ActionTypes.cs`     | `ActionType`, `PartCapability`, `ActionablePart`, `PlannedAction`, `ActionPlan` |
| `red-alert.lib/ActionScanner.cs`   | Discovers actionable parts on a vehicle                          |
| `red-alert.lib/LightActions.cs`    | Internal reflection helpers for light color + on/off             |
| `red-alert.lib/ActionExecutor.cs`  | Resolves a `PlannedAction` to a live part and executes it        |
| `red-alert.lib/RedAlertSubmod.cs`  | `ISubmod` ImGui UI (create-plan form, plan list, add-action form) |

The lib has no UI dependency on the standalone mod, so it can be reused by other mods (e.g. an aggregate supermod or an RPC endpoint).

## Notes

- Light intensity, color, and `LightModule` access uses reflection (same approach as `zippo.lib`) because the `KSA.LightModule` types are not part of the public API surface.
- Solar panel deploy/retract and continuous actuate are driven by directly setting `KeyframeAnimationModule.TimeGoal`. The KSA solver handles the actual interpolation each frame.
- "Toggle" actions read the current state at engage time, so the same plan can flip-flop on each engage.
