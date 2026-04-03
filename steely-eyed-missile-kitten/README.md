# steely-eyed-missile-kitten

Mission monitoring, event detection, and achievement tracking mod for Kitten Space Agency.

## Features

- **Passive Telemetry Monitoring** — Samples all vehicles at a configurable rate (default 0.5s / 2 Hz). Monitors altitude, speed (orbital/surface/inertial), orbital parameters, g-forces, mass, and more.
- **Flight Event Detection** — Automatically detects and records key flight events:
  - SOI transitions (entering/leaving a body''s sphere of influence)
  - Liftoff and landing
  - Splashdown in ocean
  - Atmosphere entry/exit
  - Stable orbit achieved
  - Escape trajectory (orbit escape)
- **YAML Mission System** — Define missions with flexible condition trees that evaluate against live telemetry. Missions support:
  - Altitude/speed/orbital threshold conditions
  - Event-based conditions (did this event occur?)
  - Location conditions (in SOI of, landed on surface of)
  - Composite conditions: all_of, any_of, sequence (ordered steps)
- **SQLite Persistence** — All flight events and mission progress are saved to a local SQLite database at ``Documents/My Games/Kitten Space Agency/.steely-eyed-missile-kitten/events.db``
- **F11 ImGui Window** with three tabs:
  - **Telemetry** — Live vehicle data table with configurable sample interval
  - **Events** — Color-coded scrolling event feed with filtering
  - **Missions** — Mission activation, progress tracking, and abandonment

## Usage

Press **F11** to open/close the Steely-Eyed Missile Kitten window.

### Telemetry Tab
View live telemetry for all active vehicles. Use the **Sample Interval** drag slider to adjust monitoring frequency (50ms-10s).

### Events Tab
See all detected flight events in real-time. Filter by event type or vehicle. Events are color-coded:
- Green: Liftoff, Stable Orbit Achieved
- Yellow: Atmosphere transitions, SOI changes, Orbit Escaped
- Blue: Landing, Splashdown

### Missions Tab
Browse available missions (loaded from YAML files), select a vehicle, and activate missions. Completed missions are saved to the database.

## Mission YAML Format

Missions are defined as YAML files in the ``missions/`` subdirectory.

See ``missions/mission-schema.json`` for full schema with IDE autocompletion support.

### Condition Types

| Type | Description | Required Fields |
|------|-------------|-----------------|
| ``altitude_above`` | Baro altitude > value | ``value`` (m) |
| ``altitude_below`` | Baro altitude < value | ``value`` (m) |
| ``speed_above`` | Speed > value | ``value`` (m/s), ``speed_frame`` (orbital/surface/inertial) |
| ``speed_below`` | Speed < value | ``value`` (m/s), ``speed_frame`` |
| ``apoapsis_above`` | Apoapsis altitude > value | ``value`` (m) |
| ``periapsis_above`` | Periapsis altitude > value | ``value`` (m) |
| ``periapsis_below`` | Periapsis altitude < value | ``value`` (m) |
| ``eccentricity_below`` | Eccentricity < value | ``value`` |
| ``inclination_between`` | Inclination in range | ``min_value``, ``max_value`` (radians) |
| ``event_occurred`` | A flight event of this type fired | ``event_type`` |
| ``in_soi_of`` | Vehicle is in SOI of body | ``body_id`` |
| ``on_surface_of`` | Vehicle is landed on body | ``body_id`` |
| ``all_of`` | All sub-conditions must be met | ``sub_conditions`` |
| ``any_of`` | Any sub-condition must be met | ``sub_conditions`` |
| ``sequence`` | Sub-conditions must be met in order | ``sub_conditions`` |

## Data Location

All persistent data is stored in:
``%USERPROFILE%\Documents\My Games\Kitten Space Agency\.steely-eyed-missile-kitten\``

- ``events.db`` - SQLite database with flight events and mission progress
- ``missions/`` - User-defined mission YAML files (supplements bundled missions)

## Architecture

The mod is split into two projects:
- ``steely-eyed-missile-kitten`` - Entry point (StarMap lifecycle, ImGui window)
- ``steely-eyed-missile-kitten.lib`` - Headless library (all logic, reusable)

See ``steely-eyed-missile-kitten.lib/README.md`` for library architecture details.
