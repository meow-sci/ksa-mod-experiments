# steely-eyed-missile-kitten.lib

Headless library powering the Steely-Eyed Missile Kitten KSA mod. Contains all game logic separated from the UI entry point.

## Module Structure

### Telemetry/
Centralized KSA API access layer:
- **`VehicleTelemetry`** — Static class. ALL KSA game-state reads go here. Single file to update when KSA changes its API.
- **`TelemetrySnapshot`** — Immutable POCO capturing all vehicle metrics at a point in time.
- **`CoordinateFrames`** — `SpeedFrame` enum (Orbital/Surface/Inertial).

### Monitoring/
Accumulator-based telemetry sampling:
- **`MonitoringLoop`** — Calls `VehicleTelemetry.CaptureSnapshot` at a configurable interval for all vehicles. Call `Update(dt)` every frame.
- **`MonitoringConfig`** — Configurable sample interval (0.05s–10s, default 0.5s).
- **`VehicleMonitorState`** — Per-vehicle state (previous snapshot, event debounce timers).

### Events/
Flight event detection and pub/sub:
- **`EventDetector`** — Compares consecutive snapshots and emits `FlightEvent` objects.
- **`EventBus`** — Simple `Action<FlightEvent>` pub/sub.
- **`FlightEvent`** — Immutable event record with type, vehicle, timestamp, and details.
- **`FlightEventType`** — Enum: SoiChanged, Liftoff, Landed, SplashDown, AtmosphereEntered, AtmosphereExited, StableOrbitAchieved, OrbitEscaped.

### Persistence/
SQLite storage:
- **`EventDatabase`** — Schema creation, event insert/query, mission progress save/load using `Microsoft.Data.Sqlite`.
- **`EventWriter`** — Thread-safe EventBus subscriber that batches writes.
- **`DatabaseSchema`** — DDL constants with schema versioning.

### Missions/
YAML-based mission system:
- **`MissionLoader`** — Discovers and deserializes `.yaml` files using YamlDotNet with underscore naming convention.
- **`MissionDefinition`** — Top-level mission POCO (name, description, category, difficulty, objective condition).
- **`MissionCondition`** — Condition node tree (threshold, event, location, composite).
- **`MissionEvaluator`** — Stateless condition tree evaluator.
- **`MissionManager`** — Lifecycle: load definitions, activate/abandon missions, evaluate each tick, persist completion.
- **`MissionState`** — Runtime state (Active/Completed/Failed/Abandoned, timing, sequence progress).

### UI/
ImGui rendering components (static classes for embedding in a tabbed window):
- **`MonitorUI`** — Live vehicle telemetry table with interval config drag slider.
- **`EventFeedUI`** — Color-coded scrolling event log with type filter and auto-scroll.
- **`MissionUI`** — Mission browser, activation controls, and progress display.

## Usage

```csharp
// Initialize
var config = new MonitoringConfig();
var eventBus = new EventBus();
var loop = new MonitoringLoop(config, new EventDetector(), eventBus);

var db = new EventDatabase(dbPath);
db.Initialize();
var writer = new EventWriter(db, eventBus);

// Per-frame (from OnBeforeUi)
loop.Update(dt);
missionManager.EvaluateAll(loop.CurrentSnapshots);

// Periodic flush (every ~5s)
writer.Flush();

// Cleanup (on Unload)
writer.Dispose(); // flushes
db.Dispose();
```
