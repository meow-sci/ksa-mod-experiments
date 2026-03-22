# Kiwi's Marbles

A KSA mod for repositioning celestial bodies (planets, moons) by "welding" them to follow other celestial bodies or vehicles at user-defined offsets.

## Overview

Kiwi's Marbles lets you attach a planet or moon to any orbiter (another celestial body or vehicle). Once welded, the source body is teleported on every game tick to maintain its position relative to the target — effectively overriding physics for that body. Multiple welds are supported and processed in dependency order via topological sort.

Toggle the window with **F9**.

## Features

- **Celestial welding**: Weld any planet or moon to any other orbiter (celestial or vehicle)
- **Offset in CCI frame**: Specify an XYZ offset in the CCI (inertial) frame of the target's parent body
- **Unit scale selector**: Enter offsets in m / km / Mm / Gm for convenience; computed double-precision offset is displayed
- **Live offset editing**: Adjust the weld offset in real-time from the active welds panel, with a per-weld unit selector
- **Cross-parent welding**: Source body's parent automatically changes via `SetOrbit()` when target has a different parent
- **Multiple welds**: Create as many welds as needed; processed in topological order so weld chains work correctly
- **Unweld**: Remove any active weld instantly

## Usage

1. Press **F9** to open the Kiwi's Marbles window.
2. Choose a **Source** (the planet/moon to move) from the first dropdown.
3. Choose a **Target** (anything it should follow — another planet, moon, or vehicle).
4. Enter an **offset** (X / Y / Z) and pick a scale unit (m / km / Mm / Gm).
5. Click **Create Weld**. The source body will immediately begin following the target.
6. Use the **Active Welds** panel to adjust the offset in real-time or click **Unweld** to detach.

### Offset Conventions

- Offsets are in the **CCI (Celestial-Centered Inertial)** frame of the target's parent body.
- X ≈ along the major axis (roughly sunward/anti-sunward), Y and Z are transverse.
- Planetary distances are typically millions to billions of meters — use Mm or Gm units.
- Example: offset `(384.4, 0, 0) km` ≈ Moon–Earth distance.

## Architecture

| Component | Purpose |
|-----------|---------|
| `kiwis-marbles/Mod.cs` | ImGui UI: create/manage welds, per-frame update loop |
| `kiwis-marbles.lib/CelestialWeldEntry.cs` | Data class: Source (Celestial), Target (IOrbiter), Offset (double3) |
| `kiwis-marbles.lib/CelestialWeldEngine.cs` | Per-frame repositioning via `SetOrbit` + `UpdatePerFrameData`; topological sort |
| `ksa-abstractions.lib/CelestialProvider.cs` | `GetAllCelestials()` and `GetAllOrbiters()` from `Universe.CurrentSystem` |

## Key Game APIs

- `Celestial.SetOrbit(Orbit)` — replaces orbit and auto-re-parents via `SetParent()`
- `Celestial.UpdatePerFrameData()` — refreshes cached CCI/CCE position and transform data
- `Orbit.CreateFromStateCci(parent, time, posCci, velCci, color)` — creates new orbit from state vectors
- `CelestialSystem.All.GetList()` — returns all `Astronomical` objects (filter with `OfType<Celestial>()`)

## Notes

- Stars (`StellarBody`) cannot be sources — they have no orbit and always sit at origin.
- Source body's children (moons of the moved planet) automatically follow since their orbits are defined relative to their parent.
- Weld chains (Moon → Earth → Mars) work correctly: the engine sorts welds topologically so Earth is moved before Moon's weld is applied.
- Welds are not persisted across mod reloads.
